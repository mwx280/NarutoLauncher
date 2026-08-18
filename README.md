# 火影忍者Online 启动器（naruto-launcher）

高性能火影忍者OL Flash 游戏启动器。原生 Qt 外壳 + 内嵌 CEF 渲染器，支持脚本与变速功能。

## 架构总览

```
shell.exe     Qt 6.8.3 · x64 单构建（x64 原生 / ARM64 模拟）
├─ 原生 UI：登录态 · 服务器列表 · 公告 · 设置（Qt Widgets）
├─ 脚本引擎：QJSEngine（JS 脚本，零额外依赖）
├─ IPC 客户端：QLocalSocket → Named Pipe
└─ 游戏视图容器：createWindowContainer(fromWinId(rendererHwnd))

renderer.exe  C++ · CEF 87.1.13 · x86 单构建（三平台通用）
├─ CEF 宿主（browser + subprocess 双模式，同一 exe）
├─ Flash PPAPI 34 x86 注册
├─ CefMessageRouter JS↔原生桥（脚本控制通道）
├─ 变速 hook 模块（子进程内建，时间 API 缩放）
├─ IPC 服务端：Named Pipe
└─ cookie store 持久化（免登录）
```

## 核心技术决策

| 决策点 | 结论 | 原因 |
|---|---|---|
| 渲染引擎 | CEF 87.1.13（x86）| Chromium 88+ 移除 Flash；CEF 87 是最后一个支持 PPAPI Flash 的版本 |
| Flash 插件 | Flash.cn PPAPI 34.0.0.380（x86）| 中国区官方维护，明确支持 Chromium 88 以下内核 |
| 外壳技术栈 | Qt 6.8.3（x64，Widgets）| 原生三能力全覆盖（HTTP/JSON/NamedPipe/窗口嵌入）；本机已装 ARM64 kit |
| 渲染器架构 | 固定 x86 | x86 程序三平台通吃（x64 走 WoW64、ARM64 走 x86 模拟，模拟最快最成熟）|
| 嵌入方式 | createWindowContainer | Qt 官方 foreign window 机制，免手写 SetParent 焦点/DPI 处理 |
| 脚本引擎 | JS / QJSEngine | 游戏可编程面 = 页面级 JS（ExternalInterface 桥，`allowScriptAccess="always"`）|
| 变速方案 | 子进程内建时间 API hook | CE 变速同款原理，x86 进程 hook 生态最成熟 |
| 签名 | 暂不签名 | 接受 SmartScreen 手动放行 |

## 目录结构

```
naruto-launcher/
├── shell/        外壳（Qt x64）：UI · 脚本引擎 · IPC 客户端 · 视图容器
├── renderer/     渲染器（C++/CEF 87 x86）：宿主 · Flash · JS 桥 · 变速 hook · IPC 服务端
├── third_party/  第三方依赖（CEF SDK、Flash 插件，.gitignore，由脚本下载）
├── tools/        依赖下载 / 提取 / 打包脚本
└── docs/         文档
```

## 构建

> 详细分阶段计划见 [docs/PLAN.md](docs/PLAN.md)。

```powershell
# 准备依赖（下载 CEF SDK、提取 Flash 插件）
powershell -ExecutionPolicy Bypass -File tools/download_deps.ps1

# 配置构建（需先执行 vcvarsall 进入 MSVC 环境）
cmake -S . -B build/shell -G Ninja -DCMAKE_PREFIX_PATH="<Qt x64 路径>"
cmake --build build/shell
```

## 环境要求

- Windows（开发机：Windows ARM64 VM on Apple Silicon）
- Qt 6.8.3（msvc2022_arm64 已装；msvc2022_win64 待加装）
- Visual Studio 2026 Community（MSVC v145，含 x86/x64/arm64 交叉工具链）
- CMake 3.30 + Ninja（Qt 自带）
