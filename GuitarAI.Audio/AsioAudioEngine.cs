using NAudio.Wave;
using System;
using System.Collections.Generic;
using GuitarAI.Core;

namespace GuitarAI.Audio
{
    /// <summary>
    /// Low-latency ASIO-based audio engine for guitar processing
    /// </summary>
    public class AsioAudioEngine : IDisposable
    {
        private AsioOut? asioOut;
        private AsioAudioProvider? provider;
        private readonly List<IEffect> effects = new List<IEffect>();
        private float volume = 1.0f;
        private bool isRunning;

        public bool IsRunning => isRunning;
        public event EventHandler<string>? ErrorOccurred;
        public event EventHandler<float>? AudioLevelChanged;
        public IReadOnlyList<IEffect> Effects => effects;

        public void Start(string asioDriverName)
        {
            try
            {
                asioOut = new AsioOut(asioDriverName);

                // Create our audio provider
                provider = new AsioAudioProvider(this);

                asioOut.Init(provider);

                System.Diagnostics.Debug.WriteLine($"ASIO Driver: {asioDriverName}");
                System.Diagnostics.Debug.WriteLine($"ASIO Latency: {asioOut.PlaybackLatency}ms");

                asioOut.Play();

                isRunning = true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Failed to start ASIO: {ex.Message}");
                Stop();
            }
        }

        internal void ProcessAudio(float[] buffer, int offset, int count)
        {
            // Calculate level before processing
            float level = CalculateLevel(buffer, offset, count);
            AudioLevelChanged?.Invoke(this, level);

            // Convert to 16-bit for effect processing
            byte[] byteBuffer = new byte[count * 2];
            FloatToBytes(buffer, offset, count, byteBuffer);

            // Process effects
            foreach (var effect in effects)
            {
                if (effect.Enabled)
                {
                    effect.Process(byteBuffer, 0, byteBuffer.Length);
                }
            }

            // Convert back to float and apply volume
            BytesToFloat(byteBuffer, buffer, offset, count, volume);
        }

        private void FloatToBytes(float[] floatBuffer, int offset, int count, byte[] byteBuffer)
        {
            for (int i = 0; i < count; i++)
            {
                short sample = (short)(Math.Clamp(floatBuffer[offset + i], -1f, 1f) * 32767f);
                byteBuffer[i * 2] = (byte)(sample & 0xFF);
                byteBuffer[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }
        }

        private void BytesToFloat(byte[] byteBuffer, float[] floatBuffer, int offset, int count, float volumeMultiplier)
        {
            for (int i = 0; i < count; i++)
            {
                short sample = (short)((byteBuffer[i * 2 + 1] << 8) | byteBuffer[i * 2]);
                floatBuffer[offset + i] = (sample / 32768f) * volumeMultiplier;
            }
        }

        private float CalculateLevel(float[] buffer, int offset, int count)
        {
            float sum = 0;
            for (int i = offset; i < offset + count; i++)
            {
                sum += buffer[i] * buffer[i];
            }
            return (float)Math.Sqrt(sum / count);
        }

        public void SetVolume(float newVolume)
        {
            volume = Math.Clamp(newVolume, 0f, 2f);
        }

        public void AddEffect(IEffect effect)
        {
            effects.Add(effect);
        }

        public void RemoveEffect(IEffect effect)
        {
            effects.Remove(effect);
        }

        public void ClearEffects()
        {
            effects.Clear();
        }

        public void Stop()
        {
            isRunning = false;

            if (asioOut != null)
            {
                asioOut.Stop();
                asioOut.Dispose();
                asioOut = null;
            }

            provider = null;
        }

        public void Dispose()
        {
            Stop();
        }

        public static string[] GetAsioDriverNames()
        {
            return AsioOut.GetDriverNames();
        }
    }

    /// <summary>
    /// Audio provider for ASIO that processes input and provides output
    /// </summary>
    internal class AsioAudioProvider : ISampleProvider
    {
        private readonly AsioAudioEngine engine;

        public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

        public AsioAudioProvider(AsioAudioEngine engine)
        {
            this.engine = engine;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            // ASIO provides input in the buffer
            // We process it and return the same buffer as output
            engine.ProcessAudio(buffer, offset, count);
            return count;
        }
    }
}