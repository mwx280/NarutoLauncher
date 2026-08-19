#include "frameless_window.h"

#include <QApplication>
#include <QEasingCurve>
#include <QKeyEvent>
#include <QLabel>
#include <QParallelAnimationGroup>
#include <QPropertyAnimation>
#include <QScreen>
#include <QVBoxLayout>

#include <functional>

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
        OnMinimizeWithAnimation();
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
    if (animating_)
        return;

    if (IsActuallyMaximized(this)) {
        // 还原：动画回原来的普通几何。
        AnimateGeometry(normalGeometry_, [this]() {
            showNormal();
        });
    } else {
        // 最大化：保存当前几何，动画到工作区。
        normalGeometry_ = geometry();
        QScreen* screen = QApplication::screenAt(geometry().center());
        if (!screen)
            return;
        const QRect target = screen->availableGeometry();
        AnimateGeometry(target, [this]() {
            showMaximized();
        });
    }
}

void FramelessWindow::OnFullscreenToggle() {
    if (animating_)
        return;

    if (isFullScreen()) {
        AnimateGeometry(normalGeometry_, [this]() {
            showNormal();
        });
    } else {
        normalGeometry_ = geometry();
        QScreen* screen = QApplication::screenAt(geometry().center());
        if (!screen)
            return;
        // 全屏目标：整个屏幕（含任务栏区域）。
        const QRect target = screen->geometry();
        AnimateGeometry(target, [this]() {
            showFullScreen();
        });
    }
}

void FramelessWindow::OnMinimizeWithAnimation() {
    if (animating_)
        return;

    // 缩小 + 淡出动画，结束后真正最小化。
    const QRect from = geometry();
    // 缩小到屏幕底部居中的一个窄条。
    QScreen* screen = QApplication::screenAt(geometry().center());
    if (!screen) {
        showMinimized();
        return;
    }
    const QRect sr = screen->geometry();
    QRect to(sr.left() + sr.width() / 2 - 40, sr.bottom() - 2, 80, 2);

    QPropertyAnimation* geo = new QPropertyAnimation(this, "geometry", this);
    geo->setDuration(180);
    geo->setStartValue(from);
    geo->setEndValue(to);
    geo->setEasingCurve(QEasingCurve::InCubic);

    QPropertyAnimation* opacity = new QPropertyAnimation(this, "windowOpacity", this);
    opacity->setDuration(180);
    opacity->setStartValue(1.0);
    opacity->setEndValue(0.0);
    opacity->setEasingCurve(QEasingCurve::InCubic);

    // 两个动画并行，结束一起触发最小化。
    auto group = new QParallelAnimationGroup(this);
    group->addAnimation(geo);
    group->addAnimation(opacity);
    animating_ = true;
    connect(group, &QParallelAnimationGroup::finished, this, [this, group]() {
        animating_ = false;
        setWindowOpacity(1.0);  // 还原透明度，供下次显示
        showMinimized();
        group->deleteLater();
    });
    group->start();
}

void FramelessWindow::RestoreForDrag() {
    if (animating_)
        return;  // 动画中不打断
    showNormal();
    // showNormal 恢复后，normalGeometry_ 保持拖拽前的普通几何用于还原。
}

void FramelessWindow::AnimateGeometry(const QRect& to,
                                      std::function<void()> finish) {
    const QRect from = geometry();
    QPropertyAnimation* anim = new QPropertyAnimation(this, "geometry", this);
    anim->setDuration(220);
    anim->setStartValue(from);
    anim->setEndValue(to);
    anim->setEasingCurve(QEasingCurve::OutCubic);
    animating_ = true;
    connect(anim, &QPropertyAnimation::finished, this, [this, finish, anim]() {
        animating_ = false;
        if (finish)
            finish();
        anim->deleteLater();
    });
    anim->start();
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
