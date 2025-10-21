using GuitarAI.Audio;
using System;
using System.Windows;

namespace GuitarAI
{
    public partial class MainWindow : Window
    {
        private AudioEngine? audioEngine;

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
                // Get selected devices
                var inputDevice = InputDeviceComboBox.SelectedItem as AudioDeviceManager.AudioDevice;
                var outputDevice = OutputDeviceComboBox.SelectedItem as AudioDeviceManager.AudioDevice;

                if (inputDevice == null || outputDevice == null)
                {
                    MessageBox.Show("Please select both input and output devices.", "Device Selection",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Create and start audio engine
                audioEngine = new AudioEngine();
                audioEngine.ErrorOccurred += AudioEngine_ErrorOccurred;
                audioEngine.AudioLevelChanged += AudioEngine_AudioLevelChanged;

                audioEngine.Start(inputDevice.DeviceNumber, outputDevice.DeviceNumber);

                // Update UI
                StartStopButton.Content = "Stop Audio Engine";
                InputDeviceComboBox.IsEnabled = false;
                OutputDeviceComboBox.IsEnabled = false;

                LogStatus($"Audio engine started.");
                LogStatus($"Input: {inputDevice.Name}");
                LogStatus($"Output: {outputDevice.Name}");
                LogStatus("Playing audio through (currently no effects applied)...");
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

                // Update UI
                StartStopButton.Content = "Start Audio Engine";
                InputDeviceComboBox.IsEnabled = true;
                OutputDeviceComboBox.IsEnabled = true;
                AudioLevelMeter.Value = 0;

                LogStatus("Audio engine stopped.");
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (audioEngine != null)
            {
                audioEngine.SetVolume((float)e.NewValue);
            }

            if (VolumeLabel != null)
            {
                VolumeLabel.Text = $"{(int)(e.NewValue * 100)}%";
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