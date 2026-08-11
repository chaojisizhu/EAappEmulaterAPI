using EAappEmulater.Enums;
using EAappEmulater.Helper;
using EAappEmulater.Utils;
using System.Text.RegularExpressions;

namespace EAappEmulater.Core;

/// <summary>
/// 启动参数模板管理
/// 参考 NoRenderBF1FarmBot 的 RunArgsManager 机制通用化：
/// 每个游戏一个模板文件，可保存完整启动参数（如 BF1 的 requestState 直连服务器参数），
/// 启动时仅替换目标字段（如 gameId），其余参数原样透传。
/// </summary>
public static class RunArgsManager
{
    /// <summary>
    /// 模板目录：Documents\EAappEmulater\Config\RunArgs
    /// </summary>
    public static string TemplateDir => Path.Combine(CoreUtil.Dir_Config, "RunArgs");

    /// <summary>
    /// 获取模板文件完整路径
    /// </summary>
    public static string GetTemplatePath(GameType gameType)
    {
        return Path.Combine(TemplateDir, $"{gameType}.txt");
    }

    /// <summary>
    /// 读取指定游戏的启动参数模板
    /// 模板不存在时返回 null
    /// </summary>
    public static string GetTemplate(GameType gameType)
    {
        try
        {
            var path = GetTemplatePath(gameType);
            if (!File.Exists(path))
                return null;

            var text = File.ReadAllText(path);
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"读取启动参数模板失败: {gameType}, {ex}");
            return null;
        }
    }

    /// <summary>
    /// 保存/更新指定游戏的启动参数模板
    /// </summary>
    public static bool SetTemplate(GameType gameType, string content)
    {
        try
        {
            FileHelper.CreateDirectory(TemplateDir);

            var path = GetTemplatePath(gameType);
            File.WriteAllText(path, content ?? string.Empty);
            LoggerHelper.Info($"保存启动参数模板成功: {gameType}");
            return true;
        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"保存启动参数模板失败: {gameType}, {ex}");
            return false;
        }
    }

    /// <summary>
    /// 双语法变量替换引擎：
    /// 1. 占位符语法：{{gameId}} -> 值
    /// 2. farm 风格正则：-gameId 123 -> -gameId <新值>（匹配 "-{name}\s+["']?值["']?"）
    /// 变量名大小写不敏感；模板中不存在的变量名保持原样。
    /// </summary>
    public static string ApplyVariables(string template, Dictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(template) || vars is null || vars.Count == 0)
            return template;

        var result = template;

        foreach (var pair in vars)
        {
            var key = pair.Key?.Trim() ?? string.Empty;
            var value = pair.Value ?? string.Empty;
            if (key.Length == 0)
                continue;

            // 语法一：{{占位符}}（用 MatchEvaluator 避免 replacement 中 $ 被转义）
            result = Regex.Replace(result, @"\{\{\s*" + Regex.Escape(key) + @"\s*\}\}", _ => value, RegexOptions.IgnoreCase);

            // 语法二：farm 风格 "-key 值" 或 "-key "值""
            var farmPattern = $@"(?i)(?<!(?<!\S)-)(?<prefix>-{Regex.Escape(key)}\s+[""']?)(?<old>(?:[^-\s""']+|""[^""]*""|'[^']*'))(?<suffix>[""']?)(?=\s|$)";
            result = Regex.Replace(result, farmPattern, m =>
            {
                var escapedValue = value.Contains(" ") ? $"\"{value}\"" : value;
                return m.Groups["prefix"].Value + escapedValue + m.Groups["suffix"].Value;
            });
        }

        return result;
    }

    /// <summary>
    /// 提取模板中的 gameId（farm 兼容：-gameId 纯数字字段）
    /// 未找到返回空字符串
    /// </summary>
    public static string ExtractGameId(string template)
    {
        if (string.IsNullOrEmpty(template))
            return string.Empty;

        var match = Regex.Match(template, @"(?i)-gameId\s+[""']?(\d+)[""']?");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}
