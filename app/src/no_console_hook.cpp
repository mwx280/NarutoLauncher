#include "no_console_hook.h"

#include <windows.h>
#include <MinHook.h>

namespace {

// ---- hook 目标：CreateProcessW / CreateProcessA / CreateProcessInternalW ----
// 目的：为 Flash 沙箱探测启动的 cmd.exe 子进程强制附加 CREATE_NO_WINDOW，
// 避免控制台窗口一闪而过。使用 MinHook（自动处理 x86/x64 绝对跳转与指令
// 重定位），替代原手工 inline hook（其 jmp rel32 在 x64 下可能超出 ±2GB
// 跳转范围导致崩溃）。

using CreateProcessWFn = BOOL(WINAPI*)(LPCWSTR, LPWSTR, LPSECURITY_ATTRIBUTES,
                                       LPSECURITY_ATTRIBUTES, BOOL, DWORD,
                                       LPVOID, LPCWSTR, LPSTARTUPINFOW,
                                       LPPROCESS_INFORMATION);
using CreateProcessAFn = BOOL(WINAPI*)(LPCSTR, LPSTR, LPSECURITY_ATTRIBUTES,
                                       LPSECURITY_ATTRIBUTES, BOOL, DWORD,
                                       LPVOID, LPCSTR, LPSTARTUPINFOA,
                                       LPPROCESS_INFORMATION);
// kernel32 私有导出：CreateProcessW/A 内部转发到它，Flash 探测可能直接调用
using CreateProcessInternalWFn = BOOL(WINAPI*)(
    LPCWSTR, LPWSTR, LPSECURITY_ATTRIBUTES, LPSECURITY_ATTRIBUTES, BOOL, DWORD,
    LPVOID, LPCWSTR, LPSTARTUPINFOW, LPPROCESS_INFORMATION, PHANDLE);

CreateProcessWFn g_real_create_process_w = nullptr;
CreateProcessAFn g_real_create_process_a = nullptr;
CreateProcessInternalWFn g_real_create_process_internal_w = nullptr;

constexpr DWORD kCreateNoWindow = 0x08000000;

BOOL WINAPI HookedCreateProcessW(LPCWSTR app, LPWSTR cmd,
                                 LPSECURITY_ATTRIBUTES pa,
                                 LPSECURITY_ATTRIBUTES ta, BOOL inherit,
                                 DWORD flags, LPVOID env, LPCWSTR cwd,
                                 LPSTARTUPINFOW si, LPPROCESS_INFORMATION pi) {
    return g_real_create_process_w(app, cmd, pa, ta, inherit,
                                   flags | kCreateNoWindow, env, cwd, si, pi);
}

BOOL WINAPI HookedCreateProcessA(LPCSTR app, LPSTR cmd,
                                 LPSECURITY_ATTRIBUTES pa,
                                 LPSECURITY_ATTRIBUTES ta, BOOL inherit,
                                 DWORD flags, LPVOID env, LPCSTR cwd,
                                 LPSTARTUPINFOA si, LPPROCESS_INFORMATION pi) {
    return g_real_create_process_a(app, cmd, pa, ta, inherit,
                                   flags | kCreateNoWindow, env, cwd, si, pi);
}

BOOL WINAPI HookedCreateProcessInternalW(
    LPCWSTR app, LPWSTR cmd, LPSECURITY_ATTRIBUTES pa,
    LPSECURITY_ATTRIBUTES ta, BOOL inherit, DWORD flags, LPVOID env,
    LPCWSTR cwd, LPSTARTUPINFOW si, LPPROCESS_INFORMATION pi, PHANDLE token) {
    return g_real_create_process_internal_w(app, cmd, pa, ta, inherit,
                                            flags | kCreateNoWindow, env, cwd,
                                            si, pi, token);
}

}  // namespace

void InstallNoConsoleHooks() {
    if (MH_Initialize() != MH_OK)
        return;

    // 注意：MH_EnableHook 的参数是目标函数地址，不是 detour 地址。
    void* cpw = reinterpret_cast<void*>(
        GetProcAddress(GetModuleHandleW(L"kernel32.dll"), "CreateProcessW"));
    if (cpw && MH_CreateHookApi(L"kernel32.dll", "CreateProcessW",
                                &HookedCreateProcessW,
                                reinterpret_cast<void**>(
                                    &g_real_create_process_w)) == MH_OK) {
        MH_EnableHook(cpw);
    }

    void* cpa = reinterpret_cast<void*>(
        GetProcAddress(GetModuleHandleW(L"kernel32.dll"), "CreateProcessA"));
    if (cpa && MH_CreateHookApi(L"kernel32.dll", "CreateProcessA",
                                &HookedCreateProcessA,
                                reinterpret_cast<void**>(
                                    &g_real_create_process_a)) == MH_OK) {
        MH_EnableHook(cpa);
    }

    void* cpiw = reinterpret_cast<void*>(
        GetProcAddress(GetModuleHandleW(L"kernel32.dll"),
                       "CreateProcessInternalW"));
    if (cpiw && MH_CreateHook(cpiw, &HookedCreateProcessInternalW,
                              reinterpret_cast<void**>(
                                  &g_real_create_process_internal_w)) ==
                    MH_OK) {
        MH_EnableHook(cpiw);
    }
}
