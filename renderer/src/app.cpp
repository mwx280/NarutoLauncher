#include "app.h"

#include "include/cef_version.h"

#include <string>

namespace {

// Flash PPAPI 插件路径与版本，需在运行时与 third_party/pepflashplayer.dll 一致
const char* kFlashPluginPath = "pepflashplayer.dll";
const char* kFlashVersion = "34.0.0.380";

}  // namespace

NarutoApp::NarutoApp() {}

void NarutoApp::OnBeforeCommandLineProcessing(
    const CefString& process_type,
    CefRefPtr<CefCommandLine> command_line) {
    // 该回调在所有进程（浏览器/渲染/GPU/插件子进程）中都会触发，
    // 统一注入 Flash PPAPI 插件开关。
    // 用绝对路径解析，避免子进程工作目录不同导致找不到插件。
    std::string pluginPath = kFlashPluginPath;

    // 若以相对路径启动，尝试转为绝对路径
    if (pluginPath.find(':') == std::string::npos) {
        char buf[1024] = {0};
        DWORD n = ::GetFullPathNameA(pluginPath.c_str(), sizeof(buf), buf, nullptr);
        if (n > 0 && n < sizeof(buf)) {
            pluginPath = buf;
        }
    }

    command_line->AppendSwitchWithValue("ppapi-flash-path", pluginPath);
    command_line->AppendSwitchWithValue("ppapi-flash-version", kFlashVersion);

    // 开启 GPU 加速，保证 Flash wmode=direct 的渲染性能
    command_line->AppendSwitch("enable-gpu");

    // 可行性验证阶段：开启详细日志，便于确认 Flash 插件加载
    command_line->AppendSwitch("enable-logging");
    command_line->AppendSwitchWithValue("v", "1");
}
