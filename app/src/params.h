#pragma once

// 命令行参数解析：把 lpCmdLine 解析为结构化配置，供 wWinMain 使用。
// 火影版：除 CEFFlashGameHost 通用参数外，扩展登录/自动登录/cookie 参数。

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>

#include <string>

// 解析结果：wWinMain 启动参数
struct NarutoRunOptions {
    std::wstring url;      // 要加载的页面 URL
    std::wstring userdata; // 独立缓存目录
    std::wstring title;    // 窗口标题
    bool embed = false;    // 内嵌子窗口模式
    bool windowed = false; // 独立有边框窗口模式
    bool login = false;    // 扫码登录模式（--login）
    HWND parent = nullptr; // 内嵌父窗口句柄
    bool show_usage = false; // 请求显示帮助（--help）
    // 渲染 / 登录配置写入全局，供 HostApp / HostClient 读取
};

// 解析命令行。返回的选项中的 URL 默认 kDefaultUrl。
NarutoRunOptions ParseCommandLine(const wchar_t* lpCmdLine);

// 判断当前进程是否为指定类型的 CEF 子进程（如 --type=ppapi）。
bool IsChildProcessType(const wchar_t* lpCmdLine, const wchar_t* type);
