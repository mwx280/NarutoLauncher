// 命令行参数解析实现（火影版）。
// 取值规则：--key=value 形式；开关 --key 形式。
// 除 CEFFlashGameHost 通用参数外，支持登录/自动登录/cookie 参数。

#include "params.h"

#include <shellapi.h>

#include <string>
#include <vector>

#include "globals.h"

namespace {

// UTF-8 编码辅助（--flash-quality / --user / --pass 等需要按字节传给环境变量）。
std::string Utf8Of(const std::wstring& ws) {
    if (ws.empty()) return "";
    int len = ::WideCharToMultiByte(CP_UTF8, 0, ws.c_str(),
                                    (int)ws.size(), nullptr, 0,
                                    nullptr, nullptr);
    if (len <= 0) return "";
    std::vector<char> buf(len);
    ::WideCharToMultiByte(CP_UTF8, 0, ws.c_str(),
                          (int)ws.size(), buf.data(), len,
                          nullptr, nullptr);
    return std::string(buf.data(), len);
}

}  // namespace

NarutoRunOptions ParseCommandLine(const wchar_t* lpCmdLine) {
    NarutoRunOptions opt;
    opt.url = kDefaultUrl;

    int argc = 0;
    wchar_t** argv = CommandLineToArgvW(lpCmdLine, &argc);
    if (!argv)
        return opt;

    auto val = [&](const wchar_t* key) -> std::wstring {
        std::wstring prefix = std::wstring(L"--") + key + L"=";
        for (int i = 0; i < argc; ++i) {
            std::wstring arg = argv[i];
            if (arg.rfind(prefix, 0) == 0)
                return arg.substr(prefix.size());
        }
        return L"";
    };

    // 帮助
    for (int i = 0; i < argc; ++i) {
        std::wstring arg = argv[i];
        if (arg == L"--help" || arg == L"-h" || arg == L"/?") {
            opt.show_usage = true;
        }
    }

    // 通用取值
    auto u = val(L"url");
    if (!u.empty()) opt.url = u;
    auto d = val(L"userdata");
    if (!d.empty()) opt.userdata = d;
    auto t = val(L"title");
    if (!t.empty()) opt.title = t;
    auto p = val(L"parent");
    if (!p.empty()) opt.parent = (HWND)_wtoi64(p.c_str());
    // 开关
    for (int i = 0; i < argc; ++i) {
        std::wstring arg = argv[i];
        if (arg == L"--embed") opt.embed = true;
        else if (arg == L"--windowed") opt.windowed = true;
        else if (arg == L"--login") opt.login = true;
    }

    // 渲染配置（写入全局，供 HostApp 读取）
    auto fg = val(L"flash-gpu");
    if (!fg.empty())
        g_flash_gpu = (fg == L"1");
    auto dp = val(L"debug-port");
    if (!dp.empty()) {
        int port = _wtoi(dp.c_str());
        if (port > 0 && port < 65536)
            g_debug_port = port;
    }
    auto fq = val(L"flash-quality");
    if (!fq.empty()) {
        std::string q = Utf8Of(fq);
        if (q == "medium" || q == "high")
            g_flash_quality = q;
        else
            g_flash_quality = "low";
    }
    auto fd = val(L"force-dpr");
    if (!fd.empty())
        g_force_dpr = (fd != L"0");

    // 登录 / 自动登录 / cookie（写入全局，供 HostClient / wWinMain 使用）
    auto c = val(L"cookie");
    if (!c.empty()) g_cookie_json = Utf8Of(c);
    auto usr = val(L"user");
    if (!usr.empty()) g_auto_user_b64 = Utf8Of(usr);
    auto psw = val(L"pass");
    if (!psw.empty()) g_auto_pass_b64 = Utf8Of(psw);

    LocalFree(argv);
    return opt;
}

bool IsChildProcessType(const wchar_t* lpCmdLine, const wchar_t* type) {
    int argc = 0;
    wchar_t** argv = CommandLineToArgvW(lpCmdLine, &argc);
    if (!argv)
        return false;
    std::wstring needle = std::wstring(L"--type=") + type;
    bool found = false;
    for (int i = 0; i < argc; ++i) {
        if (wcsstr(argv[i], needle.c_str()) != nullptr) {
            found = true;
            break;
        }
    }
    LocalFree(argv);
    return found;
}
