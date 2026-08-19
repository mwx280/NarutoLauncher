// 外壳（HuoYinLauncher）—— 阶段 3 主入口
//
// 无边框自绘启动器：
//   - FramelessWindow：去系统边框，自绘标题栏，支持边缘缩放 / 最大化
//   - SideNav：左侧账号管理导航栏（可收缩），账号数据持久化于 QSettings
//   - 右侧为游戏视图占位（阶段 3 后续接入渲染器窗口嵌入）
//
// 账号存储依赖组织名 / 应用名（决定 QSettings 路径），必须在创建
// AccountStore 之前设置。

#include <QApplication>
#include <QGraphicsDropShadowEffect>
#include <QHBoxLayout>
#include <QIcon>
#include <QLabel>
#include <QObject>
#include <QPushButton>
#include <QVBoxLayout>
#include "account.h"
#include "frameless_window.h"
#include "side_nav.h"
#include "tray_icon.h"

namespace {

// 全局样式：现代深色主题 —— 蓝黑基调 + 火焰橙点缀，圆角 + 柔和渐变。
const char* kGlobalStyle = R"(
* { font-family: "Microsoft YaHei"; outline: none; }
QWidget { color: #e6eaf0; font-size: 13px; background: transparent; }

/* 窗口根容器：深色渐变 + 圆角 + 高光描边 */
QFrame#WindowRoot {
    background: qlineargradient(x1:0,y1:0,x2:1,y2:1,
        stop:0 #1c2230, stop:0.55 #171c27, stop:1 #12161f);
    border: 1px solid rgba(255,255,255,0.08);
    border-radius: 14px;
}

/* ---------- 标题栏 ---------- */
QWidget#TitleBar {
    background: rgba(255,255,255,0.02);
    border-bottom: 1px solid rgba(255,255,255,0.06);
    border-top-left-radius: 14px;
    border-top-right-radius: 14px;
}
QLabel#TitleLogo {
    background: transparent;
}
QLabel#TitleText { color: #dbe2ed; font-size: 13px; font-weight: bold; letter-spacing: 1px; }
QPushButton#WinButton {
    background: transparent; border: none; color: #8b95a7;
    font-size: 13px; min-width: 42px; height: 30px; border-radius: 7px;
}
QPushButton#WinButton:hover { background: rgba(255,255,255,0.07); color: #e6eaf0; }
QPushButton#WinButtonClose:hover { background: #e5484d; color: white; }

/* ---------- 左侧导航栏 ---------- */
QFrame#SideNav {
    background: rgba(0,0,0,0.22);
    border-right: 1px solid rgba(255,255,255,0.06);
}
QLabel#SideLogo {
    background: qlineargradient(x1:0,y1:0,x2:0,y2:1, stop:0 #ffb347, stop:1 #ff7a00);
    color: #1a0f00; font-weight: bold; border-radius: 7px; padding: 0 8px;
}
QLabel#SideTitle { color: #ffa13d; font-weight: bold; font-size: 14px; letter-spacing: 0.5px; }
QPushButton#CollapseButton {
    background: transparent; border: none; color: #6b7588; font-size: 12px; border-radius: 6px;
}
QPushButton#CollapseButton:hover { background: rgba(255,255,255,0.06); color: #e6eaf0; }
QLabel#SectionTitle { color: #6b7588; font-size: 11px; font-weight: bold; letter-spacing: 1.5px; }

/* ---------- 按钮 ---------- */
QPushButton#AddAccountButton, QPushButton#SmallButtonPrimary, QPushButton#CardButtonPrimary {
    background: qlineargradient(x1:0,y1:0,x2:0,y2:1, stop:0 #ffb347, stop:1 #ff7a00);
    color: #1a0f00; border: none; border-radius: 8px;
    font-size: 12px; font-weight: bold; padding: 5px 12px;
}
QPushButton#AddAccountButton:hover, QPushButton#SmallButtonPrimary:hover, QPushButton#CardButtonPrimary:hover {
    background: qlineargradient(x1:0,y1:0,x2:0,y2:1, stop:0 #ffc061, stop:1 #ff8c1a);
}
QPushButton#SmallButton {
    background: rgba(255,255,255,0.04); border: 1px solid rgba(255,255,255,0.10);
    border-radius: 8px; color: #aab3c5; font-size: 12px; padding: 5px 12px;
}
QPushButton#SmallButton:hover { background: rgba(255,255,255,0.09); border-color: rgba(255,255,255,0.18); }

/* ---------- 添加账号表单 ---------- */
QWidget#AddForm {
    background: rgba(255,140,26,0.06);
    border: 1px dashed rgba(255,161,61,0.45);
    border-radius: 10px;
}

/* ---------- 输入框 ---------- */
QLineEdit {
    background: rgba(0,0,0,0.30);
    border: 1px solid rgba(255,255,255,0.08);
    border-radius: 8px; padding: 6px 10px; color: #e6eaf0;
}
QLineEdit:hover { border-color: rgba(255,255,255,0.16); }
QLineEdit:focus { border-color: #ffa13d; background: rgba(0,0,0,0.42); }
QLineEdit::placeholder { color: #4d5768; }

/* ---------- 账号卡片 ---------- */
QFrame#AccountCard {
    background: rgba(255,255,255,0.045);
    border: 1px solid rgba(255,255,255,0.07);
    border-radius: 10px;
}
QFrame#AccountCard:hover { background: rgba(255,255,255,0.07); border-color: rgba(255,161,61,0.4); }
QLineEdit#CardRemark {
    background: transparent; border: none; font-weight: bold; padding: 0;
    color: #e6eaf0; font-size: 13px;
}
QLabel#CardTypeLabel {
    color: #ffa13d; font-size: 11px; font-weight: bold;
    border: 1px solid rgba(255,161,61,0.4); border-radius: 4px;
    padding: 1px 5px; background: rgba(255,161,61,0.08);
}
QPushButton#CardDelete {
    background: transparent; border: none; color: #5d677a; font-size: 12px; border-radius: 5px;
}
QPushButton#CardDelete:hover { background: rgba(229,72,77,0.15); color: #ff6b70; }
QPushButton#CardButton {
    background: rgba(255,255,255,0.04); border: 1px solid rgba(255,255,255,0.10);
    border-radius: 7px; color: #aab3c5; font-size: 12px; padding: 4px 12px;
}
QPushButton#CardButton:hover { background: rgba(255,255,255,0.09); border-color: rgba(255,255,255,0.18); }
QCheckBox { color: #aab3c5; font-size: 12px; spacing: 5px; }
QCheckBox::indicator { width: 15px; height: 15px; border-radius: 4px; border: 1px solid rgba(255,255,255,0.2); background: rgba(255,255,255,0.05); }
QCheckBox::indicator:hover { border-color: #ffa13d; }
QCheckBox::indicator:checked { background: qlineargradient(x1:0,y1:0,x2:0,y2:1, stop:0 #ffb347, stop:1 #ff7a00); border-color: #ff7a00; }
QLabel#EmptyLabel { color: #5d677a; font-size: 12px; padding: 30px 10px; }

/* ---------- 滚动条 ---------- */
QScrollArea { border: none; background: transparent; }
QScrollArea QWidget#qt_scrollarea_viewport { background: transparent; }
QScrollArea > QWidget > QWidget { background: transparent; }
QScrollBar:vertical { background: transparent; width: 6px; margin: 2px; }
QScrollBar::handle:vertical { background: rgba(255,255,255,0.10); border-radius: 3px; min-height: 30px; }
QScrollBar::handle:vertical:hover { background: rgba(255,255,255,0.22); }
QScrollBar::add-line, QScrollBar::sub-line { height: 0; }
QLabel#VersionLabel { color: #414a5c; font-size: 10px; }

/* ---------- 游戏视图占位 ---------- */
QFrame#GameView {
    background: qradialgradient(cx:0.5, cy:0.35, radius:0.7,
        fx:0.5, fy:0.35, stop:0 rgba(255,161,61,0.08), stop:1 rgba(0,0,0,0));
}
QLabel#GameTitle {
    color: #ffa13d; font-size: 48px; font-weight: bold; letter-spacing: 8px;
}
QLabel#GameHint { color: #8b95a7; font-size: 13px; letter-spacing: 0.5px; }
QLabel#GameTip { color: #4d5768; font-size: 12px; }
QPushButton#GameStartButton {
    background: qlineargradient(x1:0,y1:0,x2:0,y2:1, stop:0 #ffb347, stop:1 #ff7a00);
    color: #1a0f00; border: none; border-radius: 10px;
    font-size: 15px; font-weight: bold; padding: 10px 44px;
    margin-top: 8px;
}
QPushButton#GameStartButton:hover {
    background: qlineargradient(x1:0,y1:0,x2:0,y2:1, stop:0 #ffc061, stop:1 #ff8c1a);
}
QPushButton#GameStartButton:pressed {
    background: qlineargradient(x1:0,y1:0,x2:0,y2:1, stop:0 #ff8c1a, stop:1 #ff6a00);
}

/* ---------- 状态栏 ---------- */
QWidget#StatusBar { background: rgba(0,0,0,0.25); border-top: 1px solid rgba(255,255,255,0.04); }
QLabel#StatusText { color: #6b7588; font-size: 11px; }
)";

// 创建右侧游戏视图占位。
QWidget* MakeGameView(QWidget* parent) {
    QWidget* right = new QWidget(parent);
    QVBoxLayout* rl = new QVBoxLayout(right);
    rl->setContentsMargins(0, 0, 0, 0);
    rl->setSpacing(0);

    QFrame* view = new QFrame(right);
    view->setObjectName("GameView");
    QVBoxLayout* vl = new QVBoxLayout(view);
    vl->addStretch();

    // 游戏标题（带投影）
    QLabel* title = new QLabel("火影忍者OL", view);
    title->setObjectName("GameTitle");
    title->setAlignment(Qt::AlignCenter);
    QGraphicsDropShadowEffect* shadow = new QGraphicsDropShadowEffect(view);
    shadow->setBlurRadius(40);
    shadow->setColor(QColor(255, 138, 26, 90));
    shadow->setOffset(0, 6);
    title->setGraphicsEffect(shadow);
    vl->addWidget(title);

    QLabel* hint = new QLabel("高性能火影忍者OL 启动器", view);
    hint->setObjectName("GameHint");
    hint->setAlignment(Qt::AlignCenter);
    vl->addWidget(hint);

    // 主操作按钮：开始游戏（阶段 3 暂为占位，后续接真实流程）
    QPushButton* startBtn = new QPushButton("开始游戏", view);
    startBtn->setObjectName("GameStartButton");
    startBtn->setCursor(Qt::PointingHandCursor);
    QObject::connect(startBtn, &QPushButton::clicked, view, []() {
        // 阶段 3 占位：接入渲染器进程与真实登录前，先给出引导提示。
        // TODO: 待阶段 3 IPC 落地后，此处改为拉起 renderer 并启动游戏。
    });
    QHBoxLayout* btnRow = new QHBoxLayout;
    btnRow->addStretch();
    btnRow->addWidget(startBtn);
    btnRow->addStretch();
    vl->addLayout(btnRow);

    QLabel* tip = new QLabel("在左侧「我的账号」添加账号，登录后即可进入游戏", view);
    tip->setObjectName("GameTip");
    tip->setAlignment(Qt::AlignCenter);
    vl->addWidget(tip);
    vl->addStretch();
    rl->addWidget(view, 1);

    QWidget* status = new QWidget(right);
    status->setObjectName("StatusBar");
    status->setFixedHeight(26);
    QHBoxLayout* sl = new QHBoxLayout(status);
    sl->setContentsMargins(14, 0, 14, 0);
    QLabel* st = new QLabel("就绪", status);
    st->setObjectName("StatusText");
    sl->addWidget(st);
    sl->addStretch();
    QLabel* renderer = new QLabel("渲染器: 未启动", status);
    renderer->setObjectName("StatusText");
    sl->addWidget(renderer);
    rl->addWidget(status);

    return right;
}

}  // namespace

int main(int argc, char* argv[]) {
    QApplication app(argc, argv);
    QApplication::setOrganizationName("NarutoLauncher");
    QApplication::setApplicationName("naruto-launcher");
    app.setStyleSheet(kGlobalStyle);

    // 程序图标：苦无（官方 favicon）
    app.setWindowIcon(QIcon(":/assets/app.ico"));

    FramelessWindow win;
    win.SetTitle("火影忍者Online");

    // 主体：左侧导航 + 右侧游戏视图
    QWidget* content = new QWidget(&win);
    QHBoxLayout* body = new QHBoxLayout(content);
    body->setContentsMargins(0, 0, 0, 0);
    body->setSpacing(0);

    SideNav* nav = new SideNav(content);
    body->addWidget(nav);
    body->addWidget(MakeGameView(content), 1);
    win.SetContent(content);

    // 加载已保存账号
    AccountStore store;
    nav->LoadFromStore(store.LoadAll());

    // 系统托盘图标
    TrayIcon tray(&win);
    if (!tray.IsAvailable()) {
        // 系统托盘不可用时不启用关闭即隐藏，保持直接退出。
        win.SetCloseToTray(false);
    }

    win.resize(1180, 720);
    win.show();

    return app.exec();
}
