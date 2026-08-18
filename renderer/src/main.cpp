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
void CreateBrowser(HWND parent, const std::string& url) {
    CefWindowInfo window_info;
    RECT rect;
    GetClientRect(parent, &rect);
    window_info.SetAsChild(parent, rect);

    CefBrowserSettings settings;

    CefString url_str(url);
    CefBrowserHost::CreateBrowser(window_info, g_client, url_str,
                                  settings, nullptr, nullptr);
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
    CreateBrowser(hwnd, url);

    ShowWindow(hwnd, SW_SHOW);
    UpdateWindow(hwnd);

    // 运行消息循环（非多线程模式，用 CefRunMessageLoop）
    CefRunMessageLoop();

    // 关闭并清理
    g_client->CloseAllBrowsers(false);
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

    return RunBrowserProcess(main_args, app, settings);
}
