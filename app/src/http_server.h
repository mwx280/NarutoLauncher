#pragma once

// 必须在任何 windows.h 之前引入 winsock2，且阻止 windows.h 自动带 winsock.h，
// 否则 winsock.h 与 winsock2.h 冲突（sockaddr 等重复定义）。
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <winsock2.h>

#include <string>
#include <thread>
#include <atomic>
#include <memory>

// 内嵌极简 HTTP 静态文件服务器。
//
// 用途：CEF 宿主加载 Vue UI 构建产物（dist/）的本地 HTTP 服务。
// 为什么不用 file://：Vite 产物为 ES module（type="module" crossorigin），
// file:// 协议下模块加载受 CORS 限制会黑屏；HTTP 服务无此问题。
//
// 仅支持 GET 静态文件，MIME 按扩展名映射，路径严格限制在根目录内
// （防目录穿越）。单线程 + 短连接，足够启动器 UI 使用。
class StaticHttpServer {
public:
    StaticHttpServer();
    ~StaticHttpServer();

    // 设置服务根目录（dist 路径）。
    void SetRoot(const std::string& root);

    // 启动监听，返回分配的端口（0 表示失败）。
    unsigned short Start();

    // 停止并释放资源。
    void Stop();

    // 是否正在运行。
    bool IsRunning() const { return listening_; }

private:
    // 单连接处理（阻塞，单线程串行）。返回是否继续监听。
    bool ServeOnce();

    std::string root_;
    std::wstring rootW_;            // 宽字符根目录（中文路径必须用宽字符访问文件）
    SOCKET listen_sock_ = INVALID_SOCKET;
    bool listening_ = false;
    std::unique_ptr<std::thread> thread_;   // 后台服务线程
};
