namespace AorusLcd.Core;

/// <summary>
/// Converts 24-bit RGB pixels into the panel's little-endian RGB565 frame
/// format. Pure and hardware-free.
/// </summary>
public static class Rgb565Encoder
{
    /// <summary>
    /// RGB888 (length = pixelCount * 3, row-major) -> little-endian RGB565
    /// (length = pixelCount * 2).
    /// </summary>
    public static byte[] Encode(ReadOnlySpan<byte> rgb888)
    {
        int pixels = rgb888.Length / 3;
        var outBuf = new byte[pixels * 2];
        int j = 0;
        for (int i = 0; i < rgb888.Length; i += 3)
        {
            int v = ((rgb888[i] & 0xF8) << 8)
                    | ((rgb888[i + 1] & 0xFC) << 3)
                    | (rgb888[i + 2] >> 3);
            outBuf[j] = (byte)(v & 0xFF);
            outBuf[j + 1] = (byte)((v >> 8) & 0xFF);
            j += 2;
        }
        return outBuf;
    }
}
