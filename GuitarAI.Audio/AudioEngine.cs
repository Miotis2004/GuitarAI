using NAudio.Wave;
using System;
using System.Collections.Generic;
using GuitarAI.Core;

namespace GuitarAI.Audio
{
    /// <summary>
    /// Core audio engine that manages input/output and processing chain
    /// </summary>
    public class AudioEngine : IDisposable
    {
        private WaveInEvent? waveIn;
        private WaveOutEvent? waveOut;
        private BufferedWaveProvider? waveProvider;

        private bool isRunning;
        private readonly int sampleRate;
        private readonly int channels;
        private readonly List<IEffect> effects = new List<IEffect>();
        private float volume = 1.0f;

        public bool IsRunning => isRunning;
        public event EventHandler<string>? ErrorOccurred;
        public event EventHandler<float>? AudioLevelChanged;
        public IReadOnlyList<IEffect> Effects => effects;

        public AudioEngine(int sampleRate = 48000, int channels = 2)
        {
            this.sampleRate = sampleRate;
            this.channels = channels;
        }

        /// <summary>
        /// Initialize and start the audio engine
        /// </summary>
        public void Start(int inputDeviceNumber = 0, int outputDeviceNumber = 0)
        {
            try
            {
                // Set up input (guitar) - 16-bit PCM
                waveIn = new WaveInEvent
                {
                    DeviceNumber = inputDeviceNumber,
                    WaveFormat = new WaveFormat(sampleRate, 16, channels),
                    BufferMilliseconds = 50
                };

                // Set up buffered provider - keep in 16-bit PCM format
                waveProvider = new BufferedWaveProvider(waveIn.WaveFormat)
                {
                    BufferLength = sampleRate * channels * 4,  // 2 seconds
                    DiscardOnBufferOverflow = true
                };

                // Process audio callback
                waveIn.DataAvailable += OnDataAvailable;

                // Set up output - same format as input
                waveOut = new WaveOutEvent
                {
                    DeviceNumber = outputDeviceNumber,
                    DesiredLatency = 100,
                    NumberOfBuffers = 3
                };

                waveOut.Init(waveProvider);

                System.Diagnostics.Debug.WriteLine($"AudioEngine: Input format: {waveIn.WaveFormat}");
                System.Diagnostics.Debug.WriteLine($"AudioEngine: Output format: {waveOut.OutputWaveFormat}");

                // Start recording first, then playback
                waveIn.StartRecording();

                System.Diagnostics.Debug.WriteLine("AudioEngine: Recording started");

                // Give the buffer a moment to start filling
                System.Threading.Thread.Sleep(100);

                waveOut.Play();

                System.Diagnostics.Debug.WriteLine($"AudioEngine: Playback started. WaveOut state: {waveOut.PlaybackState}");
                System.Diagnostics.Debug.WriteLine($"AudioEngine: Effects count: {effects.Count}");

                isRunning = true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Failed to start audio engine: {ex.Message}");
                Stop();
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (waveProvider == null) return;

            // Process effects on the audio data
            ProcessEffects(e.Buffer, 0, e.BytesRecorded);

            // Apply volume by modifying the buffer directly
            ApplyVolume(e.Buffer, 0, e.BytesRecorded);

            // Write processed data to the buffer for output
            waveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);

            // Calculate audio level for UI feedback
            float level = CalculateLevel(e.Buffer, e.BytesRecorded);
            AudioLevelChanged?.Invoke(this, level);
        }

        private void ProcessEffects(byte[] buffer, int offset, int count)
        {
            // Process each effect in the chain
            foreach (var effect in effects)
            {
                if (effect.Enabled)
                {
                    effect.Process(buffer, offset, count);
                }
            }
        }

        private void ApplyVolume(byte[] buffer, int offset, int count)
        {
            if (volume == 1.0f) return; // Skip if unity gain

            // Apply volume to 16-bit samples
            for (int i = offset; i < offset + count; i += 2)
            {
                short sample = (short)((buffer[i + 1] << 8) | buffer[i]);
                sample = (short)(sample * volume);
                buffer[i] = (byte)(sample & 0xFF);
                buffer[i + 1] = (byte)((sample >> 8) & 0xFF);
            }
        }

        private float CalculateLevel(byte[] buffer, int bytesRecorded)
        {
            if (bytesRecorded == 0) return 0;

            float sum = 0;
            int samplesRecorded = bytesRecorded / 2; // 16-bit samples

            for (int i = 0; i < samplesRecorded; i++)
            {
                short sample = (short)((buffer[i * 2 + 1] << 8) | buffer[i * 2]);
                float sampleValue = sample / 32768f;
                sum += sampleValue * sampleValue;
            }

            return (float)Math.Sqrt(sum / samplesRecorded);
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

            if (waveIn != null)
            {
                waveIn.DataAvailable -= OnDataAvailable;
                waveIn.StopRecording();
                waveIn.Dispose();
                waveIn = null;
            }

            if (waveOut != null)
            {
                waveOut.Stop();
                waveOut.Dispose();
                waveOut = null;
            }

            waveProvider = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}