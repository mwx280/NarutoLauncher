# third_party —— 第三方预编译依赖

本目录存放第三方预编译依赖，**由脚本下载或复制，不入 Git**（见根目录 `.gitignore`）。

## 依赖清单

| 依赖 | 用途 | 获取方式 |
|---|---|---|
| CEF 87.1.13 运行时（x86）| 渲染器内核（最后一个支持 PPAPI Flash 的 Chromium）| `tools/download_deps.ps1` 自动下载（NuGet cef.redist.x86）|
| CEF 87.1.13 SDK | 渲染器头文件 + libcef.lib + libcef_dll_wrapper.lib | `tools/download_deps.ps1` 自动下载（NuGet cef.sdk）|
| pepflashplayer.dll（Flash PPAPI 34 x86）| Flash 插件 | `tools/extract_flash.ps1`，见下方说明 |

## 准备命令

```powershell
# 一次性准备 CEF 运行时与 SDK
powershell -ExecutionPolicy Bypass -File tools/download_deps.ps1

# 获取 Flash 插件
powershell -ExecutionPolicy Bypass -File tools/extract_flash.ps1
```

## Flash 插件获取说明（重要）

- Flash.cn 官方 PPAPI 安装包是**专有引导程序，不支持静默解压**（实测 `/extract` 无效），也不内嵌可读的 `pepflashplayer.dll`。
- `tools/extract_flash.ps1` 逻辑：
  1. 若系统已装 Flash（`C:\Windows\SysWOW64\Macromed\Flash\pepflashplayer.dll`），直接复制到本目录；
  2. 否则下载官方安装包到本目录，**提示你手动运行安装**，装完再跑一次脚本即自动复制。
- **安全提示**：务必使用官方安装包并核对签名，勿从第三方站点下载插件。
- 版本锁定：Flash.cn 官方 34.0.0.380（2026-06 更新），与 CEF 87（Chromium 88 以下内核）兼容。

## 版本锁定

- CEF：`87.1.13`（`cef.redist.x86` / `cef.sdk` NuGet，与 chromium 87.0.4280.141 对应），勿随意升级。
- Flash：`34.0.0.380`（x86），中国区官方维护。
