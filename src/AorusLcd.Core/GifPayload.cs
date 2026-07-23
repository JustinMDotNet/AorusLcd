using System.Buffers.Binary;

namespace AorusLcd.Core;

/// <summary>Builds animated-GIF payload: frame count, per-frame inclusive end offsets/dimensions/format, then RLE blobs; no checksum.</summary>
public static class GifPayload
{
    private const int FormatRle = 3;
    private const int TableEntrySize = 10;

    /// <summary>[frameCount:2 LE] + one 10-byte entry per frame.</summary>
    public static byte[] FrameTable(IReadOnlyList<int> sizes, int w = Panel.Width,
        int h = Panel.Height, int fmt = FormatRle)
    {
        var table = new byte[2 + (TableEntrySize * sizes.Count)];
        WriteTable(table, sizes, w, h, fmt);
        return table;
    }

    /// <summary>RLE-compress frames into <c>frameCount + table + blobs</c>; returned delays are raw GCC milliseconds.</summary>
    public static (byte[] Payload, int FrameCount, int DelayMs) Build(
        IReadOnlyList<byte[]> le565Frames, IReadOnlyList<int> frameDelaysMs, int? delayOverride)
    {
        int n = le565Frames.Count;
        var blobs = new byte[n][];
        var sizes = new int[n];
        int blobTotal = 0;
        for (int i = 0; i < n; i++)
        {
            blobs[i] = RleEncoder.EncodeFrame(le565Frames[i]);
            sizes[i] = blobs[i].Length;
            blobTotal += sizes[i];
        }

        int tableSize = 2 + (TableEntrySize * n);
        var payload = new byte[tableSize + blobTotal];
        WriteTable(payload, sizes, Panel.Width, Panel.Height, FormatRle);
        int offset = tableSize;
        foreach (var blob in blobs)
        {
            blob.CopyTo(payload.AsSpan(offset));
            offset += blob.Length;
        }

        int delay = delayOverride
            ?? Math.Min(255, Math.Max(1, (int)Math.Round(frameDelaysMs.Average())));
        return (payload, n, delay);
    }

    private static void WriteTable(Span<byte> dst, IReadOnlyList<int> sizes, int w, int h, int fmt)
    {
        int n = sizes.Count;
        BinaryPrimitives.WriteUInt16LittleEndian(dst, (ushort)n);
        int cum = 2 + (TableEntrySize * n);
        int pos = 2;
        for (int i = 0; i < n; i++)
        {
            cum += sizes[i];
            BinaryPrimitives.WriteUInt32LittleEndian(dst[pos..], (uint)(cum - 1)); // inclusive end offset
            BinaryPrimitives.WriteUInt16LittleEndian(dst[(pos + 4)..], (ushort)w);
            BinaryPrimitives.WriteUInt16LittleEndian(dst[(pos + 6)..], (ushort)h);
            BinaryPrimitives.WriteUInt16LittleEndian(dst[(pos + 8)..], (ushort)fmt);
            pos += TableEntrySize;
        }
    }
}
