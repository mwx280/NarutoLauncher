#pragma once

// 浏览器客户端：管理浏览器生命周期、Flash 插件策略、崩溃自动恢复、页面注入。
// 火影版：在 CEFFlashGameHost 基础上扩展画质 hook 注入、zone_id 自动补全、
// 登录检测（扫码/自动登录）、窗口铺满等游戏特定逻辑。

#include "include/cef_browser.h"
#include "include/cef_client.h"
#include "include/cef_life_span_handler.h"
#include "include/cef_load_handler.h"
#include "include/cef_request_context_handler.h"
#include "include/cef_request_handler.h"
#include "include/cef_web_plugin.h"

class HostClient : public CefClient,
                   public CefLifeSpanHandler,
                   public CefLoadHandler,
                   public CefRequestContextHandler,
                   public CefRequestHandler,
                   public CefCookieVisitor {
public:
    HostClient() = default;

    CefRefPtr<CefLifeSpanHandler> GetLifeSpanHandler() override;
    CefRefPtr<CefRequestHandler> GetRequestHandler() override;
    CefRefPtr<CefLoadHandler> GetLoadHandler() override;

    void OnRequestContextInitialized(
        CefRefPtr<CefRequestContext> request_context) override;

    bool OnBeforePluginLoad(const CefString& mime_type,
                            const CefString& plugin_url,
                            bool is_main_frame,
                            const CefString& top_origin_url,
                            CefRefPtr<CefWebPluginInfo> plugin_info,
                            PluginPolicy* plugin_policy) override;

    void OnAfterCreated(CefRefPtr<CefBrowser> browser) override;
    bool OnBeforePopup(CefRefPtr<CefBrowser> browser,
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
                       bool* no_javascript_access) override;
    void OnPluginCrashed(CefRefPtr<CefBrowser> browser,
                         const CefString& plugin_path) override;
    void OnRenderProcessTerminated(CefRefPtr<CefBrowser> browser,
                                   TerminationStatus status) override;
    void OnLoadStart(CefRefPtr<CefBrowser> browser,
                     CefRefPtr<CefFrame> frame,
                     TransitionType transition_type) override;
    void OnLoadEnd(CefRefPtr<CefBrowser> browser,
                   CefRefPtr<CefFrame> frame,
                   int httpStatusCode) override;
    bool DoClose(CefRefPtr<CefBrowser> browser) override;
    void OnBeforeClose(CefRefPtr<CefBrowser> browser) override;

    // ---- 登录检测（CefCookieVisitor）----
    bool Visit(const CefCookie& cookie, int count, int total,
               bool& deleteCookie) override;

    // 自动登录轮询是否已结束（cookie 出现登录态）。
    bool IsAutoLoginDone() const { return _login_detected; }

    // 登录检测完成后写结果（由 cookie 遍历结束触发）。
    void OnCookieVisitedDone();

private:
    bool _login_detected = false;
    bool _auto_login_started = false;
    std::string _pending_qq;

    IMPLEMENT_REFCOUNTING(HostClient);
};
