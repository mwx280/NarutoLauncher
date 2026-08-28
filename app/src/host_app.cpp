// HostApp 实现：注册 Flash 插件、按全局配置透传命令行开关。

#include "host_app.h"

#include <string>
#include <vector>

#include "globals.h"

std::string HostApp::FlashPluginPath() {
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

void HostApp::OnBeforeCommandLineProcessing(
    const CefString& process_type,
    CefRefPtr<CefCommandLine> command_line) {
    // 禁用沙盒：保证所有子进程（含 ppapi Flash 插件进程）以无沙盒运行。
    // 原因：x64 下 Flash 插件进程在沙盒环境初始化时崩溃（BEX64/0xc0000005），
    // 关闭沙盒可消除该兼容性问题。对本地固定内容（腾讯游戏）无安全影响。
    command_line->AppendSwitch("no-sandbox");
    command_line->AppendSwitch("disable-setuid-sandbox");
    command_line->AppendSwitchWithValue("ppapi-flash-path",
                                        FlashPluginPath());
    command_line->AppendSwitchWithValue("ppapi-flash-version",
                                        kFlashVersion);
    // Flash 硬件加速：默认关闭（软件渲染）。
    // 关闭时同时禁用 CEF 全局 GPU 与 Flash 的 GPU 加速（Stage3D/3D API/
    // GPU 合成），确保 Flash 完全走 CPU 渲染。开启时恢复 GPU 加速。
    // 传统 Flash 页游画面主要由 CPU 渲染，开启基本无提升，反而可能花屏/
    // 兼容性问题。此设置需重新进入游戏才生效（浏览器进程创建时读取）。
    // 仅在浏览器进程（process_type 为空）设置，子进程会自动继承该开关。
    if (process_type.empty()) {
        // CEF DevTools 远程调试（--debug-port=<port>），用于注入脚本分析游戏内部对象
        if (g_debug_port > 0) {
            command_line->AppendSwitchWithValue("remote-debugging-port",
                                                std::to_string(g_debug_port));
            command_line->AppendSwitch("remote-allow-origins=*");
        }
        if (g_flash_gpu) {
            command_line->AppendSwitch("enable-gpu");
        } else {
            command_line->AppendSwitch("disable-gpu");
            command_line->AppendSwitch("disable-gpu-compositing");
            command_line->AppendSwitch("disable-flash-stage3d");
            command_line->AppendSwitch("disable-3d-apis");
        }
        // 强制 device-scale-factor=1：Flash 以 1 倍物理分辨率渲染，
        // quality=low 的降质真正生效。与 DPI 感知组合：DPI 感知保证
        // 内嵌坐标一致（铺满），DPR=1 降低 Flash 渲染分辨率。
        // 可通过 --force-dpr=0 关闭（画质优先模式）。
        if (g_force_dpr) {
            command_line->AppendSwitchWithValue("force-device-scale-factor",
                                                "1");
        }
    }
    // 透传 Flash 渲染质量到子进程（ppapi Flash 插件进程据此 hook 改写 quality）。
    command_line->AppendSwitchWithValue("flash-quality",
                                        g_flash_quality);
    command_line->AppendSwitch("persist-session-cookies");
    // 日志写文件而非控制台，避免弹出 cmd 窗口
    command_line->AppendSwitchWithValue("log-file",
        "CEFFlashGameHost_cef.log");
}

CefRefPtr<CefBrowserProcessHandler> HostApp::GetBrowserProcessHandler() {
    return this;
}
