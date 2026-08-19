#pragma once

#include <cstdio>
#include <string>

// 极简日志：输出到 exe 同级的 huoyin_launcher.log。
// 用于启动/初始化阶段的关键步骤排查（CEF 初始化、HTTP、窗口、浏览器创建）。
class AppLog {
public:
    // 初始化日志文件（应在程序最早期调用一次）。
    static void Init();

    // 写入一行带时间戳的日志。
    static void Write(const char* fmt, ...);
};
