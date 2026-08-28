#pragma once

// 应用级 CefApp：注册 Flash 插件、透传渲染相关命令行开关。

#include "include/cef_app.h"

// 应用级 CefApp（Flash 注册）。
class HostApp : public CefApp,
                public CefBrowserProcessHandler {
public:
    // Flash 插件绝对路径（宽字符解析，避免中文路径编码问题）。
    static std::string FlashPluginPath();

    void OnBeforeCommandLineProcessing(
        const CefString& process_type,
        CefRefPtr<CefCommandLine> command_line) override;

    CefRefPtr<CefBrowserProcessHandler> GetBrowserProcessHandler() override;

private:
    IMPLEMENT_REFCOUNTING(HostApp);
};
