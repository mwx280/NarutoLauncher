// 渲染器（Renderer）—— 阶段 1 可行性验证宿主
//
// 最小 CEF 87 (x86) 宿主，用于验证：
//   1. CEF 87 + Flash PPAPI 34 能加载游戏页（SWF 渲染）
//   2. wmode=direct 的 GPU 加速是否正常
//   3. 有无 UA 检测 / 反调试拦截
//
// 双进程模式：同一 exe 既是 browser 宿主，也作为子进程
// （renderer / gpu / utility / ppapi 插件进程）运行。

#include "app.h"
#include "client.h"

#include "include/cef_app.h"
#include "include/cef_browser.h"
#include "include/cef_command_line.h"
#include "include/cef_cookie.h"
#include "include/cef_request_context.h"
#include "include/cef_values.h"
#include "include/internal/cef_win.h"

#include <windows.h>
#include <string>

namespace {

// 浏览器进程参数
const char kDefaultUrl[] = "https://www.baidu.com";
const int kWindowWidth = 1280;
const int kWindowHeight = 800;

// 保存主窗口句柄
HWND g_main_window = nullptr;
// 浏览器客户端
CefRefPtr<NarutoClient> g_client;

// 解析用户数据目录（cookie 持久化用）。
// 使用宽字符 API 解析 exe 路径并拼接子目录，避免中文安装路径的编码问题。
std::wstring ResolveUserDataDir() {
    wchar_t exe_path[MAX_PATH] = {0};
    DWORD n = ::GetModuleFileNameW(nullptr, exe_path, MAX_PATH);
    std::wstring dir;
    if (n > 0 && n < MAX_PATH) {
        dir.assign(exe_path, n);
        size_t sep = dir.find_last_of(L"\\/");
        if (sep != std::wstring::npos)
            dir = dir.substr(0, sep + 1);
        dir += L"userdata";
    } else {
        dir = L"userdata";
    }
    // 确保目录存在（CEF 要求缓存目录存在，否则不持久化）
    ::CreateDirectoryW(dir.c_str(), nullptr);
    return dir;
}

// 注册 Windows 窗口类
bool RegisterWindowClass(const wchar_t* class_name,
                         WNDPROC wndproc) {
    WNDCLASSEX wc = {};
    wc.cbSize = sizeof(WNDCLASSEX);
    wc.style = CS_HREDRAW | CS_VREDRAW;
    wc.lpfnWndProc = wndproc;
    wc.hInstance = GetModuleHandle(nullptr);
    wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
    wc.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW);
    wc.lpszClassName = class_name;
    return RegisterClassEx(&wc) != 0;
}

// 窗口过程
LRESULT CALLBACK WindowProc(HWND hwnd, UINT message,
                            WPARAM wparam, LPARAM lparam) {
    switch (message) {
        case WM_CREATE:
            g_main_window = hwnd;
            return 0;
        case WM_DESTROY:
            PostQuitMessage(0);
            return 0;
        default:
            return DefWindowProc(hwnd, message, wparam, lparam);
    }
}

// 创建浏览器（嵌入到主窗口客户区）
void CreateBrowser(HWND parent, const std::string& url,
                   CefRefPtr<NarutoClient> client) {
    CefWindowInfo window_info;
    RECT rect;
    GetClientRect(parent, &rect);
    window_info.SetAsChild(parent, rect);

    CefBrowserSettings settings;
    // 启用插件（含 Flash），去除"右键点击运行 Flash"的 click-to-play 占位
    settings.plugins = STATE_ENABLED;

    // 创建带请求上下文的浏览器，以便通过 OnBeforePluginLoad 允许 Flash 自动运行。
    // 使用独立（隔离）上下文；设置 cache_path 使 cookie 持久化（登录态跨启动）。
    CefRequestContextSettings ctx_settings;
    // cache_path 是原始 cef_string_t，需用 CefString 包装后填充
    CefString(&ctx_settings.cache_path).FromWString(ResolveUserDataDir());
    // 会话级 cookie（如 skey/p_skey）也写入磁盘，避免每次重启重新登录
    ctx_settings.persist_session_cookies = true;
    CefRefPtr<CefRequestContext> request_context =
        CefRequestContext::CreateContext(ctx_settings, client.get());

    // 关键 workaround：将插件的 content setting 设为 ALLOW(1)。
    // CEF 87 中 Flash 的 click-to-play 占位根因是
    // profile.default_content_setting_values.plugins 默认不是 ALLOW，
    // 且其优先级高于 OnBeforePluginLoad 返回的 PLUGIN_POLICY_ALLOW。
    // 设置该偏好后 Flash 应自动运行（CEF issue #2768 官方 workaround）。
    {
        CefRefPtr<CefValue> plugins_value = CefValue::Create();
        plugins_value->SetInt(1);  // 1 = CONTENT_SETTING_ALLOW
        CefString error;
        bool ok = request_context->SetPreference(
            "profile.default_content_setting_values.plugins",
            plugins_value, error);
        // 注：CEF 87 中即使此设置成功（ok=true），Flash 仍可能被 click-to-play
        // 拦截，需用户右键选择"运行此插件"后才会真正加载。
        (void)ok;
        (void)error;
    }

    CefString url_str(url);
    CefBrowserHost::CreateBrowser(window_info, client, url_str,
                                  settings, nullptr, request_context);
}

// 浏览器进程入口
int RunBrowserProcess(const CefMainArgs& main_args,
                      CefRefPtr<NarutoApp> app,
                      CefSettings& settings) {
    // 初始化 CEF
    if (!CefInitialize(main_args, settings, app.get(), nullptr)) {
        MessageBox(nullptr, L"CEF 初始化失败", L"错误", MB_OK | MB_ICONERROR);
        return 1;
    }

    // 创建主窗口
    const wchar_t kClassName[] = L"NarutoRendererWindow";
    RegisterWindowClass(kClassName, WindowProc);

    HWND hwnd = CreateWindowEx(
        0, kClassName, L"火影忍者OL 渲染器 (阶段1验证)",
        WS_OVERLAPPEDWINDOW, CW_USEDEFAULT, CW_USEDEFAULT,
        kWindowWidth, kWindowHeight, nullptr, nullptr,
        GetModuleHandle(nullptr), nullptr);
    if (!hwnd) {
        MessageBox(nullptr, L"创建主窗口失败", L"错误", MB_OK | MB_ICONERROR);
        CefShutdown();
        return 1;
    }

    // 从命令行读取要加载的 URL，默认百度
    std::string url = kDefaultUrl;
    CefRefPtr<CefCommandLine> cmd = CefCommandLine::GetGlobalCommandLine();
    if (cmd->HasSwitch("url")) {
        url = cmd->GetSwitchValue("url").ToString();
    }

    g_client = new NarutoClient();
    CreateBrowser(hwnd, url, g_client);

    ShowWindow(hwnd, SW_SHOW);
    UpdateWindow(hwnd);

    // 运行消息循环（非多线程模式，用 CefRunMessageLoop）
    CefRunMessageLoop();

    // 关闭并清理
    g_client->CloseAllBrowsers(false);
    // 强制 cookie 落盘（含 session cookie，如 QQ 登录态 skey/p_skey），
    // 保证免登录跨启动生效
    CefRefPtr<CefCookieManager> cookie_mgr =
        CefCookieManager::GetGlobalManager(nullptr);
    if (cookie_mgr) {
        cookie_mgr->FlushStore(nullptr);
    }
    CefShutdown();
    return 0;
}

}  // namespace

int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE, wchar_t*, int) {
    (void)hInstance;

    CefMainArgs main_args(GetModuleHandle(nullptr));

    CefRefPtr<NarutoApp> app = new NarutoApp();

    // 若为子进程，则走 CefExecuteProcess 后直接退出
    int exit_code = CefExecuteProcess(main_args, app.get(), nullptr);
    if (exit_code >= 0) {
        return exit_code;
    }

    CefSettings settings;
    // 不启用沙箱（Flash 插件 + 简单宿主，降低调试复杂度）
    settings.no_sandbox = true;
    settings.log_severity = LOGSEVERITY_WARNING;
    // 开启远程调试端口，便于验证 Flash 是否真正加载
    settings.remote_debugging_port = 9222;
    // 全局 cache_path：cookie 持久化依赖全局路径，
    // 自定义 context 的 cache_path 需与全局一致才能继承 persist_session_cookies
    CefString(&settings.cache_path).FromWString(ResolveUserDataDir());
    settings.persist_session_cookies = true;

    return RunBrowserProcess(main_args, app, settings);
}
