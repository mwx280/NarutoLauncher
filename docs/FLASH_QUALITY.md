# Flash 画质调节问题专项记录

> 状态：调查中（方案 1 探测阶段）
> 日期：2026-08-21

## 一、问题现象

1. 启动器独立游戏窗口菜单栏的「画质」下拉（低/中/高）调节**不生效**。
2. 游戏内对 Flash 画面点右键 → 选择「品质（Quality）」**也不生效**。
3. 对比：QQ 游戏大厅 / 360 游戏大厅里同一游戏可通过 Flash 右键菜单改画质。

## 二、准确根因分析

### 2.1 启动器画质菜单为什么不生效

- 启动器菜单 → `PostMessage(WM_APP+2)` → `OnWindowCommand(2)` → `SetFlashQuality(level)`
  → `InjectFlashQuality(q)`（`app/src/main.cpp` L358）。
- `InjectFlashQuality` 做的事情是**运行时修改已创建 SWF 的 DOM**：
  - 改 `<param name="quality" value="...">`
  - `setAttribute('quality', q)`
  - `swf.quality = q`
- **关键事实**：Flash `quality` 参数（low/medium/high/best）**只在 SWF 播放器实例创建时读取一次**，
  运行期修改 DOM 属性对已运行的 SWF 渲染质量**无效**。
- 日志证实命令已到达（`命令: 设置 Flash 画质=medium (1)`、`=low (0)`），但画面无变化，符合上述事实。

### 2.2 游戏内 Flash 右键「品质」为什么不生效

- 本启动器使用 **CEF 87 + PPAPI Flash（pepflashplayer.dll）**。
- **Chromium/CEF 从 41 版起，PPAPI Flash 右键菜单的 Quality（品质）项失效**，
  是上游 Chromium 已知问题，非本实现缺陷。
- 参考：CefSharp issue #2810（"Change flash player quality not working"）。

### 2.3 页面 JS 能否直接改 `stage.quality`

- AS3 规范：`Stage.quality` 只允许与 Stage owner（主 SWF）**同安全沙箱**的调用者读写。
- 页面 JS 跨域调用会抛 `SecurityError`，除非主 SWF 主动 `Security.allowDomain()` 授权。
- 因此**无法从页面 JS 直接改运行中 SWF 的渲染质量**（除非游戏开放通道）。

### 2.4 为什么 QQ / 360 游戏大厅可以

区别在 **Flash 宿主播放器类型**，而非游戏是否支持：

| 项 | 本启动器 | QQ/360 游戏大厅 |
|---|---|---|
| Flash 宿主 | CEF 87 + PPAPI Flash | IE 内核 + **Flash ActiveX（Flash.ocx）**，或独立播放器 / 腾讯游戏盒子 |
| 右键「品质」 | Chromium 41+ 起失效（上游 bug） | ActiveX 播放器**完整可用**，可实时改渲染质量 |
| 结论 | 宿主决定，非游戏限制 | 宿主决定 |

腾讯官方火影 OL 微端亦基于「腾讯游戏盒子」（IE 内核 ActiveX Flash），故右键品质正常。

## 三、候选方案

目标：让「画质」真正作用于渲染。核心矛盾是 **quality 只在实例化时生效**。

### 方案 1（首选）：Flash 实例创建前注入 quality —— 重载生效

- **思路**：`quality` 参数只在 SWF 创建时读取。因此在页面 JS 创建 Flash 对象**之前**
  改写传给 swfobject / 生成 `<object>/<embed>` 的参数，让 SWF **以目标画质创建**。
- **做法**：
  1. 在 `OnLoadStart`（页面 JS 启动前）注入脚本。
  2. hook 游戏页的 `swfobject.embedSWF(...)`（游戏用 swfobject 加载 `res.huoying.com/<version>/entry.swf`），
     在 params 里强制写入 `quality`。
  3. 或拦截/改写页面生成的 `<object>/<embed>` 标签的 `quality` 参数后再插入 DOM。
  4. 用户改档后自动触发页面重载，以新 quality 重建 SWF。
- **优点**：符合 Flash 行为模型，原理可靠；无需碰游戏内部函数。
- **缺点**：换画质需要重载页面（重新进入游戏界面）；若游戏 SWF 在内部自行设置 `stage.quality`，
  则实例化参数可能被覆盖（需探测验证）。
- **当前待验证**：游戏入口 main.html 究竟如何创建 Flash、swfobject 版本、quality 参数是否显式传递、
  游戏是否内部覆盖 stage.quality。

### 方案 2：MinHook 挂钩游戏 SWF 内部的画质函数

- **思路**：直接 hook AS3 `stage.quality` setter 或游戏内画质设置函数。
- **优点**：不改页面，可运行时实时切换；不依赖重载。
- **缺点**：需逆向 entry.swf 找函数签名/地址（AS3 虚拟机层，难度高）；改动大、风险高。
- 参考：PLAN.md 待办「加速/变速、自动脚本」（MinHook 变速同技术栈）。

### 方案 3：CDP 注入 JS，模拟游戏内设置操作

- **思路**：用 CDP（`--remote-debugging-port`）注入 JS，若游戏自身有画质/流畅度设置入口
  （或 ExternalInterface 暴露 `setMiniGameData / callNarutoSwf / closeCharge` 等），则调用或模拟点击。
- **优点**：走游戏自身逻辑，最稳。
- **缺点**：依赖游戏是否开放对应接口/UI，未知，需进入游戏实测。

## 四、探索进度（实测记录）

### 4.1 环境准备

- 用一个已含登录 cookie 的扫码账号进入游戏。
- 观察 main.html 的 Flash 创建方式（swfobject 版本、quality 参数、entry.swf 加载）。

### 4.2 待验证问题清单

- [ ] main.html 用哪个 swfobject API 创建 Flash？（`embedSWF` / `createSWF` / 直接写标签）
- [ ] 创建时是否已带 `quality` 参数？默认值多少？
- [ ] 游戏 SWF 是否在内部覆盖 `stage.quality`？
- [ ] 修改实例化参数 + 重载后，画面质量是否变化（方案 1 可行性判定）？
- [ ] 游戏 SWF 是否暴露画质相关的 ExternalInterface 接口（方案 3 可行性）？

## 五、参考资料

- CefSharp issue #2810：Change flash player quality not working（Chromium 41+ PPAPI 右键 Quality 失效）
- Adobe AS3 文档 `flash.display.Stage.quality`：非同沙箱调用抛 `SecurityError`；
  `StageQuality` 值：low / medium / high / best
- PLAN.md「游戏技术事实」：游戏入口 `game.huoying.qq.com/main.html` → swfobject 加载 entry.swf

## 六、流畅度实测与 GPU 合成优化（2026-08-21）

> 背景：用户反馈游戏"只有 30 帧很卡"，对比 360/QQ 游戏大厅（同一台 Parallels VM 上）流畅。
> 结论：**开启 Flash GPU 合成（--flash-gpu=1）明显更流畅**，已改为默认开启。

### 实测数据（Parallels VM，Apple Silicon 模拟 x86）

| 指标 | flash-gpu=0（默认旧） | flash-gpu=1 |
|---|---|---|
| Flash renderer CPU | 93.2%（接近满载） | 99%（全力渲染） |
| GPU 进程 | 无（合成走 CPU） | 有（合成走 GPU） |
| 用户体感 | 卡 | **明显流畅** |
| 画面 rAF 帧率 | 60-105fps | 60-105fps |

### 根因

- Flash 传统 2D 页游画面光栅化始终在 **CPU**（PPAPI 插件限制），
  但**合成（把各图层拼合上屏）**可以走 GPU。
- flash-gpu=0：CPU 既要光栅化又要软件合成 → 双重负担 → 帧率上不去。
- flash-gpu=1：光栅化仍 CPU，但**合成交给 GPU** → CPU 专注渲染 → 流畅。
- 360/QQ 大厅用 Flash ActiveX + 硬件加速光栅化，渲染路径不同，故更流畅。

### 变更

- `SettingsService`：FlashHardwareAcceleration 默认值 **false → true**。
- 设置页文案更新为"GPU 合成提升流畅度（默认开启）"。
- 说明：Flash 内容本身仍 CPU 渲染（架构性限制），GPU 合成只解决合成瓶颈；
  在无真实 GPU 的虚拟机环境改善有限，**真机 x86 + 独立显卡提升更大**。

## 七、Flash 渲染质量 hook（真正控制画质，2026-08-22）

> 背景：此前画质调节（改 DOM quality）对主画面无效——游戏主 OBJECT 由
> `swfobject.embedSWF` 创建，quality 缺失默认 high，且 JS 注入会被游戏覆盖。
> 解决方案：**在 Flash 插件层 hook PPP_Instance::DidCreate 改写 quality**。

### 原理

```
浏览器 → PPP_InitializeModule(PPB_GetInterface)
       → PPP_GetInterface("PPP_Instance;1.1")   ← hook 这里
       → PPP_Instance::DidCreate(argc, argn[], argv[])  ← 改写 argv 的 quality
```

- `app/src/flash_hook.cpp`：MinHook hook `PPP_GetInterface`，拦截其返回的
  `PPP_Instance;1.1` 接口表，包装 `DidCreate` 改写 `quality` 参数。
- 作用于**整个游戏**（主城/UI/战斗），游戏 JS 无法覆盖。
- 仅在 **ppapi 子进程**安装（异步线程等待 pepflashplayer.dll 加载）。

### 关键实现要点

1. `PPP_Instance_1_1` 接口表须**完整声明 5 个成员**（DidCreate/DidDestroy/
   DidChangeView/DidChangeFocus/HandleDocumentLoad），缺失会导致 Flash 调用
   其他函数时读无效内存 → 黑屏。
2. **只能 hook `PPP_Instance;1.x`**，不能误匹配 `PPP_Instance_Private`（结构不同）。
3. `MH_Initialize` 由 `InstallNoConsoleHooks()` 调用，**不可重复调用**
   （非线程安全），Flash hook 只 `MH_CreateHook` + `MH_EnableHook`。
4. quality 值通过**环境变量 `HUOYIN_FLASH_QUALITY`** 传给子进程
   （CEF 会过滤命令行自定义开关，环境变量必然继承）。

### 使用

- 命令行：`--flash-quality=<low/medium/high>`（默认 low，流畅优先）。
- 启动器：游戏窗口顶部「画质」下拉（低/中/高），切换时**重启 GameHost** 生效
  （quality 只在 Flash 实例创建时读取）。
- **低/中/高三档统一由本 Flash hook 控制**（唯一有效路径）。
  旧的 JS 层方案（改 createEntrySwfObject）只作用于 60×60 加载器且会被
  游戏运行期覆盖，从未真正生效。

### 与 DPI 感知的配合（重要）

- GameHost **不再设置 PROCESS_PER_MONITOR_DPI_AWARE**，并强制
  `--force-device-scale-factor=1`。
- 原因：若保留 DPI 感知，CEF 按系统 DPI（VM 常为 200%）设 DPR=2，
  Flash 以 2 倍物理分辨率渲染——既更耗 CPU，又让 quality=low 的降质
  不明显（看起来像中画质）。
- 若 DPI 感知 + 强制 DPR=1 组合：游戏页面视口（1280）与窗口物理尺寸
  （1512）不匹配 → 画面不完整/截断。
- 正确组合：**无 DPI 感知 + force-device-scale-factor=1**（与独立测试宿主
  一致）——Flash 以 1 倍分辨率渲染，low 画质真正生效且布局完整。

### 实测验证

- low：`DidCreate` 里 quality=low，画面明显变糊（抗锯齿关闭）。
- high：quality 保持 high，画面清晰。
- 独立对照（cefsimple_test）：low vs 原始，视觉差异明确。
- 注意：此 VM 上 CPU 均接近满载，画质切换主要影响画面质量，CPU 降幅有限；
  真机上低画质可显著降低光栅化开销。

## 八、分辨率模式（DPR）设置（2026-08-22）

> 背景：用户希望大屏用户可以选择更高分辨率渲染。

### 功能

设置页「画质」分组新增「分辨率模式」下拉：
- **性能优先**（默认）：强制 `force-device-scale-factor=1`，DPR=1，low 画质降质明显
- **画质优先**：不强制 DPR，跟随系统 DPI，画面清晰但 low 画质降质不明显

### 画质优先模式窗口显示不全 —— 已修复（2026-08-22）

- 现象：切换到「画质优先」后，游戏窗口内容显示过大/过小，无法正常游戏（显示不全）。
- **根因**：`OnLoadStart` 无条件注入的 **resizeTarget 铺满 CSS**。
  - 该 CSS 用 `transform: scale(w/1920, h/1080)` 把 Flash 容器等比放大到视口，只在
    DPR=1（性能优先，CSS 视口≈窗口物理尺寸）时正确。
  - 画质优先 DPR=2，CSS 视口与窗口物理尺寸关系改变，`scale(w/1920, h/1080)`
    补偿失效 → 显示过大/过小、不全。
- **尝试过的方案**：
  - CSS transform 乘 DPR（`scale(w*dpr/1920, h*dpr/1080)`）→ 内容过大
  - CSS transform 不乘 DPR（`scale(w/1920, h/1080)`）→ 内容过小
- **修复**：resizeTarget 铺满 CSS **仅性能优先注入**（`app/src/main.cpp` 按
  `g_force_dpr` 判断），画质优先保持游戏原始布局、不缩放，显示正常。
- 状态：**已修复**。性能优先继续铺满；画质优先不再注入该 CSS。
- 附带澄清：此前的"画质优先模式下禁用 Flash hook（方案 A）"已回滚（commit
  4a38695 重新启用 Flash hook），画质优先与性能优先均注入 Flash 质量 hook。