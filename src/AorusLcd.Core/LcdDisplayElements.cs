namespace AorusLcd.Core;

/// <summary>
/// Sensor widgets the panel's built-in dashboard can overlay/rotate, as set by
/// the <c>E1</c> SetDisplay command. Bit values and order are from Gigabyte's
/// <c>ucVga.dll</c> (<c>GvLcdApi.SetDisplay</c> / the <c>Display</c> enum):
/// the command carries one flag byte per element in this exact order, followed
/// by a rotation-interval byte.
/// </summary>
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
