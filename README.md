# DGLabPunish v0.6.0

适用于在 R.E.P.O. 游戏中连接 DG-LAB 郊狼 3.0 主机的惩罚型 Socket 模组，支持受击、死亡惩罚、左右脚步、奔跑、跳跃、落地、滑行等事件触发连续波形和强度变化。

作者 / Author: **AngelcoMilk**  
项目地址 / GitHub Repository: https://github.com/AngelcoMilk/DGLabPunish  
英文 README / English README: [README_EN.md](README_EN.md)  
Thunderstore: https://thunderstore.io/c/repo/p/AngelcoMilk/DGLabPunish/

DGLabPunish 会在游戏内启动一个 DG-LAB App Socket 控制端，让手机 DG-LAB 3.0 App 扫码连接，再由手机通过蓝牙连接郊狼 3.0 主机。电脑不需要蓝牙。
源码、更新说明和完整文档都在 GitHub 项目地址中维护。

```text
R.E.P.O. Mod -> WebSocket -> 手机 DG-LAB 3.0 App -> 手机蓝牙 -> 郊狼 3.0 主机
```

## 重要软件版本

- 请使用 Google Play 下载的 `DG-LAB 3.0+` App，建议使用 3.x 系列。
- `DG-LAB 4.0` 及以上版本目前不兼容本 Mod 使用的 Socket 接入方式。
- 手机负责蓝牙连接郊狼 3.0 主机；电脑只需要和手机网络互通。

## 游戏内面板

按 `P` 打开游戏内“郊狼 3.0 连接面板”。

![DGLabPunish 游戏内面板](docs/images/repo-coyote-panel.png)

面板标注：

1. **顶部分页**：`客户端` 用于连接和强度控制；`波形库`、`事件映射`、`导入/导出`、`事件参数` 用于调试和配置惩罚波形。
2. **Socket 控制端状态**：显示当前协议、连接模式和 App 连接状态。截图中是 `本地局域网 / LocalServer`，状态为等待手机 App 连接。
3. **二维码地址文本**：这是二维码实际内容。请用 DG-LAB 3.0+ App 的 `SOCKET` 功能扫码，不要使用普通扫码入口。
4. **二维码**：手机扫码后会连接到面板中显示的 WebSocket 地址，并完成 App Socket 绑定。
5. **检测到的电脑 IPv4**：面板会列出多个候选 IP。截图里有 `198.18.0.1` 和 `192.168.1.99`，通常优先选择手机同一 Wi-Fi 下可访问的地址，例如家用路由常见的 `192.168.x.x`。
6. **连接模式**：`LocalServer` 表示电脑本机监听端口，手机从局域网访问电脑。
7. **局域网 IP/域名与本地端口**：二维码会使用这里的地址和端口。默认端口是 `9999`。
8. **公网/远程/Relay 字段**：用于公网 Socket、自建远程 Socket 或自建 relay code。这里的 `Relay Code` 不是 DG-LAB App 原生远程口令。
9. **保存/重启/停止服务**：修改 IP、端口或远程地址后，保存并重启服务，再重新扫码。
10. **强度控制**：底部显示 A/B 当前强度和上限，也可以在面板里测试、调整或清空通道。

## 多 IP 地址选择

多网卡、虚拟网卡、加速器、Tailscale、VMware、Hyper-V、WSL、Radmin 等环境下，电脑可能同时出现多个 IPv4。

选择规则：

1. 优先选择和手机处在同一个 Wi-Fi/局域网的 IPv4。
2. 常见可用地址通常是 `192.168.x.x`、`10.x.x.x`、`172.16.x.x - 172.31.x.x`。
3. 不要选 `127.0.0.1`，手机无法通过这个地址访问电脑。
4. `198.18.x.x` 常见于虚拟网卡、代理或测试网络，不一定能被手机访问；如果扫码失败，请换成实际局域网 IP。
5. 如果 App 连接失败，换另一个 IPv4，保存并重启服务后重新扫码。
6. 确认 Windows 防火墙允许 R.E.P.O. 或端口 `9999` 通信。

## 主要功能

- **受击惩罚**：本地玩家受到伤害时触发 A/B 通道惩罚波形，强度可按伤害变化。
- **死亡惩罚**：本地玩家死亡时可先清空旧队列，再输出更长的死亡波形。
- **连续波形**：走路、奔跑、滑行可进入持续状态，不只是单次点按。
- **左右脚通道**：优先使用游戏动画里的 `LeftFootDown / RightFootDown`，默认左脚 A、右脚 B。
- **动作事件**：支持跳跃、落地、滑行、敌人近距离脚步提示等事件。
- **波形预设**：内置舒适、标准、强惩罚、调试同步等预设。
- **导入/导出**：支持从 `BepInEx/config/DGLabPunish/profiles/` 导入/导出波形配置。

## 连接方法

1. 在 r2modman/Thunderstore 导入 Mod 包并启动 R.E.P.O.
2. 进入游戏后按 `P` 打开“郊狼 3.0 控制面板”。
3. 打开手机 `DG-LAB 3.0+` App，进入 `SOCKET` 功能。
4. 扫描游戏面板中的二维码。
5. 绑定成功后，在面板中确认 A/B 当前强度，并启用触发。
6. 任意时候按 `I` 可紧急停止，清空 A/B 队列并把强度设为 0。

## 远程连接说明

本 Mod 主线使用 DG-LAB 官方 App Socket 协议。DG-LAB App 原生“远程口令/远程码”不是公开 Socket v2 API，本 Mod 不把它作为稳定接入方式。

可用的远程方式：

- `PublicSocket`：二维码使用公网 `ws://` / `wss://` 地址。
- `RemoteServer`：Mod 连接到公网 Socket v2 后端。
- `RelayCode`：使用自建 relay code。注意这不是 DG-LAB App 原生远程口令，需要兼容的自建 relay 服务。

## 快捷键

- `P`：打开/关闭游戏内控制面板。
- `I`：紧急停止，清空 A/B 队列并将强度设为 0。

## 配置说明

配置文件：

```text
BepInEx/config/com.angelcomilk.repo.dglabpunish.cfg
```

常用配置：

- `Connection.Port`：本地 Socket 端口，默认 `9999`。
- `Connection.AdvertiseHost`：二维码里展示给手机连接的电脑 IP/域名。
- `Safety.Enabled`：是否启用游戏事件触发。
- `Safety.AutoStrengthMode`：自动调强模式。
- `Safety.MaxWaveIntensity`：本 Mod 使用的波形强度上限。
- `Hurt.*`：受击惩罚强度、持续时间、频率和通道倍率。
- `Death.*`：死亡惩罚强度、持续时间、频率和是否清队列。
- `Footstep.*`：左右脚通道、脚步强度、频率、最小间隔和兜底设置。

## 安装（r2modman）

1. 导入 zip。
2. 确认 DLL 路径：`BepInEx/plugins/DGLabPunish/DGLabPunish.dll`
3. 确认依赖 `BepInExPack` 已安装。

## 已知限制

- 只支持 DG-LAB 郊狼 3.0 的 App Socket 接入，不直接控制电脑蓝牙。
- `DG-LAB 4.0` 及以上 App 当前不兼容此 Socket 方案。
- 手机和电脑不在同一局域网时，局域网二维码不能直接使用，需要公网 Socket 或自建 relay。
- 事件只在本地客户端触发，不修改游戏网络状态，也不强制同步给其他玩家。
- 不建议在公开房间测试高强度或未调好的波形配置。

---

# DGLabPunish v0.6.0

A R.E.P.O. DG-LAB Coyote 3.0 punishment Socket mod for in-game Coyote connection, hit/death punishment, footstep/action triggers, continuous waveforms, and configurable strength profiles.

Author: **AngelcoMilk**  
GitHub Repository: https://github.com/AngelcoMilk/DGLabPunish  
Thunderstore: https://thunderstore.io/c/repo/p/AngelcoMilk/DGLabPunish/

Use the `DG-LAB 3.0+` Android app from Google Play, preferably the 3.x app line. `DG-LAB 4.0` and later are currently not compatible with this Socket workflow.
