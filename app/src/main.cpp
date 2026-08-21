// GameHost —— 独立游戏窗口宿主
//
// 由 WPF 启动器拉起，每账号一个实例。
// 职责：创建无边框窗口，用 CEF 87 x86 加载 Flash 游戏。
//
// 命令行参数：
//   --url=<game_url>      游戏入口 URL（默认 game.huoying.qq.com/main.html）
//   --userdata=<dir>      独立缓存目录（多开隔离 cookie）
//   --title=<title>       窗口标题（默认"火影忍者OL"）

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#include <shellapi.h>

#include <string>
#include <vector>

#include "include/cef_app.h"
#include "include/cef_browser.h"
#include "include/cef_command_line.h"
#include "include/cef_cookie.h"
#include "include/cef_request_context.h"
#include "include/cef_request_context_handler.h"
#include "include/cef_task.h"
#include "include/cef_web_plugin.h"
#include "include/cef_parser.h"
#include "include/cef_values.h"
#include "include/internal/cef_win.h"

#include "frameless_window.h"
#include "app_log.h"
#include "no_console_hook.h"

// ---------- 常量 ----------
namespace {
const char* kFlashVersion = "34.0.0.380";
const wchar_t* kDefaultUrl = L"https://game.huoying.qq.com/main.html";
}  // namespace

// Flash 硬件加速（--flash-gpu=1 开启，默认关闭）。需在 HostApp 前声明，
// 供 OnBeforeCommandLineProcessing 在浏览器进程创建时读取。
bool g_flash_gpu = false;

// ---------- 应用级 CefApp（Flash 注册） ----------
class HostApp : public CefApp,
                public CefBrowserProcessHandler {
public:
    // Flash 插件绝对路径（宽字符解析，避免中文路径编码问题）。
    static std::string FlashPluginPath() {
        wchar_t exe[MAX_PATH] = {0};
        DWORD n = ::GetModuleFileNameW(nullptr, exe, MAX_PATH);
        if (n == 0 || n >= MAX_PATH)
            return "pepflashplayer.dll";
        std::wstring path(exe, n);
        size_t sep = path.find_last_of(L"\\/");
        if (sep != std::wstring::npos)
            path = path.substr(0, sep + 1);
        path += L"pepflashplayer.dll";
        int len = ::WideCharToMultiByte(CP_UTF8, 0, path.c_str(),
                                        static_cast<int>(path.size()),
                                        nullptr, 0, nullptr, nullptr);
        if (len <= 0)
            return "pepflashplayer.dll";
        std::vector<char> buf(len);
        ::WideCharToMultiByte(CP_UTF8, 0, path.c_str(),
                              static_cast<int>(path.size()),
                              buf.data(), len, nullptr, nullptr);
        return std::string(buf.data(), len);
    }

    void OnBeforeCommandLineProcessing(
        const CefString& process_type,
        CefRefPtr<CefCommandLine> command_line) override {
        // 禁用沙盒：保证所有子进程（含 ppapi Flash 插件进程）以无沙盒运行。
        // 原因：x64 下 Flash 插件进程在沙盒环境初始化时崩溃（BEX64/0xc0000005），
        // 关闭沙盒可消除该兼容性问题。对本地固定内容（腾讯游戏）无安全影响。
        command_line->AppendSwitch("no-sandbox");
        command_line->AppendSwitch("disable-setuid-sandbox");
        command_line->AppendSwitchWithValue("ppapi-flash-path",
                                            FlashPluginPath());
        command_line->AppendSwitchWithValue("ppapi-flash-version",
                                            kFlashVersion);
        // Flash 硬件加速：默认关闭（disable-gpu，软件渲染）。开启时用 GPU 渲染，
        // 但传统 Flash 页游画面主要由 CPU 渲染，开启基本无提升，反而可能花屏/
        // 兼容性问题。此设置需重新进入游戏才生效（浏览器进程创建时读取）。
        // 仅在浏览器进程（process_type 为空）设置，子进程会自动继承该开关。
        if (process_type.empty()) {
            if (g_flash_gpu)
                command_line->AppendSwitch("enable-gpu");
            else
                command_line->AppendSwitch("disable-gpu");
        }
        command_line->AppendSwitch("persist-session-cookies");
        // 日志写文件而非控制台，避免弹出 cmd 窗口
        command_line->AppendSwitchWithValue("log-file",
            "GameHost_cef.log");
    }

    CefRefPtr<CefBrowserProcessHandler> GetBrowserProcessHandler() override {
        return this;
    }

private:
    IMPLEMENT_REFCOUNTING(HostApp);
};

// ---------- 全局状态 ----------
FramelessWindow g_window;
CefRefPtr<CefBrowser> g_game_browser;
HWND g_game_hwnd = nullptr;   // 游戏窗口句柄（从 CEF 回调获取）
std::wstring g_window_title = L"火影忍者OL";
bool g_login_mode = false;    // 扫码登录模式（加载 QQ 登录页，登录成功写 login_result.txt）
bool g_auto_login = false;    // 账号密码自动登录模式（有 --user/--pass 参数）
std::string g_auto_user_b64;  // QQ 号（base64，注入登录框 JS 时 atob 解码）
std::string g_auto_pass_b64;  // 密码（base64）
std::wstring g_userdata_dir;  // userdata 目录（登录结果写入）
std::string g_cookie_json;    // 启动时注入的 cookie（base64 编码的 JSON）
HWND g_parent_hwnd = nullptr; // 内嵌父窗口

// 把 CEF 子窗口嵌入主窗口客户区（由 WM_SIZE 同步尺寸）。
void EmbedChild(HWND child) {
    g_window.SetClientChild(child);
}

// 主窗口收到 WM_CLOSE：优雅关闭 CEF 浏览器（触发 cookie 刷盘），随后退出消息循环。
void OnMainWindowClose() {
    AppLog::Write("收到 WM_CLOSE，关闭 CEF 浏览器");
    if (g_game_browser) {
        g_game_browser->GetHost()->CloseBrowser(true);
    }
    // 浏览器关闭后 OnBeforeClose 会 CefQuitMessageLoop，这里不重复退出
}

// 登录成功：把 QQ 号写入 userdata/login_result.txt
void WriteLoginResult(const CefString& qq) {
    if (g_userdata_dir.empty()) return;
    std::wstring file = g_userdata_dir + L"\\login_result.txt";
    FILE* f = nullptr;
    if (_wfopen_s(&f, file.c_str(), L"w") == 0 && f) {
        std::string qq_utf8 = qq.ToString();
        fprintf(f, "%s", qq_utf8.c_str());
        fclose(f);
    }
    AppLog::Write("扫码登录成功, QQ=%S", qq.c_str());
}

// ---- cookie 注入（免登录进游戏） ----

// base64 解码
std::string Base64Decode(const std::string& input) {
    static const std::string b64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    std::string out;
    int val = 0, valb = -8;
    for (unsigned char c : input) {
        if (c == '=') break;
        if (c == '\n' || c == '\r') continue;
        size_t idx = b64.find(c);
        if (idx == std::string::npos) continue;
        val = (val << 6) + (int)idx;
        valb += 6;
        if (valb >= 0) {
            out.push_back(char((val >> valb) & 0xFF));
            valb -= 8;
        }
    }
    return out;
}

// 解析 cookie JSON 并注入 CEF。格式：
// {"https://ptlogin2.qq.com":{"skey":"..","uin":".."},"https://game.huoying.qq.com":{"p_skey":".."}}
void InjectCookies(const std::string& json) {
    if (json.empty()) return;
    CefRefPtr<CefValue> root = CefParseJSON(json, JSON_PARSER_RFC);
    if (!root || root->GetType() != VTYPE_DICTIONARY) {
        AppLog::Write("cookie JSON 解析失败");
        return;
    }
    CefRefPtr<CefDictionaryValue> dict = root->GetDictionary();
    CefRefPtr<CefCookieManager> mgr =
        CefCookieManager::GetGlobalManager(nullptr);
    if (!mgr) return;

    std::vector<CefString> keys;
    dict->GetKeys(keys);
    for (const CefString& domain : keys) {
        CefRefPtr<CefValue> v = dict->GetValue(domain);
        if (!v || v->GetType() != VTYPE_DICTIONARY) continue;
        CefRefPtr<CefDictionaryValue> cookies = v->GetDictionary();
        std::vector<CefString> cnames;
        cookies->GetKeys(cnames);
        for (const CefString& name : cnames) {
            CefRefPtr<CefValue> cv = cookies->GetValue(name);
            if (!cv || cv->GetType() != VTYPE_STRING) continue;
            CefCookie cookie = {};
            CefString(&cookie.name) = name;
            CefString(&cookie.value) = cv->GetString();
            CefString(&cookie.domain) = domain;
            CefString(&cookie.path) = "/";
            cookie.secure = true;
            cookie.httponly = true;
            // 设置到期时间（一年后）
            time_t now = time(nullptr) + 365 * 24 * 3600;
            cookie.has_expires = true;
            cef_time_from_timet(now, &cookie.expires);
            AppLog::Write("注入 cookie: %S @ %S", name.c_str(), domain.c_str());
            mgr->SetCookie(domain, cookie, nullptr);
        }
    }
}

// ---- 账号密码自动登录（无 cookie 时自动填表登录） ----

// 构建登录框内执行的自动填表 JS（登录框为 xui.ptlogin2.qq.com 的跨域 iframe，
// 通过 frame->ExecuteJavaScript 直接在其上下文执行，绕过同源限制）。
// 分两步执行，保证顺序：先点击"账号密码登录"切换到密码登录界面，
// 待输入框可见后再注入账号密码（轮询机制每 500ms 重试一次）。
// 账号密码以 base64 内嵌，JS 内 atob 解码，避免引号/换行转义问题。
std::string BuildAutoLoginJs() {
    std::string js =
        "(function(){"
        "try{"
        "var sw=document.getElementById('switcher_plogin');"
        "var u=document.getElementById('u');"
        "var p=document.getElementById('p');"
        "var showPsw=sw&&sw.getClientRects().length>0;"
        "var showInput=u&&p&&u.getClientRects().length>0&&p.getClientRects().length>0;"
        "if(showPsw&&!showInput){sw.click();return;}"
        "if(showInput){"
        "var usr=atob('" + g_auto_user_b64 + "');"
        "var pwd=atob('" + g_auto_pass_b64 + "');"
        "if(u.value!==usr){u.value=usr;u.dispatchEvent(new Event('input',{bubbles:true}));"
        "u.dispatchEvent(new Event('change',{bubbles:true}));}"
        "if(p.value!==pwd){p.value=pwd;p.dispatchEvent(new Event('input',{bubbles:true}));"
        "p.dispatchEvent(new Event('change',{bubbles:true}));}"
        "}"
        "}catch(e){}"
        "})();";
    return js;
}

// 遍历 frame 列表，返回 URL 匹配 ptlogin2.qq.com 的登录框 frame。
CefRefPtr<CefFrame> FindLoginFrame(CefRefPtr<CefBrowser> browser) {
    if (!browser) return nullptr;
    std::vector<CefString> names;
    browser->GetFrameNames(names);
    for (const CefString& name : names) {
        CefRefPtr<CefFrame> f = browser->GetFrame(name);
        if (f && f->IsValid()) {
            std::string url = f->GetURL().ToString();
            if (url.find("ptlogin2.qq.com") != std::string::npos)
                return f;
        }
    }
    return nullptr;
}

// Flash 画质级别（Flash object/embed 的 quality 参数只有 低/中/高）。
const char* kFlashQualityNames[] = {"low", "medium", "high"};

// 当前 Flash 画质（默认最低）。
std::string g_flash_quality = "low";

// 刷新当前游戏页面。
void ReloadGame() {
    if (g_game_browser && g_game_browser->GetMainFrame()) {
        AppLog::Write("命令: 刷新游戏页面");
        g_game_browser->Reload();
    }
}

// 崩溃自动恢复：Flash 插件进程或渲染进程崩溃后，自动重新加载页面。
// 短时间内连续崩溃超过上限则停止，避免无限刷新循环。
namespace {
const int kMaxCrashReloads = 3;       // 连续崩溃自动重载上限
const DWORD kCrashWindowMs = 60000;   // 崩溃统计时间窗（60 秒）
DWORD g_last_crash_tick = 0;          // 上次崩溃时间（TickCount）
int g_crash_reload_count = 0;         // 时间窗内崩溃次数

// 延迟执行页面重载的任务（等崩溃进程清理完再刷新）。
class ReloadTask : public CefTask {
public:
    void Execute() override { ReloadGame(); }
    IMPLEMENT_REFCOUNTING(ReloadTask);
};
}  // namespace

// 处理崩溃并决定是否自动重载（UI 线程调用）。
void HandleProcessCrashed(const char* what) {
    DWORD now = ::GetTickCount();
    if (g_last_crash_tick == 0 ||
        now - g_last_crash_tick > kCrashWindowMs) {
        g_crash_reload_count = 0;  // 上一窗口已过期，重置计数
    }
    g_last_crash_tick = now;
    ++g_crash_reload_count;
    AppLog::Write("崩溃自动恢复: %s (第 %d 次)", what, g_crash_reload_count);
    if (g_crash_reload_count > kMaxCrashReloads) {
        AppLog::Write("崩溃自动恢复: 连续崩溃次数超限，停止自动重载");
        return;
    }
    CefPostDelayedTask(TID_UI, new ReloadTask(), 800);
}

// ---- 自动补 zone_id（修复 main.html 无 zone_id 时 fcgi 500 导致 Flash 不加载的黑屏） ----
// main.html 通过 fcgi-bin/query_svr_info.fcgi?svr_id=<zone_id> 获取服务器信息后才创建
// Flash（loadSwf）。URL 缺少 zone_id 时 fcgi 返回 500，页面保持纯黑。这里从 cookie 读取
// 上次选服的 sServerID 并拼到 URL 重载，避免用户每次手动选区。首次登录（cookie 无
// sServerID）的账号不会触发重载，需先在官网选区后 cookie 才有 sServerID。

// 遍历 cookie 收集 sServerID，结束后在 UI 线程补 zone_id 重载。
class ZoneIdVisitor : public CefCookieVisitor {
public:
    ZoneIdVisitor() = default;

    bool Visit(const CefCookie& cookie, int count, int total,
               bool& deleteCookie) override {
        deleteCookie = false;
        std::string name = CefString(&cookie.name);
        if (name == "sServerID")
            _server_id = CefString(&cookie.value);
        // 遍历到最后一个 cookie 时提交任务（CEF 可能不回调 count<0 结束信号）
        if (count >= 0 && total > 0 && count == total - 1) {
            AppLog::Write("自动补 zone_id: 遍历完成, sServerID=%s",
                          _server_id.empty() ? "(空)" : _server_id.c_str());
            if (!_server_id.empty())
                CefPostTask(TID_UI, new ApplyZoneIdTask(_server_id));
            return false;
        }
        return true;
    }

private:
    class ApplyZoneIdTask : public CefTask {
    public:
        explicit ApplyZoneIdTask(const std::string& server_id)
            : server_id_(server_id) {}
        void Execute() override {
            if (!g_game_browser) return;
            CefRefPtr<CefFrame> frame = g_game_browser->GetMainFrame();
            if (!frame || !frame->IsValid()) return;
            std::string url = frame->GetURL().ToString();
            // 已在重载流程或 URL 已带 zone_id，避免死循环
            if (url.find("zone_id") != std::string::npos) return;
            std::string sep =
                (url.find('?') == std::string::npos) ? "?" : "&";
            std::string new_url = url + sep + "zone_id=" + server_id_;
            AppLog::Write("自动补 zone_id: %s -> %s", url.c_str(),
                          new_url.c_str());
            frame->LoadURL(new_url);
        }
        IMPLEMENT_REFCOUNTING(ApplyZoneIdTask);

    private:
        std::string server_id_;
    };

    std::string _server_id;
    IMPLEMENT_REFCOUNTING(ZoneIdVisitor);
};

// 从 cookie 管理器读取 sServerID（存于 .huoying.qq.com 父域，用 VisitAllCookies 全量遍历）。
void CheckAndApplyZoneId() {
    CefRefPtr<CefCookieManager> mgr =
        CefCookieManager::GetGlobalManager(nullptr);
    if (mgr)
        mgr->VisitAllCookies(new ZoneIdVisitor());
}

// 在 UI 线程执行 zone_id 检查的任务。
class CheckAndApplyZoneIdTask : public CefTask {
public:
    void Execute() override { CheckAndApplyZoneId(); }
    IMPLEMENT_REFCOUNTING(CheckAndApplyZoneIdTask);
};

// 生成重写 narutoweb.js 的 createEntrySwfObject 的注入脚本：
// 原实现生成 <embed> 时硬编码 quality="high"，且 Flash 的 quality 只在
// SWF 实例化时读取一次，运行期改 DOM 无效。因此必须在该函数被调用前完整
// 替换它，让生成的 <embed quality="..."> 直接带目标画质，SWF 以目标画质创建。
// 脚本忠实复制原 createEntrySwfObject 逻辑（含 IE/Flash 检测分支），仅把
// 硬编码的 quality="high" 改为目标值，避免页面其他行为被破坏。
std::string BuildQualityHookScript(const std::string& q) {
    std::string js =
        R"JS((function(q,max){
var n=0;
function t(){
try{
if(window.naruto&&naruto.Web&&naruto.Web.prototype){
var p=naruto.Web.prototype;
if(p.createEntrySwfObject&&p.createEntrySwfObject.__qHook)return;
p.createEntrySwfObject=function(attributes,param,id,a){
var element=document.getElementById(id);
if(element){
if(!this.sys.chrome&&!this.sys.playerInstalled){
element.parentNode.innerHTML='<div id="flashAlert" style="font-size:18px; color: #FFFFFF;">no flash</div>';
return;
}
if(typeof attributes.id=="undefined")attributes.id=id;
var attrStr="";
for(var attr in attributes){
if(attributes[attr]!=Object.prototype[attr]){
if("data"==attr.toLowerCase()){param.movie=attributes[attr];}
else{
if("id"==attr.toLowerCase()){if(this.sys.ie){attrStr+=" "+attr+'="'+attributes[attr]+'"';}}
else{attrStr+=" "+attr+'="'+attributes[attr]+'"';}
}
}
}
attrStr+=" style=display:block;text-align:center;";
var paramStr="";
for(var d in param){if(param[d]!=Object.prototype[d])paramStr+='<param name="'+d+'" value="'+param[d]+'" />';}
var htmlStr="";
var embedStr='<embed';
embedStr+=' src="'+param.movie+'"';
embedStr+=' type="application/x-shockwave-flash"';
embedStr+=' pluginspage="http://www.adobe.com/go/getflashplayer"';
embedStr+=' quality="'+q+'"';
embedStr+=' width="'+attributes.width+'"';
embedStr+=' height="'+attributes.height+'"';
embedStr+=' align="middle"';
embedStr+=' allowScriptAccess="'+param.allowScriptAccess+'"';
embedStr+=' allowFullScreenInteractive="'+param.allowFullScreenInteractive+'"';
embedStr+=' wmode="'+param.wmode+'"';
embedStr+=' name="'+attributes.name+'"';
embedStr+=' id="'+attributes.id+'"';
embedStr+='>';
htmlStr+='<object classid="clsid:d27cdb6e-ae6d-11cf-96b8-444553540000" codebase="http://fpdownload.macromedia.com/get/flashplayer/current/swflash.cab" '+attrStr+'>'+paramStr;
htmlStr+=embedStr;
htmlStr+='</object>';
element.parentNode.innerHTML=htmlStr;
}
};
p.createEntrySwfObject.__qHook=true;
return;
}
}catch(e){}
if(++n<max)setTimeout(t,100);
}
t();
})JS";
    js += ")('" + q + "',300);";
    return js;
}

// 设置 Flash 画质（quality 参数：low/medium/high）。
// 由于 quality 只在 SWF 实例化时读取，改档后必须重载页面让 Flash 重建。
void SetFlashQuality(int level) {
    if (level < 0) level = 0;
    if (level > 2) level = 2;
    g_flash_quality = kFlashQualityNames[level];
    AppLog::Write("命令: 设置 Flash 画质=%s (%d), 重载页面生效", g_flash_quality.c_str(), level);
    ReloadGame();
}

// 主窗口自定义命令消息回调（cmd: 1=刷新, 2=画质调节）。
void OnWindowCommand(int cmd, WPARAM w, LPARAM l) {
    switch (cmd) {
        case 1:
            ReloadGame();
            break;
        case 2:
            SetFlashQuality(static_cast<int>(w));
            break;
        default:
            break;
    }
}

// ---------- 浏览器客户端 ----------
class HostClient : public CefClient,
                   public CefLifeSpanHandler,
                   public CefLoadHandler,
                   public CefRequestContextHandler,
                   public CefRequestHandler,
                   public CefCookieVisitor {
public:
    HostClient() = default;

    CefRefPtr<CefLifeSpanHandler> GetLifeSpanHandler() override { return this; }
    CefRefPtr<CefRequestHandler> GetRequestHandler() override { return this; }
    CefRefPtr<CefLoadHandler> GetLoadHandler() override { return this; }

    // 请求上下文初始化完成后调用（UI 线程）：把 Flash 插件的 content setting
    // 设为 ALLOW，避免 Chromium 默认的 click-to-play 策略导致
    // "Right-click to run Adobe Flash Player" 占位提示。
    void OnRequestContextInitialized(
        CefRefPtr<CefRequestContext> request_context) override {
        // CONTENT_SETTING_ALLOW = 1
        CefRefPtr<CefValue> allow = CefValue::Create();
        allow->SetInt(1);
        CefString error;
        bool ok = request_context->SetPreference(
            "profile.default_content_setting_values.plugins", allow, error);
        AppLog::Write("设置 plugins content setting=ALLOW: %s (%s)",
                      ok ? "成功" : "失败", error.ToString().c_str());
    }

    bool OnBeforePluginLoad(const CefString& mime_type,
                            const CefString& plugin_url,
                            bool is_main_frame,
                            const CefString& top_origin_url,
                            CefRefPtr<CefWebPluginInfo> plugin_info,
                            PluginPolicy* plugin_policy) override {
        AppLog::Write("OnBeforePluginLoad: mime=%S main=%d",
                      mime_type.c_str(), is_main_frame ? 1 : 0);
        if (mime_type == "application/x-shockwave-flash") {
            *plugin_policy = PLUGIN_POLICY_ALLOW;
            return true;
        }
        return false;
    }

    void OnAfterCreated(CefRefPtr<CefBrowser> browser) override {
        g_game_browser = browser;
        HWND game_hwnd = browser->GetHost()->GetWindowHandle();
        if (game_hwnd) {
            g_game_hwnd = game_hwnd;
            EmbedChild(game_hwnd);
        }
    }

    // Flash 插件进程崩溃（pepflashplayer 崩溃导致黑屏）：自动重载恢复。
    void OnPluginCrashed(CefRefPtr<CefBrowser> browser,
                         const CefString& plugin_path) override {
        AppLog::Write("崩溃自动恢复: Flash 插件崩溃, plugin=%s",
                      CefString(plugin_path).ToString().c_str());
        HandleProcessCrashed("Flash 插件崩溃");
    }

    // 渲染进程异常终止（如崩溃、被杀）：自动重载恢复。
    void OnRenderProcessTerminated(CefRefPtr<CefBrowser> browser,
                                   TerminationStatus status) override {
        const char* reason = "未知";
        switch (status) {
            case TS_ABNORMAL_TERMINATION: reason = "异常退出"; break;
            case TS_PROCESS_WAS_KILLED:   reason = "进程被终止"; break;
            case TS_PROCESS_CRASHED:      reason = "渲染进程崩溃"; break;
            default: break;
        }
        AppLog::Write("崩溃自动恢复: 渲染进程终止 (%s)", reason);
        HandleProcessCrashed(reason);
    }

    // 页面开始加载：注入滚动条隐藏脚本（DOM 就绪后立即执行，避免加载过程出现滚动条）
    void OnLoadStart(CefRefPtr<CefBrowser> browser,
                     CefRefPtr<CefFrame> frame,
                     TransitionType transition_type) override {
        if (!frame->IsMain())
            return;
        frame->ExecuteJavaScript(
            "var __hide=function(){"
            "if(window.__noScrollBar)return;"
            "window.__noScrollBar=true;"
            "var s=document.createElement('style');"
            "s.textContent='html,body{overflow:hidden!important;}"
            "*::-webkit-scrollbar{display:none!important;width:0!important;height:0!important;}';"
            "document.head.appendChild(s);};"
            "if(document.readyState==='loading'){"
            "document.addEventListener('DOMContentLoaded',__hide);}"
            "else{__hide();}",
            frame->GetURL(), 0);
        // 注入画质 hook：在 Flash 实例化前重写 createEntrySwfObject，
        // 使入口 SWF 以目标 quality 创建（quality 只在实例化时读取一次）。
        frame->ExecuteJavaScript(BuildQualityHookScript(g_flash_quality),
                                 frame->GetURL(), 0);
    }

    // 扫码登录模式：页面加载完成后启动 cookie 轮询检测（出现 skey 即登录成功）
    void OnLoadEnd(CefRefPtr<CefBrowser> browser,
                   CefRefPtr<CefFrame> frame,
                   int httpStatusCode) override {
        // 隐藏页面滚动条（保持内嵌区域干净）——兜底
        if (frame->IsMain()) {
            frame->ExecuteJavaScript(
                "if(!window.__noScrollBar){"
                "var s=document.createElement('style');"
                "s.textContent='html,body{overflow:hidden!important;}"
                "*::-webkit-scrollbar{display:none!important;width:0!important;height:0!important;}';"
                "document.head.appendChild(s);}",
                frame->GetURL(), 0);
            // main.html 缺 zone_id 时 fcgi 返回 500 导致 Flash 不加载（黑屏），
            // 从 cookie 读取 sServerID 自动补 zone_id 重载（仅当 URL 未带 zone_id）。
            std::string url = frame->GetURL().ToString();
            if (url.find("main.html") != std::string::npos &&
                url.find("zone_id") == std::string::npos) {
                AppLog::Write("自动补 zone_id: main.html 无 zone_id, 启动检查");
                CefPostTask(TID_UI, new CheckAndApplyZoneIdTask());
            }
        }
        if ((g_login_mode || g_auto_login) && !_login_detected &&
            frame->IsMain()) {
            AppLog::Write("登录检测: OnLoadEnd 主框架, status=%d", httpStatusCode);
            _pending_qq = "";
            CefPostDelayedTask(TID_UI,
                               new ReadLoginCookiesTask(this), 2000);
        }
        // 账号密码自动登录模式：启动填表轮询（每 500ms 尝试一次，直到登录成功）
        if (g_auto_login && !_auto_login_started && !_login_detected &&
            frame->IsMain()) {
            _auto_login_started = true;
            AppLog::Write("自动登录: OnLoadEnd 主框架, status=%d", httpStatusCode);
            CefPostDelayedTask(TID_UI, new AutoLoginTask(this, 120), 500);
        }
    }

    // 轮询读取登录 cookie 的任务（每 2 秒重查一次）
    class ReadLoginCookiesTask : public CefTask {
    public:
        explicit ReadLoginCookiesTask(HostClient* client) : client_(client) {}
        void Execute() override {
            CefRefPtr<CefCookieManager> mgr =
                CefCookieManager::GetGlobalManager(nullptr);
            if (mgr) {
                mgr->VisitUrlCookies("https://ptlogin2.qq.com", false,
                                     client_);
            }
        }
    private:
        HostClient* client_;
        IMPLEMENT_REFCOUNTING(ReadLoginCookiesTask);
    };

    // 账号密码自动登录：周期执行登录框填表脚本，直到登录成功（cookie 出现 skey）
    class AutoLoginTask : public CefTask {
    public:
        AutoLoginTask(HostClient* client, int attempts)
            : client_(client), attempts_(attempts) {}
        void Execute() override {
            if (attempts_ <= 0 || client_->IsAutoLoginDone()) {
                AppLog::Write("自动登录: 停止轮询 (attempts=%d)", attempts_);
                return;
            }
            if (g_game_browser) {
                CefRefPtr<CefFrame> login_frame = FindLoginFrame(g_game_browser);
                if (login_frame) {
                    AppLog::Write("自动登录: 向登录框注入填表脚本 (attempt=%d)",
                                  attempts_);
                    login_frame->ExecuteJavaScript(BuildAutoLoginJs(),
                                                   login_frame->GetURL(), 0);
                }
            }
            CefPostDelayedTask(TID_UI, new AutoLoginTask(client_, attempts_ - 1),
                               500);
        }
    private:
        HostClient* client_;
        int attempts_;
        IMPLEMENT_REFCOUNTING(AutoLoginTask);
    };

    // 延迟写登录结果的任务
    class WriteResultTask : public CefTask {
    public:
        explicit WriteResultTask(HostClient* client) : client_(client) {}
        void Execute() override {
            client_->OnCookieVisitedDone();
        }
    private:
        HostClient* client_;
        IMPLEMENT_REFCOUNTING(WriteResultTask);
    };

    // CefCookieVisitor：收集登录 cookie，出现 skey 视为登录成功
    bool Visit(const CefCookie& cookie, int count, int total,
               bool& deleteCookie) override {
        deleteCookie = false;
        if (count < 0) {
            // 兜底：若检测到登录但尚未写结果，写结果
            AppLog::Write("登录检测: cookie 遍历结束, detected=%d, qq=%s",
                          _login_detected ? 1 : 0, _pending_qq.c_str());
            if (_login_detected) {
                OnCookieVisitedDone();
            } else if (g_login_mode || g_auto_login) {
                CefPostDelayedTask(TID_UI,
                                   new ReadLoginCookiesTask(this), 2000);
            }
            return false;
        }
        if (g_login_mode || g_auto_login) {
            std::string name = CefString(&cookie.name);
            std::string value = CefString(&cookie.value);
            if (g_login_mode)
                AppLog::Write("登录检测: cookie %s=%s (detected=%d)",
                              name.c_str(), value.c_str(),
                              _login_detected ? 1 : 0);
            if (!_login_detected &&
                (name == "skey" || name == "p_skey" || name == "pt4_token" ||
                 name == "supertoken" || name == "superuin")) {
                if (!value.empty() && value[0] != '\0' &&
                    value != "0" && value.find("login_fail") == std::string::npos) {
                    _login_detected = true;
                    if (g_login_mode) {
                        // 延迟 800ms 写结果，确保 QQ 已从后续 cookie 提取
                        CefPostDelayedTask(TID_UI,
                                           new WriteResultTask(this), 800);
                    } else {
                        // 自动登录成功：cookie 已持久化，停止填表轮询
                        AppLog::Write("自动登录: 检测到登录态，停止轮询");
                    }
                }
            }
            if (g_login_mode && (name == "uin" || name == "ptui_loginuin" ||
                                 name == "pt2gguin" || name == "superuin")) {
                if (!value.empty() && value[0] != '\0') {
                    std::string qq;
                    for (char ch : value) {
                        if (isdigit((unsigned char)ch))
                            qq += ch;
                    }
                    if (!qq.empty())
                        _pending_qq = qq;
                }
            }
        }
        return true;
    }

    // 自动登录轮询是否已结束（cookie 出现登录态）。
    bool IsAutoLoginDone() const { return _login_detected; }

    bool DoClose(CefRefPtr<CefBrowser> browser) override {
        return false;
    }

    void OnBeforeClose(CefRefPtr<CefBrowser> browser) override {
        if (g_game_browser && g_game_browser->IsSame(browser)) {
            g_game_browser = nullptr;
            AppLog::Write("浏览器已关闭，退出消息循环");
            // 所有浏览器关闭后退出 CefRunMessageLoop，让 CEF 正常刷盘 cookie
            CefQuitMessageLoop();
        }
    }

    // 登录检测完成后写结果（由 cookie 遍历结束触发）
    void OnCookieVisitedDone() {
        if (g_login_mode && _login_detected) {
            if (_pending_qq.empty())
                _pending_qq = "0";  // 无法提取 QQ，标记但允许
            CefString qq(_pending_qq);
            WriteLoginResult(qq);
        }
    }

private:
    bool _login_detected = false;
    bool _auto_login_started = false;
    std::string _pending_qq;
    IMPLEMENT_REFCOUNTING(HostClient);
};

// ---------- 主入口 ----------
int RunBrowserProcess(const std::wstring& url,
                      const std::wstring& userdata_dir,
                      const std::wstring& title,
                      bool embed,
                      HWND parent) {
    AppLog::Write("== GameHost 开始 ==");
    if (!title.empty())
        g_window_title = title;

    // 主窗口（在 CefInitialize 之前创建，规避 CEF 环境对窗口创建的影响）
    AppLog::Write("创建主窗口...");
    if (!g_window.Create(1280, 800, embed, parent)) {
        DWORD err = GetLastError();
        AppLog::Write("创建窗口失败, GetLastError=%lu", err);
        return 1;
    }
    // 嵌入模式：把主窗口 HWND 写入 userdata 目录，供启动器桥接读取
    if (embed && !userdata_dir.empty()) {
        std::wstring hwnd_file = userdata_dir + L"\\window_hwnd.txt";
        // 先删除上次运行遗留的过期句柄文件，避免启动器读到旧句柄
        ::DeleteFileW(hwnd_file.c_str());
        FILE* f = nullptr;
        if (_wfopen_s(&f, hwnd_file.c_str(), L"w") == 0 && f) {
            fprintf(f, "%llu", (unsigned long long)g_window.Handle());
            fclose(f);
        }
    }
    ::SetWindowTextW(g_window.Handle(), g_window_title.c_str());
    AppLog::Write("主窗口创建成功, HWND=%p", g_window.Handle());
    g_window.SetCloseHandler(&OnMainWindowClose);
    g_window.SetCommandHandler(&OnWindowCommand);

    // 初始化 CEF（浏览器进程）
    CefMainArgs main_args(GetModuleHandle(nullptr));
    CefSettings settings;
    settings.no_sandbox = true;
    settings.log_severity = LOGSEVERITY_WARNING;
    if (!userdata_dir.empty()) {
        CefString(&settings.cache_path).FromWString(userdata_dir);
        settings.persist_session_cookies = true;
    }
    CefRefPtr<HostApp> app = new HostApp();
    bool ok_init = CefInitialize(main_args, settings, app.get(), nullptr);
    AppLog::Write("CefInitialize 结果: %s", ok_init ? "成功" : "失败");
    if (!ok_init) {
        MessageBox(nullptr, L"CEF 初始化失败", L"错误", MB_OK);
        return 1;
    }

    // 隐藏控制台窗口（CEF 可能为浏览器进程创建 console）
    {
        HWND console = ::GetConsoleWindow();
        if (console)
            ::ShowWindow(console, SW_HIDE);
    }

    // 启动时注入 cookie（免登录进游戏）
    if (!g_cookie_json.empty()) {
        InjectCookies(Base64Decode(g_cookie_json));
        // 等待 cookie 写入完成（异步 SetCookie）
        ::Sleep(1500);
    }

    // 创建游戏浏览器
    CefRefPtr<HostClient> game_client = new HostClient();
    CefWindowInfo info;
    int w, h;
    g_window.ClientSize(w, h);
    if (w <= 0) w = 1280;
    if (h <= 0) h = 800;
    RECT rc = {0, 0, w, h};
    info.SetAsChild(g_window.Handle(), rc);

    CefBrowserSettings settings2;
    settings2.plugins = STATE_ENABLED;

    CefString url_str(url);
    // 绑定 request context handler（HostClient 实现 CefRequestContextHandler）：
    // 使 OnBeforePluginLoad 生效，确保 Flash 插件策略为 ALLOW（否则出现
    // "Right-click to run Adobe Flash Player" click-to-play 提示）。
    // 复用全局 context 的存储，避免丢失 cookie/缓存（免登录态跨启动保留）。
    CefRefPtr<CefRequestContext> request_context =
        CefRequestContext::CreateContext(CefRequestContext::GetGlobalContext(),
                                         game_client);
    CefBrowserHost::CreateBrowser(info, game_client, url_str, settings2,
                                  nullptr, request_context);
    AppLog::Write("创建游戏浏览器: %S", url.c_str());

    // CEF 消息循环
    AppLog::Write("进入 CefRunMessageLoop");
    CefRunMessageLoop();

    // 清理
    AppLog::Write("消息循环退出，清理中");
    CefShutdown();
    return 0;
}

int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE, wchar_t* lpCmdLine, int) {
    (void)hInstance;

    // DPI 感知（文本模糊修复）
    {
        typedef HRESULT(WINAPI* SetProcessDpiAwarenessFn)(int);
        HMODULE shcore = LoadLibraryW(L"shcore.dll");
        if (shcore) {
            auto fn = reinterpret_cast<SetProcessDpiAwarenessFn>(
                GetProcAddress(shcore, "SetProcessDpiAwareness"));
            if (fn)
                fn(2);  // PROCESS_PER_MONITOR_DPI_AWARE
            FreeLibrary(shcore);
        } else {
            SetProcessDPIAware();
        }
    }

    // 解析命令行参数
    std::wstring url = kDefaultUrl;
    std::wstring userdata;
    std::wstring title;
    bool embed = false;
    bool login = false;
    HWND parent = nullptr;
    std::string cookie_b64;
    {
        int argc = 0;
        wchar_t** argv = CommandLineToArgvW(lpCmdLine, &argc);
        auto utf8_of = [](const std::wstring& ws) -> std::string {
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
        };
        for (int i = 0; i < argc; ++i) {
            std::wstring arg = argv[i];
            auto val = [&arg](const wchar_t* key) -> std::wstring {
                std::wstring prefix = std::wstring(L"--") + key + L"=";
                if (arg.rfind(prefix, 0) == 0)
                    return arg.substr(prefix.size());
                return L"";
            };
            auto u = val(L"url");
            if (!u.empty()) url = u;
            auto d = val(L"userdata");
            if (!d.empty()) userdata = d;
            auto t = val(L"title");
            if (!t.empty()) title = t;
            auto p = val(L"parent");
            if (!p.empty()) parent = (HWND)_wtoi64(p.c_str());
            auto c = val(L"cookie");
            if (!c.empty()) {
                int len = ::WideCharToMultiByte(CP_UTF8, 0, c.c_str(),
                                                (int)c.size(), nullptr, 0,
                                                nullptr, nullptr);
                if (len > 0) {
                    std::vector<char> buf(len);
                    ::WideCharToMultiByte(CP_UTF8, 0, c.c_str(),
                                          (int)c.size(), buf.data(), len,
                                          nullptr, nullptr);
                    cookie_b64.assign(buf.data(), len);
                }
            }
            auto usr = val(L"user");
            if (!usr.empty())
                g_auto_user_b64 = utf8_of(usr);
            auto psw = val(L"pass");
            if (!psw.empty())
                g_auto_pass_b64 = utf8_of(psw);
            if (arg == L"--embed")
                embed = true;
            if (arg == L"--login")
                login = true;
            auto fg = val(L"flash-gpu");
            if (!fg.empty())
                g_flash_gpu = (fg == L"1");
        }
        LocalFree(argv);
    }

    // 扫码登录模式：加载官网首页（自动弹出 QQ 登录二维码），登录成功写 login_result.txt
    if (login) {
        g_login_mode = true;
        g_userdata_dir = userdata;
        if (url == kDefaultUrl) {
            url = L"https://huoying.qq.com/server/website/";
        }
    }
    // 账号密码自动登录模式：有 --user/--pass 参数即开启（无 cookie 时自动填表登录）
    if (!g_auto_user_b64.empty() && !g_auto_pass_b64.empty()) {
        g_auto_login = true;
    }
    g_cookie_json = cookie_b64;
    g_parent_hwnd = parent;

    // 阻止 Flash 插件（ppapi 子进程）加载时弹 cmd 窗口：Flash 会执行
    // cmd.exe /c echo NOT SANDBOXED 做沙箱探测，未设置 CREATE_NO_WINDOW
    // 导致控制台窗口一闪而过。对 ppapi 子进程 hook CreateProcessW/A，
    // 强制隐藏子进程控制台窗口。
    {
        int argc2 = 0;
        wchar_t** argv2 = CommandLineToArgvW(lpCmdLine, &argc2);
        bool is_ppapi = false;
        if (argv2) {
            for (int i = 0; i < argc2; ++i) {
                if (wcsstr(argv2[i], L"--type=ppapi") != nullptr) {
                    is_ppapi = true;
                    break;
                }
            }
            LocalFree(argv2);
        }
        if (is_ppapi) {
            // hook CreateProcessW/A 强制隐藏 Flash 沙箱探测弹出的 cmd 窗口。
            // 使用 MinHook 实现（自动处理 x86/x64 绝对跳转与指令重定位），
            // 替代原手工 inline hook——后者 jmp rel32 在 x64 下可能超出
            // ±2GB 跳转范围，导致 ppapi 进程调用 CreateProcessW 时崩溃。
            InstallNoConsoleHooks();
        }
    }

    CefMainArgs main_args(GetModuleHandle(nullptr));
    CefRefPtr<HostApp> app = new HostApp();

    // 隐藏可能存在的控制台窗口（浏览器进程与所有 CEF 子进程在进入
    // CefExecuteProcess 前统一隐藏，避免 Flash 等子进程闪现 cmd 窗口）。
    {
        HWND console = ::GetConsoleWindow();
        if (console)
            ::ShowWindow(console, SW_HIDE);
    }

    int exit_code = CefExecuteProcess(main_args, app.get(), nullptr);
    if (exit_code >= 0)
        return exit_code;

    AppLog::Init();
    AppLog::Write("== GameHost 入口 ==");
    AppLog::Write("URL=%S userdata=%S embed=%d parent=%llu login=%d autologin=%d",
                  url.c_str(), userdata.c_str(), embed ? 1 : 0,
                  (unsigned long long)parent, login ? 1 : 0,
                  g_auto_login ? 1 : 0);
    return RunBrowserProcess(url, userdata, title, embed, parent);
}
