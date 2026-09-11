using System.IO;
using System.Text;
using System.Windows;

namespace HITAPEX.Services;

/// <summary>
/// 遥测支持展示数据源 —— 读取嵌入资源 Assets/遥测支持_UI.csv（28 个 UI 参数 × 31 游戏）。
/// 注意：该矩阵**仅用于 UI 展示**（"遥测支持"列表），与 SDK 实际下发的参数无关。
/// 合并口径：旗语/胎温/DRS/TC/ABS/ERS/油量等多源参数在 CSV 中已是合并后的单行结果。
/// </summary>
public static class TelemetrySupportService
{
    /// <summary>CSV 游戏列名 → GameId（列序与 遥测支持_UI.csv 表头一致）</summary>
    private static readonly Dictionary<string, int> GameColumns = new()
    {
        ["ac"] = 244210,
        ["acc"] = 805550,
        ["ac evo"] = 3058630,
        ["ac rally"] = 3917090,
        ["F1 22"] = 1692250,
        ["F1 23"] = 2108330,
        ["F1 24"] = 2488620,
        ["F1 25"] = 3059520,
        ["Forza Motorsport"] = 2440510,
        ["FH4"] = 1293830,
        ["FH5"] = 1551360,
        ["FH6"] = 2483190,
        ["lmu"] = 2399420,
        ["rf2"] = 365960,
        ["Dirt4"] = 421020,
        ["DirtRally2"] = 690790,
        ["eawrc"] = 1849250,
        ["iracing"] = 266410,
        ["RaceRoom Racing Experience"] = 211500,
        ["Automobilista 2"] = 1066890,
        ["Project CARS 2"] = 378860,
        ["Project CARS 3"] = 958400,
        ["Richard Burns Rally"] = 22,
        ["Euro Truck Simulator 2"] = 227300,
        ["American Truck Simulator"] = 270880,
        ["Live For Speed"] = 25,
        ["BeamNG.drive"] = 284160,
        ["WRC 8"] = 1004750,
        ["WRC 9"] = 1267540,
        ["WRC 10"] = 1462810,
        ["WRC Generations"] = 1953520,
    };

    /// <summary>游戏列名顺序（与 CSV 表头列序一致），用于定位 gameId 对应列索引</summary>
    private static readonly List<string> GameColumnOrder = [];

    /// <summary>矩阵缓存：CSV 行名 → 31 个游戏列的支持状态</summary>
    private static Dictionary<string, bool[]>? _matrix;

    static TelemetrySupportService()
    {
        LoadMatrix();
    }

    /// <summary>
    /// 加载指定游戏的支持状态（CSV 行名 → 是否支持）。
    /// 该游戏不在矩阵中（非遥测游戏）或解析失败返回 null，调用方应全部显示"不支持"。
    /// </summary>
    public static Dictionary<string, bool>? Load(int gameId)
    {
        var columnName = GameColumns.FirstOrDefault(kv => kv.Value == gameId).Key;
        if (_matrix == null || columnName == null) return null;

        var columnIndex = GameColumnOrder.IndexOf(columnName);
        if (columnIndex < 0) return null;

        var result = new Dictionary<string, bool>(_matrix.Count);
        foreach (var (rowName, states) in _matrix)
        {
            result[rowName] = columnIndex < states.Length && states[columnIndex];
        }
        return result;
    }

    /// <summary>从嵌入资源（WPF Resource，打包于 .g.resources）解析 CSV 到矩阵缓存</summary>
    private static void LoadMatrix()
    {
        try
        {
            // Assets\**\* 经 csproj 的 <Resource> 项嵌入，WPF 通过 pack URI 读取
            var uri = new Uri("/Assets/遥测支持_UI.csv", UriKind.Relative);
            var resourceInfo = Application.GetResourceStream(uri);

            using var stream = resourceInfo.Stream;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var lines = reader.ReadToEnd()
                .Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            if (lines.Count < 2)
            {
                _matrix = [];
                return;
            }

            // 表头：去掉引号与空列，第 1 项是"遥测名"，其余为游戏列名
            var headers = lines[0].Split(',').Select(h => h.Trim('"').Trim()).Where(h => h.Length > 0).ToList();
            GameColumnOrder.Clear();
            GameColumnOrder.AddRange(headers.Skip(1));

            var matrix = new Dictionary<string, bool[]>();
            for (int i = 1; i < lines.Count; i++)
            {
                var parts = lines[i].Split(',');
                if (parts.Length < 2) continue;

                var states = new bool[GameColumnOrder.Count];
                for (int g = 0; g < GameColumnOrder.Count && g + 1 < parts.Length; g++)
                {
                    states[g] = parts[g + 1].Trim() == "支持";
                }
                matrix[parts[0].Trim()] = states;
            }

            _matrix = matrix;
        }
        catch
        {
            // 解析失败视为无数据：UI 侧全部显示"不支持"
            _matrix = [];
        }
    }
}
