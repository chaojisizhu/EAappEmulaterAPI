using EAappEmulater.Enums;
using Newtonsoft.Json;

namespace EAappEmulater.Models;

/// <summary>
/// 远程启动游戏请求
/// </summary>
public class ApiLaunchRequest
{
    /// <summary>
    /// 游戏类型枚举名（必填，如 BF1 / BF4）
    /// </summary>
    public string GameType { get; set; }

    /// <summary>
    /// exe 完整路径（可选，缺省用注册表/自定义路径）
    /// </summary>
    public string ExePath { get; set; }

    /// <summary>
    /// 状态检测进程名，逗号分隔多进程（可选，缺省=exe 文件名去扩展名）
    /// </summary>
    public string ProcessName { get; set; }

    /// <summary>
    /// 工作目录（可选，缺省=exePath 目录）
    /// </summary>
    public string WorkingDirectory { get; set; }

    /// <summary>
    /// ContentId（可选，缺省用游戏数据库）
    /// </summary>
    public string ContentId { get; set; }

    /// <summary>
    /// 游戏语言（可选，缺省从注册表读取）
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// 启动参数来源："template"=从模板取参数 | "direct"=arguments 透传；缺省 "direct"
    /// </summary>
    public string ArgsSource { get; set; }

    /// <summary>
    /// 模板变量替换（argsSource=template 时生效，如 { "gameId": "10970733650691" }）
    /// </summary>
    public Dictionary<string, string> TemplateVars { get; set; }

    /// <summary>
    /// 完整启动参数串（argsSource=direct 时原样透传）
    /// </summary>
    public string Arguments { get; set; }
}

/// <summary>
/// 游戏列表条目
/// </summary>
public class ApiGameInfo
{
    public string GameType { get; set; }
    public string Name { get; set; }
    public string Name2 { get; set; }
    public string AppName { get; set; }
    public string ContentId { get; set; }
    public string Dir { get; set; }
    public string Dir2 { get; set; }
    public bool IsInstalled { get; set; }
    public bool IsEAAC { get; set; }
    public List<string> Locales { get; set; }
}

/// <summary>
/// 单个进程信息
/// </summary>
public class ApiProcessInfo
{
    public string Name { get; set; }
    public int Pid { get; set; }
    public double WorkingSetMB { get; set; }
    public double CpuSeconds { get; set; }
    public string StartTime { get; set; }
}

/// <summary>
/// 最近一次启动记录
/// </summary>
public class ApiLaunchInfo
{
    public string ExePath { get; set; }
    public string WorkingDirectory { get; set; }
    public string Arguments { get; set; }
    public string ContentId { get; set; }

    /// <summary>
    /// 状态检测进程名（逗号分隔，用于后续状态查询和关闭）
    /// </summary>
    public string ProcessNames { get; set; }

    public bool Success { get; set; }
    public string LaunchTime { get; set; }
}

/// <summary>
/// 游戏详细状态
/// </summary>
public class ApiGameStatus
{
    public string GameType { get; set; }
    public bool IsRunning { get; set; }
    public List<ApiProcessInfo> Processes { get; set; } = new();
    public string GameState { get; set; }
    public ApiLaunchInfo LastLaunch { get; set; }

    /// <summary>
    /// BF1 详细游戏状态（仅 BF1 有值，来自 BF1ClientAPI 内存读取）
    /// </summary>
    public ApiBf1Status Bf1Status { get; set; }
}

/// <summary>
/// BF1 详细游戏状态（来自 BF1ClientAPI /Game/GetGameStatus）
/// </summary>
public class ApiBf1Status
{
    [JsonProperty("state")]
    public int State { get; set; }

    [JsonProperty("stateName")]
    public string StateName { get; set; }

    [JsonProperty("status")]
    public int Status { get; set; }

    [JsonProperty("statusName")]
    public string StatusName { get; set; }

    [JsonProperty("isMenu")]
    public bool IsMenu { get; set; }

    [JsonProperty("isMultiplayer")]
    public bool IsMultiplayer { get; set; }

    [JsonProperty("isCoop")]
    public bool IsCoop { get; set; }

    [JsonProperty("isEpilogue")]
    public bool IsEpilogue { get; set; }

    /// <summary>
    /// 派生场景状态：menu=菜单/空闲, loading=进服中/加载中, playing=对局中, eor=回合结束, unknown=未知
    /// </summary>
    [JsonProperty("scene")]
    public string Scene { get; set; }

    /// <summary>
    /// 场景中文描述
    /// </summary>
    [JsonProperty("sceneText")]
    public string SceneText { get; set; }
}

/// <summary>
/// BF1ClientAPI 响应包装
/// </summary>
public class Bf1GameStatusResponse
{
    public int Code { get; set; }
    public string Message { get; set; }
    public ApiBf1Status Data { get; set; }
}

/// <summary>
/// 保存启动参数模板请求
/// </summary>
public class ApiArgsRequest
{
    /// <summary>
    /// 游戏类型枚举名（必填）
    /// </summary>
    public string GameType { get; set; }

    /// <summary>
    /// 模板内容（完整启动参数）
    /// </summary>
    public string Content { get; set; }
}

/// <summary>
/// 关闭游戏请求
/// </summary>
public class ApiKillRequest
{
    /// <summary>
    /// 游戏类型枚举名（与 processName 二选一）
    /// </summary>
    public string GameType { get; set; }

    /// <summary>
    /// 进程名，逗号分隔多进程（优先于 gameType）
    /// </summary>
    public string ProcessName { get; set; }
}
