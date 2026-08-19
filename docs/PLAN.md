# 火影忍者OL 启动器 —— 实施计划

当前架构：WinUI 3 启动器（C#）+ 分离的 CEF 87 x86 游戏宿主进程。本文为技术决策与实施记录。

## 架构总览

```
NarutoLauncher.exe（WinUI 3 · C# · ARM64/x64）
├─ 现代 Fluent UI：账号管理 / 扫码登录 / 多开 / 设置
├─ Named Pipe IPC
└─ GameHost.exe（CEF 87 · x86 · C++）—— 每开一个账号一个实例
     ├─ 游戏浏览器：加载 Flash 游戏（game.huoying.qq.com）
     └─ 变速 hook（MinHook x86，子进程时间 API）
```

## 已定决策（不可随意变更）

| 决策点 | 结论 | 备注 |
|---|---|---|
| 渲染引擎 | CEF 87.1.13（x86）| 最后一个支持 PPAPI Flash 的内核 |
| Flash 插件 | Flash.cn PPAPI 34.0.0.380（x86）| 官方微端同款 |
| UI 技术 | C# + WinUI 3（Windows App SDK）| 现代 Fluent，符合调研"简洁现代"诉求 |
| 启动器架构 | WinUI 3 单进程 + Named Pipe | UI 与游戏分离，UI 崩溃不影响游戏 |
| 多开 | 每账号一个 GameHost 进程 | 多账号同时在线 |
| 变速 | GameHost 子进程内建时间 API hook | MinHook x86 |
| 签名 | 暂不签名 | 接受 SmartScreen 手动放行 |

## 技术演进记录

- **阶段 0-3（Qt Widgets + CEF 双进程）**：最初方案为 Qt x64 外壳 + CEF 87 x86 渲染器双进程架构，
  已实现无边框外壳、账号管理、托盘等。因 Qt UI 效果与动画性能受限，**该架构已废弃**（git 历史可回溯）。
- **QML 评估（已放弃）**：Qt Quick 在 ARM64 VM x86 模拟下 Windows 窗口合成不稳定（D3D 后端崩溃）。
- **CEF+Vue 单宿主（已废弃）**：CEF 87 单进程同时承载 Vue UI 与 Flash 游戏。UI 受限于 Chromium 87
  老内核（无现代 CSS 特性），且 CEF runtime 体积大。**代码已删除，git 历史可回溯**。
- **最终方案（现行）**：WinUI 3 启动器 + 分离 CEF 87 游戏宿主，Named Pipe 通信。

## 调研数据（2026-08，12 份玩家问卷，指导 UI/功能设计）

- 设备：台式机/笔记本（Windows）为主；入口多为 360 游戏大厅、官方微端。
- 性能：多数"不关心体积/内存，只要流畅"；少数在意 100-500MB 安装包。
- 功能高频诉求：多账号保存+一键切换、QQ 扫码登录、记住密码、加速/变速、自动脚本、防掉线/自动重连。
- 界面风格：主流"简洁现代（类似 Steam/WeGame）"，关键词"快/丝滑/流畅/轻便"。
- 形态：独立窗口（完整 UI）为主；部分人接受悬浮球/托盘。
- 脚本态度：多数"非常需要"，少数怕封号（需做可选+风险提示）。
- 安全：账号本地加密、不读无关信息、无广告捆绑、官方可验证。

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
- 调研问卷分析（12 份，指导功能与风格）
- 清理旧架构：Vue UI（app/ui/）、旧构建产物（build/）、macOS 残留（._*）
- 保留资产：favicon 图标（assets/）、账号业务逻辑参考（docs/reference_useAccounts.ts.txt）、CEF 宿主代码（app/）

**待办（优先级从高到低）**：
1. **WinUI 3 项目搭建**：NarutoLauncher（C#）解决方案骨架，Modern UI 首页
2. **GameHost 改造**：现有 app/ CEF 宿主拆分为独立游戏宿主进程 + Named Pipe IPC 服务
3. **账号系统**：本地加密存储、扫码登录（ptqrlogin 轮询）、记住密码
4. **多开管理**：每账号一个 GameHost 进程，窗口管理、托盘
5. **角色信息同步**：登录后调 `getRoleList` 更新区服/等级/战力
6. **加速/变速、自动脚本、防掉线**：按调研高频诉求实装
7. **Flash 音频崩溃**：解决 libcef CHECK 崩溃（上述候选方案）
8. **打包分发**：GitHub Releases 整包 + version.json

## 开发环境

- 开发机：Windows ARM64 VM（Apple Silicon）
- Visual Studio 2026 Community（MSVC v145，含 x86 交叉工具链；安装"Windows 应用开发"工作负载）
- .NET SDK + Windows App SDK（WinUI 3）
- 注意：GameHost x86 全程走模拟；性能数据不代表真机
