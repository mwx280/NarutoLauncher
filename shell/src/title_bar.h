#pragma once

#include <QWidget>

class QHBoxLayout;
class QLabel;
class QPushButton;

// 自绘标题栏。
//
// 职责：
//   1. 显示应用名（左侧 Logo + 标题）
//   2. 提供窗口控制按钮（最小化 / 最大化还原 / 关闭）
//   3. 鼠标拖拽标题栏移动窗口，双击切换最大化/还原
//
// 通过信号与 FramelessWindow 协作，不直接操作窗口（便于复用与测试）。
class TitleBar : public QWidget {
    Q_OBJECT
public:
    explicit TitleBar(QWidget* parent = nullptr);

    // 设置标题栏文字。
    void SetTitle(const QString& title);

    // 更新最大化按钮图标（由外部在窗口状态变化时调用）。
    void SetMaximized(bool maximized);

signals:
    void MinimizeRequested();
    void MaximizeRequested();
    void CloseRequested();

protected:
    void mousePressEvent(QMouseEvent* event) override;
    void mouseMoveEvent(QMouseEvent* event) override;
    void mouseReleaseEvent(QMouseEvent* event) override;
    void mouseDoubleClickEvent(QMouseEvent* event) override;

private:
    QLabel* title_;
    QPushButton* maximizeBtn_;
    bool dragging_ = false;
    QPoint dragOffset_;
};
