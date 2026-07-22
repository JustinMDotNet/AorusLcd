namespace AorusLcd.Core.Sensors;

/// <summary>
/// Reads GPU sensors through NVML for the panel's live feed. Values map to the
/// <c>E3</c> packet: clocks in MHz, GPU/RAM usage as a percentage, TGP in whole
/// watts (the panel prints the raw number, so watts read correctly; GCC sent
/// deci-watts, which is why its readout looked 10x too high). FPS is not
/// available from NVML and is reported as 0.
/// </summary>
public sealed class NvmlSensorSource : ISensorSource
{
    private const uint TemperatureGpu = 0;
    private const uint ClockGraphics = 0;
    private const uint ClockMemory = 2;

    private bool _initialized;

    public NvmlSensorSource()
    {
        if (Nvml.Init() != 0)
        {
            throw new InvalidOperationException(
                "NVML initialization failed. Ensure the NVIDIA driver (nvml) is installed.");
        }
        _initialized = true;
    }

    public SensorSample Read(int gpuIndex = 0)
    {
        if (Nvml.GetHandleByIndex((uint)gpuIndex, out var device) != 0)
        {
            throw new InvalidOperationException($"NVML could not open GPU index {gpuIndex}.");
        }

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

    public void Dispose()
    {
        if (_initialized)
        {
            Nvml.Shutdown();
            _initialized = false;
        }
    }
}
