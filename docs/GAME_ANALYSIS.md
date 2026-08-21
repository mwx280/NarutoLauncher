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
