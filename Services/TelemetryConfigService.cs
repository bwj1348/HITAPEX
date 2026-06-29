using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Xml.Linq;
using HITAPEX.Models;

namespace HITAPEX.Services;

/// <summary>
/// 遥测配置服务：根据游戏 ID 执行对应的遥测配置文件部署操作。
/// </summary>
public static class TelemetryConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    /// <summary>
    /// 为指定游戏应用遥测配置。成功返回 true。
    /// </summary>
    public static bool ApplyConfig(GameItem game)
    {
        return game.Id switch
        {
            25      => ApplyLfsConfig(game),              // Live for Speed
            365960  => ApplyRFactor2Config(game),         // rFactor 2
            2399420 => ApplyLmuConfig(game),              // Le Mans Ultimate
            421020  => ApplyDiRT4Config(game),            // DiRT 4
            690790  => ApplyDiRTRally2Config(game),       // DiRT Rally 2.0
            227300  => ApplyScsConfig(game),              // Euro Truck Simulator 2
            270880  => ApplyScsConfig(game),              // American Truck Simulator
            1692250 => ApplyF1Config(game, 22),            // F1 22
            2108330 => ApplyF1Config(game, 23),            // F1 23
            2488620 => ApplyF1Config(game, 24),            // F1 24
            3059520 => ApplyF1Config(game, 25),            // F1 25
            1953520 => ApplyWrcgConfig(),                 // WRC Generations
            1849250 => ApplyEaWrcConfig(),                // EA Sports WRC
            1004750 => ApplyWrcConfig(game),              // WRC 8
            1267540 => ApplyWrcConfig(game),              // WRC 9
            1462810 => ApplyWrcConfig(game),              // WRC 10
            _ => false
        };
    }

    /// <summary>
    /// 通用的源文件复制逻辑。
    /// </summary>
    private static bool CopyConfigFile(string srcRelativePath, string destPath)
    {
        var srcPath = Path.Combine(AppContext.BaseDirectory, "Assets", "TelemetryConfigs", srcRelativePath);
        if (!File.Exists(srcPath))
        {
            Debug.WriteLine($"[TelemetryConfig] 源配置文件不存在: {srcPath}");
            return false;
        }

        try
        {
            var destDir = Path.GetDirectoryName(destPath);
            if (destDir != null)
                Directory.CreateDirectory(destDir);

            File.Copy(srcPath, destPath, overwrite: true);
            Debug.WriteLine($"[TelemetryConfig] 配置文件已部署到: {destPath}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TelemetryConfig] 配置文件部署失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 根据当前启动方式获取游戏根目录。
    /// CustomPath 模式 → LaunchPath 所在目录；Steam 模式 → Steam 安装目录。
    /// </summary>
    private static string? ResolveGameRoot(GameItem game)
    {
        if (game.LaunchMode == LaunchModeUdf.CustomPath)
        {
            // 自定义路径启动 → 取其所在目录
            if (!string.IsNullOrWhiteSpace(game.LaunchPath) && File.Exists(game.LaunchPath))
                return Path.GetDirectoryName(game.LaunchPath);

            return null;
        }

        // Steam 启动 → Steam 安装目录
        if (!string.IsNullOrWhiteSpace(game.SteamId) && game.SteamId != "22" && game.SteamId != "25")
        {
            var steamInstall = new SteamInstallService();
            var info = steamInstall.CheckInstalled([game.SteamId]);
            if (info.TryGetValue(game.SteamId, out var si) && si.IsInstalled && si.InstallDir != null)
                return si.InstallDir;
        }

        return null;
    }

    // ════════════════════════════════════════════════════════════════
    //  LFS
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// LFS — 将 cfg.txt 复制到游戏根目录，覆盖或新增。
    /// </summary>
    private static bool ApplyLfsConfig(GameItem game)
    {
        var gameRoot = ResolveGameRoot(game);
        if (gameRoot == null)
        {
            Debug.WriteLine("[TelemetryConfig] LFS 游戏根目录未找到，请先设置自定义启动路径或通过 Steam 安装");
            return false;
        }

        return CopyConfigFile("LFS/cfg.txt", Path.Combine(gameRoot, "cfg.txt"));
    }

    // ════════════════════════════════════════════════════════════════
    //  rFactor 2 / LMU
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// rFactor 2 — 将 rFactor2SharedMemoryMapPlugin64.dll 复制到 Plugins\。
    /// Steam 模式 → [rF2]\Bin64\Plugins\；CustomPath 模式 → exe 同级 Plugins\。
    /// </summary>
    private static bool ApplyRFactor2Config(GameItem game)
    {
        if (game.LaunchMode == LaunchModeUdf.CustomPath)
        {
            if (string.IsNullOrWhiteSpace(game.LaunchPath) || !File.Exists(game.LaunchPath))
            {
                Debug.WriteLine("[TelemetryConfig] rF2 自定义路径无效，请先设置启动路径");
                return false;
            }

            var exeDir = Path.GetDirectoryName(game.LaunchPath)!;
            return CopyConfigFile("rF2lmu/rFactor2SharedMemoryMapPlugin64.dll",
                Path.Combine(exeDir, "Plugins", "rFactor2SharedMemoryMapPlugin64.dll"));
        }

        var gameRoot = ResolveGameRoot(game);
        if (gameRoot == null)
        {
            Debug.WriteLine("[TelemetryConfig] rFactor 2 Steam 游戏根目录未找到");
            return false;
        }

        return CopyConfigFile("rF2lmu/rFactor2SharedMemoryMapPlugin64.dll",
            Path.Combine(gameRoot, "Bin64", "Plugins", "rFactor2SharedMemoryMapPlugin64.dll"));
    }

    /// <summary>
    /// LMU — 将 rFactor2SharedMemoryMapPlugin64.dll 复制到 Plugins\，并更新 CustomPluginVariables.JSON。
    /// Steam 模式 → [LMU]\Plugins\；CustomPath 模式 → exe 同级 Plugins\。
    /// </summary>
    private static bool ApplyLmuConfig(GameItem game)
    {
        string dllDestPath;

        if (game.LaunchMode == LaunchModeUdf.CustomPath)
        {
            if (string.IsNullOrWhiteSpace(game.LaunchPath) || !File.Exists(game.LaunchPath))
            {
                Debug.WriteLine("[TelemetryConfig] LMU 自定义路径无效，请先设置启动路径");
                return false;
            }

            var exeDir = Path.GetDirectoryName(game.LaunchPath)!;
            dllDestPath = Path.Combine(exeDir, "Plugins", "rFactor2SharedMemoryMapPlugin64.dll");

            if (!CopyConfigFile("rF2lmu/rFactor2SharedMemoryMapPlugin64.dll", dllDestPath))
                return false;

            return ApplyLmuPluginJson(exeDir);
        }

        var gameRoot = ResolveGameRoot(game);
        if (gameRoot == null)
        {
            Debug.WriteLine("[TelemetryConfig] LMU Steam 游戏根目录未找到");
            return false;
        }

        dllDestPath = Path.Combine(gameRoot, "Plugins", "rFactor2SharedMemoryMapPlugin64.dll");
        if (!CopyConfigFile("rF2lmu/rFactor2SharedMemoryMapPlugin64.dll", dllDestPath))
            return false;

        return ApplyLmuPluginJson(gameRoot);
    }

    /// <summary>
    /// LMU — 在 UserData\player\CustomPluginVariables.JSON 中确保插件已启用。
    /// </summary>
    private static bool ApplyLmuPluginJson(string gameRoot)
    {
        var jsonPath = Path.Combine(gameRoot, "UserData", "player", "CustomPluginVariables.JSON");
        const string pluginKey = "rFactor2SharedMemoryMapPlugin64.dll";

        try
        {
            var dir = Path.GetDirectoryName(jsonPath);
            if (dir != null)
                Directory.CreateDirectory(dir);

            // 读取已有 JSON，不存在则创建空对象
            var jsonObj = File.Exists(jsonPath)
                ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(jsonPath)) ?? []
                : [];

            // 插件配置模板
            var defaultConfig = new Dictionary<string, object>
            {
                [" Enabled"] = 1,
                ["DebugISIInternals"] = 0,
                ["DebugOutputLevel"] = 0,
                ["DebugOutputSource"] = 1,
                ["DedicatedServerMapGlobally"] = 0,
                ["EnableDirectMemoryAccess"] = 0,
                ["EnableHWControlInput"] = 1,
                ["EnableRulesControlInput"] = 0,
                ["EnableWeatherControlInput"] = 0,
                ["UnsubscribedBuffersMask"] = 160
            };

            // 合并：已有条目则保留已有值但确保 Enabled 为 1，没有则写入默认
            if (jsonObj.TryGetValue(pluginKey, out var existing) &&
                existing.ValueKind == JsonValueKind.Object)
            {
                // 检查 " Enabled" 是否为 1，不是则设为 1
                var existingCfg = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(existing.GetRawText());
                if (existingCfg != null)
                {
                    if (existingCfg.TryGetValue(" Enabled", out var enabledVal) &&
                        enabledVal.ValueKind == JsonValueKind.Number &&
                        enabledVal.GetInt32() != 1)
                    {
                        existingCfg[" Enabled"] = JsonSerializer.SerializeToElement(1);
                    }

                    jsonObj[pluginKey] = JsonSerializer.SerializeToElement(existingCfg);
                }
            }
            else
            {
                jsonObj[pluginKey] = JsonSerializer.SerializeToElement(defaultConfig);
            }

            // 写回文件
            var json = JsonSerializer.Serialize(jsonObj, JsonOptions);
            File.WriteAllText(jsonPath, json);

            Debug.WriteLine($"[TelemetryConfig] LMU CustomPluginVariables.JSON 已更新: {jsonPath}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TelemetryConfig] LMU JSON 配置写入失败: {ex.Message}");
            return false;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  SCS 系列 (ETS2 / ATS)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// ETS2 / ATS — 将 cwxyAETS2Telemetry.dll 复制到 Plugins\。
    /// Steam 模式 → {root}\bin\win_x64\plugins\；CustomPath 模式 → exe 同级 Plugins\。
    /// </summary>
    private static bool ApplyScsConfig(GameItem game)
    {
        if (game.LaunchMode == LaunchModeUdf.CustomPath)
        {
            if (string.IsNullOrWhiteSpace(game.LaunchPath) || !File.Exists(game.LaunchPath))
            {
                Debug.WriteLine("[TelemetryConfig] SCS 自定义路径无效，请先设置启动路径");
                return false;
            }

            var exeDir = Path.GetDirectoryName(game.LaunchPath)!;
            return CopyConfigFile("ETS2ATS/cwxyAETS2Telemetry.dll",
                Path.Combine(exeDir, "Plugins", "cwxyAETS2Telemetry.dll"));
        }

        var gameRoot = ResolveGameRoot(game);
        if (gameRoot == null)
        {
            Debug.WriteLine("[TelemetryConfig] SCS Steam 游戏根目录未找到");
            return false;
        }

        return CopyConfigFile("ETS2ATS/cwxyAETS2Telemetry.dll",
            Path.Combine(gameRoot, "bin", "win_x64", "plugins", "cwxyAETS2Telemetry.dll"));
    }

    // ════════════════════════════════════════════════════════════════
    //  F1 系列
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// F1 系列 — 在 Documents\My Games\F1 {year}\hardwaresettings\hardware_settings_config.xml 中，
    /// 将 motion/udp 的 enabled 属性设为 true。文件不存在则返回 false。
    /// </summary>
    private static bool ApplyF1Config(GameItem game, int year)
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var configPath = Path.Combine(documentsPath, "My Games", $"F1 {year}", "hardwaresettings", "hardware_settings_config.xml");

        if (!File.Exists(configPath))
        {
            Debug.WriteLine($"[TelemetryConfig] F1 {year} 配置文件不存在: {configPath}");
            return false;
        }

        try
        {
            var doc = XDocument.Load(configPath);
            var motion = doc.Root?.Element("motion");
            if (motion == null)
            {
                Debug.WriteLine($"[TelemetryConfig] F1 {year} 未找到 <motion> 节点");
                return false;
            }

            var udp = motion.Element("udp");
            if (udp == null)
            {
                Debug.WriteLine($"[TelemetryConfig] F1 {year} 未找到 <motion>/<udp> 节点");
                return false;
            }

            var enabled = udp.Attribute("enabled");
            if (enabled == null)
            {
                udp.SetAttributeValue("enabled", "true");
            }
            else if (enabled.Value != "true")
            {
                enabled.Value = "true";
            }
            else
            {
                Debug.WriteLine($"[TelemetryConfig] F1 {year} UDP 遥测已启用");
                return true;
            }

            doc.Save(configPath);
            Debug.WriteLine($"[TelemetryConfig] F1 {year} UDP 遥测已启用: {configPath}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TelemetryConfig] F1 {year} 配置修改失败: {ex.Message}");
            return false;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  WRCG
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// WRC Generations — 在 Documents\My Games\WRCG\UserSettings.cfg 中设置 UDP 遥测参数。
    /// 文件不存在则返回 false；已存在的参数更新值，不存在则追加。
    /// </summary>
    private static bool ApplyWrcgConfig()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var configPath = Path.Combine(documentsPath, "My Games", "WRCG", "UserSettings.cfg");

        if (!File.Exists(configPath))
        {
            Debug.WriteLine($"[TelemetryConfig] WRCG 配置文件不存在: {configPath}");
            return false;
        }

        var settings = new Dictionary<string, string>
        {
            ["WRC.Telemetry.EnableTelemetry"] = "true",
            ["WRC.Telemetry.TelemetryAdress"] = "\"127.0.1.1\"",
            ["WRC.Telemetry.TelemetryPort"] = "20777",
            ["WRC.Telemetry.TelemetryRate"] = "60",
        };

        try
        {
            var lines = File.ReadAllLines(configPath).ToList();
            var keysFound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < lines.Count; i++)
            {
                foreach (var kv in settings)
                {
                    if (lines[i].TrimStart().StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = $"{kv.Key} = {kv.Value};";
                        keysFound.Add(kv.Key);
                        break;
                    }
                }
            }

            foreach (var kv in settings)
            {
                if (!keysFound.Contains(kv.Key))
                    lines.Add($"{kv.Key} = {kv.Value};");
            }

            File.WriteAllLines(configPath, lines);
            Debug.WriteLine($"[TelemetryConfig] WRCG UDP 遥测配置已更新: {configPath}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TelemetryConfig] WRCG 配置修改失败: {ex.Message}");
            return false;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  WRC 8 / 9 / 10
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// WRC 8/9/10 — 将 WrcInjectionPayload.dll 复制到游戏根目录并执行 DLL 替换。
    /// </summary>
    private static bool ApplyWrcConfig(GameItem game)
    {
        var gameRoot = ResolveGameRoot(game);
        if (gameRoot == null)
        {
            Debug.WriteLine("[TelemetryConfig] WRC 游戏根目录未找到，请先设置自定义启动路径或通过 Steam 安装");
            return false;
        }

        var originalDll = Path.Combine(gameRoot, "PhysXCooking64_s.dll");
        var backupDll = Path.Combine(gameRoot, "PhysXCooking64_s_org.dll");
        var injectionDll = Path.Combine(gameRoot, "WrcInjectionPayload.dll");

        try
        {
            // 1. 检查原始 DLL 存在
            if (!File.Exists(originalDll))
            {
                Debug.WriteLine($"[TelemetryConfig] WRC PhysXCooking64_s.dll 未找到: {originalDll}");
                return false;
            }

            // 2. 复制插件到游戏根目录
            if (!CopyConfigFile("wrc8910/WrcInjectionPayload.dll", injectionDll))
                return false;

            // 3. 如果已有备份 → 先还原（移除之前的补丁）
            if (File.Exists(backupDll))
            {
                Debug.WriteLine("[TelemetryConfig] 检测到之前的补丁，正在还原");
                File.Copy(backupDll, originalDll, overwrite: true);
            }

            // 4. 备份原始 DLL
            File.Copy(originalDll, backupDll, overwrite: true);

            // 5. 替换为注入 DLL
            File.Copy(injectionDll, originalDll, overwrite: true);

            Debug.WriteLine($"[TelemetryConfig] WRC 遥测补丁安装成功: {gameRoot}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TelemetryConfig] WRC 遥测补丁安装失败: {ex.Message}");
            return false;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  EA WRC
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// EA WRC — 部署遥测配置文件到 Documents\My Games\WRC\telemetry\。
    /// </summary>
    private static bool ApplyEaWrcConfig()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var telemetryDir = Path.Combine(documentsPath, "My Games", "WRC", "telemetry");
        var configPath = Path.Combine(telemetryDir, "config.json");
        var udpDir = Path.Combine(telemetryDir, "udp");

        try
        {
            // 1. config.json 不存在 → 从内置模板复制整个文件
            if (!File.Exists(configPath))
            {
                Directory.CreateDirectory(telemetryDir);
                if (!CopyConfigFile("eawrc/config.json", configPath))
                    return false;
            }

            // 2. udp 目录处理
            if (!Directory.Exists(udpDir))
            {
                // udp 目录不存在 → 创建目录，并复制 custom1.json 和 wrc_cwyx.json
                Directory.CreateDirectory(udpDir);
                if (!CopyConfigFile("eawrc/custom1.json", Path.Combine(udpDir, "custom1.json")))
                    return false;
                if (!CopyConfigFile("eawrc/wrc_cwyx.json", Path.Combine(udpDir, "wrc_cwyx.json")))
                    return false;
            }
            else
            {
                // udp 目录已存在 → 补缺
                if (!File.Exists(Path.Combine(udpDir, "custom1.json")))
                {
                    if (!CopyConfigFile("eawrc/custom1.json", Path.Combine(udpDir, "custom1.json")))
                        return false;
                }
                if (!CopyConfigFile("eawrc/wrc_cwyx.json", Path.Combine(udpDir, "wrc_cwyx.json")))
                    return false;
            }

            // 3. 在 config.json 的 udp.packets 中注入 wrc_cwyx 条目
            if (File.Exists(configPath))
                return InjectEaWrcPackets(configPath);

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TelemetryConfig] EA WRC 配置部署失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 在 EA WRC 的 config.json 中确保 udp.packets 包含所有 wrc_cwyx 条目。
    /// 使用文本级别插入，不重新序列化，保持原文件格式不变。
    /// </summary>
    private static bool InjectEaWrcPackets(string configPath)
    {
        try
        {
            var lines = File.ReadAllLines(configPath).ToList();

            // 找到 "packets": [ 所在行
            int packetsOpenLine = -1;
            int indent = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                var t = lines[i].Trim();
                if (t == "\"packets\": [")
                {
                    packetsOpenLine = i;
                    indent = lines[i].IndexOf('"');
                    break;
                }
            }
            if (packetsOpenLine < 0)
            {
                Debug.WriteLine("[TelemetryConfig] EA WRC 未找到 \"packets\" 数组");
                return false;
            }

            // 找到 packets 数组的闭合 ]（与 "packets" 同级的 ]）
            int packetsCloseLine = -1;
            int depth = 0;
            for (int i = packetsOpenLine; i < lines.Count; i++)
            {
                foreach (char c in lines[i])
                {
                    if (c == '[') depth++;
                    else if (c == ']') depth--;
                }
                if (depth == 0 && i != packetsOpenLine)
                {
                    packetsCloseLine = i;
                    break;
                }
            }
            if (packetsCloseLine < 0)
            {
                Debug.WriteLine("[TelemetryConfig] EA WRC 未找到 \"packets\" 数组闭合括号");
                return false;
            }

            // 检查是否已有 wrc_cwyx 条目（简单字符串匹配）
            var existingText = string.Join("\n", lines);
            if (existingText.Contains("\"structure\": \"wrc_cwyx\""))
            {
                Debug.WriteLine("[TelemetryConfig] EA WRC wrc_cwyx packets 已存在，无需修改");
                return true;
            }

            // 确保前一个条目以逗号结尾（如果数组中已有条目）
            // 找到 ] 前面第一个非空行
            int lastContentLine = packetsCloseLine - 1;
            while (lastContentLine > packetsOpenLine && string.IsNullOrWhiteSpace(lines[lastContentLine]))
                lastContentLine--;

            // 确保前一个条目结尾有逗号
            if (lastContentLine > packetsOpenLine)
            {
                var trimmed = lines[lastContentLine].TrimEnd();
                if (!trimmed.EndsWith(',') && !trimmed.EndsWith('{'))
                {
                    // 行可能以 } 结尾，检查之后是否有逗号
                    if (trimmed.EndsWith('}') || trimmed.EndsWith('"'))
                    {
                        // 找最后一个非空白字符的位置
                        var lastCharIdx = lines[lastContentLine].LastIndexOfAny(['}', '"', ']']);
                        if (lastCharIdx >= 0 && lines[lastContentLine].Length > lastCharIdx + 1)
                        {
                            var after = lines[lastContentLine].Substring(lastCharIdx + 1).TrimEnd();
                            if (!after.EndsWith(','))
                                lines[lastContentLine] = lines[lastContentLine].TrimEnd() + ",";
                        }
                        else if (lastCharIdx >= 0)
                        {
                            lines[lastContentLine] = lines[lastContentLine].TrimEnd() + ",";
                        }
                    }
                }
            }

            var prefix = new string('\t', indent / 4 + 3);
            var newEntries = new[]
            {
                $"{prefix}{{",
                $"{prefix}\t\"structure\": \"wrc_cwyx\",",
                $"{prefix}\t\"packet\": \"session_start\",",
                $"{prefix}\t\"ip\": \"127.0.0.1\",",
                $"{prefix}\t\"port\": 26666,",
                $"{prefix}\t\"frequencyHz\": 0,",
                $"{prefix}\t\"bEnabled\": false",
                $"{prefix}}},",
                $"{prefix}{{",
                $"{prefix}\t\"structure\": \"wrc_cwyx\",",
                $"{prefix}\t\"packet\": \"session_update\",",
                $"{prefix}\t\"ip\": \"127.0.0.1\",",
                $"{prefix}\t\"port\": 26666,",
                $"{prefix}\t\"frequencyHz\": -1,",
                $"{prefix}\t\"bEnabled\": true",
                $"{prefix}}},",
                $"{prefix}{{",
                $"{prefix}\t\"structure\": \"wrc_cwyx\",",
                $"{prefix}\t\"packet\": \"session_end\",",
                $"{prefix}\t\"ip\": \"127.0.0.1\",",
                $"{prefix}\t\"port\": 26666,",
                $"{prefix}\t\"frequencyHz\": 0,",
                $"{prefix}\t\"bEnabled\": false",
                $"{prefix}}},",
                $"{prefix}{{",
                $"{prefix}\t\"structure\": \"wrc_cwyx\",",
                $"{prefix}\t\"packet\": \"session_pause\",",
                $"{prefix}\t\"ip\": \"127.0.0.1\",",
                $"{prefix}\t\"port\": 26666,",
                $"{prefix}\t\"frequencyHz\": 0,",
                $"{prefix}\t\"bEnabled\": false",
                $"{prefix}}},",
                $"{prefix}{{",
                $"{prefix}\t\"structure\": \"wrc_cwyx\",",
                $"{prefix}\t\"packet\": \"session_resume\",",
                $"{prefix}\t\"ip\": \"127.0.0.1\",",
                $"{prefix}\t\"port\": 26666,",
                $"{prefix}\t\"frequencyHz\": 0,",
                $"{prefix}\t\"bEnabled\": false",
                $"{prefix}}}",
            };

            lines.InsertRange(packetsCloseLine, newEntries);
            File.WriteAllLines(configPath, lines);
            Debug.WriteLine("[TelemetryConfig] EA WRC wrc_cwyx packets 已注入");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TelemetryConfig] EA WRC packets 注入失败: {ex.Message}");
            return false;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  DiRT 系列
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// DiRT 4 — 将 hardware_settings_config.xml 复制到 Documents\My Games\DiRT 4\hardwaresettings\。
    /// </summary>
    private static bool ApplyDiRT4Config(GameItem game)
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var destDir = Path.Combine(documentsPath, "My Games", "DiRT 4", "hardwaresettings");
        var destPath = Path.Combine(destDir, "hardware_settings_config.xml");

        return CopyConfigFile("DiRT4/hardware_settings_config.xml", destPath);
    }

    /// <summary>
    /// DiRT Rally 2.0 — 将 hardware_settings_config.xml 复制到 Documents\My Games\DiRT Rally 2.0\hardwaresettings\。
    /// </summary>
    private static bool ApplyDiRTRally2Config(GameItem game)
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var destDir = Path.Combine(documentsPath, "My Games", "DiRT Rally 2.0", "hardwaresettings");
        var destPath = Path.Combine(destDir, "hardware_settings_config.xml");

        return CopyConfigFile("DiRTRally2/hardware_settings_config.xml", destPath);
    }
}
