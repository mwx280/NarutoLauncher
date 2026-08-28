#include "speed_hook.h"

#include <windows.h>
#include <mmsystem.h>
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
volatile ULONGLONG g_real_time_base = 0;
volatile ULONGLONG g_virtual_time_base = 0;
volatile ULONGLONG g_real_msg_base = 0;
volatile ULONGLONG g_virtual_msg_base = 0;

typedef ULONGLONG(WINAPI* GetTickCount64Fn)();
typedef DWORD(WINAPI* GetTickCountFn)();
typedef BOOL(WINAPI* QPCFn)(LARGE_INTEGER*);
typedef DWORD(WINAPI* TimeGetTimeFn)();
typedef MMRESULT(WINAPI* TimeSetEventFn)(UINT, UINT, LPTIMECALLBACK, DWORD_PTR, UINT);
typedef DWORD(WINAPI* GetMessageTimeFn)();
typedef UINT_PTR(WINAPI* SetTimerFn)(HWND, UINT_PTR, UINT, TIMERPROC);

GetTickCount64Fn g_real_tick64 = nullptr;
GetTickCountFn g_real_tick = nullptr;
QPCFn g_real_qpc = nullptr;
TimeGetTimeFn g_real_timegettime = nullptr;
TimeSetEventFn g_real_timesetevent = nullptr;
GetMessageTimeFn g_real_getmessagetime = nullptr;
SetTimerFn g_real_settimer = nullptr;

// 读取 speed.txt 的倍速值：优先本账号 userdata 目录（环境变量 HUOYIN_USERDATA，多开隔离），
// 兜底 exe 目录。
void AdjustBases(double new_speed);
void UpdateSpeed() {
    std::wstring path;
    wchar_t buf[1024] = {0};
    DWORD len = GetEnvironmentVariableW(L"HUOYIN_USERDATA", buf, 1024);
    if (len > 0 && len < 1024 && buf[0]) {
        path.assign(buf, len);
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
            AdjustBases(s);
        fclose(f);
    }
}

// 倍速变化时重设虚拟时钟基准，使时间连续。
// 若基准不变，降倍速（如 2→1）会让虚拟时间瞬间回退，Flash 检测到时间倒退会卡死。
void AdjustBases(double new_speed) {
    double old = g_speed;
    if (new_speed == old)
        return;
    if (g_real_tick64) {
        ULONGLONG real = g_real_tick64();
        ULONGLONG virt = g_virtual_tick_base +
            (ULONGLONG)((double)(real - g_real_tick_base) * old);
        g_real_tick_base = real;
        g_virtual_tick_base = virt;
    }
    if (g_real_qpc) {
        LARGE_INTEGER li{};
        if (g_real_qpc(&li)) {
            ULONGLONG real = (ULONGLONG)li.QuadPart;
            ULONGLONG virt = g_virtual_qpc_base +
                (ULONGLONG)((double)(real - g_real_qpc_base) * old);
            g_real_qpc_base = real;
            g_virtual_qpc_base = virt;
        }
    }
    if (g_real_timegettime) {
        DWORD real = g_real_timegettime();
        ULONGLONG virt = g_virtual_time_base +
            (ULONGLONG)((DWORD)(real - (DWORD)g_real_time_base) * old);
        g_real_time_base = real;
        g_virtual_time_base = virt;
    }
    if (g_real_getmessagetime) {
        DWORD real = g_real_getmessagetime();
        ULONGLONG virt = g_virtual_msg_base +
            (ULONGLONG)((DWORD)(real - (DWORD)g_real_msg_base) * old);
        g_real_msg_base = real;
        g_virtual_msg_base = virt;
    }
    g_speed = new_speed;
}

// 后台线程：每 200ms 刷新倍速。
DWORD WINAPI SpeedWatcher(LPVOID) {
    for (;;) {
        UpdateSpeed();
        Sleep(200);
    }
    return 0;
}

// ---- 64 位时钟：GetTickCount64 / QueryPerformanceCounter ----

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

// ---- 32 位毫秒时钟：timeGetTime / GetMessageTime（处理回绕） ----

static ULONGLONG Virtual32(ULONGLONG real, volatile ULONGLONG& rbase,
                           volatile ULONGLONG& vbase) {
    if (rbase == 0) {
        rbase = real;
        vbase = real;
    }
    DWORD diff = (DWORD)(real - rbase);
    return vbase + (ULONGLONG)(diff * g_speed);
}

DWORD WINAPI HookedTimeGetTime() {
    DWORD real = g_real_timegettime();
    return (DWORD)Virtual32(real, g_real_time_base, g_virtual_time_base);
}

DWORD WINAPI HookedGetMessageTime() {
    DWORD real = g_real_getmessagetime();
    return (DWORD)Virtual32(real, g_real_msg_base, g_virtual_msg_base);
}

// ---- 定时器：timeSetEvent / SetTimer（变速周期间隔） ----

MMRESULT WINAPI HookedTimeSetEvent(UINT uDelay, UINT uResolution,
                                   LPTIMECALLBACK fptc, DWORD_PTR dwUser,
                                   UINT fuEvent) {
    double s = g_speed;
    if (s > 0.05) {
        UINT d = (UINT)(uDelay / s);
        if (d == 0) d = 1;
        uDelay = d;
    }
    return g_real_timesetevent(uDelay, uResolution, fptc, dwUser, fuEvent);
}

UINT_PTR WINAPI HookedSetTimer(HWND hWnd, UINT_PTR nIDEvent, UINT uElapse,
                               TIMERPROC lpTimerFunc) {
    double s = g_speed;
    if (s > 0.05) {
        UINT e = (UINT)(uElapse / s);
        if (e == 0) e = 1;
        uElapse = e;
    }
    return g_real_settimer(hWnd, nIDEvent, uElapse, lpTimerFunc);
}

// ---- hook 辅助 ----

static bool HookAddress(LPVOID target, LPVOID detour, LPVOID* original) {
    if (!target)
        return false;
    if (MH_CreateHook(target, detour, original) != MH_OK)
        return false;
    MH_EnableHook(target);
    return true;
}

}  // namespace

void InstallSpeedHooks() {
    // MH_Initialize 已由 InstallNoConsoleHooks() 调用，此处只 CreateHook + Enable。

    HMODULE kernel32 = GetModuleHandleW(L"kernel32.dll");
    if (!kernel32)
        return;

    // GetTickCount64 / GetTickCount / QueryPerformanceCounter
    HookAddress(reinterpret_cast<LPVOID>(
                    GetProcAddress(kernel32, "GetTickCount64")),
                &HookedGetTickCount64,
                reinterpret_cast<LPVOID*>(&g_real_tick64));
    HookAddress(reinterpret_cast<LPVOID>(
                    GetProcAddress(kernel32, "GetTickCount")),
                &HookedGetTickCount,
                reinterpret_cast<LPVOID*>(&g_real_tick));
    HookAddress(reinterpret_cast<LPVOID>(
                    GetProcAddress(kernel32, "QueryPerformanceCounter")),
                &HookedQPC,
                reinterpret_cast<LPVOID*>(&g_real_qpc));

    // timeGetTime / timeSetEvent（winmm.dll，Flash/音频常用时间源）
    HMODULE winmm = LoadLibraryW(L"winmm.dll");
    if (winmm) {
        HookAddress(reinterpret_cast<LPVOID>(
                        GetProcAddress(winmm, "timeGetTime")),
                    &HookedTimeGetTime,
                    reinterpret_cast<LPVOID*>(&g_real_timegettime));
        HookAddress(reinterpret_cast<LPVOID>(
                        GetProcAddress(winmm, "timeSetEvent")),
                    &HookedTimeSetEvent,
                    reinterpret_cast<LPVOID*>(&g_real_timesetevent));
    }

    // GetMessageTime / SetTimer（user32.dll）
    HMODULE user32 = GetModuleHandleW(L"user32.dll");
    if (user32) {
        HookAddress(reinterpret_cast<LPVOID>(
                        GetProcAddress(user32, "GetMessageTime")),
                    &HookedGetMessageTime,
                    reinterpret_cast<LPVOID*>(&g_real_getmessagetime));
        HookAddress(reinterpret_cast<LPVOID>(
                        GetProcAddress(user32, "SetTimer")),
                    &HookedSetTimer,
                    reinterpret_cast<LPVOID*>(&g_real_settimer));
    }

    HANDLE h = CreateThread(nullptr, 0, SpeedWatcher, nullptr, 0, nullptr);
    if (h)
        CloseHandle(h);
}
