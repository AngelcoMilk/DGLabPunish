using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using Newtonsoft.Json;

namespace RepoCoyoteStim
{
    internal sealed class StimProfile
    {
        public string name;
        public string version;
        public int maxWaveIntensity;
        public ContinuousStatePreset continuous;
        public List<WavePreset> waves;
        public List<EventRule> events;
    }

    internal sealed class ContinuousStatePreset
    {
        public int sendIntervalMs;
        public int lookaheadMs;
        public int fadeOutMs;
        public int maxEventDurationMs;
        public int maxQueuedPulseItems;
        public int minimumChannelStrength;
        public int walkBaseIntensity;
        public int walkBaseFrequency;
        public string walkBaseShape;
        public int walkStepBumpIntensity;
        public int walkStepBumpDurationMs;
        public int runBaseIntensity;
        public int runBaseFrequency;
        public string runBaseShape;
        public int runStepBumpIntensity;
        public int runStepBumpDurationMs;
        public int slideBaseIntensity;
        public int slideBaseFrequency;
        public int hurtMinIntensity;
        public int hurtMaxIntensity;
        public int hurtDurationMinMs;
        public int hurtDurationMaxMs;
        public int hurtFrequency;
        public int deathIntensity;
        public int deathDurationMs;
        public int deathFrequency;
    }

    internal sealed class WavePreset
    {
        public string name;
        public string displayName;
        public string type;
        public string shape;
        public string channel;
        public int frequency;
        public int minStrength;
        public int maxStrength;
        public int durationMs;
        public double phase;
        public List<string> hex;
        public List<int[]> frames;
    }

    internal sealed class EventRule
    {
        public string eventName;
        public string waveName;
        public string channelMode;
        public float strengthMultiplier;
        public bool clearBeforeSend;
        public bool continuousState;
        public string note;
    }

    internal static class StimProfileManager
    {
        internal static readonly List<StimProfile> Profiles = new List<StimProfile>();
        internal static StimProfile CurrentProfile;
        internal static string LastMessage = "";

        internal static string ProfilesDirectory
        {
            get { return Path.Combine(Paths.ConfigPath, "RepoCoyoteStim", "profiles"); }
        }

        internal static void RefreshProfiles()
        {
            Profiles.Clear();
            AddBuiltInProfiles();
            EnsureProfilesDirectory();

            try
            {
                LoadProfileFiles("*.json");
                LoadProfileFiles("*.pulse");
            }
            catch (Exception ex)
            {
                LastMessage = "读取导入目录失败：" + ex.Message;
            }

            if (CurrentProfile == null && Profiles.Count > 0)
            {
                CurrentProfile = Profiles[0];
            }

            if (string.IsNullOrEmpty(LastMessage))
            {
                LastMessage = "已加载 " + Profiles.Count + " 个波形包。";
            }
        }

        internal static bool ApplyProfile(StimProfile profile)
        {
            if (profile == null)
            {
                LastMessage = "没有可应用的波形包。";
                return false;
            }

            CurrentProfile = profile;
            ContinuousStatePreset continuous = profile.continuous;
            if (continuous != null)
            {
                ModConfig.ContinuousMode.Value = true;
                ModConfig.AutoStrengthMode.Value = AutoStrengthMode.EventScaled;
                ModConfig.MinimumChannelStrength.Value = Clamp(continuous.minimumChannelStrength, 0, 200);
                ModConfig.ContinuousSendIntervalMs.Value = Clamp(continuous.sendIntervalMs, 50, 2000);
                ModConfig.ContinuousLookaheadMs.Value = Clamp(continuous.lookaheadMs, 100, 5000);
                ModConfig.ContinuousFadeOutMs.Value = Clamp(continuous.fadeOutMs, 0, 3000);
                ModConfig.MaxEventDurationMs.Value = Clamp(continuous.maxEventDurationMs, 100, 10000);
                ModConfig.MaxQueuedPulseItems.Value = Clamp(continuous.maxQueuedPulseItems, 1, 100);
                ModConfig.WalkBaseIntensity.Value = Clamp(continuous.walkBaseIntensity, 0, 100);
                ModConfig.WalkBaseFrequency.Value = Clamp(continuous.walkBaseFrequency, 10, 1000);
                ModConfig.WalkBaseShape.Value = ParseEnvelope(continuous.walkBaseShape, ModConfig.WalkBaseShape.Value);
                ModConfig.WalkStepBumpIntensity.Value = Clamp(continuous.walkStepBumpIntensity, 0, 100);
                ModConfig.WalkStepBumpDurationMs.Value = Clamp(continuous.walkStepBumpDurationMs, 25, 2000);
                ModConfig.RunBaseIntensity.Value = Clamp(continuous.runBaseIntensity, 0, 100);
                ModConfig.RunBaseFrequency.Value = Clamp(continuous.runBaseFrequency, 10, 1000);
                ModConfig.RunBaseShape.Value = ParseEnvelope(continuous.runBaseShape, ModConfig.RunBaseShape.Value);
                ModConfig.RunStepBumpIntensity.Value = Clamp(continuous.runStepBumpIntensity, 0, 100);
                ModConfig.RunStepBumpDurationMs.Value = Clamp(continuous.runStepBumpDurationMs, 25, 2000);
                ModConfig.SlideBaseIntensity.Value = Clamp(continuous.slideBaseIntensity, 0, 100);
                ModConfig.SlideBaseFrequency.Value = Clamp(continuous.slideBaseFrequency, 10, 1000);
                ModConfig.HurtMinIntensity.Value = Clamp(continuous.hurtMinIntensity, 0, 100);
                ModConfig.HurtMaxIntensity.Value = Clamp(continuous.hurtMaxIntensity, 0, 100);
                ModConfig.HurtDurationMinMs.Value = Clamp(continuous.hurtDurationMinMs, 100, 10000);
                ModConfig.HurtDurationMaxMs.Value = Clamp(continuous.hurtDurationMaxMs, ModConfig.HurtDurationMinMs.Value, 10000);
                if (continuous.hurtFrequency > 0) ModConfig.HurtFrequency.Value = Clamp(continuous.hurtFrequency, 10, 1000);
                ModConfig.DeathIntensity.Value = Clamp(continuous.deathIntensity, 0, 100);
                ModConfig.DeathDurationMs.Value = Clamp(continuous.deathDurationMs, 100, 10000);
                if (continuous.deathFrequency > 0) ModConfig.DeathFrequency.Value = Clamp(continuous.deathFrequency, 10, 1000);
            }

            if (profile.maxWaveIntensity > 0)
            {
                ModConfig.MaxWaveIntensity.Value = Clamp(profile.maxWaveIntensity, 0, 100);
            }

            SaveConfig();
            LastMessage = "已应用波形包：" + DisplayName(profile);
            return true;
        }

        internal static string ExportCurrentConfig()
        {
            EnsureProfilesDirectory();
            StimProfile profile = CaptureCurrentProfile();
            string fileName = "RepoCoyoteStimProfile-export-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".json";
            string path = Path.Combine(ProfilesDirectory, fileName);
            File.WriteAllText(path, JsonConvert.SerializeObject(profile, Formatting.Indented), Encoding.UTF8);
            LastMessage = "已导出：" + path;
            RefreshProfiles();
            return path;
        }

        internal static bool SendWave(DGLabSocketServer server, WavePreset wave, char channel)
        {
            if (server == null || wave == null)
            {
                LastMessage = "没有可发送的波形。";
                return false;
            }

            string error;
            List<string> pulses = BuildWave(wave, out error);
            if (pulses == null)
            {
                LastMessage = error;
                return false;
            }

            bool ok = server.SendPulse(channel, pulses);
            LastMessage = ok ? ("已发送 " + DisplayName(wave) + " 到 " + channel + " 通道。") : "发送失败：App 尚未绑定或连接不可用。";
            return ok;
        }

        internal static List<string> BuildWave(WavePreset wave, out string error)
        {
            error = "";
            if (wave == null)
            {
                error = "波形为空。";
                return null;
            }

            if (IsHexWave(wave))
            {
                List<string> clean = new List<string>();
                for (int i = 0; i < wave.hex.Count && i < 100; i++)
                {
                    string item = (wave.hex[i] ?? "").Trim();
                    if (!IsPulseHex(item))
                    {
                        error = "HEX 波形格式错误：" + item;
                        return null;
                    }
                    clean.Add(item.ToUpperInvariant());
                }

                if (clean.Count == 0)
                {
                    error = "HEX 波形为空。";
                    return null;
                }

                string json = WaveformEncoder.ToJsonArray(clean, clean.Count);
                if (json.Length > 1950)
                {
                    error = "HEX 波形过长，单次指令超过 1950 字符。";
                    return null;
                }

                return clean;
            }

            if (wave.frames != null && wave.frames.Count > 0)
            {
                List<int> frequencies = new List<int>();
                List<int> strengths = new List<int>();
                int frameCount = Math.Min(400, wave.frames.Count);
                for (int i = 0; i < frameCount; i++)
                {
                    int[] frame = wave.frames[i];
                    if (frame == null || frame.Length < 2)
                    {
                        error = "frames 波形包含非法帧。";
                        return null;
                    }
                    frequencies.Add(Clamp(frame[0], 10, 1000));
                    strengths.Add(Clamp(frame[1], 0, 100));
                }

                List<string> pulses = WaveformEncoder.Pulse(frequencies, strengths);
                string json = WaveformEncoder.ToJsonArray(pulses, pulses.Count);
                if (json.Length > 1950)
                {
                    error = "frames 波形过长，单次指令超过 1950 字符。";
                    return null;
                }
                return pulses;
            }

            int frequency = Clamp(wave.frequency <= 0 ? 220 : wave.frequency, 10, 1000);
            int minStrength = Clamp(wave.minStrength, 0, 100);
            int maxStrength = Clamp(wave.maxStrength <= 0 ? ModConfig.TestIntensity.Value : wave.maxStrength, minStrength, 100);
            int duration = Clamp(wave.durationMs <= 0 ? 400 : wave.durationMs, 25, Math.Max(25, ModConfig.MaxEventDurationMs.Value));
            string type = (wave.type ?? "").Trim().ToLowerInvariant();

            if (type == "smooth")
            {
                return WaveformEncoder.SmoothPulse(frequency, maxStrength, duration);
            }

            if (type == "gradient")
            {
                return WaveformEncoder.GradientPulse(frequency, minStrength, maxStrength, duration);
            }

            if (type == "sin" || type == "sine")
            {
                return WaveformEncoder.SinPulse(frequency, minStrength, maxStrength, duration);
            }

            StimEnvelopeShape envelope = ParseEnvelope(wave.shape, StimEnvelopeShape.RampUpHoldDown);
            return WaveformEncoder.EnvelopePulse(envelope, frequency, minStrength, maxStrength, duration, wave.phase);
        }

        internal static string DisplayName(StimProfile profile)
        {
            if (profile == null) return "";
            return string.IsNullOrEmpty(profile.name) ? "未命名波形包" : profile.name;
        }

        internal static string DisplayName(WavePreset wave)
        {
            if (wave == null) return "";
            if (!string.IsNullOrEmpty(wave.displayName)) return wave.displayName;
            return string.IsNullOrEmpty(wave.name) ? "未命名波形" : wave.name;
        }

        internal static WavePreset FindWave(StimProfile profile, string name)
        {
            if (profile == null || profile.waves == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            for (int i = 0; i < profile.waves.Count; i++)
            {
                WavePreset wave = profile.waves[i];
                if (wave != null && string.Equals(wave.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return wave;
                }
            }

            return null;
        }

        private static void LoadProfileFiles(string pattern)
        {
            string[] files = Directory.GetFiles(ProfilesDirectory, pattern);
            for (int i = 0; i < files.Length; i++)
            {
                TryLoadProfileFile(files[i]);
            }
        }

        private static void TryLoadProfileFile(string path)
        {
            try
            {
                string extension = Path.GetExtension(path) ?? "";
                if (string.Equals(extension, ".pulse", StringComparison.OrdinalIgnoreCase))
                {
                    Profiles.Add(CreatePulseImportProfile(path));
                    return;
                }

                string json = File.ReadAllText(path, Encoding.UTF8);
                StimProfile profile;
                string trimmed = (json ?? "").Trim();
                if (trimmed.StartsWith("["))
                {
                    List<string> hex = JsonConvert.DeserializeObject<List<string>>(trimmed);
                    profile = CreateHexImportProfile(Path.GetFileNameWithoutExtension(path), hex);
                }
                else
                {
                    profile = JsonConvert.DeserializeObject<StimProfile>(json);
                }
                if (profile == null)
                {
                    LastMessage = "导入失败，JSON 为空：" + Path.GetFileName(path);
                    return;
                }

                ValidateProfile(profile);
                if (string.IsNullOrEmpty(profile.name))
                {
                    profile.name = Path.GetFileNameWithoutExtension(path);
                }

                Profiles.Add(profile);
            }
            catch (Exception ex)
            {
                LastMessage = "导入失败 " + Path.GetFileName(path) + "：" + ex.Message;
            }
        }

        private static void ValidateProfile(StimProfile profile)
        {
            if (profile.waves == null)
            {
                profile.waves = new List<WavePreset>();
            }

            for (int i = 0; i < profile.waves.Count; i++)
            {
                WavePreset wave = profile.waves[i];
                if (wave == null)
                {
                    continue;
                }

                if (IsHexWave(wave))
                {
                    if (wave.hex.Count > 100)
                    {
                        throw new InvalidDataException("波形 " + DisplayName(wave) + " 超过 100 条 HEX。");
                    }

                    for (int j = 0; j < wave.hex.Count; j++)
                    {
                        if (!IsPulseHex((wave.hex[j] ?? "").Trim()))
                        {
                            throw new InvalidDataException("波形 " + DisplayName(wave) + " 包含非法 HEX。");
                        }
                    }
                }

                if (wave.frames != null && wave.frames.Count > 0)
                {
                    if (wave.frames.Count > 400)
                    {
                        throw new InvalidDataException("波形 " + DisplayName(wave) + " 超过 400 个 frames。");
                    }

                    for (int j = 0; j < wave.frames.Count; j++)
                    {
                        int[] frame = wave.frames[j];
                        if (frame == null || frame.Length < 2)
                        {
                            throw new InvalidDataException("波形 " + DisplayName(wave) + " 包含非法 frame。");
                        }
                    }
                }
            }

            if (profile.events == null)
            {
                profile.events = new List<EventRule>();
            }
        }

        private static bool IsHexWave(WavePreset wave)
        {
            return wave != null && wave.hex != null && wave.hex.Count > 0;
        }

        private static StimProfile CreateHexImportProfile(string name, List<string> hex)
        {
            StimProfile profile = new StimProfile();
            profile.name = string.IsNullOrEmpty(name) ? "ImportedHex" : name;
            profile.version = "hex";
            profile.maxWaveIntensity = 100;
            profile.continuous = null;
            profile.waves = new List<WavePreset>();
            WavePreset wave = new WavePreset();
            wave.name = profile.name + ".Hex";
            wave.displayName = "导入 HEX 波形";
            wave.type = "hex";
            wave.shape = "Raw";
            wave.channel = "Both";
            wave.frequency = 0;
            wave.minStrength = 0;
            wave.maxStrength = 0;
            wave.durationMs = hex == null ? 0 : hex.Count * 100;
            wave.phase = 0.0;
            wave.hex = hex ?? new List<string>();
            profile.waves.Add(wave);
            profile.events = new List<EventRule>();
            return profile;
        }

        private static StimProfile CreatePulseImportProfile(string path)
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            List<string> hex = new List<string>();
            List<int[]> frames = new List<int[]>();
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = StripComment(lines[i]).Trim();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                if (IsPulseHex(line))
                {
                    hex.Add(line.ToUpperInvariant());
                    continue;
                }

                List<int> values = ExtractIntegers(line);
                if (values.Count >= 2)
                {
                    frames.Add(new int[] { Clamp(values[0], 10, 1000), Clamp(values[1], 0, 100) });
                }
            }

            if (hex.Count == 0 && frames.Count == 0)
            {
                throw new InvalidDataException("没有找到可导入的 HEX 或 frequency/strength 帧。");
            }

            StimProfile profile = new StimProfile();
            profile.name = Path.GetFileNameWithoutExtension(path);
            profile.version = "pulse";
            profile.maxWaveIntensity = 100;
            profile.continuous = null;
            profile.waves = new List<WavePreset>();
            profile.events = new List<EventRule>();

            if (hex.Count > 0)
            {
                WavePreset wave = new WavePreset();
                wave.name = profile.name + ".Hex";
                wave.displayName = "导入 .pulse HEX";
                wave.type = "hex";
                wave.shape = "Raw";
                wave.channel = "Both";
                wave.durationMs = Math.Min(hex.Count, 100) * 100;
                wave.hex = hex;
                profile.waves.Add(wave);
            }

            if (frames.Count > 0)
            {
                WavePreset wave = new WavePreset();
                wave.name = profile.name + ".Frames";
                wave.displayName = "导入 .pulse 帧";
                wave.type = "frames";
                wave.shape = "Raw";
                wave.channel = "Both";
                wave.durationMs = Math.Min(frames.Count, 400) * 25;
                wave.frames = frames;
                wave.hex = new List<string>();
                profile.waves.Add(wave);
            }

            return profile;
        }

        private static string StripComment(string line)
        {
            string value = line ?? "";
            int hash = value.IndexOf('#');
            if (hash >= 0) value = value.Substring(0, hash);
            int slash = value.IndexOf("//", StringComparison.Ordinal);
            if (slash >= 0) value = value.Substring(0, slash);
            return value;
        }

        private static List<int> ExtractIntegers(string line)
        {
            List<int> values = new List<int>();
            StringBuilder current = new StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if ((c >= '0' && c <= '9') || c == '-')
                {
                    current.Append(c);
                    continue;
                }

                FlushInteger(current, values);
            }

            FlushInteger(current, values);
            return values;
        }

        private static void FlushInteger(StringBuilder current, List<int> values)
        {
            if (current.Length == 0)
            {
                return;
            }

            int value;
            if (int.TryParse(current.ToString(), out value))
            {
                values.Add(value);
            }
            current.Length = 0;
        }

        private static bool IsPulseHex(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 16)
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!ok) return false;
            }

            return true;
        }

        private static void EnsureProfilesDirectory()
        {
            if (!Directory.Exists(ProfilesDirectory))
            {
                Directory.CreateDirectory(ProfilesDirectory);
            }
        }

        private static void AddBuiltInProfiles()
        {
            Profiles.Add(CreateProfile("StandardGame", 70, 11, 18, 17, 24, 65, 3500, 62, 4000, 100, 220));
            Profiles.Add(CreateProfile("ComfortContinuous", 40, 10, 8, 12, 10, 40, 3000, 40, 3500, 80, 120));
            Profiles.Add(CreateProfile("StrongPunish", 95, 14, 24, 22, 32, 90, 4500, 88, 5000, 90, 210));
            Profiles.Add(CreateProfile("DebugSync", 90, 8, 34, 10, 42, 75, 3500, 72, 4000, 90, 200));
        }

        private static StimProfile CreateProfile(string name, int maxWave, int walkBase, int walkBump, int runBase, int runBump, int hurtMax, int hurtMs, int deathIntensity, int deathMs, int sendInterval, int lookahead)
        {
            StimProfile profile = new StimProfile();
            profile.name = name;
            profile.version = "1";
            profile.maxWaveIntensity = maxWave;
            profile.continuous = new ContinuousStatePreset();
            profile.continuous.sendIntervalMs = sendInterval;
            profile.continuous.lookaheadMs = lookahead;
            profile.continuous.fadeOutMs = 400;
            profile.continuous.maxEventDurationMs = Math.Max(deathMs, hurtMs);
            profile.continuous.maxQueuedPulseItems = 30;
            profile.continuous.minimumChannelStrength = name == "ComfortContinuous" ? 10 : Math.Max(30, walkBase + 8);
            profile.continuous.walkBaseIntensity = walkBase;
            profile.continuous.walkBaseFrequency = 25;
            profile.continuous.walkBaseShape = "Tremor";
            profile.continuous.walkStepBumpIntensity = walkBump;
            profile.continuous.walkStepBumpDurationMs = 170;
            profile.continuous.runBaseIntensity = runBase;
            profile.continuous.runBaseFrequency = 280;
            profile.continuous.runBaseShape = "SawBurst";
            profile.continuous.runStepBumpIntensity = runBump;
            profile.continuous.runStepBumpDurationMs = 120;
            profile.continuous.slideBaseIntensity = Math.Max(16, runBase);
            profile.continuous.slideBaseFrequency = 260;
            profile.continuous.hurtMinIntensity = name == "ComfortContinuous" ? 10 : Math.Max(30, hurtMax / 3);
            profile.continuous.hurtMaxIntensity = hurtMax;
            profile.continuous.hurtDurationMinMs = Math.Max(800, hurtMs / 2);
            profile.continuous.hurtDurationMaxMs = hurtMs;
            profile.continuous.hurtFrequency = 60;
            profile.continuous.deathIntensity = deathIntensity;
            profile.continuous.deathDurationMs = deathMs;
            profile.continuous.deathFrequency = 150;
            profile.waves = CreateCommonWaves(name, walkBase, walkBump, runBase, runBump, hurtMax, hurtMs, deathIntensity, deathMs);
            profile.events = CreateCommonEvents();
            return profile;
        }

        private static List<WavePreset> CreateCommonWaves(string pack, int walkBase, int walkBump, int runBase, int runBump, int hurtMax, int hurtMs, int deathIntensity, int deathMs)
        {
            List<WavePreset> waves = new List<WavePreset>();
            waves.Add(Wave(pack + ".WalkBase", "走路基础波", "envelope", "Tremor", "Both", 25, Math.Max(2, walkBase / 2), walkBase, 800, 0.0));
            waves.Add(Wave(pack + ".WalkStepBump", "走路脚步叠加", "envelope", "SingleTap", "Alternate", 240, 0, walkBump, 180, 0.0));
            waves.Add(Wave(pack + ".RunBase", "奔跑基础波", "envelope", "SawBurst", "Both", 280, Math.Max(4, runBase / 2), runBase, 700, 0.12));
            waves.Add(Wave(pack + ".RunStepBump", "奔跑脚步叠加", "envelope", "SingleTap", "Alternate", 320, 0, runBump, 130, 0.0));
            int hurtMin = pack == "ComfortContinuous" ? 10 : Math.Max(30, hurtMax / 3);
            int deathMin = pack == "ComfortContinuous" ? 10 : Math.Max(30, deathIntensity / 4);
            waves.Add(Wave(pack + ".HurtPunish", "受击长惩罚", "envelope", "RampUpHoldDown", "Both", 60, hurtMin, hurtMax, hurtMs, 0.0));
            waves.Add(Wave(pack + ".DeathWave", "死亡长波", "envelope", "DeathWave", "Both", 150, deathMin, deathIntensity, deathMs, 0.17));
            waves.Add(Wave(pack + ".SlideTremor", "滑行持续颤动", "envelope", "Tremor", "Both", 260, Math.Max(8, runBase / 2), Math.Max(20, runBase + 8), 900, 0.33));
            waves.Add(Wave(pack + ".LandImpact", "落地冲击衰减", "envelope", "SingleTap", "Both", 320, 0, Math.Max(24, walkBump + 8), 450, 0.0));
            waves.Add(Wave(pack + ".EnemyHeartbeat", "敌人靠近提示", "envelope", "DoubleTap", "Both", 180, 4, 18, 900, 0.0));
            return waves;
        }

        private static WavePreset Wave(string name, string displayName, string type, string shape, string channel, int frequency, int minStrength, int maxStrength, int durationMs, double phase)
        {
            WavePreset wave = new WavePreset();
            wave.name = name;
            wave.displayName = displayName;
            wave.type = type;
            wave.shape = shape;
            wave.channel = channel;
            wave.frequency = frequency;
            wave.minStrength = minStrength;
            wave.maxStrength = maxStrength;
            wave.durationMs = durationMs;
            wave.phase = phase;
            wave.hex = new List<string>();
            return wave;
        }

        private static List<EventRule> CreateCommonEvents()
        {
            List<EventRule> events = new List<EventRule>();
            events.Add(Event("Walk", ".WalkBase", "Both", 1.0f, false, true, "移动期间持续基础波，脚步只做通道叠加。"));
            events.Add(Event("Run", ".RunBase", "Both", 1.0f, false, true, "奔跑基础波更密，脚步 bump 更尖。"));
            events.Add(Event("Hurt", ".HurtPunish", "Both", 1.0f, false, true, "受击进入更长时间的高频高强度惩罚基底。"));
            events.Add(Event("Death", ".DeathWave", "Both", 1.0f, true, true, "死亡清空旧队列后发送长波。"));
            events.Add(Event("Slide", ".SlideTremor", "Both", 1.0f, false, true, "滑行期间持续颤动。"));
            events.Add(Event("Land", ".LandImpact", "Both", 1.0f, false, false, "落地瞬间冲击后衰减。"));
            return events;
        }

        private static EventRule Event(string eventName, string waveName, string channelMode, float multiplier, bool clear, bool continuous, string note)
        {
            EventRule rule = new EventRule();
            rule.eventName = eventName;
            rule.waveName = waveName;
            rule.channelMode = channelMode;
            rule.strengthMultiplier = multiplier;
            rule.clearBeforeSend = clear;
            rule.continuousState = continuous;
            rule.note = note;
            return rule;
        }

        private static StimProfile CaptureCurrentProfile()
        {
            StimProfile profile = new StimProfile();
            profile.name = "Exported";
            profile.version = "1";
            profile.maxWaveIntensity = ModConfig.MaxWaveIntensity.Value;
            profile.continuous = new ContinuousStatePreset();
            profile.continuous.sendIntervalMs = ModConfig.ContinuousSendIntervalMs.Value;
            profile.continuous.lookaheadMs = ModConfig.ContinuousLookaheadMs.Value;
            profile.continuous.fadeOutMs = ModConfig.ContinuousFadeOutMs.Value;
            profile.continuous.maxEventDurationMs = ModConfig.MaxEventDurationMs.Value;
            profile.continuous.maxQueuedPulseItems = ModConfig.MaxQueuedPulseItems.Value;
            profile.continuous.minimumChannelStrength = ModConfig.MinimumChannelStrength.Value;
            profile.continuous.walkBaseIntensity = ModConfig.WalkBaseIntensity.Value;
            profile.continuous.walkBaseFrequency = ModConfig.WalkBaseFrequency.Value;
            profile.continuous.walkBaseShape = ModConfig.WalkBaseShape.Value.ToString();
            profile.continuous.walkStepBumpIntensity = ModConfig.WalkStepBumpIntensity.Value;
            profile.continuous.walkStepBumpDurationMs = ModConfig.WalkStepBumpDurationMs.Value;
            profile.continuous.runBaseIntensity = ModConfig.RunBaseIntensity.Value;
            profile.continuous.runBaseFrequency = ModConfig.RunBaseFrequency.Value;
            profile.continuous.runBaseShape = ModConfig.RunBaseShape.Value.ToString();
            profile.continuous.runStepBumpIntensity = ModConfig.RunStepBumpIntensity.Value;
            profile.continuous.runStepBumpDurationMs = ModConfig.RunStepBumpDurationMs.Value;
            profile.continuous.slideBaseIntensity = ModConfig.SlideBaseIntensity.Value;
            profile.continuous.slideBaseFrequency = ModConfig.SlideBaseFrequency.Value;
            profile.continuous.hurtMinIntensity = ModConfig.HurtMinIntensity.Value;
            profile.continuous.hurtMaxIntensity = ModConfig.HurtMaxIntensity.Value;
            profile.continuous.hurtDurationMinMs = ModConfig.HurtDurationMinMs.Value;
            profile.continuous.hurtDurationMaxMs = ModConfig.HurtDurationMaxMs.Value;
            profile.continuous.hurtFrequency = ModConfig.HurtFrequency.Value;
            profile.continuous.deathIntensity = ModConfig.DeathIntensity.Value;
            profile.continuous.deathDurationMs = ModConfig.DeathDurationMs.Value;
            profile.continuous.deathFrequency = ModConfig.DeathFrequency.Value;
            profile.waves = CreateCommonWaves("Exported", ModConfig.WalkBaseIntensity.Value, ModConfig.WalkStepBumpIntensity.Value, ModConfig.RunBaseIntensity.Value, ModConfig.RunStepBumpIntensity.Value, ModConfig.HurtMaxIntensity.Value, ModConfig.HurtDurationMaxMs.Value, ModConfig.DeathIntensity.Value, ModConfig.DeathDurationMs.Value);
            profile.events = CreateCommonEvents();
            return profile;
        }

        private static StimEnvelopeShape ParseEnvelope(string value, StimEnvelopeShape fallback)
        {
            try
            {
                if (string.IsNullOrEmpty(value)) return fallback;
                return (StimEnvelopeShape)Enum.Parse(typeof(StimEnvelopeShape), value, true);
            }
            catch
            {
                return fallback;
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
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
    }
}
