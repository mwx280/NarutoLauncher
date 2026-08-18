#include "title_bar.h"

#include <QHBoxLayout>
#include <QLabel>
#include <QMouseEvent>
#include <QPushButton>

#include "frameless_window.h"

TitleBar::TitleBar(QWidget* parent) : QWidget(parent) {
    setObjectName("TitleBar");
    setFixedHeight(40);
    // 标题栏区域不接收鼠标事件穿透（但自身可拖拽），用于触发 nativeEvent
    // 的 WM_NCHITTEST 前先由 Qt 捕获拖拽事件。

    QHBoxLayout* lay = new QHBoxLayout(this);
    lay->setContentsMargins(12, 0, 4, 0);
    lay->setSpacing(8);

    QLabel* logo = new QLabel("火", this);
    logo->setObjectName("TitleLogo");
    lay->addWidget(logo);

    title_ = new QLabel("火影忍者Online 启动器", this);
    title_->setObjectName("TitleText");
    lay->addWidget(title_);
    lay->addStretch();

    QPushButton* minimize = new QPushButton("—", this);
    minimize->setObjectName("WinButton");
    QPushButton* maximize = new QPushButton("□", this);
    maximize->setObjectName("WinButton");
    QPushButton* close = new QPushButton("✕", this);
    close->setObjectName("WinButton");
    close->setProperty("class", "Close");  // 关闭按钮特殊样式
    close->setObjectName("WinButtonClose");

    lay->addWidget(minimize);
    maximizeBtn_ = maximize;
    lay->addWidget(maximize);
    lay->addWidget(close);

    connect(minimize, &QPushButton::clicked, this,
            &TitleBar::MinimizeRequested);
    connect(maximize, &QPushButton::clicked, this,
            &TitleBar::MaximizeRequested);
    connect(close, &QPushButton::clicked, this, &TitleBar::CloseRequested);
}

void TitleBar::SetTitle(const QString& title) {
    title_->setText(title);
}

void TitleBar::SetMaximized(bool maximized) {
    maximizeBtn_->setText(maximized ? "❐" : "□");
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
            win->showNormal();
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
