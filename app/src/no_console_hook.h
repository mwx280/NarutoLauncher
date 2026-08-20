#pragma once

#include <windows.h>

// 阻止 Flash 插件加载时弹出 cmd 窗口。
//
// 根因：pepflashplayer.dll 每次加载都会通过 CreateProcessW/A 执行
//   cmd.exe /c echo NOT SANDBOXED
// 做沙箱探测。该调用未设置 CREATE_NO_WINDOW，导致 cmd 控制台窗口一闪而过。
//
// 方案：在 ppapi 插件子进程中 hook CreateProcessW/A，为子进程强制追加
// CREATE_NO_WINDOW 标志，命令照常执行但不再显示窗口。仅在 ppapi 子进程
// 调用一次，不影响浏览器/渲染进程。
void InstallNoConsoleHooks();
