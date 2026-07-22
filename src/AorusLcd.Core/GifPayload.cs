namespace AorusLcd.Core;

/// <summary>
/// Builds the animated-GIF upload payload:
/// <c>[frameCount:2 LE] + table[frameCount] of [endOffset:4 LE][w:2][h:2][fmt:2]
/// + concatenated RLE frames</c>. The u32 is the INCLUSIVE end offset of that
/// frame's RLE blob within the payload. There is no checksum anywhere.
/// </summary>
public static class GifPayload
{
    private const int FormatRle = 3;

    /// <summary>[frameCount:2 LE] + one 10-byte entry per frame.</summary>
    public static byte[] FrameTable(IReadOnlyList<int> sizes, int w = Panel.Width,
        int h = Panel.Height, int fmt = FormatRle)
    {
        int n = sizes.Count;
        var outBuf = new List<byte>(2 + (10 * n));
        WriteLe16(outBuf, (ushort)n);
        int cum = 2 + (10 * n);
        foreach (int s in sizes)
        {
            cum += s;
            WriteLe32(outBuf, (uint)(cum - 1));
            WriteLe16(outBuf, (ushort)w);
            WriteLe16(outBuf, (ushort)h);
            WriteLe16(outBuf, (ushort)fmt);
        }
        return outBuf.ToArray();
    }

    /// <summary>
    /// RLE-compress each frame and assemble <c>frameCount + table + blobs</c>.
    /// The returned delay unit is milliseconds (raw, as GCC stores it).
    /// </summary>
    public static (byte[] Payload, int FrameCount, int DelayMs) Build(
        IReadOnlyList<byte[]> le565Frames, IReadOnlyList<int> frameDelaysMs, int? delayOverride)
    {
        var rle = new List<byte[]>(le565Frames.Count);
        foreach (var frame in le565Frames)
        {
            rle.Add(RleEncoder.EncodeFrame(frame));
        }

        int delay = delayOverride
            ?? Math.Min(255, Math.Max(1, (int)Math.Round(frameDelaysMs.Average())));

        var payload = new List<byte>();
        payload.AddRange(FrameTable(rle.ConvertAll(r => r.Length)));
        foreach (var blob in rle)
        {
            payload.AddRange(blob);
        }
        return (payload.ToArray(), le565Frames.Count, delay);
    }

    private static void WriteLe16(List<byte> buf, ushort value)
    {
        buf.Add((byte)(value & 0xFF));
        buf.Add((byte)((value >> 8) & 0xFF));
    }

    private static void WriteLe32(List<byte> buf, uint value)
    {
        buf.Add((byte)(value & 0xFF));
        buf.Add((byte)((value >> 8) & 0xFF));
        buf.Add((byte)((value >> 16) & 0xFF));
        buf.Add((byte)((value >> 24) & 0xFF));
    }
}
