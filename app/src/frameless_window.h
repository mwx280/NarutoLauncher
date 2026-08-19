#pragma once

#include <windows.h>

// 无边框窗口框架（Win32 原生实现）。
//
// 与 Qt 版思路一致：去系统边框，WM_NCHITTEST 实现边缘缩放与标题栏拖拽，
// 最大化/全屏时铺满。与 CEF 浏览器窗口协作：
//   - Create() 创建无边框主窗口
//   - SetClientChild() 把 CEF 窗口嵌入客户区（SetParent + 尺寸同步）
class FramelessWindow {
public:
    FramelessWindow();
    ~FramelessWindow();

    // 创建主窗口。embed=true 时创建为 WS_CHILD 子窗口（供 DesktopChildSiteBridge 内嵌）。
    bool Create(int width, int height, bool embed = false, HWND parent = nullptr);

    // 销毁窗口。
    void Destroy();

    // 主窗口句柄。
    HWND Handle() const { return hwnd_; }

    // 客户区尺寸。
    void ClientSize(int& w, int& h) const;

    // 设置/移除 CEF 子窗口（嵌入客户区，随窗口缩放同步）。
    void SetClientChild(HWND child);

    // 窗口状态。
    bool IsWindowMaximized() const { return maximized_; }
    bool IsWindowFullscreen() const { return fullscreen_; }

    // 触发窗口状态切换（由标题栏按钮/JS 桥调用）。
    void ToggleMaximize();
    void ToggleFullscreen();
    void Minimize();
    void Close();

private:
    HWND hwnd_ = nullptr;
    HWND child_ = nullptr;
    bool maximized_ = false;
    bool fullscreen_ = false;
    RECT normal_rect_;  // 普通状态几何（还原/退全屏目标）

    static LRESULT CALLBACK WndProc(HWND hwnd, UINT msg,
                                    WPARAM w, LPARAM l);
    LRESULT HandleMessage(UINT msg, WPARAM w, LPARAM l);
    void OnResize();
    void UpdateMaximizedState();
    void ApplyFrameStyle(bool fullscreen);
};
