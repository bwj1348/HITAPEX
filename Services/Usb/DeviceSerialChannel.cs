using System.IO;
using System.IO.Ports;
using HITAPEX.Models.Usb;

namespace HITAPEX.Services.Usb;

public class DeviceSerialChannel : IDisposable
{
    private readonly UsbDeviceInfo _deviceInfo;
    private readonly DeviceLogger _logger;
    private SerialPort? _serialPort;
    private CancellationTokenSource? _readCts;
    private bool _disposed;

    public event Action<DeviceSerialChannel, byte[]>? RawDataReceived;
    public event Action<DeviceSerialChannel, string>? ErrorOccurred;
    public event Action<DeviceSerialChannel, DeviceConnectionState, DeviceConnectionState>? StateChanged;

    public UsbDeviceInfo DeviceInfo => _deviceInfo;
    public DeviceConnectionState State => _deviceInfo.State;
    public bool IsConnected => _deviceInfo.State == DeviceConnectionState.Connected;

    public DeviceSerialChannel(UsbDeviceInfo deviceInfo, DeviceLogger logger)
    {
        _deviceInfo = deviceInfo;
        _logger = logger;
    }

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

    private void StartReading()
    {
        _readCts = new CancellationTokenSource();
        _ = Task.Run(() => ReadLoop(_readCts.Token), _readCts.Token);
    }

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
                    _logger.Log(DeviceEventType.RawDataReceived, _deviceInfo.DeviceKey,
                        $"接收原始数据: {bytesRead} 字节",
                        $"Hex={BitConverter.ToString(rawData)}");
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

    private void SetState(DeviceConnectionState newState)
    {
        var oldState = _deviceInfo.State;
        if (oldState == newState) return;

        _deviceInfo.State = newState;
        StateChanged?.Invoke(this, oldState, newState);
    }

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
