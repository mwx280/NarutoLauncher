#pragma once

#include "include/cef_client.h"
#include "include/cef_life_span_handler.h"
#include "include/cef_load_handler.h"
#include "include/cef_request_context_handler.h"

#include <set>

// CefClient 实现：负责浏览器生命周期、页面加载状态，
// 并处理 Flash 插件的自动运行策略。
class NarutoClient : public CefClient,
                     public CefLifeSpanHandler,
                     public CefLoadHandler,
                     public CefRequestContextHandler {
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

    // CefRequestContextHandler
    // 允许 Flash 插件自动运行，避免出现"右键点击运行 Flash"的占位提示
    bool OnBeforePluginLoad(const CefString& mime_type,
                            const CefString& plugin_url,
                            bool is_main_frame,
                            const CefString& top_origin_url,
                            CefRefPtr<CefWebPluginInfo> plugin_info,
                            PluginPolicy* plugin_policy) override;

    // 关闭所有浏览器（退出时调用）
    void CloseAllBrowsers(bool force_close);

    // 是否仍有存活的浏览器
    bool HasBrowser() const { return !browser_list_.empty(); }

    // 获取当前浏览器（供主窗口 WM_SIZE 同步视图尺寸）
    CefRefPtr<CefBrowser> GetFirstBrowser() const;

private:
    // 存活浏览器集合
    typedef std::set<CefRefPtr<CefBrowser>> BrowserSet;
    BrowserSet browser_list_;
    bool is_closing_;

    IMPLEMENT_REFCOUNTING(NarutoClient);
    DISALLOW_COPY_AND_ASSIGN(NarutoClient);
};
