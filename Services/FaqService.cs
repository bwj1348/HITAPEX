using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace HITAPEX.Services;

/// <summary>一条 FAQ 问答。</summary>
public class FaqItem
{
    public string Q { get; set; } = string.Empty;
    public string A { get; set; } = string.Empty;
}

/// <summary>
/// 帮助页面 FAQ 内容服务：从程序目录 Resources/FAQ/faq.{culture}.json 读取问答内容。
/// 每语言一个独立文件（参照 Resources/Locales 的发布方式随程序分发）。
/// 指定语言文件缺失时回退到 faq.zh-CN.json，均缺失时返回空数据。
/// </summary>
public static class FaqService
{
    private const string FallbackCulture = "zh-CN";

    /// <summary>
    /// 读取指定语言（或回退语言）的 FAQ 内容。
    /// 返回字典：类别（Base/Wheel/Pedal/Software）→ 有序问答列表。
    /// </summary>
    public static Dictionary<string, List<FaqItem>> Load(string culture)
    {
        foreach (var candidate in new[] { culture, FallbackCulture })
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Resources", "FAQ", $"faq.{candidate}.json");
            if (!File.Exists(path)) continue;

            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, List<FaqItem>>>(
                    File.ReadAllText(path));
                if (data != null)
                    return data;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FAQ] 读取失败 {path}: {ex.Message}");
            }
        }

        Debug.WriteLine($"[FAQ] 未找到 FAQ 文件（culture={culture}），返回空内容");
        return new Dictionary<string, List<FaqItem>>();
    }
}