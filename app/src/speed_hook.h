#pragma once

#include <windows.h>

// 游戏变速 hook（ppapi 子进程）：按 exe 目录 speed.txt 的倍速，
// hook GetTickCount64 / GetTickCount / QueryPerformanceCounter，
// 用虚拟时钟对 Flash 游戏做加速/减速。需在 no_console_hook 之后安装
// （其已 MH_Initialize）。
void InstallSpeedHooks();