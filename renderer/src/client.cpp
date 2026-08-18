#include "client.h"

#include "include/cef_browser.h"
#include "include/cef_frame.h"
#include "include/cef_web_plugin.h"

#include <algorithm>
#include <string>

namespace {

// 兜底：在页面里自动移除 Flash 的 click-to-play 占位。
// Chromium 87 的占位是渲染进程内建的，JS 无法直接"点击"，这里通过重新创建
// 插件元素强制 Chromium 重新评估加载策略；并非所有场景都有效，仅作兜底。
const char kAutoClickFlashJs[] =
    "(function() {"
    "  var sels = 'embed[type=\"application/x-shockwave-flash\"],"
    "             object[type=\"application/x-shockwave-flash\"]';"
    "  var list = document.querySelectorAll(sels);"
    "  for (var i = 0; i < list.length; i++) {"
    "    var el = list[i];"
    "    var parent = el.parentNode;"
    "    if (!parent || el.dataset.narutoTouched) continue;"
    "    el.dataset.narutoTouched = '1';"
    "    var clone = el.cloneNode(false);"
    "    for (var j = 0; j < el.attributes.length; j++) {"
    "      clone.setAttribute(el.attributes[j].name, el.attributes[j].value);"
    "    }"
    "    parent.replaceChild(clone, el);"
    "  }"
    "})();";

}  // namespace

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

void NarutoClient::OnLoadEnd(CefRefPtr<CefBrowser> browser,
                             CefRefPtr<CefFrame> frame,
                             int httpStatusCode) {
    (void)browser;
    (void)httpStatusCode;
    // 只在主 frame 加载完成后执行，避免每个子 frame 都重复注入
    if (frame->IsMain()) {
        AutoClickFlashPlaceholder(frame);
    }
}

void NarutoClient::AutoClickFlashPlaceholder(CefRefPtr<CefFrame> frame) {
    frame->ExecuteJavaScript(kAutoClickFlashJs, frame->GetURL(), 0);
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
