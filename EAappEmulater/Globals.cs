using EAappEmulater.Core;
using EAappEmulater.Enums;
using EAappEmulater.Helper;
using EAappEmulater.Utils;

namespace EAappEmulater;

public static class Globals
{
    /// <summary>
    /// 全局配置文件路径
    /// </summary>
    private static readonly string _configPath;

    /// <summary>
    /// 当前使用的账号槽
    /// </summary>
    public static AccountSlot AccountSlot { get; set; } = AccountSlot.S0;

    public static bool IsGetFriendsSuccess { get; set; } = false;
    public static string FriendsXmlString { get; set; } = string.Empty;
    public static string QueryPresenceString { get; set; } = string.Empty;

    /// <summary>
    /// 程序主体语言, 默认跟随系统.
    /// </summary>
    public static string Language { get; set; } = string.Empty;

    public static string DefaultLanguage { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用自动登录
    /// </summary>
    public static bool AutoLoginEnabled { get; set; } = false;

    /// <summary>
    /// 远程 API 服务端口（仅监听本机回环）
    /// </summary>
    public static int ApiPort { get; set; } = 12000;

    /// <summary>
    /// 是否启用远程 API 服务
    /// </summary>
    public static bool ApiEnabled { get; set; } = true;

    /// <summary>
    /// BF1ClientAPI 服务端口（BF1 详细状态读取，默认 GHS 版 10087）
    /// </summary>
    public static int Bf1ClientApiPort { get; set; } = 10087;

    static Globals()
    {
        _configPath = Path.Combine(CoreUtil.Dir_Config, "Config.ini");
    }

    /// <summary>
    /// 读取全局配置文件
    /// </summary>
    public static void Read()
    {
        LoggerHelper.Info(I18nHelper.I18n._("Globals.ReadConfig"));

        var slot = IniHelper.ReadString("Globals", "AccountSlot", _configPath);
        var defaultLanguage = IniHelper.ReadString("Globals", "lang", _configPath);
        var autoLoginEnabled = IniHelper.ReadString("Globals", "AutoLoginEnabled", _configPath);

        LoggerHelper.Info(I18nHelper.I18n._("Globals.CurrentConfigPath", _configPath));
        LoggerHelper.Info(I18nHelper.I18n._("Globals.ReadConfigSuccess", slot));

        if (Enum.TryParse(slot, out AccountSlot accountSlot))
        {
            AccountSlot = accountSlot;
            LoggerHelper.Info(I18nHelper.I18n._("Globals.EnumTryParseSuccess", AccountSlot));
        }
        else
        {
            LoggerHelper.Warn(I18nHelper.I18n._("Globals.EnumTryParseError", slot));
        }


        // Accept any configured language if it's in the supported language list
        if (!string.IsNullOrWhiteSpace(defaultLanguage))
        {
            try
            {
                var langEntry = LanguageConfigHelper.FindByCode(defaultLanguage);
                if (langEntry != null)
                {
                    DefaultLanguage = defaultLanguage;
                    LoggerHelper.Info(I18nHelper.I18n._("Globals.SetDefaultLanguageSuccess", DefaultLanguage));
                }
                else
                {
                    LoggerHelper.Warn(I18nHelper.I18n._("Globals.SetDefaultLanguageError"));
                }
            }
            catch
            {
                LoggerHelper.Warn(I18nHelper.I18n._("Globals.SetDefaultLanguageError"));
            }
        }

        if (!string.IsNullOrWhiteSpace(autoLoginEnabled))
        {
            if (bool.TryParse(autoLoginEnabled, out bool autoLogin))
            {
                AutoLoginEnabled = autoLogin;
                LoggerHelper.Info(I18nHelper.I18n._("Globals.AutoLoginSetting", AutoLoginEnabled));
            }
            else
            {
                LoggerHelper.Warn(I18nHelper.I18n._("Globals.AutoLoginParseError", autoLoginEnabled));
            }
        }
        else
        {
            LoggerHelper.Info(I18nHelper.I18n._("Globals.AutoLoginNotConfigured"));
        }

        // 读取远程 API 配置
        var apiPort = IniHelper.ReadString("Api", "Port", _configPath);
        var apiEnabled = IniHelper.ReadString("Api", "Enabled", _configPath);

        if (int.TryParse(apiPort, out int port) && port is > 0 and < 65536)
        {
            ApiPort = port;
            LoggerHelper.Info($"API 配置读取成功: 端口 {ApiPort}");
        }
        else
        {
            LoggerHelper.Info($"API 配置未设置或无效，使用默认端口 {ApiPort}");
        }

        if (bool.TryParse(apiEnabled, out bool enabled))
        {
            ApiEnabled = enabled;
            LoggerHelper.Info($"API 启用状态: {ApiEnabled}");
        }

        // 读取 BF1ClientAPI 端口
        var bf1ClientPort = IniHelper.ReadString("Api", "Bf1ClientApiPort", _configPath);
        if (int.TryParse(bf1ClientPort, out int bf1Port) && bf1Port is > 0 and < 65536)
        {
            Bf1ClientApiPort = bf1Port;
            LoggerHelper.Info($"BF1ClientAPI 端口: {Bf1ClientApiPort}");
        }

        LoggerHelper.Info(I18nHelper.I18n._("Globals.ReadGlobalConfigSuccess"));
    }

    /// <summary>
    /// 写入全局配置文件
    /// </summary>
    public static void Write()
    {
        LoggerHelper.Info(I18nHelper.I18n._("Globals.SaveGlobalConfigProcess"));

        try
        {
            // ensure config dir and file exist
            FileHelper.CreateDirectory(CoreUtil.Dir_Config);
            FileHelper.CreateFile(_configPath);

            IniHelper.WriteString("Globals", "AccountSlot", $"{AccountSlot}", _configPath);
            IniHelper.WriteString("Globals", "lang", DefaultLanguage ?? string.Empty, _configPath);
            IniHelper.WriteString("Globals", "AutoLoginEnabled", $"{AutoLoginEnabled}", _configPath);

            IniHelper.WriteString("Api", "Port", $"{ApiPort}", _configPath);
            IniHelper.WriteString("Api", "Enabled", $"{ApiEnabled}", _configPath);
            IniHelper.WriteString("Api", "Bf1ClientApiPort", $"{Bf1ClientApiPort}", _configPath);

            LoggerHelper.Info(I18nHelper.I18n._("Globals.SaveGlobalConfigPath", _configPath));
            LoggerHelper.Info(I18nHelper.I18n._("Globals.SaveGlobalConfigSuccess"));
        }
        catch (Exception ex)
        {
            LoggerHelper.Error(I18nHelper.I18n._("Globals.SaveGlobalConfigError", ex));
        }
    }

    /// <summary>
    /// 获取当前账号槽全局配置文件路径
    /// </summary>
    public static string GetAccountIniPath()
    {
        return Account.AccountPathDb[AccountSlot];
    }

    /// <summary>
    /// 获取当前账号槽WebView2缓存路径
    /// </summary>
    public static string GetAccountCacheDir()
    {
        return CoreUtil.AccountCacheDb[AccountSlot];
    }
}