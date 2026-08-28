# 火影忍者Online 启动器（NarutoLauncher）

基于 **CEF 87** 的火影忍者Online Flash 游戏启动器，开源版本。WPF 界面 + 独立 CEF 游戏宿主，游戏窗口通过 HwndHost 跨进程内嵌，支持多账号多开、QQ 扫码登录、记住密码、自动登录。

> **技术背景**：Chromium 88+ 彻底移除了 Flash（PPAPI）支持，**CEF 87 是最后一个支持 PPAPI Flash 的版本**。本项目围绕这一内核做了大量兼容与性能打磨，使其能稳定、流畅地承载火影忍者Online 页游。

> **游戏内核来源**：本仓库的 `cef_flash_game_host.exe`（游戏渲染进程）基于开源项目 [CEFFlashGameHost](https://github.com/mwx280/CEFFlashGameHost)（通用 CEF 87 Flash 渲染内核）改造而来，在其模块化架构上增加了火影忍者Online 的登录、cookie、zone_id、变速等特定功能。

## 核心亮点

### Flash 兼容性

- **选择 CEF 87.1.13**：最后一个内置 PPAPI Flash 支持的 Chromium 内核，与 Flash.cn PPAPI 34.0.0.380 插件精确匹配。
- **修复 x64 Flash 沙盒崩溃**：Flash 插件进程在沙盒环境初始化时崩溃（BEX64/0xc0000005），通过 `no-sandbox` 消除，x64 下 Flash 正常运行。
- **绕过 click-to-play 拦截**：把插件 content setting 强制设为 `ALLOW`，并拦截 `OnBeforePluginLoad` 对所有 Flash 请求放行，杜绝 "Right-click to run Adobe Flash Player" 占位提示。
- **消除 Flash 沙箱探测弹窗**：Flash 每次加载会执行 `cmd.exe /c echo NOT SANDBOXED` 探测沙箱，导致 cmd 窗口一闪而过；通过 hook `CreateProcessW/A` 强制追加 `CREATE_NO_WINDOW`，全程无控制台闪现。

### 渲染性能优化

- **PPAPI 级画质控制**：Flash 的 `quality` 只在 SWF 实例化时读取一次。通过 hook `PPP_GetInterface` → `PPP_Instance::DidCreate`，在 Flash 实例创建时真正改写 quality 参数（low/medium/high），全场景（主城/UI/战斗）生效。
- **强制 DPR=1**：Flash 以 1 倍物理分辨率渲染，让 `quality=low` 的降质真正生效（否则 DPR=2 下降质不明显）。
- **GPU 加速开关**：默认软件渲染、禁用 Stage3D/3D API，也可用 `--flash-gpu=1` 恢复硬件加速。

### 游戏能力

- **多开隔离**：每账号独立 `--userdata` 缓存目录，cookie 与本地存储互不干扰。
- **登录闭环**：QQ 扫码登录（写 `login_result.txt`）、账号密码自动填表、cookie 注入免登录进游戏。
- **自动补 zone_id**：main.html 缺 zone_id 时 fcgi 500 导致 Flash 不加载（黑屏），从 cookie 读取 sServerID 自动补参重载。
- **崩溃自动恢复**：Flash 插件/渲染进程崩溃后自动重载页面（60 秒内超 3 次自动停止）。
- **游戏变速**：子进程内建时间 API hook（MinHook），可 0.5x/1x/2x/4x 变速。

### 稳定性保障

- **优雅关闭**：收到 `WM_CLOSE` 先关闭 CEF 浏览器再退出，确保 cookie / 本地存储正常刷盘。
- **防误启动**：游戏内核直接双击（无 `--embed` / `--windowed` 参数）视为误启动，静默退出不创建窗口。

## 架构

```
┌─────────────────────────────────────────────────────────────┐
│  NarutoLauncher.exe   WPF · C# · x64 —— 启动器界面              │
│  ├─ 系统原生 UI：首页 / 游戏 / 账号管理 / 设置                    │
│  └─ HwndHost（GameHostView）→ 跨进程 SetParent                   │
└───────────────────────────┬─────────────────────────────────┘
                            │ HwndHost / SetParent 内嵌
                            ▼
   cef_flash_game_host.exe   CEF 87 · C++ · x64（每账号一个实例）
   ├─ 无边框窗口（FramelessWindow，WM_NCHITTEST 缩放/拖拽）
   ├─ Flash 插件（pepflashplayer.dll，PPAPI 34）
   └─ Flash 画质 hook / 沙箱弹窗 hook / 变速 hook（MinHook）
```

游戏窗口真正内嵌在启动器界面内（WPF HwndHost 跨进程嵌入），UI 与游戏进程分离，游戏崩了不影响启动器。

## 目录结构

```
NarutoLauncher/
├── app/                CEF 游戏内核（C++，模块化）
│   └── src/            main（入口）/ params / host_app / host_client / globals /
│                       flash_hook / no_console_hook / frameless_window / speed_hook / app_log
├── NarutoLauncher/     WPF 启动器（C#/WPF）
│   ├── Services/       账号、设置、游戏进程、更新检查等服务
│   └── Views/          各页面（首页/游戏/账号管理/设置/关于）
├── assets/             图标等资源（favicon.png / app.ico）
├── docs/               技术调研与排错记录
├── installer/          Inno Setup 安装脚本
├── tools/              构建 / 依赖下载 / Flash 提取脚本
├── third_party/        第三方依赖（CEF 由脚本下载；Flash 插件与 MinHook 随仓库分发）
└── .github/workflows/  GitHub Actions 构建发布流水线
```

## 快速构建

环境要求：Windows、Visual Studio（MSVC，x64 工具链）、CMake + Ninja、.NET SDK 10。

```powershell
# 1. 准备依赖（下载 CEF SDK 与运行时，-Arch 选 x64）
powershell -ExecutionPolicy Bypass -File tools/download_deps.ps1 -Arch x64

# 2. 一键构建（GameHost + 启动器 + GameHost 复制）
powershell -ExecutionPolicy Bypass -File tools/build.ps1 -Arch x64
```

产物：`NarutoLauncher\bin\Release\net10.0-windows\win-x64\NarutoLauncher.exe`（含 `GameHost\cef_flash_game_host.exe`、CEF 运行时、Flash 插件、MinHook）。

> 说明：CEF SDK 与运行时由脚本从 NuGet 下载，不提交进仓库；Flash 插件与 MinHook 已随仓库分发。

## 快速使用

```powershell
# 调试：独立窗口加载游戏（独立会话）
NarutoLauncher.exe

# 游戏内核嵌入宿主（由启动器调用）
cef_flash_game_host.exe --embed --parent=123456 --url="https://game.huoying.qq.com/main.html" --userdata="C:\game\account1" --flash-quality=low

# 查看内核帮助
cef_flash_game_host.exe --help
```

## 命令行参数

| 参数 | 说明 |
|---|---|
| `--url=<url>` | 要加载的页面 URL（默认游戏入口） |
| `--userdata=<dir>` | 独立缓存目录（多开隔离 cookie / 本地存储） |
| `--title=<title>` | 窗口标题 |
| `--parent=<hwnd>` | 内嵌父窗口句柄 |
| `--embed` | 以内嵌子窗口运行，窗口句柄写入 `<userdata>\window_hwnd.txt` |
| `--windowed` | 以独立有边框窗口运行（调试 / 独立会话） |
| `--login` | 扫码登录模式（加载 QQ 登录页，登录成功写 `login_result.txt`） |
| `--cookie=<b64>` | 启动时注入的 cookie（base64 编码的 JSON，免登录进游戏） |
| `--user=<b64>` / `--pass=<b64>` | 账号密码自动登录（base64，无 cookie 时自动填表） |
| `--flash-gpu=1` | 开启 Flash 硬件加速（默认关闭，软件渲染） |
| `--flash-quality=<low/medium/high>` | Flash 渲染画质（默认 low，PPAPI hook 生效，改档需重载） |
| `--force-dpr=0` | 关闭强制 DPR=1（跟随系统 DPI，画质优先） |
| `--debug-port=<port>` | 启用 CEF DevTools 远程调试 |

> 游戏内核直接双击（无 `--embed` / `--windowed` 参数）视为误启动，静默退出不创建窗口。

## 更新

更新走 **GitHub Releases**。`GitHubUpdateService` 请求 `api.github.com/repos/mwx280/NarutoLauncher/releases/latest`，比对语义化版本号，有新版弹窗提示并跳转下载页。

- 当前版本：`v1.0.0`（在 `GitHubUpdateService.cs` 的 `CurrentVersion` 修改，须与 GitHub tag 一致）
- 发布新版：在 GitHub 打 tag（如 `v1.0.0`），由 CI 自动构建安装包并上传到 Release

## 许可证

本项目以 **GNU General Public License v3.0（GPL-3.0）** 开源，见 [LICENSE](LICENSE)。游戏内核上游项目 [CEFFlashGameHost](https://github.com/mwx280/CEFFlashGameHost) 以 AGPL-3.0 开源。
