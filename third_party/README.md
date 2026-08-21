# third_party —— 第三方预编译依赖

本目录存放第三方预编译依赖。除 **Flash 插件**（特殊优化版，随仓库分发）外，
其余均由脚本下载，不入 Git（见根目录 `.gitignore`）。

## 内容清单

| 文件/目录 | 用途 | 获取方式 |
|---|---|---|
| CEF 87.1.13 运行时（x86/x64） | 渲染内核，最后一个支持 PPAPI Flash 的 Chromium | `tools/download_deps.ps1` 自动下载（NuGet cef.redist.x86/x64） |
| CEF 87.1.13 SDK | 渲染头文件 + libcef.lib + libcef_dll_wrapper.lib | `tools/download_deps.ps1` 自动下载（NuGet cef.sdk） |
| pepflashplayer.dll（Flash PPAPI 34 x86） | Flash 插件（x86，特殊优化版） | **随仓库分发**（已入库） |
| pepflashplayer_x64.dll（Flash PPAPI 34 x64） | Flash 插件（x64，特殊优化版，**当前不可用**） | **随仓库分发**（已入库） |

## 准备依赖

```powershell
# 一键准备 CEF 运行时 + SDK（-Arch 可选 x86/x64）
powershell -ExecutionPolicy Bypass -File tools/download_deps.ps1 -Arch x86
```

> Flash 插件已随仓库分发，无需额外下载；如需重新提取可参考 `tools/extract_flash.ps1`。

## Flash 插件说明（重要）

- **x86 版（pepflashplayer.dll）**：特殊优化版，已通过测试，Flash 游戏正常运行，为正式使用版本。
- **x64 版（pepflashplayer_x64.dll）**：特殊优化版，但 **x64 Flash 在当前环境无法运行**
  （ppapi 进程启动即崩溃 0xc0000005，ARM64 模拟器与 x64 真机均失败）。
  详见 [docs/X64_FLASH_ISSUE.md](../docs/X64_FLASH_ISSUE.md)。
- 渲染器注册时 `--ppapi-flash-path` 指向 GameHost 同目录下的 `pepflashplayer.dll`，
  CMake 按架构自动复制对应版本。

## 版本记录

- CEF：`87.1.13`（cef.redist.x86 / cef.sdk NuGet，对应 chromium 87.0.4280.141）
- Flash：`34.0.0.380`（x86 优化版可用；x64 优化版不可用）
