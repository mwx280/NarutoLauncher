#include <QApplication>
#include <QMainWindow>
#include <QLabel>

// 占位外壳窗口，用于阶段 0 验证 Qt x64 工具链。
// 阶段 3 将替换为真正的启动器 UI（登录/服务器列表/公告/设置/游戏视图容器）。
int main(int argc, char *argv[])
{
    QApplication app(argc, argv);
    app.setApplicationName(QStringLiteral("naruto_shell"));

    QMainWindow window;
    window.setWindowTitle(QStringLiteral("火影忍者Online 启动器"));
    window.setMinimumSize(1024, 680);
    window.setCentralWidget(new QLabel(QStringLiteral("外壳占位 —— 阶段 0 工具链验证"), &window));
    window.show();

    return app.exec();
}
