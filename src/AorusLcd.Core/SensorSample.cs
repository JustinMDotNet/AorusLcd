namespace AorusLcd.Core;

/// <summary>GPU sensor snapshot for E3: MHz clocks, usage/fan raw display values, and TGP in whole watts.</summary>
public sealed record SensorSample
{
    public int GpuTempC { get; init; }
    public int GpuClockMhz { get; init; }
    public int GpuUsagePercent { get; init; }
    public int FanSpeed { get; init; }
    public int RamClockMhz { get; init; }
    public int RamUsagePercent { get; init; }
    public int Fps { get; init; }
    public int TgpWatts { get; init; }
}
