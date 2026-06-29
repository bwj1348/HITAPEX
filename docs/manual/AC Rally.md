# AC Rally - 遥测接入与排坑指南

## 连接方式

AC Rally 启动后自动开启遥测，无需任何游戏内设置。全部数据通过共享内存读取。

> 速度单位直接为 **km/h**。

## 已知限制

AC Rally 为拉力赛，大量归一化参数不适用：

| 参数 | 状态 |
|------|------|
| 最大转速 (maxRpm) | 不支持 |
| 燃油量 (fuelRemaining / fuelRemainingPct) | 不支持 |
| 维修区限速器 (isPitLimiterActive) | 不支持（拉力赛无维修区通道） |
| ERS 系统 | 不支持 |
| 旗语 (raceFlag) | 不支持（拉力赛无赛道旗语） |
| TC / ABS | 不支持（拉力赛车无牵引力/防抱死控制） |
| DRS | 不支持 |
