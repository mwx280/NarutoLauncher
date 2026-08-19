// CEF 单宿主 —— 主入口
//
// 架构：一个 CEF 87 x86 进程同时承载：
//   - UI 窗口：加载本地 HTTP 服务的 Vue 构建产物（启动器界面）
//   - 游戏窗口：加载 Flash 游戏（huoying.qq.com）
//
// 无边框窗口由 FramelessWindow（Win32 原生）实现，标题栏拖拽/边缘缩放
// 走 WM_NCHITTEST；HTML 界面通过 JS 桥调用窗口控制。

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#include <shellapi.h>

#include <string>
#include <vector>

#include "include/cef_app.h"
#include "include/cef_browser.h"
#include "include/cef_command_line.h"
#include "include/cef_request_context_handler.h"
#include "include/cef_web_plugin.h"
#include "include/wrapper/cef_message_router.h"
#include "include/internal/cef_win.h"

#include "frameless_window.h"
#include "http_server.h"
#include "app_log.h"

// ---------- 常量 ----------
namespace {
const char* kFlashVersion = "34.0.0.380";
}  // namespace

// ---------- 应用级 CefApp（Flash 注册 + 渲染进程 JS 桥） ----------
class HostApp : public CefApp,
                public CefBrowserProcessHandler,
                public CefRenderProcessHandler {
public:
    // Flash 插件绝对路径（宽字符解析，避免中文路径编码问题）。
    static std::string FlashPluginPath() {
        wchar_t exe[MAX_PATH] = {0};
        DWORD n = ::GetModuleFileNameW(nullptr, exe, MAX_PATH);
        if (n == 0 || n >= MAX_PATH)
            return "pepflashplayer.dll";
        std::wstring path(exe, n);
        size_t sep = path.find_last_of(L"\\/");
        if (sep != std::wstring::npos)
            path = path.substr(0, sep + 1);
        path += L"pepflashplayer.dll";
        int len = ::WideCharToMultiByte(CP_UTF8, 0, path.c_str(),
                                        static_cast<int>(path.size()),
                                        nullptr, 0, nullptr, nullptr);
        if (len <= 0)
            return "pepflashplayer.dll";
        std::vector<char> buf(len);
        ::WideCharToMultiByte(CP_UTF8, 0, path.c_str(),
                              static_cast<int>(path.size()),
                              buf.data(), len, nullptr, nullptr);
        return std::string(buf.data(), len);
    }

    void OnBeforeCommandLineProcessing(
        const CefString& process_type,
        CefRefPtr<CefCommandLine> command_line) override {
        command_line->AppendSwitchWithValue("ppapi-flash-path",
                                            FlashPluginPath());
        command_line->AppendSwitchWithValue("ppapi-flash-version",
                                            kFlashVersion);
        command_line->AppendSwitch("enable-gpu");
        command_line->AppendSwitch("persist-session-cookies");
        command_line->AppendSwitch("mute-audio");  // Flash 音频崩溃 workaround
        // UI 本地 HTTP 加载，无需 web security 放宽；保留日志便于排查
        command_line->AppendSwitch("enable-logging");
    }

    CefRefPtr<CefBrowserProcessHandler> GetBrowserProcessHandler() override {
        return this;
    }

    CefRefPtr<CefRenderProcessHandler> GetRenderProcessHandler() override {
        return this;
    }

    // ---- 渲染进程：注入 nativeQuery JS 桥 ----
    void OnContextCreated(CefRefPtr<CefBrowser> browser, CefRefPtr<CefFrame> frame,
                          CefRefPtr<CefV8Context> context) override {
        if (!renderer_router_)
            renderer_router_ = CefMessageRouterRendererSide::Create(config_);
        renderer_router_->OnContextCreated(browser, frame, context);
    }

    void OnContextReleased(CefRefPtr<CefBrowser> browser, CefRefPtr<CefFrame> frame,
                           CefRefPtr<CefV8Context> context) override {
        if (renderer_router_)
            renderer_router_->OnContextReleased(browser, frame, context);
    }

    bool OnProcessMessageReceived(CefRefPtr<CefBrowser> browser,
                                  CefRefPtr<CefFrame> frame,
                                  CefProcessId source_process,
                                  CefRefPtr<CefProcessMessage> message) override {
        if (renderer_router_ &&
            renderer_router_->OnProcessMessageReceived(browser, frame,
                                                       source_process, message))
            return true;
        return false;
    }

    // 与浏览器进程保持一致的路由配置（构造函数统一初始化，跨进程一致）
    static const CefMessageRouterConfig& RouterConfig() { return config_; }

private:
    static CefMessageRouterConfig config_;
    CefRefPtr<CefMessageRouterRendererSide> renderer_router_;
    IMPLEMENT_REFCOUNTING(HostApp);
};

// 静态成员定义：构造函数里统一设置函数名，浏览器/渲染进程都会初始化。
CefMessageRouterConfig HostApp::config_ = [] {
    CefMessageRouterConfig c;
    c.js_query_function = "nativeQuery";
    c.js_cancel_function = "nativeQueryCancel";
    return c;
}();

// ---------- 全局状态 ----------
FramelessWindow g_window;
StaticHttpServer g_http;
CefRefPtr<CefBrowser> g_ui_browser;
CefRefPtr<CefBrowser> g_game_browser;
HWND g_game_hwnd = nullptr;   // 游戏窗口句柄（从 CEF 回调获取）
bool g_game_created = false;  // 游戏窗口是否已创建（用于嵌入）
CefRefPtr<CefMessageRouterBrowserSide> g_router;

// 把 CEF 子窗口嵌入主窗口客户区（由 WM_SIZE 同步尺寸）。
void EmbedChild(HWND child) {
    g_window.SetClientChild(child);
}

// ---------- 浏览器客户端 ----------
class HostClient : public CefClient,
                   public CefLifeSpanHandler,
                   public CefRequestContextHandler,
                   public CefRequestHandler,
                   public CefMessageRouterBrowserSide::Handler {
public:
    explicit HostClient(bool is_game) : is_game_(is_game) {}

    CefRefPtr<CefLifeSpanHandler> GetLifeSpanHandler() override { return this; }
    CefRefPtr<CefRequestHandler> GetRequestHandler() override { return this; }

    bool OnBeforePluginLoad(const CefString& mime_type,
                            const CefString& plugin_url,
                            bool is_main_frame,
                            const CefString& top_origin_url,
                            CefRefPtr<CefWebPluginInfo> plugin_info,
                            PluginPolicy* plugin_policy) override {
        if (mime_type == "application/x-shockwave-flash") {
            *plugin_policy = PLUGIN_POLICY_ALLOW;
            return true;
        }
        return false;
    }

    // ---- CefRequestHandler：转发给消息路由器（页面导航/渲染进程退出清理） ----
    bool OnBeforeBrowse(CefRefPtr<CefBrowser> browser, CefRefPtr<CefFrame> frame,
                        CefRefPtr<CefRequest> request, bool user_gesture,
                        bool is_redirect) override {
        if (g_router) {
            g_router->OnBeforeBrowse(browser, frame);
        }
        return false;
    }

    void OnRenderProcessTerminated(CefRefPtr<CefBrowser> browser,
                                   TerminationStatus status) override {
        if (g_router) {
            g_router->OnRenderProcessTerminated(browser);
        }
    }

    // ---- CefClient：转发渲染进程消息给消息路由器 ----
    bool OnProcessMessageReceived(CefRefPtr<CefBrowser> browser,
                                  CefRefPtr<CefFrame> frame,
                                  CefProcessId source_process,
                                  CefRefPtr<CefProcessMessage> message) override {
        if (g_router &&
            g_router->OnProcessMessageReceived(browser, frame,
                                               source_process, message))
            return true;
        return false;
    }

    // ---- CefMessageRouterBrowserSide::Handler：处理 JS 查询 ----
    bool OnQuery(CefRefPtr<CefBrowser> browser, CefRefPtr<CefFrame> frame,
                 int64 query_id, const CefString& request, bool persistent,
                 CefRefPtr<Callback> callback) override {
        // 只有 UI 浏览器接收 JS 桥调用
        if (is_game_)
            return false;
        const std::string req = request.ToString();
        AppLog::Write("JS 桥调用: %s", req.c_str());

        std::string result;
        if (req == "win.min") {
            g_window.Minimize();
            result = "ok";
        } else if (req == "win.max") {
            g_window.ToggleMaximize();
            result = "ok";
        } else if (req == "win.full") {
            g_window.ToggleFullscreen();
            result = "ok";
        } else if (req == "win.close") {
            g_window.Close();
            result = "ok";
        } else if (req == "win.isMax") {
            result = g_window.IsWindowMaximized() ? "true" : "false";
        } else if (req == "win.isFull") {
            result = g_window.IsWindowFullscreen() ? "true" : "false";
        } else if (req == "app.version") {
            result = "0.1.0";
        } else {
            result = "unknown:" + req;
        }
        callback->Success(result);
        return true;
    }

    void OnQueryCanceled(CefRefPtr<CefBrowser> browser, CefRefPtr<CefFrame> frame,
                         int64 query_id) override {
        // UI 浏览器查询取消时无需处理
        (void)browser; (void)frame; (void)query_id;
    }

    void OnAfterCreated(CefRefPtr<CefBrowser> browser) override {
        if (is_game_) {
            g_game_browser = browser;
            // 游戏窗口创建后嵌入主窗口
            HWND game_hwnd = browser->GetHost()->GetWindowHandle();
            if (game_hwnd) {
                g_game_hwnd = game_hwnd;
                EmbedChild(game_hwnd);
            }
        } else {
            g_ui_browser = browser;
        }
    }

    bool DoClose(CefRefPtr<CefBrowser> browser) override {
        return false;
    }

    void OnBeforeClose(CefRefPtr<CefBrowser> browser) override {
        if (is_game_)
            g_game_browser = nullptr;
        else
            g_ui_browser = nullptr;
    }

private:
    bool is_game_;
    IMPLEMENT_REFCOUNTING(HostClient);
};

// ---------- 创建浏览器 ----------
void CreateBrowserWindow(CefRefPtr<CefClient> client, const std::string& url,
                         bool is_game) {
    CefWindowInfo info;
    // 游戏窗口：直接嵌入主窗口客户区
    // UI 窗口：也嵌入主窗口（同一客户区，通过 JS 桥切换可见性）
    int w, h;
    g_window.ClientSize(w, h);
    if (w <= 0) w = 1280;
    if (h <= 0) h = 800;
    RECT rc = {0, 0, w, h};
    info.SetAsChild(g_window.Handle(), rc);

    CefBrowserSettings settings;
    settings.plugins = STATE_ENABLED;

    CefRefPtr<CefRequestContext> ctx;
    if (is_game) {
        // 游戏用持久化上下文（cookie 免登录）
        CefRequestContextSettings ctx_settings;
        wchar_t exe[MAX_PATH] = {0};
        DWORD n = ::GetModuleFileNameW(nullptr, exe, MAX_PATH);
        std::wstring dir;
        if (n > 0 && n < MAX_PATH) {
            dir.assign(exe, n);
            size_t sep = dir.find_last_of(L"\\/");
            if (sep != std::wstring::npos)
                dir = dir.substr(0, sep + 1);
        }
        dir += L"userdata";
        ::CreateDirectoryW(dir.c_str(), nullptr);
        CefString(&ctx_settings.cache_path).FromWString(dir);
        ctx_settings.persist_session_cookies = true;
        ctx = CefRequestContext::CreateContext(ctx_settings, nullptr);
    }

    CefString url_str(url);
    CefBrowserHost::CreateBrowser(info, client, url_str, settings,
                                  nullptr, ctx);
}

// ---------- 主入口 ----------
int RunBrowserProcess() {
    AppLog::Write("== RunBrowserProcess 开始 ==");

    // 初始化 HTTP 服务器（服务 Vue dist 目录）
    // dist 目录 = exe 同级的 ui/ 目录
    wchar_t exe[MAX_PATH] = {0};
    DWORD n = ::GetModuleFileNameW(nullptr, exe, MAX_PATH);
    std::wstring dir;
    if (n > 0 && n < MAX_PATH) {
        dir.assign(exe, n);
        size_t sep = dir.find_last_of(L"\\/");
        if (sep != std::wstring::npos)
            dir = dir.substr(0, sep + 1);
    }
    // exe 旁的 ui/dist
    std::wstring dist = dir + L"ui\\dist";
    int len = ::WideCharToMultiByte(CP_UTF8, 0, dist.c_str(),
                                    static_cast<int>(dist.size()),
                                    nullptr, 0, nullptr, nullptr);
    std::string dist_utf8;
    if (len > 0) {
        std::vector<char> buf(len);
        ::WideCharToMultiByte(CP_UTF8, 0, dist.c_str(),
                              static_cast<int>(dist.size()),
                              buf.data(), len, nullptr, nullptr);
        dist_utf8.assign(buf.data(), len);
    }
    g_http.SetRoot(dist_utf8);
    AppLog::Write("HTTP 根目录: %s", dist_utf8.c_str());
    unsigned short port = g_http.Start();
    AppLog::Write("HTTP 服务器端口: %u (%s)", port,
                  port == 0 ? "失败" : "成功");
    if (port == 0) {
        MessageBox(nullptr, L"HTTP 服务器启动失败", L"错误", MB_OK);
        CefShutdown();
        return 1;
    }

    // 主窗口（在 CefInitialize 之前创建，规避 CEF 环境对窗口创建的影响）
    AppLog::Write("创建主窗口...");
    if (!g_window.Create(1280, 800)) {
        DWORD err = GetLastError();
        AppLog::Write("创建窗口失败, GetLastError=%lu", err);
        wchar_t msg[256];
        swprintf_s(msg, L"创建窗口失败 (错误码 %lu)", err);
        MessageBox(nullptr, msg, L"错误", MB_OK);
        g_http.Stop();
        return 1;
    }
    AppLog::Write("主窗口创建成功, HWND=%p", g_window.Handle());

    // 窗口创建成功后再初始化 CEF（浏览器进程）。
    CefMainArgs main_args(GetModuleHandle(nullptr));
    CefSettings settings;
    settings.no_sandbox = true;
    settings.log_severity = LOGSEVERITY_WARNING;
    CefRefPtr<HostApp> app = new HostApp();
    bool ok_init = CefInitialize(main_args, settings, app.get(), nullptr);
    AppLog::Write("CefInitialize 结果: %s", ok_init ? "成功" : "失败");
    if (!ok_init) {
        MessageBox(nullptr, L"CEF 初始化失败", L"错误", MB_OK);
        g_http.Stop();
        return 1;
    }

    // 创建 UI 浏览器（加载本地 HTTP）
    CefRefPtr<HostClient> ui_client = new HostClient(false);
    std::string ui_url = "http://127.0.0.1:" +
                         std::to_string(port) + "/index.html";
    AppLog::Write("创建 UI 浏览器: %s", ui_url.c_str());

    // 创建 JS 桥（CefMessageRouterBrowserSide）。配置必须与渲染进程一致。
    {
        CefMessageRouterConfig config = HostApp::RouterConfig();
        g_router = CefMessageRouterBrowserSide::Create(config);
        // UI 客户端作为 handler（OnQuery 处理窗口控制）
        g_router->AddHandler(ui_client.get(), true);
    }

    CreateBrowserWindow(ui_client, ui_url, false);

    // 创建游戏浏览器（加载 Flash 游戏；后续由 JS 桥触发，先占位）
    CefRefPtr<HostClient> game_client = new HostClient(true);
    AppLog::Write("创建游戏浏览器（占位，暂不加载）");

    // CEF 消息循环
    AppLog::Write("进入 CefRunMessageLoop");
    CefRunMessageLoop();

    // 清理
    AppLog::Write("消息循环退出，清理中");
    g_http.Stop();
    CefShutdown();
    return 0;
}

int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE, wchar_t*, int) {
    (void)hInstance;

    // 文本模糊修复：声明 Per-Monitor DPI 感知，避免系统缩放导致的字体模糊。
    {
        // 优先用 Shcore.dll 的 SetProcessDpiAwareness（Win10+）
        typedef HRESULT(WINAPI* SetProcessDpiAwarenessFn)(int);
        HMODULE shcore = LoadLibraryW(L"shcore.dll");
        if (shcore) {
            auto fn = reinterpret_cast<SetProcessDpiAwarenessFn>(
                GetProcAddress(shcore, "SetProcessDpiAwareness"));
            if (fn)
                fn(2);  // PROCESS_PER_MONITOR_DPI_AWARE
            FreeLibrary(shcore);
        } else {
            // 旧系统 fallback
            SetProcessDPIAware();
        }
    }

    CefMainArgs main_args(GetModuleHandle(nullptr));
    CefRefPtr<HostApp> app = new HostApp();

    int exit_code = CefExecuteProcess(main_args, app.get(), nullptr);
    if (exit_code >= 0)
        return exit_code;

    // 仅浏览器进程初始化日志（子进程会走 CefExecuteProcess 提前返回）
    AppLog::Init();
    AppLog::Write("== 浏览器进程入口 ==");
    return RunBrowserProcess();
}
