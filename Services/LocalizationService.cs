using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;

namespace HITAPEX.Services;

/// <summary>
/// 单例本地化服务，从 JSON 文件加载翻译字符串，支持运行时切换语言。
/// 实现 INotifyPropertyChanged，XAML 可通过 LocExtension 绑定到索引器实现动态切换。
/// </summary>
public class LocalizationService : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationService> _instance = new(() => new());
    public static LocalizationService Instance => _instance.Value;

    private Dictionary<string, string> _strings = new();
    private string _currentLanguage = "zh-CN";
    private string _currentFontFamily = "Microsoft YaHei";

    /// <summary>
    /// 当前语言代码（zh-CN / en-US）
    /// </summary>
    public string CurrentLanguage => _currentLanguage;

    /// <summary>
    /// 当前语言对应的字体。切换语言时自动更新。
    /// XAML 中可通过 {lex:Font} 绑定。
    /// </summary>
    public string CurrentFontFamily
    {
        get => _currentFontFamily;
        private set
        {
            if (_currentFontFamily != value)
            {
                _currentFontFamily = value;
                OnPropertyChanged(nameof(CurrentFontFamily));
            }
        }
    }

    private Thickness _navSpacing = new(31, 0, 0, 0);

    /// <summary>
    /// 导航栏图标与文本之间的左边距。从 JSON Nav.IconTextSpacing 读取，
    /// 根据语言动态调整，确保较长语言下的文本完整显示。
    /// </summary>
    public Thickness NavSpacing
    {
        get => _navSpacing;
        private set
        {
            if (_navSpacing != value)
            {
                _navSpacing = value;
                OnPropertyChanged(nameof(NavSpacing));
            }
        }
    }

    /// <summary>
    /// 索引器 — XAML 绑定和代码中获取翻译字符串的入口。
    /// 用法：LocalizationService.Instance["Nav.Home"]
    /// </summary>
    public string this[string key]
    {
        get
        {
            if (_strings.TryGetValue(key, out var value))
                return value;

#if DEBUG
            Debug.WriteLine($"[Loc] Missing key: {key}");
#endif
            return key; // fallback: 显示 key 本身
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 初始化服务，加载指定语言的翻译文件。
    /// 应在 App.OnStartup 中调用。
    /// </summary>
    public void Initialize(string culture)
    {
        _currentLanguage = culture;
        LoadStrings(culture);
    }

    /// <summary>
    /// 切换语言 — 重新加载 JSON、保存设置、通知所有绑定刷新。
    /// </summary>
    public void SetLanguage(string culture)
    {
        if (_currentLanguage == culture)
            return;

        _currentLanguage = culture;
        LoadStrings(culture);

        // 持久化语言设置
        HITAPEX.Properties.Settings.Default.Language = culture;
        HITAPEX.Properties.Settings.Default.Save();

        // 通知所有绑定刷新（null 使所有属性绑定失效，包括索引器）
        OnPropertyChanged(null);
    }

    /// <summary>
    /// 获取格式化字符串（string.Format 包装）
    /// </summary>
    public string Format(string key, params object[] args)
    {
        var template = this[key];
        return template == key ? template : string.Format(template, args);
    }

    private const string FontFamilyKey = "App.FontFamily";
    private const string NavSpacingKey = "Nav.IconTextSpacing";

    private void LoadStrings(string culture)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Resources", "Locales");
        var path = Path.Combine(dir, $"{culture}.json");

        if (!File.Exists(path))
        {
            Debug.WriteLine($"[Loc] Locale file not found: {path}");
            _strings = new Dictionary<string, string>();
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                       ?? new Dictionary<string, string>();

            // 从 JSON 中读取当前语言对应的字体
            if (_strings.TryGetValue(FontFamilyKey, out var font))
            {
                CurrentFontFamily = font;
            }

            // 从 JSON 中读取导航栏图标与文本之间的间距
            if (_strings.TryGetValue(NavSpacingKey, out var spacingStr)
                && double.TryParse(spacingStr, out var spacing))
            {
                NavSpacing = new Thickness(spacing, 0, 0, 0);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Loc] Failed to load locale '{culture}': {ex.Message}");
            _strings = new Dictionary<string, string>();
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
