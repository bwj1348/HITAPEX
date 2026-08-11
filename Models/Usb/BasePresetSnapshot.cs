using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace HITAPEX.Models.Usb;

/// <summary>基座参数完整快照，用于预设存取与修改比对</summary>
public class BasePresetSnapshot
{
    /// <summary>最大转向角度 (0-65535)</summary>
    [JsonPropertyName("maxSteeringAngle")]
    public ushort MaxSteeringAngle { get; set; }

    /// <summary>限位刚度 (0-100)</summary>
    [JsonPropertyName("limitRigidity")]
    public byte LimitRigidity { get; set; }

    /// <summary>最大速度 (0-100)</summary>
    [JsonPropertyName("maxSpeed")]
    public byte MaxSpeed { get; set; }

    /// <summary>平滑等级 (0-100)</summary>
    [JsonPropertyName("smoothLevel")]
    public byte SmoothLevel { get; set; }

    /// <summary>力回馈强度 (0-100)</summary>
    [JsonPropertyName("forceStrength")]
    public byte ForceStrength { get; set; }

    /// <summary>机械惯性</summary>
    [JsonPropertyName("mechInertia")]
    public byte MechInertia { get; set; }

    /// <summary>机械回中</summary>
    [JsonPropertyName("mechCentering")]
    public byte MechCentering { get; set; }

    /// <summary>机械阻尼</summary>
    [JsonPropertyName("mechDamping")]
    public byte MechDamping { get; set; }

    /// <summary>机械摩擦</summary>
    [JsonPropertyName("mechFriction")]
    public byte MechFriction { get; set; }

    /// <summary>游戏惯性</summary>
    [JsonPropertyName("gameInertia")]
    public byte GameInertia { get; set; }

    /// <summary>游戏弹性</summary>
    [JsonPropertyName("gameElastic")]
    public byte GameElastic { get; set; }

    /// <summary>游戏阻尼</summary>
    [JsonPropertyName("gameDamping")]
    public byte GameDamping { get; set; }

    /// <summary>游戏摩擦</summary>
    [JsonPropertyName("gameFriction")]
    public byte GameFriction { get; set; }

    /// <summary>游戏惯性强度</summary>
    [JsonPropertyName("gameInertiaStr")]
    public byte GameInertiaStr { get; set; }

    /// <summary>脱手保护 (0-100)</summary>
    [JsonPropertyName("handsOffProtect")]
    public byte HandsOffProtect { get; set; }

    /// <summary>力回馈反向</summary>
    [JsonPropertyName("forceReverse")]
    public byte ForceReverse { get; set; }

    /// <summary>逐字段比较基座参数是否一致，同时输出差异日志</summary>
    public bool ParametersEqual(BasePresetSnapshot other)
    {
        var diffs = new List<string>();

        void Check(string name, object? a, object? b)
        {
            if (!Equals(a, b))
                diffs.Add($"{name}: device={a}, preset={b}");
        }

        Check("MaxSteeringAngle", MaxSteeringAngle, other.MaxSteeringAngle);
        Check("LimitRigidity", LimitRigidity, other.LimitRigidity);
        Check("MaxSpeed", MaxSpeed, other.MaxSpeed);
        Check("SmoothLevel", SmoothLevel, other.SmoothLevel);
        Check("ForceStrength", ForceStrength, other.ForceStrength);
        Check("MechInertia", MechInertia, other.MechInertia);
        Check("MechCentering", MechCentering, other.MechCentering);
        Check("MechDamping", MechDamping, other.MechDamping);
        Check("MechFriction", MechFriction, other.MechFriction);
        Check("GameInertia", GameInertia, other.GameInertia);
        Check("GameElastic", GameElastic, other.GameElastic);
        Check("GameDamping", GameDamping, other.GameDamping);
        Check("GameFriction", GameFriction, other.GameFriction);
        Check("GameInertiaStr", GameInertiaStr, other.GameInertiaStr);
        Check("HandsOffProtect", HandsOffProtect, other.HandsOffProtect);
        Check("ForceReverse", ForceReverse, other.ForceReverse);

        if (diffs.Count > 0)
        {
            Debug.WriteLine($"[BasePresetSnapshot.ParametersEqual] 发现 {diffs.Count} 处不一致:");
            foreach (var d in diffs)
                Debug.WriteLine($"  {d}");
            return false;
        }
        else
        {
            Debug.WriteLine("[BasePresetSnapshot.ParametersEqual] 参数完全一致");
            return true;
        }
    }

    /// <summary>校验参数值是否在合法范围内，返回错误消息列表（空列表表示通过）</summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        void InRange(string name, int value, int min, int max)
        {
            if (value < min || value > max)
                errors.Add($"{name}: {value}, 允许范围 [{min}, {max}]");
        }

        InRange("MaxSteeringAngle", MaxSteeringAngle, 0, 65535);
        InRange("LimitRigidity", LimitRigidity, 0, 100);
        InRange("MaxSpeed", MaxSpeed, 0, 100);
        InRange("SmoothLevel", SmoothLevel, 0, 100);
        InRange("ForceStrength", ForceStrength, 0, 100);
        InRange("MechInertia", MechInertia, 0, 100);
        InRange("MechCentering", MechCentering, 0, 100);
        InRange("MechDamping", MechDamping, 0, 100);
        InRange("MechFriction", MechFriction, 0, 100);
        InRange("GameInertia", GameInertia, 0, 100);
        InRange("GameElastic", GameElastic, 0, 100);
        InRange("GameDamping", GameDamping, 0, 100);
        InRange("GameFriction", GameFriction, 0, 100);
        InRange("GameInertiaStr", GameInertiaStr, 0, 100);
        InRange("HandsOffProtect", HandsOffProtect, 0, 100);
        InRange("ForceReverse", ForceReverse, 0, 1);

        return errors;
    }
}
