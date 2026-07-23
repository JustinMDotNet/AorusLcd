using System.Buffers.Binary;

namespace AorusLcd.Core;

/// <summary>
/// Builders for the 256-byte protocol frames. Field layouts are verified
/// byte-for-byte against live GCC captures - keep exactly, including the
/// min(255, delay) clamp and the 20480-byte auto-mode threshold.
/// </summary>
public static class ProtocolFrames
{
    public const int FrameSize = 256;

    /// <summary>256-byte command frame: opcode + CB 55 AC 38 + params, zero-padded.</summary>
    public static byte[] CmdFrame(byte opcode, ReadOnlySpan<byte> tail = default)
    {
        var buf = new byte[FrameSize];
        buf[0] = opcode;
        Opcode.Magic.CopyTo(buf, 1);
        tail.CopyTo(buf.AsSpan(5));
        return buf;
    }

    /// <summary>Upload BEGIN (flag=1) / END (flag=2) marker frame.</summary>
    public static byte[] F2Frame(byte flag)
    {
        var b = new byte[FrameSize];
        b[0] = Opcode.UploadMarker;
        Opcode.Magic.CopyTo(b, 1);
        b[5] = flag;
        return b;
    }

    /// <summary>
    /// 19-byte F1 upload header (padded to 256). <paramref name="mode"/> null =
    /// auto (2 when the payload is >= 20480 bytes, else 1).
    /// </summary>
    public static byte[] MakeF1Header(uint fbAddr, uint nchunks, ushort nframes,
        int delay, int usize, byte flag = 1, byte? mode = null)
    {
        var h = new byte[FrameSize];
        h[0] = Opcode.UploadHeader;
        Opcode.Magic.CopyTo(h, 1);
        BinaryPrimitives.WriteUInt32BigEndian(h.AsSpan(5), fbAddr);   // framebuffer target
        h[9] = flag;                                                 // 1 = static/text, 2 = gif
        BinaryPrimitives.WriteUInt32BigEndian(h.AsSpan(10), nchunks); // chunk count
        BinaryPrimitives.WriteUInt16BigEndian(h.AsSpan(14), nframes); // frame count
        h[16] = (byte)Math.Min(255, delay);                          // per-frame delay (ms, clamped)
        h[17] = mode ?? (byte)(usize >= 20480 ? 2 : 1);
        h[18] = 0;
        return h;
    }

    /// <summary>
    /// Split into 256-byte zero-padded chunks. Chunk count is usize/256 + 1
    /// (an exact 256-multiple still gets a full pad chunk).
    /// </summary>
    public static List<byte[]> ChunkPayload(ReadOnlySpan<byte> pdata)
    {
        int nchunks = pdata.Length / FrameSize + 1;
        var outList = new List<byte[]>(nchunks);
        for (int c = 0; c < nchunks; c++)
        {
            int start = c * FrameSize;
            int len = Math.Min(FrameSize, pdata.Length - start);
            var chunk = new byte[FrameSize];
            if (len > 0)
            {
                pdata.Slice(start, len).CopyTo(chunk);
            }
            outList.Add(chunk);
        }
        return outList;
    }

    /// <summary>
    /// Full upload sequence as role-tagged frames: BEGIN -> F1 header -> chunks
    /// -> END. Callers pace each frame by its <see cref="UploadFrameKind"/>.
    /// </summary>
    public static List<UploadFrame> BuildUpload(ReadOnlySpan<byte> pdata, uint fbAddr,
        byte flag = 1, ushort nframes = 0, int delay = 0, byte? mode = null)
        => BuildUpload(default, pdata, fbAddr, flag, nframes, delay, mode);

    /// <summary>
    /// Same as <see cref="BuildUpload(ReadOnlySpan{byte},uint,byte,ushort,int,byte?)"/>
    /// but chunks the logical concatenation of <paramref name="prefix"/> and
    /// <paramref name="pdata"/> directly, so callers that prepend a fixed
    /// descriptor (image/text) never allocate a full combined payload buffer.
    /// </summary>
    public static List<UploadFrame> BuildUpload(ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> pdata,
        uint fbAddr, byte flag = 1, ushort nframes = 0, int delay = 0, byte? mode = null)
    {
        int total = checked(prefix.Length + pdata.Length);
        uint nchunks = (uint)(total / FrameSize + 1);
        var frames = new List<UploadFrame>((int)nchunks + 3)
        {
            new(UploadFrameKind.Begin, F2Frame(1)),
            new(UploadFrameKind.Header, MakeF1Header(fbAddr, nchunks, nframes, delay, total, flag, mode)),
        };
        for (int c = 0; c < nchunks; c++)
        {
            int start = c * FrameSize;
            var chunk = new byte[FrameSize];
            int len = Math.Min(FrameSize, total - start);
            if (len > 0)
            {
                CopyLogical(prefix, pdata, start, len, chunk);
            }
            frames.Add(new UploadFrame(UploadFrameKind.Chunk, chunk));
        }
        frames.Add(new UploadFrame(UploadFrameKind.End, F2Frame(2)));
        return frames;
    }

    /// <summary>
    /// Copy <paramref name="count"/> bytes starting at logical offset
    /// <paramref name="start"/> from the concatenation <paramref name="a"/> +
    /// <paramref name="b"/> into <paramref name="dst"/>.
    /// </summary>
    private static void CopyLogical(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b,
        int start, int count, Span<byte> dst)
    {
        int written = 0;
        if (start < a.Length)
        {
            int n = Math.Min(count, a.Length - start);
            a.Slice(start, n).CopyTo(dst);
            written = n;
        }
        int bStart = Math.Max(0, start - a.Length);
        if (written < count && bStart < b.Length)
        {
            int n = Math.Min(count - written, b.Length - bStart);
            b.Slice(bStart, n).CopyTo(dst[written..]);
        }
    }
}
