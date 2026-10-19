using System.Runtime.InteropServices;
using SonyHeadphonesNet.Devices;
using SonyHeadphonesNet.Devices.Models;
using SonyHeadphonesNet.Transport;

namespace SonyHeadphonesNet;

/// <summary>
/// High-level manager and factory for Sony headphones.
/// Enumerates paired Bluetooth devices on Windows and instantiates appropriate device controllers.
/// </summary>
public static class SonyHeadphoneManager
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BLUETOOTH_DEVICE_SEARCH_PARAMS
    {
        public uint dwSize;
        public int fReturnAuthenticated;
        public int fReturnRemembered;
        public int fReturnUnknown;
        public int fReturnConnected;
        public int fIssueInquiry;
        public byte cTimeoutMultiplier;
        public nint hRadio;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BLUETOOTH_DEVICE_INFO
    {
        public uint dwSize;
        public ulong Address;
        public uint ulClassofDevice;
        public int fConnected;
        public int fRemembered;
        public int fAuthenticated;
        public SYSTEMTIME stLastSeen;
        public SYSTEMTIME stLastUsed;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
        public string szName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEMTIME
    {
        public ushort wYear, wMonth, wDayOfWeek, wDay, wHour, wMinute, wSecond, wMilliseconds;
    }

    [DllImport("Bthprops.cpl", SetLastError = true)]
    private static extern nint BluetoothFindFirstDevice(ref BLUETOOTH_DEVICE_SEARCH_PARAMS searchParams, ref BLUETOOTH_DEVICE_INFO deviceInfo);

    [DllImport("Bthprops.cpl", SetLastError = true)]
    private static extern bool BluetoothFindNextDevice(nint hFind, ref BLUETOOTH_DEVICE_INFO deviceInfo);

    [DllImport("Bthprops.cpl", SetLastError = true)]
    private static extern bool BluetoothFindDeviceClose(nint hFind);

    /// <summary>
    /// Enumerates remembered / paired Bluetooth devices on the local Windows machine.
    /// Filters by common Sony headphone keywords (WH-, WF-, MDR, LinkBuds, Sony) if requested.
    /// </summary>
    public static IReadOnlyList<DeviceInfo> GetPairedDevices(bool sonyOnly = true)
    {
        var devices = new List<DeviceInfo>();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return devices;

        try
        {
            var searchParams = new BLUETOOTH_DEVICE_SEARCH_PARAMS
            {
                dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_SEARCH_PARAMS>(),
                fReturnRemembered = 1,
                fReturnAuthenticated = 1,
                fReturnConnected = 1,
                fReturnUnknown = 0,
                fIssueInquiry = 0,
                hRadio = nint.Zero
            };

            var info = new BLUETOOTH_DEVICE_INFO
            {
                dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>()
            };

            nint hFind = BluetoothFindFirstDevice(ref searchParams, ref info);
            if (hFind != nint.Zero)
            {
                try
                {
                    do
                    {
                        string name = info.szName ?? "Unknown";
                        string mac = FormatMacAddress(info.Address);

                        bool match = !sonyOnly || IsLikelySonyHeadphone(name);
                        if (match)
                        {
                            devices.Add(new DeviceInfo(name, mac, null, info.fConnected != 0));
                        }
                    }
                    while (BluetoothFindNextDevice(hFind, ref info));
                }
                finally
                {
                    BluetoothFindDeviceClose(hFind);
                }
            }
        }
        catch
        {
            // Windows Bluetooth API query fallback (e.g. if Bluetooth service is off)
        }

        return devices;
    }

    /// <summary>
    /// Creates a device controller for WH-1000XM5.
    /// </summary>
    public static Wh1000Xm5Device CreateWh1000Xm5(bool useMock = false) =>
        useMock ? Wh1000Xm5Device.CreateMock() : Wh1000Xm5Device.Create();

    /// <summary>
    /// Creates a device controller dynamically based on model name or default.
    /// Easily extensible for future devices (WH-1000XM4, WF-1000XM5, etc.).
    /// </summary>
    public static ISonyHeadphoneDevice CreateDevice(string modelName, bool useMock = false)
    {
        // Currently supports WH-1000XM5. Future devices can be routed here.
        return CreateWh1000Xm5(useMock);
    }

    private static bool IsLikelySonyHeadphone(string name)
    {
        string upper = name.ToUpperInvariant();
        return upper.Contains("WH-") ||
               upper.Contains("WF-") ||
               upper.Contains("MDR") ||
               upper.Contains("LINKBUDS") ||
               upper.Contains("SONY") ||
               upper.Contains("1000X");
    }

    private static string FormatMacAddress(ulong address)
    {
        byte[] bytes = BitConverter.GetBytes(address);
        // Address is stored in reverse byte order
        return $"{bytes[5]:X2}:{bytes[4]:X2}:{bytes[3]:X2}:{bytes[2]:X2}:{bytes[1]:X2}:{bytes[0]:X2}";
    }
}
