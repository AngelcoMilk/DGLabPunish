# RepoCoyoteStim

作者：AngelcoMilk

R.E.P.O. 的 DG-LAB 郊狼 3.0 App Socket Mod。电脑不需要蓝牙，手机负责通过蓝牙连接郊狼主机。

```text
R.E.P.O. Mod -> WebSocket -> 手机 DG-LAB 3.0 App -> 手机蓝牙 -> 郊狼 3.0 主机
```

## 快速使用

1. 安装后启动 R.E.P.O.
2. 按 `P` 打开游戏内控制面板。
3. 使用 `DG-LAB 3.0` App 的 `SOCKET` 功能扫描二维码。
4. 绑定成功后启用触发并调整强度。
5. 按 `I` 可随时紧急停止。

## 主要功能

- 受击、死亡、走路、奔跑、跳跃、落地、滑行事件触发。
- 连续基础波形、左右脚通道增强、受击/死亡惩罚波形。
- A/B 强度控制、测试波形、清空队列、连接诊断。
- 内置舒适、标准、强惩罚、调试同步预设。
- 支持从 `BepInEx/config/RepoCoyoteStim/profiles/` 导入/导出波形配置。

## 注意

请使用 DG-LAB App 的 `SOCKET` 功能扫码，不要用普通扫码入口。若无法连接，优先把面板地址改为电脑当前 IPv4，并确认手机与电脑网络互通。
