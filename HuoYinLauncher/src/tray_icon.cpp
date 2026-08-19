#include "tray_icon.h"

#include <QAction>
#include <QApplication>
#include <QMenu>

#include "frameless_window.h"

TrayIcon::TrayIcon(FramelessWindow* window, QObject* parent)
    : QObject(parent), window_(window) {
    tray_ = new QSystemTrayIcon(this);
    tray_->setIcon(QIcon(":/assets/app.ico"));
    tray_->setToolTip("火影忍者Online");

    // 右键菜单
    menu_ = new QMenu();
    showAction_ = menu_->addAction("显示主窗口");
    quitAction_ = menu_->addAction("退出");
    tray_->setContextMenu(menu_);

    connect(showAction_, &QAction::triggered, this, [this]() {
        window_->showNormal();
        window_->raise();
        window_->activateWindow();
    });
    connect(quitAction_, &QAction::triggered, qApp, &QCoreApplication::quit);

    // 左键单击切换显示/隐藏；双击显示
    connect(tray_, &QSystemTrayIcon::activated, this,
            [this](QSystemTrayIcon::ActivationReason reason) {
        if (reason == QSystemTrayIcon::Trigger ||
            reason == QSystemTrayIcon::DoubleClick) {
            ToggleWindow();
        }
    });

    tray_->show();
}

bool TrayIcon::IsAvailable() const {
    return QSystemTrayIcon::isSystemTrayAvailable();
}

void TrayIcon::ToggleWindow() {
    if (window_->isVisible() && !window_->isMinimized()) {
        window_->hide();
    } else {
        window_->showNormal();
        window_->raise();
        window_->activateWindow();
    }
}
