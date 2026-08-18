#include "app.h"

#include "include/cef_version.h"

#include <windows.h>

#include <string>
#include <vector>

namespace {

// Flash PPAPI 插件路径与版本，需在运行时与 third_party/pepflashplayer.dll 一致
const char* kFlashVersion = "34.0.0.380";

// 以 UTF-8 返回插件绝对路径。
// 必须用宽字符 API 解析：安装目录可能含中文（如"火影忍者Online"），
// 若用 GetFullPathNameA 得到的是本地 ANSI(GBK) 字节，再按 UTF-8 传给 CEF
// 会在子进程里变成乱码，导致插件加载失败（error 126）。
std::string ResolveFlashPluginPath() {
    wchar_t exe_path[MAX_PATH] = {0};
    DWORD n = ::GetModuleFileNameW(nullptr, exe_path, MAX_PATH);
    if (n == 0 || n >= MAX_PATH)
        return "pepflashplayer.dll";

    std::wstring plugin_path(exe_path, n);
    size_t sep = plugin_path.find_last_of(L"\\/");
    if (sep != std::wstring::npos)
        plugin_path = plugin_path.substr(0, sep + 1);
    plugin_path += L"pepflashplayer.dll";

    int len = ::WideCharToMultiByte(CP_UTF8, 0, plugin_path.c_str(),
                                    static_cast<int>(plugin_path.size()),
                                    nullptr, 0, nullptr, nullptr);
    if (len <= 0)
        return "pepflashplayer.dll";

    std::vector<char> buf(len);
    ::WideCharToMultiByte(CP_UTF8, 0, plugin_path.c_str(),
                          static_cast<int>(plugin_path.size()),
                          buf.data(), len, nullptr, nullptr);
    return std::string(buf.data(), len);
}

}  // namespace

NarutoApp::NarutoApp() {}

void NarutoApp::OnBeforeCommandLineProcessing(
    const CefString& process_type,
    CefRefPtr<CefCommandLine> command_line) {
    // 该回调在所有进程（浏览器/渲染/GPU/插件子进程）中都会触发，
    // 统一注入 Flash PPAPI 插件开关。
    command_line->AppendSwitchWithValue("ppapi-flash-path",
                                        ResolveFlashPluginPath());
    command_line->AppendSwitchWithValue("ppapi-flash-version", kFlashVersion);

    // 开启 GPU 加速，保证 Flash wmode=direct 的渲染性能
    command_line->AppendSwitch("enable-gpu");

    // 可行性验证阶段：开启详细日志，便于确认 Flash 插件加载
    command_line->AppendSwitch("enable-logging");
    command_line->AppendSwitchWithValue("v", "1");
}
