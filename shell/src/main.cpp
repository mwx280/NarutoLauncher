// 外壳（Shell）—— 阶段 3 主入口
//
// 无边框自绘启动器：
//   - FramelessWindow：去系统边框，自绘标题栏，支持边缘缩放 / 最大化
//   - SideNav：左侧账号管理导航栏（可收缩），账号数据持久化于 QSettings
//   - 右侧为游戏视图占位（阶段 3 后续接入渲染器窗口嵌入）
//
// 账号存储依赖组织名 / 应用名（决定 QSettings 路径），必须在创建
// AccountStore 之前设置。

#include <QApplication>
#include <QHBoxLayout>
#include <QLabel>
#include <QVBoxLayout>

#include "account.h"
#include "frameless_window.h"
#include "side_nav.h"

namespace {

// 全局样式：深色主题 + 圆角 + 渐变，塑造"无边框自绘"观感。
const char* kGlobalStyle = R"(
* { font-family: "Microsoft YaHei"; outline: none; }
QWidget { color: #e8ecf1; font-size: 13px; }

/* 窗口根容器：圆角 + 阴影边距（配合透明背景） */
QFrame#WindowRoot {
    background: qlineargradient(x1:0,y1:0,x2:1,y2:1, stop:0 #262f40, stop:1 #141922);
    border: 1px solid #3a4457;
    border-radius: 10px;
}

/* 标题栏 */
QWidget#TitleBar {
    background: transparent;
    border-top-left-radius: 10px;
    border-top-right-radius: 10px;
}
QLabel#TitleLogo {
    background: qlineargradient(x1:0,y1:0,x2:0,y2:1, stop:0 #ffa13d, stop:1 #ff7a00);
    color: #1a1408; font-weight: bold; border-radius: 5px; padding: 0 8px;
}
QLabel#TitleText { color: #c9d2e0; font-size: 12px; }
QPushButton#WinButton {
    background: transparent; border: none; color: #8b95a7;
    font-size: 13px; min-width: 40px; height: 28px;
}
QPushButton#WinButton:hover { background: rgba(255,255,255,0.08); color: #e8ecf1; }
QPushButton#WinButtonClose:hover { background: #d64545; color: white; }

/* 左侧导航栏 */
QFrame#SideNav { background: rgba(0,0,0,0.28); border-right: 1px solid rgba(255,255,255,0.05); }
QLabel#SideLogo {
    background: qlineargradient(x1:0,y1:0,x2:0,y2:1, stop:0 #ffa13d, stop:1 #ff7a00);
    color: #1a1408; font-weight: bold; border-radius: 5px; padding: 0 7px;
}
QLabel#SideTitle { color: #ff8c1a; font-weight: bold; font-size: 13px; }
QPushButton#CollapseButton {
    background: transparent; border: none; color: #8b95a7; font-size: 12px;
}
QPushButton#CollapseButton:hover { color: #e8ecf1; }
QLabel#SectionTitle { color: #6b7689; font-size: 11px; font-weight: bold; letter-spacing: 1px; }

/* 按钮 */
QPushButton#AddAccountButton, QPushButton#SmallButtonPrimary {
    background: qlineargradient(x1:0,y1:0,x2:0,y2:1, stop:0 #ffa13d, stop:1 #ff7a00);
    color: #1a1408; border: none; border-radius: 5px;
    font-size: 12px; font-weight: bold; padding: 4px 10px;
}
QPushButton#AddAccountButton:hover, QPushButton#SmallButtonPrimary:hover {
    background: qlineargradient(x1:0,y1:0,x2:0,y2:1, stop:0 #ffb055, stop:1 #ff8c1a);
}
QPushButton#SmallButton {
    background: transparent; border: 1px solid rgba(255,255,255,0.15);
    border-radius: 5px; color: #c9d2e0; font-size: 12px; padding: 4px 10px;
}
QPushButton#SmallButton:hover { background: rgba(255,255,255,0.08); }

/* 添加账号表单 */
QWidget#AddForm {
    background: rgba(255,140,26,0.08);
    border: 1px dashed rgba(255,140,26,0.5);
    border-radius: 8px;
}

/* 输入框 */
QLineEdit {
    background: rgba(0,0,0,0.35);
    border: 1px solid rgba(255,255,255,0.10);
    border-radius: 6px; padding: 5px 8px; color: #e8ecf1;
}
QLineEdit:focus { border: 1px solid #ff8c1a; }
QLineEdit::placeholder { color: #4b5668; }

/* 账号卡片 */
QFrame#AccountCard {
    background: rgba(255,255,255,0.04);
    border: 1px solid rgba(255,255,255,0.07);
    border-radius: 8px;
}
QFrame#AccountCard:hover { background: rgba(255,255,255,0.06); border-color: rgba(255,140,26,0.5); }
QLineEdit#CardRemark {
    background: transparent; border: none; font-weight: bold; padding: 0;
}
QPushButton#CardDelete {
    background: transparent; border: none; color: #5f6c82; font-size: 12px;
}
QPushButton#CardDelete:hover { color: #e05a5a; }
QPushButton#CardButton {
    background: transparent; border: 1px solid rgba(255,255,255,0.15);
    border-radius: 5px; color: #c9d2e0; font-size: 12px; padding: 3px 10px;
}
QPushButton#CardButton:hover { background: rgba(255,255,255,0.08); }
QPushButton#CardButtonPrimary {
    background: qlineargradient(x1:0,y1:0,x2:0,y2:1, stop:0 #ffa13d, stop:1 #ff7a00);
    color: #1a1408; border: none; border-radius: 5px;
    font-size: 12px; font-weight: bold; padding: 3px 12px;
}
QPushButton#CardButtonPrimary:hover {
    background: qlineargradient(x1:0,y1:0,x2:0,y2:1, stop:0 #ffb055, stop:1 #ff8c1a);
}
QCheckBox { color: #c9d2e0; font-size: 12px; spacing: 5px; }

/* 滚动条 */
QScrollArea { border: none; background: transparent; }
QScrollBar:vertical { background: transparent; width: 8px; }
QScrollBar::handle:vertical { background: rgba(255,255,255,0.12); border-radius: 4px; min-height: 30px; }
QScrollBar::handle:vertical:hover { background: rgba(255,255,255,0.2); }
QScrollBar::add-line, QScrollBar::sub-line { height: 0; }
QLabel#VersionLabel { color: #3f4a5c; font-size: 10px; }

/* 游戏视图占位 */
QFrame#GameView { background: transparent; }
QLabel#GameTitle { color: #ff8c1a; font-size: 42px; font-weight: bold; letter-spacing: 4px; }
QLabel#GameHint { color: #9aa7bd; font-size: 13px; }
QLabel#GameTip { color: #4b5668; font-size: 12px; }

/* 状态栏 */
QWidget#StatusBar { background: rgba(0,0,0,0.2); }
QLabel#StatusText { color: #5f6c82; font-size: 11px; }
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
    QLabel* title = new QLabel("火影忍者OL", view);
    title->setObjectName("GameTitle");
    title->setAlignment(Qt::AlignCenter);
    vl->addWidget(title);
    QLabel* hint = new QLabel("游戏视图区 —— 渲染器窗口将嵌入此处", view);
    hint->setObjectName("GameHint");
    hint->setAlignment(Qt::AlignCenter);
    vl->addWidget(hint);
    QLabel* tip = new QLabel("在左侧「我的账号」添加账号并点击「开始游戏」", view);
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

    FramelessWindow win;
    win.SetTitle("火影忍者OL 启动器");

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

    win.resize(1180, 720);
    win.show();

    return app.exec();
}
