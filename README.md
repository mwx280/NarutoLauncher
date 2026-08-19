# 火影忍者OL 启动器（naruto-launcher）

高性能火影忍者OL Flash 游戏启动器。WinUI 3 启动器 + 分离 CEF 87 x86 游戏宿主，支持多账号多开、扫码登录、变速/脚本等。

## 架构

```
NarutoLauncher.exe   WinUI 3 · C# · ARM64/x64
├─ 现代 Fluent UI：账号管理 / 扫码登录 / 多开 / 设置
└─ Named Pipe IPC
     └─ GameHost.exe   CEF 87 · x86 · C++（每账号一个实例）
          ├─ 游戏浏览器：加载 Flash 游戏（game.huoying.qq.com）
          └─ 变速 hook（MinHook x86）
```

UI 与游戏分离：UI 崩溃不影响游戏，游戏卡顿不影响 UI。

## 核心技术决策

| 决策点 | 结论 | 原因 |
|---|---|---|
| 渲染引擎 | CEF 87.1.13（x86）| Chromium 88+ 移除 Flash；CEF 87 是最后一个支持 PPAPI Flash 的版本 |
| Flash 插件 | Flash.cn PPAPI 34.0.0.380（x86）| 中国区官方维护，明确支持 Chromium 88 以下内核 |
| UI 技术 | C# + WinUI 3（Windows App SDK）| 现代 Fluent 风格，符合玩家调研"简洁现代"诉求 |
| 启动器架构 | WinUI 3 单进程 + Named Pipe | UI 与游戏分离，互不干扰 |
| 多开 | 每账号一个 GameHost 进程 | 多账号同时在线，独立窗口管理 |

## 目录结构

```
naruto-launcher/
├── app/           CEF 游戏宿主（C++，改造成 GameHost 独立进程）
├── assets/        图标等静态资源（favicon.png）
├── third_party/   第三方依赖（CEF SDK、Flash 插件，.gitignore，由脚本下载）
├── tools/         依赖下载 / 提取脚本
└── docs/          文档（PLAN 实施计划 / 调研数据参考）
```

## 构建

> 详细计划见 [docs/PLAN.md](docs/PLAN.md)。

```powershell
# 1. 准备依赖（下载 CEF SDK、提取 Flash 插件）
powershell -ExecutionPolicy Bypass -File tools/download_deps.ps1

# 2. 构建 WinUI 3 启动器（NarutoLauncher，待建）
dotnet build

# 3. 构建 CEF 游戏宿主（x86，MSVC 交叉环境 vcvarsarm64_x86.bat）
cmake -S app -B build/app -G Ninja -DCMAKE_BUILD_TYPE=Release
cmake --build build/app
```

## 环境要求

- Windows（开发机：Windows ARM64 VM on Apple Silicon）
- Visual Studio 2026 Community（MSVC v145，含 x86 交叉工具链；"Windows 应用开发"工作负载）
- .NET SDK + Windows App SDK（WinUI 3）
- CMake 3.16 + Ninja
