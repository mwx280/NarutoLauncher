# 火影忍者OL 启动器（NarutoLauncher）

高性能火影忍者OL Flash 游戏启动器。WPF 启动器 + 分离 CEF 87 x86 游戏宿主，游戏窗口通过 HwndHost 内嵌在界面中，支持多账号多开、扫码登录、记住密码、自动登录。

## 架构

```
NarutoLauncher.exe   WPF · C# · x64
├─ 系统原生 UI：首页 / 游戏 / 账号管理 / 设置
└─ HwndHost（GameHostView）→ 跨进程 SetParent
     └─ GameHost.exe   CEF 87 · x86 · C++（每账号一个实例）
          ├─ 游戏窗口内嵌显示在启动器界面中
          ├─ Flash 游戏（game.huoying.qq.com）
          └─ 变速 hook（MinHook x86）
```

游戏窗口真正内嵌在启动器界面内（WPF HwndHost 跨进程嵌入），UI 与游戏进程分离。

## 核心技术决策

| 决策点 | 结论 | 原因 |
|---|---|---|
| 渲染引擎 | CEF 87.1.13（x86）| Chromium 88+ 移除 Flash；CEF 87 是最后一个支持 PPAPI Flash 的版本 |
| Flash 插件 | Flash.cn PPAPI 34.0.0.380（x86）| 中国区官方维护，明确支持 Chromium 88 以下内核 |
| UI 技术 | C# + WPF | HwndHost 原生支持跨进程窗口内嵌（已验证）|
| 启动器架构 | WPF 单进程 + 跨进程 SetParent | UI 与游戏分离，游戏窗口内嵌 |
| 多开 | 每账号一个 GameHost 进程 | 多账号同时在线，独立窗口内嵌 |

## 目录结构

```
NarutoLauncher/
├── app/            CEF 游戏宿主（C++，独立进程）
├── NarutoLauncher/ WPF 启动器（C#，含 HwndHost 内嵌）
├── assets/         图标等静态资源（favicon.png）
├── third_party/    第三方依赖（CEF SDK、Flash 插件，.gitignore，由脚本下载）
├── tools/          依赖下载 / 提取脚本
└── docs/           文档（PLAN 实施计划 / 调研数据参考）
```

## 构建

> 详细计划见 [docs/PLAN.md](docs/PLAN.md)。

```powershell
# 1. 准备依赖（下载 CEF SDK、提取 Flash 插件）
powershell -ExecutionPolicy Bypass -File tools/download_deps.ps1

# 2. 构建 CEF 游戏宿主（x86，MSVC 交叉环境 vcvarsarm64_x86.bat）
cmake -S app -B build/app -G Ninja -DCMAKE_BUILD_TYPE=Release
cmake --build build/app

# 3. 构建 WPF 启动器，并把 GameHost 复制到其输出目录的 GameHost/ 下
dotnet build NarutoLauncher -c Release
```

> 一键构建（依赖下载 + GameHost x86 + WPF + GameHost 复制）见 [build.ps1](build.ps1)。

## 环境要求

- Windows（开发机：Windows ARM64 VM on Apple Silicon）
- Visual Studio 2026 Community（MSVC v145，含 x86 交叉工具链）
- .NET SDK 10
- CMake 3.16 + Ninja
