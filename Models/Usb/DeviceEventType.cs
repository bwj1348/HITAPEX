namespace HITAPEX.Models.Usb;

public enum DeviceEventType
{
    DeviceConnected,
    DeviceDisconnected,
    DeviceConnectFailed,
    DeviceReconnecting,
    DeviceReconnectFailed,
    DeviceRecovered,
    RawDataReceived,
    DataSendFailed,
    SerialError,
    DiscoveryStarted,
    DiscoveryCompleted,
    VidPidMatched,
    VidPidNotMatched
}
