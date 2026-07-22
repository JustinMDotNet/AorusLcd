namespace AorusLcd.Core;

/// <summary>
/// A snapshot of GPU sensor values for the panel's live dashboard feed (the
/// <c>E3</c> packet). Field order/units match Gigabyte's AorusLcdService:
/// clocks in MHz, usage/fan as the raw displayed number, TGP in whole watts
/// (the panel prints the raw number as-is).
/// </summary>
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
