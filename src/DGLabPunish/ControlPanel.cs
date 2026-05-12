using System;
using BepInEx.Configuration;
using Net.Codecrete.QrCodeGenerator;
using UnityEngine;

namespace DGLabPunish
{
    internal sealed class ControlPanel
    {
        private readonly DGLabSocketServer _server;
        private readonly StimController _stim;
        private Rect _window;
        private Vector2 _scroll;
        private Texture2D _qrTexture;
        private string _qrText;
        private bool _visible;
        private bool _cursorSaved;
        private bool _previousCursorVisible;
        private CursorLockMode _previousLockState;
        private bool _draftsInitialized;
        private string _hostDraft;
        private string _portDraft;
        private string _publishUriDraft;
        private string _remoteUriDraft;
        private string _remoteCodeDraft;
        private string _panelKeyDraft;
        private string _stopKeyDraft;
        private string _settingsMessage;
        private int _panelTab;
        private int _selectedProfileIndex;
        private int _selectedWaveIndex;
        private string _manualStrengthA;
        private string _manualStrengthB;

        internal bool Visible
        {
            get { return _visible; }
        }

        internal ControlPanel(DGLabSocketServer server, StimController stim)
        {
            _server = server;
            _stim = stim;
            _window = new Rect(40, 40, 560, 760);
            _scroll = Vector2.zero;
            _visible = false;
            _settingsMessage = "";
            _panelTab = 0;
            _selectedProfileIndex = 0;
            _selectedWaveIndex = 0;
            _manualStrengthA = "20";
            _manualStrengthB = "20";
        }

        internal void ToggleVisible()
        {
            SetVisible(!_visible);
        }

        internal void SetVisible(bool visible)
        {
            if (_visible == visible)
            {
                return;
            }

            _visible = visible;
            if (_visible)
            {
                InitDrafts();
                _previousCursorVisible = Cursor.visible;
                _previousLockState = Cursor.lockState;
                _cursorSaved = true;
                UnlockCursor();
            }
            else if (_cursorSaved)
            {
                Cursor.visible = _previousCursorVisible;
                Cursor.lockState = _previousLockState;
                _cursorSaved = false;
            }
        }

        internal void UpdateCursor()
        {
            if (_visible)
            {
                UnlockCursor();
            }
        }

        private static void UnlockCursor()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        internal void Draw()
        {
            if (!_visible)
            {
                return;
            }

            float maxWidth = Mathf.Max(420f, Screen.width - 20f);
            float maxHeight = Mathf.Max(360f, Screen.height * 0.85f);
            _window.width = Mathf.Min(_window.width, maxWidth);
            _window.height = Mathf.Min(_window.height, maxHeight);
            if (_window.xMax > Screen.width) _window.x = Mathf.Max(0f, Screen.width - _window.width - 10f);
            if (_window.yMax > Screen.height) _window.y = Mathf.Max(0f, Screen.height - _window.height - 10f);

            _window = GUI.Window(88991, _window, DrawWindow, "郊狼 3.0 连接面板");
        }

        private void DrawWindow(int id)
        {
            InitDrafts();
            _scroll = GUILayout.BeginScrollView(_scroll, false, true, GUILayout.Height(Mathf.Max(260f, _window.height - 34f)));

            DrawTabs();
            if (_panelTab == 0)
            {
                DrawClientPage();
                FinishWindow();
                return;
            }
            if (_panelTab == 1)
            {
                DrawWaveLibraryPage();
                FinishWindow();
                return;
            }
            if (_panelTab == 2)
            {
                DrawEventMappingPage();
                FinishWindow();
                return;
            }
            if (_panelTab == 3)
            {
                DrawImportExportPage();
                FinishWindow();
                return;
            }

            Section("连接");
            GUILayout.Label("协议：DG-LAB 郊狼 3.0 App Socket");
            GUILayout.Label("模式：" + _server.ConnectionModeText());
            GUILayout.Label("状态：" + _server.StatusText());
            if (!string.IsNullOrEmpty(_server.LastRemoteEndpoint))
            {
                GUILayout.Label("连接来源/远程地址：" + _server.LastRemoteEndpoint);
            }

            EnumCycle("连接模式", ModConfig.ConnectionMode);
            GUILayout.Label("二维码地址（用于核对）：");
            GUILayout.TextArea(_server.GetQrUrl(), GUILayout.Height(48));

            EnsureQrTexture(_server.GetQrUrl());
            if (_qrTexture != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Box(_qrTexture, GUILayout.Width(180), GUILayout.Height(180));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.Label("检测到的电脑 IPv4：" + _server.LocalAddressSummary());
            string[] addresses = _server.GetLocalIPv4Addresses();
            if (addresses.Length > 0)
            {
                GUILayout.BeginHorizontal();
                for (int i = 0; i < addresses.Length && i < 3; i++)
                {
                    if (GUILayout.Button("使用 " + addresses[i]))
                    {
                        _hostDraft = addresses[i];
                    }
                }
                GUILayout.EndHorizontal();
            }

            TextRow("局域网 IP/域名", ref _hostDraft);
            TextRow("本地端口", ref _portDraft);
            TextRow("公网 Socket URI", ref _publishUriDraft);
            TextRow("远程 Socket URI", ref _remoteUriDraft);
            TextRow("自建 Relay Code", ref _remoteCodeDraft);
            GUILayout.Label("说明：Relay Code 不是 DG-LAB App 原生远程口令，需要你自己的兼容 relay 服务。优先用公网/远程 Socket。");

            TextRow("面板键", ref _panelKeyDraft);
            TextRow("急停键", ref _stopKeyDraft);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("保存连接/键位并重启"))
            {
                SavePanelSettings(true);
            }

            if (GUILayout.Button(_server.IsRunning ? "重启服务" : "启动服务"))
            {
                _server.Stop();
                _server.Start();
                RefreshQr();
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("停止服务"))
            {
                _server.Stop();
            }

            if (!string.IsNullOrEmpty(_settingsMessage))
            {
                GUILayout.Label(_settingsMessage);
            }

            Section("状态与测试");
            GUILayout.Label("触发总开关：" + (ModConfig.Armed.Value ? "已启用" : "未启用") + "    绑定后自动启用：" + (ModConfig.AutoArmOnBind.Value ? "开启" : "关闭"));
            if (!ModConfig.Armed.Value)
            {
                GUILayout.Label("注意：真实游戏事件需要启用触发；下方模拟按钮会绕过此开关，只用于测试波形。");
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(ModConfig.Armed.Value ? "解除触发" : "启用触发"))
            {
                ModConfig.Armed.Value = !ModConfig.Armed.Value;
                SaveConfig();
            }

            if (GUILayout.Button("紧急停止"))
            {
                _stim.EmergencyStop();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("测试 A")) _stim.SendTest('A');
            if (GUILayout.Button("测试 B")) _stim.SendTest('B');
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("模拟左脚")) _stim.TriggerFootstepForced(true);
            if (GUILayout.Button("模拟右脚")) _stim.TriggerFootstepForced(false);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("模拟奔跑左脚")) _stim.SimulateRunFootstep(true);
            if (GUILayout.Button("模拟奔跑右脚")) _stim.SimulateRunFootstep(false);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("模拟持续走路 2秒")) _stim.SimulateContinuousMovement(false);
            if (GUILayout.Button("模拟持续奔跑 2秒")) _stim.SimulateContinuousMovement(true);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("模拟跳跃")) _stim.SimulateJump();
            if (GUILayout.Button("模拟落地")) _stim.SimulateLand();
            if (GUILayout.Button("模拟滑行")) _stim.SimulateSlide();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("模拟包络受击")) _stim.SimulateHurt(10);
            if (GUILayout.Button("模拟死亡")) _stim.SimulateDeath();
            GUILayout.EndHorizontal();

            StrengthState strength = _server.Strength;
            GUILayout.Label("A 强度：" + strength.ACurrent + " / " + strength.AMax + "    B 强度：" + strength.BCurrent + " / " + strength.BMax);
            if ((strength.ACurrent == 0 || strength.BCurrent == 0) && ModConfig.AutoStrengthMode.Value == AutoStrengthMode.Off && !ModConfig.AllowStrengthControl.Value)
            {
                GUILayout.Label("提示：有通道当前强度为 0，且自动调强关闭；该通道可能没有体感。");
            }

            StimDiagnostics d = _stim.Diagnostics;
            GUILayout.Label("连续：" + d.ContinuousState + "    补包：" + d.ContinuousRefillCount + "    队列估算：" + d.EstimatedQueueMs + "ms    Gen：" + d.ContinuousGeneration);
            GUILayout.Label("Overlay：" + d.ActiveOverlays);
            GUILayout.Label("连续强度：A 基础 " + d.ContinuousBaseA + " + " + d.ContinuousOverlayA + " / B 基础 " + d.ContinuousBaseB + " + " + d.ContinuousOverlayB);
            GUILayout.Label("步态：" + d.MovementState + "    来源：" + d.LastFootstepSource + "    间隔：" + d.LastFootstepIntervalMs + "ms");
            GUILayout.Label("计数：左脚 " + d.LeftFootsteps + " / 右脚 " + d.RightFootsteps + " / 动画脚步 " + d.AnimationFootsteps + " / 兜底 " + d.FallbackFootsteps);
            GUILayout.Label("去重：" + d.DuplicateFootsteps + " / 奔跑补拍 " + d.RunSupplementFootsteps + " / 左右间隔 " + d.LastLeftRightDeltaMs + "ms");
            GUILayout.Label("动作：跳跃 " + d.JumpCount + " / 落地 " + d.LandCount + " / 滑行 " + d.SlideCount + " / 敌脚 " + d.EnemyFootsteps);
            GUILayout.Label("事件：受击 " + d.HurtCount + " / 死亡 " + d.DeathCount);
            GUILayout.Label("最后事件：" + d.LastEvent + "    通道：" + d.LastChannel + "    结果：" + d.LastCommandResult);
            GUILayout.Label("最后拦截：" + d.LastBlockedReason);
            GUILayout.Label("下一步：" + (d.NextFootLeft ? "左脚" : "右脚"));
            if (GUILayout.Button("重置下一步为配置的第一脚"))
            {
                _stim.ResetFootToConfiguredFirst();
            }

            Section("事件开关");
            Toggle("本地玩家受击触发", ModConfig.HurtEnabled);
            Toggle("本地玩家死亡触发", ModConfig.DeathEnabled);
            Toggle("本地玩家脚步触发", ModConfig.LocalFootstepEnabled);
            Toggle("跳跃触发", ModConfig.JumpEnabled);
            Toggle("落地触发", ModConfig.LandEnabled);
            Toggle("滑行触发", ModConfig.SlideEnabled);
            Toggle("跳跃/落地/滑行总开关", ModConfig.LandJumpEnabled);
            Toggle("猎人脚步近距离提示", ModConfig.EnemyFootstepEnabled);

            Section("安全与自动强度");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("舒适持续")) ApplyComfortPreset();
            if (GUILayout.Button("标准游戏")) ApplyPreset(20, 24, 30, 45, 60, 30, 65);
            if (GUILayout.Button("强惩罚")) ApplyPreset(28, 36, 42, 65, 85, 35, 90);
            if (GUILayout.Button("调试同步")) ApplyPreset(36, 45, 55, 70, 90, 35, 90);
            GUILayout.EndHorizontal();
            Toggle("连续模式", ModConfig.ContinuousMode);
            IntSlider("补包间隔 ms", ModConfig.ContinuousSendIntervalMs, 50, 1000);
            IntSlider("Lookahead ms", ModConfig.ContinuousLookaheadMs, 100, 1500);
            IntSlider("停止淡出 ms", ModConfig.ContinuousFadeOutMs, 0, 1500);
            Toggle("绑定后自动启用触发", ModConfig.AutoArmOnBind);
            EnumCycle("自动调强模式", ModConfig.AutoStrengthMode);
            Toggle("兼容旧自动调强开关", ModConfig.AllowStrengthControl);
            IntSlider("最低可感强度", ModConfig.MinimumChannelStrength, 0, 80);
            IntSlider("总强度上限", ModConfig.MaxWaveIntensity, 1, 100);
            IntSlider("测试强度", ModConfig.TestIntensity, 1, 100);
            IntSlider("最长持续 ms", ModConfig.MaxEventDurationMs, 100, 6000);
            IntSlider("Clear 延迟 ms", ModConfig.ClearDelayMs, 0, 1000);

            Section("脚步");
            Toggle("第一步为左脚", ModConfig.FirstFootLeft);
            EnumCycle("左脚通道", ModConfig.LeftFootChannel);
            EnumCycle("右脚通道", ModConfig.RightFootChannel);
            Toggle("站停后重置左右脚", ModConfig.ResetFootOnIdle);
            IntSlider("站停重置 ms", ModConfig.FootstepIdleResetMs, 100, 3000);
            IntSlider("脚步强度", ModConfig.FootstepIntensity, 1, 50);
            IntSlider("脚步持续 ms", ModConfig.FootstepDurationMs, 25, 600);
            IntSlider("脚步频率", ModConfig.FootstepFrequency, 10, 1000);
            EnumCycle("脚步波形", ModConfig.FootstepWaveShape);
            IntSlider("走路基础强度", ModConfig.WalkBaseIntensity, 0, 50);
            IntSlider("走路基础频率", ModConfig.WalkBaseFrequency, 10, 1000);
            EnumCycle("走路基础波形", ModConfig.WalkBaseShape);
            IntSlider("走路脚步增量", ModConfig.WalkStepBumpIntensity, 0, 60);
            IntSlider("走路增量持续 ms", ModConfig.WalkStepBumpDurationMs, 25, 600);
            IntSlider("动画去重 ms", ModConfig.FootstepDuplicateWindowMs, 0, 200);
            IntSlider("走路最小间隔 ms", ModConfig.WalkFootstepMinIntervalMs, 20, 500);
            IntSlider("奔跑基础强度", ModConfig.RunBaseIntensity, 0, 60);
            IntSlider("奔跑基础频率", ModConfig.RunBaseFrequency, 10, 1000);
            EnumCycle("奔跑基础波形", ModConfig.RunBaseShape);
            IntSlider("奔跑脚步强度", ModConfig.RunFootstepIntensity, 1, 80);
            IntSlider("奔跑持续 ms", ModConfig.RunFootstepDurationMs, 25, 600);
            IntSlider("奔跑脚步增量", ModConfig.RunStepBumpIntensity, 0, 80);
            IntSlider("奔跑增量持续 ms", ModConfig.RunStepBumpDurationMs, 25, 600);
            IntSlider("奔跑最小间隔 ms", ModConfig.RunFootstepMinIntervalMs, 20, 500);
            Toggle("兜底脚步补发", ModConfig.FallbackFootstepSupplement);
            IntSlider("兜底等待 ms", ModConfig.FallbackFootstepNoAnimationMs, 100, 2000);
            Toggle("奔跑节奏补拍", ModConfig.RunCadenceSupplement);
            IntSlider("奔跑补拍间隔 ms", ModConfig.RunSupplementIntervalMs, 55, 500);

            Section("动作");
            EnumCycle("跳跃通道", ModConfig.JumpChannelMode);
            IntSlider("跳跃强度", ModConfig.JumpIntensity, 1, 80);
            IntSlider("跳跃持续 ms", ModConfig.JumpDurationMs, 25, 1000);
            IntSlider("跳跃频率", ModConfig.JumpFrequency, 10, 1000);
            IntSlider("跳跃冷却 ms", ModConfig.JumpCooldownMs, 40, 2000);

            EnumCycle("落地通道", ModConfig.LandChannelMode);
            IntSlider("落地强度", ModConfig.LandIntensity, 1, 80);
            IntSlider("落地持续 ms", ModConfig.LandDurationMs, 25, 1000);
            IntSlider("落地频率", ModConfig.LandFrequency, 10, 1000);
            EnumCycle("落地包络", ModConfig.LandEnvelope);
            IntSlider("落地冷却 ms", ModConfig.LandCooldownMs, 40, 2000);

            EnumCycle("滑行通道", ModConfig.SlideChannelMode);
            IntSlider("滑行强度", ModConfig.SlideIntensity, 1, 80);
            IntSlider("滑行持续 ms", ModConfig.SlideDurationMs, 25, 1000);
            IntSlider("滑行频率", ModConfig.SlideFrequency, 10, 1000);
            EnumCycle("滑行包络", ModConfig.SlideEnvelope);
            IntSlider("滑行冷却 ms", ModConfig.SlideCooldownMs, 40, 2000);

            Section("受击");
            EnumCycle("受击通道", ModConfig.HurtChannelMode);
            IntSlider("受击基础强度", ModConfig.HurtMinIntensity, 1, 100);
            IntSlider("受击最大强度", ModConfig.HurtMaxIntensity, 1, 100);
            FloatSlider("伤害倍率", ModConfig.HurtDamageMultiplier, 0f, 10f);
            FloatSlider("A 通道倍率", ModConfig.HurtAMultiplier, 0f, 2f);
            FloatSlider("B 通道倍率", ModConfig.HurtBMultiplier, 0f, 2f);
            IntSlider("受击最短 ms", ModConfig.HurtDurationMinMs, 25, 6000);
            IntSlider("受击最长 ms", ModConfig.HurtDurationMaxMs, 25, 6000);
            IntSlider("受击频率", ModConfig.HurtFrequency, 10, 1000);
            EnumCycle("受击包络", ModConfig.HurtEnvelope);
            IntSlider("受击冷却 ms", ModConfig.HurtCooldownMs, 100, 3000);

            Section("死亡");
            EnumCycle("死亡通道", ModConfig.DeathChannelMode);
            EnumCycle("死亡 A 波形", ModConfig.DeathAWaveShape);
            EnumCycle("死亡 B 波形", ModConfig.DeathBWaveShape);
            EnumCycle("死亡包络", ModConfig.DeathEnvelope);
            Toggle("死亡前清空旧队列", ModConfig.DeathClearBeforePulse);
            IntSlider("死亡强度", ModConfig.DeathIntensity, 1, 100);
            IntSlider("死亡持续 ms", ModConfig.DeathDurationMs, 100, 6000);
            IntSlider("死亡频率", ModConfig.DeathFrequency, 10, 1000);
            IntSlider("死亡冷却 ms", ModConfig.DeathCooldownMs, 500, 10000);

            Section("说明");
            GUILayout.Label("电脑不需要蓝牙。手机连接郊狼 3.0，Mod 只负责 Socket。");
            GUILayout.Label("公网/远程模式请使用可被手机访问的 ws:// 或 wss:// 地址。正式远程建议 wss。");
            GUILayout.Label("DG-LAB App 原生远程口令不是 Socket v2 API，本 Mod 不把它作为稳定主线接入。");

            FinishWindow();
        }

        private void FinishWindow()
        {
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        private void DrawTabs()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_panelTab == 0, "客户端", GUI.skin.button)) _panelTab = 0;
            if (GUILayout.Toggle(_panelTab == 1, "波形库", GUI.skin.button)) _panelTab = 1;
            if (GUILayout.Toggle(_panelTab == 2, "事件映射", GUI.skin.button)) _panelTab = 2;
            if (GUILayout.Toggle(_panelTab == 3, "导入/导出", GUI.skin.button)) _panelTab = 3;
            if (GUILayout.Toggle(_panelTab == 4, "事件参数", GUI.skin.button)) _panelTab = 4;
            GUILayout.EndHorizontal();
        }

        private void DrawClientPage()
        {
            Section("Socket 控制端");
            GUILayout.Label("协议：DG-LAB 郊狼 3.0 App Socket v2。电脑不需要蓝牙，手机 App 负责蓝牙连接主机。");
            GUILayout.Label("模式：" + _server.ConnectionModeText());
            GUILayout.Label("状态：" + _server.StatusText());
            if (!string.IsNullOrEmpty(_server.LastRemoteEndpoint))
            {
                GUILayout.Label("连接来源/远程地址：" + _server.LastRemoteEndpoint);
            }
            if (!string.IsNullOrEmpty(_server.LastError))
            {
                GUILayout.Label("最后错误：" + _server.LastError);
            }

            GUILayout.Label("二维码地址：");
            GUILayout.TextArea(_server.GetQrUrl(), GUILayout.Height(48));
            EnsureQrTexture(_server.GetQrUrl());
            if (_qrTexture != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Box(_qrTexture, GUILayout.Width(180), GUILayout.Height(180));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.Label("检测到的电脑 IPv4：" + _server.LocalAddressSummary());
            string[] addresses = _server.GetLocalIPv4Addresses();
            if (addresses.Length > 0)
            {
                GUILayout.BeginHorizontal();
                for (int i = 0; i < addresses.Length && i < 3; i++)
                {
                    if (GUILayout.Button("使用 " + addresses[i]))
                    {
                        _hostDraft = addresses[i];
                    }
                }
                GUILayout.EndHorizontal();
            }

            EnumCycle("连接模式", ModConfig.ConnectionMode);
            TextRow("局域网 IP/域名", ref _hostDraft);
            TextRow("本地端口", ref _portDraft);
            TextRow("公网 Socket URI", ref _publishUriDraft);
            TextRow("远程 Socket URI", ref _remoteUriDraft);
            TextRow("自建 Relay Code", ref _remoteCodeDraft);
            GUILayout.Label("Relay Code 是本 Mod 自建 relay 口令，不是 DG-LAB App 原生远程口令。");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("保存连接/键位并重启")) SavePanelSettings(true);
            if (GUILayout.Button(_server.IsRunning ? "重启服务" : "启动服务"))
            {
                _server.Stop();
                _server.Start();
                RefreshQr();
            }
            if (GUILayout.Button("停止服务")) _server.Stop();
            GUILayout.EndHorizontal();

            Section("强度控制");
            StrengthState strength = _server.Strength;
            GUILayout.Label("A 当前/上限：" + strength.ACurrent + " / " + strength.AMax + "    B 当前/上限：" + strength.BCurrent + " / " + strength.BMax);
            TextRow("手动 A 强度", ref _manualStrengthA);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("设置 A")) SetManualStrength(1, _manualStrengthA);
            if (GUILayout.Button("A -5")) AdjustStrength(1, -5);
            if (GUILayout.Button("A +5")) AdjustStrength(1, 5);
            if (GUILayout.Button("Clear A")) _server.ClearChannel(1);
            GUILayout.EndHorizontal();

            TextRow("手动 B 强度", ref _manualStrengthB);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("设置 B")) SetManualStrength(2, _manualStrengthB);
            if (GUILayout.Button("B -5")) AdjustStrength(2, -5);
            if (GUILayout.Button("B +5")) AdjustStrength(2, 5);
            if (GUILayout.Button("Clear B")) _server.ClearChannel(2);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("启用触发")) { ModConfig.Armed.Value = true; SaveConfig(); }
            if (GUILayout.Button("解除触发")) { ModConfig.Armed.Value = false; SaveConfig(); }
            if (GUILayout.Button("紧急停止")) _stim.EmergencyStop();
            GUILayout.EndHorizontal();

            Section("快速测试");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("测试 A")) _stim.SendTest('A');
            if (GUILayout.Button("测试 B")) _stim.SendTest('B');
            GUILayout.EndHorizontal();
            WavePreset selected = SelectedWave();
            GUILayout.Label("选中波形：" + (selected == null ? "无" : StimProfileManager.DisplayName(selected)));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("播放选中波形 A")) StimProfileManager.SendWave(_server, selected, 'A');
            if (GUILayout.Button("播放选中波形 B")) StimProfileManager.SendWave(_server, selected, 'B');
            if (GUILayout.Button("Clear A/B")) _server.ClearAll();
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_settingsMessage)) GUILayout.Label(_settingsMessage);
            if (!string.IsNullOrEmpty(StimProfileManager.LastMessage)) GUILayout.Label(StimProfileManager.LastMessage);
        }

        private void DrawWaveLibraryPage()
        {
            Section("波形包");
            DrawProfileSelector();
            StimProfile profile = SelectedProfile();
            if (profile == null)
            {
                GUILayout.Label("没有可用波形包。");
                return;
            }

            GUILayout.Label("当前波形包：" + StimProfileManager.DisplayName(profile) + "    版本：" + profile.version + "    上限：" + profile.maxWaveIntensity);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("应用此波形包"))
            {
                StimProfileManager.ApplyProfile(profile);
                _settingsMessage = StimProfileManager.LastMessage;
            }
            if (GUILayout.Button("刷新波形包"))
            {
                StimProfileManager.RefreshProfiles();
                ClampSelections();
            }
            GUILayout.EndHorizontal();

            Section("波形列表");
            if (profile.waves == null || profile.waves.Count == 0)
            {
                GUILayout.Label("该波形包没有波形。");
                return;
            }

            for (int i = 0; i < profile.waves.Count; i++)
            {
                WavePreset wave = profile.waves[i];
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(_selectedWaveIndex == i, StimProfileManager.DisplayName(wave), GUI.skin.button, GUILayout.Width(180)))
                {
                    _selectedWaveIndex = i;
                }
                GUILayout.Label(WaveSummary(wave));
                GUILayout.EndHorizontal();
            }

            WavePreset selected = SelectedWave();
            if (selected != null)
            {
                Section("波形测试");
                GUILayout.Label("名称：" + StimProfileManager.DisplayName(selected));
                GUILayout.Label("类型：" + selected.type + " / " + selected.shape + "    通道建议：" + selected.channel);
                GUILayout.Label("频率：" + selected.frequency + "    强度：" + selected.minStrength + "-" + selected.maxStrength + "    时长：" + selected.durationMs + "ms");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("测试 A")) StimProfileManager.SendWave(_server, selected, 'A');
                if (GUILayout.Button("测试 B")) StimProfileManager.SendWave(_server, selected, 'B');
                if (GUILayout.Button("测试 A+B"))
                {
                    StimProfileManager.SendWave(_server, selected, 'A');
                    StimProfileManager.SendWave(_server, selected, 'B');
                }
                GUILayout.EndHorizontal();
            }

            if (!string.IsNullOrEmpty(StimProfileManager.LastMessage)) GUILayout.Label(StimProfileManager.LastMessage);
        }

        private void DrawEventMappingPage()
        {
            Section("事件映射");
            DrawProfileSelector();
            StimProfile profile = SelectedProfile();
            if (profile == null || profile.events == null || profile.events.Count == 0)
            {
                GUILayout.Label("当前波形包没有事件映射。");
                return;
            }

            GUILayout.Label("这里展示波形包对事件的推荐映射；应用波形包会把持续状态参数写入配置。");
            for (int i = 0; i < profile.events.Count; i++)
            {
                EventRule rule = profile.events[i];
                GUILayout.Label(rule.eventName + " -> " + rule.waveName + "    通道：" + rule.channelMode + "    倍率：" + rule.strengthMultiplier.ToString("0.00"));
                if (!string.IsNullOrEmpty(rule.note))
                {
                    GUILayout.Label("  " + rule.note);
                }
            }

            Section("当前诊断");
            StimDiagnostics d = _stim.Diagnostics;
            GUILayout.Label("连续状态：" + d.ContinuousState + "    补包：" + d.ContinuousRefillCount + "    队列估算：" + d.EstimatedQueueMs + "ms    Gen：" + d.ContinuousGeneration);
            GUILayout.Label("连续强度：A " + d.ContinuousBaseA + "+" + d.ContinuousOverlayA + " / B " + d.ContinuousBaseB + "+" + d.ContinuousOverlayB);
            GUILayout.Label("脚步：左 " + d.LeftFootsteps + " / 右 " + d.RightFootsteps + " / 来源 " + d.LastFootstepSource + " / 下一步 " + (d.NextFootLeft ? "左" : "右"));
            GUILayout.Label("动作：跳跃 " + d.JumpCount + " / 落地 " + d.LandCount + " / 滑行 " + d.SlideCount + " / 受击 " + d.HurtCount + " / 死亡 " + d.DeathCount);

            Section("模拟事件");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("模拟左脚")) _stim.TriggerFootstepForced(true);
            if (GUILayout.Button("模拟右脚")) _stim.TriggerFootstepForced(false);
            if (GUILayout.Button("模拟走路 2秒")) _stim.SimulateContinuousMovement(false);
            if (GUILayout.Button("模拟奔跑 2秒")) _stim.SimulateContinuousMovement(true);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("模拟跳跃")) _stim.SimulateJump();
            if (GUILayout.Button("模拟落地")) _stim.SimulateLand();
            if (GUILayout.Button("模拟滑行")) _stim.SimulateSlide();
            if (GUILayout.Button("模拟受击")) _stim.SimulateHurt(10);
            if (GUILayout.Button("模拟死亡")) _stim.SimulateDeath();
            GUILayout.EndHorizontal();
        }

        private void DrawImportExportPage()
        {
            Section("导入/导出");
            GUILayout.Label("导入目录：");
            GUILayout.TextArea(StimProfileManager.ProfilesDirectory, GUILayout.Height(40));
            GUILayout.Label("支持 DGLabPunishProfile.json、原始 HEX JSON 数组和 .pulse 文本。第三方项目建议只导入数据，不复制代码。");
            GUILayout.Label("HEX 示例：type=hex，hex=[\"0A0A0A0A64646464\"]；.pulse 可每行写 16 位 HEX 或 frequency strength。");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("刷新导入目录"))
            {
                StimProfileManager.RefreshProfiles();
                ClampSelections();
            }
            if (GUILayout.Button("导出当前配置"))
            {
                try
                {
                    StimProfileManager.ExportCurrentConfig();
                }
                catch (Exception ex)
                {
                    StimProfileManager.LastMessage = "导出失败：" + ex.Message;
                }
            }
            GUILayout.EndHorizontal();

            DrawProfileSelector();
            if (GUILayout.Button("应用选中的导入/内置波形包"))
            {
                StimProfileManager.ApplyProfile(SelectedProfile());
                _settingsMessage = StimProfileManager.LastMessage;
            }

            if (!string.IsNullOrEmpty(_settingsMessage)) GUILayout.Label(_settingsMessage);
            if (!string.IsNullOrEmpty(StimProfileManager.LastMessage)) GUILayout.Label(StimProfileManager.LastMessage);

            Section("当前核心配置");
            GUILayout.Label("连续模式：" + ModConfig.ContinuousMode.Value + "    自动调强：" + ModConfig.AutoStrengthMode.Value);
            GUILayout.Label("补包/Lookahead：" + ModConfig.ContinuousSendIntervalMs.Value + " / " + ModConfig.ContinuousLookaheadMs.Value + " ms");
            GUILayout.Label("走路基础/增量：" + ModConfig.WalkBaseIntensity.Value + " / " + ModConfig.WalkStepBumpIntensity.Value);
            GUILayout.Label("奔跑基础/增量：" + ModConfig.RunBaseIntensity.Value + " / " + ModConfig.RunStepBumpIntensity.Value);
            GUILayout.Label("受击：" + ModConfig.HurtMinIntensity.Value + "-" + ModConfig.HurtMaxIntensity.Value + " / " + ModConfig.HurtDurationMaxMs.Value + "ms");
            GUILayout.Label("死亡：" + ModConfig.DeathIntensity.Value + " / " + ModConfig.DeathDurationMs.Value + "ms");
        }

        private void DrawProfileSelector()
        {
            ClampSelections();
            GUILayout.BeginHorizontal();
            GUILayout.Label("波形包", GUILayout.Width(80));
            if (GUILayout.Button("<", GUILayout.Width(32)))
            {
                _selectedProfileIndex--;
                ClampSelections();
            }
            StimProfile profile = SelectedProfile();
            GUILayout.Label(profile == null ? "无" : StimProfileManager.DisplayName(profile), GUILayout.Width(220));
            if (GUILayout.Button(">", GUILayout.Width(32)))
            {
                _selectedProfileIndex++;
                ClampSelections();
            }
            GUILayout.EndHorizontal();
        }

        private StimProfile SelectedProfile()
        {
            ClampSelections();
            if (StimProfileManager.Profiles.Count == 0) return null;
            return StimProfileManager.Profiles[_selectedProfileIndex];
        }

        private WavePreset SelectedWave()
        {
            StimProfile profile = SelectedProfile();
            if (profile == null || profile.waves == null || profile.waves.Count == 0) return null;
            ClampSelections();
            return profile.waves[_selectedWaveIndex];
        }

        private void ClampSelections()
        {
            if (StimProfileManager.Profiles.Count == 0)
            {
                _selectedProfileIndex = 0;
                _selectedWaveIndex = 0;
                return;
            }

            if (_selectedProfileIndex < 0) _selectedProfileIndex = StimProfileManager.Profiles.Count - 1;
            if (_selectedProfileIndex >= StimProfileManager.Profiles.Count) _selectedProfileIndex = 0;
            StimProfile profile = StimProfileManager.Profiles[_selectedProfileIndex];
            int waveCount = profile != null && profile.waves != null ? profile.waves.Count : 0;
            if (waveCount == 0)
            {
                _selectedWaveIndex = 0;
                return;
            }
            if (_selectedWaveIndex < 0) _selectedWaveIndex = waveCount - 1;
            if (_selectedWaveIndex >= waveCount) _selectedWaveIndex = 0;
        }

        private string WaveSummary(WavePreset wave)
        {
            if (wave == null) return "";
            if (wave.hex != null && wave.hex.Count > 0)
            {
                return "HEX " + wave.hex.Count + " 条 / 建议 " + wave.channel;
            }
            return wave.type + " " + wave.shape + " / " + wave.frequency + "Hz / " + wave.minStrength + "-" + wave.maxStrength + " / " + wave.durationMs + "ms";
        }

        private void SetManualStrength(int channel, string value)
        {
            int strength;
            if (!int.TryParse(value, out strength))
            {
                _settingsMessage = "强度必须是数字。";
                return;
            }

            strength = Mathf.Clamp(strength, 0, 200);
            bool ok = _server.SetStrength(channel, strength);
            _settingsMessage = ok ? ("已设置 " + (channel == 1 ? "A" : "B") + " 强度为 " + strength) : "设置失败：App 尚未绑定或连接不可用。";
        }

        private void AdjustStrength(int channel, int delta)
        {
            StrengthState state = _server.Strength;
            int current = channel == 1 ? state.ACurrent : state.BCurrent;
            int next = Mathf.Clamp(current + delta, 0, 200);
            if (channel == 1) _manualStrengthA = next.ToString();
            else _manualStrengthB = next.ToString();
            bool ok = _server.SetStrength(channel, next);
            _settingsMessage = ok ? ("已调整 " + (channel == 1 ? "A" : "B") + " 强度为 " + next) : "调整失败：App 尚未绑定或连接不可用。";
        }

        private void InitDrafts()
        {
            if (_draftsInitialized)
            {
                return;
            }

            _hostDraft = ModConfig.AdvertiseHost.Value;
            if (string.IsNullOrEmpty(_hostDraft))
            {
                _hostDraft = _server.GetAdvertiseHost();
            }
            _portDraft = ModConfig.Port.Value.ToString();
            _publishUriDraft = ModConfig.PublishSocketUri.Value;
            _remoteUriDraft = ModConfig.RemoteServerUri.Value;
            _remoteCodeDraft = ModConfig.RemotePairCode.Value;
            _panelKeyDraft = ModConfig.PanelKey.Value.ToString();
            _stopKeyDraft = ModConfig.EmergencyStopKey.Value.ToString();
            _draftsInitialized = true;
        }

        private void SavePanelSettings(bool restartServer)
        {
            int port;
            if (!int.TryParse(_portDraft, out port) || port < 1 || port > 65535)
            {
                _settingsMessage = "端口无效，应为 1-65535。";
                return;
            }

            KeyCode panelKey;
            KeyCode stopKey;
            if (!TryParseKey(_panelKeyDraft, out panelKey))
            {
                _settingsMessage = "面板键无效，例如 P、F8、Insert。";
                return;
            }
            if (!TryParseKey(_stopKeyDraft, out stopKey))
            {
                _settingsMessage = "急停键无效，例如 I、F9、BackQuote。";
                return;
            }

            ModConfig.AdvertiseHost.Value = (_hostDraft ?? "").Trim();
            ModConfig.Port.Value = port;
            ModConfig.PublishSocketUri.Value = (_publishUriDraft ?? "").Trim();
            ModConfig.RemoteServerUri.Value = (_remoteUriDraft ?? "").Trim();
            ModConfig.RemotePairCode.Value = (_remoteCodeDraft ?? "").Trim();
            ModConfig.PanelKey.Value = panelKey;
            ModConfig.EmergencyStopKey.Value = stopKey;
            SaveConfig();
            RefreshQr();

            if (restartServer)
            {
                _server.Stop();
                _server.Start();
            }

            _settingsMessage = "设置已保存。当前二维码已刷新。";
        }

        private static bool TryParseKey(string value, out KeyCode keyCode)
        {
            try
            {
                keyCode = (KeyCode)Enum.Parse(typeof(KeyCode), (value ?? "").Trim(), true);
                return true;
            }
            catch
            {
                keyCode = KeyCode.None;
                return false;
            }
        }

        private static void SaveConfig()
        {
            try
            {
                if (Plugin.Instance != null)
                {
                    Plugin.Instance.Config.Save();
                }
            }
            catch
            {
            }
        }

        private static void Section(string label)
        {
            GUILayout.Space(8);
            GUILayout.Label("=== " + label + " ===");
        }

        private void TextRow(string label, ref string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(120));
            value = GUILayout.TextField(value ?? "", GUILayout.Width(360));
            GUILayout.EndHorizontal();
        }

        private void Toggle(string label, ConfigEntry<bool> entry)
        {
            bool value = GUILayout.Toggle(entry.Value, label);
            if (value != entry.Value)
            {
                entry.Value = value;
                SaveConfig();
            }
        }

        private void ApplyPreset(int footstep, int slide, int jump, int land, int death, int minimum, int hurtMax)
        {
            ModConfig.AutoStrengthMode.Value = AutoStrengthMode.EventScaled;
            ModConfig.ContinuousMode.Value = true;
            ModConfig.MinimumChannelStrength.Value = minimum;
            ModConfig.MaxWaveIntensity.Value = Mathf.Max(hurtMax, death);

            ModConfig.FootstepIntensity.Value = footstep;
            ModConfig.FootstepDurationMs.Value = footstep >= 24 ? 320 : 250;
            ModConfig.FootstepWaveShape.Value = StimWaveShape.Sin;
            ModConfig.WalkBaseIntensity.Value = Mathf.Max(4, footstep / 2);
            ModConfig.WalkBaseFrequency.Value = 25;
            ModConfig.WalkBaseShape.Value = StimEnvelopeShape.Tremor;
            ModConfig.WalkStepBumpIntensity.Value = Mathf.Max(8, footstep - 4);
            ModConfig.WalkStepBumpDurationMs.Value = 160;
            ModConfig.WalkFootstepMinIntervalMs.Value = 90;
            ModConfig.RunBaseIntensity.Value = Mathf.Max(6, footstep / 2 + 5);
            ModConfig.RunBaseFrequency.Value = 260;
            ModConfig.RunBaseShape.Value = StimEnvelopeShape.SawBurst;
            ModConfig.RunFootstepIntensity.Value = Mathf.Max(footstep + 4, footstep);
            ModConfig.RunFootstepDurationMs.Value = footstep >= 28 ? 170 : 140;
            ModConfig.RunStepBumpIntensity.Value = Mathf.Max(12, footstep);
            ModConfig.RunStepBumpDurationMs.Value = 110;
            ModConfig.RunFootstepMinIntervalMs.Value = 55;

            ModConfig.SlideIntensity.Value = slide;
            ModConfig.JumpIntensity.Value = jump;
            ModConfig.LandIntensity.Value = land;
            ModConfig.HurtMaxIntensity.Value = hurtMax;
            ModConfig.HurtDurationMinMs.Value = hurtMax >= 80 ? 1800 : 1500;
            ModConfig.HurtDurationMaxMs.Value = hurtMax >= 80 ? 4500 : 3500;
            ModConfig.HurtFrequency.Value = 60;
            ModConfig.DeathIntensity.Value = death;
            ModConfig.DeathDurationMs.Value = death >= 80 ? 5000 : 4000;
            ModConfig.DeathFrequency.Value = 150;
            ModConfig.MaxEventDurationMs.Value = Mathf.Max(ModConfig.DeathDurationMs.Value, ModConfig.HurtDurationMaxMs.Value);
            ModConfig.MaxQueuedPulseItems.Value = death >= 80 ? 55 : 45;
            ModConfig.TestIntensity.Value = minimum;
            ModConfig.HurtEnvelope.Value = StimEnvelopeShape.RampUpHoldDown;
            ModConfig.LandEnvelope.Value = StimEnvelopeShape.SingleTap;
            ModConfig.SlideEnvelope.Value = StimEnvelopeShape.Tremor;
            ModConfig.DeathEnvelope.Value = StimEnvelopeShape.DeathWave;

            SaveConfig();
            _settingsMessage = "已应用预设，可继续微调各事件强度。";
        }

        private void ApplyComfortPreset()
        {
            ModConfig.AutoStrengthMode.Value = AutoStrengthMode.EventScaled;
            ModConfig.ContinuousMode.Value = true;
            ModConfig.MinimumChannelStrength.Value = 10;
            ModConfig.MaxWaveIntensity.Value = 40;
            ModConfig.MaxEventDurationMs.Value = 3500;
            ModConfig.MaxQueuedPulseItems.Value = 35;
            ModConfig.ContinuousSendIntervalMs.Value = 80;
            ModConfig.ContinuousLookaheadMs.Value = 120;
            ModConfig.ContinuousFadeOutMs.Value = 450;

            ModConfig.FootstepIntensity.Value = 12;
            ModConfig.FootstepDurationMs.Value = 220;
            ModConfig.FootstepWaveShape.Value = StimWaveShape.Sin;
            ModConfig.WalkBaseIntensity.Value = 10;
            ModConfig.WalkBaseFrequency.Value = 25;
            ModConfig.WalkBaseShape.Value = StimEnvelopeShape.Tremor;
            ModConfig.WalkStepBumpIntensity.Value = 8;
            ModConfig.WalkStepBumpDurationMs.Value = 150;
            ModConfig.WalkFootstepMinIntervalMs.Value = 90;
            ModConfig.RunBaseIntensity.Value = 12;
            ModConfig.RunBaseFrequency.Value = 240;
            ModConfig.RunBaseShape.Value = StimEnvelopeShape.SawBurst;
            ModConfig.RunFootstepIntensity.Value = 14;
            ModConfig.RunFootstepDurationMs.Value = 130;
            ModConfig.RunStepBumpIntensity.Value = 10;
            ModConfig.RunStepBumpDurationMs.Value = 100;
            ModConfig.RunFootstepMinIntervalMs.Value = 55;

            ModConfig.SlideIntensity.Value = 14;
            ModConfig.JumpIntensity.Value = 16;
            ModConfig.LandIntensity.Value = 18;
            ModConfig.HurtMinIntensity.Value = 10;
            ModConfig.HurtMaxIntensity.Value = 40;
            ModConfig.HurtDurationMinMs.Value = 1200;
            ModConfig.HurtDurationMaxMs.Value = 3000;
            ModConfig.HurtFrequency.Value = 60;
            ModConfig.DeathIntensity.Value = 40;
            ModConfig.DeathDurationMs.Value = 3500;
            ModConfig.DeathFrequency.Value = 150;
            ModConfig.TestIntensity.Value = 10;
            ModConfig.HurtEnvelope.Value = StimEnvelopeShape.RampUpHoldDown;
            ModConfig.LandEnvelope.Value = StimEnvelopeShape.SingleTap;
            ModConfig.SlideEnvelope.Value = StimEnvelopeShape.Tremor;
            ModConfig.DeathEnvelope.Value = StimEnvelopeShape.DeathWave;

            SaveConfig();
            _settingsMessage = "已应用舒适模式：最高 40，基础强度 10。";
        }

        private void IntSlider(string label, ConfigEntry<int> entry, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + "：" + entry.Value, GUILayout.Width(180));
            int value = (int)Math.Round(GUILayout.HorizontalSlider(entry.Value, min, max, GUILayout.Width(230)));
            if (value != entry.Value)
            {
                entry.Value = value;
                SaveConfig();
            }
            GUILayout.EndHorizontal();
        }

        private void FloatSlider(string label, ConfigEntry<float> entry, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + "：" + entry.Value.ToString("0.00"), GUILayout.Width(180));
            float value = GUILayout.HorizontalSlider(entry.Value, min, max, GUILayout.Width(230));
            value = (float)Math.Round(value, 2);
            if (Math.Abs(value - entry.Value) > 0.001f)
            {
                entry.Value = value;
                SaveConfig();
            }
            GUILayout.EndHorizontal();
        }

        private void EnumCycle<T>(string label, ConfigEntry<T> entry)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(180));
            if (GUILayout.Button(entry.Value.ToString(), GUILayout.Width(160)))
            {
                T[] values = (T[])Enum.GetValues(typeof(T));
                int index = Array.IndexOf(values, entry.Value);
                if (index < 0) index = 0;
                entry.Value = values[(index + 1) % values.Length];
                SaveConfig();
                RefreshQr();
            }
            GUILayout.EndHorizontal();
        }

        private void RefreshQr()
        {
            _qrText = null;
            if (_qrTexture != null)
            {
                UnityEngine.Object.Destroy(_qrTexture);
                _qrTexture = null;
            }
        }

        private void EnsureQrTexture(string text)
        {
            if (_qrTexture != null && _qrText == text)
            {
                return;
            }

            RefreshQr();
            _qrText = text;

            try
            {
                QrCode qr = QrCode.EncodeText(text, QrCode.Ecc.Medium);
                int border = 4;
                int moduleSize = Math.Max(3, 180 / (qr.Size + border * 2));
                int textureSize = (qr.Size + border * 2) * moduleSize;
                Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
                Color32 black = new Color32(0, 0, 0, 255);
                Color32 white = new Color32(255, 255, 255, 255);

                for (int y = 0; y < textureSize; y++)
                {
                    for (int x = 0; x < textureSize; x++)
                    {
                        int moduleX = x / moduleSize - border;
                        int moduleY = y / moduleSize - border;
                        bool dark = moduleX >= 0 && moduleY >= 0 && moduleX < qr.Size && moduleY < qr.Size && qr.GetModule(moduleX, moduleY);
                        texture.SetPixel(x, textureSize - 1 - y, dark ? black : white);
                    }
                }

                texture.Apply(false, true);
                _qrTexture = texture;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Failed to generate QR texture: " + ex.Message);
            }
        }
    }
}
