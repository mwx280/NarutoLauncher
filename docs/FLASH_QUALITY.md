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

- 用扫码账号 **0725354631**（userdata：`scan_20260821_013709`，已含登录 cookie）进入游戏。
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