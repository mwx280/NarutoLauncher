// Flash 插件 hook：拦截 PPP_Instance::DidCreate，改写 quality 参数，
// 从而真正控制整个游戏（主城/UI/战斗）的渲染质量。游戏 JS 无法覆盖。
#include "flash_hook.h"

#include <string>
#include <vector>

#include <MinHook.h>

namespace {

// ---- PPAPI ABI 声明（标准公开接口，Chromium ppapi/c 定义）----

// PP_Instance / PP_Resource 是实例/资源句柄（不透明整数）。
typedef intptr_t PP_Instance;
typedef intptr_t PP_Resource;

// PP_Bool（PPAPI 使用 int32 表示布尔）。
typedef int32_t PP_Bool;

// PPP_GetInterface：Flash 导出，返回接口表指针。
typedef const void* (*PPP_GetInterfaceFn)(const char* interface_name);

// PPP_Instance_1_1 接口表（完整布局，顺序必须与 Chromium
// ppapi/c/ppp_instance.h 一致）。共 5 个成员，缺失会导致 Flash 通过
// 返回的接口表调用其他函数时读到无效内存而崩溃/黑屏。
struct PPP_Instance_1_1 {
    PP_Bool(*DidCreate)(PP_Instance instance, uint32_t argc,
                        const char** argn, const char** argv);
    void (*DidDestroy)(PP_Instance instance);
    void (*DidChangeView)(PP_Instance instance, PP_Resource view);
    void (*DidChangeFocus)(PP_Instance instance, PP_Bool has_focus);
    PP_Bool (*HandleDocumentLoad)(PP_Instance instance,
                                  PP_Resource url_loader);
};

// 目标 quality（外部通过 InstallFlashQualityHooksAsync 设置）。
const char* g_target_quality = "low";

// 待安装的目标 quality（线程参数，先存全局再给 hook 读取）。
std::string g_requested_quality = "low";

// 原始 PPP_GetInterface。
PPP_GetInterfaceFn g_real_get_interface = nullptr;

// 原始 PPP_Instance::DidCreate（从接口表读取）。
PP_Bool(*g_real_did_create)(PP_Instance, uint32_t, const char**, const char**) =
    nullptr;

// ---- 包装 DidCreate：改写 quality ----
PP_Bool HookedDidCreate(PP_Instance instance, uint32_t argc,
                        const char** argn, const char** argv) {
    // 深拷贝参数数组，便于修改（argv 指向插件内部只读内存）。
    std::vector<std::string> names, values;
    for (uint32_t i = 0; i < argc; ++i) {
        names.push_back(argn && argn[i] ? argn[i] : "");
        values.push_back(argv && argv[i] ? argv[i] : "");
    }

    // 改写 quality 参数（可能不存在则追加）。
    bool found = false;
    for (uint32_t i = 0; i < argc; ++i) {
        if (names[i] == "quality") {
            values[i] = g_target_quality;
            found = true;
            break;
        }
    }
    if (!found) {
        names.push_back("quality");
        values.push_back(g_target_quality);
        argc += 1;
    }

    // 重建 C 风格指针数组。
    std::vector<const char*> pn, pv;
    for (uint32_t i = 0; i < argc; ++i) {
        pn.push_back(names[i].c_str());
        pv.push_back(values[i].c_str());
    }

    return g_real_did_create(instance, argc, pn.data(), pv.data());
}

// ---- 包装 PPP_GetInterface：替换返回的 PPP_Instance 接口表 ----
const void* HookedGetInterface(const char* interface_name) {
    const void* iface = g_real_get_interface(interface_name);
    if (!iface)
        return iface;

    // 只对标准 PPP_Instance;1.x 接口做包装（排除 PPP_Instance_Private 等
    // 其他以 PPP_Instance 开头但结构不同的接口，避免返回损坏的接口表）。
    if (interface_name &&
        (strcmp(interface_name, "PPP_Instance;1.1") == 0 ||
         strcmp(interface_name, "PPP_Instance;1.0") == 0)) {
        auto* inst = static_cast<const PPP_Instance_1_1*>(iface);
        if (inst->DidCreate && inst->DidCreate != HookedDidCreate) {
            // 保存原始 DidCreate（用于转发）。
            g_real_did_create = inst->DidCreate;
            // 返回修改后的接口表（复制一份，替换 DidCreate）。
            static PPP_Instance_1_1 hooked = *inst;
            hooked.DidCreate = HookedDidCreate;
            return &hooked;
        }
    }
    return iface;
}

}  // namespace

// 前置声明（InstallThread 使用）。
void InstallFlashQualityHooks();

// 后台线程安装 hook：等待 Flash DLL 加载后 hook。
static DWORD WINAPI InstallThread(LPVOID) {
    InstallFlashQualityHooks();
    return 0;
}

void InstallFlashQualityHooksAsync(const char* quality) {
    // 记录目标画质（供 hook 在 DidCreate 时使用）。
    g_requested_quality = quality ? quality : "low";
    g_target_quality = g_requested_quality.c_str();
    // 异步安装：不阻塞 ppapi 进程主线程（DLL 加载依赖 CEF 流程继续执行）。
    HANDLE h = CreateThread(nullptr, 0, InstallThread, nullptr, 0, nullptr);
    if (h)
        CloseHandle(h);
}

void InstallFlashQualityHooks() {
    // 轮询等待 pepflashplayer.dll 加载（Flash 插件进程启动早期 DLL 尚未加载，
    // GetModuleHandleW 会失败）。最多等待 10 秒，每 20ms 重试一次。
    constexpr int kMaxWaitMs = 10000;
    constexpr int kPollMs = 20;
    HMODULE flash = nullptr;
    for (int waited = 0; waited < kMaxWaitMs; waited += kPollMs) {
        flash = GetModuleHandleW(L"pepflashplayer.dll");
        if (flash)
            break;
        Sleep(kPollMs);
    }
    if (!flash)
        return;

    // MH_Initialize 已由 InstallNoConsoleHooks() 调用（ppapi 进程中先执行），
    // 此处不再重复初始化（MinHook 的 MH_Initialize 非线程安全，重复调用会失败）。
    void* pgi = reinterpret_cast<void*>(
        GetProcAddress(flash, "PPP_GetInterface"));
    if (!pgi)
        return;

    if (MH_CreateHook(pgi, &HookedGetInterface,
                      reinterpret_cast<void**>(&g_real_get_interface)) ==
        MH_OK) {
        MH_EnableHook(pgi);
    }
}
