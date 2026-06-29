# WRC Generations - 原生 UDP 遥测接入指南

## 连接方式

WRC Generations **原生支持 UDP 遥测输出**，无需安装第三方补丁 DLL。数据通过 UDP 端口 `20777` 广播，数据包格式（64 个 float，256 字节）与 DiRT 系列完全一致。

## 开启步骤

1. 导航到本地文件：`Documents\My Games\WRCG\`
2. 使用文本编辑器（如记事本）打开 `UserSettings.cfg` 文件
3. 定位并修改遥测参数为以下内容：
   ```ini
   WRC.Telemetry.EnableTelemetry = true;
   WRC.Telemetry.TelemetryPort = 20777;    // 接收端口，可自定义
   WRC.Telemetry.TelemetryRate = 60;       // 数据包输出速率 60Hz
   ```
4. 保存文件后启动游戏即可

## SDK 接入参数

| 参数 | 值 |
|-----|-----|
| gameId | `GAME_WRC_GENERATIONS` |
| 默认端口 | 20777 |
| 传输方式 | UDP 广播 |
| 数据包大小 | 256 字节（64 个 float） |
| 需要补丁 | **否** |

## 已知限制

| 参数 | 状态 |
|------|------|
| TC / ABS 触发状态 | 不支持 |
| TC / ABS 档位 | 不支持 |
| 燃油系统 | 不支持 |
| ERS 系统 | 不支持 |
| DRS | 不支持 |
| 维修区限速器 | 不支持 |
| 旗语 (raceFlag) | 不支持（拉力赛无赛道旗语） |
| 轮胎滑移角 / 总滑移 | 不支持（缺少侧向速度数据） |

## 测试推荐

| 测试目标 | 推荐场景 | 验证要点 |
|----------|---------|---------|
| 基础数据（速度/挡位/踏板） | 任意拉力赛段 | 观察速度、转速、档位、踏板变化 |
| 纵向滑移率 | 急加速 / 重刹 | slipRatio 数值变化 |
| UDP 连接成功 | 启动游戏进入赛段 | SDK 返回有效数据（speed > 0 或 rpm > 0） |
