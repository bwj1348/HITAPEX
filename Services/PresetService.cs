using System.IO;
using System.Text.Json;
using HITAPEX.Models.Usb;
using HITAPEX.Views.DeviceParameters;

namespace HITAPEX.Services;

/// <summary>
/// 预设服务 —— 管理官方预设和个人预设的加载、保存、导入、导出。
/// 官方预设从安装目录的 JSON 文件加载（只读）；个人预设存储在
/// %LocalAppData%\HITAPEX\Presets\personal.json，支持按设备类型过滤。
/// </summary>
/// <remarks>
/// 线程安全性：文件写入通过 SemaphoreSlim 加锁，避免并发写入冲突。
/// 预设分为两种来源：Official（官方，IsPersonal=false）和 Personal（用户自定义，IsPersonal=true）。
/// </remarks>
public class PresetService
{
    /// <summary>JSON 序列化选项：缩进格式化 + 属性名大小写不敏感</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _personalDir;
    private readonly string _personalFilePath;
    private readonly string _officialFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    /// <summary>
    /// 初始化预设服务。确定官方预设和个人预设的文件路径。
    /// 官方预设：{AppBase}\Assets\Presets\official_presets.json
    /// 个人预设：%LocalAppData%\HITAPEX\Presets\personal.json
    /// </summary>
    public PresetService()
    {
        var baseDir = AppContext.BaseDirectory;
        _personalDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HITAPEX", "Presets");
        Directory.CreateDirectory(_personalDir);
        _personalFilePath = Path.Combine(_personalDir, "personal.json");
        _officialFilePath = Path.Combine(baseDir, "Assets", "Presets", "official_presets.json");
    }

    /// <summary>加载官方预设（从安装目录 JSON 文件）</summary>
    public List<PresetItem> LoadOfficialPresets()
    {
        return LoadOfficialPresets(null);
    }

    /// <summary>加载官方预设，按设备类型过滤</summary>
    public List<PresetItem> LoadOfficialPresets(DeviceType? deviceType)
    {
        try
        {
            if (!File.Exists(_officialFilePath))
                return new List<PresetItem>();

            var json = File.ReadAllText(_officialFilePath);
            var presets = JsonSerializer.Deserialize<List<PresetItem>>(json, JsonOptions);
            if (presets != null)
            {
                foreach (var p in presets)
                    p.IsPersonal = false;
                if (deviceType.HasValue)
                    presets = presets.Where(p => p.DeviceType == deviceType.Value).ToList();
                return presets;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PresetService] 加载官方预设失败: {ex.Message}");
        }
        return new List<PresetItem>();
    }

    /// <summary>加载个人预设（从 AppData 文件）</summary>
    public List<PresetItem> LoadPersonalPresets()
    {
        return LoadPersonalPresets(null);
    }

    /// <summary>加载个人预设，按设备类型过滤</summary>
    public List<PresetItem> LoadPersonalPresets(DeviceType? deviceType)
    {
        try
        {
            if (!File.Exists(_personalFilePath))
                return new List<PresetItem>();

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
            System.Diagnostics.Debug.WriteLine($"[PresetService] 加载个人预设失败: {ex.Message}");
        }
        return new List<PresetItem>();
    }

    /// <summary>保存指定设备类型的个人预设（合并其他类型预设后写入文件）</summary>
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
            System.Diagnostics.Debug.WriteLine($"[PresetService] 保存个人预设失败: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>保存全部个人预设（覆盖写入，用于兼容旧调用）</summary>
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
            System.Diagnostics.Debug.WriteLine($"[PresetService] 保存个人预设失败: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>加载个人预设（不加锁的内部版本，仅供 SavePersonalPresets 持有锁时调用）</summary>
    private List<PresetItem> LoadPersonalPresetsUnlocked()
    {
        try
        {
            if (!File.Exists(_personalFilePath))
                return new List<PresetItem>();

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
            System.Diagnostics.Debug.WriteLine($"[PresetService] 加载个人预设失败: {ex.Message}");
        }
        return new List<PresetItem>();
    }

    /// <summary>导出单个预设到指定文件路径</summary>
    public void ExportPreset(PresetItem preset, string filePath)
    {
        var exportItem = new PresetItem
        {
            Name = preset.Name,
            Description = preset.Description,
            Category = preset.Category,
            Games = preset.Games,
            PedalParameters = preset.PedalParameters,
            WheelParameters = preset.WheelParameters,
            IsPersonal = true
        };

        var json = JsonSerializer.Serialize(exportItem, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    /// <summary>从文件导入预设</summary>
    public PresetItem? ImportPreset(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var preset = JsonSerializer.Deserialize<PresetItem>(json, JsonOptions);
            if (preset != null)
            {
                preset.IsPersonal = true;
            }
            return preset;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PresetService] 导入预设失败: {ex.Message}");
            return null;
        }
    }
}
