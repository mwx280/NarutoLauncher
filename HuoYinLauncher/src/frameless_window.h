#pragma once

#include <QFrame>
#include <QMainWindow>
#include <QPoint>
#include <QRect>

class QVBoxLayout;

class TitleBar;

// 无边框窗口根容器：承载圆角渐变背景与阴影边距。
// 窗口透明背景 + 该容器带边框留白，实现圆角与阴影效果。
class WindowRoot : public QFrame {
public:
    explicit WindowRoot(QWidget* parent = nullptr);
};

// 无边框窗口框架。
//
// 职责：
//   1. 去掉系统边框（FramelessWindowHint + 透明背景）
//   2. 内置自绘标题栏（TitleBar），负责拖拽移动 / 最小化 / 最大化 / 关闭
//   3. 边缘与四角缩放：通过 nativeEvent 拦截 WM_NCHITTEST，
//      把窗口边缘映射到 HTLEFT/HTRIGHT/HTTOP/HTBOTTOM 等命中区，
//      让系统完成真正的缩放（尺寸计算、最小/最大尺寸约束）。
//   4. 最大化 / 还原 / 全屏切换时播放平滑几何动画。
//
// 使用方式：
//   FramelessWindow w;
//   w.SetTitle("火影忍者Online");
//   w.SetContent(widget);   // 主体内容（例如左侧导航 + 游戏视图）
//   w.show();
class FramelessWindow : public QMainWindow {
    Q_OBJECT
public:
    explicit FramelessWindow(QWidget* parent = nullptr);

    // 设置标题栏文字。
    void SetTitle(const QString& title);

    // 设置主体内容（替换当前内容区，不含标题栏）。
    void SetContent(QWidget* content);

    // 边缘缩放的敏感区宽度（像素）。
    int ResizeBorder() const { return 6; }

    // 窗口是否处于最大化状态（基于当前屏幕工作区判断，时序安全）。
    bool IsWindowMaximized() const;

    // 窗口是否处于全屏状态。
    bool IsWindowFullscreen() const;

    // 拖拽时从最大化立即还原（无动画），供 TitleBar 拖动调用。
    void RestoreForDrag();

    // 设置是否"关闭即隐藏"（配合托盘）。默认 true：点关闭按钮隐藏到托盘，
    // 由托盘菜单"退出"真正结束程序。
    void SetCloseToTray(bool enabled);

    // 供托盘"退出"调用：置允许关闭后真正退出。
    void Quit();

protected:
    // 拦截 WM_NCHITTEST 实现无边框边缘缩放；其余事件交给基类。
    bool nativeEvent(const QByteArray& event_type,
                     void* message, qintptr* result) override;
    void changeEvent(QEvent* event) override;
    // ESC 退出全屏。
    void keyPressEvent(QKeyEvent* event) override;
    void resizeEvent(QResizeEvent* event) override;
    // 关闭拦截：关闭即隐藏到托盘。
    void closeEvent(QCloseEvent* event) override;

private:
    void OnMaximizeToggle();
    void OnFullscreenToggle();
    void OnMinimize();

    bool closeToTray_ = true;

    // 根据消息坐标判定当前命中区域（返回 Win32 HT* 常量）。
    long HitTestFromMsg(long x, long y);

    // 同步阴影边距与标题栏按钮图标（在窗口状态变化后调用）。
    void SyncWindowState();

    // 窗口普通（非最大化/全屏）状态下的几何，供还原使用。
    QRect normalGeometry_;

    TitleBar* titleBar_;
    QVBoxLayout* rootLayout_;
};
