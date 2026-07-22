using AorusLcd.Core;

namespace AorusLcd.Tests;

/// <summary>Byte-exact RLE / GIF-table / RGB565 encoder tests.</summary>
public class EncoderTests
{
    private static byte[] Px(params int[] words)
    {
        var b = new byte[words.Length * 2];
        for (int i = 0; i < words.Length; i++)
        {
            b[i * 2] = (byte)(words[i] & 0xFF);
            b[(i * 2) + 1] = (byte)((words[i] >> 8) & 0xFF);
        }
        return b;
    }

    [Fact]
    public void Rle_RunOfEqualPixels()
        => Assert.Equal(Concat([0x06, 0x80], Px(0x1234)),
            RleEncoder.EncodeFrame(Px(0x1234, 0x1234, 0x1234, 0x1234, 0x1234, 0x1234)));

    [Fact]
    public void Rle_LiteralRun()
        => Assert.Equal(Concat([0x06, 0x00], Px(1, 2, 1, 2, 1, 2)),
            RleEncoder.EncodeFrame(Px(1, 2, 1, 2, 1, 2)));

    [Fact]
    public void Rle_ShortTailIsLiteral()
        => Assert.Equal(Concat([0x03, 0x00], Px(7, 7, 7)), RleEncoder.EncodeFrame(Px(7, 7, 7)));

    [Fact]
    public void GifFrameTable_InclusiveEndOffsets()
    {
        var expected = Concat([0x02, 0x00],
            [31, 0, 0, 0], Px(Panel.Width, Panel.Height, 3),
            [51, 0, 0, 0], Px(Panel.Width, Panel.Height, 3));
        Assert.Equal(expected, GifPayload.FrameTable([10, 20]));
    }

    [Fact]
    public void Rgb565_EncodesLittleEndian()
    {
        // Pure red 0xFF0000 -> RGB565 0xF800 -> LE bytes 00 F8
        Assert.Equal(new byte[] { 0x00, 0xF8 }, Rgb565Encoder.Encode([0xFF, 0x00, 0x00]));
        // Pure green 0x00FF00 -> 0x07E0 -> E0 07
        Assert.Equal(new byte[] { 0xE0, 0x07 }, Rgb565Encoder.Encode([0x00, 0xFF, 0x00]));
        // Pure blue 0x0000FF -> 0x001F -> 1F 00
        Assert.Equal(new byte[] { 0x1F, 0x00 }, Rgb565Encoder.Encode([0x00, 0x00, 0xFF]));
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var r = new byte[parts.Sum(p => p.Length)];
        int i = 0;
        foreach (var p in parts)
        {
            p.CopyTo(r, i);
            i += p.Length;
        }
        return r;
    }
}
