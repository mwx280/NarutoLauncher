#pragma once

#include <QSystemTrayIcon>
#include <QObject>

class QMenu;
class QAction;

class FramelessWindow;

// 系统托盘图标。
//
// 职责：
//   1. 常驻系统托盘，图标为苦无（app.ico）
//   2. 左键单击 / 双击：显示或隐藏主窗口
//   3. 右键菜单：显示主窗口 / 退出
//
// 配合 FramelessWindow 的"关闭即隐藏"：点标题栏关闭按钮后窗口不退出，
// 而是隐藏到托盘，由托盘菜单"退出"真正结束进程。
class TrayIcon : public QObject {
    Q_OBJECT
public:
    explicit TrayIcon(FramelessWindow* window, QObject* parent = nullptr);

    // 是否可用（系统托盘支持）。
    bool IsAvailable() const;

private:
    FramelessWindow* window_;
    QSystemTrayIcon* tray_;
    QMenu* menu_;
    QAction* showAction_;
    QAction* quitAction_;

    void ToggleWindow();
};
