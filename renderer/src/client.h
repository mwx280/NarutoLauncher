#pragma once

#include "include/cef_client.h"
#include "include/cef_life_span_handler.h"
#include "include/cef_load_handler.h"

#include <set>

// CefClient 实现：负责浏览器生命周期与页面加载状态。
// 阶段 1 可行性验证用最小实现。
class NarutoClient : public CefClient,
                     public CefLifeSpanHandler,
                     public CefLoadHandler {
public:
    NarutoClient();

    // CefClient
    CefRefPtr<CefLifeSpanHandler> GetLifeSpanHandler() override { return this; }
    CefRefPtr<CefLoadHandler> GetLoadHandler() override { return this; }

    // CefLifeSpanHandler
    void OnAfterCreated(CefRefPtr<CefBrowser> browser) override;
    bool DoClose(CefRefPtr<CefBrowser> browser) override;
    void OnBeforeClose(CefRefPtr<CefBrowser> browser) override;

    // CefLoadHandler
    void OnLoadingStateChange(CefRefPtr<CefBrowser> browser,
                              bool isLoading,
                              bool canGoBack,
                              bool canGoForward) override;
    void OnLoadError(CefRefPtr<CefBrowser> browser,
                     CefRefPtr<CefFrame> frame,
                     ErrorCode errorCode,
                     const CefString& errorText,
                     const CefString& failedUrl) override;

    // 关闭所有浏览器（退出时调用）
    void CloseAllBrowsers(bool force_close);

    // 是否仍有存活的浏览器
    bool HasBrowser() const { return !browser_list_.empty(); }

private:
    // 存活浏览器集合
    typedef std::set<CefRefPtr<CefBrowser>> BrowserSet;
    BrowserSet browser_list_;
    bool is_closing_;

    IMPLEMENT_REFCOUNTING(NarutoClient);
    DISALLOW_COPY_AND_ASSIGN(NarutoClient);
};
