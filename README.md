# RepoCoyoteStim

作者：AngelcoMilk

R.E.P.O. 的 DG-LAB 郊狼 3.0 App Socket Mod。电脑不需要蓝牙，连接链路是：

```text
R.E.P.O. Mod -> WebSocket -> 手机 DG-LAB 3.0 App -> 手机蓝牙 -> 郊狼 3.0 主机
```

## 功能

- 游戏内中文控制面板：按 `P` 打开/关闭。
- 紧急停止：按 `I` 清空 A/B 队列，并把强度设为 0。
- 支持受击、死亡、走路、奔跑、跳跃、落地、滑行等事件触发。
- 支持连续基础波形、脚步左右通道增强、受击/死亡惩罚波形。
- 内置舒适、标准、强惩罚、调试同步等预设。
- 支持导入/导出波形配置：`BepInEx/config/RepoCoyoteStim/profiles/`。

## 使用

1. 在 r2modman/Thunderstore 导入 zip 包并启动 R.E.P.O.
2. 游戏内按 `P` 打开“郊狼 3.0 控制面板”。
3. 用 `DG-LAB 3.0` App 的 `SOCKET` 功能扫描面板二维码。
4. 绑定成功后，在面板中启用触发并按需调整强度。
5. 任意时候按 `I` 可紧急停止。

如果二维码连不上，优先把面板里的地址改成电脑当前 Wi-Fi/以太网 IPv4，并确认手机和电脑在同一网络。远程使用请走公网 `ws://` / `wss://` 或自建 relay，不是 DG-LAB App 原生远程口令。

## 构建

```powershell
.\build.ps1 -PackageToDesktop
```

默认读取：

- Game：`D:\SteamLibrary\steamapps\common\REPO`
- r2modman Profile：`%APPDATA%\r2modmanPlus-local\REPO\profiles\REPO`

输出：

```text
C:\Users\Administrator\Desktop\RepoCoyoteStim-0.5.8.zip
```
