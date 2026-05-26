using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using PowerMonitor.Core.Models;

namespace PowerMonitor.Core.Hardware;

/// <summary>
/// Estimate module power for peripherals that rarely expose direct watt sensors.
/// </summary>
public sealed class PeripheralPowerEstimator : IDisposable
{
    private DateTimeOffset _lastDisplayRefresh = DateTimeOffset.MinValue;
    private DateTimeOffset _lastBluetoothRefresh = DateTimeOffset.MinValue;

    private double _cachedDisplayPower;
    private bool _cachedBluetoothEnabled;

    private bool _wifiInitialized;
    private long _lastWifiBytes;
    private DateTimeOffset _lastWifiSampleTime;
    private double _lastWifiPower;

    public PowerModuleReading[] ReadEstimatedModules(bool includeWifiEstimate)
    {
        var modules = new List<PowerModuleReading>(3);

        var now = DateTimeOffset.Now;

        var displayPower = GetDisplayPowerEstimate(now);
        if (displayPower > 0)
        {
            modules.Add(new PowerModuleReading("屏幕(估算)", displayPower, 1));
        }

        if (includeWifiEstimate)
        {
            var wifiPower = GetWifiPowerEstimate(now);
            if (wifiPower > 0)
            {
                modules.Add(new PowerModuleReading("无线网(估算)", wifiPower, 1));
            }
        }

        var bluetoothPower = GetBluetoothPowerEstimate(now);
        if (bluetoothPower > 0)
        {
            modules.Add(new PowerModuleReading("蓝牙(估算)", bluetoothPower, 1));
        }

        return modules.ToArray();
    }

    private double GetDisplayPowerEstimate(DateTimeOffset now)
    {
        if ((now - _lastDisplayRefresh).TotalSeconds >= 5)
        {
            _lastDisplayRefresh = now;
            _cachedDisplayPower = EstimateDisplayPowerFromBrightness();
        }

        return _cachedDisplayPower;
    }

    private double GetWifiPowerEstimate(DateTimeOffset now)
    {
        try
        {
            var wifiInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                .ToArray();

            if (wifiInterfaces.Length == 0)
            {
                _wifiInitialized = false;
                _lastWifiPower = 0;
                return 0;
            }

            var activeInterfaces = wifiInterfaces
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .ToArray();

            if (activeInterfaces.Length == 0)
            {
                _wifiInitialized = false;
                _lastWifiPower = 0;
                return 0;
            }

            long totalBytes = 0;
            foreach (var nic in activeInterfaces)
            {
                try
                {
                    var stats = nic.GetIPv4Statistics();
                    totalBytes += stats.BytesReceived + stats.BytesSent;
                }
                catch
                {
                    // Skip interfaces that cannot provide statistics.
                }
            }

            if (!_wifiInitialized)
            {
                _wifiInitialized = true;
                _lastWifiBytes = totalBytes;
                _lastWifiSampleTime = now;
                _lastWifiPower = 0.6;
                return _lastWifiPower;
            }

            var elapsedSeconds = Math.Max(0.2, (now - _lastWifiSampleTime).TotalSeconds);
            var deltaBytes = Math.Max(0, totalBytes - _lastWifiBytes);

            _lastWifiBytes = totalBytes;
            _lastWifiSampleTime = now;

            var mbps = (deltaBytes * 8.0) / 1_000_000.0 / elapsedSeconds;
            // Typical Wi-Fi draw model: idle floor + activity slope with cap.
            _lastWifiPower = 0.5 + Math.Min(5.5, mbps * 0.12);
            return _lastWifiPower;
        }
        catch
        {
            return _lastWifiPower;
        }
    }

    private double GetBluetoothPowerEstimate(DateTimeOffset now)
    {
        if ((now - _lastBluetoothRefresh).TotalSeconds >= 10)
        {
            _lastBluetoothRefresh = now;
            _cachedBluetoothEnabled = IsBluetoothEnabled();
        }

        return _cachedBluetoothEnabled ? 0.2 : 0;
    }

    private static double EstimateDisplayPowerFromBrightness()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\wmi",
                "SELECT CurrentBrightness, Active FROM WmiMonitorBrightness");

            foreach (var obj in searcher.Get().OfType<ManagementObject>())
            {
                var active = obj["Active"] as bool?;
                if (active.HasValue && !active.Value)
                {
                    continue;
                }

                var brightnessObj = obj["CurrentBrightness"];
                if (brightnessObj is null)
                {
                    continue;
                }

                var brightness = Convert.ToDouble(brightnessObj);
                brightness = Math.Clamp(brightness, 0, 100);

                var refreshRate = GetPrimaryDisplayRefreshRate();
                var refreshBonus = Math.Max(0, refreshRate - 60) * 0.0065;

                // Rough panel model: base driver/panel floor + brightness + refresh scaling.
                return 0.8 + 0.03 * brightness + refreshBonus;
            }
        }
        catch
        {
            // Ignore WMI failures and keep display module hidden.
        }

        return 0;
    }

    private static int GetPrimaryDisplayRefreshRate()
    {
        var devMode = new DevMode();
        devMode.dmSize = (short)Marshal.SizeOf<DevMode>();

        try
        {
            if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref devMode))
            {
                return devMode.dmDisplayFrequency > 0 ? devMode.dmDisplayFrequency : 60;
            }
        }
        catch
        {
            // Fall back below.
        }

        return 60;
    }

    private static bool IsBluetoothEnabled()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Status FROM Win32_PnPEntity WHERE PNPClass = 'Bluetooth'");

            foreach (var obj in searcher.Get().OfType<ManagementObject>())
            {
                var status = Convert.ToString(obj["Status"]);
                if (string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Ignore WMI failures.
        }

        return false;
    }

    public void Dispose()
    {
        // No unmanaged resources held. Dispose exists for future extension.
    }

    private const int ENUM_CURRENT_SETTINGS = -1;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DevMode devMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }
}
