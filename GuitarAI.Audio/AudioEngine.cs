using GuitarAI.Core;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Reflection;

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
        private VolumeSampleProvider? volumeProvider;

        private bool isRunning;
        private readonly int sampleRate;
        private readonly int channels;
        private readonly List<IEffect> effects = new List<IEffect>();

        public bool IsRunning => isRunning;
        public event EventHandler<string>? ErrorOccurred;
        public event EventHandler<float>? AudioLevelChanged;
        public IReadOnlyList<IEffect> Effects => effects;

        public AudioEngine(int sampleRate = 44100, int channels = 1)
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
                // Set up input (guitar)
                waveIn = new WaveInEvent
                {
                    DeviceNumber = inputDeviceNumber,
                    WaveFormat = new WaveFormat(sampleRate, 16, channels),
                    BufferMilliseconds = 50  // Increased for stability
                };

                // Set up buffered provider for processing
                waveProvider = new BufferedWaveProvider(waveIn.WaveFormat)
                {
                    BufferLength = sampleRate * channels * 2,  // 1 second buffer
                    DiscardOnBufferOverflow = true,
                    ReadFully = false  // Important: don't wait for buffer to fill
                };

                // Convert to float samples for processing
                var sampleProvider = waveProvider.ToSampleProvider();

                // Add volume control
                volumeProvider = new VolumeSampleProvider(sampleProvider)
                {
                    Volume = 1.0f
                };

                // Process audio callback
                waveIn.DataAvailable += OnDataAvailable;

                // Set up output with larger latency to prevent crackling
                waveOut = new WaveOutEvent
                {
                    DeviceNumber = outputDeviceNumber,
                    DesiredLatency = 100,  // Increased from 40ms
                    NumberOfBuffers = 3    // More buffers for stability
                };

                waveOut.Init(volumeProvider);

                // Start recording first, then playback
                waveIn.StartRecording();

                // Give the buffer a moment to start filling
                System.Threading.Thread.Sleep(50);

                waveOut.Play();

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

            // Check buffer level - if too full, we have latency building up
            var bufferedDuration = waveProvider.BufferedDuration;
            if (bufferedDuration.TotalMilliseconds > 500)
            {
                // Clear excess to prevent echo/delay buildup
                waveProvider.ClearBuffer();
            }

            // Process effects on the audio data
            ProcessEffects(e.Buffer, 0, e.BytesRecorded);

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

        public void SetVolume(float volume)
        {
            if (volumeProvider != null)
            {
                volumeProvider.Volume = Math.Clamp(volume, 0f, 2f);
            }
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
            volumeProvider = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}