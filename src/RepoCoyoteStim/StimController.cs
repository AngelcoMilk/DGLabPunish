using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RepoCoyoteStim
{
    internal sealed class StimDiagnostics
    {
        public int LeftFootsteps;
        public int RightFootsteps;
        public int AnimationFootsteps;
        public int FallbackFootsteps;
        public int DuplicateFootsteps;
        public int RunSupplementFootsteps;
        public int JumpCount;
        public int LandCount;
        public int SlideCount;
        public int HurtCount;
        public int DeathCount;
        public int EnemyFootsteps;
        public string LastEvent = "";
        public string LastChannel = "";
        public string LastBlockedReason = "";
        public string LastCommandResult = "";
        public string LastFootstepSource = "";
        public string MovementState = "";
        public int LastFootstepIntervalMs;
        public string ContinuousState = "";
        public int ContinuousBaseA;
        public int ContinuousBaseB;
        public int ContinuousOverlayA;
        public int ContinuousOverlayB;
        public int ContinuousRefillCount;
        public int ActiveOverlays;
        public int EstimatedQueueMs;
        public int ContinuousGeneration;
        public int LastLeftRightDeltaMs;
        public bool NextFootLeft = true;
    }

    internal sealed class StimController
    {
        private readonly DGLabSocketServer _server;
        private readonly StimDiagnostics _diagnostics;
        private readonly ContinuousStimEngine _continuous;
        private float _lastHurt;
        private float _lastFootstep;
        private float _lastJump;
        private float _lastLand;
        private float _lastSlide;
        private float _lastEnemyFootstep;
        private float _lastDeath;
        private float _lastAnimationFootstep;
        private float _lastRunSupplement;
        private bool _nextFootLeft;
        private bool _alternateA;
        private bool _isMoving;
        private bool _isSprinting;
        private bool _isSliding;
        private float _speed;
        private float _lastLeftFootTime;
        private float _lastRightFootTime;
        private float _simulateMovementUntil;
        private bool _simulateRun;

        public StimController(DGLabSocketServer server)
        {
            _server = server;
            _diagnostics = new StimDiagnostics();
            _continuous = new ContinuousStimEngine(server, _diagnostics);
            _lastHurt = -999f;
            _lastFootstep = -999f;
            _lastJump = -999f;
            _lastLand = -999f;
            _lastSlide = -999f;
            _lastEnemyFootstep = -999f;
            _lastDeath = -999f;
            _lastAnimationFootstep = -999f;
            _lastRunSupplement = -999f;
            _nextFootLeft = ModConfig.FirstFootLeft.Value;
            _alternateA = true;
            _diagnostics.NextFootLeft = _nextFootLeft;
        }

        internal void Tick()
        {
            if (_simulateMovementUntil > Time.realtimeSinceStartup)
            {
                _isMoving = true;
                _isSprinting = _simulateRun;
                _isSliding = false;
                _diagnostics.MovementState = _simulateRun ? "Run(Sim)" : "Walk(Sim)";
                _continuous.SetMovement(true, _simulateRun, false, _simulateRun ? 5f : 2f);
            }

            _continuous.Tick();

            if (!ModConfig.RunCadenceSupplement.Value || !_isMoving || !_isSprinting)
            {
                return;
            }

            if (!IsArmedAndBound("奔跑补拍"))
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            int interval = Mathf.Max(55, ModConfig.RunSupplementIntervalMs.Value);
            if ((now - _lastRunSupplement) * 1000f < interval)
            {
                return;
            }

            if ((now - _lastAnimationFootstep) * 1000f < interval)
            {
                return;
            }

            _lastRunSupplement = now;
            _diagnostics.RunSupplementFootsteps++;
            SendFootstep("奔跑补拍", _nextFootLeft, false, true);
        }

        internal void UpdatePlayerMovementState(PlayerController controller)
        {
            if (controller == null || !object.ReferenceEquals(controller, PlayerController.instance))
            {
                return;
            }

            _isMoving = controller.moving;
            _isSprinting = controller.sprinting;
            _isSliding = controller.Sliding;
            _speed = controller.Velocity.magnitude;
            _diagnostics.MovementState = _isSliding ? "Slide" : (_isSprinting ? "Run" : (_isMoving ? "Walk" : "Idle"));
            _continuous.SetMovement(_isMoving, _isSprinting, _isSliding, _speed);
        }

        internal StimDiagnostics Diagnostics
        {
            get { return _diagnostics; }
        }

        internal void ResetFootToConfiguredFirst()
        {
            _nextFootLeft = ModConfig.FirstFootLeft.Value;
            _diagnostics.NextFootLeft = _nextFootLeft;
            _diagnostics.LastEvent = "重置脚步";
            _diagnostics.LastChannel = "";
        }

        internal void TriggerHurt(int damage)
        {
            if (!CanTrigger("受击", ModConfig.HurtEnabled.Value, _lastHurt, ModConfig.HurtCooldownMs.Value))
            {
                return;
            }

            _lastHurt = Time.realtimeSinceStartup;
            SendHurt("受击", damage);
        }

        internal void SimulateHurt(int damage)
        {
            SendHurt("模拟受击", damage);
        }

        private void SendHurt(string eventName, int damage)
        {
            _diagnostics.HurtCount++;

            int eventMax = Clamp(ModConfig.HurtMaxIntensity.Value, 1, ModConfig.MaxWaveIntensity.Value);
            int max = Clamp((int)(ModConfig.HurtMinIntensity.Value + damage * ModConfig.HurtDamageMultiplier.Value), 1, eventMax);
            int strengthA = Clamp((int)(max * ClampMultiplier(ModConfig.HurtAMultiplier.Value)), 1, ModConfig.MaxWaveIntensity.Value);
            int strengthB = Clamp((int)(max * ClampMultiplier(ModConfig.HurtBMultiplier.Value)), 1, ModConfig.MaxWaveIntensity.Value);
            int minA = Clamp(strengthA / 4, 1, strengthA);
            int minB = Clamp(strengthB / 4, 1, strengthB);
            int duration = Clamp(ModConfig.HurtDurationMinMs.Value + damage * 20, ModConfig.HurtDurationMinMs.Value, ModConfig.HurtDurationMaxMs.Value);

            if (ModConfig.ContinuousMode.Value)
            {
                _continuous.AddOverlayBoth(strengthA, strengthB, duration, ModConfig.HurtFrequency.Value, ModConfig.HurtEnvelope.Value, 80);
                _diagnostics.LastEvent = eventName;
                _diagnostics.LastChannel = "A+B";
                _diagnostics.LastCommandResult = "已加入连续惩罚包络";
                return;
            }

            List<string> pulseA = WaveformEncoder.EnvelopePulse(ModConfig.HurtEnvelope.Value, ModConfig.HurtFrequency.Value, minA, strengthA, duration, 0.0);
            List<string> pulseB = WaveformEncoder.EnvelopePulse(ModConfig.HurtEnvelope.Value, ModConfig.HurtFrequency.Value, minB, strengthB, duration, 0.16);
            SendByMode(eventName, ModConfig.HurtChannelMode.Value, pulseA, pulseB, strengthA, strengthB);
        }

        internal void TriggerDeath()
        {
            if (!CanTrigger("死亡", ModConfig.DeathEnabled.Value, _lastDeath, ModConfig.DeathCooldownMs.Value))
            {
                return;
            }

            _lastDeath = Time.realtimeSinceStartup;
            SendDeath("死亡");
        }

        internal void SimulateDeath()
        {
            SendDeath("模拟死亡");
        }

        private void SendDeath(string eventName)
        {
            _diagnostics.DeathCount++;

            int max = Clamp(ModConfig.DeathIntensity.Value, 1, ModConfig.MaxWaveIntensity.Value);
            int duration = Clamp(ModConfig.DeathDurationMs.Value, 300, ModConfig.MaxEventDurationMs.Value);
            List<string> pulseA = WaveformEncoder.EnvelopePulse(ModConfig.DeathEnvelope.Value, ModConfig.DeathFrequency.Value, max / 4, max, duration, 0.0);
            List<string> pulseB = WaveformEncoder.EnvelopePulse(ModConfig.DeathEnvelope.Value, ModConfig.DeathFrequency.Value, max / 5, max, duration, 0.33);

            if (ModConfig.DeathClearBeforePulse.Value)
            {
                _server.ClearAll();
                _continuous.Clear();
            }

            if (ModConfig.ContinuousMode.Value)
            {
                int continuousDelay = ModConfig.DeathClearBeforePulse.Value ? Clamp(ModConfig.ClearDelayMs.Value, 0, 1000) : 0;
                if (continuousDelay > 0 && Plugin.Instance != null)
                {
                    Plugin.Instance.StartCoroutine(AddDeathOverlayAfterDelay(eventName, continuousDelay, max, duration));
                }
                else
                {
                    AddDeathOverlay(eventName, max, duration);
                }
                return;
            }

            int delay = ModConfig.DeathClearBeforePulse.Value ? Clamp(ModConfig.ClearDelayMs.Value, 0, 1000) : 0;
            if (delay > 0 && Plugin.Instance != null)
            {
                Plugin.Instance.StartCoroutine(SendDeathAfterDelay(eventName, delay, pulseA, pulseB, max));
            }
            else
            {
                SendByMode(eventName, ModConfig.DeathChannelMode.Value, pulseA, pulseB, max, max);
            }
        }

        private IEnumerator AddDeathOverlayAfterDelay(string eventName, int delayMs, int strength, int duration)
        {
            yield return new WaitForSecondsRealtime(delayMs / 1000f);
            AddDeathOverlay(eventName, strength, duration);
        }

        private void AddDeathOverlay(string eventName, int strength, int duration)
        {
            _continuous.AddOverlayPair(strength, strength, duration, ModConfig.DeathFrequency.Value, ModConfig.DeathEnvelope.Value, 0.0, 0.33, 100);
            _diagnostics.LastEvent = eventName;
            _diagnostics.LastChannel = "A+B";
            _diagnostics.LastCommandResult = "已加入连续死亡波";
        }

        private IEnumerator SendDeathAfterDelay(string eventName, int delayMs, List<string> pulseA, List<string> pulseB, int strength)
        {
            yield return new WaitForSecondsRealtime(delayMs / 1000f);
            SendByMode(eventName, ModConfig.DeathChannelMode.Value, pulseA, pulseB, strength, strength);
        }

        internal void TriggerFootstepFromVisuals(string weight)
        {
            _diagnostics.LastFootstepSource = "声音脚步-" + weight;
            if (!ModConfig.FallbackFootstepSupplement.Value)
            {
                Block("声音脚步-" + weight, "已使用左右脚落地事件，声音脚步仅作诊断");
                return;
            }

            float now = Time.realtimeSinceStartup;
            if ((now - _lastAnimationFootstep) * 1000f < Mathf.Max(100, ModConfig.FallbackFootstepNoAnimationMs.Value))
            {
                Block("声音脚步-" + weight, "左右脚落地事件正常，声音脚步不参与相位");
                return;
            }

            TriggerAnimationFootstep("声音脚步兜底-" + weight, null);
        }

        internal void TriggerFootDown(bool left, string source)
        {
            TriggerAnimationFootstep(source, left);
        }

        internal void TriggerFootstepFallback()
        {
            _diagnostics.FallbackFootsteps++;
            _diagnostics.LastFootstepSource = "兜底";
            if (!ModConfig.FallbackFootstepSupplement.Value)
            {
                Block("脚步-兜底", "兜底补发已关闭");
                return;
            }

            float now = Time.realtimeSinceStartup;
            if ((now - _lastAnimationFootstep) * 1000f < Mathf.Max(100, ModConfig.FallbackFootstepNoAnimationMs.Value))
            {
                Block("脚步-兜底", "动画脚步正常，兜底不参与相位");
                return;
            }

            if (!CanTrigger("脚步-兜底补发", ModConfig.LocalFootstepEnabled.Value, _lastFootstep, GetFootstepMinIntervalMs()))
            {
                return;
            }

            _lastFootstep = now;
            SendFootstep("脚步-兜底补发", _nextFootLeft, true, false);
        }

        internal void TriggerFootstepForced(bool left)
        {
            SendFootstep(left ? "模拟左脚" : "模拟右脚", left, false, false);
        }

        internal void SimulateRunFootstep(bool left)
        {
            bool wasMoving = _isMoving;
            bool wasSprinting = _isSprinting;
            _isMoving = true;
            _isSprinting = true;
            _diagnostics.MovementState = "Run";
            SendFootstep(left ? "模拟奔跑左脚" : "模拟奔跑右脚", left, false, false);
            _isMoving = wasMoving;
            _isSprinting = wasSprinting;
        }

        internal void SimulateContinuousMovement(bool run)
        {
            _simulateRun = run;
            _simulateMovementUntil = Time.realtimeSinceStartup + 2.0f;
            _diagnostics.LastEvent = run ? "模拟持续奔跑" : "模拟持续走路";
            _diagnostics.LastCommandResult = "已启动 2 秒基础波形";
        }

        private void TriggerAnimationFootstep(string eventName, bool? explicitLeft)
        {
            if (!CanTrigger(eventName, ModConfig.LocalFootstepEnabled.Value, _lastFootstep, 0))
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            int elapsed = (int)((now - _lastFootstep) * 1000f);
            _diagnostics.LastFootstepIntervalMs = elapsed;

            if (elapsed < Mathf.Max(0, ModConfig.FootstepDuplicateWindowMs.Value))
            {
                _diagnostics.DuplicateFootsteps++;
                Block(eventName, "动画脚步极短重复 " + elapsed + "ms");
                return;
            }

            int minInterval = GetFootstepMinIntervalMs();
            if (elapsed < minInterval)
            {
                _diagnostics.DuplicateFootsteps++;
                Block(eventName, (_isSprinting ? "奔跑" : "走路") + "脚步节奏限制 " + elapsed + "/" + minInterval + "ms");
                return;
            }

            if (!explicitLeft.HasValue && ModConfig.ResetFootOnIdle.Value && (now - _lastFootstep) * 1000f >= Mathf.Max(100, ModConfig.FootstepIdleResetMs.Value))
            {
                _nextFootLeft = ModConfig.FirstFootLeft.Value;
            }

            _lastFootstep = now;
            _lastAnimationFootstep = now;
            bool left = explicitLeft.HasValue ? explicitLeft.Value : _nextFootLeft;
            _nextFootLeft = !left;
            _diagnostics.NextFootLeft = _nextFootLeft;
            _diagnostics.AnimationFootsteps++;
            _diagnostics.LastFootstepSource = eventName;

            SendFootstep(eventName + (left ? " 左脚" : " 右脚"), left, false, false);
        }

        private void SendFootstep(string eventName, bool left, bool fallback, bool advancePhase)
        {
            if (advancePhase)
            {
                _nextFootLeft = !left;
                _diagnostics.NextFootLeft = _nextFootLeft;
            }

            if (left)
            {
                _diagnostics.LeftFootsteps++;
            }
            else
            {
                _diagnostics.RightFootsteps++;
            }

            if (fallback)
            {
                _diagnostics.FallbackFootsteps++;
            }

            char channel = ChannelToChar(left ? ModConfig.LeftFootChannel.Value : ModConfig.RightFootChannel.Value);
            bool running = _isSprinting && _isMoving;
            int strength = Clamp(running ? ModConfig.RunFootstepIntensity.Value : ModConfig.FootstepIntensity.Value, 1, ModConfig.MaxWaveIntensity.Value);
            int duration = Clamp(running ? ModConfig.RunFootstepDurationMs.Value : ModConfig.FootstepDurationMs.Value, 25, ModConfig.MaxEventDurationMs.Value);
            int bump = Clamp(running ? ModConfig.RunStepBumpIntensity.Value : ModConfig.WalkStepBumpIntensity.Value, 1, ModConfig.MaxWaveIntensity.Value);
            int bumpDuration = Clamp(running ? ModConfig.RunStepBumpDurationMs.Value : ModConfig.WalkStepBumpDurationMs.Value, 25, ModConfig.MaxEventDurationMs.Value);

            if (left)
            {
                _lastLeftFootTime = Time.realtimeSinceStartup;
                if (_lastRightFootTime > 0f)
                {
                    _diagnostics.LastLeftRightDeltaMs = (int)Mathf.Abs((_lastLeftFootTime - _lastRightFootTime) * 1000f);
                }
            }
            else
            {
                _lastRightFootTime = Time.realtimeSinceStartup;
                if (_lastLeftFootTime > 0f)
                {
                    _diagnostics.LastLeftRightDeltaMs = (int)Mathf.Abs((_lastRightFootTime - _lastLeftFootTime) * 1000f);
                }
            }

            if (ModConfig.ContinuousMode.Value)
            {
                _continuous.AddOverlay(channel, bump, bumpDuration, ModConfig.FootstepFrequency.Value, StimEnvelopeShape.SingleTap, 0.0, 20);
                _diagnostics.LastEvent = eventName;
                _diagnostics.LastChannel = channel.ToString();
                _diagnostics.LastCommandResult = "脚步 bump 已叠加";
                return;
            }

            List<string> pulse = BuildPulse(ModConfig.FootstepWaveShape.Value, ModConfig.FootstepFrequency.Value, strength / 3, strength, duration);
            SendChannel(eventName, channel, pulse, strength);
        }

        internal void TriggerJump()
        {
            if (!CanTrigger("跳跃", ModConfig.LandJumpEnabled.Value && ModConfig.JumpEnabled.Value, _lastJump, ModConfig.JumpCooldownMs.Value))
            {
                return;
            }

            _lastJump = Time.realtimeSinceStartup;
            SendJump("跳跃");
        }

        internal void SimulateJump()
        {
            SendJump("模拟跳跃");
        }

        private void SendJump(string eventName)
        {
            _diagnostics.JumpCount++;
            if (ModConfig.ContinuousMode.Value)
            {
                _continuous.AddOverlayBoth(ModConfig.JumpIntensity.Value, ModConfig.JumpIntensity.Value, ModConfig.JumpDurationMs.Value, ModConfig.JumpFrequency.Value, StimEnvelopeShape.DoubleTap, 40);
                _diagnostics.LastEvent = eventName;
                _diagnostics.LastChannel = "A+B";
                _diagnostics.LastCommandResult = "跳跃包络已叠加";
                return;
            }
            SendAction(eventName, ModConfig.JumpChannelMode.Value, ModConfig.JumpFrequency.Value, ModConfig.JumpIntensity.Value, ModConfig.JumpDurationMs.Value, StimEnvelopeShape.DoubleTap);
        }

        internal void TriggerLand()
        {
            if (!CanTrigger("落地", ModConfig.LandJumpEnabled.Value && ModConfig.LandEnabled.Value, _lastLand, ModConfig.LandCooldownMs.Value))
            {
                return;
            }

            _lastLand = Time.realtimeSinceStartup;
            SendLand("落地");
        }

        internal void SimulateLand()
        {
            SendLand("模拟落地");
        }

        private void SendLand(string eventName)
        {
            _diagnostics.LandCount++;
            if (ModConfig.ContinuousMode.Value)
            {
                _continuous.AddOverlayBoth(ModConfig.LandIntensity.Value, ModConfig.LandIntensity.Value, ModConfig.LandDurationMs.Value, ModConfig.LandFrequency.Value, ModConfig.LandEnvelope.Value, 45);
                _diagnostics.LastEvent = eventName;
                _diagnostics.LastChannel = "A+B";
                _diagnostics.LastCommandResult = "落地包络已叠加";
                return;
            }
            SendAction(eventName, ModConfig.LandChannelMode.Value, ModConfig.LandFrequency.Value, ModConfig.LandIntensity.Value, ModConfig.LandDurationMs.Value, ModConfig.LandEnvelope.Value);
        }

        internal void TriggerSlide()
        {
            if (!CanTrigger("滑行", ModConfig.LandJumpEnabled.Value && ModConfig.SlideEnabled.Value, _lastSlide, ModConfig.SlideCooldownMs.Value))
            {
                return;
            }

            _lastSlide = Time.realtimeSinceStartup;
            SendSlide("滑行");
        }

        internal void SimulateSlide()
        {
            SendSlide("模拟滑行");
        }

        private void SendSlide(string eventName)
        {
            _diagnostics.SlideCount++;
            if (ModConfig.ContinuousMode.Value)
            {
                _continuous.AddOverlayBoth(ModConfig.SlideIntensity.Value, ModConfig.SlideIntensity.Value, ModConfig.SlideDurationMs.Value, ModConfig.SlideFrequency.Value, ModConfig.SlideEnvelope.Value, 35);
                _diagnostics.LastEvent = eventName;
                _diagnostics.LastChannel = "A+B";
                _diagnostics.LastCommandResult = "滑行包络已叠加";
                return;
            }
            SendAction(eventName, ModConfig.SlideChannelMode.Value, ModConfig.SlideFrequency.Value, ModConfig.SlideIntensity.Value, ModConfig.SlideDurationMs.Value, ModConfig.SlideEnvelope.Value);
        }

        internal void TriggerMovement(string kind)
        {
            if (kind == "jump")
            {
                TriggerJump();
            }
            else if (kind == "land")
            {
                TriggerLand();
            }
            else if (kind == "slide")
            {
                TriggerSlide();
            }
        }

        internal void TriggerEnemyFootstep(Vector3 sourcePosition)
        {
            if (!ModConfig.EnemyFootstepEnabled.Value || !IsArmedAndBound("敌人脚步"))
            {
                return;
            }

            PlayerAvatar local = PlayerAvatar.instance;
            if (local == null)
            {
                Block("敌人脚步", "找不到本地玩家");
                return;
            }

            float range = Mathf.Max(0.1f, ModConfig.EnemyFootstepRange.Value);
            if (Vector3.Distance(local.transform.position, sourcePosition) > range)
            {
                Block("敌人脚步", "超过触发距离");
                return;
            }

            if (!CanTrigger("敌人脚步", true, _lastEnemyFootstep, ModConfig.FootstepCooldownMs.Value))
            {
                return;
            }

            _lastEnemyFootstep = Time.realtimeSinceStartup;
            _diagnostics.EnemyFootsteps++;
            int strength = Clamp(ModConfig.EnemyFootstepIntensity.Value, 1, ModConfig.MaxWaveIntensity.Value);
            List<string> pulse = WaveformEncoder.SmoothPulse(ModConfig.FootstepFrequency.Value, strength, ModConfig.FootstepDurationMs.Value);
            SendChannel("敌人脚步", 'B', pulse, strength);
        }

        internal void SendTest(char channel)
        {
            int strength = Clamp(ModConfig.TestIntensity.Value, 1, ModConfig.MaxWaveIntensity.Value);
            List<string> pulse = WaveformEncoder.SinPulse(300, strength / 3, strength, 350);
            SendChannel("测试 " + channel, channel, pulse, strength);
        }

        internal void EmergencyStop()
        {
            _server.ClearAll();
            _continuous.Clear();
            _server.SetStrength(1, 0);
            _server.SetStrength(2, 0);
            _diagnostics.LastEvent = "紧急停止";
            _diagnostics.LastChannel = "A+B";
            _diagnostics.LastCommandResult = "已发送 clear 与强度归零";
            Plugin.Log.LogWarning("DG-LAB emergency stop sent.");
        }

        private void SendAction(string eventName, StimChannelMode mode, int frequency, int intensity, int durationMs, StimEnvelopeShape shape)
        {
            int strength = Clamp(intensity, 1, ModConfig.MaxWaveIntensity.Value);
            int duration = Clamp(durationMs, 25, ModConfig.MaxEventDurationMs.Value);
            List<string> pulseA = WaveformEncoder.EnvelopePulse(shape, frequency, strength / 3, strength, duration, 0.0);
            List<string> pulseB = WaveformEncoder.EnvelopePulse(shape, frequency, strength / 4, strength, duration, 0.18);
            SendByMode(eventName, mode, pulseA, pulseB, strength, strength);
        }

        private void SendByMode(string eventName, StimChannelMode mode, List<string> pulseA, List<string> pulseB, int strengthA, int strengthB)
        {
            if (mode == StimChannelMode.A)
            {
                SendChannel(eventName, 'A', pulseA, strengthA);
                return;
            }

            if (mode == StimChannelMode.B)
            {
                SendChannel(eventName, 'B', pulseB, strengthB);
                return;
            }

            if (mode == StimChannelMode.Alternate)
            {
                char channel = _alternateA ? 'A' : 'B';
                _alternateA = !_alternateA;
                SendChannel(eventName, channel, channel == 'A' ? pulseA : pulseB, channel == 'A' ? strengthA : strengthB);
                return;
            }

            SendChannel(eventName, 'A', pulseA, strengthA);
            SendChannel(eventName, 'B', pulseB, strengthB);
        }

        private void SendChannel(string eventName, char channel, List<string> pulse, int eventStrength)
        {
            PrepareStrength(channel, eventStrength);
            bool ok = _server.SendPulse(channel, pulse);
            _diagnostics.LastEvent = eventName;
            _diagnostics.LastChannel = channel.ToString();
            _diagnostics.LastCommandResult = ok ? "已发送" : "发送失败";
            if (!ok)
            {
                _diagnostics.LastBlockedReason = "发送失败，可能未绑定或已断线";
            }
        }

        private void PrepareStrength(char channel, int eventStrength)
        {
            AutoStrengthMode mode = ModConfig.AutoStrengthMode.Value;
            if (ModConfig.AllowStrengthControl.Value)
            {
                mode = AutoStrengthMode.EventScaled;
            }

            if (mode == AutoStrengthMode.Off)
            {
                return;
            }

            StrengthState state = _server.Strength;
            int channelIndex = channel == 'A' ? 1 : 2;
            int current = channel == 'A' ? state.ACurrent : state.BCurrent;
            int max = channel == 'A' ? state.AMax : state.BMax;
            int minimum = Clamp(ModConfig.MinimumChannelStrength.Value, 0, 200);
            int target = mode == AutoStrengthMode.MinimumOnly ? Mathf.Max(current, minimum) : Mathf.Max(minimum, eventStrength);
            if (max > 0)
            {
                target = Mathf.Min(target, max);
            }
            target = Clamp(target, 0, 200);
            _server.SetStrength(channelIndex, target);
        }

        private bool CanTrigger(string eventName, bool eventEnabled, float lastTime, int cooldownMs)
        {
            if (!eventEnabled)
            {
                Block(eventName, "事件已关闭");
                return false;
            }

            if (!IsArmedAndBound(eventName))
            {
                return false;
            }

            float elapsedMs = (Time.realtimeSinceStartup - lastTime) * 1000f;
            if (elapsedMs < Mathf.Max(0, cooldownMs))
            {
                Block(eventName, "冷却中 " + (int)elapsedMs + "/" + cooldownMs + "ms");
                return false;
            }

            return true;
        }

        private int GetFootstepMinIntervalMs()
        {
            return Mathf.Max(
                Mathf.Max(0, ModConfig.FootstepDuplicateWindowMs.Value),
                _isSprinting && _isMoving ? ModConfig.RunFootstepMinIntervalMs.Value : ModConfig.WalkFootstepMinIntervalMs.Value);
        }

        private bool IsArmedAndBound(string eventName)
        {
            if (!ModConfig.Armed.Value)
            {
                Block(eventName, "触发未启用");
                return false;
            }

            if (_server == null || !_server.IsBound)
            {
                Block(eventName, "DG-LAB 未绑定");
                return false;
            }

            return true;
        }

        private void Block(string eventName, string reason)
        {
            _diagnostics.LastEvent = eventName;
            _diagnostics.LastBlockedReason = reason;
            _diagnostics.LastCommandResult = "未发送";
        }

        private static List<string> BuildPulse(StimWaveShape shape, int frequency, int startStrength, int endStrength, int durationMs)
        {
            int start = Clamp(startStrength, 0, 100);
            int end = Clamp(endStrength, 0, 100);
            if (shape == StimWaveShape.Smooth)
            {
                return WaveformEncoder.SmoothPulse(frequency, end, durationMs);
            }

            if (shape == StimWaveShape.Sin)
            {
                return WaveformEncoder.SinPulse(frequency, Mathf.Min(start, end), Mathf.Max(start, end), durationMs);
            }

            return WaveformEncoder.GradientPulse(frequency, start, end, durationMs);
        }

        private static char ChannelToChar(StimChannel channel)
        {
            return channel == StimChannel.A ? 'A' : 'B';
        }

        private static float ClampMultiplier(float value)
        {
            if (value < 0f) return 0f;
            if (value > 2f) return 2f;
            return value;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
