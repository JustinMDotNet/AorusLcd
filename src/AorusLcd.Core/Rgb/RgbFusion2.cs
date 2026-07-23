namespace AorusLcd.Core.Rgb;

/// <summary>Gigabyte RGB Fusion 2 GPU constants from OpenRGB; the Aorus Master exposes 5 zones.</summary>
public static class RgbFusion2
{
    public const int ZoneCount = 5;

    // Registers (first byte of each 8-byte packet).
    public const byte RegColor = 0x40;
    public const byte RegMode = 0x88;
    public const byte RegColorLeftMid = 0xB0;
    public const byte RegColorRight = 0xB1;
    public const byte RegSave = 0xAA;
    public const byte RegQuery = 0xAB;

    public const int BrightnessMin = 0x00;
    public const int BrightnessMax = 0x63;

    public const int SpeedSlowest = 0x00;
    public const int SpeedNormal = 0x02;
    public const int SpeedFastest = 0x05;
}

/// <summary>Effect modes supported by the RGB Fusion 2 GPU controller.</summary>
public enum RgbMode : byte
{
    Static = 0x01,
    Breathing = 0x02,
    ColorCycle = 0x03,
    Flashing = 0x04,
    Gradient = 0x05,
    ColorShift = 0x06,
    Wave = 0x07,
    DualFlashing = 0x08,
    Tricolor = 0x0B,
}
