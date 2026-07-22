namespace AorusLcd.Core.Sensors;

/// <summary>
/// Reads GPU sensors through NVML for the panel's live feed. Values map to the
/// <c>E3</c> packet: clocks in MHz, GPU/RAM usage as a percentage, TGP in whole
/// watts (the panel prints the raw number, so watts read correctly; GCC sent
/// deci-watts, which is why its readout looked 10x too high). FPS is not
/// available from NVML and is reported as 0.
///
/// When a PCI bus id is supplied, the matching NVML device is selected so a
/// multi-GPU system feeds the correct card's sensors; otherwise device 0 is used.
/// </summary>
public sealed class NvmlSensorSource : ISensorSource
{
    private const uint TemperatureGpu = 0;
    private const uint ClockGraphics = 0;
    private const uint ClockMemory = 2;

    private readonly IntPtr _device;
    private bool _initialized;

    public NvmlSensorSource(uint? pciBusId = null)
    {
        if (Nvml.Init() != 0)
        {
            throw new InvalidOperationException(
                "NVML initialization failed. Ensure the NVIDIA driver (nvml) is installed.");
        }
        _initialized = true;
        try
        {
            _device = ResolveDevice(pciBusId);
        }
        catch
        {
            Dispose(); // release the NVML init reference if device resolution fails
            throw;
        }
    }

    public SensorSample Read()
    {
        var device = _device;

        int temp = Nvml.GetTemperature(device, TemperatureGpu, out uint t) == 0 ? (int)t : 0;
        int gpuClock = Nvml.GetClockInfo(device, ClockGraphics, out uint gc) == 0 ? (int)gc : 0;
        int ramClock = Nvml.GetClockInfo(device, ClockMemory, out uint mc) == 0 ? (int)mc : 0;
        var util = Nvml.GetUtilizationRates(device, out var u) == 0 ? u : default;
        int fan = Nvml.GetFanSpeed(device, out uint f) == 0 ? (int)f : 0;
        int powerMw = Nvml.GetPowerUsage(device, out uint mw) == 0 ? (int)mw : 0;

        return new SensorSample
        {
            GpuTempC = temp,
            GpuClockMhz = gpuClock,
            GpuUsagePercent = (int)util.Gpu,
            FanSpeed = fan,
            RamClockMhz = ramClock,
            RamUsagePercent = (int)util.Memory,
            Fps = 0,
            TgpWatts = (powerMw + 999) / 1000, // mW -> W, rounded up; panel prints the raw number
        };
    }

    /// <summary>
    /// Pick the NVML device whose PCI bus matches <paramref name="pciBusId"/>;
    /// fall back to device 0 if unknown or unmatched.
    /// </summary>
    private static IntPtr ResolveDevice(uint? pciBusId)
    {
        if (pciBusId is uint wanted && Nvml.GetCount(out uint count) == 0)
        {
            for (uint i = 0; i < count; i++)
            {
                if (Nvml.GetHandleByIndex(i, out var device) != 0)
                {
                    continue;
                }
                var pci = new Nvml.PciInfo { BusIdLegacy = new byte[16], BusId = new byte[32] };
                if (Nvml.GetPciInfo(device, ref pci) == 0 && pci.Bus == wanted)
                {
                    return device;
                }
            }
        }

        if (Nvml.GetHandleByIndex(0, out var first) != 0)
        {
            throw new InvalidOperationException("NVML could not open any GPU (index 0).");
        }
        return first;
    }

    public void Dispose()
    {
        if (_initialized)
        {
            Nvml.Shutdown();
            _initialized = false;
        }
    }
}
