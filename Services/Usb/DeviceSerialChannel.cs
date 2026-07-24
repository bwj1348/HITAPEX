using System.IO;
using System.IO.Ports;
using HITAPEX.Models.Usb;

namespace HITAPEX.Services.Usb;

/// <summary>
/// 设备串口通道 —— 封装单个 USB 串口设备的连接、断开、数据收发和状态管理。
/// 每个已发现的设备都拥有独立的 DeviceSerialChannel 实例，
/// 在后台线程中异步读取串口数据并通过事件向上层抛出。
/// </summary>
/// <remarks>
/// 线程模型：ReadLoop 在独立 Task 中运行，通过 CancellationToken 控制生命周期。
/// 串口参数：115200-8-N-1，DTR/RTS 开启，读写超时 500ms。
/// 自动重连：连接失败时由 UsbSerialManager 通过 TryReconnectAsync 管理，指数退避最多 5 次。
/// </remarks>
public class DeviceSerialChannel : IDisposable
{
    private readonly UsbDeviceInfo _deviceInfo;
    private readonly DeviceLogger _logger;
    private SerialPort? _serialPort;
    private CancellationTokenSource? _readCts;
    private bool _disposed;

    /// <summary>收到原始数据时触发（未经协议解析）</summary>
    public event Action<DeviceSerialChannel, byte[]>? RawDataReceived;

    /// <summary>发生错误时触发（包含错误描述字符串）</summary>
    public event Action<DeviceSerialChannel, string>? ErrorOccurred;

    /// <summary>连接状态变更时触发（旧状态 → 新状态）</summary>
    public event Action<DeviceSerialChannel, DeviceConnectionState, DeviceConnectionState>? StateChanged;

    /// <summary>设备信息</summary>
    public UsbDeviceInfo DeviceInfo => _deviceInfo;

    /// <summary>当前连接状态</summary>
    public DeviceConnectionState State => _deviceInfo.State;

    /// <summary>是否已连接</summary>
    public bool IsConnected => _deviceInfo.State == DeviceConnectionState.Connected;

    /// <summary>
    /// 初始化设备串口通道。
    /// </summary>
    /// <param name="deviceInfo">设备信息（包含 PortName、VID、PID 等）</param>
    /// <param name="logger">设备日志记录器</param>
    public DeviceSerialChannel(UsbDeviceInfo deviceInfo, DeviceLogger logger)
    {
        _deviceInfo = deviceInfo;
        _logger = logger;
    }

    /// <summary>
    /// 连接到设备串口。
    /// </summary>
    /// <param name="baudRate">波特率（默认 115200）</param>
    /// <param name="parity">校验位（默认 None）</param>
    /// <param name="dataBits">数据位（默认 8）</param>
    /// <param name="stopBits">停止位（默认 One）</param>
    /// <param name="readTimeout">读取超时毫秒数（默认 500）</param>
    /// <param name="writeTimeout">写入超时毫秒数（默认 500）</param>
    /// <returns>是否连接成功</returns>
    public bool Connect(int baudRate = 115200, Parity parity = Parity.None,
                        int dataBits = 8, StopBits stopBits = StopBits.One,
                        int readTimeout = 500, int writeTimeout = 500)
    {
        if (_disposed)
            return false;

        SetState(DeviceConnectionState.Connecting);

        try
        {
            _serialPort = new SerialPort(_deviceInfo.PortName, baudRate, parity, dataBits, stopBits)
            {
                ReadTimeout = readTimeout,
                WriteTimeout = writeTimeout,
                ReadBufferSize = 65536,
                WriteBufferSize = 4096,
                DtrEnable = true,
                RtsEnable = true
            };

            _serialPort.ErrorReceived += OnSerialError;
            _serialPort.Open();

            if (!_serialPort.IsOpen)
            {
                SetState(DeviceConnectionState.Error);
                return false;
            }

            _serialPort.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();

            SetState(DeviceConnectionState.Connected);
            _deviceInfo.LastConnectedTime = DateTime.Now;
            _deviceInfo.ReconnectAttempts = 0;

            _logger.Log(DeviceEventType.DeviceConnected, _deviceInfo.DeviceKey,
                $"串口连接成功: {_deviceInfo.PortName}",
                $"Baud={baudRate}, Parity={parity}, DataBits={dataBits}, StopBits={stopBits}");

            StartReading();
            return true;
        }
        catch (Exception ex)
        {
            SetState(DeviceConnectionState.Error);
            _logger.Log(DeviceEventType.DeviceConnectFailed, _deviceInfo.DeviceKey,
                $"串口连接失败: {_deviceInfo.PortName}", null, ex);
            CleanupSerialPort();
            return false;
        }
    }

    /// <summary>
    /// 断开设备串口连接。已断开或正在断开中则跳过。
    /// </summary>
    public void Disconnect()
    {
        if (_deviceInfo.State == DeviceConnectionState.Disconnected ||
            _deviceInfo.State == DeviceConnectionState.Disconnecting)
            return;

        SetState(DeviceConnectionState.Disconnecting);

        StopReading();
        CleanupSerialPort();

        SetState(DeviceConnectionState.Disconnected);
        _logger.Log(DeviceEventType.DeviceDisconnected, _deviceInfo.DeviceKey,
            $"串口已断开: {_deviceInfo.PortName}");
    }

    /// <summary>
    /// 通过串口发送字节数据。
    /// </summary>
    /// <param name="data">要发送的字节数组</param>
    /// <returns>是否发送成功</returns>
    public bool Send(byte[] data)
    {
        if (_serialPort == null || !_serialPort.IsOpen || _disposed)
            return false;

        try
        {
            _serialPort.Write(data, 0, data.Length);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Log(DeviceEventType.DataSendFailed, _deviceInfo.DeviceKey,
                "数据发送失败", null, ex);
            return false;
        }
    }

    /// <summary>
    /// 启动后台异步读取循环。
    /// </summary>
    private void StartReading()
    {
        _readCts = new CancellationTokenSource();
        _ = Task.Run(() => ReadLoop(_readCts.Token), _readCts.Token);
    }

    /// <summary>
    /// 停止后台读取循环。取消 CancellationTokenSource 并延迟 1 秒销毁，
    /// 避免 ReadLoop 中仍有未完成的 Task.Delay 引用已释放的 Token。
    /// </summary>
    private void StopReading()
    {
        var cts = Interlocked.Exchange(ref _readCts, null);
        if (cts == null)
            return;

        cts.Cancel();
        // 延迟销毁 CancellationTokenSource，避免 ReadLoop 中仍有未完成的 Task.Delay 引用已释放的 Token
        _ = Task.Run(async () =>
        {
            await Task.Delay(1000);
            cts.Dispose();
        });
    }

    /// <summary>
    /// 后台读取循环 —— 持续从串口缓冲区读取数据并触发 RawDataReceived 事件。
    /// 读取间隔约 1ms，使用异步 I/O 避免阻塞线程。
    /// </summary>
    private async Task ReadLoop(CancellationToken token)
    {
        var readBuffer = new byte[4096];

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen)
                    break;

                var bytesToRead = _serialPort.BytesToRead;
                if (bytesToRead == 0)
                {
                    await Task.Delay(1, token);
                    continue;
                }

                var bytesRead = await _serialPort.BaseStream.ReadAsync(
                    readBuffer, 0, Math.Min(readBuffer.Length, bytesToRead), token);

                if (bytesRead > 0)
                {
                    _deviceInfo.IncrementBytesReceived(bytesRead);

                    var rawData = new byte[bytesRead];
                    Array.Copy(readBuffer, 0, rawData, 0, bytesRead);
                    // 原始数据日志量极大，调试时取消注释
                    // _logger.Log(DeviceEventType.RawDataReceived, _deviceInfo.DeviceKey,
                    //     $"接收原始数据: {bytesRead} 字节",
                    //     $"Hex={BitConverter.ToString(rawData)}");
                    RawDataReceived?.Invoke(this, rawData);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (InvalidOperationException)
            {
                break;
            }
            catch (IOException ex)
            {
                _logger.Log(DeviceEventType.SerialError, _deviceInfo.DeviceKey,
                    "串口读取IO异常", null, ex);
                ErrorOccurred?.Invoke(this, ex.Message);
                break;
            }
            catch (Exception ex)
            {
                _logger.Log(DeviceEventType.SerialError, _deviceInfo.DeviceKey,
                    "串口读取异常", null, ex);
                ErrorOccurred?.Invoke(this, ex.Message);
                break;
            }
        }
    }

    /// <summary>
    /// 串口错误事件处理 —— 将 SerialError 枚举转换为中文描述。
    /// </summary>
    private void OnSerialError(object sender, SerialErrorReceivedEventArgs e)
    {
        var errorMsg = e.EventType switch
        {
            SerialError.Frame => "帧错误",
            SerialError.Overrun => "缓冲区溢出",
            SerialError.RXOver => "接收缓冲区溢出",
            SerialError.RXParity => "奇偶校验错误",
            SerialError.TXFull => "发送缓冲区满",
            _ => $"未知串口错误: {e.EventType}"
        };

        _logger.Log(DeviceEventType.SerialError, _deviceInfo.DeviceKey,
            $"串口错误: {errorMsg}");

        ErrorOccurred?.Invoke(this, errorMsg);
    }

    /// <summary>
    /// 更新设备连接状态。状态未变化则跳过。状态变更时触发 StateChanged 事件。
    /// </summary>
    private void SetState(DeviceConnectionState newState)
    {
        var oldState = _deviceInfo.State;
        if (oldState == newState) return;

        _deviceInfo.State = newState;
        StateChanged?.Invoke(this, oldState, newState);
    }

    /// <summary>
    /// 清理串口资源 —— 取消事件订阅、清空缓冲区、关闭并释放 SerialPort。
    /// </summary>
    private void CleanupSerialPort()
    {
        try
        {
            if (_serialPort != null)
            {
                _serialPort.ErrorReceived -= OnSerialError;
                if (_serialPort.IsOpen)
                {
                    _serialPort.DiscardInBuffer();
                    _serialPort.DiscardOutBuffer();
                    _serialPort.Close();
                }
                _serialPort.Dispose();
                _serialPort = null;
            }
        }
        catch (Exception ex)
        {
            _logger.Log(DeviceEventType.DeviceDisconnected, _deviceInfo.DeviceKey,
                "清理串口资源异常", null, ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
    }
}
