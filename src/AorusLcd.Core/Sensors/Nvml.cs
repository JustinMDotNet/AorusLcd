using System.Runtime.InteropServices;

namespace AorusLcd.Core.Sensors;

/// <summary>
/// Minimal P/Invoke layer over NVIDIA's Management Library (NVML), which ships
/// with the driver (<c>nvml.dll</c> on Windows, <c>libnvidia-ml.so.1</c> on
/// Linux). NVML is documented and stable, so it is the preferred source for the
/// panel's live sensor feed. The native library name is resolved per-OS.
/// </summary>
internal static class Nvml
{
    private const string Lib = "nvml";

    static Nvml()
    {
        NativeLibrary.SetDllImportResolver(typeof(Nvml).Assembly, (name, assembly, path) =>
        {
            if (name != Lib)
            {
                return IntPtr.Zero;
            }
            foreach (var candidate in Candidates())
            {
                if (NativeLibrary.TryLoad(candidate, out var handle))
                {
                    return handle;
                }
            }
            return IntPtr.Zero;
        });
    }

    private static IEnumerable<string> Candidates()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return "nvml.dll";
        }
        else
        {
            yield return "libnvidia-ml.so.1";
            yield return "libnvidia-ml.so";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Utilization
    {
        public uint Gpu;
        public uint Memory;
    }

    // nvmlPciInfo_t: busIdLegacy[16], domain, bus, device, pciDeviceId,
    // pciSubSystemId, busId[32]. Only domain/bus/device are needed for matching.
    [StructLayout(LayoutKind.Sequential)]
    public struct PciInfo
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] BusIdLegacy;
        public uint Domain;
        public uint Bus;
        public uint Device;
        public uint PciDeviceId;
        public uint PciSubSystemId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] BusId;
    }

    // 0 = NVML_SUCCESS. sensorType 0 = GPU; clockType 0 = graphics, 2 = mem.
    [DllImport(Lib, EntryPoint = "nvmlInit_v2")]
    public static extern int Init();

    [DllImport(Lib, EntryPoint = "nvmlShutdown")]
    public static extern int Shutdown();

    [DllImport(Lib, EntryPoint = "nvmlDeviceGetCount_v2")]
    public static extern int GetCount(out uint count);

    [DllImport(Lib, EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
    public static extern int GetHandleByIndex(uint index, out IntPtr device);

    [DllImport(Lib, EntryPoint = "nvmlDeviceGetPciInfo_v3")]
    public static extern int GetPciInfo(IntPtr device, ref PciInfo pci);

    [DllImport(Lib, EntryPoint = "nvmlDeviceGetTemperature")]
    public static extern int GetTemperature(IntPtr device, uint sensorType, out uint tempC);

    [DllImport(Lib, EntryPoint = "nvmlDeviceGetClockInfo")]
    public static extern int GetClockInfo(IntPtr device, uint clockType, out uint clockMhz);

    [DllImport(Lib, EntryPoint = "nvmlDeviceGetUtilizationRates")]
    public static extern int GetUtilizationRates(IntPtr device, out Utilization util);

    [DllImport(Lib, EntryPoint = "nvmlDeviceGetFanSpeed")]
    public static extern int GetFanSpeed(IntPtr device, out uint speedPercent);

    [DllImport(Lib, EntryPoint = "nvmlDeviceGetPowerUsage")]
    public static extern int GetPowerUsage(IntPtr device, out uint milliwatts);
}
