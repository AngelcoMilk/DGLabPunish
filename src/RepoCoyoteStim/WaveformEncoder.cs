using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RepoCoyoteStim
{
    internal static class WaveformEncoder
    {
        internal static List<string> SmoothPulse(int frequency, int strength, int durationMs)
        {
            int segments = DurationToSegments(durationMs);
            List<int> frequencies = new List<int>();
            List<int> strengths = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                frequencies.Add(frequency);
                strengths.Add(strength);
            }

            return Pulse(frequencies, strengths);
        }

        internal static List<string> GradientPulse(int frequency, int startStrength, int endStrength, int durationMs)
        {
            int segments = DurationToSegments(durationMs);
            List<int> frequencies = new List<int>();
            List<int> strengths = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                double t = segments <= 1 ? 1.0 : (double)i / (double)(segments - 1);
                frequencies.Add(frequency);
                strengths.Add((int)Math.Round(startStrength + (endStrength - startStrength) * t));
            }

            return Pulse(frequencies, strengths);
        }

        internal static List<string> SinPulse(int frequency, int minStrength, int maxStrength, int durationMs)
        {
            int segments = DurationToSegments(durationMs);
            List<int> frequencies = new List<int>();
            List<int> strengths = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                double t = segments <= 1 ? 1.0 : (double)i / (double)(segments - 1);
                double curve = Math.Sin(Math.PI * t);
                frequencies.Add(frequency);
                strengths.Add((int)Math.Round(minStrength + (maxStrength - minStrength) * curve));
            }

            return Pulse(frequencies, strengths);
        }

        internal static List<string> EnvelopePulse(StimEnvelopeShape shape, int frequency, int minStrength, int maxStrength, int durationMs, double phase)
        {
            int segments = DurationToSegments(durationMs);
            List<int> frequencies = new List<int>();
            List<int> strengths = new List<int>();
            int min = Clamp(minStrength, 0, 100);
            int max = Clamp(maxStrength, min, 100);

            for (int i = 0; i < segments; i++)
            {
                double t = segments <= 1 ? 1.0 : (double)i / (double)(segments - 1);
                double curve = EnvelopeCurve(shape, t, phase);
                frequencies.Add(frequency);
                strengths.Add((int)Math.Round(min + (max - min) * curve));
            }

            return Pulse(frequencies, strengths);
        }

        internal static double EvaluateEnvelope(StimEnvelopeShape shape, double t, double phase)
        {
            return EnvelopeCurve(shape, t, phase);
        }

        internal static List<string> Pulse(IList<int> frequencies, IList<int> strengths)
        {
            if (frequencies == null || strengths == null || frequencies.Count != strengths.Count)
            {
                throw new ArgumentException("Frequency and strength counts must match.");
            }

            List<string> output = new List<string>();
            StringBuilder freqPart = new StringBuilder();
            StringBuilder strengthPart = new StringBuilder();

            for (int i = 0; i < strengths.Count; i++)
            {
                freqPart.Append(ToHexByte(ConvertFrequency(frequencies[i])));
                strengthPart.Append(ToHexByte(Clamp(strengths[i], 0, 100)));

                if ((i + 1) % 4 == 0)
                {
                    output.Add(freqPart.ToString() + strengthPart.ToString());
                    freqPart.Length = 0;
                    strengthPart.Length = 0;
                }
            }

            if (freqPart.Length > 0 || strengthPart.Length > 0)
            {
                while (freqPart.Length < 8) freqPart.Append('0');
                while (strengthPart.Length < 8) strengthPart.Append('0');
                output.Add(freqPart.ToString() + strengthPart.ToString());
            }

            return output;
        }

        internal static string ToJsonArray(IList<string> pulses, int count)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < count && i < pulses.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append("\"");
                sb.Append(pulses[i]);
                sb.Append("\"");
            }
            sb.Append("]");
            return sb.ToString();
        }

        private static int DurationToSegments(int durationMs)
        {
            int safeDuration = Clamp(durationMs, 25, Math.Max(25, ModConfig.MaxEventDurationMs.Value));
            return Math.Max(1, (int)Math.Ceiling(safeDuration / 25.0));
        }

        private static double EnvelopeCurve(StimEnvelopeShape shape, double t, double phase)
        {
            if (shape == StimEnvelopeShape.SingleTap)
            {
                return Math.Pow(Math.Max(0.0, 1.0 - t), 1.35);
            }

            if (shape == StimEnvelopeShape.DoubleTap)
            {
                double first = Math.Sin(Math.PI * Clamp01(t / 0.42));
                double second = Math.Sin(Math.PI * Clamp01((t - 0.52) / 0.42));
                return Math.Max(0.0, Math.Max(first, second * 0.9));
            }

            if (shape == StimEnvelopeShape.RampUpHoldDown)
            {
                if (t < 0.32) return Smooth01(t / 0.32);
                if (t < 0.64) return 1.0;
                return Smooth01(1.0 - (t - 0.64) / 0.36);
            }

            if (shape == StimEnvelopeShape.SawBurst)
            {
                double wave = (t * 5.0 + phase) % 1.0;
                return 0.25 + 0.75 * (1.0 - wave);
            }

            if (shape == StimEnvelopeShape.Tremor)
            {
                double wave = Math.Sin((t * 8.0 + phase) * Math.PI * 2.0);
                return 0.45 + 0.55 * Math.Abs(wave);
            }

            double baseWave = Math.Sin(Math.PI * t);
            double ripple = 0.18 * Math.Sin((t * 6.0 + phase) * Math.PI * 2.0);
            return Clamp01(baseWave + ripple);
        }

        private static double Smooth01(double t)
        {
            double x = Clamp01(t);
            return x * x * (3.0 - 2.0 * x);
        }

        private static double Clamp01(double value)
        {
            if (value < 0.0) return 0.0;
            if (value > 1.0) return 1.0;
            return value;
        }

        private static int ConvertFrequency(int frequency)
        {
            int f = Clamp(frequency, 10, 1000);
            if (f <= 100) return f;
            if (f < 600) return (f - 100) / 5 + 100;
            return (f - 600) / 10 + 200;
        }

        private static string ToHexByte(int value)
        {
            return Clamp(value, 0, 255).ToString("X2", CultureInfo.InvariantCulture);
        }

        internal static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
