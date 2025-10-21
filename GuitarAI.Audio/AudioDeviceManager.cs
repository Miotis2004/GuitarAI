using NAudio.Wave;
using System.Collections.Generic;
using System.Linq;

namespace GuitarAI.Audio
{
    /// <summary>
    /// Helper class to enumerate and manage audio devices
    /// </summary>
    public class AudioDeviceManager
    {
        public class AudioDevice
        {
            public int DeviceNumber { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Channels { get; set; }

            public override string ToString() => Name;
        }

        /// <summary>
        /// Get all available input devices
        /// </summary>
        public static List<AudioDevice> GetInputDevices()
        {
            var devices = new List<AudioDevice>();

            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var caps = WaveInEvent.GetCapabilities(i);
                devices.Add(new AudioDevice
                {
                    DeviceNumber = i,
                    Name = caps.ProductName,
                    Channels = caps.Channels
                });
            }

            return devices;
        }

        /// <summary>
        /// Get all available output devices using WaveOutEvent
        /// </summary>
        public static List<AudioDevice> GetOutputDevices()
        {
            var devices = new List<AudioDevice>();

            // Enumerate output devices by trying to query capabilities
            // WaveOutEvent doesn't have static DeviceCount, so we probe until we fail
            int deviceNumber = 0;
            while (true)
            {
                try
                {
                    var waveOut = new WaveOutEvent { DeviceNumber = deviceNumber };

                    // If we can create it, the device exists
                    // Get capabilities using the Windows Multimedia API
                    var capabilities = WaveInterop.WaveOutGetCapabilities(deviceNumber);

                    devices.Add(new AudioDevice
                    {
                        DeviceNumber = deviceNumber,
                        Name = capabilities.ProductName,
                        Channels = capabilities.Channels
                    });

                    deviceNumber++;
                }
                catch
                {
                    // No more devices
                    break;
                }
            }

            return devices;
        }

        /// <summary>
        /// Find device by name (partial match)
        /// </summary>
        public static AudioDevice? FindInputDevice(string namePattern)
        {
            return GetInputDevices()
                .FirstOrDefault(d => d.Name.Contains(namePattern, System.StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Find output device by name (partial match)
        /// </summary>
        public static AudioDevice? FindOutputDevice(string namePattern)
        {
            return GetOutputDevices()
                .FirstOrDefault(d => d.Name.Contains(namePattern, System.StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Helper class to access WaveOut capabilities
    /// </summary>
    internal static class WaveInterop
    {
        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        private static extern int waveOutGetNumDevs();

        [System.Runtime.InteropServices.DllImport("winmm.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int waveOutGetDevCaps(int deviceID, out WaveOutCapabilities caps, int sizeOfWaveOutCaps);

        public static WaveOutCapabilities WaveOutGetCapabilities(int deviceNumber)
        {
            WaveOutCapabilities caps;
            int result = waveOutGetDevCaps(deviceNumber, out caps, System.Runtime.InteropServices.Marshal.SizeOf<WaveOutCapabilities>());
            if (result != 0)
            {
                throw new System.Exception($"Failed to get device capabilities for device {deviceNumber}");
            }
            return caps;
        }

        public static int GetDeviceCount()
        {
            return waveOutGetNumDevs();
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public struct WaveOutCapabilities
        {
            private short manufacturerId;
            private short productId;
            private int driverVersion;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 32)]
            public string ProductName;
            private int formats;
            public short Channels;
            private short reserved;
            private int support;
        }
    }
}