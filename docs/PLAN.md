# 火影忍者OL 启动器 —— 分阶段实施计划

方案①：原生 Qt x64 外壳 + x86 CEF 87 渲染器。本文为实施路线图与各阶段门禁。

## 架构总览

```
shell.exe     Qt 6.9.3 · x64 单构建（x64 原生 / ARM64 模拟）
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

## 已定决策（不可随意变更）

| 决策点 | 结论 | 备注 |
|---|---|---|
| 渲染引擎 | CEF 87.1.13（x86）| 最后一个支持 PPAPI Flash 的内核 |
| Flash 插件 | Flash.cn PPAPI 34.0.0.380（x86）| 官方微端同款 |
| 外壳 | Qt 6.9.3 · x64 · Widgets | 单 x64 构建，ARM64 走 x64 模拟 |
| 渲染器架构 | 固定 x86 | x86 模拟是 ARM64 上最快的模拟路径 |
| 脚本引擎 | JS / QJSEngine | 游戏可编程面 = 页面级 JS + ExternalInterface |
| 变速 | 子进程内建时间 API hook | MinHook x86，倍率经 IPC 下发 |
| 签名 | 暂不签名 | 接受 SmartScreen 手动放行 |
| 更新 | GitHub Releases 整包 | version.json 版本检查 |

## 阶段 0：开发环境准备

1. 加装 **Qt 6.9.3 msvc2022_64** kit（x64 目标）并验证工具链
2. 验证 **MSVC v145 编译 Qt 6.9.3**：三目标 hello-world 试编译
3. 下载 **CEF 87.1.13 windows32** 标准发行版到 `third_party/`
4. 下载 **Flash.cn PPAPI 34 x86**，提取 `pepflashplayer.dll` 到 `third_party/`
5. 建立 CMake 工程骨架（shell x64 + renderer x86 两个独立构建）
- **门禁**：shell(x64)/renderer(x86) 两目标 CMake 配置通过

## 阶段 1：可行性验证（最高风险，先做）

1. 最小 CEF 87 x86 工程：注册 Flash（`--ppapi-flash-path` / `--ppapi-flash-version`），加载 `game.huoying.qq.com/main.html`
2. 验证：**SWF 能跑**、`wmode=direct` GPU 加速正常、无 UA 检测/反调试拦截
3. 验证 **x64 Qt 窗口嵌 x86 CEF 窗口**（跨架构 `createWindowContainer`）：焦点、输入、DPI、尺寸同步
4. 验证 **Named Pipe 跨架构 IPC**（x64↔x86）
5. 验证 ARM64 VM 上 x86 模拟运行稳定性
- **门禁**：以上全部 PASS 才进阶段 2；FAIL 则重估方案

### 阶段 1 调研结论（2026-08-19）

**已达成**：
- Flash 34 PPAPI 插件在 CEF 87 中成功加载（修复了中文路径编码导致的 error 126）
- 登录 / 选区 / 进游戏全流程跑通，游戏画面正常渲染
- cookie 持久化生效：改用全局 `CefSettings.cache_path` + `persist_session_cookies` 后免登录跨启动生效（`CefRequestContextSettings` 单独设置时 session cookie 不落盘）

**关键阻断：Flash 音频触发 libcef CHECK 崩溃**：
- 现象：进游戏/战斗后加载大量音效 SWF，随即 `SyncReader::Read timed out`（audio glitch 累积到 100）→ Flash（ppapi）进程崩溃 → 黑屏
- Windows 事件日志：`libcef.dll` 触发 `0x80000003`（STATUS_BREAKPOINT / CHECK）崩溃，偏移 0x02a74c2a
- **ARM64 VM 与 x86 真机均复现**，排除模拟环境因素
- `mute-audio` 无效：Flash PPAPI 音频输出不走 Chromium 网页音频路径，不受该开关控制
- 官方微端（QQ游戏盒子）用独立 Flash 播放器，音频不经 Chromium 音频栈，故无此崩溃

**后续候选方案（未实施）**：
1. 禁用系统音频设备后重试，确认是否能完全避开该 CHECK（需在真机验证）
2. 换用独立 Flash 播放器做渲染器（架构变更大，规避 CEF 音频缺陷）
3. 深挖 libcef.dll 崩溃点 / CEF 87 音频配置（需 PDB 或源码级分析）

## 阶段 2：渲染器核心（C++/CEF 87）

1. CEF 宿主：`CefExecuteProcess` 双模式 + 多线程消息循环
2. Flash PPAPI 注册 + 生效校验
3. cookie store 持久化（登录态跨启动）
4. 登录流：导航官方选区页 → ptlogin → `skey`/`p_skey` cookie 监听
5. 选区：`loginNew(serverID)` → 解析 `CommLoginApp.cgi` 返回 gameurl → 加载 `main.html`（含 `query_svr_info.fcgi` 拉 ip/host/version）
6. Named Pipe **协议 v1**：命令 / 状态 / 事件三类消息，带版本与错误码
7. 游戏控制通道：`ExecuteJavaScript` + `CefMessageRouter` 桥
- **门禁**：渲染器可独立完成 登录 → 选区 → 进游戏全流程，IPC 协议可用

## 阶段 3：外壳核心（Qt Widgets x64）

1. 原生 UI：登录按钮、服务器列表（`hyol.js` API）、公告、设置
2. `QLocalSocket` IPC 客户端（对齐协议 v1）
3. 游戏视图容器：`createWindowContainer` 嵌入 + 尺寸/DPI/焦点管理 + 加载占位
- **门禁**：外壳能拉起渲染器进程、建立 IPC、嵌入渲染器窗口

## 阶段 4：集成走查

1. 登录态同步：未登录 → 渲染器导航登录页 → 成功回传 → 外壳切列表
2. 选服 → IPC → 渲染器 `loginNew` → 游戏加载 → 进度回传
3. 崩溃恢复：渲染器进程退出 → 外壳检测 → 自动重启 + 重连
4. 完整走查：登录 → 选服 → 进游戏 → 切服 → 登出 → 退出
- **门禁**：全流程稳定通过，无泄漏/卡死

## 阶段 5：脚本引擎（QJSEngine）

1. 集成 QJSEngine（Qt 内置，零依赖）
2. 脚本 API：
   - `game.eval(js)`：向游戏页执行 JS（ExecuteJavaScript）
   - `game.swfCall(name, args)`：经桥直调 SWF 暴露方法（如 `setMiniGameData`）
   - `game.bind(event, cb)` / `game.state()` / `game.click(x,y)` / `game.speed(x)` / `game.status()`
3. CefMessageRouter 注入页面监听器，战斗/任务事件回传
4. 脚本生命周期：加载 / 运行 / 停止 / 保存 / 热更新
5. 示例脚本：自动战斗、自动任务
6. 安全边界：脚本崩溃隔离、资源限制、停用/恢复
- **门禁**：示例脚本端到端跑通

## 阶段 6：变速

1. 子进程内建 hook（MinHook x86）：缩放 `QueryPerformanceCounter / GetTickCount / GetTickCount64 / timeGetTime / NtQueryPerformanceCounter`
2. 变速倍率经 IPC 下发（0.5x–10x），实时生效
3. JS 层 timer 补丁（辅助，针对 JS 驱动逻辑）
- **门禁**：各倍率下游戏逻辑速度与真实耗时匹配、退出恢复

## 阶段 7：打包与分发

1. 打包结构：`shell_x64.exe + renderer\（renderer.exe + libcef.dll + cef.pak + icudtl.dat + pepflashplayer.dll）+ version.json`
2. GitHub Releases 整包发布 + 版本检查 + 下载更新
3. 测试矩阵：Windows x64 真机 + ARM64 真机/VM

## 游戏技术事实（已验证，开发依据）

- 游戏入口：`game.huoying.qq.com/main.html` → swfobject 加载 `res.huoying.com/<version>/entry.swf`，Flash 渲染
- 登录：ptlogin（QQ 扫码/账密），cookie 域 `.qq.com`；`web.checkLogin()` 校验 `skey`/`p_skey`
- 选区：官方选区页 `huoying.qq.com/server/website/`，`loginNew(serverId)` → `CommLoginApp.cgi` 返回 gameurl
- 服务器列表：`gameact.qq.com/comm-htdocs/js/game_area/standard/hyol.js`
- 服务器信息：`game.huoying.qq.com/fcgi-bin/query_svr_info.fcgi?svr_id=<zone_id>` → `[ip, port, host, version]`
- 游戏 SWF 暴露 AS3 方法给页面 JS（ExternalInterface，`allowScriptAccess="always"`）：`setMiniGameData / callNarutoSwf / closeCharge / closeBangBang / miniGame* / confirmAct` 等

## 开发环境

- 开发机：Windows ARM64（Apple Silicon 上的 VM）
- Qt 6.9.3（msvc2022_64 x64 kit 已装；msvc2022_arm64 亦已装）
- Visual Studio 2026 Community（MSVC v145，交叉工具链完整）
- 注意：本机为 ARM64 VM，渲染器 x86 全程走模拟；阶段 1 功能可验、性能数据不代表真机
