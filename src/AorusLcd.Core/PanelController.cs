namespace AorusLcd.Core;

/// <summary>
/// High-level panel operations over an <see cref="II2cBus"/>. Sequencing and
/// pacing match the working GCC captures: 0.5 s after BEGIN, 1.0 s after the F1
/// header, ~10 ms between chunks.
/// </summary>
public sealed class PanelController(II2cBus bus)
{
    private const int PaceBeginMs = 500;
    private const int PaceHeaderMs = 1000;
    public const int DefaultChunkDelayMs = 10;

    private readonly II2cBus _bus = bus;

    /// <summary>One raw I2C write of a 256-byte frame to the controller.</summary>
    public void WriteFrame(ReadOnlySpan<byte> data) => _bus.Write(data);

    /// <summary>Write a command frame, then read <paramref name="nbytes"/> back.</summary>
    public byte[] ReadCommand(byte opcode, ReadOnlySpan<byte> tail, int nbytes = 8)
    {
        WriteFrame(ProtocolFrames.CmdFrame(opcode, tail));
        return _bus.Read(nbytes);
    }

    /// <summary>
    /// Presence check: send the EB 03 status query (the same poll GCC uses) and
    /// return the 8-byte read-back. Throws if the controller does not answer.
    /// </summary>
    public byte[] Probe() => ReadCommand(Opcode.GetData, [0x03]);

    public void OpenLcd(bool on)
        => WriteFrame(ProtocolFrames.CmdFrame(Opcode.OpenLcd, [(byte)(on ? 1 : 2)]));

    /// <summary>E5 SetMode: byte5 = mode+1. Mode 7 maps to internal 9 (GCC quirk).</summary>
    public void SetMode(int mode)
        => WriteFrame(ProtocolFrames.CmdFrame(Opcode.SetMode, [(byte)((mode == 7 ? 9 : mode) + 1)]));

    /// <summary>
    /// E1 SetDisplay: enable/disable the panel's built-in sensor dashboard
    /// widgets and their rotation interval. Sends one flag byte per element
    /// (GpuTemp, GpuClock, GpuUsage, FanSpeed, RamClock, RamUsage, Fps, Tgp)
    /// followed by the interval byte — the exact layout from ucVga.dll's
    /// GvLcdApi.SetDisplay. Pass <see cref="LcdDisplayElements.None"/> to turn
    /// the whole dashboard off (clean image with no overlay).
    /// </summary>
    public void SetDisplay(LcdDisplayElements elements, int intervalSeconds)
    {
        var tail = new byte[9];
        for (int i = 0; i < 8; i++)
        {
            if (((uint)elements & (1u << i)) != 0)
            {
                tail[i] = 1;
            }
        }
        tail[8] = (byte)intervalSeconds;
        WriteFrame(ProtocolFrames.CmdFrame(Opcode.SetDisplay, tail));
    }

    /// <summary>F3 SetLoop: byte5 = arg, byte6.. = (mode+1) in play order. Modes 0..6.</summary>
    public void SetCarousel(IEnumerable<int> modes, int arg = 0)
    {
        var tail = new List<byte> { (byte)(arg & 0xFF) };
        foreach (int m in modes)
        {
            if (m is >= 0 and <= 6)
            {
                tail.Add((byte)((m & 0xFF) + 1));
            }
        }
        WriteFrame(ProtocolFrames.CmdFrame(Opcode.SetLoop, tail.ToArray()));
    }

    public void PowerOffMode() => WriteFrame(ProtocolFrames.CmdFrame(Opcode.PowerOff));

    public void TextEffect() => WriteFrame(ProtocolFrames.CmdFrame(Opcode.TextEffect));

    /// <summary>
    /// Write the upload frames with the pacing the panel firmware needs.
    /// </summary>
    public void SendUpload(IReadOnlyList<byte[]> frames, int chunkDelayMs = DefaultChunkDelayMs)
    {
        foreach (var fr in frames)
        {
            WriteFrame(fr);
            if (fr[0] == Opcode.UploadMarker && fr[5] == 0x01)
            {
                Thread.Sleep(PaceBeginMs);
            }
            else if (fr[0] == Opcode.UploadHeader)
            {
                Thread.Sleep(PaceHeaderMs);
            }
            else
            {
                Thread.Sleep(chunkDelayMs);
            }
        }
    }

    /// <summary>
    /// Stream an upload and select its display mode. ORDER MATTERS: a GIF
    /// streams to a live framebuffer, so SetMode goes BEFORE the upload;
    /// image/text store to numbered framebuffers, so SetMode goes AFTER.
    /// </summary>
    public void UploadContent(IReadOnlyList<byte[]> frames, int mode, bool isGif,
        bool setDisplayMode = true, int chunkDelayMs = DefaultChunkDelayMs)
    {
        if (isGif && setDisplayMode)
        {
            SetMode(mode);
            Thread.Sleep(200);
        }
        SendUpload(frames, chunkDelayMs);
        if (!isGif && setDisplayMode)
        {
            SetMode(mode);
        }
    }
}
