using System;
using System.Collections.Generic;
using UnityEngine;

namespace DGLabPunish
{
    internal enum ContinuousStimState
    {
        Idle,
        Walk,
        Run,
        Slide,
        Punish
    }

    internal sealed class StimOverlay
    {
        public char Channel;
        public float StartTime;
        public int DurationMs;
        public int Strength;
        public int Frequency;
        public StimEnvelopeShape Shape;
        public double Phase;
        public int Priority;
        public int Generation;
    }

    internal sealed class ContinuousStimEngine
    {
        private readonly DGLabSocketServer _server;
        private readonly StimDiagnostics _diagnostics;
        private readonly List<StimOverlay> _overlays;
        private float _lastSendTime;
        private float _lastActiveTime;
        private float _lastQueueUpdateTime;
        private float _lastStrengthTimeA;
        private float _lastStrengthTimeB;
        private bool _moving;
        private bool _sprinting;
        private bool _sliding;
        private float _speed;
        private bool _forceSend;
        private int _generation;
        private int _estimatedQueueMs;
        private int _lastStrengthA;
        private int _lastStrengthB;

        internal ContinuousStimEngine(DGLabSocketServer server, StimDiagnostics diagnostics)
        {
            _server = server;
            _diagnostics = diagnostics;
            _overlays = new List<StimOverlay>();
            _lastSendTime = -999f;
            _lastActiveTime = -999f;
            _lastQueueUpdateTime = -999f;
            _lastStrengthTimeA = -999f;
            _lastStrengthTimeB = -999f;
            _forceSend = false;
            _generation = 0;
            _estimatedQueueMs = 0;
            _lastStrengthA = -1;
            _lastStrengthB = -1;
        }

        internal void SetMovement(bool moving, bool sprinting, bool sliding, float speed)
        {
            if (moving != _moving || sprinting != _sprinting || sliding != _sliding)
            {
                _forceSend = true;
            }

            _moving = moving;
            _sprinting = sprinting;
            _sliding = sliding;
            _speed = speed;
            if (moving || sliding)
            {
                _lastActiveTime = Time.realtimeSinceStartup;
            }
        }

        internal void AddOverlay(char channel, int strength, int durationMs, int frequency, StimEnvelopeShape shape, double phase, int priority)
        {
            if (priority >= 80)
            {
                Interrupt(priority);
            }

            AddOverlayInternal(channel, strength, durationMs, frequency, shape, phase, priority);
        }

        private void AddOverlayInternal(char channel, int strength, int durationMs, int frequency, StimEnvelopeShape shape, double phase, int priority)
        {
            _overlays.Add(new StimOverlay
            {
                Channel = channel,
                StartTime = Time.realtimeSinceStartup,
                DurationMs = Mathf.Max(25, durationMs),
                Strength = Mathf.Max(0, strength),
                Frequency = Mathf.Clamp(frequency, 10, 1000),
                Shape = shape,
                Phase = phase,
                Priority = priority,
                Generation = _generation
            });

            _forceSend = true;
            _lastSendTime = -999f;
        }

        internal void AddOverlayBoth(int strengthA, int strengthB, int durationMs, int frequency, StimEnvelopeShape shape, int priority)
        {
            AddOverlayPair(strengthA, strengthB, durationMs, frequency, shape, 0.0, 0.18, priority);
        }

        internal void AddOverlayPair(int strengthA, int strengthB, int durationMs, int frequency, StimEnvelopeShape shape, double phaseA, double phaseB, int priority)
        {
            if (priority >= 80)
            {
                Interrupt(priority);
            }

            AddOverlayInternal('A', strengthA, durationMs, frequency, shape, phaseA, priority);
            AddOverlayInternal('B', strengthB, durationMs, frequency, shape, phaseB, priority);
        }

        internal void Clear()
        {
            _overlays.Clear();
            _lastSendTime = -999f;
            _lastActiveTime = -999f;
            _lastQueueUpdateTime = -999f;
            _forceSend = false;
            _generation++;
            _estimatedQueueMs = 0;
            _diagnostics.EstimatedQueueMs = 0;
            _diagnostics.ContinuousGeneration = _generation;
        }

        internal void Tick()
        {
            if (_server == null || !_server.IsBound || !ModConfig.Armed.Value || !ModConfig.ContinuousMode.Value)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            UpdateEstimatedQueue(now);
            PruneOverlays(now);

            int intervalMs = Mathf.Clamp(ModConfig.ContinuousSendIntervalMs.Value, 50, 1000);
            if (!_forceSend && (now - _lastSendTime) * 1000f < intervalMs)
            {
                return;
            }

            ContinuousStimState state = CurrentState(now);
            _diagnostics.ContinuousState = state.ToString();
            _diagnostics.ActiveOverlays = _overlays.Count;
            _diagnostics.EstimatedQueueMs = _estimatedQueueMs;
            _diagnostics.ContinuousGeneration = _generation;

            if (state == ContinuousStimState.Idle && _overlays.Count == 0)
            {
                _diagnostics.ContinuousBaseA = 0;
                _diagnostics.ContinuousBaseB = 0;
                _diagnostics.ContinuousOverlayA = 0;
                _diagnostics.ContinuousOverlayB = 0;
                return;
            }

            int lookaheadMs = Mathf.Clamp(ModConfig.ContinuousLookaheadMs.Value, 100, 1500);
            int lowWaterMs = Mathf.Clamp(lookaheadMs / 2, 25, 80);
            if (!_forceSend && _estimatedQueueMs > lowWaterMs)
            {
                _diagnostics.LastCommandResult = "队列水位足够，暂不补包";
                return;
            }

            List<int> freqA = new List<int>();
            List<int> freqB = new List<int>();
            List<int> strA = new List<int>();
            List<int> strB = new List<int>();
            int segments = Mathf.Max(1, (int)Math.Ceiling(lookaheadMs / 25.0));

            int peakA = 0;
            int peakB = 0;
            int baseA = 0;
            int baseB = 0;
            int overlayA = 0;
            int overlayB = 0;

            for (int i = 0; i < segments; i++)
            {
                float sampleTime = now + i * 0.025f;
                Sample sampleA = SampleChannel('A', state, sampleTime);
                Sample sampleB = SampleChannel('B', state, sampleTime);
                freqA.Add(sampleA.Frequency);
                freqB.Add(sampleB.Frequency);
                strA.Add(sampleA.Strength);
                strB.Add(sampleB.Strength);
                peakA = Mathf.Max(peakA, sampleA.Strength);
                peakB = Mathf.Max(peakB, sampleB.Strength);
                baseA = Mathf.Max(baseA, sampleA.BaseStrength);
                baseB = Mathf.Max(baseB, sampleB.BaseStrength);
                overlayA = Mathf.Max(overlayA, sampleA.OverlayStrength);
                overlayB = Mathf.Max(overlayB, sampleB.OverlayStrength);
            }

            PrepareStrength('A', peakA);
            PrepareStrength('B', peakB);
            List<string> pulseA = WaveformEncoder.Pulse(freqA, strA);
            List<string> pulseB = WaveformEncoder.Pulse(freqB, strB);
            bool sentA = _server.SendPulse('A', pulseA);
            bool sentB = _server.SendPulse('B', pulseB);
            _lastSendTime = now;
            _forceSend = false;
            if (sentA || sentB)
            {
                int sentItems = Mathf.Max(sentA ? pulseA.Count : 0, sentB ? pulseB.Count : 0);
                _estimatedQueueMs = Mathf.Clamp(_estimatedQueueMs + sentItems * 100, 0, 1000);
            }

            _diagnostics.ContinuousBaseA = baseA;
            _diagnostics.ContinuousBaseB = baseB;
            _diagnostics.ContinuousOverlayA = overlayA;
            _diagnostics.ContinuousOverlayB = overlayB;
            _diagnostics.ContinuousRefillCount++;
            _diagnostics.EstimatedQueueMs = _estimatedQueueMs;
            _diagnostics.ContinuousGeneration = _generation;
            _diagnostics.LastCommandResult = sentA && sentB ? "连续补包已发送，队列约 " + _estimatedQueueMs + "ms" : "连续补包发送失败";
        }

        private ContinuousStimState CurrentState(float now)
        {
            if (_sliding)
            {
                return ContinuousStimState.Slide;
            }

            if (_sprinting && _moving)
            {
                return ContinuousStimState.Run;
            }

            if (_moving)
            {
                return ContinuousStimState.Walk;
            }

            int fadeMs = Mathf.Max(0, ModConfig.ContinuousFadeOutMs.Value);
            if (fadeMs > 0 && (now - _lastActiveTime) * 1000f < fadeMs)
            {
                return ContinuousStimState.Walk;
            }

            return _overlays.Count > 0 ? ContinuousStimState.Punish : ContinuousStimState.Idle;
        }

        private Sample SampleChannel(char channel, ContinuousStimState state, float sampleTime)
        {
            int frequency = 180;
            int baseStrength = BaseStrength(state, sampleTime, out frequency);
            int overlayStrength = 0;
            int overlayFrequency = frequency;

            for (int i = 0; i < _overlays.Count; i++)
            {
                StimOverlay overlay = _overlays[i];
                if (overlay.Channel != channel || overlay.Generation != _generation)
                {
                    continue;
                }

                double t = (sampleTime - overlay.StartTime) * 1000.0 / overlay.DurationMs;
                if (t < 0.0 || t > 1.0)
                {
                    continue;
                }

                int contribution = (int)Math.Round(overlay.Strength * WaveformEncoder.EvaluateEnvelope(overlay.Shape, t, overlay.Phase));
                if (contribution > overlayStrength)
                {
                    overlayStrength = contribution;
                    overlayFrequency = overlay.Frequency;
                }
            }

            int strength = Mathf.Clamp(baseStrength + overlayStrength, 0, ModConfig.MaxWaveIntensity.Value);
            if (overlayStrength > 0)
            {
                frequency = overlayFrequency;
            }

            return new Sample
            {
                Strength = strength,
                Frequency = Mathf.Clamp(frequency, 10, 1000),
                BaseStrength = baseStrength,
                OverlayStrength = overlayStrength
            };
        }

        private int BaseStrength(ContinuousStimState state, float sampleTime, out int frequency)
        {
            frequency = 180;
            if (state == ContinuousStimState.Idle || state == ContinuousStimState.Punish)
            {
                return 0;
            }

            int strength;
            StimEnvelopeShape shape;
            if (state == ContinuousStimState.Run)
            {
                strength = ModConfig.RunBaseIntensity.Value;
                frequency = ModConfig.RunBaseFrequency.Value;
                shape = ModConfig.RunBaseShape.Value;
            }
            else if (state == ContinuousStimState.Slide)
            {
                strength = ModConfig.SlideBaseIntensity.Value;
                frequency = ModConfig.SlideBaseFrequency.Value;
                shape = StimEnvelopeShape.Tremor;
            }
            else
            {
                strength = ModConfig.WalkBaseIntensity.Value;
                frequency = ModConfig.WalkBaseFrequency.Value;
                shape = ModConfig.WalkBaseShape.Value;
            }

            int fadeMs = Mathf.Max(1, ModConfig.ContinuousFadeOutMs.Value);
            if (!_moving && !_sliding)
            {
                float elapsedMs = (sampleTime - _lastActiveTime) * 1000f;
                if (elapsedMs >= fadeMs)
                {
                    return 0;
                }
                strength = (int)Math.Round(strength * (1.0 - elapsedMs / fadeMs));
            }

            double t = (sampleTime * 1000.0 % 900.0) / 900.0;
            double curve = WaveformEncoder.EvaluateEnvelope(shape, t, 0.0);
            double floor = 0.55;
            return Mathf.Clamp((int)Math.Round(strength * (floor + (1.0 - floor) * curve)), 0, ModConfig.MaxWaveIntensity.Value);
        }

        private void PruneOverlays(float now)
        {
            for (int i = _overlays.Count - 1; i >= 0; i--)
            {
                StimOverlay overlay = _overlays[i];
                if (overlay.Generation != _generation || (now - overlay.StartTime) * 1000f > overlay.DurationMs + 100)
                {
                    _overlays.RemoveAt(i);
                }
            }
        }

        private void Interrupt(int priority)
        {
            _generation++;
            for (int i = _overlays.Count - 1; i >= 0; i--)
            {
                if (_overlays[i].Priority <= priority)
                {
                    _overlays.RemoveAt(i);
                }
            }

            _server.ClearAll();
            _estimatedQueueMs = 0;
            _lastQueueUpdateTime = Time.realtimeSinceStartup;
            _lastSendTime = -999f;
            _forceSend = true;
            _diagnostics.EstimatedQueueMs = 0;
            _diagnostics.ContinuousGeneration = _generation;
        }

        private void UpdateEstimatedQueue(float now)
        {
            if (_lastQueueUpdateTime < -100f)
            {
                _lastQueueUpdateTime = now;
                return;
            }

            int elapsedMs = Mathf.Max(0, (int)Math.Round((now - _lastQueueUpdateTime) * 1000f));
            if (elapsedMs > 0)
            {
                _estimatedQueueMs = Mathf.Max(0, _estimatedQueueMs - elapsedMs);
                _lastQueueUpdateTime = now;
            }
        }

        private void PrepareStrength(char channel, int eventStrength)
        {
            AutoStrengthMode mode = ModConfig.AutoStrengthMode.Value;
            if (ModConfig.AllowStrengthControl.Value)
            {
                mode = AutoStrengthMode.EventScaled;
            }

            if (mode == AutoStrengthMode.Off || eventStrength <= 0)
            {
                return;
            }

            StrengthState state = _server.Strength;
            int channelIndex = channel == 'A' ? 1 : 2;
            int current = channel == 'A' ? state.ACurrent : state.BCurrent;
            int max = channel == 'A' ? state.AMax : state.BMax;
            int minimum = Mathf.Clamp(ModConfig.MinimumChannelStrength.Value, 0, 200);
            int target = mode == AutoStrengthMode.MinimumOnly ? Mathf.Max(current, minimum) : Mathf.Max(minimum, eventStrength);
            if (max > 0)
            {
                target = Mathf.Min(target, max);
            }
            target = Mathf.Clamp(target, 0, 200);

            float now = Time.realtimeSinceStartup;
            int lastTarget = channel == 'A' ? _lastStrengthA : _lastStrengthB;
            float lastTime = channel == 'A' ? _lastStrengthTimeA : _lastStrengthTimeB;
            if (lastTarget >= 0 && Mathf.Abs(target - lastTarget) < 2 && (now - lastTime) < 0.35f)
            {
                return;
            }

            _server.SetStrength(channelIndex, target);
            if (channel == 'A')
            {
                _lastStrengthA = target;
                _lastStrengthTimeA = now;
            }
            else
            {
                _lastStrengthB = target;
                _lastStrengthTimeB = now;
            }
        }

        private struct Sample
        {
            public int Strength;
            public int Frequency;
            public int BaseStrength;
            public int OverlayStrength;
        }
    }
}
