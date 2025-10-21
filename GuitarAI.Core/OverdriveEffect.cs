using System;

namespace GuitarAI.Core
{
    /// <summary>
    /// Overdrive/Distortion effect using soft clipping
    /// </summary>
    public class OverdriveEffect : IEffect
    {
        private float gain = 1.0f;
        private float drive = 1.0f;
        private float tone = 0.5f;
        private float outputLevel = 1.0f;

        // Simple lowpass filter for tone control
        private float lastSample = 0f;

        public string Name => "Overdrive";
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Input gain (1.0 = unity, higher = more overdrive)
        /// Range: 1.0 to 20.0
        /// </summary>
        public float Gain
        {
            get => gain;
            set => gain = Math.Clamp(value, 1.0f, 20.0f);
        }

        /// <summary>
        /// Drive amount (how hard the signal clips)
        /// Range: 0.1 to 10.0
        /// </summary>
        public float Drive
        {
            get => drive;
            set => drive = Math.Clamp(value, 0.1f, 10.0f);
        }

        /// <summary>
        /// Tone control (0 = dark, 1 = bright)
        /// Range: 0.0 to 1.0
        /// </summary>
        public float Tone
        {
            get => tone;
            set => tone = Math.Clamp(value, 0.0f, 1.0f);
        }

        /// <summary>
        /// Output level compensation
        /// Range: 0.0 to 2.0
        /// </summary>
        public float OutputLevel
        {
            get => outputLevel;
            set => outputLevel = Math.Clamp(value, 0.0f, 2.0f);
        }

        public void Process(byte[] buffer, int offset, int count)
        {
            if (!Enabled) return;

            // Process 16-bit samples
            for (int i = offset; i < offset + count; i += 2)
            {
                // Read 16-bit sample
                short sample = (short)((buffer[i + 1] << 8) | buffer[i]);

                // Convert to float (-1.0 to 1.0)
                float floatSample = sample / 32768f;

                // Apply overdrive
                floatSample = ProcessSample(floatSample);

                // Convert back to 16-bit
                short processed = (short)(Math.Clamp(floatSample, -1.0f, 1.0f) * 32767f);

                // Write back
                buffer[i] = (byte)(processed & 0xFF);
                buffer[i + 1] = (byte)((processed >> 8) & 0xFF);
            }
        }

        public void ProcessSamples(float[] samples, int offset, int count)
        {
            if (!Enabled) return;

            for (int i = offset; i < offset + count; i++)
            {
                samples[i] = ProcessSample(samples[i]);
            }
        }

        private float ProcessSample(float input)
        {
            // Apply input gain
            float signal = input * gain;

            // Soft clipping (hyperbolic tangent)
            signal = SoftClip(signal * drive) / drive;

            // Simple tone control (lowpass filter)
            signal = ApplyTone(signal);

            // Output level compensation
            signal *= outputLevel;

            return signal;
        }

        private float SoftClip(float sample)
        {
            // Hyperbolic tangent soft clipping
            // Keeps signal in -1 to 1 range while adding harmonics
            if (sample > 1.5f) return 1.0f;
            if (sample < -1.5f) return -1.0f;

            return (float)Math.Tanh(sample);
        }

        private float ApplyTone(float sample)
        {
            // Simple one-pole lowpass filter
            // Tone = 0: very dark (heavy filtering)
            // Tone = 1: bright (no filtering)
            float alpha = tone;
            lastSample = (alpha * sample) + ((1.0f - alpha) * lastSample);
            return lastSample;
        }

        public void Reset()
        {
            lastSample = 0f;
        }
    }
}