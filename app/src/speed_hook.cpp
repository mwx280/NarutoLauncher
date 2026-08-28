#include "speed_hook.h"

#include <windows.h>
#include <MinHook.h>
#include <stdio.h>
#include <string>

namespace {

// 当前倍速（虚拟时钟倍率），由后台线程从 speed.txt 定期更新。
volatile double g_speed = 1.0;

// 虚拟时钟基准：真实时间 → 虚拟时间 = 基准 + (真实-基准) * 倍速
volatile ULONGLONG g_real_tick_base = 0;
volatile ULONGLONG g_virtual_tick_base = 0;
volatile ULONGLONG g_real_qpc_base = 0;
volatile ULONGLONG g_virtual_qpc_base = 0;

typedef ULONGLONG(WINAPI* GetTickCount64Fn)();
typedef DWORD(WINAPI* GetTickCountFn)();
typedef BOOL(WINAPI* QPCFn)(LARGE_INTEGER*);

GetTickCount64Fn g_real_tick64 = nullptr;
GetTickCountFn g_real_tick = nullptr;
QPCFn g_real_qpc = nullptr;

// 读取 speed.txt 的倍速值：优先本账号 userdata 目录（环境变量 HUOYIN_USERDATA，多开隔离），
// 兜底 exe 目录。返回是否成功读取。
void UpdateSpeed() {
    std::wstring path;
    wchar_t buf[1024] = {0};
    DWORD len = GetEnvironmentVariableW(L"HUOYIN_USERDATA", buf, 1024);
    if (len > 0 && len < 1024) {
        path = buf;
        path += L"\\speed.txt";
    } else {
        wchar_t exe[MAX_PATH] = {0};
        DWORD n = GetModuleFileNameW(nullptr, exe, MAX_PATH);
        if (n == 0 || n >= MAX_PATH)
            return;
        path.assign(exe, n);
        size_t sep = path.find_last_of(L"\\/");
        if (sep != std::wstring::npos)
            path = path.substr(0, sep + 1);
        path += L"speed.txt";
    }
    FILE* f = nullptr;
    if (_wfopen_s(&f, path.c_str(), L"r") == 0 && f) {
        double s = 1.0;
        if (fscanf_s(f, "%lf", &s) == 1 && s > 0.05 && s <= 10.0)
            g_speed = s;
        fclose(f);
    }
}

// 后台线程：每 200ms 刷新倍速。
DWORD WINAPI SpeedWatcher(LPVOID) {
    for (;;) {
        UpdateSpeed();
        Sleep(200);
    }
    return 0;
}

ULONGLONG WINAPI HookedGetTickCount64() {
    ULONGLONG real = g_real_tick64();
    if (g_real_tick_base == 0) {
        g_real_tick_base = real;
        g_virtual_tick_base = real;
    }
    double elapsed = (double)(real - g_real_tick_base);
    return (ULONGLONG)(g_virtual_tick_base + elapsed * g_speed);
}

DWORD WINAPI HookedGetTickCount() {
    return (DWORD)HookedGetTickCount64();
}

BOOL WINAPI HookedQPC(LARGE_INTEGER* lp) {
    BOOL ok = g_real_qpc(lp);
    if (ok && lp) {
        ULONGLONG now = (ULONGLONG)lp->QuadPart;
        if (g_real_qpc_base == 0) {
            g_real_qpc_base = now;
            g_virtual_qpc_base = now;
        }
        double elapsed = (double)(now - g_real_qpc_base);
        lp->QuadPart = (LONGLONG)(g_virtual_qpc_base + elapsed * g_speed);
    }
    return ok;
}

}  // namespace

void InstallSpeedHooks() {
    // MH_Initialize 已由 InstallNoConsoleHooks() 调用，此处只 CreateHook + Enable。

    HMODULE kernel32 = GetModuleHandleW(L"kernel32.dll");
    if (!kernel32)
        return;

    void* p = nullptr;

    p = reinterpret_cast<void*>(GetProcAddress(kernel32, "GetTickCount64"));
    if (p && MH_CreateHookApi(L"kernel32.dll", "GetTickCount64",
                              &HookedGetTickCount64,
                              reinterpret_cast<void**>(&g_real_tick64)) == MH_OK) {
        MH_EnableHook(p);
    }

    p = reinterpret_cast<void*>(GetProcAddress(kernel32, "GetTickCount"));
    if (p && MH_CreateHookApi(L"kernel32.dll", "GetTickCount",
                              &HookedGetTickCount,
                              reinterpret_cast<void**>(&g_real_tick)) == MH_OK) {
        MH_EnableHook(p);
    }

    p = reinterpret_cast<void*>(GetProcAddress(kernel32, "QueryPerformanceCounter"));
    if (p && MH_CreateHookApi(L"kernel32.dll", "QueryPerformanceCounter",
                              &HookedQPC,
                              reinterpret_cast<void**>(&g_real_qpc)) == MH_OK) {
        MH_EnableHook(p);
    }

    HANDLE h = CreateThread(nullptr, 0, SpeedWatcher, nullptr, 0, nullptr);
    if (h)
        CloseHandle(h);
}