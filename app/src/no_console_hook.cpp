#include "no_console_hook.h"

#include <cstdint>
#include <cstring>

namespace {

// ---- x86 inline hook 基础 ----
// 目标函数入口写入 5 字节绝对跳转（jmp rel32）。被覆盖的前 5 字节保存到
// trampoline，调用原函数时经 trampoline 进入剩余部分。

constexpr int kHookLen = 5;

// 保存原始前 5 字节的 trampoline。
struct Trampoline {
    unsigned char bytes[kHookLen];
    unsigned char* rest;  // 原始函数 + kHookLen
};

// 为函数 fn 写入 5 字节跳转到 target。
void WriteJump(unsigned char* dst, void* target) {
    dst[0] = 0xE9;  // jmp rel32
    intptr_t rel = reinterpret_cast<intptr_t>(target) -
                   reinterpret_cast<intptr_t>(dst + 5);
    std::memcpy(dst + 1, &rel, 4);
}

// 生成 trampoline（nop 填充 5 字节 + 跳回原函数剩余部分）。
void* BuildTrampoline(unsigned char* origin, int len) {
    auto* buf = static_cast<unsigned char*>(
        VirtualAlloc(nullptr, 32, MEM_COMMIT | MEM_RESERVE,
                     PAGE_EXECUTE_READWRITE));
    if (!buf) return nullptr;
    for (int i = 0; i < kHookLen; ++i)
        buf[i] = 0x90;  // nop
    std::memcpy(buf, origin, len < kHookLen ? len : kHookLen);
    WriteJump(buf + kHookLen, origin + kHookLen);
    return buf;
}

// ---- 被 hook 的原始函数指针 ----
using CreateProcessWFn = BOOL(WINAPI*)(LPCWSTR, LPWSTR, LPSECURITY_ATTRIBUTES,
                                       LPSECURITY_ATTRIBUTES, BOOL, DWORD,
                                       LPVOID, LPCWSTR, LPSTARTUPINFOW,
                                       LPPROCESS_INFORMATION);
using CreateProcessAFn = BOOL(WINAPI*)(LPCSTR, LPSTR, LPSECURITY_ATTRIBUTES,
                                       LPSECURITY_ATTRIBUTES, BOOL, DWORD,
                                       LPVOID, LPCSTR, LPSTARTUPINFOA,
                                       LPPROCESS_INFORMATION);

CreateProcessWFn g_real_create_process_w = nullptr;
CreateProcessAFn g_real_create_process_a = nullptr;

constexpr DWORD kCreateNoWindow = 0x08000000;

BOOL WINAPI HookedCreateProcessW(LPCWSTR app, LPWSTR cmd,
                                 LPSECURITY_ATTRIBUTES pa,
                                 LPSECURITY_ATTRIBUTES ta, BOOL inherit,
                                 DWORD flags, LPVOID env, LPCWSTR cwd,
                                 LPSTARTUPINFOW si, LPPROCESS_INFORMATION pi) {
    // 强制隐藏子进程控制台窗口（Flash 的 cmd.exe /c 沙箱探测等）
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

// 对单个导出函数安装 hook。
template <typename T>
bool HookExport(HMODULE k32, const char* name, T hookFn, T* realOut) {
    auto* origin = reinterpret_cast<unsigned char*>(GetProcAddress(k32, name));
    if (!origin) return false;

    DWORD old = 0;
    if (!VirtualProtect(origin, kHookLen, PAGE_EXECUTE_READWRITE, &old))
        return false;

    auto* tramp = static_cast<T>(BuildTrampoline(origin, kHookLen));
    if (!tramp) {
        VirtualProtect(origin, kHookLen, old, &old);
        return false;
    }
    *realOut = tramp;
    WriteJump(origin, reinterpret_cast<void*>(hookFn));
    VirtualProtect(origin, kHookLen, old, &old);
    return true;
}

}  // namespace

void InstallNoConsoleHooks() {
    HMODULE k32 = GetModuleHandleW(L"kernel32.dll");
    if (!k32) return;
    // CreateProcessW / CreateProcessA 签名不同，分别 hook。
    {
        auto* origin = reinterpret_cast<unsigned char*>(
            GetProcAddress(k32, "CreateProcessW"));
        if (origin) {
            DWORD old = 0;
            if (VirtualProtect(origin, kHookLen, PAGE_EXECUTE_READWRITE, &old)) {
                auto* tramp = static_cast<CreateProcessWFn>(
                    BuildTrampoline(origin, kHookLen));
                if (tramp) {
                    g_real_create_process_w = tramp;
                    WriteJump(origin,
                              reinterpret_cast<void*>(HookedCreateProcessW));
                }
                VirtualProtect(origin, kHookLen, old, &old);
            }
        }
    }
    {
        auto* origin = reinterpret_cast<unsigned char*>(
            GetProcAddress(k32, "CreateProcessA"));
        if (origin) {
            DWORD old = 0;
            if (VirtualProtect(origin, kHookLen, PAGE_EXECUTE_READWRITE, &old)) {
                auto* tramp = static_cast<CreateProcessAFn>(
                    BuildTrampoline(origin, kHookLen));
                if (tramp) {
                    g_real_create_process_a = tramp;
                    WriteJump(origin,
                              reinterpret_cast<void*>(HookedCreateProcessA));
                }
                VirtualProtect(origin, kHookLen, old, &old);
            }
        }
    }
}
