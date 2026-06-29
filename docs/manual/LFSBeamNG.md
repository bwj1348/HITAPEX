# LFS / BeamNG.drive - 遥测接入与排坑指南

## 连接方式

LFS 和 BeamNG.drive 均使用 OutGauge UDP 协议输出遥测数据，但配置方式不同：

### Live for Speed (LFS)

1. 启动一次游戏后退出（生成配置文件）
2. 打开游戏根目录下的 `cfg.txt`
3. 修改以下 OutGauge 参数：

```
OutGauge Mode 1       // 0=关闭, 1=驾驶中, 2=驾驶+回放
OutGauge IP 127.0.0.1 // 发送目标 IP
OutGauge Port 30000   // 发送目标端口（需与插件监听端口一致）
```

4. 保存后重新启动游戏，插件监听对应端口即可接收数据

### BeamNG.drive

在游戏内 **设置 → OutGauge** 中开启并配置 IP 地址和端口。

> 两款游戏 OutGauge 端口均修改为 30000。

> 速度单位原始为 **m/s**，插件内部已转换为 km/h。

## 已知限制

OutGauge 协议仅提供仪表盘级别的基础数据，大量归一化参数不支持：

| 参数 | 状态 |
|------|------|
| 最大转速 (maxRpm) | 不支持 |
| 转向角度 (steer) | 不支持（OutGauge 协议不提供） |
| TC / ABS 触发状态 | 不支持（OutGauge 的 TC/ABS 指示灯位含义模糊，无法可靠判断是否介入） |
| TC / ABS 档位 | 不支持 |
| 维修区限速器 | 不支持 |
| DRS | 不支持 |
| ERS 系统 | 不支持 |
| 轮胎滑移数据 | 不支持 |
| 旗语 (raceFlag) | 不支持 |
| 燃油剩余量 (fuelRemaining) | 不支持（仅提供百分比） |

### 发动机点火状态通过转速推断

isEngineRunning 和 isIgnitionOn 均通过 RPM > 0 推断，无独立点火字段。

## 测试推荐

LFS 与 BeamNG.drive 支持参数少，默认驾驶即可验证所有支持参数，无特殊场景要求。
