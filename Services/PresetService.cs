using System.IO;
using System.Text.Json;
using HITAPEX.Models.Usb;
using HITAPEX.Views.DeviceParameters;

namespace HITAPEX.Services;

public class PresetService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _personalDir;
    private readonly string _personalFilePath;
    private readonly string _officialFilePath;

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
        try
        {
            var allPresets = LoadPersonalPresets();
            allPresets.RemoveAll(p => p.DeviceType == deviceType);
            allPresets.AddRange(presets);
            var json = JsonSerializer.Serialize(allPresets, JsonOptions);
            File.WriteAllText(_personalFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PresetService] 保存个人预设失败: {ex.Message}");
        }
    }

    /// <summary>保存全部个人预设（覆盖写入，用于兼容旧调用）</summary>
    public void SavePersonalPresets(List<PresetItem> presets)
    {
        try
        {
            var json = JsonSerializer.Serialize(presets, JsonOptions);
            File.WriteAllText(_personalFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PresetService] 保存个人预设失败: {ex.Message}");
        }
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
            Parameters = preset.Parameters,
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
