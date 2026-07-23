namespace AorusLcd.Core;

/// <summary>E1 SetDisplay dashboard widgets, in ucVga.dll <c>Display</c> order; one flag byte per element, then rotation interval.</summary>
[Flags]
public enum LcdDisplayElements : uint
{
    None = 0,
    GpuTemp = 0x01,
    GpuClock = 0x02,
    GpuUsage = 0x04,
    FanSpeed = 0x08,
    RamClock = 0x10,
    RamUsage = 0x20,
    Fps = 0x40,
    Tgp = 0x80,
    All = GpuTemp | GpuClock | GpuUsage | FanSpeed | RamClock | RamUsage | Fps | Tgp,
}
