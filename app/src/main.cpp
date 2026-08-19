// GameHost —— 独立游戏窗口宿主
//
// 由启动器（WinUI 3）拉起，每账号一个实例。
// 职责：创建无边框窗口，用 CEF 87 x86 加载 Flash 游戏。
//
// 命令行参数：
//   --url=<game_url>      游戏入口 URL（默认 game.huoying.qq.com/main.html）
//   --userdata=<dir>      独立缓存目录（多开隔离 cookie）
//   --title=<title>       窗口标题（默认"火影忍者OL"）

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
#include "include/internal/cef_win.h"

#include "frameless_window.h"
#include "app_log.h"

// ---------- 常量 ----------
namespace {
const char* kFlashVersion = "34.0.0.380";
const wchar_t* kDefaultUrl = L"https://game.huoying.qq.com/main.html";
}  // namespace

// ---------- 应用级 CefApp（Flash 注册） ----------
class HostApp : public CefApp,
                public CefBrowserProcessHandler {
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
        command_line->AppendSwitch("enable-logging");
    }

    CefRefPtr<CefBrowserProcessHandler> GetBrowserProcessHandler() override {
        return this;
    }

private:
    IMPLEMENT_REFCOUNTING(HostApp);
};

// ---------- 全局状态 ----------
FramelessWindow g_window;
CefRefPtr<CefBrowser> g_game_browser;
HWND g_game_hwnd = nullptr;   // 游戏窗口句柄（从 CEF 回调获取）
std::wstring g_window_title = L"火影忍者OL";

// 把 CEF 子窗口嵌入主窗口客户区（由 WM_SIZE 同步尺寸）。
void EmbedChild(HWND child) {
    g_window.SetClientChild(child);
}

// ---------- 浏览器客户端 ----------
class HostClient : public CefClient,
                   public CefLifeSpanHandler,
                   public CefRequestContextHandler,
                   public CefRequestHandler {
public:
    HostClient() = default;

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

    void OnAfterCreated(CefRefPtr<CefBrowser> browser) override {
        g_game_browser = browser;
        HWND game_hwnd = browser->GetHost()->GetWindowHandle();
        if (game_hwnd) {
            g_game_hwnd = game_hwnd;
            EmbedChild(game_hwnd);
        }
    }

    bool DoClose(CefRefPtr<CefBrowser> browser) override {
        return false;
    }

    void OnBeforeClose(CefRefPtr<CefBrowser> browser) override {
        g_game_browser = nullptr;
    }

private:
    IMPLEMENT_REFCOUNTING(HostClient);
};

// ---------- 主入口 ----------
int RunBrowserProcess(const std::wstring& url,
                      const std::wstring& userdata_dir,
                      const std::wstring& title) {
    AppLog::Write("== GameHost 开始 ==");
    if (!title.empty())
        g_window_title = title;

    // 主窗口（在 CefInitialize 之前创建，规避 CEF 环境对窗口创建的影响）
    AppLog::Write("创建主窗口...");
    if (!g_window.Create(1280, 800)) {
        DWORD err = GetLastError();
        AppLog::Write("创建窗口失败, GetLastError=%lu", err);
        return 1;
    }
    ::SetWindowTextW(g_window.Handle(), g_window_title.c_str());
    AppLog::Write("主窗口创建成功, HWND=%p", g_window.Handle());

    // 初始化 CEF（浏览器进程）
    CefMainArgs main_args(GetModuleHandle(nullptr));
    CefSettings settings;
    settings.no_sandbox = true;
    settings.log_severity = LOGSEVERITY_WARNING;
    if (!userdata_dir.empty()) {
        CefString(&settings.cache_path).FromWString(userdata_dir);
        settings.persist_session_cookies = true;
    }
    CefRefPtr<HostApp> app = new HostApp();
    bool ok_init = CefInitialize(main_args, settings, app.get(), nullptr);
    AppLog::Write("CefInitialize 结果: %s", ok_init ? "成功" : "失败");
    if (!ok_init) {
        MessageBox(nullptr, L"CEF 初始化失败", L"错误", MB_OK);
        return 1;
    }

    // 创建游戏浏览器
    CefRefPtr<HostClient> game_client = new HostClient();
    CefWindowInfo info;
    int w, h;
    g_window.ClientSize(w, h);
    if (w <= 0) w = 1280;
    if (h <= 0) h = 800;
    RECT rc = {0, 0, w, h};
    info.SetAsChild(g_window.Handle(), rc);

    CefBrowserSettings settings2;
    settings2.plugins = STATE_ENABLED;

    CefString url_str(url);
    CefBrowserHost::CreateBrowser(info, game_client, url_str, settings2,
                                  nullptr, nullptr);
    AppLog::Write("创建游戏浏览器: %S", url.c_str());

    // CEF 消息循环
    AppLog::Write("进入 CefRunMessageLoop");
    CefRunMessageLoop();

    // 清理
    AppLog::Write("消息循环退出，清理中");
    CefShutdown();
    return 0;
}

int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE, wchar_t* lpCmdLine, int) {
    (void)hInstance;

    // DPI 感知（文本模糊修复）
    {
        typedef HRESULT(WINAPI* SetProcessDpiAwarenessFn)(int);
        HMODULE shcore = LoadLibraryW(L"shcore.dll");
        if (shcore) {
            auto fn = reinterpret_cast<SetProcessDpiAwarenessFn>(
                GetProcAddress(shcore, "SetProcessDpiAwareness"));
            if (fn)
                fn(2);  // PROCESS_PER_MONITOR_DPI_AWARE
            FreeLibrary(shcore);
        } else {
            SetProcessDPIAware();
        }
    }

    // 解析命令行参数
    std::wstring url = kDefaultUrl;
    std::wstring userdata;
    std::wstring title;
    {
        int argc = 0;
        wchar_t** argv = CommandLineToArgvW(lpCmdLine, &argc);
        for (int i = 0; i < argc; ++i) {
            std::wstring arg = argv[i];
            auto val = [&arg](const wchar_t* key) -> std::wstring {
                std::wstring prefix = std::wstring(L"--") + key + L"=";
                if (arg.rfind(prefix, 0) == 0)
                    return arg.substr(prefix.size());
                return L"";
            };
            auto u = val(L"url");
            if (!u.empty()) url = u;
            auto d = val(L"userdata");
            if (!d.empty()) userdata = d;
            auto t = val(L"title");
            if (!t.empty()) title = t;
        }
        LocalFree(argv);
    }

    CefMainArgs main_args(GetModuleHandle(nullptr));
    CefRefPtr<HostApp> app = new HostApp();

    int exit_code = CefExecuteProcess(main_args, app.get(), nullptr);
    if (exit_code >= 0)
        return exit_code;

    // 仅浏览器进程初始化日志（子进程会走 CefExecuteProcess 提前返回）
    AppLog::Init();
    AppLog::Write("== GameHost 入口 ==");
    AppLog::Write("URL=%S userdata=%S", url.c_str(), userdata.c_str());
    return RunBrowserProcess(url, userdata, title);
}
