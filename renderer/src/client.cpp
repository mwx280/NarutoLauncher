#include "client.h"

#include "include/cef_browser.h"
#include "include/cef_web_plugin.h"

#include <algorithm>

NarutoClient::NarutoClient() : is_closing_(false) {}

bool NarutoClient::OnBeforePluginLoad(const CefString& mime_type,
                                      const CefString& plugin_url,
                                      bool is_main_frame,
                                      const CefString& top_origin_url,
                                      CefRefPtr<CefWebPluginInfo> plugin_info,
                                      PluginPolicy* plugin_policy) {
    // 对 Flash 插件（mime 为 application/x-shockwave-flash）强制允许运行。
    // 注：在 Chromium 87 中，即使此处返回 ALLOW，Flash 仍可能被
    // click-to-play 占位拦截（见工程笔记），需要配合其他手段。
    if (mime_type == "application/x-shockwave-flash") {
        *plugin_policy = PLUGIN_POLICY_ALLOW;
        return true;
    }
    return false;
}

void NarutoClient::OnAfterCreated(CefRefPtr<CefBrowser> browser) {
    browser_list_.insert(browser);
}

CefRefPtr<CefBrowser> NarutoClient::GetFirstBrowser() const {
    if (browser_list_.empty())
        return nullptr;
    return *browser_list_.begin();
}

bool NarutoClient::DoClose(CefRefPtr<CefBrowser> browser) {
    // 若正在关闭全部浏览器，则允许关闭
    return is_closing_;
}

void NarutoClient::OnBeforeClose(CefRefPtr<CefBrowser> browser) {
    browser_list_.erase(browser);
}

void NarutoClient::OnLoadingStateChange(CefRefPtr<CefBrowser> browser,
                                        bool isLoading,
                                        bool canGoBack,
                                        bool canGoForward) {
    (void)isLoading;
    (void)canGoBack;
    (void)canGoForward;
}

void NarutoClient::OnLoadError(CefRefPtr<CefBrowser> browser,
                               CefRefPtr<CefFrame> frame,
                               ErrorCode errorCode,
                               const CefString& errorText,
                               const CefString& failedUrl) {
    (void)browser;
    (void)frame;
    (void)errorText;
    // 记录加载错误
    OutputDebugStringA(("LoadError: " + errorText.ToString()).c_str());
}

void NarutoClient::CloseAllBrowsers(bool force_close) {
    if (browser_list_.empty())
        return;

    BrowserSet::const_iterator it = browser_list_.begin();
    for (; it != browser_list_.end(); ++it) {
        (*it)->GetHost()->CloseBrowser(force_close);
    }
}
