using GuitarAI.Audio;
using GuitarAI.Core;
using System;
using System.Windows;

namespace GuitarAI
{
    public partial class MainWindow : Window
    {
        private AudioEngine? audioEngine;
        private AsioAudioEngine? asioEngine;
        private OverdriveEffect? overdriveEffect;
        private bool useAsio = true; // Default to ASIO for low latency

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAudioDevices();
            LogStatus("Application started. Select devices and click 'Start Audio Engine'.");
        }

        private void LoadAudioDevices()
        {
            // Load input devices
            var inputDevices = AudioDeviceManager.GetInputDevices();
            InputDeviceComboBox.ItemsSource = inputDevices;
            if (inputDevices.Count > 0)
            {
                InputDeviceComboBox.SelectedIndex = 0;
            }

            // Load output devices
            var outputDevices = AudioDeviceManager.GetOutputDevices();
            OutputDeviceComboBox.ItemsSource = outputDevices;
            if (outputDevices.Count > 0)
            {
                OutputDeviceComboBox.SelectedIndex = 0;
            }

            LogStatus($"Found {inputDevices.Count} input device(s) and {outputDevices.Count} output device(s).");
        }

        private void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (audioEngine == null || !audioEngine.IsRunning)
            {
                StartAudioEngine();
            }
            else
            {
                StopAudioEngine();
            }
        }

        private void StartAudioEngine()
        {
            try
            {
                // Try to use ASIO first (much lower latency)
                var asioDrivers = AsioAudioEngine.GetAsioDriverNames();

                if (asioDrivers.Length > 0 && useAsio)
                {
                    // Use ASIO
                    LogStatus("Using ASIO for low latency...");

                    // Find Focusrite driver (or use first available)
                    string driverName = asioDrivers[0];
                    foreach (var driver in asioDrivers)
                    {
                        if (driver.Contains("Focusrite", StringComparison.OrdinalIgnoreCase))
                        {
                            driverName = driver;
                            break;
                        }
                    }

                    LogStatus($"ASIO Driver: {driverName}");

                    // Create and configure overdrive effect
                    overdriveEffect = new OverdriveEffect
                    {
                        Enabled = OverdriveEnabledCheckBox.IsChecked ?? true,
                        Gain = (float)GainSlider.Value,
                        Drive = (float)DriveSlider.Value,
                        Tone = (float)ToneSlider.Value,
                        OutputLevel = (float)OutputLevelSlider.Value
                    };

                    // Create ASIO engine
                    asioEngine = new AsioAudioEngine();
                    asioEngine.ErrorOccurred += AudioEngine_ErrorOccurred;
                    asioEngine.AudioLevelChanged += AudioEngine_AudioLevelChanged;
                    asioEngine.AddEffect(overdriveEffect);

                    asioEngine.Start(driverName);

                    // Update UI
                    StartStopButton.Content = "Stop Audio Engine";
                    InputDeviceComboBox.IsEnabled = false;
                    OutputDeviceComboBox.IsEnabled = false;

                    LogStatus("ASIO audio engine started (low latency mode)");
                    LogStatus("Overdrive effect loaded and active.");
                }
                else
                {
                    // Fall back to regular audio engine
                    LogStatus("ASIO not available, using standard audio...");

                    // Get selected devices
                    var inputDevice = InputDeviceComboBox.SelectedItem as AudioDeviceManager.AudioDevice;
                    var outputDevice = OutputDeviceComboBox.SelectedItem as AudioDeviceManager.AudioDevice;

                    if (inputDevice == null || outputDevice == null)
                    {
                        MessageBox.Show("Please select both input and output devices.", "Device Selection",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Create audio engine
                    audioEngine = new AudioEngine();
                    audioEngine.ErrorOccurred += AudioEngine_ErrorOccurred;
                    audioEngine.AudioLevelChanged += AudioEngine_AudioLevelChanged;

                    // Create and configure overdrive effect
                    overdriveEffect = new OverdriveEffect
                    {
                        Enabled = OverdriveEnabledCheckBox.IsChecked ?? true,
                        Gain = (float)GainSlider.Value,
                        Drive = (float)DriveSlider.Value,
                        Tone = (float)ToneSlider.Value,
                        OutputLevel = (float)OutputLevelSlider.Value
                    };

                    // Add effect to the audio engine
                    audioEngine.AddEffect(overdriveEffect);

                    // Start the engine
                    audioEngine.Start(inputDevice.DeviceNumber, outputDevice.DeviceNumber);

                    // Update UI
                    StartStopButton.Content = "Stop Audio Engine";
                    InputDeviceComboBox.IsEnabled = false;
                    OutputDeviceComboBox.IsEnabled = false;

                    LogStatus($"Audio engine started.");
                    LogStatus($"Input: {inputDevice.Name}");
                    LogStatus($"Output: {outputDevice.Name}");
                    LogStatus($"Overdrive effect loaded and active.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start audio engine: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                LogStatus($"ERROR: {ex.Message}");
            }
        }

        private void StopAudioEngine()
        {
            if (audioEngine != null)
            {
                audioEngine.ErrorOccurred -= AudioEngine_ErrorOccurred;
                audioEngine.AudioLevelChanged -= AudioEngine_AudioLevelChanged;
                audioEngine.Stop();
                audioEngine.Dispose();
                audioEngine = null;
            }

            if (asioEngine != null)
            {
                asioEngine.ErrorOccurred -= AudioEngine_ErrorOccurred;
                asioEngine.AudioLevelChanged -= AudioEngine_AudioLevelChanged;
                asioEngine.Stop();
                asioEngine.Dispose();
                asioEngine = null;
            }

            // Update UI
            StartStopButton.Content = "Start Audio Engine";
            InputDeviceComboBox.IsEnabled = true;
            OutputDeviceComboBox.IsEnabled = true;
            AudioLevelMeter.Value = 0;

            LogStatus("Audio engine stopped.");

            overdriveEffect = null;
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (audioEngine != null)
            {
                audioEngine.SetVolume((float)e.NewValue);
            }

            if (asioEngine != null)
            {
                asioEngine.SetVolume((float)e.NewValue);
            }

            if (VolumeLabel != null)
            {
                VolumeLabel.Text = $"{(int)(e.NewValue * 100)}%";
            }
        }

        private void OverdriveEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (overdriveEffect != null)
            {
                overdriveEffect.Enabled = OverdriveEnabledCheckBox.IsChecked ?? false;
                LogStatus($"Overdrive effect {(overdriveEffect.Enabled ? "enabled" : "disabled")}");
            }
        }

        private void GainSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (overdriveEffect != null)
            {
                overdriveEffect.Gain = (float)e.NewValue;
            }

            if (GainLabel != null)
            {
                GainLabel.Text = e.NewValue.ToString("F1");
            }
        }

        private void DriveSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (overdriveEffect != null)
            {
                overdriveEffect.Drive = (float)e.NewValue;
            }

            if (DriveLabel != null)
            {
                DriveLabel.Text = e.NewValue.ToString("F1");
            }
        }

        private void ToneSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (overdriveEffect != null)
            {
                overdriveEffect.Tone = (float)e.NewValue;
            }

            if (ToneLabel != null)
            {
                ToneLabel.Text = e.NewValue.ToString("F2");
            }
        }

        private void OutputLevelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (overdriveEffect != null)
            {
                overdriveEffect.OutputLevel = (float)e.NewValue;
            }

            if (OutputLevelLabel != null)
            {
                OutputLevelLabel.Text = e.NewValue.ToString("F2");
            }
        }

        private void AudioEngine_AudioLevelChanged(object? sender, float level)
        {
            // Update on UI thread
            Dispatcher.Invoke(() =>
            {
                AudioLevelMeter.Value = level;
            });
        }

        private void AudioEngine_ErrorOccurred(object? sender, string errorMessage)
        {
            Dispatcher.Invoke(() =>
            {
                LogStatus($"ERROR: {errorMessage}");
                MessageBox.Show(errorMessage, "Audio Engine Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        private void LogStatus(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            StatusTextBlock.Text += $"[{timestamp}] {message}\n";
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopAudioEngine();
        }
    }
}