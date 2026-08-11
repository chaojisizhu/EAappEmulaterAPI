using EAappEmulater.Enums;
using EAappEmulater.Helper;
using EAappEmulater.Models;
using System.Runtime.Serialization.Formatters.Binary;

namespace EAappEmulater.Core;

public static class Game
{
    /// <summary>
    /// 获取系统环境变量集合
    /// </summary>
    private static Dictionary<string, string> GetEnvironmentVariables()
    {
        var environmentVariables = new Dictionary<string, string>();
        foreach (DictionaryEntry dirEnity in Environment.GetEnvironmentVariables())
        {   
            environmentVariables.Add(dirEnity.Key.ToString(), dirEnity.Value.ToString());
        }
        return environmentVariables;
    }

    /// <summary>
    /// 启动游戏
    /// </summary>
    public static void RunGame(GameType gameType, string webArgs = "", bool isNotice = true)
    {
        try
        {
            var gameInfo = Base.GameInfoDb[gameType];

            ////////////////////////////////////////////////////////

            var execPath = string.Empty;        // 注册表路径
            var execPath2 = string.Empty;       // 自定义启动路径

            // 处理 双人成行 特殊启动路径
            if (gameInfo.GameType is GameType.ITT)
            {
                // 双人成行
                execPath = Path.Combine(gameInfo.Dir, "Nuts\\Binaries\\Win64", gameInfo.AppName);
                execPath2 = Path.Combine(gameInfo.Dir2, "Nuts\\Binaries\\Win64", gameInfo.AppName);
            }
            else if (gameInfo.GameType is GameType.SWJFO)
            {
                execPath = Path.Combine(gameInfo.Dir, "SwGame\\Binaries\\Win64", gameInfo.AppName);
                execPath2 = Path.Combine(gameInfo.Dir2, "SwGame\\Binaries\\Win64", gameInfo.AppName);
            }
            else
            {
                // 其他
                execPath = Path.Combine(gameInfo.Dir, gameInfo.AppName);
                execPath2 = Path.Combine(gameInfo.Dir2, gameInfo.AppName);
            }

            // 判断是否使用自定义路径启动游戏
            if (gameInfo.IsUseCustom)
            {
                // 自定义游戏路径

                // 判断游戏路径
                if (string.IsNullOrWhiteSpace(gameInfo.Dir2))
                {
                    LoggerHelper.Warn(I18nHelper.I18n._("Core.Game.StartGameErrorDir", gameType, gameInfo.Dir));
                    if (isNotice)
                        NotifierHelper.Warning(I18nHelper.I18n._("Core.Game.StartGameErrorDir", gameType, ""));

                    return;
                }

                // 判断游戏文件
                if (!File.Exists(execPath2))
                {
                    LoggerHelper.Warn(I18nHelper.I18n._("Core.Game.StartGameErrorExe", gameType, execPath2));
                    if (isNotice)
                        NotifierHelper.Warning(I18nHelper.I18n._("Core.Game.StartGameErrorExe", gameType, ""));

                    return;
                }
            }
            else
            {
                // 注册表游戏路径

                // 判断游戏路径
                if (string.IsNullOrWhiteSpace(gameInfo.Dir))
                {
                    LoggerHelper.Warn(I18nHelper.I18n._("Core.Game.StartGameErrorDir", gameType, gameInfo.Dir));
                    if (isNotice)
                        NotifierHelper.Warning(I18nHelper.I18n._("Core.Game.StartGameErrorDir", gameType, ""));

                    return;
                }

                // 判断游戏文件
                if (!File.Exists(execPath))
                {
                    LoggerHelper.Warn(I18nHelper.I18n._("Core.Game.StartGameErrorExe", gameType, execPath));
                    if (isNotice)
                        NotifierHelper.Warning(I18nHelper.I18n._("Core.Game.StartGameErrorExe", gameType, ""));

                    return;
                }
            }

            ////////////////////////////////////////////////////////

            if (string.IsNullOrWhiteSpace(Account.OriginPCToken))
            {
                LoggerHelper.Warn(I18nHelper.I18n._("Core.Game.StartGameErrorToken", gameType));
                if (isNotice)
                    NotifierHelper.Warning(I18nHelper.I18n._("Core.Game.StartGameErrorToken", gameType));

                return;
            }

            ////////////////////////////////////////////////////////

            // 处理旧的 LSX，设置 Battlelog 监听类型
            SetBattlelogType(gameInfo);

            LoggerHelper.Info(I18nHelper.I18n._("Core.Game.StartGameProcess", gameInfo.Name));
            if (isNotice)
                NotifierHelper.Notice(I18nHelper.I18n._("Core.Game.StartGameProcess", gameInfo.Name));

            // 初始化进程类实例
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = false
            };
            startInfo.Verb = "";

            // 判断是否使用自定义路径启动游戏
            if (gameInfo.IsUseCustom)
            {
                // 自定义游戏路径

                if (gameInfo.GameType is GameType.ITT)
                {
                    // 双人成行
                    startInfo.FileName = Path.Combine(gameInfo.Dir2, "Nuts\\Binaries\\Win64", gameInfo.AppName);
                    startInfo.WorkingDirectory = Path.Combine(gameInfo.Dir2, "Nuts\\Binaries\\Win64", gameInfo.AppName);
                }
                else if (gameInfo.GameType is GameType.SWJFO)
                {
                    // 星球大战 绝地：陨落的武士团
                    startInfo.FileName = Path.Combine(gameInfo.Dir2, "SwGame\\Binaries\\Win64", gameInfo.AppName);
                    startInfo.WorkingDirectory = Path.Combine(gameInfo.Dir2, "SwGame\\Binaries\\Win64", gameInfo.AppName);
                }
                else
                {
                    // 其他
                    startInfo.FileName = Path.Combine(gameInfo.Dir2, gameInfo.AppName);
                    startInfo.WorkingDirectory = gameInfo.Dir2;
                }

                // 启动参数
                startInfo.Arguments = string.Concat(webArgs, " ", gameInfo.Args2).Trim();
            }
            else
            {
                // 注册表游戏路径

                if (gameInfo.GameType is GameType.ITT)
                {
                    // 双人成行
                    startInfo.FileName = Path.Combine(gameInfo.Dir, "Nuts\\Binaries\\Win64", gameInfo.AppName);
                    startInfo.WorkingDirectory = Path.Combine(gameInfo.Dir, "Nuts\\Binaries\\Win64");
                }
                else if (gameInfo.GameType is GameType.SWJFO)
                {
                    // 星球大战 绝地：陨落的武士团
                    startInfo.FileName = Path.Combine(gameInfo.Dir, "SwGame\\Binaries\\Win64", gameInfo.AppName);
                    startInfo.WorkingDirectory = Path.Combine(gameInfo.Dir, "SwGame\\Binaries\\Win64");
                }
                else
                {
                    // 其他
                    startInfo.FileName = Path.Combine(gameInfo.Dir, gameInfo.AppName);
                    startInfo.WorkingDirectory = gameInfo.Dir;
                }

                // 启动参数
                startInfo.Arguments = string.Concat(webArgs, " ", gameInfo.Args).Trim();
            }
            // 通过 OriginDebug 服务进程启动游戏
            SendToOriginDebug(startInfo.FileName, startInfo.WorkingDirectory, startInfo.Arguments, gameInfo.ContentId, RegistryHelper.GetLocaleByContentId(gameInfo.ContentId));

            LoggerHelper.Info(I18nHelper.I18n._("Core.Game.StartGameSuccess", gameInfo.Name));
            if (isNotice)
                NotifierHelper.Success(I18nHelper.I18n._("Core.Game.StartGameSuccess", gameInfo.Name));
        }
        catch (Exception ex)
        {
            LoggerHelper.Error(I18nHelper.I18n._("Core.Game.StartGameError", gameType, ex));
            if (isNotice)
                NotifierHelper.Error(I18nHelper.I18n._("Core.Game.StartGameErrorNotice", gameType));
        }
    }

    /// <summary>
    /// 通过显式 exe 路径启动游戏（供 API 远程调用）
    /// 支持自定义 exe 路径/工作目录/完整启动参数/语言
    /// </summary>
    public static void RunGameWithExePath(GameType gameType, string exePath, string workingDirectory, string arguments, string contentId = "", string locale = "", bool isNotice = true)
    {
        try
        {
            if (!Base.GameInfoDb.ContainsKey(gameType))
            {
                LoggerHelper.Warn(I18nHelper.I18n._("Core.Game.StartGameErrorDir", gameType, ""));
                return;
            }

            var gameInfo = Base.GameInfoDb[gameType];

            // 校验 exe 路径
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                LoggerHelper.Warn(I18nHelper.I18n._("Core.Game.StartGameErrorExe", gameType, exePath));
                if (isNotice)
                    NotifierHelper.Warning(I18nHelper.I18n._("Core.Game.StartGameErrorExe", gameType, ""));

                return;
            }

            // 校验登录 Token
            if (string.IsNullOrWhiteSpace(Account.OriginPCToken))
            {
                LoggerHelper.Warn(I18nHelper.I18n._("Core.Game.StartGameErrorToken", gameType));
                if (isNotice)
                    NotifierHelper.Warning(I18nHelper.I18n._("Core.Game.StartGameErrorToken", gameType));

                return;
            }

            // 缺省参数回填
            if (string.IsNullOrWhiteSpace(contentId))
                contentId = gameInfo.ContentId;

            if (string.IsNullOrWhiteSpace(workingDirectory))
                workingDirectory = Path.GetDirectoryName(exePath);

            // 设置 Battlelog 类型
            SetBattlelogType(gameInfo);

            LoggerHelper.Info(I18nHelper.I18n._("Core.Game.StartGameProcess", gameInfo.Name));
            if (isNotice)
                NotifierHelper.Notice(I18nHelper.I18n._("Core.Game.StartGameProcess", gameInfo.Name));

            SendToOriginDebug(exePath, workingDirectory, arguments, contentId, locale);

            LoggerHelper.Info(I18nHelper.I18n._("Core.Game.StartGameSuccess", gameInfo.Name));
            if (isNotice)
                NotifierHelper.Success(I18nHelper.I18n._("Core.Game.StartGameSuccess", gameInfo.Name));
        }
        catch (Exception ex)
        {
            LoggerHelper.Error(I18nHelper.I18n._("Core.Game.StartGameError", gameType, ex));
            if (isNotice)
                NotifierHelper.Error(I18nHelper.I18n._("Core.Game.StartGameErrorNotice", gameType));
        }
    }

    /// <summary>
    /// 设置 Battlelog 监听类型
    /// </summary>
    private static void SetBattlelogType(GameInfo gameInfo)
    {
        // 处理旧的 LSX
        if (gameInfo.IsOldLSX)
            BattlelogHttpServer.BattlelogType = BattlelogType.BFH;
        else
            BattlelogHttpServer.BattlelogType = gameInfo.GameType switch
            {
                GameType.BF3 => BattlelogType.BF3,
                GameType.BF4 => BattlelogType.BF4,
                GameType.BFH => BattlelogType.BFH,
                _ => BattlelogType.None,
            };
    }

    /// <summary>
    /// 通过命名管道发送启动数据给 OriginDebug 服务进程
    /// </summary>
    private static void SendToOriginDebug(string exePath, string workingDir, string arguments, string contentId, string locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            locale = RegistryHelper.GetLocaleByContentId(contentId);

        var serializedData = $"{exePath};{workingDir};{arguments};{Account.OriginPCToken};{Account.PlayerName};{EaCrypto.GetRTPHandshakeCode()};{contentId};{locale}";

        // 启动程序
        using var pipeClient = new NamedPipeClientStream(".", "RunGame_OriginDebug", PipeDirection.Out);
        pipeClient.Connect();
        using var writer = new StreamWriter(pipeClient);
        writer.WriteLine(serializedData);
    }
}
