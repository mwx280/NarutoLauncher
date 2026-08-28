// HostClient 实现：浏览器生命周期、Flash 插件 ALLOW、崩溃自动恢复、
// 画质 hook 注入、zone_id 自动补全、登录检测（扫码/自动登录）、窗口铺满。

#include "host_client.h"

#include <string>
#include <vector>

#include "include/cef_app.h"
#include "include/cef_task.h"
#include "include/cef_web_plugin.h"
#include "include/cef_values.h"
#include "include/cef_cookie.h"
#include "include/cef_parser.h"

#include "globals.h"
#include "app_log.h"

// ---------- 通用工具 ----------

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

// 刷新当前页面。
void ReloadPage() {
    if (g_game_browser && g_game_browser->GetMainFrame()) {
        AppLog::Write("命令: 刷新游戏页面");
        g_game_browser->Reload();
    }
}

// 把 CEF 子窗口嵌入主窗口客户区（由 WM_SIZE 同步尺寸）。
void EmbedChild(HWND child) {
    g_window.SetClientChild(child);
}

// 把倍速写入 exe 目录 speed.txt，供 ppapi 子进程（Flash）读取变速。
void SaveSpeedToFile(double speed) {
    wchar_t exe[MAX_PATH] = {0};
    DWORD n = ::GetModuleFileNameW(nullptr, exe, MAX_PATH);
    if (n == 0 || n >= MAX_PATH)
        return;
    std::wstring path(exe, n);
    size_t sep = path.find_last_of(L"\\/");
    if (sep != std::wstring::npos)
        path = path.substr(0, sep + 1);
    path += L"speed.txt";
    FILE* f = nullptr;
    if (_wfopen_s(&f, path.c_str(), L"w") == 0 && f) {
        fprintf(f, "%.1f", speed);
        fclose(f);
    }
}

// 主窗口收到 WM_CLOSE：优雅关闭 CEF 浏览器（触发 cookie 刷盘），随后退出消息循环。
void OnMainWindowClose() {
    AppLog::Write("收到 WM_CLOSE，关闭 CEF 浏览器");
    if (g_game_browser) {
        g_game_browser->GetHost()->CloseBrowser(true);
    }
}

// 主窗口自定义命令消息回调（cmd: 1=刷新, 2=画质调节, 3=选区, 4=静音, 5=倍速）。
void OnWindowCommand(int cmd, WPARAM w, LPARAM l) {
    switch (cmd) {
        case 1:
            ReloadPage();
            break;
        case 2:
            SetFlashQuality(static_cast<int>(w));
            break;
        case 3:
            // 选区：导航到官网选区页，用户自行选区后点开始进区
            if (g_game_browser && g_game_browser->GetMainFrame()) {
                AppLog::Write("命令: 打开选区页");
                g_game_browser->GetMainFrame()->LoadURL(
                    "https://huoying.qq.com/server/website/");
            }
            break;
        case 4:
            // 静音：切换浏览器音频静音（Flash 音频经浏览器进程控制）
            if (g_game_browser && g_game_browser->GetHost()) {
                g_muted = !g_muted;
                g_game_browser->GetHost()->SetAudioMuted(g_muted);
                AppLog::Write("命令: 静音=%d", g_muted ? 1 : 0);
            }
            break;
        case 5:
            // 倍速：w 为倍速×10（5=0.5x, 10=1x, 20=2x, 40=4x），写入文件供 ppapi 子进程读取
            {
                double speed = static_cast<int>(w) / 10.0;
                if (speed < 0.1) speed = 1.0;
                g_speed = speed;
                AppLog::Write("命令: 倍速=%.1f", speed);
                SaveSpeedToFile(speed);
            }
            break;
        default:
            break;
    }
}

// Flash 画质级别（Flash object/embed 的 quality 参数只有 低/中/高）。
const char* kFlashQualityNames[] = {"low", "medium", "high"};

// 设置 Flash 画质（quality 参数：low/medium/high）。
// 由于 quality 只在 SWF 实例化时读取，改档后必须重载页面让 Flash 重建。
void SetFlashQuality(int level) {
    if (level < 0) level = 0;
    if (level > 2) level = 2;
    g_flash_quality = kFlashQualityNames[level];
    AppLog::Write("命令: 设置 Flash 画质=%s (%d), 重载页面生效", g_flash_quality.c_str(), level);
    ReloadPage();
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

// 构建登录框内执行的自动填表 JS（登录框为 xui.ptlogin2.qq.com 的跨域 iframe，
// 通过 frame->ExecuteJavaScript 直接在其上下文执行，绕过同源限制）。
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

// 生成重写 narutoweb.js 的 createEntrySwfObject 的注入脚本：
// 原实现生成 <embed> 时硬编码 quality="high"，且 Flash 的 quality 只在
// SWF 实例化时读取一次，运行期改 DOM 无效。因此必须在该函数被调用前完整
// 替换它，让生成的 <embed quality="..."> 直接带目标画质，SWF 以目标画质创建。
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

// ---------- 自动补 zone_id（修复 main.html 无 zone_id 时 fcgi 500 导致 Flash 不加载的黑屏） ----------
class ZoneIdVisitor : public CefCookieVisitor {
public:
    ZoneIdVisitor() = default;

    bool Visit(const CefCookie& cookie, int count, int total,
               bool& deleteCookie) override {
        deleteCookie = false;
        std::string name = CefString(&cookie.name);
        if (name == "sServerID") {
            _server_id = CefString(&cookie.value);
        } else if (name == "tmpLastLoginInfo") {
            _last_login_info = CefString(&cookie.value);
        }
        // 遍历到最后一个 cookie 时提交任务（CEF 可能不回调 count<0 结束信号）
        if (count >= 0 && total > 0 && count == total - 1) {
            if (_server_id.empty())
                ExtractServerFromLoginInfo();
            AppLog::Write("自动补 zone_id: 遍历完成, sServerID=%s",
                          _server_id.empty() ? "(空)" : _server_id.c_str());
            if (!_server_id.empty())
                CefPostTask(TID_UI, new ApplyZoneIdTask(_server_id));
            return false;
        }
        return true;
    }

private:
    static std::string UrlDecode(const std::string& in) {
        std::string out;
        out.reserve(in.size());
        for (size_t i = 0; i < in.size(); ++i) {
            if (in[i] == '%' && i + 2 < in.size()) {
                auto hex = [](char c) -> int {
                    if (c >= '0' && c <= '9') return c - '0';
                    if (c >= 'a' && c <= 'f') return c - 'a' + 10;
                    if (c >= 'A' && c <= 'F') return c - 'A' + 10;
                    return -1;
                };
                int hi = hex(in[i + 1]), lo = hex(in[i + 2]);
                if (hi >= 0 && lo >= 0) {
                    out.push_back(static_cast<char>((hi << 4) | lo));
                    i += 2;
                    continue;
                }
            }
            out.push_back(in[i]);
        }
        return out;
    }

    void ExtractServerFromLoginInfo() {
        if (_last_login_info.empty())
            return;
        std::string raw = UrlDecode(_last_login_info);
        if (raw.empty())
            return;
        std::string key = "\"zonelist\"";
        size_t pos = raw.find(key);
        if (pos == std::string::npos)
            return;
        size_t colon = raw.find(':', pos);
        size_t bracket = raw.find('[', colon);
        if (bracket == std::string::npos)
            return;
        size_t i = bracket + 1;
        while (i < raw.size() && (raw[i] == ' ' || raw[i] == '\t' ||
                                  raw[i] == '\r' || raw[i] == '\n'))
            ++i;
        size_t start = i;
        while (i < raw.size() && isdigit((unsigned char)raw[i]))
            ++i;
        if (i > start) {
            _server_id = raw.substr(start, i - start);
            AppLog::Write("自动补 zone_id: sServerID 缺失，从 tmpLastLoginInfo 提取=%s",
                          _server_id.c_str());
        }
    }

    static std::string SetZoneId(const std::string& url,
                                 const std::string& sid) {
        size_t q = url.find('?');
        if (q == std::string::npos)
            return url + "?zone_id=" + sid;
        std::string base = url.substr(0, q);
        std::string newq;
        size_t pos = q + 1;
        while (pos <= url.size()) {
            size_t amp = url.find('&', pos);
            std::string part =
                url.substr(pos, amp == std::string::npos
                                    ? std::string::npos
                                    : amp - pos);
            if (part.rfind("zone_id=", 0) != 0) {
                if (!newq.empty()) newq += '&';
                newq += part;
            }
            if (amp == std::string::npos) break;
            pos = amp + 1;
        }
        if (!newq.empty()) newq += '&';
        newq += "zone_id=" + sid;
        return base + "?" + newq;
    }

    class ApplyZoneIdTask : public CefTask {
    public:
        explicit ApplyZoneIdTask(const std::string& server_id)
            : server_id_(server_id) {}
        void Execute() override {
            if (!g_game_browser) return;
            CefRefPtr<CefFrame> frame = g_game_browser->GetMainFrame();
            if (!frame || !frame->IsValid()) return;
            std::string url = frame->GetURL().ToString();
            if (url.empty()) return;
            if (url.find("zone_id=" + server_id_) != std::string::npos)
                return;
            std::string new_url = SetZoneId(url, server_id_);
            AppLog::Write("自动补 zone_id: %s -> %s", url.c_str(),
                          new_url.c_str());
            frame->LoadURL(new_url);
        }
        IMPLEMENT_REFCOUNTING(ApplyZoneIdTask);

    private:
        std::string server_id_;
    };

    std::string _server_id;
    std::string _last_login_info;
    IMPLEMENT_REFCOUNTING(ZoneIdVisitor);
};

// 从 cookie 管理器读取 sServerID（存于 .huoying.qq.com 父域，用 VisitAllCookies 全量遍历）。
void CheckAndApplyZoneId() {
    CefRefPtr<CefCookieManager> mgr =
        CefCookieManager::GetGlobalManager(nullptr);
    if (mgr)
        mgr->VisitAllCookies(new ZoneIdVisitor());
}

class CheckAndApplyZoneIdTask : public CefTask {
public:
    void Execute() override { CheckAndApplyZoneId(); }
    IMPLEMENT_REFCOUNTING(CheckAndApplyZoneIdTask);
};

// ---------- 崩溃自动恢复 ----------
namespace {
const int kMaxCrashReloads = 3;       // 连续崩溃自动重载上限
const DWORD kCrashWindowMs = 60000;   // 崩溃统计时间窗（60 秒）
DWORD g_last_crash_tick = 0;          // 上次崩溃时间（TickCount）
int g_crash_reload_count = 0;         // 时间窗内崩溃次数

class ReloadTask : public CefTask {
public:
    void Execute() override { ReloadPage(); }
    IMPLEMENT_REFCOUNTING(ReloadTask);
};

void HandleProcessCrashed(const char* what) {
    DWORD now = ::GetTickCount();
    if (g_last_crash_tick == 0 ||
        now - g_last_crash_tick > kCrashWindowMs) {
        g_crash_reload_count = 0;
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
}  // namespace

// ---------- HostClient ----------

CefRefPtr<CefLifeSpanHandler> HostClient::GetLifeSpanHandler() {
    return this;
}

CefRefPtr<CefRequestHandler> HostClient::GetRequestHandler() {
    return this;
}

CefRefPtr<CefLoadHandler> HostClient::GetLoadHandler() {
    return this;
}

void HostClient::OnRequestContextInitialized(
    CefRefPtr<CefRequestContext> request_context) {
    CefRefPtr<CefValue> allow = CefValue::Create();
    allow->SetInt(1);
    CefString error;
    bool ok = request_context->SetPreference(
        "profile.default_content_setting_values.plugins", allow, error);
    AppLog::Write("设置 plugins content setting=ALLOW: %s (%s)",
                  ok ? "成功" : "失败", error.ToString().c_str());
}

bool HostClient::OnBeforePluginLoad(const CefString& mime_type,
                                    const CefString& plugin_url,
                                    bool is_main_frame,
                                    const CefString& top_origin_url,
                                    CefRefPtr<CefWebPluginInfo> plugin_info,
                                    PluginPolicy* plugin_policy) {
    AppLog::Write("OnBeforePluginLoad: mime=%S main=%d",
                  mime_type.c_str(), is_main_frame ? 1 : 0);
    if (mime_type == "application/x-shockwave-flash") {
        *plugin_policy = PLUGIN_POLICY_ALLOW;
        return true;
    }
    return false;
}

void HostClient::OnAfterCreated(CefRefPtr<CefBrowser> browser) {
    g_game_browser = browser;
    HWND game_hwnd = browser->GetHost()->GetWindowHandle();
    if (game_hwnd) {
        g_game_hwnd = game_hwnd;
        EmbedChild(game_hwnd);
    }
}

// 拦截网页弹窗（选区页「开始游戏」等用 window.open 新窗口）：
// 取消独立窗口，改在主窗口加载目标地址，避免点击后无反应。
bool HostClient::OnBeforePopup(
    CefRefPtr<CefBrowser> browser,
    CefRefPtr<CefFrame> frame,
    const CefString& target_url,
    const CefString& target_frame_name,
    CefLifeSpanHandler::WindowOpenDisposition target_disposition,
    bool user_gesture,
    const CefPopupFeatures& popup_features,
    CefWindowInfo& window_info,
    CefRefPtr<CefClient>& client,
    CefBrowserSettings& settings,
    CefRefPtr<CefDictionaryValue>& extra_info,
    bool* no_javascript_access) {
    if (!target_url.empty() && target_url != "about:blank") {
        browser->GetMainFrame()->LoadURL(target_url);
    }
    return true;
}

void HostClient::OnPluginCrashed(CefRefPtr<CefBrowser> browser,
                                 const CefString& plugin_path) {
    AppLog::Write("崩溃自动恢复: Flash 插件崩溃, plugin=%s",
                  CefString(plugin_path).ToString().c_str());
    HandleProcessCrashed("Flash 插件崩溃");
}

void HostClient::OnRenderProcessTerminated(CefRefPtr<CefBrowser> browser,
                                           TerminationStatus status) {
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

void HostClient::OnLoadStart(CefRefPtr<CefBrowser> browser,
                             CefRefPtr<CefFrame> frame,
                             TransitionType transition_type) {
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
    // Flash 画面铺满窗口（仅性能优先：DPR=1 时 Flash 按 1 倍渲染，铺满到
    // 窗口；画质优先跟随系统 DPI，保持原始布局，不缩放）。
    if (g_force_dpr) {
    frame->ExecuteJavaScript(
        "(function(){"
        "var __fit=function(){"
        "try{"
        "var rt=document.getElementById('resizeTarget');"
        "if(!rt)return;"
        "var w=window.innerWidth,h=window.innerHeight;"
        "if(w<=0||h<=0)return;"
        "rt.style.transformOrigin='0 0';"
        "rt.style.transform='scale('+(w/1920)+','+(h/1080)+')';"
        "}catch(e){}"
        "};"
        "var __fitAll=function(){__fit();setTimeout(__fit,300);};"
        "__fit();"
        "window.addEventListener('resize',__fitAll);"
        "var mo=new MutationObserver(function(){__fit();});"
        "var __watch=function(){"
        "var rt=document.getElementById('resizeTarget');"
        "if(rt){mo.observe(rt,{attributes:true,childList:true,subtree:true});}"
        "else{setTimeout(__watch,200);}"
        "};"
        "__watch();"
        "})();",
        frame->GetURL(), 0);
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

void HostClient::OnLoadEnd(CefRefPtr<CefBrowser> browser,
                           CefRefPtr<CefFrame> frame,
                           int httpStatusCode) {
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
        // 从 cookie 读取 sServerID 自动补 zone_id 重载。
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
bool HostClient::Visit(const CefCookie& cookie, int count, int total,
                       bool& deleteCookie) {
    deleteCookie = false;
    if (count < 0) {
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
                    CefPostDelayedTask(TID_UI,
                                       new WriteResultTask(this), 800);
                } else {
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

bool HostClient::DoClose(CefRefPtr<CefBrowser> browser) {
    return false;
}

void HostClient::OnBeforeClose(CefRefPtr<CefBrowser> browser) {
    if (g_game_browser && g_game_browser->IsSame(browser)) {
        g_game_browser = nullptr;
        AppLog::Write("浏览器已关闭，退出消息循环");
        CefQuitMessageLoop();
    }
}

void HostClient::OnCookieVisitedDone() {
    if (g_login_mode && _login_detected) {
        if (_pending_qq.empty())
            _pending_qq = "0";  // 无法提取 QQ，标记但允许
        CefString qq(_pending_qq);
        WriteLoginResult(qq);
    }
}
