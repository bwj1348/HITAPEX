using System.Diagnostics;
using System.IO;
using System.Text.Json;
using HITAPEX.Models.Usb;
using HITAPEX.Services.Data.Api;
using HITAPEX.Views.DeviceParameters;

namespace HITAPEX.Services;

/// <summary>
/// 预设服务 —— 管理官方预设和个人预设的加载、保存、导入、导出。
/// 官方预设优先从云端 API 获取并缓存到本地，逐条按 publishedAt 增量更新；
/// 首次运行时若缓存不存在则从安装目录的 official_presets.json 回退加载。
/// 个人预设存储在 %LocalAppData%\HITAPEX\Presets\personal.json，支持按设备类型过滤。
/// </summary>
/// <remarks>
/// 线程安全性：文件写入通过 SemaphoreSlim 加锁，避免并发写入冲突。
/// 缓存文件：%LocalAppData%\HITAPEX\Presets\official_cache.json
///   格式: { "presets": [{ "publishedAt": "...", "preset": {...} }, ...] }
/// 更新策略：云端每条预设携带各自的 publishedAt，与本地缓存逐条对比；
///   云端更新 → 覆盖本条；云端新增 → 加入缓存；云端删除 → 本地同步删除。
/// </remarks>
public class PresetService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _personalDir;
    private readonly string _personalFilePath;
    private readonly string _officialCacheFilePath;
    private readonly string _shippedOfficialFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    private bool _hasCheckedApi;
    private readonly SemaphoreSlim _checkLock = new(1, 1);

    public PresetService()
    {
        var baseDir = AppContext.BaseDirectory;
        _personalDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HITAPEX", "Presets");
        Directory.CreateDirectory(_personalDir);
        _personalFilePath = Path.Combine(_personalDir, "personal.json");
        _officialCacheFilePath = Path.Combine(_personalDir, "official_cache.json");
        _shippedOfficialFilePath = Path.Combine(baseDir, "Assets", "Presets", "official_presets.json");
    }

    /// <summary>
    /// 后台异步检查云端预设并逐条按 publishedAt 增量更新本地缓存。
    /// 内部通过锁保证只执行一次。
    /// </summary>
    public async Task EnsureOfficialPresetsRefreshedAsync()
    {
        if (_hasCheckedApi) return;

        await _checkLock.WaitAsync();
        try
        {
            if (_hasCheckedApi) return;

            var api = new DevicePresetApiService();
            var cloudEntries = await api.FetchPresetsAsync();
            if (cloudEntries == null)
            {
                Debug.WriteLine("[PresetService] API 请求失败，使用本地缓存");
                _hasCheckedApi = true;
                return;
            }

            // 加载本地缓存（必要时回退到安装目录 JSON）
            var localEntries = LoadCacheEntries();

            // 按预设名称构建本地索引（名称是唯一标识）
            var localByName = new Dictionary<string, DevicePresetEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in localEntries)
                localByName[e.Preset.Name] = e;

            // 收集云端返回的所有预设名称，用于后续识别已删除的条目
            var cloudNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            int updated = 0, added = 0;
            foreach (var cloud in cloudEntries)
            {
                if (string.IsNullOrEmpty(cloud.PublishedAt)) continue;

                cloudNames.Add(cloud.Preset.Name);

                if (localByName.TryGetValue(cloud.Preset.Name, out var local))
                {
                    // 更新：本地已存在，且云端 publishedAt 更新 → 覆盖
                    if (string.Compare(cloud.PublishedAt, local.PublishedAt, StringComparison.Ordinal) > 0)
                    {
                        local.PublishedAt = cloud.PublishedAt;
                        local.Preset = cloud.Preset;
                        updated++;
                    }
                }
                else
                {
                    // 新增：本地不存在 → 加入
                    localByName[cloud.Preset.Name] = cloud;
                    added++;
                }
            }

            // 删除：本地有但云端没有 → 同步移除
            int removed = 0;
            foreach (var name in localByName.Keys.ToList())
            {
                if (!cloudNames.Contains(name))
                {
                    localByName.Remove(name);
                    removed++;
                }
            }

            if (updated > 0 || added > 0 || removed > 0)
            {
                Debug.WriteLine($"[PresetService] 云端更新: {updated} 条, 新增: {added} 条, 删除: {removed} 条");
                WriteCacheEntries(localByName.Values.ToList());
            }
            else
            {
                Debug.WriteLine("[PresetService] 云端无变化");
            }

            _hasCheckedApi = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PresetService] 刷新官方预设异常: {ex.Message}");
            _hasCheckedApi = true;
        }
        finally
        {
            _checkLock.Release();
        }
    }

    /// <summary>从缓存或安装目录 JSON 加载预设条目列表</summary>
    private List<DevicePresetEntry> LoadCacheEntries()
    {
        try
        {
            // 优先从缓存加载
            var path = File.Exists(_officialCacheFilePath)
                ? _officialCacheFilePath
                : _shippedOfficialFilePath;

            if (!File.Exists(path))
                return [];

            var json = File.ReadAllText(path);

            if (path == _officialCacheFilePath)
            {
                // 缓存文件格式
                var cache = JsonSerializer.Deserialize<OfficialCacheFile>(json, JsonOptions);
                return cache?.Presets ?? [];
            }
            else
            {
                // 安装目录的旧格式（纯 PresetItem 数组，无 publishedAt）
                var presets = JsonSerializer.Deserialize<List<PresetItem>>(json, JsonOptions);
                if (presets == null) return [];
                return presets.Select(p => new DevicePresetEntry { Preset = p }).ToList();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PresetService] 加载缓存失败: {ex.Message}");
            return [];
        }
    }

    /// <summary>将预设条目列表写入缓存文件</summary>
    private void WriteCacheEntries(List<DevicePresetEntry> entries)
    {
        _fileLock.Wait();
        try
        {
            var cache = new OfficialCacheFile { Presets = entries };
            var json = JsonSerializer.Serialize(cache, JsonOptions);
            File.WriteAllText(_officialCacheFilePath, json);
            Debug.WriteLine($"[PresetService] 缓存已写入: {_officialCacheFilePath} ({entries.Count} 条)");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PresetService] 写入缓存失败: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>加载官方预设（纯 PresetItem，不包含 publishedAt）</summary>
    public List<PresetItem> LoadOfficialPresets()
    {
        return LoadOfficialPresets(null);
    }

    /// <summary>加载官方预设，按设备类型过滤</summary>
    public List<PresetItem> LoadOfficialPresets(DeviceType? deviceType)
    {
        try
        {
            var entries = LoadCacheEntries();
            var presets = entries.Select(e => e.Preset).ToList();

            foreach (var p in presets)
                p.IsPersonal = false;

            if (deviceType.HasValue)
                presets = presets.Where(p => p.DeviceType == deviceType.Value).ToList();

            return presets;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PresetService] 加载官方预设失败: {ex.Message}");
        }
        return [];
    }

    // ══════════════════════════════════════════
    //  个人预设（不变）
    // ══════════════════════════════════════════

    public List<PresetItem> LoadPersonalPresets()
    {
        return LoadPersonalPresets(null);
    }

    public List<PresetItem> LoadPersonalPresets(DeviceType? deviceType)
    {
        try
        {
            if (!File.Exists(_personalFilePath))
                return [];

            var json = File.ReadAllText(_personalFilePath);
            var presets = JsonSerializer.Deserialize<List<PresetItem>>(json, JsonOptions);
            if (presets != null)
            {
                foreach (var p in presets)
                    p.IsPersonal = true;
                if (deviceType.HasValue)
                    presets = presets.Where(p => p.DeviceType == deviceType.Value).ToList();
                return presets;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PresetService] 加载个人预设失败: {ex.Message}");
        }
        return [];
    }

    public void SavePersonalPresets(List<PresetItem> presets, DeviceType deviceType)
    {
        _fileLock.Wait();
        try
        {
            var allPresets = LoadPersonalPresetsUnlocked();
            allPresets.RemoveAll(p => p.DeviceType == deviceType);
            allPresets.AddRange(presets);
            var json = JsonSerializer.Serialize(allPresets, JsonOptions);
            File.WriteAllText(_personalFilePath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PresetService] 保存个人预设失败: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public void SavePersonalPresets(List<PresetItem> presets)
    {
        _fileLock.Wait();
        try
        {
            var json = JsonSerializer.Serialize(presets, JsonOptions);
            File.WriteAllText(_personalFilePath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PresetService] 保存个人预设失败: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private List<PresetItem> LoadPersonalPresetsUnlocked()
    {
        try
        {
            if (!File.Exists(_personalFilePath))
                return [];

            var json = File.ReadAllText(_personalFilePath);
            var presets = JsonSerializer.Deserialize<List<PresetItem>>(json, JsonOptions);
            if (presets != null)
            {
                foreach (var p in presets)
                    p.IsPersonal = true;
                return presets;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PresetService] 加载个人预设失败: {ex.Message}");
        }
        return [];
    }

    public void ExportPreset(PresetItem preset, string filePath)
    {
        var exportItem = new PresetItem
        {
            Name = preset.Name,
            Games = preset.Games,
            IsPersonal = true,
            DeviceType = preset.DeviceType,
            PedalParameters = preset.PedalParameters,
            WheelParameters = preset.WheelParameters,
            BaseParameters = preset.BaseParameters
        };

        var json = JsonSerializer.Serialize(exportItem, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// 从文件导入预设。返回 null 表示导入失败（文件无效或参数不完整）。
    /// 校验规则：
    ///   1. JSON 反序列化成功
    ///   2. DeviceType 在枚举范围内
    ///   3. 对应设备类型的参数快照存在且字段完整
    ///   4. 参数值在合法范围内
    /// </summary>
    public PresetItem? ImportPreset(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var preset = JsonSerializer.Deserialize<PresetItem>(json, JsonOptions);
            if (preset == null) return null;

            // 1. 校验 DeviceType 有效性
            if (!Enum.IsDefined(typeof(DeviceType), preset.DeviceType))
            {
                Debug.WriteLine($"[PresetService] 导入失败: DeviceType 无效 ({preset.DeviceType})");
                return null;
            }

            // 2. 校验参数快照存在性
            var (paramKey, snapshot, expectedFields) = preset.DeviceType switch
            {
                DeviceType.Base => ("BaseParameters", (object?)preset.BaseParameters, BaseRequiredFields),
                DeviceType.Pedal => ("PedalParameters", (object?)preset.PedalParameters, PedalRequiredFields),
                DeviceType.Wheel => ("WheelParameters", (object?)preset.WheelParameters, WheelRequiredFields),
                _ => (null, null, null!)
            };

            if (paramKey == null || snapshot == null)
            {
                Debug.WriteLine($"[PresetService] 导入失败: 缺少 {paramKey ?? "<未知>"} (DeviceType={preset.DeviceType})");
                return null;
            }

            // 3. 校验参数快照字段完整性：必须逐个包含所有预期字段
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(paramKey, out var paramElement) ||
                paramElement.ValueKind != JsonValueKind.Object)
            {
                Debug.WriteLine($"[PresetService] 导入失败: {paramKey} 不是有效的 JSON 对象");
                return null;
            }

            var actualFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in paramElement.EnumerateObject())
                actualFields.Add(prop.Name);

            var missingFields = new List<string>();
            foreach (var expected in expectedFields)
            {
                if (!actualFields.Contains(expected))
                    missingFields.Add(expected);
            }

            if (missingFields.Count > 0)
            {
                Debug.WriteLine($"[PresetService] 导入失败: {paramKey} 缺少字段 ({missingFields.Count}): {string.Join(", ", missingFields)}");
                return null;
            }

            // 额外检查：JSON 中是否有未识别的字段（拼写错误等）
            var unknownFields = actualFields
                .Where(f => !expectedFields.Contains(f))
                .ToList();
            if (unknownFields.Count > 0)
            {
                Debug.WriteLine($"[PresetService] 导入失败: {paramKey} 包含未知字段 ({unknownFields.Count}): {string.Join(", ", unknownFields)}");
                return null;
            }

            // 4. 校验参数值合法性
            var validationErrors = preset.DeviceType switch
            {
                DeviceType.Base => preset.BaseParameters!.Validate(),
                DeviceType.Pedal => preset.PedalParameters!.Validate(),
                DeviceType.Wheel => preset.WheelParameters!.Validate(),
                _ => null
            };

            if (validationErrors is { Count: > 0 })
            {
                Debug.WriteLine($"[PresetService] 导入失败: 参数值不合法 ({preset.DeviceType}, {validationErrors.Count} 处)");
                foreach (var err in validationErrors)
                    Debug.WriteLine($"  - {err}");
                return null;
            }

            preset.IsPersonal = true;
            return preset;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PresetService] 导入预设失败: {ex.Message}");
            return null;
        }
    }

    // ══════════════════════════════════════════
    //  导入校验 — 各设备类型参数快照的必需字段清单
    // ══════════════════════════════════════════

    private static readonly HashSet<string> BaseRequiredFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "maxSteeringAngle", "limitRigidity", "maxSpeed", "smoothLevel",
        "forceStrength", "mechInertia", "mechCentering", "mechDamping",
        "mechFriction", "gameInertia", "gameElastic", "gameDamping",
        "gameFriction", "gameInertiaStr", "handsOffProtect", "forceReverse"
    };

    private static readonly HashSet<string> PedalRequiredFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "clutchCurveType", "clutchDirection",
        "clutchPoint1Y", "clutchPoint1X", "clutchPoint2Y", "clutchPoint2X",
        "clutchPoint3Y", "clutchPoint3X", "clutchPoint4Y", "clutchPoint4X",
        "clutchDeadZoneFront", "clutchDeadZoneRear",
        "brakeCurveType", "brakeDirection",
        "brakePoint1Y", "brakePoint1X", "brakePoint2Y", "brakePoint2X",
        "brakePoint3Y", "brakePoint3X", "brakePoint4Y", "brakePoint4X",
        "brakeDeadZoneFront", "brakeDeadZoneRear",
        "throttleCurveType", "throttleDirection",
        "throttlePoint1Y", "throttlePoint1X", "throttlePoint2Y", "throttlePoint2X",
        "throttlePoint3Y", "throttlePoint3X", "throttlePoint4Y", "throttlePoint4X",
        "throttleDeadZoneFront", "throttleDeadZoneRear"
    };

    private static readonly HashSet<string> WheelRequiredFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "keyColorEnabled", "globalKeyColor", "showKeyNumber", "keyBrightness", "rpmBrightness",
        "sleepLightDuration", "standbyLightEffect", "globalFlashSpeed",
        "buttonColors", "buttonTelemetryEnabled", "buttonTelemetryLightEffect",
        "buttonTelemetryFunc", "buttonTelemetryTriggerColor", "buttonSpeeds",
        "rpmColors", "rpmValues", "rpmCapValue", "rpmCurveType",
        "rpmDisplayMode", "rpmLightMode", "rpmStrobeMode", "rpmStrobeColor",
        "rpmSpeed", "rpmBaseLightMode", "rpmBaseLightSpeed", "rpmTelemetryEnabled",
        "clutchMode", "clutchPointValue"
    };
}
