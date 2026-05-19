# USB 串口通信模块

## 概述

本模块为 HITAPEX 提供完整的 USB 串口设备通信能力，基于 .NET `System.IO.Ports` 和 WMI 实现。支持多设备并发连接、热插拔检测、自动重连、详细日志记录。

**依赖包：** `System.IO.Ports` `System.Management`（仅 Windows）

## 文件结构

```
Models/Usb/
├── VidPidPair.cs              VID/PID 识别对
├── DeviceConnectionState.cs   设备连接状态枚举
├── DeviceEventType.cs         日志事件类型枚举
├── UsbDeviceInfo.cs           设备信息模型
└── DeviceLogEntry.cs          日志条目模型

Services/Usb/
├── DeviceLogger.cs            日志记录器
├── UsbDeviceDiscovery.cs      设备发现与热插拔监控
├── DeviceSerialChannel.cs     单设备串口通道
├── IUsbSerialManager.cs       管理器接口
└── UsbSerialManager.cs        中央管理器

App.xaml.cs                    启动入口，初始化管理器
```

---

## 一、Models 层

### 1.1 VidPidPair — VID/PID 识别对

```
Models/Usb/VidPidPair.cs
```

用于标识一类 USB 设备的厂商 ID 和产品 ID。

| 成员 | 类型 | 说明 |
|------|------|------|
| `Vid` | `int` | 厂商识别码（Vendor ID），十六进制格式 |
| `Pid` | `int` | 产品识别码（Product ID），十六进制格式 |
| `ToString()` | `string` | 返回 `"VID_XXXX&PID_XXXX"` 格式的字符串 |

**用法示例：**
```csharp
var pair = new VidPidPair(0x0483, 0x5750);
// 表示 VID=0x0483, PID=0x5750 的设备
```

---

### 1.2 DeviceConnectionState — 设备连接状态

```
Models/Usb/DeviceConnectionState.cs
```

| 值 | 说明 |
|------|------|
| `Disconnected` | 未连接 |
| `Connecting` | 正在连接中 |
| `Connected` | 已连接，通信正常 |
| `Disconnecting` | 正在断开中 |
| `Reconnecting` | 正在重连中 |
| `Error` | 错误状态，已达最大重连次数 |

状态流转：
```
Disconnected → Connecting → Connected (成功)
                            → Error (失败)
Connected → Disconnecting → Disconnected
Connected → Error (异常) → Reconnecting → Connected (恢复)
                                         → Error (失败)
```

---

### 1.3 UsbDeviceInfo — 设备信息

```
Models/Usb/UsbDeviceInfo.cs
```

单个 USB 串口设备的完整信息描述。

| 成员 | 类型 | 说明 |
|------|------|------|
| `DeviceId` | `string` | Windows 设备实例 ID |
| `PortName` | `string` | 串口名称，如 `"COM3"` |
| `Vid` | `int` | 厂商 ID |
| `Pid` | `int` | 产品 ID |
| `Name` | `string` | 设备显示名称 |
| `Description` | `string` | 设备描述 |
| `SerialNumber` | `string` | 设备序列号（从 PNPDeviceID 提取） |
| `State` | `DeviceConnectionState` | 当前连接状态 |
| `LastConnectedTime` | `DateTime?` | 最后连接成功时间 |
| `ReconnectAttempts` | `int` | 当前重连尝试次数 |
| `TotalBytesReceived` | `long` | 累计接收字节数（线程安全） |
| `DeviceKey` | `string` | 设备唯一标识，格式 `"VID:PID_COMx"` |

`DeviceKey` 用作内部字典键值，格式如 `"0483:5750_COM3"`，可唯一定位一个物理设备。

---

### 1.4 DeviceEventType — 日志事件类型

```
Models/Usb/DeviceEventType.cs
```

| 值 | 说明 | 触发时机 |
|------|------|------|
| `DeviceConnected` | 设备已连接 | 串口打开成功 |
| `DeviceDisconnected` | 设备已断开 | 串口关闭 |
| `DeviceConnectFailed` | 连接失败 | 串口打开失败 |
| `DeviceReconnecting` | 正在重连 | 自动重连开始 |
| `DeviceReconnectFailed` | 重连失败 | 达到最大重连次数 |
| `DeviceRecovered` | 设备恢复 | 重连成功 |
| `RawDataReceived` | 原始数据接收 | 每次读取到数据 |
| `DataSendFailed` | 数据发送失败 | 发送异常 |
| `SerialError` | 串口错误 | 帧错误/溢出/校验错误 |
| `DiscoveryStarted` | 发现开始 | 管理器启动 |
| `DiscoveryCompleted` | 发现完成 | 设备扫描完成 |
| `VidPidMatched` | VID/PID 匹配 | 发现匹配的目标设备 |
| `VidPidNotMatched` | VID/PID 不匹配 | 发现不匹配的设备（预留） |

---

### 1.5 DeviceLogEntry — 日志条目

```
Models/Usb/DeviceLogEntry.cs
```

| 成员 | 类型 | 说明 |
|------|------|------|
| `Timestamp` | `DateTime` | 时间戳 |
| `EventType` | `DeviceEventType` | 事件类型 |
| `DeviceKey` | `string` | 设备标识 |
| `Message` | `string` | 事件描述 |
| `Detail` | `string?` | 附加详情（如 Hex 数据） |
| `Exception` | `Exception?` | 关联异常 |

---

## 二、Services 层

### 2.1 DeviceLogger — 日志记录器

```
Services/Usb/DeviceLogger.cs
```

线程安全的设备日志记录器，同时输出到文件、内存队列和 Debug 输出。

#### 构造函数

```csharp
public DeviceLogger(string logDirectory)
```
| 参数 | 说明 |
|------|------|
| `logDirectory` | 日志文件目录，目录不存在时自动创建 |

日志文件命名格式：`usb_device_yyyyMMdd.log`，按天自动分割。

#### 属性

| 成员 | 类型 | 说明 | 默认值 |
|------|------|------|------|
| `MaxInMemoryEntries` | `int` | 内存中保留的最大日志条目数 | 1000 |
| `LogEntryAdded` | `event Action<DeviceLogEntry>?` | 新日志条目产生时触发 | — |

#### 方法

```csharp
public void Log(DeviceEventType eventType, string deviceKey, string message,
                string? detail = null, Exception? ex = null)
```
记录一条日志。同时写入内存队列、触发 `LogEntryAdded` 事件、写入磁盘文件、输出到 `Debug.WriteLine`。

| 参数 | 说明 |
|------|------|
| `eventType` | 事件类型 |
| `deviceKey` | 设备标识（可为空字符串表示管理器级事件） |
| `message` | 事件描述 |
| `detail` | 附加详情，如 Hex 数据 |
| `ex` | 关联异常 |

```csharp
public IReadOnlyList<DeviceLogEntry> GetRecentEntries(int count = 100)
```
获取内存中最近的 N 条日志条目。

```csharp
public void SetEnabled(bool enabled)
```
启用/禁用日志记录。

```csharp
public void Clear()
```
清空内存中的日志队列。

---

### 2.2 UsbDeviceDiscovery — 设备发现

```
Services/Usb/UsbDeviceDiscovery.cs
```

通过 WMI 查询 Windows 串口设备列表，解析 PNPDeviceID 中的 VID/PID 并进行匹配。支持 WMI 事件热插拔监控和轮询降级模式。

#### 事件

| 事件 | 签名 | 说明 |
|------|------|------|
| `DeviceArrived` | `Action<UsbDeviceInfo>` | 发现匹配设备插入 |
| `DeviceRemoved` | `Action<UsbDeviceInfo>` | 匹配设备拔出 |

#### 方法

```csharp
public void AddTargetDevice(VidPidPair pair)
```
添加一个需要监控的 VID/PID 对。

```csharp
public void AddTargetDevices(IEnumerable<VidPidPair> pairs)
```
批量添加需要监控的 VID/PID 对。

```csharp
public void RemoveTargetDevice(VidPidPair pair)
```
移除一个监控目标。

```csharp
public void ClearTargetDevices()
```
清空所有监控目标。

```csharp
public IReadOnlyCollection<VidPidPair> GetTargetDevices()
```
获取当前所有已注册的监控目标。

```csharp
public IReadOnlyList<UsbDeviceInfo> DiscoverDevices()
```
执行一次全量设备扫描，返回当前插入的所有匹配设备列表。内部流程：

1. 通过 WMI 查询 `Win32_PnPEntity` 中 `PNPClass = 'Ports'` 且名称包含 `(COM` 的设备
2. 从 `PNPDeviceID` 中解析 VID/PID（格式 `USB\VID_XXXX&PID_XXXX\...`）
3. 从设备名称中提取 COM 端口号（如 `"USB Serial Device (COM3)"` → `"COM3"`）
4. 匹配已注册的 VID/PID 目标列表
5. 从 `PNPDeviceID` 末尾提取序列号

```csharp
public void StartHotplugMonitoring(int pollIntervalMs = 2000)
```
启动热插拔监控。优先使用 WMI 事件（`__InstanceCreationEvent` / `__InstanceDeletionEvent`），失败时自动降级为轮询模式。

| 参数 | 说明 | 默认值 |
|------|------|------|
| `pollIntervalMs` | 轮询模式下的扫描间隔（毫秒） | 2000 |

```csharp
public void StopHotplugMonitoring()
```
停止热插拔监控。

```csharp
public void Dispose()
```
释放所有资源，停止监控。

---

### 2.3 DeviceSerialChannel — 串口通道

```
Services/Usb/DeviceSerialChannel.cs
```

管理单个设备的串口连接和数据收发。每个设备对应一个 `DeviceSerialChannel` 实例，在独立的后台任务中持续异步读取数据。

#### 属性

| 成员 | 类型 | 说明 |
|------|------|------|
| `DeviceInfo` | `UsbDeviceInfo` | 关联的设备信息 |
| `State` | `DeviceConnectionState` | 当前连接状态 |
| `IsConnected` | `bool` | 是否已连接 |

#### 事件

| 事件 | 签名 | 说明 |
|------|------|------|
| `RawDataReceived` | `Action<DeviceSerialChannel, byte[]>` | 收到原始数据时触发，`byte[]` 为接收到的字节 |
| `ErrorOccurred` | `Action<DeviceSerialChannel, string>` | 通信异常时触发 |
| `StateChanged` | `Action<DeviceSerialChannel, DeviceConnectionState, DeviceConnectionState>` | 状态变化时触发，参数为 (旧状态, 新状态) |

#### 方法

```csharp
public DeviceSerialChannel(UsbDeviceInfo deviceInfo, DeviceLogger logger)
```
构造函数。传入设备信息和共享的日志记录器。

```csharp
public bool Connect(int baudRate = 115200, Parity parity = Parity.None,
                    int dataBits = 8, StopBits stopBits = StopBits.One,
                    int readTimeout = 500, int writeTimeout = 500)
```
打开串口连接。

| 参数 | 说明 | 默认值 |
|------|------|------|
| `baudRate` | 波特率 | 115200 |
| `parity` | 奇偶校验 | None |
| `dataBits` | 数据位 | 8 |
| `stopBits` | 停止位 | One |
| `readTimeout` | 读取超时（毫秒） | 500 |
| `writeTimeout` | 写入超时（毫秒） | 500 |

返回值：`true` 连接成功，`false` 失败。

连接成功的操作：打开串口 → 清空缓冲区 → 状态设为 `Connected` → 启动后台读取任务。

```csharp
public void Disconnect()
```
主动断开串口连接。停止读取任务 → 清空缓冲区 → 关闭串口 → 状态设为 `Disconnected`。

```csharp
public bool Send(byte[] data)
```
向设备发送原始字节数据。返回 `true` 发送成功，`false` 失败（未连接或异常）。

```csharp
public void Dispose()
```
释放所有资源，自动断开连接。

#### 读取循环机制

`ReadLoop` 在后台上持续运行：
1. 检查串口是否打开
2. 检测 `BytesToRead` 是否有可用数据
3. 通过 `BaseStream.ReadAsync` 异步读取
4. 将原始数据通过 `RawDataReceived` 事件透传
5. 记录日志（事件类型 `RawDataReceived`，详情包含 Hex 格式数据）
6. 异常时触发 `ErrorOccurred`，由上层 `UsbSerialManager` 处理重连

---

### 2.4 IUsbSerialManager — 管理器接口

```
Services/Usb/IUsbSerialManager.cs
```

定义 USB 串口管理器的公共契约，继承 `IDisposable`。

#### 属性

| 成员 | 类型 | 说明 |
|------|------|------|
| `ConnectedDevices` | `IReadOnlyList<UsbDeviceInfo>` | 当前已连接的设备列表 |
| `IsRunning` | `bool` | 管理器是否正在运行 |

#### 事件

| 事件 | 签名 | 说明 |
|------|------|------|
| `DeviceConnected` | `Action<UsbDeviceInfo>` | 有新设备连接成功 |
| `DeviceDisconnected` | `Action<UsbDeviceInfo>` | 设备断开 |
| `RawDataReceived` | `Action<UsbDeviceInfo, byte[]>` | 收到设备上报的原始数据 |
| `LogEntryAdded` | `Action<DeviceLogEntry>` | 新日志条目产生 |
| `DeviceError` | `Action<UsbDeviceInfo, string>` | 设备通信异常 |

#### 方法

| 方法 | 说明 |
|------|------|
| `RegisterTargetDevice(VidPidPair)` | 注册单个目标设备 VID/PID |
| `RegisterTargetDevices(IEnumerable<VidPidPair>)` | 批量注册目标设备 |
| `UnregisterTargetDevice(VidPidPair)` | 取消注册 |
| `GetRegisteredDevices()` | 获取已注册的 VID/PID 列表 |
| `Start()` | 启动管理器（扫描现有设备 + 开启热插拔监控） |
| `Stop()` | 停止管理器（断开所有设备 + 停止监控） |
| `ConnectDevice(UsbDeviceInfo)` | 手动连接指定设备 |
| `DisconnectDevice(UsbDeviceInfo)` | 手动断开指定设备 |
| `DisconnectAll()` | 断开所有设备 |
| `SendToDevice(string deviceKey, byte[] data)` | 向指定设备发送数据 |
| `GetRecentLogs(int count)` | 获取最近 N 条日志 |
| `SetLoggingEnabled(bool)` | 启用/禁用日志 |

---

### 2.5 UsbSerialManager — 中央管理器

```
Services/Usb/UsbSerialManager.cs
```

实现 `IUsbSerialManager`，是整个模块的中央协调器。负责：

- **设备生命周期管理**：发现 → 连接 → 监控 → 断开
- **多设备并发**：通过 `ConcurrentDictionary` 管理多个 `DeviceSerialChannel`
- **自动重连**：指数退避策略，最多 5 次，间隔 1s/2s/4s/8s/16s 最大 30s
- **事件转发**：将底层事件统一转发给业务层

#### 重连策略

| 参数 | 值 |
|------|------|
| 最大重连次数 | 5 |
| 基础延迟 | 1000ms |
| 最大延迟 | 30000ms |
| 退避算法 | `min(1000 × 2^n, 30000)` |

触发重连的场景：
1. 首次连接失败时自动触发
2. 运行期间串口 IO 异常时自动触发

---

## 三、App 入口配置

在 [App.xaml.cs](App.xaml.cs) 中完成管理器初始化：

```csharp
// 创建管理器，指定日志目录
var logDir = Path.Combine(AppContext.BaseDirectory, "logs", "usb");
UsbManager = new UsbSerialManager(logDir);

// 注册需要连接的设备 VID/PID
UsbManager.RegisterTargetDevices(new[]
{
    new VidPidPair(0x0483, 0x5750),  // 设备A
    new VidPidPair(0xFF3F, 0x0002),  // 设备B
});

// 订阅事件
UsbManager.DeviceConnected += device => { /* 设备连接 */ };
UsbManager.DeviceDisconnected += device => { /* 设备断开 */ };
UsbManager.RawDataReceived += (device, data) => { /* 处理原始数据 */ };
UsbManager.DeviceError += (device, error) => { /* 处理异常 */ };

// 启动
UsbManager.Start();
```

`App.UsbManager` 为静态属性，全局可通过 `App.UsbManager` 访问。

---

## 四、典型调用场景

### 场景 1：配置实际硬件 VID/PID

修改 [App.xaml.cs:52-54](App.xaml.cs#L52) 中的示例值：

```csharp
UsbManager.RegisterTargetDevices(new[]
{
    new VidPidPair(0x1234, 0x5678),  // 替换为实际值
});
```

### 场景 2：在 ViewModel 中订阅设备数据

```csharp
public class SomeViewModel
{
    public SomeViewModel()
    {
        if (App.UsbManager != null)
        {
            App.UsbManager.RawDataReceived += OnRawData;
        }
    }

    private void OnRawData(UsbDeviceInfo device, byte[] data)
    {
        // device.DeviceKey 区分是哪个设备的数据
        // data 是原始字节，自行按设备协议解析
    }
}
```

### 场景 3：向设备发送指令

```csharp
var deviceKey = "0483:5750_COM3";
var command = new byte[] { 0x01, 0x02, 0x03 };
bool ok = App.UsbManager.SendToDevice(deviceKey, command);
```

### 场景 4：获取设备连接状态

```csharp
var devices = App.UsbManager.ConnectedDevices;
foreach (var d in devices)
{
    Debug.WriteLine($"{d.DeviceKey}: {d.State}, 接收字节: {d.TotalBytesReceived}");
}
```

### 场景 5：查看调试日志

```csharp
var logs = App.UsbManager.GetRecentLogs(50);
foreach (var log in logs)
{
    Debug.WriteLine(log.ToString());
}
```

日志文件位于 `bin/Debug/net9.0-windows/logs/usb/usb_device_yyyyMMdd.log`。

---

## 五、日志格式说明

每行日志格式：
```
[yyyy-MM-dd HH:mm:ss.fff] [EventType] [DeviceKey] Message | Detail | Exception
```

示例：
```
[2026-05-18 15:10:01.123] [DiscoveryStarted] [] USB串口管理器启动
[2026-05-18 15:10:01.456] [VidPidMatched] [0483:5750_COM3] 发现匹配设备: USB Serial Device | VID=0483, PID=5750, Port=COM3
[2026-05-18 15:10:01.789] [DeviceConnected] [0483:5750_COM3] 串口连接成功: COM3 | Baud=115200, Parity=None, DataBits=8, StopBits=One
[2026-05-18 15:10:02.012] [RawDataReceived] [0483:5750_COM3] 接收原始数据: 8 字节 | Hex=01-02-03-04-05-06-07-08
```

- `DeviceKey` 为空字符串 `[]` 表示管理器级事件（非特定设备）
- `Detail` 字段用 `|` 分隔，包含具体参数或 Hex 数据

## 六、异常处理说明

| 异常 | 处理方式 |
|------|------|
| 设备未插入 | `DiscoverDevices` 返回空列表，不报错 |
| 串口打开失败 | 自动触发重连流程 |
| 串口读取 IO 异常 | 断开通道 → 创建新通道 → 自动重连 |
| 串口帧错误/溢出 | 记录日志，不中断读取 |
| WMI 热插拔监控失败 | 自动降级为轮询模式（默认 2 秒间隔） |
| VID/PID 格式不匹配 | 跳过该设备，不影响其他设备发现 |
| 日志文件写入失败 | 静默吞下异常，不影响主流程 |
