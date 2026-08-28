// CEFFlashGameHost（火影版）—— 独立游戏窗口宿主
//
// 由 WPF 启动器拉起，每账号一个实例。
// 职责：创建无边框窗口，用 CEF 87 x64 加载 Flash 游戏。
//
// 本文件是主入口：命令行解析、双击误启动防护、CEF 初始化、消息循环。
// 应用级 CefApp（Flash 注册）见 host_app，浏览器客户端（生命周期/登录/画质）
// 见 host_client，参数解析见 params。
//
// 命令行参数：
//   --url=<game_url>      游戏入口 URL（默认 game.huoying.qq.com/main.html）
//   --userdata=<dir>      独立缓存目录（多开隔离 cookie）
//   --title=<title>       窗口标题（默认"火影忍者Online"）
//   --embed               以内嵌子窗口运行（供启动器 SetParent）
//   --windowed            以独立有边框窗口运行（调试 / 独立会话）
//   --login               扫码登录模式（加载 QQ 登录页，登录成功写 login_result.txt）
//   --cookie=<b64>        启动时注入的 cookie（base64 编码的 JSON）
//   --user=<b64>          QQ 号（账号密码自动登录，base64）
//   --pass=<b64>          密码（账号密码自动登录，base64）
//   --flash-gpu=1         开启 Flash 硬件加速（默认关闭）
//   --flash-quality=<low/medium/high>  Flash 渲染画质（默认 low）
//   --force-dpr=0         关闭强制 DPR=1（画质优先）
//   --debug-port=<port>   启用 CEF DevTools 远程调试

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#include <shellapi.h>

#include <string>

#include "include/cef_app.h"

#include "frameless_window.h"
#include "app_log.h"
#include "no_console_hook.h"
#include "flash_hook.h"
#include "speed_hook.h"
#include "globals.h"
#include "params.h"
#include "host_app.h"
#include "host_client.h"

// ---------- 常量定义 ----------
const char* kFlashVersion = "34.0.0.380";
const wchar_t* kDefaultUrl = L"https://game.huoying.qq.com/main.html";
const int kDefaultWidth = 1280;
const int kDefaultHeight = 800;

// ---------- 渲染 / 宿主配置定义 ----------
bool g_flash_gpu = false;
bool g_force_dpr = true;
int g_debug_port = 0;
std::string g_flash_quality = "low";

// ---------- 运行期全局状态定义 ----------
FramelessWindow g_window;
CefRefPtr<CefBrowser> g_game_browser;
bool g_muted = false;
double g_speed = 1.0;
HWND g_game_hwnd = nullptr;
std::wstring g_window_title = L"火影忍者Online";
bool g_login_mode = false;
bool g_auto_login = false;
std::string g_auto_user_b64;
std::string g_auto_pass_b64;
std::wstring g_userdata_dir;
std::string g_cookie_json;
HWND g_parent_hwnd = nullptr;

// ---------- 浏览器进程 ----------
int RunBrowserProcess(const NarutoRunOptions& opt) {
    AppLog::Write("== CEFFlashGameHost 开始 ==");
    if (!opt.title.empty())
        g_window_title = opt.title;

    // 主窗口（在 CefInitialize 之前创建，规避 CEF 环境对窗口创建的影响）
    AppLog::Write("创建主窗口...");
    if (!g_window.Create(kDefaultWidth, kDefaultHeight, opt.embed, opt.parent, opt.windowed)) {
        DWORD err = GetLastError();
        AppLog::Write("创建窗口失败, GetLastError=%lu", err);
        return 1;
    }
    // 嵌入模式：把主窗口 HWND 写入 userdata 目录，供启动器桥接读取
    if (opt.embed && !opt.userdata.empty()) {
        // userdata 目录可能尚不存在，先建目录
        ::CreateDirectoryW(opt.userdata.c_str(), nullptr);
        std::wstring hwnd_file = opt.userdata + L"\\window_hwnd.txt";
        // 先删除上次运行遗留的过期句柄文件，避免启动器读到旧句柄
        ::DeleteFileW(hwnd_file.c_str());
        FILE* f = nullptr;
        if (_wfopen_s(&f, hwnd_file.c_str(), L"w") == 0 && f) {
            fprintf(f, "%llu", (unsigned long long)g_window.Handle());
            fclose(f);
        }
    }
    ::SetWindowTextW(g_window.Handle(), g_window_title.c_str());
    AppLog::Write("主窗口创建成功, HWND=%p", g_window.Handle());
    g_window.SetCloseHandler(&OnMainWindowClose);
    g_window.SetCommandHandler(&OnWindowCommand);

    // 初始化 CEF（浏览器进程）
    CefMainArgs main_args(GetModuleHandle(nullptr));
    CefSettings settings;
    settings.no_sandbox = true;
    settings.log_severity = LOGSEVERITY_WARNING;
    if (!opt.userdata.empty()) {
        CefString(&settings.cache_path).FromWString(opt.userdata);
        settings.persist_session_cookies = true;
    }
    CefRefPtr<HostApp> app = new HostApp();
    bool ok_init = CefInitialize(main_args, settings, app.get(), nullptr);
    AppLog::Write("CefInitialize 结果: %s", ok_init ? "成功" : "失败");
    if (!ok_init) {
        MessageBox(nullptr, L"CEF 初始化失败", L"错误", MB_OK);
        return 1;
    }

    // 隐藏控制台窗口（CEF 可能为浏览器进程创建 console）
    {
        HWND console = ::GetConsoleWindow();
        if (console)
            ::ShowWindow(console, SW_HIDE);
    }

    // 启动时注入 cookie（免登录进游戏）
    if (!g_cookie_json.empty()) {
        InjectCookies(Base64Decode(g_cookie_json));
        // 等待 cookie 写入完成（异步 SetCookie）
        ::Sleep(1500);
    }

    // 创建游戏浏览器
    CefRefPtr<HostClient> game_client = new HostClient();
    CefWindowInfo info;
    int w, h;
    g_window.ClientSize(w, h);
    if (w <= 0) w = kDefaultWidth;
    if (h <= 0) h = kDefaultHeight;
    RECT rc = {0, 0, w, h};
    info.SetAsChild(g_window.Handle(), rc);

    CefBrowserSettings settings2;
    settings2.plugins = STATE_ENABLED;

    CefString url_str(opt.url);
    // 绑定 request context handler（HostClient 实现 CefRequestContextHandler）：
    // 使 OnBeforePluginLoad 生效，确保 Flash 插件策略为 ALLOW（否则出现
    // "Right-click to run Adobe Flash Player" click-to-play 提示）。
    // 复用全局 context 的存储，避免丢失 cookie/缓存（免登录态跨启动保留）。
    CefRefPtr<CefRequestContext> request_context =
        CefRequestContext::CreateContext(CefRequestContext::GetGlobalContext(),
                                         game_client);
    CefBrowserHost::CreateBrowser(info, game_client, url_str, settings2,
                                  nullptr, request_context);
    AppLog::Write("创建游戏浏览器: %S", opt.url.c_str());

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

    // DPI 感知：必须保留 PROCESS_PER_MONITOR_DPI_AWARE。
    // 移除后内嵌（HwndHost）时 WPF（DPI 感知）与 GameHost（无感知）坐标体系
    // 不一致，占位窗口物理尺寸被 WPF 换算为 DIP×DPI，导致内嵌画面缩小。
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
    NarutoRunOptions opt = ParseCommandLine(lpCmdLine);

    // 扫码登录模式：加载官网首页（自动弹出 QQ 登录二维码），登录成功写 login_result.txt
    // 所有模式统一记录 userdata 目录（用于写本账号 speed.txt、传环境变量给 ppapi）
    g_userdata_dir = opt.userdata;

    if (opt.login) {
        g_login_mode = true;
        if (opt.url == kDefaultUrl) {
            opt.url = L"https://huoying.qq.com/server/website/";
        }
    }
    // 账号密码自动登录模式：有 --user/--pass 参数即开启（无 cookie 时自动填表登录）
    if (!g_auto_user_b64.empty() && !g_auto_pass_b64.empty()) {
        g_auto_login = true;
    }

    // 浏览器进程：把 Flash 渲染质量写入环境变量，供 ppapi 子进程读取
    // （CEF 会过滤命令行自定义开关，环境变量必然被子进程继承）。
    // 同时把 userdata 目录通过 HUOYIN_USERDATA 传给子进程，供变速 hook 定位本账号 speed.txt。
    if (wcsstr(lpCmdLine, L"--type=") == nullptr) {
        ::SetEnvironmentVariableA("HUOYIN_FLASH_QUALITY",
                                  g_flash_quality.c_str());
        // 启动时把倍速重置为 1x，避免上次退出遗留倍速影响本次会话
        SaveSpeedToFile(1.0);
    }
    if (!opt.userdata.empty()) {
        ::SetEnvironmentVariableW(L"HUOYIN_USERDATA", opt.userdata.c_str());
    }

    // 阻止 Flash 插件（ppapi 子进程）加载时弹 cmd 窗口：Flash 会执行
    // cmd.exe /c echo NOT SANDBOXED 做沙箱探测，未设置 CREATE_NO_WINDOW
    // 导致控制台窗口一闪而过。对 ppapi 子进程 hook CreateProcessW/A，
    // 强制隐藏子进程控制台窗口。
    if (IsChildProcessType(lpCmdLine, L"ppapi")) {
        // hook CreateProcessW/A 强制隐藏 Flash 沙箱探测弹出的 cmd 窗口。
        // 使用 MinHook 实现（自动处理 x86/x64 绝对跳转与指令重定位）。
        InstallNoConsoleHooks();

        // 安装 Flash 渲染质量 hook：改写 Flash 实例创建时的 quality 参数，
        // 真正控制整个游戏（主城/UI/战斗）的渲染质量。目标值由浏览器进程
        // 通过环境变量 HUOYIN_FLASH_QUALITY 传入（CEF 会过滤命令行自定义
        // 开关，环境变量必然被子进程继承）。
        const char* q = "low";
        char env_buf[16] = {0};
        DWORD env_len = ::GetEnvironmentVariableA(
            "HUOYIN_FLASH_QUALITY", env_buf, sizeof(env_buf));
        if (env_len > 0 && env_len < sizeof(env_buf) && env_buf[0])
            q = env_buf;
        InstallFlashQualityHooksAsync(q);

        // 游戏变速：按 exe 目录 speed.txt 的倍速 hook 时间 API。
        InstallSpeedHooks();
    }

    CefMainArgs main_args(GetModuleHandle(nullptr));
    CefRefPtr<HostApp> app = new HostApp();

    // 隐藏可能存在的控制台窗口（浏览器进程与所有 CEF 子进程在进入
    // CefExecuteProcess 前统一隐藏，避免 Flash 等子进程闪现 cmd 窗口）。
    {
        HWND console = ::GetConsoleWindow();
        if (console)
            ::ShowWindow(console, SW_HIDE);
    }

    int exit_code = CefExecuteProcess(main_args, app.get(), nullptr);
    if (exit_code >= 0)
        return exit_code;

    // 防误启动：CEFFlashGameHost 是启动器的私有组件，只应由启动器以 --embed
    // 内嵌模式拉起，或显式 --windowed 手动会话调试。
    // 直接双击（无这两个参数）视为误启动，静默退出，不创建窗口。
    if (!opt.embed && !opt.windowed)
        return 0;

    AppLog::Init();
    AppLog::Write("== CEFFlashGameHost 入口 ==");
    AppLog::Write("URL=%S userdata=%S embed=%d parent=%llu login=%d autologin=%d",
                  opt.url.c_str(), opt.userdata.c_str(), opt.embed ? 1 : 0,
                  (unsigned long long)opt.parent, opt.login ? 1 : 0,
                  g_auto_login ? 1 : 0);
    return RunBrowserProcess(opt);
}
