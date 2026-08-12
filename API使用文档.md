# EAappEmulater 远程控制 API 使用文档

本 API 为 EAappEmulater（EA app 模拟器）内置的本机 REST 服务，用于**远程启动游戏、查询游戏详细状态、关闭游戏**。外部程序（自动化脚本、串流工具、自写前端、游戏农场等）可通过 HTTP 调用，复用主程序已登录的 EA 账号 Token 和完整的 OriginDebug / LSX / Battlelog 模拟链路，**免 EA App / Origin 客户端启动本地已安装的 EA 游戏**。

- **监听地址**：`http://127.0.0.1:12000`（仅本机回环，默认端口可配置）
- **数据格式**：JSON（UTF-8）
- **依赖**：无需额外安装；主程序运行即自动启动本服务
- **前置条件**：主程序已登录 EA 账号（`launch` 需要 Token，未登录返回 409）

---

## 目录

1. [配置](#配置)
2. [接口一览](#接口一览)
3. [接口详情](#接口详情)
   - [GET /api/health](#get-apihealth)
   - [GET /api/games](#get-apigames)
   - [POST /api/game/launch](#post-apigamelaunch)
   - [GET /api/game/status](#get-apigamestatus)
   - [POST /api/game/kill](#post-apigamekill)
   - [GET /api/games/args](#get-apigamesargs)
   - [POST /api/games/args](#post-apigamesargs)
4. [启动参数封装（模板 / 透传双模式）](#启动参数封装模板--透传双模式)
5. [BF1 详细游戏状态](#bf1-详细游戏状态)
6. [错误码](#错误码)
7. [调用示例](#调用示例)

---

## 配置

### Config.ini `[Api]` 段

配置文件位于 `文档\EAappEmulater\Config\Config.ini`：

```ini
[Api]
Port=12000            ; API 监听端口（仅本机回环）
Enabled=True          ; 是否启用 API 服务
Bf1ClientApiPort=10087 ; BF1ClientAPI 服务端口（BF1 详细状态读取）
```

### 启动参数

| 参数 | 说明 |
|---|---|
| `--api-port <端口>` | 覆盖 API 监听端口 |
| `--api-disable` | 禁用 API 服务 |
| `--bf1-client-port <端口>` | 覆盖 BF1ClientAPI 端口 |

示例：`EAappEmulater.exe --api-port 10100 --bf1-client-port 10085`

---

## 接口一览

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/health` | 健康检查 |
| GET | `/api/games` | 全部支持游戏列表 |
| POST | `/api/game/launch` | 启动游戏（模板 / 透传双模式） |
| GET | `/api/game/status` | 查询游戏详细状态（含 BF1 场景状态） |
| POST | `/api/game/kill` | 关闭游戏 |
| GET | `/api/games/args` | 读取启动参数模板 |
| POST | `/api/games/args` | 保存启动参数模板 |

统一响应：成功 `HTTP 200` + JSON 对象；失败 `HTTP 400 / 404 / 405 / 409 / 500` + `{"error": "中文说明"}`。

---

## 接口详情

### GET /api/health

健康检查。

**请求**：
```bash
curl http://127.0.0.1:12000/api/health
```

**响应 200**：
```json
{
  "status": "ok",
  "version": "1.9.1.3",
  "port": 12000,
  "service": "EAappEmulater Remote API"
}
```

---

### GET /api/games

返回全部受支持的游戏及其默认配置。

**请求**：
```bash
curl http://127.0.0.1:12000/api/games
```

**响应 200**（数组，共 36 款游戏）：
```json
[
  {
    "GameType": "BF3",
    "Name": "战地风云3",
    "Name2": "Battlefield 3",
    "AppName": "bf3.exe",
    "ContentId": "71067",
    "Dir": "C:\\Program Files\\Battlefield 3",
    "Dir2": "",
    "IsInstalled": true,
    "IsEAAC": false,
    "Locales": ["zh_TW", "fr_FR", "ko_KR", "it_IT", "cs_CZ", "ja_JP", "de_DE", "es_ES", "pl_PL", "en_US"]
  }
]
```

| 字段 | 说明 |
|---|---|
| `GameType` | 游戏枚举名，即 `launch` 的 `gameType` 取值 |
| `AppName` | 默认 exe 文件名 |
| `ContentId` | EA 内容 ID |
| `Dir` | 注册表安装目录（未安装为空） |
| `Dir2` | 自定义启动路径 |
| `IsInstalled` | 注册表路径或自定义路径存在 |
| `IsEAAC` | 是否 EA 反作弊启动器启动 |

---

### POST /api/game/launch

启动游戏。**启动参数支持"模板 / 透传"双模式**（见[第 4 节](#启动参数封装模板--透传双模式)）。

**请求体**：
```json
{
  "gameType": "BF1",                    // 必填，游戏枚举名
  "exePath": "D:\\Games\\BF1\\bf1.exe", // 可选，exe 完整路径；缺省用注册表/自定义路径
  "processName": "bf1_x64,bf1",         // 可选，状态检测进程名（逗号分隔多进程）；缺省=exe 文件名去扩展名
  "workingDirectory": "",               // 可选，工作目录；缺省=exePath 目录
  "contentId": "1026023",               // 可选，缺省用游戏数据库
  "locale": "zh_TW",                    // 可选，游戏语言；缺省从注册表读取
  "argsSource": "template",             // 可选，参数来源："template" | "direct"；缺省 "direct"
  "templateVars": { "gameId": "10970733650691" },  // argsSource=template 时：模板变量替换
  "arguments": "-Window.Minimized true"            // argsSource=direct 时：完整参数串原样透传
}
```

**最小请求（缺省路径 / 缺省参数）**：
```json
{ "gameType": "BF4" }
```

**响应 200**：
```json
{
  "ExePath": "D:\\DepotDownloader-windows-x64\\depots\\1238841\\17371042\\EAAntiCheat.GameServiceLauncher.exe",
  "WorkingDirectory": null,
  "Arguments": "requestState State_ConnectToGameId -gameId 10970733650691 -gameMode MP -role soldier -asSpectator false -Window.Minimized true",
  "ContentId": "1026023",
  "ProcessNames": "EAAntiCheat.GameServiceLauncher.exe",
  "Success": true,
  "LaunchTime": "2026-08-11 15:02:02"
}
```

**行为说明**：
- 显式传入 `exePath` 时，会自动写入该游戏的**自定义路径槽**（`Dir2`/`Args2`），与主程序设置界面一致，之后 UI 也能看到
- `processName` 会记录，供后续 `status` / `kill` 复用（`kill` 缺省进程名时用它）
- API 触发的启动**不弹 UI 通知**（静默模式）
- 游戏实际由后台 `OriginDebug` 进程通过 explorer 拉起，并注入完整 EA 环境变量（Token / ContentId / 语言 / RTP 握手码等）

---

### GET /api/game/status

查询游戏详细状态。

**带 gameType 查询**：
```bash
curl "http://127.0.0.1:12000/api/game/status?gameType=BF1"
curl "http://127.0.0.1:12000/api/game/status?gameType=BF1&processName=EAAntiCheat.GameServiceLauncher.exe"
```

**响应 200**（BF1 示例，含详细状态）：
```json
{
  "GameType": "BF1",
  "IsRunning": true,
  "Processes": [
    {
      "Name": "EAAntiCheat.GameServiceLauncher",
      "Pid": 34480,
      "WorkingSetMB": 1588.4,
      "CpuSeconds": 11.9,
      "StartTime": "2026-08-11 15:05:05"
    }
  ],
  "GameState": null,
  "LastLaunch": {
    "ExePath": "D:\\DepotDownloader-windows-x64\\depots\\1238841\\17371042\\EAAntiCheat.GameServiceLauncher.exe",
    "WorkingDirectory": null,
    "Arguments": "requestState State_ConnectToGameId -gameId 10970733650691 -gameMode MP -role soldier -asSpectator false",
    "ContentId": "1026023",
    "ProcessNames": "EAAntiCheat.GameServiceLauncher.exe",
    "Success": true,
    "LaunchTime": "2026-08-11 15:02:02"
  },
  "Bf1Status": {
    "state": 12,
    "stateName": "Ingame",
    "status": 3,
    "statusName": "Ingame",
    "isMenu": false,
    "isMultiplayer": true,
    "isCoop": false,
    "isEpilogue": false,
    "scene": "playing",
    "sceneText": "对局中"
  }
}
```

**不带参数查询**（返回全部有启动记录的游戏状态数组）：
```bash
curl http://127.0.0.1:12000/api/game/status
```
```json
[ { "GameType": "BF1", "IsRunning": true, ... }, ... ]
```

| 字段 | 说明 |
|---|---|
| `IsRunning` | 是否有匹配进程在运行 |
| `Processes` | 进程详情数组（名称 / PID / 内存 MB / CPU 秒 / 启动时间） |
| `GameState` | **仅 BF3/BF4/BFH**：雪球管道上报的游戏内状态原文（其他游戏为 `null`） |
| `LastLaunch` | 最近一次通过 API 启动的记录（含进程名，供 `kill` 复用） |
| `Bf1Status` | **仅 BF1**：BF1ClientAPI 内存读取的详细状态，见[第 5 节](#bf1-详细游戏状态) |

> 注意：`IsRunning` 只检测**实际被 API 启动过的游戏**的进程名，不会误报。因为 `EAAntiCheat.GameServiceLauncher.exe` 被 11 个 EA 反作弊游戏共用，不能按游戏数据库默认进程名扫描。

---

### POST /api/game/kill

关闭游戏（结束进程树）。

**请求体**（二选一，`processName` 优先）：
```json
{ "gameType": "BF1" }
```
或
```json
{ "processName": "bf1_x64,bf1,EAAntiCheat.GameServiceLauncher" }
```

`gameType` 方式会自动使用该游戏**最近启动记录里的进程名**；无启动记录时用数据库默认进程名。

**响应 200**：
```json
{ "success": true, "processName": "EAAntiCheat.GameServiceLauncher.exe" }
```

---

### GET /api/games/args

读取指定游戏的启动参数模板。

**请求**：
```bash
curl "http://127.0.0.1:12000/api/games/args?gameType=BF1"
```

**响应 200**：
```json
{
  "gameType": "BF1",
  "content": "requestState State_ConnectToGameId -gameId 10970733650691 -gameMode MP -role soldier -asSpectator false -Window.Minimized true",
  "exists": true
}
```

模板不存在时 `exists: false`、`content: ""`。

---

### POST /api/games/args

保存指定游戏的启动参数模板。

**请求体**：
```json
{
  "gameType": "BF1",
  "content": "requestState State_ConnectToGameId -gameId 10970733650691 -gameMode MP -role soldier -asSpectator false -joinWithParty false -Window.Minimized true -Sound.Enable false -Render.NullRendererEnable true"
}
```

**响应 200**：
```json
{ "success": true, "gameType": "BF1" }
```

---

## 启动参数封装（模板 / 透传双模式）

设计参考 `NoRenderBF1FarmBot` 的 `RunArgsManager` 机制，用于把**完整启动参数**（如 BF1 直连服务器的 `requestState State_ConnectToGameId -gameId xxx ...` + 无渲染优化参数）集中管理、按需替换目标字段。

### 模板文件存储

每个游戏一个模板文件：`文档\EAappEmulater\Config\RunArgs\{GameType}.txt`，可用 `/api/games/args` 读写。

### 双语法变量替换引擎

`templateVars` 中的每个键值对会同时尝试两种替换语法：

1. **占位符语法**：`{{gameId}}` → 值
2. **farm 风格正则**：`-gameId 123` → `-gameId <新值>`（匹配 `-gameId` 后跟数字/引号包裹值）

示例 —— 模板：
```
requestState State_ConnectToGameId -gameId 123456 -gameMode MP -role soldier -asSpectator false
```
`templateVars: { "gameId": "10970733650691" }` 后：
```
requestState State_ConnectToGameId -gameId 10970733650691 -gameMode MP -role soldier -asSpectator false
```

- 键大小写不敏感；模板中不存在的变量名保持原样
- 模板模式下未找到模板文件 → 返回 400

### 两种模式选择

| `argsSource` | 参数来源 |
|---|---|
| `direct`（缺省） | 请求体 `arguments` 字段原样透传 |
| `template` | 读取该游戏模板文件 → 应用 `templateVars` 替换 → 透传 |

---

## BF1 详细游戏状态

BF1 通过与 **BF1ClientAPI**（读取 bf1.exe 进程内存的配套工具）集成，提供比"进程存活"更细的状态——能区分**菜单 / 进服中 / 对局中 / 回合结束**。

### 数据链路

```
bf1.exe 进程内存 → BF1ClientAPI (127.0.0.1:10087) → EAappEmulater API → 调用方
```

- BF1ClientAPI 需独立运行（以管理员身份，用于读写游戏内存）
- 默认端口 `10087`（GHS 版默认），可通过 Config.ini `[Api] Bf1ClientApiPort` 或 `--bf1-client-port` 覆盖
- EAappEmulater 每次查询 BF1 状态时请求 `http://127.0.0.1:{端口}/Game/GetGameStatus`（3 秒超时）
- BF1ClientAPI **未运行 / 游戏内存未初始化 / 请求超时** → `Bf1Status` 返回 `null`（优雅降级，不影响进程级状态）

### `Bf1Status` 字段

| 字段 | 来源 | 说明 |
|---|---|---|
| `state` / `stateName` | 客户端状态枚举 | 如 `12` = `Ingame`、`7` = `Start Loading Level`、`17` = `None` |
| `status` / `statusName` | 游戏状态 | `3` = `Ingame`、`1` = `Not Ingame` |
| `isMenu` | 内存标志 | 是否在菜单 |
| `isMultiplayer` / `isCoop` | 内存标志 | 多人 / 合作 |
| `isEpilogue` | 内存标志 | 是否结算页/回合结束 |
| `scene` | **派生** | `menu` / `loading` / `playing` / `eor` |
| `sceneText` | 派生 | 中文描述 |

### 场景映射规则

| `scene` | `sceneText` | 判定 |
|---|---|---|
| `menu` | 菜单/空闲 | `isMenu=true`，或 state=17（未进游戏） |
| `eor` | 回合结束 | `isEpilogue=true` |
| `playing` | 对局中 | `status=3` 或 `state=12`（Ingame） |
| `loading` | 进服中/加载中 | 其余（如 `Start Loading Level`、`Waiting For Ghosts` 等进服流程状态） |

### 完整 BF1 无渲染启动参数模板（挂机/农场用）

可一次性保存为模板：
```json
POST /api/games/args
{
  "gameType": "BF1",
  "content": "requestState State_ConnectToGameId -gameId 10970733650691 -gameMode MP -role soldier -asSpectator false -joinWithParty false -Window.Minimized true -Sound.Enable false -Render.NullRendererEnable true -Client.EmittersEnabled false -Core.HardwareProfile Hardware_Low -Client.TerrainEnabled false -Core.HardwareCpuBias -1 -Core.HardwareGpuBias -1 -Texture.RenderTexturesEnabled false -RenderDevice.CreateMinimalWindow true -RenderDevice.NullDriverEnable true -RenderDevice.MinDriverRequired false"
}
```
之后直连某服务器只需替换 `gameId`：
```json
POST /api/game/launch
{
  "gameType": "BF1",
  "exePath": "D:\\DepotDownloader-windows-x64\\depots\\1238841\\17371042\\EAAntiCheat.GameServiceLauncher.exe",
  "processName": "EAAntiCheat.GameServiceLauncher.exe,bf1_x64",
  "argsSource": "template",
  "templateVars": { "gameId": "9340024730318" }
}
```

---

## 错误码

| HTTP | 场景 | 响应示例 |
|---|---|---|
| 400 | 请求体解析失败 / 缺必填字段 / 非法 gameType / exe 不存在 / 模板不存在 / 非法 argsSource | `{"error":"无效的游戏类型: XXX"}` |
| 404 | 接口路径不存在 | `{"error":"未找到接口: /api/nonexistent"}` |
| 405 | 方法不支持（如对 launch 用 GET） | `{"error":"不支持的请求方法"}` |
| 409 | EA 账号未登录 | `{"error":"EA 账号未登录，无法启动游戏"}` |
| 500 | 服务内部错误 | `{"error":"服务器内部错误: ..."}` |

---

## 调用示例

### curl

```bash
# 健康检查
curl http://127.0.0.1:12000/api/health

# 游戏列表
curl http://127.0.0.1:12000/api/games

# 启动 BF4（默认路径/参数）
curl -X POST http://127.0.0.1:12000/api/game/launch \
  -H "Content-Type: application/json" \
  -d '{"gameType":"BF4"}'

# 启动 BF1 直连服务器（模板模式，替换 gameId）
curl -X POST http://127.0.0.1:12000/api/game/launch \
  -H "Content-Type: application/json" \
  -d '{"gameType":"BF1","exePath":"D:\\DepotDownloader-windows-x64\\depots\\1238841\\17371042\\EAAntiCheat.GameServiceLauncher.exe","processName":"EAAntiCheat.GameServiceLauncher.exe,bf1_x64","argsSource":"template","templateVars":{"gameId":"9340024730318"}}'

# 查询 BF1 详细状态
curl "http://127.0.0.1:12000/api/game/status?gameType=BF1"

# 关闭 BF1
curl -X POST http://127.0.0.1:12000/api/game/kill \
  -H "Content-Type: application/json" \
  -d '{"gameType":"BF1"}'
```

### Python

```python
import json, urllib.request

BASE = "http://127.0.0.1:12000"

def api(method, path, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    if body is not None:
        req.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(req, timeout=10) as resp:
        return resp.status, json.loads(resp.read().decode())

# 启动 BF1 直连服务器
_, launch = api("POST", "/api/game/launch", {
    "gameType": "BF1",
    "exePath": r"D:\DepotDownloader-windows-x64\depots\1238841\17371042\EAAntiCheat.GameServiceLauncher.exe",
    "processName": "EAAntiCheat.GameServiceLauncher.exe,bf1_x64",
    "argsSource": "template",
    "templateVars": {"gameId": "9340024730318"},
})
print("已发送启动信号:", launch["Success"])

# 轮询游戏状态
import time
for _ in range(30):
    _, status = api("GET", "/api/game/status?gameType=BF1")
    bf1 = status.get("Bf1Status") or {}
    print(f"进程运行: {status['IsRunning']}  场景: {bf1.get('sceneText')}  状态: {bf1.get('statusName')}")
    if bf1.get("scene") == "playing":
        print("已进入对局")
        break
    time.sleep(5)
```

---

## 常见问题

**Q: 访问不了 API？**
检查主程序是否运行、端口是否被占用（`netstat -ano | findstr 12000`）、Config.ini 的 `Enabled` 是否为 True。

**Q: launch 返回 409？**
EA 账号未登录。先在主程序界面登录，或确认当前账号槽有有效 Cookie/Token。

**Q: BF1 的 Bf1Status 一直是 null？**
BF1ClientAPI 未运行、端口不对（默认 10087），或游戏内存未初始化（需游戏启动后）。请先运行 BF1ClientAPI（管理员权限）。

**Q: launch 返回 400 "未找到游戏可执行文件"？**
`exePath` 未传且注册表/自定义路径都没有游戏。请显式传 `exePath`，或先在主程序"游戏选项"里配置自定义路径。
