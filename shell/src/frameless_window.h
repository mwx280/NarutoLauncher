#pragma once

#include <QFrame>
#include <QMainWindow>
#include <QPoint>

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
//   4. 最大化 / 还原切换时调整阴影边距。
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

protected:
    // 拦截 WM_NCHITTEST 实现无边框边缘缩放；其余事件交给基类。
    bool nativeEvent(const QByteArray& event_type,
                     void* message, qintptr* result) override;
    void changeEvent(QEvent* event) override;

private:
    void OnMaximizeToggle();

    // 根据消息坐标判定当前命中区域（返回 Win32 HT* 常量）。
    long HitTestFromMsg(long x, long y);

    TitleBar* titleBar_;
    QVBoxLayout* rootLayout_;
};
