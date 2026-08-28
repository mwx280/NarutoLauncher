#include "app_log.h"

#include <windows.h>

#include <cstdarg>
#include <ctime>

namespace {
FILE* g_log = nullptr;
}  // namespace

void AppLog::Init() {
    if (g_log)
        return;
    // 日志文件位于 exe 同级目录（中文路径用宽字符 API 解析）
    wchar_t exe[MAX_PATH] = {0};
    DWORD n = ::GetModuleFileNameW(nullptr, exe, MAX_PATH);
    std::wstring path;
    if (n > 0 && n < MAX_PATH) {
        path.assign(exe, n);
        size_t sep = path.find_last_of(L"\\/");
        if (sep != std::wstring::npos)
            path = path.substr(0, sep + 1);
    }
    path += L"CEFFlashGameHost.log";
    // 追加模式：避免多 CEF 进程并发打开互相覆盖
    _wfopen_s(&g_log, path.c_str(), L"a");
}

void AppLog::Write(const char* fmt, ...) {
    if (!g_log)
        return;
    // 时间戳
    time_t now = time(nullptr);
    struct tm tmv;
    localtime_s(&tmv, &now);
    fprintf(g_log, "[%02d:%02d:%02d] ",
            tmv.tm_hour, tmv.tm_min, tmv.tm_sec);
    va_list args;
    va_start(args, fmt);
    vfprintf(g_log, fmt, args);
    va_end(args);
    fprintf(g_log, "\n");
    fflush(g_log);
}
