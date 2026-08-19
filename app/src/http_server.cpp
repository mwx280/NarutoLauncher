#include "http_server.h"

#include <windows.h>
#include <ws2tcpip.h>

#include <cstdio>
#include <fstream>
#include <sstream>
#include <vector>

#include "app_log.h"

#pragma comment(lib, "ws2_32.lib")

namespace {

// 根据扩展名返回 MIME 类型。
const char* MimeFor(const std::string& path) {
    std::string ext;
    size_t dot = path.find_last_of('.');
    if (dot != std::string::npos)
        ext = path.substr(dot);
    // 转小写
    for (auto& c : ext)
        c = static_cast<char>(tolower(static_cast<unsigned char>(c)));

    if (ext == ".html") return "text/html; charset=utf-8";
    if (ext == ".js")   return "application/javascript; charset=utf-8";
    if (ext == ".css")  return "text/css; charset=utf-8";
    if (ext == ".json") return "application/json";
    if (ext == ".png")  return "image/png";
    if (ext == ".jpg" || ext == ".jpeg") return "image/jpeg";
    if (ext == ".gif")  return "image/gif";
    if (ext == ".svg")  return "image/svg+xml";
    if (ext == ".ico")  return "image/x-icon";
    if (ext == ".woff") return "font/woff";
    if (ext == ".woff2") return "font/woff2";
    if (ext == ".ttf")  return "font/ttf";
    if (ext == ".map")  return "application/json";
    if (ext == ".wasm") return "application/wasm";
    return "application/octet-stream";
}

// URL 解码（用于把 %20 等还原，同时防止目录穿越的路径处理）。
std::string UrlDecode(const std::string& s) {
    std::string out;
    for (size_t i = 0; i < s.size(); ++i) {
        if (s[i] == '%' && i + 2 < s.size()) {
            auto hex = [](char c) -> int {
                if (c >= '0' && c <= '9') return c - '0';
                if (c >= 'a' && c <= 'f') return c - 'a' + 10;
                if (c >= 'A' && c <= 'F') return c - 'A' + 10;
                return -1;
            };
            int hi = hex(s[i + 1]), lo = hex(s[i + 2]);
            if (hi >= 0 && lo >= 0) {
                out.push_back(static_cast<char>((hi << 4) | lo));
                i += 2;
                continue;
            }
        }
        out.push_back(s[i]);
    }
    return out;
}

// 发送完整数据（循环发送直到全部发完）。
bool SendAll(SOCKET sock, const char* data, size_t len) {
    size_t sent = 0;
    while (sent < len) {
        int n = send(sock, data + sent, static_cast<int>(len - sent), 0);
        if (n <= 0)
            return false;
        sent += static_cast<size_t>(n);
    }
    return true;
}

}  // namespace

StaticHttpServer::StaticHttpServer() {}

StaticHttpServer::~StaticHttpServer() {
    Stop();
}

void StaticHttpServer::SetRoot(const std::string& root) {
    root_ = root;
    // 统一末尾分隔符
    if (!root_.empty() && root_.back() != '\\' && root_.back() != '/')
        root_.push_back('\\');
    // 转宽字符（中文路径用宽字符 API 打开文件，避免 UTF-8/GBK 乱码）
    rootW_.clear();
    int len = MultiByteToWideChar(CP_UTF8, 0, root_.c_str(),
                                  static_cast<int>(root_.size()),
                                  nullptr, 0);
    if (len > 0) {
        rootW_.resize(len);
        MultiByteToWideChar(CP_UTF8, 0, root_.c_str(),
                            static_cast<int>(root_.size()),
                            &rootW_[0], len);
    }
    AppLog::Write("SetRoot: utf8_len=%zu, wide_len=%d, utf8_bytes=[%s]",
                  root_.size(), len, root_.c_str());
}

unsigned short StaticHttpServer::Start() {
    WSADATA wsa;
    if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0)
        return 0;

    listen_sock_ = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (listen_sock_ == INVALID_SOCKET)
        return 0;

    // 端口 0 → 系统自动分配
    sockaddr_in addr = {};
    addr.sin_family = AF_INET;
    addr.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
    addr.sin_port = htons(0);

    if (bind(listen_sock_, reinterpret_cast<sockaddr*>(&addr),
             sizeof(addr)) != 0) {
        closesocket(listen_sock_);
        listen_sock_ = INVALID_SOCKET;
        return 0;
    }
    if (listen(listen_sock_, SOMAXCONN) != 0) {
        closesocket(listen_sock_);
        listen_sock_ = INVALID_SOCKET;
        return 0;
    }

    // 读取实际分配的端口
    int len = sizeof(addr);
    getsockname(listen_sock_, reinterpret_cast<sockaddr*>(&addr), &len);
    listening_ = true;
    unsigned short port = ntohs(addr.sin_port);

    // 后台线程持续 accept 并服务连接，直到 Stop()。
    thread_.reset(new std::thread([this]() {
        while (listening_) {
            if (!ServeOnce())
                break;
        }
    }));
    return port;
}

void StaticHttpServer::Stop() {
    listening_ = false;
    // 唤醒阻塞的 accept：关闭监听 socket 使 accept 返回错误。
    if (listen_sock_ != INVALID_SOCKET) {
        closesocket(listen_sock_);
        listen_sock_ = INVALID_SOCKET;
    }
    if (thread_ && thread_->joinable()) {
        thread_->join();
        thread_.reset();
    }
    WSACleanup();
}

bool StaticHttpServer::ServeOnce() {
    if (!listening_)
        return false;

    // 非阻塞 accept：listen_sock_ 保持可立即返回，便于 Stop 时退出。
    // 实际用阻塞 + 短超时，简化实现。
    SOCKET client = accept(listen_sock_, nullptr, nullptr);
    if (client == INVALID_SOCKET)
        return listening_;

    // 读请求行 + 头（简化：只读首行请求行，忽略其余直到空行）
    char buf[8192];
    int recv_total = 0;
    bool got_request_line = false;
    std::string request_line;
    while (recv_total < static_cast<int>(sizeof(buf)) - 1) {
        int n = recv(client, buf + recv_total,
                     static_cast<int>(sizeof(buf)) - 1 - recv_total, 0);
        if (n <= 0)
            break;
        recv_total += n;
        buf[recv_total] = '\0';
        // 请求行以 \r\n 结束
        std::string all(buf);
        size_t line_end = all.find("\r\n");
        if (line_end != std::string::npos) {
            request_line = all.substr(0, line_end);
            got_request_line = true;
            break;
        }
    }

    std::string response;
    if (!got_request_line) {
        response = "HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\n\r\n";
        SendAll(client, response.data(), response.size());
        closesocket(client);
        return listening_;
    }

    // 解析请求行：METHOD SP PATH SP HTTP/x.y
    std::istringstream iss(request_line);
    std::string method, path, version;
    iss >> method >> path >> version;
    AppLog::Write("HTTP %s %s", method.c_str(), path.c_str());

    if (method != "GET" && method != "HEAD") {
        response = "HTTP/1.1 405 Method Not Allowed\r\nContent-Length: 0\r\n\r\n";
        SendAll(client, response.data(), response.size());
        closesocket(client);
        return listening_;
    }

    // 去掉查询串，URL 解码
    size_t q = path.find('?');
    if (q != std::string::npos)
        path = path.substr(0, q);
    std::string rel = UrlDecode(path);
    if (rel.empty() || rel[0] != '/')
        rel = "/" + rel;
    // 默认首页
    if (rel == "/")
        rel = "/index.html";

    // 防目录穿越：解析后必须以根目录开头
    std::string full = root_ + rel.substr(1);
    // 规范化检查：拒绝任何 .. 路径段
    {
        std::string norm;
        std::istringstream path_ss(full);
        std::string seg;
        bool bad = false;
        while (std::getline(path_ss, seg, '/')) {
            // 也按 \ 切分（Windows 路径）
            std::istringstream seg_ss(seg);
            std::string part;
            while (std::getline(seg_ss, part, '\\')) {
                if (part == "..") { bad = true; break; }
            }
            if (bad) break;
        }
        if (bad) {
            response = "HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n";
            SendAll(client, response.data(), response.size());
            closesocket(client);
            return listening_;
        }
    }

    // 读文件（用宽字符路径，中文目录才能正确打开）
    std::wstring fullW = rootW_;
    // rel 是 UTF-8，转宽字符追加
    int rel_len = MultiByteToWideChar(CP_UTF8, 0, rel.c_str(),
                                      static_cast<int>(rel.size()),
                                      nullptr, 0);
    std::wstring relW;
    if (rel_len > 0) {
        relW.resize(rel_len);
        MultiByteToWideChar(CP_UTF8, 0, rel.c_str(),
                            static_cast<int>(rel.size()),
                            &relW[0], rel_len);
    }
    // 去掉开头的 '/'
    if (!relW.empty() && relW[0] == L'/')
        relW = relW.substr(1);
    fullW += relW;

    std::vector<char> body;
    bool exists = false;
    HANDLE hFile = CreateFileW(fullW.c_str(), GENERIC_READ,
                               FILE_SHARE_READ, nullptr, OPEN_EXISTING,
                               FILE_ATTRIBUTE_NORMAL, nullptr);
    if (hFile != INVALID_HANDLE_VALUE) {
        LARGE_INTEGER size;
        if (GetFileSizeEx(hFile, &size) && size.QuadPart > 0) {
            body.resize(static_cast<size_t>(size.QuadPart));
            DWORD read = 0;
            ReadFile(hFile, body.data(), static_cast<DWORD>(body.size()),
                     &read, nullptr);
            body.resize(read);
        }
        CloseHandle(hFile);
        exists = true;
    }
    AppLog::Write("读文件: %S (%s, %zu bytes)", fullW.c_str(),
                  exists ? "存在" : "不存在", body.size());

    if (!exists) {
        std::string msg = "Not Found";
        response = "HTTP/1.1 404 Not Found\r\nContent-Type: text/plain\r\n"
                   "Content-Length: " + std::to_string(msg.size()) +
                   "\r\nConnection: close\r\n\r\n" + msg;
        SendAll(client, response.data(), response.size());
        closesocket(client);
        return listening_;
    }

    // 成功响应
    AppLog::Write("HTTP 200 %s (%zu bytes, %s)", full.c_str(),
                  body.size(), MimeFor(full));
    std::string header =
        "HTTP/1.1 200 OK\r\n"
        "Content-Type: " + std::string(MimeFor(full)) + "\r\n"
        "Content-Length: " + std::to_string(body.size()) + "\r\n"
        "Cache-Control: no-cache\r\n"
        "Connection: close\r\n\r\n";
    SendAll(client, header.data(), header.size());
    if (method == "GET" && !body.empty())
        SendAll(client, body.data(), body.size());
    closesocket(client);
    return listening_;
}
