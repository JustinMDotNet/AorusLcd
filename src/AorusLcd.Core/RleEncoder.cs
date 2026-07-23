using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace AorusLcd.Core;

/// <summary>Byte-exact GCC Compress_RLE: u16 LE header, bit15 run flag, low 15-bit count; runs need >=3 equal pixels, else literals.</summary>
public static class RleEncoder
{
    private const int MaxWindow = 0x7FFF;

    /// <summary>RLE-encode one little-endian RGB565 frame.</summary>
    public static byte[] EncodeFrame(ReadOnlySpan<byte> px)
    {
        // Worst case (incompressible): every window is one literal, adding a
        // 2-byte head per window on top of the pixel bytes.
        int n = px.Length / 2;
        int worstCase = px.Length + (((n / MaxWindow) + 1) * 2);
        var writer = new ArrayBufferWriter<byte>(Math.Max(worstCase, 1));
        EncodeInto(px, writer);
        return writer.WrittenSpan.ToArray();
    }

    /// <summary>RLE-encode one frame directly into <paramref name="writer"/>.</summary>
    public static void EncodeInto(ReadOnlySpan<byte> px, IBufferWriter<byte> writer)
    {
        var pixels = MemoryMarshal.Cast<byte, ushort>(px); // host is little-endian, matching the frame
        int n = pixels.Length;
        if (n < 3)
        {
            throw new ArgumentException("frame too small for Compress_RLE semantics", nameof(px));
        }

        int i = 0;
        while (i < n)
        {
            int wend = i + Math.Min(MaxWindow, n - i); // window [i, wend)
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
                    if (pixels[j] == pixels[j + 1] && pixels[j + 1] == pixels[j + 2])
                    {
                        int rs = j;
                        j += 2;
                        while (j < wend - 1 && pixels[j] == pixels[j + 1])
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
                WriteHead(writer, (ushort)diff);
                writer.Write(px.Slice(2 * i, 2 * diff));
            }
            if (same != 0)
            {
                WriteHead(writer, (ushort)(same | 0x8000));
                writer.Write(px.Slice(2 * (i + diff), 2));
            }
            i += diff + same;
        }
    }

    private static void WriteHead(IBufferWriter<byte> writer, ushort head)
    {
        var span = writer.GetSpan(2);
        BinaryPrimitives.WriteUInt16LittleEndian(span, head);
        writer.Advance(2);
    }
}
