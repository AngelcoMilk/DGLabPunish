# DGLabPunish v0.6.1

适用于在 R.E.P.O. 游戏中连接 DG-LAB 郊狼 3.0 主机的惩罚型 Socket 模组，支持受击、死亡惩罚、左右脚步、奔跑、跳跃、落地、滑行等事件触发连续波形和强度变化。

作者 / Author: **AngelcoMilk**  
项目地址 / GitHub Repository: https://github.com/AngelcoMilk/DGLabPunish  
英文 README / English README: https://github.com/AngelcoMilk/DGLabPunish/blob/main/README_EN.md  
Thunderstore: https://thunderstore.io/c/repo/p/AngelcoMilk/DGLabPunish/

DGLabPunish 会在游戏内启动一个 DG-LAB App Socket 控制端，让手机 DG-LAB 3.0 App 扫码连接，再由手机通过蓝牙连接郊狼 3.0 主机。电脑不需要蓝牙。
源码、更新说明和完整文档都在 GitHub 项目地址中维护。

## 游戏内面板

按 `P` 打开游戏内“郊狼 3.0 连接面板”。

![DGLabPunish 游戏内面板](https://raw.githubusercontent.com/AngelcoMilk/DGLabPunish/main/docs/images/repo-coyote-panel.png)

面板标注：

1. **顶部分页**：`客户端` 用于连接和强度控制；`波形库`、`事件映射`、`导入/导出`、`事件参数` 用于调试和配置惩罚波形。
2. **Socket 控制端状态**：显示当前协议、连接模式和 App 连接状态。
3. **二维码地址文本**：这是二维码实际内容。请用 DG-LAB 3.0+ App 的 `SOCKET` 功能扫码，不要使用普通扫码入口。
4. **二维码**：手机扫码后会连接到面板中显示的 WebSocket 地址，并完成 App Socket 绑定。
5. **检测到的电脑 IPv4**：面板会列出多个候选 IP。优先选择手机同一 Wi-Fi 下可访问的地址，例如 `192.168.x.x`。
6. **连接模式**：`LocalServer` 表示电脑本机监听端口，手机从局域网访问电脑。
7. **局域网 IP/域名与本地端口**：二维码会使用这里的地址和端口。默认端口是 `9999`。
8. **公网/远程/Relay 字段**：用于公网 Socket、自建远程 Socket 或自建 relay code。这里的 `Relay Code` 不是 DG-LAB App 原生远程口令。
9. **保存/重启/停止服务**：修改 IP、端口或远程地址后，保存并重启服务，再重新扫码。
10. **强度控制**：底部显示 A/B 当前强度和上限，也可以在面板里测试、调整或清空通道。

## 重要软件版本

- 请使用 Google Play 下载的 `DG-LAB 3.0+` App，建议使用 3.x 系列。
- `DG-LAB 4.0` 及以上版本目前不兼容本 Mod 使用的 Socket 接入方式。
- 手机负责蓝牙连接郊狼 3.0 主机；电脑只需要和手机网络互通。

## 多 IP 地址选择

1. 优先选择和手机处在同一个 Wi-Fi/局域网的 IPv4。
2. 常见可用地址通常是 `192.168.x.x`、`10.x.x.x`、`172.16.x.x - 172.31.x.x`。
3. 不要选 `127.0.0.1`，手机无法通过这个地址访问电脑。
4. `198.18.x.x` 常见于虚拟网卡、代理或测试网络，不一定能被手机访问；如果扫码失败，请换成实际局域网 IP。
5. 修改 IP 或端口后，保存并重启服务，再重新扫码。
6. 确认 Windows 防火墙允许 R.E.P.O. 或端口 `9999` 通信。

## 主要功能

- 受击和死亡惩罚波形。
- 走路、奔跑、滑行连续波形。
- 左脚 A / 右脚 B 的脚步通道映射。
- 跳跃、落地、滑行、敌人脚步提示。
- A/B 强度控制、测试波形、清空队列、连接诊断。
- 波形预设和导入/导出配置。

## 快捷键

- `P`：打开/关闭游戏内控制面板。
- `I`：紧急停止，清空 A/B 队列并将强度设为 0。

## 安装（r2modman）

1. 导入 zip。
2. 确认 DLL 路径：`BepInEx/plugins/DGLabPunish/DGLabPunish.dll`
3. 确认依赖 `BepInExPack` 已安装。
