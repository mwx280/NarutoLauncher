#pragma once

#include <windows.h>

// Hook Flash 插件（pepflashplayer.dll）：
// 拦截 PPP_GetInterface 返回的 PPP_Instance 接口表，包装 DidCreate 函数，
// 在 Flash 实例创建时改写 argv 中的 quality 参数（如 quality=low），
// 从而真正降低整个游戏（主城/UI/战斗）的渲染质量。游戏 JS 无法覆盖。
//
// 仅在 ppapi 子进程（--type=ppapi）中安装。异步安装（独立线程等待
// Flash DLL 加载），不阻塞主流程。quality 为目标画质（low/medium/high）。
void InstallFlashQualityHooksAsync(const char* quality);
