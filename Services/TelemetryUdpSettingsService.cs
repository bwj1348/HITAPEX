using System.IO;
using System.Text.Json;

namespace HITAPEX.Services;

/// <summary>
/// UDP 遥测设置服务 —— 按游戏持久化 UDP 监听端口与 fan-out 转发目标，
/// 并在游戏启动前下发到 SDK（SetUDPSettings，须在 StartTelemetry 之前调用）。
/// 存储文件：%LocalAppData%\HITAPEX\telemetry_udp_settings.json
/// </summary>
public static class TelemetryUdpSettingsService
{
    private static readonly string SettingsFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>单个转发目标的持久化模型</summary>
    public sealed class ForwardTargetSettings
    {
        public bool Enabled { get; set; }
        public string Ip { get; set; } = "";
        public ushort Port { get; set; }
    }

    /// <summary>单个游戏的 UDP 设置</summary>
    public sealed class GameUdpSettings
    {
        /// <summary>0 = 不覆盖，使用适配器默认端口</summary>
        public ushort ListenPort { get; set; }
        public List<ForwardTargetSettings> ForwardTargets { get; set; } = new();
    }

    static TelemetryUdpSettingsService()
    {
        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HITAPEX");
        Directory.CreateDirectory(cacheDir);
        SettingsFilePath = Path.Combine(cacheDir, "telemetry_udp_settings.json");
    }

    /// <summary>
    /// SDK 各 UDP 游戏的默认监听端口（《TelemetrySDK_API_Document.md》8.1 节）。
    /// 仅对 UDP 适配器游戏有意义；共享内存游戏无默认端口。
    /// </summary>
    private static readonly Dictionary<int, ushort> DefaultListenPorts = new()
    {
        { 1692250, 20777 },  // F1 22
        { 2108330, 20777 },  // F1 23
        { 2488620, 20777 },  // F1 24
        { 3059520, 20777 },  // F1 25
        { 421020,  20777 },  // DiRT 4
        { 690790,  20777 },  // DiRT Rally 2.0
        { 1953520, 20777 },  // WRC Generations
        { 2440510, 1024  },  // Forza Motorsport (2023)
        { 1293830, 1024  },  // Forza Horizon 4
        { 1551360, 1024  },  // Forza Horizon 5
        { 2483190, 20440 },  // Forza Horizon 6
        { 1849250, 26666 },  // EA SPORTS WRC
        { 284160,  30000 },  // BeamNG.drive（OutGauge 协议）
        { 25,      30000 },  // Live for Speed（OutGauge 协议）
        { 22,      30000 },  // Richard Burns Rally
    };

    /// <summary>获取 SDK 文档定义的默认监听端口；非 UDP 游戏（无默认）返回 0</summary>
    public static ushort GetDefaultListenPort(int gameId)
        => DefaultListenPorts.TryGetValue(gameId, out var port) ? port : (ushort)0;

    /// <summary>加载全部游戏的 UDP 设置（文件不存在或损坏返回空字典）</summary>
    public static Dictionary<int, GameUdpSettings> LoadAll()
    {
        if (!File.Exists(SettingsFilePath))
            return [];

        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<Dictionary<int, GameUdpSettings>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>加载指定游戏的 UDP 设置，无配置返回 null</summary>
    public static GameUdpSettings? Load(int gameId)
    {
        var all = LoadAll();
        return all.TryGetValue(gameId, out var settings) ? settings : null;
    }

    /// <summary>保存指定游戏的 UDP 设置，保留其他游戏的配置</summary>
    public static void Save(int gameId, GameUdpSettings settings)
    {
        var all = LoadAll();
        all[gameId] = settings;
        File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(all, JsonOptions));
    }

    /// <summary>
    /// 将指定游戏的 UDP 设置下发到 SDK（须在 StartTelemetry 之前调用）。
    /// 无保存配置时清除该游戏配置、恢复 SDK 默认。
    /// </summary>
    public static bool Apply(int gameId)
    {
        var settings = Load(gameId);
        if (settings == null)
        {
            // 无配置 → 清除该游戏配置、恢复默认端口
            return TelemetryAPI.SetUDPSettings(gameId, IntPtr.Zero);
        }

        // 启用的转发目标最多取前 8 个（SDK 上限）
        var targets = settings.ForwardTargets
            .Where(t => t.Enabled && !string.IsNullOrWhiteSpace(t.Ip) && t.Port > 0)
            .Take(8)
            .ToList();

        var sdkSettings = new TelemetryAPI.TelemetryUDPSettings
        {
            listenPort = settings.ListenPort,
            forwardCount = (uint)targets.Count,
            forwardTargets = new TelemetryAPI.TelemetryForwardTarget[8]
        };

        for (int i = 0; i < targets.Count; i++)
        {
            sdkSettings.forwardTargets[i] = new TelemetryAPI.TelemetryForwardTarget
            {
                ip = targets[i].Ip,
                port = targets[i].Port
            };
        }

        return TelemetryAPI.SetUDPSettings(gameId, ref sdkSettings);
    }
}
