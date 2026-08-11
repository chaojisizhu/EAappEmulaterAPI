using EAappEmulater.Enums;
using EAappEmulater.Helper;
using EAappEmulater.Models;
using EAappEmulater.Utils;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net.Http;

namespace EAappEmulater.Core;

/// <summary>
/// 远程控制 REST API 服务
/// 仅监听本机回环 127.0.0.1:{port}
/// </summary>
public static class GameApiServer
{
    private static HttpListener _httpListener = null;
    private static int _port = 10090;

    /// <summary>
    /// 最近启动记录（供状态查询和关闭接口使用）
    /// </summary>
    private static readonly Dictionary<GameType, ApiLaunchInfo> LastLaunchDb = new();

    /// <summary>
    /// BF1ClientAPI 请求客户端（仅用于读取 BF1 详细游戏状态）
    /// </summary>
    private static readonly HttpClient _bf1Client = new() { Timeout = TimeSpan.FromSeconds(3) };

    /// <summary>
    /// 启动 API 监听服务
    /// </summary>
    public static void Run(int port)
    {
        if (_httpListener is not null)
        {
            LoggerHelper.Warn("API 服务已经在运行，请勿重复启动");
            return;
        }

        _port = port;

        try
        {
            _httpListener = new HttpListener
            {
                AuthenticationSchemes = AuthenticationSchemes.Anonymous
            };

            _httpListener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _httpListener.Start();

            LoggerHelper.Info($"API 服务监听成功: http://127.0.0.1:{_port}");

            _httpListener.BeginGetContext(Result, null);
        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"API 服务启动失败: {ex}");
            _httpListener = null;
        }
    }

    /// <summary>
    /// 停止 API 监听服务
    /// </summary>
    public static void Stop()
    {
        _httpListener?.Stop();
        _httpListener = null;
        LoggerHelper.Info("API 服务已停止");
    }

    /// <summary>
    /// 处理传入的请求
    /// </summary>
    private static void Result(IAsyncResult asyncResult)
    {
        try
        {
            // 避免关闭时抛出异常
            if (_httpListener is null)
                return;

            var context = _httpListener.EndGetContext(asyncResult);
            // 开始异步检索下一个请求
            _httpListener.BeginGetContext(Result, null);

            HandleRequest(context);
        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"API 处理请求异常: {ex}");
        }
    }

    /// <summary>
    /// 路由分发
    /// </summary>
    private static async void HandleRequest(HttpListenerContext context)
    {
        try
        {
            var method = context.Request.HttpMethod.ToUpperInvariant();
            var path = context.Request.Url.AbsolutePath.TrimEnd('/');
            var query = context.Request.QueryString;

            LoggerHelper.Debug($"API 请求: {method} {context.Request.Url}");

            switch (path)
            {
                case "/api/health":
                    if (method == "GET")
                        HandleHealth(context);
                    else
                        WriteError(context, 405, "不支持的请求方法");
                    break;

                case "/api/games":
                    if (method == "GET")
                        HandleGames(context);
                    else
                        WriteError(context, 405, "不支持的请求方法");
                    break;

                case "/api/game/launch":
                    if (method == "POST")
                        HandleLaunch(context);
                    else
                        WriteError(context, 405, "不支持的请求方法");
                    break;

                case "/api/game/status":
                    if (method == "GET")
                        await HandleStatus(context, query["gameType"], query["processName"]);
                    else
                        WriteError(context, 405, "不支持的请求方法");
                    break;

                case "/api/game/kill":
                    if (method == "POST")
                        HandleKill(context);
                    else
                        WriteError(context, 405, "不支持的请求方法");
                    break;

                case "/api/games/args":
                    if (method == "GET")
                        HandleGetArgs(context, query["gameType"]);
                    else if (method == "POST")
                        HandleSetArgs(context);
                    else
                        WriteError(context, 405, "不支持的请求方法");
                    break;

                default:
                    WriteError(context, 404, $"未找到接口: {path}");
                    break;
            }
        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"API 路由处理异常: {ex}");
            WriteError(context, 500, $"服务器内部错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 健康检查
    /// </summary>
    private static void HandleHealth(HttpListenerContext context)
    {
        WriteJson(context, 200, new
        {
            status = "ok",
            version = CoreUtil.VersionInfo.ToString(),
            port = _port,
            service = "EAappEmulater Remote API"
        });
    }

    /// <summary>
    /// 游戏列表
    /// </summary>
    private static void HandleGames(HttpListenerContext context)
    {
        var games = new List<ApiGameInfo>();

        foreach (var item in Base.GameInfoDb)
        {
            var info = item.Value;
            var installDir = string.Empty;

            try
            {
                installDir = RegistryHelper.GetInstallDirByContentId(info.ContentId);
            }
            catch
            {
                // 忽略注册表读取异常
            }

            games.Add(new ApiGameInfo
            {
                GameType = info.GameType.ToString(),
                Name = info.Name,
                Name2 = info.Name2,
                AppName = info.AppName,
                ContentId = info.ContentId,
                Dir = installDir,
                Dir2 = info.Dir2,
                IsInstalled = !string.IsNullOrWhiteSpace(installDir) || !string.IsNullOrWhiteSpace(info.Dir2),
                IsEAAC = info.IsEAAC,
                Locales = info.Locales
            });
        }

        WriteJson(context, 200, games);
    }

    /// <summary>
    /// 启动游戏
    /// </summary>
    private static void HandleLaunch(HttpListenerContext context)
    {
        ApiLaunchRequest request;
        try
        {
            request = JsonConvert.DeserializeObject<ApiLaunchRequest>(ReadBody(context));
        }
        catch (Exception ex)
        {
            WriteError(context, 400, $"请求体解析失败: {ex.Message}");
            return;
        }

        if (request is null)
        {
            WriteError(context, 400, "请求体为空");
            return;
        }

        // 解析游戏类型
        if (string.IsNullOrWhiteSpace(request.GameType))
        {
            WriteError(context, 400, "缺少必填字段 gameType");
            return;
        }

        if (!Enum.TryParse(request.GameType, true, out GameType gameType) || !Base.GameInfoDb.ContainsKey(gameType))
        {
            WriteError(context, 400, $"无效的游戏类型: {request.GameType}");
            return;
        }

        var gameInfo = Base.GameInfoDb[gameType];

        // 确定 exe 路径
        var exePath = request.ExePath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            // 缺省用自定义路径或注册表路径
            if (gameInfo.IsUseCustom && !string.IsNullOrWhiteSpace(gameInfo.Dir2))
                exePath = Path.Combine(gameInfo.Dir2, gameInfo.AppName);
            else if (!string.IsNullOrWhiteSpace(gameInfo.Dir))
                exePath = Path.Combine(gameInfo.Dir, gameInfo.AppName);
            else
            {
                var installDir = RegistryHelper.GetInstallDirByContentId(gameInfo.ContentId);
                if (!string.IsNullOrWhiteSpace(installDir))
                    exePath = Path.Combine(installDir, gameInfo.AppName);
            }
        }

        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            WriteError(context, 400, $"未找到游戏可执行文件: {exePath ?? "(空)"}");
            return;
        }

        // 校验登录 Token
        if (string.IsNullOrWhiteSpace(Account.OriginPCToken))
        {
            WriteError(context, 409, "EA 账号未登录，无法启动游戏");
            return;
        }

        // 确定启动参数（模板 / 透传双模式）
        var argsSource = string.IsNullOrWhiteSpace(request.ArgsSource) ? "direct" : request.ArgsSource.ToLowerInvariant();
        string finalArgs;

        if (argsSource == "template")
        {
            var template = RunArgsManager.GetTemplate(gameType);
            if (template is null)
            {
                WriteError(context, 400, $"未找到游戏 {gameType} 的启动参数模板，请先通过 /api/games/args 保存");
                return;
            }

            finalArgs = RunArgsManager.ApplyVariables(template, request.TemplateVars);
        }
        else if (argsSource == "direct")
        {
            finalArgs = request.Arguments ?? string.Empty;
        }
        else
        {
            WriteError(context, 400, $"无效的 argsSource: {request.ArgsSource}，可选值 template/direct");
            return;
        }

        finalArgs = finalArgs.Trim();

        // 确定进程名（供状态检测和关闭）
        var processNames = request.ProcessName;
        if (string.IsNullOrWhiteSpace(processNames))
        {
            // 缺省用 exe 文件名去扩展名；EAAC 游戏可额外检测游戏本体进程
            var exeName = Path.GetFileNameWithoutExtension(exePath);
            processNames = string.IsNullOrWhiteSpace(exeName) ? gameInfo.AppName : exeName;
        }

        // 显式传入 exePath 时写入自定义路径槽，供 UI 查看复用
        if (!string.IsNullOrWhiteSpace(request.ExePath))
        {
            gameInfo.Dir2 = Path.GetDirectoryName(exePath);
            gameInfo.Args2 = finalArgs;
        }

        // 记录最近启动信息
        var launchInfo = new ApiLaunchInfo
        {
            ExePath = exePath,
            WorkingDirectory = request.WorkingDirectory,
            Arguments = finalArgs,
            ContentId = request.ContentId ?? gameInfo.ContentId,
            ProcessNames = processNames,
            Success = false,
            LaunchTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
        LastLaunchDb[gameType] = launchInfo;

        // 启动游戏（API 触发不弹 UI 通知）
        Game.RunGameWithExePath(
            gameType,
            exePath,
            request.WorkingDirectory,
            finalArgs,
            request.ContentId ?? gameInfo.ContentId,
            request.Locale,
            false);

        launchInfo.Success = true;

        WriteJson(context, 200, launchInfo);
    }

    /// <summary>
    /// 查询游戏状态
    /// </summary>
    private static async Task HandleStatus(HttpListenerContext context, string gameTypeStr, string processName)
    {
        // 无 gameType 参数：返回全部有启动记录的游戏状态数组
        // 注意：不能用 AppName 扫描全部游戏判断运行——EAAntiCheat.GameServiceLauncher 等
        // 进程名被多个游戏共用，会导致误报；只有启动记录里的 ProcessNames 是准确的
        if (string.IsNullOrWhiteSpace(gameTypeStr))
        {
            var result = new List<ApiGameStatus>();
            foreach (var type in LastLaunchDb.Keys)
                result.Add(await BuildStatus(type, null));

            WriteJson(context, 200, result);
            return;
        }

        if (!Enum.TryParse(gameTypeStr, true, out GameType gameType) || !Base.GameInfoDb.ContainsKey(gameType))
        {
            WriteError(context, 400, $"无效的游戏类型: {gameTypeStr}");
            return;
        }

        WriteJson(context, 200, await BuildStatus(gameType, processName));
    }

    /// <summary>
    /// 构建单游戏状态
    /// </summary>
    private static async Task<ApiGameStatus> BuildStatus(GameType gameType, string processNameOverride)
    {
        var gameInfo = Base.GameInfoDb[gameType];

        // 确定进程名列表
        var processNames = processNameOverride;
        if (string.IsNullOrWhiteSpace(processNames) && LastLaunchDb.TryGetValue(gameType, out var lastLaunch))
            processNames = lastLaunch.ProcessNames;
        if (string.IsNullOrWhiteSpace(processNames))
            processNames = gameInfo.AppName;

        var processes = CollectProcesses(processNames);

        // 仅 BF3/BF4/BFH 有雪球管道游戏内状态
        string gameState = null;
        switch (gameType)
        {
            case GameType.BF3:
            case GameType.BF4:
            case GameType.BFH:
                gameState = BattlelogHttpServer.GetPipeServerGameState(GetBattlelogType(gameType));
                break;
        }

        // BF1 有独立详细状态（BF1ClientAPI 内存读取）
        ApiBf1Status bf1Status = null;
        if (gameType == GameType.BF1)
            bf1Status = await GetBf1StatusAsync();

        LastLaunchDb.TryGetValue(gameType, out var lastLaunchInfo);

        return new ApiGameStatus
        {
            GameType = gameType.ToString(),
            IsRunning = processes.Count > 0,
            Processes = processes,
            GameState = gameState,
            LastLaunch = lastLaunchInfo,
            Bf1Status = bf1Status
        };
    }

    /// <summary>
    /// 获取 BF1 详细游戏状态（请求 BF1ClientAPI 内存读取服务）
    /// 服务未运行 / 内存未初始化 / 请求超时返回 null
    /// </summary>
    private static async Task<ApiBf1Status> GetBf1StatusAsync()
    {
        try
        {
            var url = $"http://127.0.0.1:{Globals.Bf1ClientApiPort}/Game/GetGameStatus";
            var json = await _bf1Client.GetStringAsync(url);

            var response = JsonConvert.DeserializeObject<Bf1GameStatusResponse>(json);
            if (response is null || response.Code != 200 || response.Data is null)
                return null;

            var data = response.Data;
            data.Scene = MapBf1Scene(data);
            data.SceneText = Bf1SceneText(data.Scene);

            return data;
        }
        catch (Exception ex)
        {
            LoggerHelper.Debug($"获取 BF1 详细状态失败（BF1ClientAPI 可能未运行）: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 派生 BF1 场景状态（对齐 farm 的 scene 分类）
    /// </summary>
    private static string MapBf1Scene(ApiBf1Status status)
    {
        // 优先用明确的布尔标志
        if (status.IsMenu)
            return "menu";

        // 回合结束/结算页（可能在 Ingame 状态但仍显示结算）
        if (status.IsEpilogue)
            return "eor";

        // status=3 Ingame 或 state=12 Ingame
        if (status.Status == 3 || status.State == 12)
            return "playing";

        // state=17 None（游戏刚启动未进游戏）
        if (status.State == 17)
            return "menu";

        // 其余为进服中/加载中
        return "loading";
    }

    /// <summary>
    /// BF1 场景中文描述
    /// </summary>
    private static string Bf1SceneText(string scene)
    {
        return scene switch
        {
            "menu" => "菜单/空闲",
            "loading" => "进服中/加载中",
            "playing" => "对局中",
            "eor" => "回合结束",
            _ => "未知",
        };
    }

    /// <summary>
    /// 关闭游戏
    /// </summary>
    private static void HandleKill(HttpListenerContext context)
    {
        ApiKillRequest request;
        try
        {
            request = JsonConvert.DeserializeObject<ApiKillRequest>(ReadBody(context));
        }
        catch (Exception ex)
        {
            WriteError(context, 400, $"请求体解析失败: {ex.Message}");
            return;
        }

        if (request is null)
        {
            WriteError(context, 400, "请求体为空");
            return;
        }

        // processName 优先，其次 gameType
        var processName = request.ProcessName;
        if (string.IsNullOrWhiteSpace(processName) && !string.IsNullOrWhiteSpace(request.GameType))
        {
            if (!Enum.TryParse(request.GameType, true, out GameType gameType) || !Base.GameInfoDb.ContainsKey(gameType))
            {
                WriteError(context, 400, $"无效的游戏类型: {request.GameType}");
                return;
            }

            // 优先用最近启动记录里的进程名
            if (LastLaunchDb.TryGetValue(gameType, out var lastLaunch))
                processName = lastLaunch.ProcessNames;
            else
                processName = Base.GameInfoDb[gameType].AppName;
        }

        if (string.IsNullOrWhiteSpace(processName))
        {
            WriteError(context, 400, "缺少 processName 或 gameType");
            return;
        }

        foreach (var name in processName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            ProcessHelper.CloseProcess(name);

        WriteJson(context, 200, new
        {
            success = true,
            processName
        });
    }

    /// <summary>
    /// 读取启动参数模板
    /// </summary>
    private static void HandleGetArgs(HttpListenerContext context, string gameTypeStr)
    {
        if (string.IsNullOrWhiteSpace(gameTypeStr))
        {
            WriteError(context, 400, "缺少参数 gameType");
            return;
        }

        if (!Enum.TryParse(gameTypeStr, true, out GameType gameType) || !Base.GameInfoDb.ContainsKey(gameType))
        {
            WriteError(context, 400, $"无效的游戏类型: {gameTypeStr}");
            return;
        }

        var template = RunArgsManager.GetTemplate(gameType);
        if (template is null)
        {
            WriteJson(context, 200, new
            {
                gameType = gameType.ToString(),
                content = "",
                exists = false
            });
            return;
        }

        WriteJson(context, 200, new
        {
            gameType = gameType.ToString(),
            content = template,
            exists = true
        });
    }

    /// <summary>
    /// 保存启动参数模板
    /// </summary>
    private static void HandleSetArgs(HttpListenerContext context)
    {
        ApiArgsRequest request;
        try
        {
            request = JsonConvert.DeserializeObject<ApiArgsRequest>(ReadBody(context));
        }
        catch (Exception ex)
        {
            WriteError(context, 400, $"请求体解析失败: {ex.Message}");
            return;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.GameType))
        {
            WriteError(context, 400, "缺少必填字段 gameType");
            return;
        }

        if (!Enum.TryParse(request.GameType, true, out GameType gameType) || !Base.GameInfoDb.ContainsKey(gameType))
        {
            WriteError(context, 400, $"无效的游戏类型: {request.GameType}");
            return;
        }

        if (RunArgsManager.SetTemplate(gameType, request.Content))
        {
            WriteJson(context, 200, new { success = true, gameType = gameType.ToString() });
        }
        else
        {
            WriteError(context, 500, "保存模板失败");
        }
    }

    /////////////////////////////////////////////////////////

    /// <summary>
    /// 收集进程详情
    /// </summary>
    private static List<ApiProcessInfo> CollectProcesses(string processNames)
    {
        var result = new List<ApiProcessInfo>();

        foreach (var rawName in processNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var name = rawName;
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name = name[..^4];

            try
            {
                foreach (var process in Process.GetProcessesByName(name))
                {
                    result.Add(new ApiProcessInfo
                    {
                        Name = process.ProcessName,
                        Pid = process.Id,
                        WorkingSetMB = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 1),
                        CpuSeconds = GetCpuSeconds(process),
                        StartTime = GetStartTime(process)
                    });
                }
            }
            catch
            {
                // 进程不存在或已退出，忽略
            }
        }

        return result;
    }

    /// <summary>
    /// 获取进程 CPU 占用秒数（可能抛异常）
    /// </summary>
    private static double GetCpuSeconds(Process process)
    {
        try
        {
            return Math.Round(process.TotalProcessorTime.TotalSeconds, 1);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 获取进程启动时间（可能抛异常）
    /// </summary>
    private static string GetStartTime(Process process)
    {
        try
        {
            return process.StartTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 映射游戏类型到 Battlelog 管道类型
    /// </summary>
    private static BattlelogType GetBattlelogType(GameType gameType)
    {
        return gameType switch
        {
            GameType.BF3 => BattlelogType.BF3,
            GameType.BF4 => BattlelogType.BF4,
            GameType.BFH => BattlelogType.BFH,
            _ => BattlelogType.None,
        };
    }

    /// <summary>
    /// 读取请求体（统一按 UTF-8 解析，避免依赖客户端 Content-Type charset）
    /// </summary>
    private static string ReadBody(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// 输出 JSON 响应
    /// </summary>
    private static void WriteJson(HttpListenerContext context, int code, object data)
    {
        var json = JsonConvert.SerializeObject(data);
        var bytes = Encoding.UTF8.GetBytes(json);

        context.Response.StatusCode = code;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        context.Response.Close();
    }

    /// <summary>
    /// 输出错误响应
    /// </summary>
    private static void WriteError(HttpListenerContext context, int code, string message)
    {
        WriteJson(context, code, new { error = message });
    }
}
