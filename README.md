# 火影忍者Online 启动器

一款 Windows 下的火影忍者Online Flash 游戏启动器。WPF 写了界面，游戏窗口通过 HwndHost 嵌在启动器里，用独立的 CEF 游戏进程渲染。支持多账号多开、QQ 扫码登录、记住密码、自动登录。

## 为什么还在用 Flash

火影忍者Online 是 Flash 游戏（AS3），而 Chromium 88 起彻底移除了 Flash（PPAPI）支持。CEF 87 是最后一个能跑 PPAPI Flash 的内核，所以整个项目都是围绕 CEF 87.1.13 做的，Flash 插件用的是 Flash.cn 的 PPAPI 34.0.0.380（中国区官方维护）。

## 架构

```
NarutoLauncher.exe   WPF / C# / x64 —— 启动器界面
└─ HwndHost（GameHostView）跨进程 SetParent，把游戏窗口嵌入界面里
   └─ GameHost.exe（huoyin_launcher）  CEF 87 / x64 / C++ —— 每个账号一个实例
      ├─ 加载 game.huoying.qq.com 的 Flash 游戏
      └─ 变速用 MinHook 挂钩时间 API
```

界面和游戏进程是分开的，游戏崩了不影响启动器，平时也能独立调试。

## 主要功能

- 多账号多开，每个账号独立的缓存目录，cookie 互不串
- QQ 扫码登录、记住密码、免登录进游戏
- 游戏常驻后台时自动补 zone_id、崩溃自动重载
- x64 下 Flash 画质调节（改的是实例创建时的 quality 参数），低画质能明显降负载
- 设置里可切 Flash 画质、分辨率模式、硬件加速开关

## 目录结构

```
NarutoLauncher/
├── app/              游戏宿主（C++/CEF）
├── NarutoLauncher/   启动器（C#/WPF）
├── assets/           图标等资源
├── tools/            构建、依赖下载脚本
├── docs/             技术调研与排错记录
└── third_party/      第三方依赖（CEF SDK、Flash 插件、MinHook）
```

`third_party` 里的 CEF SDK 和运行时由脚本下载，不提交进仓库；Flash 插件和 MinHook 随仓库分发。

## 构建

依赖 .NET SDK 10、Visual Studio（MSVC，含 x64 工具链）、CMake、Ninja。

```powershell
# 1. 下载依赖（CEF SDK + 运行时）
powershell -ExecutionPolicy Bypass -File tools/download_deps.ps1 -Arch x64

# 2. 一键构建（GameHost + 启动器）
powershell -ExecutionPolicy Bypass -File tools/build.ps1 -Arch x64
```

Flash 插件不在仓库里，需要从本机安装的 Flash Player 复制，见 `tools/extract_flash.ps1`。

## 更新

更新走 GitHub Releases。`GitHubUpdateService` 请求 `api.github.com/repos/<owner>/<repo>/releases/latest`，比对版本号，有新版就弹窗让你跳去下载页。

- 当前版本：`v1.0.0`（在 `GitHubUpdateService.cs` 的 `CurrentVersion` 里改，要和 GitHub 的 tag 对上）
- 发布新版：打 tag（比如 `v1.0.0`），把安装包作为 release 资产传上去

## 环境要求

- Windows（开发机是 Apple Silicon 上的 Windows ARM64 VM，也跑得动）
- Visual Studio 2026 Community（MSVC v145）
- .NET SDK 10
- CMake 3.16 + Ninja

## 许可证

MIT，见 [LICENSE](LICENSE)。
