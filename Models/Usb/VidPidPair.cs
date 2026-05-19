namespace HITAPEX.Models.Usb;

public readonly record struct VidPidPair(int Vid, int Pid)
{
    public override string ToString() => $"VID_{Vid:X4}&PID_{Pid:X4}";
}
