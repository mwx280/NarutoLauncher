# 火影忍者OL 启动器 —— 实施计划

当前架构：CEF 87 x86 单宿主 + Vue 3 UI（HTML/CSS）。本文为技术决策与实施记录。

## 架构总览

```
huoyin_launcher.exe   CEF 87 · x86 单进程
├─ 无边框窗口（Win32 WM_NCHITTEST：拖拽/边缘缩放/最大化/全屏）
├─ 内嵌 HTTP 服务器（服务 Vue UI 的 dist/ 静态文件）
├─ UI 浏览器：加载本地 HTML（Vue 3 构建产物）
├─ 游戏浏览器：加载 Flash 游戏（game.huoying.qq.com）
└─ CefMessageRouter JS↔C++ 桥（窗口控制 / 登录 / 游戏控制）
```

## 已定决策（不可随意变更）

| 决策点 | 结论 | 备注 |
|---|---|---|
| 渲染引擎 | CEF 87.1.13（x86）| 最后一个支持 PPAPI Flash 的内核 |
| Flash 插件 | Flash.cn PPAPI 34.0.0.380（x86）| 官方微端同款 |
| UI 技术 | Vue 3 + Vite + HTML/CSS | 现代化界面，无 Qt 局限 |
| 宿主 | CEF 87 x86 单进程 | UI 与游戏同内核，省 IPC/窗口嵌入 |
| 无边框窗口 | Win32 原生（WM_NCHITTEST）| HTML 拖拽区 + 原生缩放 |
| 多开 | 每账号一个浏览器窗口/标签 | 多账号同时在线 |
| 变速 | 子进程内建时间 API hook | MinHook x86 |
| 签名 | 暂不签名 | 接受 SmartScreen 手动放行 |

## 技术演进记录

- **阶段 0-3（Qt Widgets + CEF 双进程）**：最初方案为 Qt x64 外壳 + CEF 87 x86 渲染器双进程架构，
  已实现无边框外壳、账号管理、托盘等。因 Qt UI 效果与动画性能受限，**该架构已废弃**（代码已删除，git 历史可回溯）。
- **QML 评估（已放弃）**：Qt Quick 在 ARM64 VM x86 模拟下 Windows 窗口合成不稳定（D3D 后端崩溃），
  且引入第二套 UI 技术栈成本高。
- **最终方案（现行）**：CEF 87 x86 单宿主同时承载 UI（HTML/Vue）与游戏（Flash），去 Qt。

## 关键调研结论（仍然有效）

**Flash 音频触发 libcef CHECK 崩溃**（未解决，待攻关）：
- 现象：进游戏/战斗后加载大量音效 SWF，`SyncReader::Read timed out`（audio glitch 累积到 100）
  → Flash（ppapi）进程崩溃 → 黑屏
- Windows 事件日志：`libcef.dll` 触发 `0x80000003`（STATUS_BREAKPOINT / CHECK）崩溃，偏移 0x02a74c2a
- **ARM64 VM 与 x86 真机均复现**，排除模拟环境因素
- `mute-audio` 无效：Flash PPAPI 音频输出不走 Chromium 网页音频路径
- 官方微端用独立 Flash 播放器，音频不经 Chromium 音频栈，故无此崩溃
- 候选方案（未实施）：禁用系统音频设备验证 / 换独立 Flash 播放器 / 深挖 libcef 崩溃点

**Cookie 持久化**：
- 全局 `CefSettings.cache_path` + `persist_session_cookies` 后免登录跨启动生效
- `CefRequestContextSettings` 单独设置时 session cookie 不落盘（需与全局一致）

**游戏技术事实（已验证，开发依据）**：
- 游戏入口：`game.huoying.qq.com/main.html` → swfobject 加载 `res.huoying.com/<version>/entry.swf`
- 登录：ptlogin（QQ 扫码/账密），cookie 域 `.qq.com`；`web.checkLogin()` 校验 `skey`/`p_skey`
- 选区：官方选区页 `huoying.qq.com/server/website/`，`loginNew(serverId)` → `CommLoginApp.cgi` 返回 gameurl
- 扫码：`ssl.ptlogin2.qq.com/ptqrshow?appid=102045649` 获取二维码；`ptqrlogin` 轮询登录态
- 角色信息：`web.huoying.qq.com/getRoleList?openid=<openid>&appid=102045649&access_token=<token>`
  → `role_list` 含 `isvrid`（区服）、`iRoleLevel`（等级），供 UI 展示区服/等级/战力
- 服务器信息：`game.huoying.qq.com/fcgi-bin/query_svr_info.fcgi?svr_id=<zone_id>` → `[ip, port, host, version]`
- 游戏 SWF 暴露 AS3 方法给页面 JS（ExternalInterface）：`setMiniGameData / callNarutoSwf / closeCharge` 等

## 当前进度与待办

**已完成**：
- CEF 宿主：无边框窗口（WM_NCHITTEST 缩放/拖拽）、内嵌 HTTP 服务器、双浏览器实例
- Vue UI：和风卷轴多窗口卷轴台（账号列表/扫码添加/多窗口标签切换/区服等级战力展示/编辑）
- 构建链路：`npm run build` → CMake POST_BUILD 复制 dist + CEF 运行时 + Flash 插件

**待办（优先级从高到低）**：
1. **CEF JS 桥**：CefMessageRouter 实现 JS↔C++（窗口控制按钮、账号持久化到本地文件、系统托盘）
2. **扫码登录实装**：宿主代理 `ptqrlogin` 轮询（跨域 + cookie），成功后保存登录态
3. **游戏窗口接入**：UI「开始游戏」→ 宿主创建/切换游戏浏览器实例，加载 Flash 游戏
4. **角色信息同步**：登录后调 `getRoleList` 更新区服/等级/战力到 UI
5. **Flash 音频崩溃**：解决 libcef CHECK 崩溃（上述候选方案）
6. **打包分发**：GitHub Releases 整包 + version.json

## 开发环境

- 开发机：Windows ARM64 VM（Apple Silicon）
- Visual Studio 2026 Community（MSVC v145，含 x86 交叉工具链）
- Node.js 22+（Vue UI 构建）
- 注意：宿主 x86 全程走模拟；性能数据不代表真机
