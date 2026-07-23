namespace AorusLcd.Core.Rgb;

/// <summary>Gigabyte RGB Fusion 2 "Blackwell" GPU constants (from OpenRGB's GigabyteRGBFusion2BlackwellGPU) for RTX 50-series cards.</summary>
public static class RgbFusion2Blackwell
{
    public const int PacketSize = 64;

    // Packet register (byte 0): 0x12 = mode (throttled), 0x16 = direct/color (faster).
    public const byte RegMode = 0x12;
    public const byte RegColor = 0x16;

    public const byte RegSave = 0x13;  // persist config to the controller
    public const byte RegQuery = 0x10; // detection write; the card replies 01 01 01

    // Zone packets sent per update for the AORUS 5090/5080 MASTER "gaming" layout.
    public const int GamingLayoutZones = 6;

    // Colours in a mode-specific packet start here. Offset 11 is validated on
    // the RTX 5090 Master (OpenRGB's Master layouts also use 11, not 12).
    public const int ColorDataOffset = 11;

    /// <summary>Max RGB triples that fit in one packet after the colour-data offset.</summary>
    public const int MaxColors = (PacketSize - ColorDataOffset) / 3;

    public const byte BrightnessMin = 0x01;
    public const byte BrightnessMax = 0x0A;

    public const byte SpeedSlowest = 0x01;
    public const byte SpeedNormal = 0x03;
    public const byte SpeedFastest = 0x06;
}

/// <summary>Effect modes for the Blackwell GPU controller (OpenRGB mode values).</summary>
public enum RgbBlackwellMode : byte
{
    Direct = 0x00,
    Static = 0x01,
    Breathing = 0x02,
    Flashing = 0x03,
    DualFlashing = 0x04,
    ColorCycle = 0x05,
    Wave = 0x06,
    Gradient = 0x07,
    ColorShift = 0x08,
    Tricolor = 0x09,
    Dazzle = 0x0A,
}
