# 游戏内部结构分析（火影忍者OL · 725账号 · 区服8856）

> 日期：2026-08-21
> 方法：CEF DevTools 远程调试（GameHost `--debug-port=9222`）+ 静态分析（SWF/JS/XML 下载）

## 一、总体结论

火影忍者OL 是**逻辑全封闭在 Flash 内部**的传统页游，三层防护：

| 层 | 技术 | 可访问性 |
|---|---|---|
| JS 层 | 页面脚本 | 仅登录态 cookie、Flash→JS 单向回调（启动期） |
| SWF 层 | ZWS + LZMA 自定义压缩（加密） | 静态分析受阻 |
| Socket 层 | 加密 TCP（10741） | 不可读 |

**JS 注入无法读取游戏数据 / 控制游戏逻辑。**

## 二、通信架构

### 2.1 Flash → JS（唯一通道，flash_external_interface.js）

Flash 启动期调用的 JS 全局函数（均已在页面定义，可 hook 观察）：

```
getCookie(str)   // Flash 要 cookie（uin/skey 等）
getUin()         // 要 QQ 号
lazyLoader(u0..u4)  // 预加载 SWF/图片/XML（战斗资源预加载——帧率优化关键）
callNarutoSwf(status)  // 通知 JS 加载状态
clientReport(params)   // 上报（含 flashVersion）
debugInstall(isDebugger)  // 空函数，Flash 通知调试器状态
getSwfInstance(movieName) // 取 Flash embed
```

**验证结论**：运行期 Flash 基本不调用这些函数（仅在启动/资源加载时机）。

### 2.2 JS → Flash（封闭）

- `entry` embed 元素**无任何 JS 可调用方法**（getOwnPropertyNames 确认）
- `web.callNarutoSwf(status)` → `entry.callNarutoSwf(status)` 仅触发 Flash 内部通知，无数据回传

### 2.3 游戏数据通道（Socket）

- 服务器：`183.194.190.49:10741`（entry.swf 参数 `port=10741` 印证）
- 协议：Flash 二进制（AMF/自定义），加密，CDP/JS 均不可见
- 角色/任务/邮箱/好友/充值数据全走此通道

## 三、发现的特殊入口（已排查）

| 名称 | 位置 | 说明 | 可用性 |
|---|---|---|---|
| `jstest` | entry.swf 内部 | Flash ExternalInterface 自检：`console.log('jstest.SUCCESS:'+a0+a1)` | JS 层无此函数，不可用 |
| `callLoginCgi` | entry.swf 内部 | 登录 CGI 调用 | Flash 内部，不可用 |
| `loginCfg` / `LoginConfig.xml` | entry.swf + 加密 XML | 登录配置（已加密） | 不可读 |
| QoS 测速 | `http://ied-tqosweb.qq.com:8001` | 腾讯网络测速 | 外部服务 |
| `hijacking.huoying.qq.com/Naruto.zip` | entry.swf | 劫持检测资源 | 防御机制 |
| `serverProto.inc`/`serverProto.system` | entry.swf | Socket 协议定义模块 | Flash 内部 |

**未发现**：可用的邮箱/GM/调试/管理 JS 接口。

## 四、登录态（脚本基础能力）

页面 cookie 可读取（`getUin()` 返回 725354631）：

```
uin=725354631  sServerID=8856  openid=027D5877851D4BA8C8D7FE523BC0E3EC
sServerName=(公测856区 光刃那都)  skey/p_skey/access_token 等
```

启动器已有 cookie 注入（`--cookie=`）能力 → **免登录/多开可用**。

## 五、对脚本开发的指导

### 可行方向
1. **免登录/多开**：cookie 注入（已实现）
2. **战斗资源预加载**：hook `lazyLoader` 提前加载战斗 SWF/音频，减少进战斗卡顿 → 帧率优化直接价值
3. **Win32 模拟输入**：SendInput 到 GameHost 窗口（Flash 捕获键盘鼠标）→ 自动点击/自动任务
4. **屏幕截取 + 图像识别**：定位游戏内 UI 元素 → 辅助自动化（Flash 画面不受 CDP 截屏，需 PrintWindow）

### 不可行方向
- JS 注入读写角色属性/金币/任务数据（Flash 内部 + Socket 加密）

## 六、SWF 静态分析记录

- `entry.swf` = 加载器（245KB，可解压 CWS→FWS，含大量接口线索）
- 核心插件（naruto.core/naruto.include/PlayerPlugin 等）为 **ZWS + LZMA1 自定义头**，标准 zlib/lzma 解压失败 → 疑似二次加密/混淆
- 配置 XML（LoginConfig.xml）加密为二进制

## 七、调试手段（已加入代码）

- GameHost 支持 `--debug-port=<port>` 启用 CEF DevTools 远程调试
- CDP 可：执行 JS、hook Flash 回调、监听 HTTP 网络
- 启动示例：`huoyin_launcher.exe --debug-port=9222 --flash-gpu=0 ...`

## 八、缓存解密（重大发现）

**游戏运行时会把加密的 SWF/配置解密后加载，Chromium 将解密结果缓存到本地，
其中大量是标准 LZMA/Zlib 压缩的明文，可直接还原。**

- 位置：`userdata/<账号>/Cache/f_*`
- 类型分布（725 账号实测 526 个缓存条目）：
  - `SWF-CWS`（标准 zlib 压缩，**可解压**）：约 60 个
  - `ZLIB`/`LZMA`（**明文配置数据**）：约 25 个
  - `SWF-ZWS`（LZMA 自定义头，**仍加密**）：核心逻辑 SWF
  - 图片/其他
- 已还原的明文配置（示例）：
  - `config/user/NinjaInfoCFG.cfg`、`NinjaLevelInfoCFG.cfg`（忍者/等级）
  - `config/skill/SkillCFG.cfg`（技能，含 `tupoSkillId` 突破、`awakenSkillNum` 觉醒）
  - `config/battle/*.xml`（NinjaInfos、SkillInfos、BuffRefInfo、Battlestance）
  - `config/dungeon/DungeonInfoCFG.cfg`（副本）、`EquipmentItemCFG.cfg`（装备）
  - `config/task/taskConditionTypeConfig.xml`（任务）、NPC 表、BUFF 效果表
  - 忍者字段：`baseNinjaAttack/growthNinjaAttack`、`baseNinjaDefense`、
    `baseNinjaStrike`、`waterResist`、`maxLeaderShip`、`maxNinjaOnFormation` 等
- **工具**：`tools/analyze_cache.py <userdata_dir>` 一键还原全部可解压缓存
  （输出到 `<userdata_dir>/Cache/decoded/`）

**结论**：SWF 本体加密无法静态分析，但**运行时解密的数据可在本地缓存还原**，
游戏全部静态配置（忍者/技能/副本/装备/任务/战斗数值）均可获取。玩家动态数据
（战力/等级/背包）仍走加密 Socket，不在缓存中。

## 九、赛尔号对比（独立测试 seer_test）

在独立宿主（`C:\Users\xiaowu\Desktop\seer_test`，加载 `https://seer.61.com`）分析：

| 项目 | 火影忍者OL | 赛尔号 |
|---|---|---|
| SWF 压缩 | ZWS+LZMA 自定义（加密） | CWS+Zlib（明文） |
| 类名 | 无法解析 | 明文（`com.robot.app.MainEntry` 等） |
| 核心库 | 全部加密 | 仅 `TaomeeLibraryDLL.swf` 加密 |
| 登录协议 | 不可静态分析 | `Login.swf` 明文，可反编译 |
| 游戏 Socket | 183.194.190.49:10741 | 111.229.85.11:1218 |
| 本地缓存 | **运行时解密后明文可还原** | 待验证 |

赛尔号外围（加载/登录/资源）明文可分析，核心库单个加密；火影全加密但运行时
缓存可还原配置。两者玩家动态数据均走 Socket。

## 十、Socket 协议抓包分析（重大进展）

用 Windows 内置 `pktmon`（管理员权限）抓取 10741 端口流量，成功解析协议。

### 抓包方法

```
pktmon filter add naruto -p 10741
pktmon start --capture --pkt-size 65535 --file-name naruto.etl
# ...游戏操作 90 秒...
pktmon stop
pktmon etl2txt naruto.etl -o hex.txt --hex   # 注意：输出是 UTF-16LE
```

### 协议结构（请求，52 字节）

```
[0:2]  09 01              协议标识（固定）
[2:4]  XX XX              数据长度
[4:6]  00 03 / 00 10      框架类型（03=基础，10=功能模块）
[6:8]  XX XX              功能号 sub-command
[8:12] 00 00 00           保留
[12]   XX                 命令序号（会话内递增）
[13:24]...                会话/请求标记
[24:28] 2b 3c 08 87 00 00 22 98  服务器会话标识（恒定）
[28:]  数据载荷
```

### 关键发现

1. **协议是"头部明文 + 载荷混合"**：
   - 小数据包（玩家信息等）**明文**，含 UTF-8 角色名（如"此生无悔爱熊"、"重楼"）
   - 大数据包（1460B 及以上）**加密/高熵**，无 zlib 结构
2. **功能号 50+ 种**：0x0004（玩家列表/排行）、0x0008、0x0100、0x0A0B、
   0x0204、0x5203、0x6002 等，对应游戏各系统
3. **请求-响应配对**：同功能号请求有对应响应
4. **明文响应 96 个**（含中文），可识别功能号对应的数据结构

### 工具

- `tools/parse_protocol.py <hex.txt>`：解析抓包，聚合功能号分布

### 结论

- 可通过**构造 Socket 请求**（正确功能号+会话标识）查询游戏数据
- **明文部分**（玩家信息、排行榜等）可直接读取
- **加密部分**（装备/属性等大块数据）需先还原 SWF 内的解密算法

### 会话抓包窗口模式

GameHost 新增 `--windowed`：以标准有边框窗口运行（可拖动/调整/关闭），
用于独立会话（抓包、手动操作）。启动示例：
`huoyin_launcher.exe --windowed --url=... --userdata=... --flash-gpu=0`
