using BepInEx.Configuration;
using UnityEngine;

namespace RepoCoyoteStim
{
    internal enum SocketConnectionMode
    {
        LocalServer,
        PublicSocket,
        RemoteServer,
        RelayCode
    }

    internal enum StimChannel
    {
        A,
        B
    }

    internal enum StimChannelMode
    {
        A,
        B,
        Both,
        Alternate
    }

    internal enum AutoStrengthMode
    {
        Off,
        MinimumOnly,
        EventScaled
    }

    internal enum StimWaveShape
    {
        Smooth,
        Gradient,
        Sin
    }

    internal enum StimEnvelopeShape
    {
        SingleTap,
        DoubleTap,
        RampUpHoldDown,
        SawBurst,
        Tremor,
        DeathWave
    }

    internal static class ModConfig
    {
        internal static ConfigEntry<bool> AutoStartServer;
        internal static ConfigEntry<SocketConnectionMode> ConnectionMode;
        internal static ConfigEntry<int> Port;
        internal static ConfigEntry<string> AdvertiseHost;
        internal static ConfigEntry<string> PublishSocketUri;
        internal static ConfigEntry<string> RemoteServerUri;
        internal static ConfigEntry<string> RemotePairCode;
        internal static ConfigEntry<int> HeartbeatIntervalMs;

        internal static ConfigEntry<bool> Armed;
        internal static ConfigEntry<bool> AutoArmOnBind;
        internal static ConfigEntry<bool> AllowStrengthControl;
        internal static ConfigEntry<AutoStrengthMode> AutoStrengthMode;
        internal static ConfigEntry<int> MinimumChannelStrength;
        internal static ConfigEntry<int> MaxWaveIntensity;
        internal static ConfigEntry<int> MaxEventDurationMs;
        internal static ConfigEntry<int> MaxQueuedPulseItems;
        internal static ConfigEntry<int> ClearDelayMs;
        internal static ConfigEntry<bool> ContinuousMode;
        internal static ConfigEntry<int> ContinuousSendIntervalMs;
        internal static ConfigEntry<int> ContinuousLookaheadMs;
        internal static ConfigEntry<int> ContinuousFadeOutMs;
        internal static ConfigEntry<KeyCode> PanelKey;
        internal static ConfigEntry<KeyCode> EmergencyStopKey;

        internal static ConfigEntry<bool> HurtEnabled;
        internal static ConfigEntry<bool> DeathEnabled;
        internal static ConfigEntry<bool> LocalFootstepEnabled;
        internal static ConfigEntry<bool> JumpEnabled;
        internal static ConfigEntry<bool> LandEnabled;
        internal static ConfigEntry<bool> SlideEnabled;
        internal static ConfigEntry<bool> LandJumpEnabled;
        internal static ConfigEntry<bool> EnemyFootstepEnabled;

        internal static ConfigEntry<int> HurtCooldownMs;
        internal static ConfigEntry<int> FootstepCooldownMs;
        internal static ConfigEntry<int> JumpCooldownMs;
        internal static ConfigEntry<int> LandCooldownMs;
        internal static ConfigEntry<int> SlideCooldownMs;
        internal static ConfigEntry<int> MovementCooldownMs;
        internal static ConfigEntry<int> DeathCooldownMs;

        internal static ConfigEntry<bool> FirstFootLeft;
        internal static ConfigEntry<StimChannel> LeftFootChannel;
        internal static ConfigEntry<StimChannel> RightFootChannel;
        internal static ConfigEntry<bool> ResetFootOnIdle;
        internal static ConfigEntry<int> FootstepIdleResetMs;
        internal static ConfigEntry<int> FootstepIntensity;
        internal static ConfigEntry<int> FootstepDurationMs;
        internal static ConfigEntry<int> FootstepFrequency;
        internal static ConfigEntry<StimWaveShape> FootstepWaveShape;
        internal static ConfigEntry<int> FootstepDuplicateWindowMs;
        internal static ConfigEntry<int> WalkFootstepMinIntervalMs;
        internal static ConfigEntry<int> RunFootstepIntensity;
        internal static ConfigEntry<int> RunFootstepDurationMs;
        internal static ConfigEntry<int> RunFootstepMinIntervalMs;
        internal static ConfigEntry<bool> FallbackFootstepSupplement;
        internal static ConfigEntry<int> FallbackFootstepNoAnimationMs;
        internal static ConfigEntry<bool> RunCadenceSupplement;
        internal static ConfigEntry<int> RunSupplementIntervalMs;
        internal static ConfigEntry<int> WalkBaseIntensity;
        internal static ConfigEntry<int> WalkBaseFrequency;
        internal static ConfigEntry<StimEnvelopeShape> WalkBaseShape;
        internal static ConfigEntry<int> WalkStepBumpIntensity;
        internal static ConfigEntry<int> WalkStepBumpDurationMs;
        internal static ConfigEntry<int> RunBaseIntensity;
        internal static ConfigEntry<int> RunBaseFrequency;
        internal static ConfigEntry<StimEnvelopeShape> RunBaseShape;
        internal static ConfigEntry<int> RunStepBumpIntensity;
        internal static ConfigEntry<int> RunStepBumpDurationMs;

        internal static ConfigEntry<int> JumpIntensity;
        internal static ConfigEntry<int> JumpDurationMs;
        internal static ConfigEntry<int> JumpFrequency;
        internal static ConfigEntry<StimChannelMode> JumpChannelMode;

        internal static ConfigEntry<int> LandIntensity;
        internal static ConfigEntry<int> LandDurationMs;
        internal static ConfigEntry<int> LandFrequency;
        internal static ConfigEntry<StimChannelMode> LandChannelMode;
        internal static ConfigEntry<StimEnvelopeShape> LandEnvelope;

        internal static ConfigEntry<int> SlideIntensity;
        internal static ConfigEntry<int> SlideDurationMs;
        internal static ConfigEntry<int> SlideFrequency;
        internal static ConfigEntry<StimChannelMode> SlideChannelMode;
        internal static ConfigEntry<StimEnvelopeShape> SlideEnvelope;
        internal static ConfigEntry<int> SlideBaseIntensity;
        internal static ConfigEntry<int> SlideBaseFrequency;

        internal static ConfigEntry<int> HurtMinIntensity;
        internal static ConfigEntry<int> HurtMaxIntensity;
        internal static ConfigEntry<float> HurtDamageMultiplier;
        internal static ConfigEntry<float> HurtAMultiplier;
        internal static ConfigEntry<float> HurtBMultiplier;
        internal static ConfigEntry<int> HurtDurationMinMs;
        internal static ConfigEntry<int> HurtDurationMaxMs;
        internal static ConfigEntry<int> HurtFrequency;
        internal static ConfigEntry<StimChannelMode> HurtChannelMode;
        internal static ConfigEntry<StimEnvelopeShape> HurtEnvelope;

        internal static ConfigEntry<int> DeathIntensity;
        internal static ConfigEntry<int> DeathDurationMs;
        internal static ConfigEntry<int> DeathFrequency;
        internal static ConfigEntry<StimChannelMode> DeathChannelMode;
        internal static ConfigEntry<StimWaveShape> DeathAWaveShape;
        internal static ConfigEntry<StimWaveShape> DeathBWaveShape;
        internal static ConfigEntry<StimEnvelopeShape> DeathEnvelope;
        internal static ConfigEntry<bool> DeathClearBeforePulse;

        internal static ConfigEntry<int> EnemyFootstepIntensity;
        internal static ConfigEntry<float> EnemyFootstepRange;
        internal static ConfigEntry<int> TestIntensity;

        internal static void Bind(ConfigFile config)
        {
            AutoStartServer = config.Bind("Connection", "AutoStartServer", true, "Mod 加载时自动启动 DG-LAB Socket 连接。");
            ConnectionMode = config.Bind("Connection", "ConnectionMode", SocketConnectionMode.LocalServer, "LocalServer=本机监听；PublicSocket=本机监听但二维码使用公网地址；RemoteServer=连接公网 Socket v2 后端；RelayCode=自建 relay code 兼容模式。");
            Port = config.Bind("Connection", "Port", 9999, "本机 DG-LAB App Socket 监听端口。");
            AdvertiseHost = config.Bind("Connection", "AdvertiseHost", "", "局域网二维码里展示给手机连接的电脑 IP/域名。留空时自动选择局域网 IPv4。");
            PublishSocketUri = config.Bind("Connection", "PublishSocketUri", "", "公网/隧道模式二维码使用的 WebSocket 基础地址，例如 wss://ws.example.com。留空时使用局域网地址。");
            RemoteServerUri = config.Bind("Connection", "RemoteServerUri", "", "RemoteServer/RelayCode 模式连接的公网 DG-LAB Socket v2 后端地址，例如 wss://ws.example.com。");
            RemotePairCode = config.Bind("Connection", "RemotePairCode", "", "RelayCode 模式使用的自定义终端码。该功能需要兼容自定义码的 relay 服务，不能直接填写 DG-LAB App 原生远程口令。");
            HeartbeatIntervalMs = config.Bind("Connection", "HeartbeatIntervalMs", 60000, "发送给 DG-LAB App 或远程服务的心跳间隔，单位毫秒。");

            Armed = config.Bind("Safety", "Enabled", false, "为 false 时，游戏事件不会触发波形。可在面板里点击“启用触发”。");
            AutoArmOnBind = config.Bind("Safety", "AutoArmOnBind", true, "绑定 DG-LAB App 后自动启用游戏事件触发。关闭后需手动点击“启用触发”。");
            AllowStrengthControl = config.Bind("Safety", "AllowStrengthControl", false, "兼容旧配置：开启后等同于 EventScaled 自动调强。");
            AutoStrengthMode = config.Bind("Safety", "AutoStrengthMode", RepoCoyoteStim.AutoStrengthMode.EventScaled, "Off=不调强；MinimumOnly=只补到最低可感强度；EventScaled=按事件强度设置通道强度。");
            MinimumChannelStrength = config.Bind("Safety", "MinimumChannelStrength", 30, "MinimumOnly 模式下 A/B 通道会被补到的最低强度。");
            MaxWaveIntensity = config.Bind("Safety", "MaxWaveIntensity", 60, "本 Mod 使用的波形强度上限。DG-LAB 波形强度范围为 0-100。");
            MaxEventDurationMs = config.Bind("Safety", "MaxEventDurationMs", 4000, "单次事件波形最长持续时间，单位毫秒。");
            MaxQueuedPulseItems = config.Bind("Safety", "MaxQueuedPulseItems", 45, "单次事件最多发送多少条 100ms 波形数据。");
            ClearDelayMs = config.Bind("Safety", "ClearDelayMs", 60, "高优先级事件 clear 后等待多久再发送新波形，单位毫秒。");
            ContinuousMode = config.Bind("Safety", "ContinuousMode", true, "开启后使用持续状态机：走路/跑步/滑行期间持续输出基础波形，事件叠加强度。关闭后使用旧版单发事件模式。");
            ContinuousSendIntervalMs = config.Bind("Continuous", "SendIntervalMs", 70, "连续模式补包间隔，单位毫秒。");
            ContinuousLookaheadMs = config.Bind("Continuous", "LookaheadMs", 100, "连续模式每次生成未来多久的波形，单位毫秒。");
            ContinuousFadeOutMs = config.Bind("Continuous", "FadeOutMs", 450, "移动停止后的淡出时间，单位毫秒。");
            PanelKey = config.Bind("Safety", "PanelKey", KeyCode.P, "打开/关闭游戏内郊狼连接面板。");
            EmergencyStopKey = config.Bind("Safety", "EmergencyStopKey", KeyCode.I, "清空 A/B 波形队列并将 A/B 通道强度设为 0。");

            HurtEnabled = config.Bind("Events", "HurtEnabled", true, "本地玩家受击时触发波形。");
            DeathEnabled = config.Bind("Events", "DeathEnabled", true, "本地玩家死亡时触发波形。");
            LocalFootstepEnabled = config.Bind("Events", "LocalFootstepEnabled", true, "本地玩家脚步时触发低强度波形。");
            JumpEnabled = config.Bind("Events", "JumpEnabled", true, "本地玩家跳跃时触发低强度波形。");
            LandEnabled = config.Bind("Events", "LandEnabled", true, "本地玩家落地时触发低强度波形。");
            SlideEnabled = config.Bind("Events", "SlideEnabled", true, "本地玩家滑行时触发低强度波形。");
            LandJumpEnabled = config.Bind("Events", "LandJumpEnabled", true, "兼容旧配置：总开关，关闭后跳跃、落地、滑行都不触发。");
            EnemyFootstepEnabled = config.Bind("Events", "EnemyFootstepEnabled", false, "猎人敌人近距离脚步提示，默认关闭。");

            HurtCooldownMs = config.Bind("Timing", "HurtCooldownMs", 450, "两次受击波形之间的最短间隔，单位毫秒。");
            FootstepCooldownMs = config.Bind("Timing", "FootstepCooldownMs", 120, "两次脚步波形之间的最短间隔，单位毫秒。");
            JumpCooldownMs = config.Bind("Timing", "JumpCooldownMs", 250, "两次跳跃波形之间的最短间隔，单位毫秒。");
            LandCooldownMs = config.Bind("Timing", "LandCooldownMs", 250, "两次落地波形之间的最短间隔，单位毫秒。");
            SlideCooldownMs = config.Bind("Timing", "SlideCooldownMs", 300, "两次滑行波形之间的最短间隔，单位毫秒。");
            MovementCooldownMs = config.Bind("Timing", "MovementCooldownMs", 300, "兼容旧配置：旧版跳跃/落地/滑行冷却。");
            DeathCooldownMs = config.Bind("Timing", "DeathCooldownMs", 5000, "两次死亡波形之间的最短间隔，单位毫秒。");

            FirstFootLeft = config.Bind("Footstep", "FirstFootLeft", true, "站停重置后第一步是否按左脚处理。默认左脚。");
            LeftFootChannel = config.Bind("Footstep", "LeftFootChannel", StimChannel.A, "左脚对应通道。默认 A。");
            RightFootChannel = config.Bind("Footstep", "RightFootChannel", StimChannel.B, "右脚对应通道。默认 B。");
            ResetFootOnIdle = config.Bind("Footstep", "ResetFootOnIdle", true, "长时间没有脚步后，下一步是否重置为 FirstFootLeft。");
            FootstepIdleResetMs = config.Bind("Footstep", "FootstepIdleResetMs", 900, "多久没有脚步后重置左右脚，单位毫秒。");
            FootstepIntensity = config.Bind("Footstep", "FootstepIntensity", 20, "本地脚步波形强度。");
            FootstepDurationMs = config.Bind("Footstep", "FootstepDurationMs", 250, "本地脚步波形持续时间，单位毫秒。");
            FootstepFrequency = config.Bind("Footstep", "FootstepFrequency", 180, "脚步波形频率值。");
            FootstepWaveShape = config.Bind("Footstep", "FootstepWaveShape", StimWaveShape.Sin, "脚步波形形状。Sin 更像短促踩踏，Smooth 更平滑。");
            FootstepDuplicateWindowMs = config.Bind("Footstep", "FootstepDuplicateWindowMs", 35, "极短时间内重复脚步事件的去重窗口，单位毫秒。");
            WalkFootstepMinIntervalMs = config.Bind("Footstep", "WalkFootstepMinIntervalMs", 90, "走路脚步最小发送间隔，单位毫秒。");
            RunFootstepIntensity = config.Bind("Footstep", "RunFootstepIntensity", 24, "奔跑脚步波形强度。");
            RunFootstepDurationMs = config.Bind("Footstep", "RunFootstepDurationMs", 140, "奔跑脚步波形持续时间，单位毫秒。");
            RunFootstepMinIntervalMs = config.Bind("Footstep", "RunFootstepMinIntervalMs", 55, "奔跑脚步最小发送间隔，单位毫秒。");
            FallbackFootstepSupplement = config.Bind("Footstep", "FallbackFootstepSupplement", false, "动画脚步长时间没有触发时，是否允许 PlayerAvatar.Footstep 兜底补发。默认关闭。");
            FallbackFootstepNoAnimationMs = config.Bind("Footstep", "FallbackFootstepNoAnimationMs", 450, "多久没有动画脚步后允许兜底补发，单位毫秒。");
            RunCadenceSupplement = config.Bind("Footstep", "RunCadenceSupplement", false, "奔跑时如果动画脚步偏慢，是否按间隔补一拍。默认关闭。");
            RunSupplementIntervalMs = config.Bind("Footstep", "RunSupplementIntervalMs", 170, "奔跑补拍间隔，单位毫秒。");
            WalkBaseIntensity = config.Bind("ContinuousWalk", "WalkBaseIntensity", 10, "走路持续基础波形强度。");
            WalkBaseFrequency = config.Bind("ContinuousWalk", "WalkBaseFrequency", 25, "走路持续基础波形频率。");
            WalkBaseShape = config.Bind("ContinuousWalk", "WalkBaseShape", StimEnvelopeShape.Tremor, "走路持续基础波形形状。");
            WalkStepBumpIntensity = config.Bind("ContinuousWalk", "WalkStepBumpIntensity", 16, "走路每步叠加强度。");
            WalkStepBumpDurationMs = config.Bind("ContinuousWalk", "WalkStepBumpDurationMs", 160, "走路每步叠加强度持续时间。");
            RunBaseIntensity = config.Bind("ContinuousRun", "RunBaseIntensity", 15, "奔跑持续基础波形强度。");
            RunBaseFrequency = config.Bind("ContinuousRun", "RunBaseFrequency", 260, "奔跑持续基础波形频率。");
            RunBaseShape = config.Bind("ContinuousRun", "RunBaseShape", StimEnvelopeShape.SawBurst, "奔跑持续基础波形形状。");
            RunStepBumpIntensity = config.Bind("ContinuousRun", "RunStepBumpIntensity", 22, "奔跑每步叠加强度。");
            RunStepBumpDurationMs = config.Bind("ContinuousRun", "RunStepBumpDurationMs", 110, "奔跑每步叠加强度持续时间。");

            JumpIntensity = config.Bind("Actions", "JumpIntensity", 18, "跳跃波形强度。");
            JumpDurationMs = config.Bind("Actions", "JumpDurationMs", 250, "跳跃波形持续时间，单位毫秒。");
            JumpFrequency = config.Bind("Actions", "JumpFrequency", 260, "跳跃波形频率值。");
            JumpChannelMode = config.Bind("Actions", "JumpChannelMode", StimChannelMode.Both, "跳跃触发通道。");

            LandIntensity = config.Bind("Actions", "LandIntensity", 22, "落地波形强度。");
            LandDurationMs = config.Bind("Actions", "LandDurationMs", 300, "落地波形持续时间，单位毫秒。");
            LandFrequency = config.Bind("Actions", "LandFrequency", 300, "落地波形频率值。");
            LandChannelMode = config.Bind("Actions", "LandChannelMode", StimChannelMode.Both, "落地触发通道。");
            LandEnvelope = config.Bind("Actions", "LandEnvelope", StimEnvelopeShape.SingleTap, "落地包络波形。");

            SlideIntensity = config.Bind("Actions", "SlideIntensity", 16, "滑行波形强度。");
            SlideDurationMs = config.Bind("Actions", "SlideDurationMs", 300, "滑行波形持续时间，单位毫秒。");
            SlideFrequency = config.Bind("Actions", "SlideFrequency", 220, "滑行波形频率值。");
            SlideChannelMode = config.Bind("Actions", "SlideChannelMode", StimChannelMode.Both, "滑行触发通道。");
            SlideEnvelope = config.Bind("Actions", "SlideEnvelope", StimEnvelopeShape.Tremor, "滑行包络波形。");
            SlideBaseIntensity = config.Bind("Actions", "SlideBaseIntensity", 18, "连续模式下滑行基础颤动强度。");
            SlideBaseFrequency = config.Bind("Actions", "SlideBaseFrequency", 240, "连续模式下滑行基础颤动频率。");

            HurtMinIntensity = config.Bind("Hurt", "HurtMinIntensity", 18, "受击波形基础强度。");
            HurtMaxIntensity = config.Bind("Hurt", "HurtMaxIntensity", 60, "受击波形最大强度。");
            HurtDamageMultiplier = config.Bind("Hurt", "HurtDamageMultiplier", 3.0f, "受击伤害换算为波形强度的倍率。");
            HurtAMultiplier = config.Bind("Hurt", "HurtAMultiplier", 1.0f, "受击 A 通道倍率。");
            HurtBMultiplier = config.Bind("Hurt", "HurtBMultiplier", 0.7f, "受击 B 通道倍率。");
            HurtDurationMinMs = config.Bind("Hurt", "HurtDurationMinMs", 1500, "受击波形最短持续时间，单位毫秒。");
            HurtDurationMaxMs = config.Bind("Hurt", "HurtDurationMaxMs", 3500, "受击波形最长持续时间，单位毫秒。");
            HurtFrequency = config.Bind("Hurt", "HurtFrequency", 60, "受击波形频率值。");
            HurtChannelMode = config.Bind("Hurt", "HurtChannelMode", StimChannelMode.Both, "受击触发通道。");
            HurtEnvelope = config.Bind("Hurt", "HurtEnvelope", StimEnvelopeShape.RampUpHoldDown, "受击包络波形。");

            DeathIntensity = config.Bind("Death", "DeathIntensity", 45, "死亡波形强度。");
            DeathDurationMs = config.Bind("Death", "DeathDurationMs", 4000, "死亡波形持续时间，单位毫秒。");
            DeathFrequency = config.Bind("Death", "DeathFrequency", 150, "死亡波形频率值。");
            DeathChannelMode = config.Bind("Death", "DeathChannelMode", StimChannelMode.Both, "死亡触发通道。");
            DeathAWaveShape = config.Bind("Death", "DeathAWaveShape", StimWaveShape.Sin, "死亡 A 通道波形。");
            DeathBWaveShape = config.Bind("Death", "DeathBWaveShape", StimWaveShape.Gradient, "死亡 B 通道波形。");
            DeathEnvelope = config.Bind("Death", "DeathEnvelope", StimEnvelopeShape.DeathWave, "死亡包络波形。");
            DeathClearBeforePulse = config.Bind("Death", "DeathClearBeforePulse", true, "死亡触发前是否清空 A/B 旧波形队列。");

            EnemyFootstepIntensity = config.Bind("Enemy", "EnemyFootstepIntensity", 10, "敌人脚步提示波形强度。");
            EnemyFootstepRange = config.Bind("Enemy", "EnemyFootstepRange", 12f, "敌人脚步提示触发范围，单位为 Unity 距离。");
            TestIntensity = config.Bind("Waveform", "TestIntensity", 20, "面板测试波形强度。");

            MigrateOldSoftDefaults();
            config.Save();
        }

        private static void MigrateOldSoftDefaults()
        {
            if (MinimumChannelStrength.Value <= 20 && MaxWaveIntensity.Value > 40)
            {
                MinimumChannelStrength.Value = 30;
            }

            if (MaxEventDurationMs.Value <= 2200)
            {
                MaxEventDurationMs.Value = 4000;
            }

            if (MaxQueuedPulseItems.Value <= 30)
            {
                MaxQueuedPulseItems.Value = 45;
            }

            if (ContinuousSendIntervalMs.Value >= 100)
            {
                ContinuousSendIntervalMs.Value = 70;
            }

            if (ContinuousLookaheadMs.Value > 150)
            {
                ContinuousLookaheadMs.Value = 100;
            }

            if (ClearDelayMs.Value >= 150)
            {
                ClearDelayMs.Value = 60;
            }

            if (WalkBaseFrequency.Value >= 180)
            {
                WalkBaseFrequency.Value = 25;
            }

            if (HurtDurationMinMs.Value < 1500)
            {
                HurtDurationMinMs.Value = 1500;
            }

            if (HurtDurationMaxMs.Value <= 2200)
            {
                HurtDurationMaxMs.Value = 3500;
            }

            if (HurtFrequency.Value >= 100)
            {
                HurtFrequency.Value = 60;
            }

            if (DeathDurationMs.Value <= 2200)
            {
                DeathDurationMs.Value = 4000;
            }

            if (DeathFrequency.Value >= 160)
            {
                DeathFrequency.Value = 150;
            }

            if (FootstepIntensity.Value <= 10)
            {
                FootstepIntensity.Value = 20;
            }

            if (FootstepDurationMs.Value <= 150)
            {
                FootstepDurationMs.Value = 250;
            }

            if (AutoStrengthMode.Value == RepoCoyoteStim.AutoStrengthMode.MinimumOnly && AllowStrengthControl.Value)
            {
                AutoStrengthMode.Value = RepoCoyoteStim.AutoStrengthMode.EventScaled;
            }
        }
    }
}
