namespace AorusLcd.Core.Rgb;

/// <summary>An 8-bit-per-channel RGB color.</summary>
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public static RgbColor Black => new(0, 0, 0);

    /// <summary>Parse an <c>RRGGBB</c> hex string (leading '#' optional).</summary>
    public static RgbColor Parse(string hex)
    {
        hex = hex.TrimStart('#');
        return new RgbColor(
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16));
    }
}
