#include "title_bar.h"

#include <QHBoxLayout>
#include <QLabel>
#include <QMouseEvent>
#include <QPainter>
#include <QPixmap>
#include <QPolygonF>
#include <QPushButton>

#include <functional>

#include "frameless_window.h"

namespace {

// 窗口控制按钮的统一尺寸。
constexpr int kWinBtnWidth = 44;
constexpr int kWinBtnHeight = 30;

// 图标画布尺寸（像素）。
constexpr int kIconSize = 16;

// 统一绘制窗口控制图标（QPainter 矢量绘制，保证各图标视觉大小一致）。
QIcon MakeWinIcon(std::function<void(QPainter&)> draw) {
    QPixmap pm(kIconSize, kIconSize);
    pm.fill(Qt::transparent);
    QPainter p(&pm);
    p.setRenderHint(QPainter::Antialiasing);
    p.setRenderHint(QPainter::SmoothPixmapTransform);
    QPen pen;
    pen.setWidthF(1.6);
    pen.setCapStyle(Qt::RoundCap);
    pen.setJoinStyle(Qt::RoundJoin);
    pen.setColor(Qt::white);
    p.setPen(pen);
    p.translate(0.5, 0.5);  // 对齐像素网格，避免线宽模糊
    draw(p);
    p.end();
    QIcon icon(pm);
    // 作为 alpha 蒙版使用，颜色由按钮文字色（QSS color）决定，
    // 悬停时文字变亮则图标同步变亮。
    icon.setIsMask(true);
    return icon;
}

// 最小化：单条横线。
QIcon MinimizeIcon() {
    return MakeWinIcon([](QPainter& p) {
        p.drawLine(QPointF(3, 8), QPointF(13, 8));
    });
}

// 最大化：单个圆角方框。
QIcon MaximizeIcon() {
    return MakeWinIcon([](QPainter& p) {
        p.drawRoundedRect(QRectF(3, 3, 10, 10), 1.5, 1.5);
    });
}

// 还原：两个叠放方框。
QIcon RestoreIcon() {
    return MakeWinIcon([](QPainter& p) {
        p.drawRoundedRect(QRectF(4, 5, 8, 8), 1.5, 1.5);
        p.drawRoundedRect(QRectF(4, 3, 9, 8), 1.5, 1.5);
    });
}

// 进入全屏：四角向外箭头。
QIcon FullscreenIcon() {
    return MakeWinIcon([](QPainter& p) {
        p.drawPolyline(QPolygonF({QPointF(3, 7), QPointF(3, 3), QPointF(7, 3)}));
        p.drawPolyline(QPolygonF({QPointF(9, 3), QPointF(13, 3), QPointF(13, 7)}));
        p.drawPolyline(QPolygonF({QPointF(3, 9), QPointF(3, 13), QPointF(7, 13)}));
        p.drawPolyline(QPolygonF({QPointF(9, 13), QPointF(13, 13), QPointF(13, 9)}));
    });
}

// 退出全屏：四角向内箭头。
QIcon FullscreenExitIcon() {
    return MakeWinIcon([](QPainter& p) {
        p.drawPolyline(QPolygonF({QPointF(5, 3), QPointF(5, 5), QPointF(3, 5)}));
        p.drawPolyline(QPolygonF({QPointF(13, 5), QPointF(11, 5), QPointF(11, 3)}));
        p.drawPolyline(QPolygonF({QPointF(5, 13), QPointF(5, 11), QPointF(3, 11)}));
        p.drawPolyline(QPolygonF({QPointF(13, 11), QPointF(11, 11), QPointF(11, 13)}));
    });
}

// 关闭：X 形。
QIcon CloseIcon() {
    return MakeWinIcon([](QPainter& p) {
        p.drawLine(QPointF(4, 4), QPointF(12, 12));
        p.drawLine(QPointF(12, 4), QPointF(4, 12));
    });
}

// 创建统一尺寸、带图标的窗口控制按钮。
QPushButton* MakeWinButton(const QIcon& icon, QWidget* parent) {
    QPushButton* btn = new QPushButton(parent);
    btn->setObjectName("WinButton");
    btn->setIcon(icon);
    btn->setIconSize(QSize(kIconSize, kIconSize));
    btn->setFixedSize(kWinBtnWidth, kWinBtnHeight);
    btn->setCursor(Qt::ArrowCursor);
    return btn;
}

}  // namespace

TitleBar::TitleBar(QWidget* parent) : QWidget(parent) {
    setObjectName("TitleBar");
    setFixedHeight(40);

    QHBoxLayout* lay = new QHBoxLayout(this);
    lay->setContentsMargins(12, 0, 4, 0);
    lay->setSpacing(2);

    QLabel* logo = new QLabel("火", this);
    logo->setObjectName("TitleLogo");
    lay->addWidget(logo);

    title_ = new QLabel("火影忍者Online 启动器", this);
    title_->setObjectName("TitleText");
    lay->addWidget(title_);
    lay->addStretch();

    // 最小化 / 最大化 / 全屏 / 关闭（统一尺寸 + 矢量图标）
    QPushButton* minimize = MakeWinButton(MinimizeIcon(), this);
    maximizeBtn_ = MakeWinButton(MaximizeIcon(), this);
    fullscreenBtn_ = MakeWinButton(FullscreenIcon(), this);
    QPushButton* close = MakeWinButton(CloseIcon(), this);
    close->setObjectName("WinButtonClose");  // 关闭按钮特殊悬停样式

    lay->addWidget(minimize);
    lay->addWidget(maximizeBtn_);
    lay->addWidget(fullscreenBtn_);
    lay->addWidget(close);

    connect(minimize, &QPushButton::clicked, this,
            &TitleBar::MinimizeRequested);
    connect(maximizeBtn_, &QPushButton::clicked, this,
            &TitleBar::MaximizeRequested);
    connect(fullscreenBtn_, &QPushButton::clicked, this,
            &TitleBar::FullscreenRequested);
    connect(close, &QPushButton::clicked, this, &TitleBar::CloseRequested);
}

void TitleBar::SetTitle(const QString& title) {
    title_->setText(title);
}

void TitleBar::SetMaximized(bool maximized) {
    maximizeBtn_->setIcon(maximized ? RestoreIcon() : MaximizeIcon());
}

void TitleBar::SetFullscreen(bool fullscreen) {
    fullscreenBtn_->setIcon(fullscreen ? FullscreenExitIcon()
                                       : FullscreenIcon());
}

void TitleBar::mousePressEvent(QMouseEvent* event) {
    if (event->button() == Qt::LeftButton) {
        dragging_ = true;
        dragOffset_ = event->globalPosition().toPoint()
                      - window()->frameGeometry().topLeft();
        event->accept();
        return;
    }
    QWidget::mousePressEvent(event);
}

void TitleBar::mouseMoveEvent(QMouseEvent* event) {
    if (dragging_ && (event->buttons() & Qt::LeftButton)) {
        // 拖拽时若窗口处于最大化，先还原到原始位置再继续拖动。
        FramelessWindow* win = qobject_cast<FramelessWindow*>(window());
        if (win && win->IsWindowMaximized()) {
            win->RestoreForDrag();
            // 还原后重新计算偏移，保持鼠标相对窗口位置不变
            dragOffset_ = event->globalPosition().toPoint()
                          - win->frameGeometry().topLeft();
        }
        window()->move(event->globalPosition().toPoint() - dragOffset_);
        event->accept();
        return;
    }
    QWidget::mouseMoveEvent(event);
}

void TitleBar::mouseReleaseEvent(QMouseEvent* event) {
    dragging_ = false;
    QWidget::mouseReleaseEvent(event);
}

void TitleBar::mouseDoubleClickEvent(QMouseEvent* event) {
    if (event->button() == Qt::LeftButton) {
        emit MaximizeRequested();
        event->accept();
        return;
    }
    QWidget::mouseDoubleClickEvent(event);
}
