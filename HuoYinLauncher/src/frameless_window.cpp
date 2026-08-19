#include "frameless_window.h"

#include <QApplication>
#include <QCloseEvent>
#include <QKeyEvent>
#include <QScreen>
#include <QVBoxLayout>

#include "title_bar.h"

#include <windows.h>
#include <windowsx.h>

namespace {

// 阴影边距（像素）：WindowRoot 四周留白，形成阴影/圆角视觉。
const int kShadowMargin = 8;

// 标题栏高度（与 TitleBar::setFixedHeight 保持一致）。
const int kTitleBarHeight = 40;

// Win32 非客户区命中区域定义（与 windows.h 中一致，这里显式声明避免歧义）
constexpr long kHtCaption = 2;      // 标题栏（可拖拽移动）
constexpr long kHtLeft = 10;        // 左边缘
constexpr long kHtRight = 11;       // 右边缘
constexpr long kHtTop = 12;         // 上边缘
constexpr long kHtTopLeft = 13;     // 左上角
constexpr long kHtTopRight = 14;    // 右上角
constexpr long kHtBottom = 15;      // 下边缘
constexpr long kHtBottomLeft = 16;  // 左下角
constexpr long kHtBottomRight = 17; // 右下角

// 窗口是否处于最大化状态（依赖当前屏幕工作区判断，避免 showMaximized
// 尚未生效时的时序问题）。
bool IsActuallyMaximized(const QWidget* w) {
    QScreen* screen = QApplication::screenAt(w->geometry().center());
    if (!screen)
        return false;
    QRect avail = screen->availableGeometry();
    return w->geometry() == avail;
}

}  // namespace

WindowRoot::WindowRoot(QWidget* parent) : QFrame(parent) {
    setObjectName("WindowRoot");
}

FramelessWindow::FramelessWindow(QWidget* parent) : QMainWindow(parent) {
    setWindowFlags(Qt::FramelessWindowHint);
    setAttribute(Qt::WA_TranslucentBackground);

    WindowRoot* root = new WindowRoot(this);
    setCentralWidget(root);

    // 根布局：先放标题栏，再放内容区。
    rootLayout_ = new QVBoxLayout(root);
    rootLayout_->setContentsMargins(kShadowMargin, kShadowMargin,
                                    kShadowMargin, kShadowMargin);
    rootLayout_->setSpacing(0);

    // 自绘标题栏
    titleBar_ = new TitleBar(root);
    rootLayout_->addWidget(titleBar_);

    connect(titleBar_, &TitleBar::MinimizeRequested, this, [this]() {
        OnMinimize();
    });
    connect(titleBar_, &TitleBar::MaximizeRequested, this, [this]() {
        OnMaximizeToggle();
    });
    connect(titleBar_, &TitleBar::FullscreenRequested, this, [this]() {
        OnFullscreenToggle();
    });
    connect(titleBar_, &TitleBar::CloseRequested, this, &QWidget::close);

    // 窗口需获得键盘焦点，ESC 才能退出全屏。
    setFocusPolicy(Qt::StrongFocus);

    // 记录初始窗口几何（作为还原/退出全屏的目标位置）。
    normalGeometry_ = QRect(100, 100, 1180, 720);
}

void FramelessWindow::SetTitle(const QString& title) {
    titleBar_->SetTitle(title);
}

void FramelessWindow::SetContent(QWidget* content) {
    // 移除旧内容区（保留标题栏）。
    if (rootLayout_->count() > 1) {
        QLayoutItem* item = rootLayout_->takeAt(1);
        if (item->widget())
            item->widget()->deleteLater();
        delete item;
    }
    rootLayout_->addWidget(content, 1);
}

bool FramelessWindow::IsWindowMaximized() const {
    return IsActuallyMaximized(this);
}

bool FramelessWindow::IsWindowFullscreen() const {
    return isFullScreen();
}

void FramelessWindow::OnMaximizeToggle() {
    if (IsActuallyMaximized(this)) {
        showNormal();
    } else {
        normalGeometry_ = geometry();  // 记录还原位置
        showMaximized();
    }
}

void FramelessWindow::OnFullscreenToggle() {
    if (isFullScreen()) {
        showNormal();
    } else {
        normalGeometry_ = geometry();
        showFullScreen();
    }
}

void FramelessWindow::OnMinimize() {
    showMinimized();
}

void FramelessWindow::RestoreForDrag() {
    showNormal();
    // showNormal 恢复后，normalGeometry_ 保持拖拽前的普通几何用于还原。
}

void FramelessWindow::SetCloseToTray(bool enabled) {
    closeToTray_ = enabled;
}

void FramelessWindow::Quit() {
    closeToTray_ = false;  // 允许关闭
    close();
}

void FramelessWindow::closeEvent(QCloseEvent* event) {
    if (closeToTray_) {
        // 关闭即隐藏到托盘，不退出。
        hide();
        event->ignore();
        return;
    }
    QMainWindow::closeEvent(event);
}

void FramelessWindow::resizeEvent(QResizeEvent* event) {
    QMainWindow::resizeEvent(event);
    // 若窗口尺寸变化期间动画未结束，屏蔽 SyncWindowState 的重复触发；
    // 状态变化最终由 showMaximized/showNormal 的 WindowStateChange 处理。
}

void FramelessWindow::keyPressEvent(QKeyEvent* event) {
    // ESC 退出全屏。
    if (event->key() == Qt::Key_Escape && isFullScreen()) {
        showNormal();
        event->accept();
        return;
    }
    QMainWindow::keyPressEvent(event);
}

void FramelessWindow::SyncWindowState() {
    // 阴影边距：最大化 / 全屏时铺满（无留白），还原时恢复阴影。
    WindowRoot* root = static_cast<WindowRoot*>(centralWidget());
    if (root) {
        const bool edge =
            IsActuallyMaximized(this) || isFullScreen();
        const int m = edge ? 0 : kShadowMargin;
        root->setContentsMargins(m, m, m, m);
    }
    titleBar_->SetMaximized(IsActuallyMaximized(this));
    titleBar_->SetFullscreen(isFullScreen());
}

void FramelessWindow::changeEvent(QEvent* event) {
    QMainWindow::changeEvent(event);
    if (event->type() == QEvent::WindowStateChange) {
        SyncWindowState();
    }
}

bool FramelessWindow::nativeEvent(const QByteArray& event_type,
                                  void* message, qintptr* result) {
#ifdef Q_OS_WIN
    MSG* msg = static_cast<MSG*>(message);
    if (msg->message == WM_NCHITTEST) {
        // 全屏时不响应拖拽/缩放。
        if (isFullScreen())
            return false;

        // 最大化时禁止边缘缩放，但标题栏拖动由 Qt 手动处理（走 HTCLIENT）。
        long x = GET_X_LPARAM(msg->lParam);
        long y = GET_Y_LPARAM(msg->lParam);
        long ht = HitTestFromMsg(x, y);
        if (IsActuallyMaximized(this)) {
            // 最大化下忽略所有边缘命中（不允许缩放），其余交给 Qt。
            if (ht != HTCLIENT)
                return false;
            return false;
        }
        if (ht != HTCLIENT) {
            *result = ht;
            return true;
        }
        return false;
    }
#endif
    return QMainWindow::nativeEvent(event_type, message, result);
}

long FramelessWindow::HitTestFromMsg(long x, long y) {
    // 命中判定基于屏幕坐标（WM_NCHITTEST 的 lParam 为屏幕坐标）。
    QPoint global(x, y);
    const QPoint topLeft = mapToGlobal(QPoint(0, 0));
    const QSize size = this->size();
    const QRect rect(topLeft, size);

    const int b = ResizeBorder();
    const bool left = global.x() >= rect.left() && global.x() < rect.left() + b;
    const bool right = global.x() > rect.right() - b && global.x() <= rect.right();
    const bool top = global.y() >= rect.top() && global.y() < rect.top() + b;
    const bool bottom = global.y() > rect.bottom() - b && global.y() <= rect.bottom();

    // 优先级：先判四角，再判四边。
    if (left && top) return kHtTopLeft;
    if (right && top) return kHtTopRight;
    if (left && bottom) return kHtBottomLeft;
    if (right && bottom) return kHtBottomRight;
    if (left) return kHtLeft;
    if (right) return kHtRight;
    if (top) return kHtTop;
    if (bottom) return kHtBottom;

    // 标题栏区域交给 Qt（返回 HTCLIENT），由 TitleBar 手动拖拽。
    return HTCLIENT;
}
