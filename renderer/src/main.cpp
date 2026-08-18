#include <windows.h>

// 占位渲染器入口，用于阶段 0 验证 x86 工具链。
// 阶段 1 将替换为 CEF 87 宿主（browser + subprocess 双模式）并注册 Flash PPAPI。
int WINAPI wWinMain(HINSTANCE, HINSTANCE, wchar_t *, int)
{
    return 0;
}
