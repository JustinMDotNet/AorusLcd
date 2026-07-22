namespace AorusLcd.Core;

/// <summary>
/// Byte-exact port of GCC's Compress_RLE, used for animated-GIF frame uploads.
/// Token grammar (head = u16 LE, bit15 = run flag, low 15 bits = pixel count):
///   run     : &lt;count|0x8000 : u16 LE&gt; &lt;pixel : 2B&gt;  (only for &gt;=3 equal px)
///   literal : &lt;count : u16 LE&gt; &lt;count pixels&gt;
/// The firmware decoder mirrors this encoder, so the quirks are intentional —
/// do not "optimize" them.
/// </summary>
public static class RleEncoder
{
    /// <summary>RLE-encode one little-endian RGB565 frame.</summary>
    public static byte[] EncodeFrame(ReadOnlySpan<byte> px)
    {
        int n = px.Length / 2; // pixel count
        if (n < 3)
        {
            throw new ArgumentException("frame too small for Compress_RLE semantics", nameof(px));
        }

        var outBuf = new List<byte>(px.Length);
        int i = 0;
        while (i < n)
        {
            int wend = i + Math.Min(0x7FFF, n - i); // window [i, wend)
            int wlen = wend - i;
            int diff, same;

            if (wlen < 4)
            {
                diff = wlen;
                same = 0;
            }
            else
            {
                int j = i;
                while (true)
                {
                    if (j + 2 == wend) // no repeat before window end: all literal
                    {
                        diff = wlen;
                        same = 0;
                        break;
                    }
                    if (Pixel(px, j) == Pixel(px, j + 1) && Pixel(px, j + 1) == Pixel(px, j + 2))
                    {
                        int rs = j;
                        j += 2;
                        while (j < wend - 1 && Pixel(px, j) == Pixel(px, j + 1))
                        {
                            j++;
                        }
                        diff = rs - i;
                        same = j + 1 - rs;
                        break;
                    }
                    j++;
                }
            }

            if (diff != 0)
            {
                WriteLe16(outBuf, (ushort)diff);
                outBuf.AddRange(px.Slice(2 * i, 2 * diff));
            }
            if (same != 0)
            {
                WriteLe16(outBuf, (ushort)(same | 0x8000));
                outBuf.AddRange(px.Slice(2 * (i + diff), 2));
            }
            i += diff + same;
        }
        return outBuf.ToArray();
    }

    private static ushort Pixel(ReadOnlySpan<byte> px, int index)
        => (ushort)(px[2 * index] | (px[(2 * index) + 1] << 8));

    private static void WriteLe16(List<byte> buf, ushort value)
    {
        buf.Add((byte)(value & 0xFF));
        buf.Add((byte)((value >> 8) & 0xFF));
    }
}
