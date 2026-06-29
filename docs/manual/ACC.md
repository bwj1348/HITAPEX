# ACC (Assetto Corsa Competizione) - 遥测接入与排坑指南

## 连接方式

ACC 启动后**自动开启遥测**，无需任何游戏内设置。全部数据通过共享内存读取，无需 UDP。

> 速度单位直接为 **km/h**。

## 已知限制

### 无 ERS 系统

ACC 专注 GT 赛事，所有车辆均无混合动力系统。ERS 相关字段（电量、部署模式、回收级别等）均为默认值。

### DRS 极少可用

DRS 仅在 Bentley Continental GT3（DLC 车辆）上配备，绝大多数 GT3/GT4 车辆没有 DRS，相关字段始终为 0。

### 旗语系统

ACC 的 `flag` 字段在某些模式下不够可靠。插件优先使用 Global 旗语字段（`GlobalRed`、`GlobalYellow`、`GlobalChequered` 等）判断，仅在 Global 字段全为 0 时回退到 `flag` 字段。

## 测试推荐

| 测试目标 | 推荐车辆 | 场景 |
|----------|---------|------|
| 基础数据（速度/挡位/踏板） | 任意 GT3 车辆 | 任意练习赛 |
| TC / ABS 触发 | Ferrari 488 GT3 Evo | 出弯地板油验证 TC，重刹验证 ABS |
| 旗语 | 任意车辆 | Quick Race（加入 AI） |
| DRS | Bentley Continental GT3 | Quick Race，DRS 检测区域按键观察跳变 |
| TC/ABS 档位调节 | 任意车辆 | 练习赛中通过游戏设置调整 TC/ABS 级别，观察档位变化 |
