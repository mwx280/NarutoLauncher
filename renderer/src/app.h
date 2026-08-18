#pragma once

#include "include/cef_app.h"
#include "include/cef_base.h"

// CefApp 实现：负责在命令行处理阶段注册 Flash PPAPI 插件
// （--ppapi-flash-path / --ppapi-flash-version），并为浏览器/渲染/子进程
// 提供对应的 handler。
class NarutoApp : public CefApp,
                  public CefBrowserProcessHandler {
public:
    NarutoApp();

    // CefApp
    void OnBeforeCommandLineProcessing(
        const CefString& process_type,
        CefRefPtr<CefCommandLine> command_line) override;
    CefRefPtr<CefBrowserProcessHandler> GetBrowserProcessHandler() override {
        return this;
    }

private:
    IMPLEMENT_REFCOUNTING(NarutoApp);
    DISALLOW_COPY_AND_ASSIGN(NarutoApp);
};
