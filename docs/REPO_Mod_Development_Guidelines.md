# R.E.P.O. Mod 开发通用规范

作者：AngelcoMilk

本文是一份面向 R.E.P.O. 的 BepInEx / Harmony Mod 开发规范，目标是让项目可维护、可验证、少踩版权与联机风险，并尽量使用强类型引用，避免不必要的反射。

## 1. 基本原则

- 只发布自己的代码、配置、图标和自制资产，不发布游戏原始 DLL、反编译源码、ripped 资源或 patched Unity 工程。
- 优先使用 BepInEx + Harmony 做运行时补丁；不要直接修改游戏原始 DLL。
- 优先引用本地游戏程序集并使用强类型写法，例如 `typeof(PlayerHealth)`、`PlayerAvatar.instance`。
- 不必要不使用反射。能通过本地程序集引用、公开字段、公开方法和 Harmony 参数拿到的数据，就不要用 `AccessTools`、`GetField`、`GetMethod`、`Invoke`。
- 每次游戏更新后重新核对目标方法签名、字段名和行为，不假设旧版本 hook 永久有效。
- 联机相关逻辑只影响本地客户端，不修改游戏网络状态，不向其他玩家强制同步非必要行为。

## 2. 环境与引用

推荐把开发 profile 与日常游玩 profile 分开，避免测试插件污染正常游玩环境。

常用本地路径：

```text
游戏目录:
C:\Program Files (x86)\Steam\steamapps\common\REPO
或
D:\SteamLibrary\steamapps\common\REPO

游戏程序集:
REPO_Data\Managed\Assembly-CSharp.dll

r2modman profile:
%APPDATA%\r2modmanPlus-local\REPO\profiles\<ProfileName>

BepInEx 核心:
%APPDATA%\r2modmanPlus-local\REPO\profiles\<ProfileName>\BepInEx\core
```

常用编译引用：

- `Assembly-CSharp.dll`：游戏核心类型。
- `UnityEngine.dll`、`UnityEngine.CoreModule.dll`、`UnityEngine.IMGUIModule.dll` 等：Unity API。
- `BepInEx.dll`：插件入口与配置。
- `0Harmony.dll`：Harmony patch。
- 其他游戏实际需要的托管 DLL，例如 Photon、websocket-sharp、Newtonsoft.Json。

引用这些 DLL 是为了编译和类型检查，不要把游戏 DLL 放进公开仓库或发布包。

## 3. 如何获取内部方法

先用 ILSpy 或 dnSpyEx 打开：

```text
REPO_Data\Managed\Assembly-CSharp.dll
```

查找内部方法时按这个顺序做：

1. 先从功能关键词搜索，例如 `Hurt`、`Death`、`Footstep`、`Jump`、`Land`、`Slide`。
2. 找到候选类后，记录完整信息：类名、方法名、参数、返回值、字段依赖。
3. 查看调用者，确认方法是否只在本地玩家触发，还是所有玩家/敌人都会触发。
4. 反编译结果只作为定位依据，实际 patch 必须在游戏内用日志或调试验证。
5. 对多人游戏事件，必须确认本地玩家判断，例如 `PlayerAvatar.instance`、`photonView.IsMine`。

记录格式建议：

```text
目标事件: 玩家受击
目标类: PlayerHealth
目标方法: Hurt(int damage)
Patch 类型: Postfix
本地判断: PlayerAvatar.instance.playerHealth == __instance
风险: 游戏更新后参数名或调用时机可能变化
```

## 4. Harmony Patch 规范

优先使用强类型类引用：

```csharp
[HarmonyPatch(typeof(PlayerHealth), "Hurt")]
internal static class PlayerHealthHurtPatch
{
    private static void Postfix(PlayerHealth __instance, int damage)
    {
        if (!RepoGuards.IsLocalHealth(__instance))
        {
            return;
        }

        Plugin.Instance.Stim.TriggerHurt(damage);
    }
}
```

推荐顺序：

- Postfix：优先使用，最不容易破坏原游戏逻辑。
- Prefix：只在需要提前记录状态或拦截前置条件时使用。
- Transpiler：只在 Prefix/Postfix 无法满足时使用，并且必须写清楚原因。
- RuntimeDetour / Preloader patch：只用于极少数必须提前处理程序集或底层调用的场景。

Patch 命名建议：

```text
<目标类><目标方法>Patch
PlayerHealthHurtPatch
PlayerAvatarDeathPatch
PlayerAvatarVisualsFootstepLightPatch
```

不要在 patch 中写大量业务逻辑。Patch 应该只负责捕获游戏事件，然后转发给自己的控制器：

```csharp
stim.TriggerFootstepFromVisuals("Light");
```

## 5. 反射使用边界

默认禁止在核心逻辑里大量使用反射。

可以接受的情况：

- 配置 UI 枚举解析：`Enum.Parse`、`Enum.GetValues`。
- JSON 导入导出。
- 兼容不同游戏版本的 optional hook，但必须集中封装，并有日志说明。

需要谨慎的情况：

- `AccessTools.Field`
- `AccessTools.Method`
- `BindingFlags.NonPublic`
- `GetField` / `GetMethod`
- `MethodInfo.Invoke`
- 通过字符串访问私有字段并频繁读取

如果确实必须使用反射，遵守这些规则：

- 只在启动或初始化时查找一次，缓存结果。
- 失败时安全降级，不阻塞游戏启动。
- 写清楚目标版本、目标字段/方法、为什么不能强类型引用。
- 不在 `Update()`、高频脚步事件、网络事件中反复查找。

示例：

```csharp
// 仅作为版本兼容 fallback，不作为主路径。
private static readonly FieldInfo SomeField =
    AccessTools.Field(typeof(SomeGameType), "somePrivateField");
```

## 6. 本地玩家与联机安全

所有玩家事件默认必须做本地过滤：

```csharp
internal static bool IsLocalAvatar(PlayerAvatar avatar)
{
    if (avatar == null)
    {
        return false;
    }

    if (object.ReferenceEquals(avatar, PlayerAvatar.instance))
    {
        return true;
    }

    try
    {
        return avatar.photonView != null && avatar.photonView.IsMine;
    }
    catch
    {
        return false;
    }
}
```

不要做这些事：

- 不要修改其他玩家状态。
- 不要篡改 Photon ownership。
- 不要把本地娱乐效果同步给全房间。
- 不要在公共房间测试不稳定功能。

推荐测试顺序：

1. 单机或训练环境。
2. 私人房间，只有自己。
3. 私人房间，多人同版本。
4. 确认不影响联机状态后再考虑发布。

## 7. 配置与面板

所有可能影响体验或安全的参数都应该可配置：

- 启用/禁用总开关。
- 事件开关。
- 强度上限。
- 持续时间。
- 冷却时间。
- 键位。
- 调试日志。
- 紧急停止键。

默认值应保守。尤其是体感、联动、音频、网络、自动触发类 Mod，应默认提供紧急停止。

配置命名建议：

```text
Safety.Enabled
Safety.EmergencyStopKey
Safety.MaxIntensity
Events.HurtEnabled
Events.DeathEnabled
Events.FootstepEnabled
Connection.Port
Diagnostics.VerboseLogging
```

UI 面板应显示：

- 当前启用状态。
- 当前连接状态。
- 当前 profile / preset。
- 关键事件计数。
- 最近一次触发原因。
- 最近一次拦截原因。
- 紧急停止按钮。

## 8. 日志规范

日志要能回答三个问题：

- Mod 是否加载成功。
- Patch 是否生效。
- 事件为什么触发或为什么被拦截。

推荐日志级别：

- `Info`：启动、绑定、profile 切换、关键状态变化。
- `Warning`：目标方法缺失、配置不合法、连接失败。
- `Error`：不可恢复异常。
- `Debug`：高频事件计数，不默认开启。

不要在每帧或每个脚步都默认刷大量日志。

## 9. 构建与发布

发布包结构建议：

```text
manifest.json
README.md
icon.png
BepInEx/plugins/YourMod/YourMod.dll
BepInEx/plugins/YourMod/YourDependency.dll
```

`icon.png` 建议为 `256x256` PNG。

README 应简短说明：

- Mod 是什么。
- 作者。
- 依赖。
- 快速使用方式。
- 默认键位。
- 已知限制。
- 安全注意事项。

不要把这些内容放进发布包：

- `Assembly-CSharp.dll`
- Unity 游戏原始 DLL
- ripped 游戏资源
- patched Unity 工程
- 反编译导出的游戏源码
- 本地用户配置或 token

## 10. 游戏更新后的维护流程

游戏更新后按顺序检查：

1. 启动游戏，确认 BepInEx 正常加载。
2. 用 ILSpy/dnSpyEx 重新打开新的 `Assembly-CSharp.dll`。
3. 核对所有 Harmony 目标方法是否还存在。
4. 核对参数数量和类型是否变化。
5. 跑一次最小 smoke test。
6. 测试每个核心事件。
7. 更新版本号和 README。
8. 重新打包。

建议维护一个事件表：

```text
PlayerHealth.Hurt
PlayerAvatar.PlayerDeathRPC
PlayerAvatarVisuals.FootstepLight
PlayerAvatarVisuals.FootstepMedium
PlayerAvatarVisuals.FootstepHeavy
PlayerAvatar.Jump
PlayerAvatar.Land
PlayerAvatar.Slide
PlayerController.Update
```

## 11. 最小质量门槛

提交或发布前至少确认：

- 能编译。
- 能启动游戏。
- BepInEx 日志显示插件加载。
- 面板能打开和关闭。
- 紧急停止可用。
- 本地玩家事件能触发。
- 非本地玩家不会误触发。
- 断线、异常、配置错误不会导致游戏崩溃。
- 发布包不包含游戏专有文件。

## 12. 推荐开发流程

```text
确认需求
-> 反编译定位目标方法
-> 记录签名和本地玩家判断
-> 写最小 Harmony patch
-> 转发到自己的控制器
-> 加配置和安全开关
-> 游戏内验证
-> 私房多人验证
-> 清理日志和 README
-> 打包发布
```

核心思想：先让 hook 小而准，再让业务逻辑可配置、可诊断、可停止。
