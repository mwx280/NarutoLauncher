#pragma once

// 共享全局状态与通用工具（跨 host_app / host_client / params / main 使用）。
// 火影版：在 CEFFlashGameHost 通用状态基础上，扩展登录/变速/画质等全局量。

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>

#include <string>

#include "include/cef_browser.h"

#include "frameless_window.h"

// ---------- 常量 ----------
extern const char* kFlashVersion;      // Flash 插件版本号
extern const wchar_t* kDefaultUrl;     // 默认加载 URL（游戏入口）
extern const int kDefaultWidth;        // 默认窗口宽度
extern const int kDefaultHeight;       // 默认窗口高度

// ---------- 渲染 / 宿主配置（命令行参数写入，HostApp 读取） ----------
extern bool g_flash_gpu;               // Flash 硬件加速（--flash-gpu=1）
extern bool g_force_dpr;               // 强制 DPR=1（--force-dpr）
extern int g_debug_port;               // CEF DevTools 端口（--debug-port）
extern std::string g_flash_quality;    // Flash 渲染画质（--flash-quality）

// ---------- 运行期全局状态 ----------
extern FramelessWindow g_window;       // 主窗口
extern CefRefPtr<CefBrowser> g_game_browser;  // 游戏浏览器
extern bool g_muted;                   // 页面音频静音状态
extern double g_speed;                 // 游戏倍速（1.0 正常）
extern HWND g_game_hwnd;               // 游戏窗口句柄（从 CEF 回调获取）
extern std::wstring g_window_title;    // 窗口标题
extern bool g_login_mode;              // 扫码登录模式（--login）
extern bool g_auto_login;              // 账号密码自动登录模式（--user/--pass）
extern std::string g_auto_user_b64;    // QQ 号（base64，注入登录框 JS 时 atob 解码）
extern std::string g_auto_pass_b64;    // 密码（base64）
extern std::wstring g_userdata_dir;    // userdata 目录（登录结果写入）
extern std::string g_cookie_json;      // 启动时注入的 cookie（base64 编码的 JSON）
extern HWND g_parent_hwnd;             // 内嵌父窗口

// ---------- 通用工具 ----------

// 刷新当前页面。
void ReloadPage();

// 设置 Flash 画质（level: 0=low / 1=medium / 2=high），改档自动重载页面。
void SetFlashQuality(int level);

// base64 解码（cookie 注入用）。
std::string Base64Decode(const std::string& input);

// 把 CEF 子窗口嵌入主窗口客户区（由 WM_SIZE 同步尺寸）。
void EmbedChild(HWND child);

// 解析 cookie JSON（域分组的字典）并注入 CEF（免登录进游戏）。
void InjectCookies(const std::string& json);

// 主窗口收到 WM_CLOSE：优雅关闭 CEF 浏览器（触发 cookie 刷盘）。
void OnMainWindowClose();

// 主窗口自定义命令消息回调（cmd: 1=刷新, 2=画质, 3=选区, 4=静音, 5=倍速）。
void OnWindowCommand(int cmd, WPARAM w, LPARAM l);
