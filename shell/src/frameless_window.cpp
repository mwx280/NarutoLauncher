#include "frameless_window.h"

#include <QApplication>
#include <QLabel>
#include <QScreen>
#include <QVBoxLayout>

#include "title_bar.h"

#include <windows.h>
#include <windowsx.h>

namespace {

// 阴影边距（像素）：WindowRoot 四周留白，形成阴影/圆角视觉。
const int kShadowMargin = 8;

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

    connect(titleBar_, &TitleBar::MinimizeRequested, this, &QWidget::showMinimized);
    connect(titleBar_, &TitleBar::MaximizeRequested, this, [this]() {
        OnMaximizeToggle();
    });
    connect(titleBar_, &TitleBar::CloseRequested, this, &QWidget::close);
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

void FramelessWindow::OnMaximizeToggle() {
    if (IsActuallyMaximized(this)) {
        showNormal();
    } else {
        showMaximized();
    }
}

void FramelessWindow::changeEvent(QEvent* event) {
    QMainWindow::changeEvent(event);
    // 最大化/还原切换后，WindowRoot 的阴影边距需要相应调整：
    //   最大化时铺满工作区（无留白），还原时恢复阴影。
    if (event->type() == QEvent::WindowStateChange) {
        WindowRoot* root = static_cast<WindowRoot*>(centralWidget());
        if (root) {
            const int m = IsActuallyMaximized(this) ? 0 : kShadowMargin;
            root->setContentsMargins(m, m, m, m);
        }
        titleBar_->SetMaximized(IsActuallyMaximized(this));
    }
}

bool FramelessWindow::nativeEvent(const QByteArray& event_type,
                                  void* message, qintptr* result) {
#ifdef Q_OS_WIN
    MSG* msg = static_cast<MSG*>(message);
    if (msg->message == WM_NCHITTEST) {
        // 仅处理鼠标在客户区边缘时的命中判定，其余交由系统。
        if (IsActuallyMaximized(this))
            return false;  // 最大化时不允许拖边缩放

        long x = GET_X_LPARAM(msg->lParam);
        long y = GET_Y_LPARAM(msg->lParam);
        long ht = HitTestFromMsg(x, y);
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
    return HTCLIENT;
}
