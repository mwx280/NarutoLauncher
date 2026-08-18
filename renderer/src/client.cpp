#include "client.h"

#include "include/cef_browser.h"

#include <algorithm>

NarutoClient::NarutoClient() : is_closing_(false) {}

void NarutoClient::OnAfterCreated(CefRefPtr<CefBrowser> browser) {
    browser_list_.insert(browser);
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
