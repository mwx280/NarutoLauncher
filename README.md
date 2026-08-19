# 火影忍者OL 启动器（naruto-launcher）

高性能火影忍者OL Flash 游戏启动器。CEF 87 单宿主 + Vue UI，支持多账号多开、扫码登录、免登录切换。

## 架构

```
huoyin_launcher.exe   CEF 87 · x86 单进程
├─ 无边框窗口（Win32 原生：拖拽/边缘缩放/最大化/全屏）
├─ 内嵌 HTTP 服务器（服务 Vue UI 构建产物）
├─ UI 浏览器：加载本地 HTML（Vue 3 构建）
├─ 游戏浏览器：加载 Flash 游戏（huoying.qq.com）
└─ JS 桥（CefMessageRouter）←→ 原生窗口控制 / 登录 / 游戏控制
```

单进程双浏览器实例，无 Qt、无独立 IPC 进程。

## 核心技术决策

| 决策点 | 结论 | 原因 |
|---|---|---|
| 渲染引擎 | CEF 87.1.13（x86）| Chromium 88+ 移除 Flash；CEF 87 是最后一个支持 PPAPI Flash 的版本 |
| Flash 插件 | Flash.cn PPAPI 34.0.0.380（x86）| 中国区官方维护，明确支持 Chromium 88 以下内核 |
| UI 技术 | Vue 3 + Vite + HTML/CSS | 现代化界面（和风卷轴风格），无 Qt 局限 |
| 宿主 | CEF 87 x86 单进程 | UI 与游戏同内核，省 IPC 与窗口嵌入复杂度 |
| 无边框窗口 | Win32 WM_NCHITTEST | HTML `-webkit-app-region` 拖拽 + 原生边缘缩放 |
| 多开 | 每账号一个浏览器实例/窗口 | 多账号同时在线，顶部标签切换 |

## 目录结构

```
naruto-launcher/
├── app/           CEF 宿主 + Vue UI
│   ├── src/       C++ 宿主（main/frameless_window/http_server/app_log）
│   └── ui/        Vue 3 前端（components/composables）
├── third_party/   第三方依赖（CEF SDK、Flash 插件，.gitignore，由脚本下载）
├── tools/         依赖下载 / 提取脚本
└── docs/          文档
```

## 构建

> 详细计划见 [docs/PLAN.md](docs/PLAN.md)。

```powershell
# 1. 准备依赖（下载 CEF SDK、提取 Flash 插件）
powershell -ExecutionPolicy Bypass -File tools/download_deps.ps1

# 2. 构建 Vue UI
cd app/ui
npm install
npm run build

# 3. 构建宿主（x86，MSVC 交叉环境 vcvarsarm64_x86.bat）
cmake -S app -B build/app -G Ninja
cmake --build build/app
```

## 环境要求

- Windows（开发机：Windows ARM64 VM on Apple Silicon）
- Visual Studio 2026 Community（MSVC v145，含 x86 交叉工具链）
- Node.js 22+（Vue UI 构建）
- CMake 3.16 + Ninja
