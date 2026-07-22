namespace AorusLcd.Core;

/// <summary>
/// Builders for the 256-byte protocol frames. Field layouts are verified
/// byte-for-byte against live GCC captures — keep exactly, including the
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
        WriteBigEndian(h, 5, fbAddr);                 // framebuffer target
        h[9] = flag;                                  // 1 = static/text, 2 = gif
        WriteBigEndian(h, 10, nchunks);               // chunk count
        h[14] = (byte)((nframes >> 8) & 0xFF);        // frame count (2 bytes, big)
        h[15] = (byte)(nframes & 0xFF);
        h[16] = (byte)Math.Min(255, delay);           // per-frame delay (ms, clamped)
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

    /// <summary>Full upload frame list: BEGIN -> F1 header -> chunks -> END.</summary>
    public static List<byte[]> BuildUpload(ReadOnlySpan<byte> pdata, uint fbAddr,
        byte flag = 1, ushort nframes = 0, int delay = 0, byte? mode = null)
    {
        uint nchunks = (uint)(pdata.Length / FrameSize + 1);
        var frames = new List<byte[]>
        {
            F2Frame(1),
            MakeF1Header(fbAddr, nchunks, nframes, delay, pdata.Length, flag, mode),
        };
        frames.AddRange(ChunkPayload(pdata));
        frames.Add(F2Frame(2));
        return frames;
    }

    private static void WriteBigEndian(byte[] buf, int offset, uint value)
    {
        buf[offset] = (byte)((value >> 24) & 0xFF);
        buf[offset + 1] = (byte)((value >> 16) & 0xFF);
        buf[offset + 2] = (byte)((value >> 8) & 0xFF);
        buf[offset + 3] = (byte)(value & 0xFF);
    }
}
