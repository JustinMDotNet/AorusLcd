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
    public byte[] Probe() => ReadCommand(Opcode.Probe, [0x03]);

    public void OpenLcd(bool on)
        => WriteFrame(ProtocolFrames.CmdFrame(Opcode.OpenLcd, [(byte)(on ? 1 : 2)]));

    /// <summary>AA Save: persist the current LCD configuration to panel NVRAM.</summary>
    public void Save() => WriteFrame(ProtocolFrames.CmdFrame(Opcode.Save));

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

    /// <summary>
    /// E3 sensor feed: push live GPU values the panel's dashboard displays and
    /// rotates through. Layout from AorusLcdService: temp, GPU clock(2),
    /// usage, fan(2), RAM clock(2), RAM usage, FPS(2), TGP(2) — 16-bit fields
    /// big-endian. Must be sent continuously (≈1 Hz) for the widgets to update.
    /// </summary>
    public void SendSensorFeed(SensorSample s)
    {
        byte[] tail =
        [
            Clamp8(s.GpuTempC),
            High(s.GpuClockMhz), Low(s.GpuClockMhz),
            Clamp8(s.GpuUsagePercent),
            High(s.FanSpeed), Low(s.FanSpeed),
            High(s.RamClockMhz), Low(s.RamClockMhz),
            Clamp8(s.RamUsagePercent),
            High(s.Fps), Low(s.Fps),
            High(s.TgpWatts), Low(s.TgpWatts),
        ];
        WriteFrame(ProtocolFrames.CmdFrame(Opcode.SensorFeed, tail));
    }

    /// <summary>
    /// EA SetImageTpl: configure an overlay template (color + image/data
    /// positions) for image/gif/pet content. Layout from ucVga.dll's
    /// GvLcdApi.SetImageTpl: type, RGB, image X/Y and data X/Y as 16-bit
    /// big-endian pairs, and an enable flag.
    /// </summary>
    public void SetImageTemplate(LcdTemplate template)
    {
        byte[] tail =
        [
            (byte)template.Type,
            template.ColorR, template.ColorG, template.ColorB,
            High(template.ImagePosition.X), Low(template.ImagePosition.X),
            High(template.ImagePosition.Y), Low(template.ImagePosition.Y),
            High(template.DataPosition.X), Low(template.DataPosition.X),
            High(template.DataPosition.Y), Low(template.DataPosition.Y),
            (byte)(template.Enabled ? 1 : 0),
        ];
        WriteFrame(ProtocolFrames.CmdFrame(Opcode.SetImageTpl, tail));
    }

    // ---- read-back commands (write the query frame, then read bytes) -----------

    /// <summary>D6 GetFWVersion: panel firmware version as "major.minor".</summary>
    public string GetFirmwareVersion()
    {
        var r = ReadCommand(Opcode.GetFwVersion, [], 4);
        return $"{r[1] >> 4}.{r[1] & 0xF}";
    }

    /// <summary>DE GetMode: current display mode and on/off state.</summary>
    public (LcdMode Mode, bool On) GetMode()
    {
        var r = ReadCommand(Opcode.GetMode, [], 4);
        int mode = r[1] - 1;
        if (mode == 9)
        {
            mode = 7;
        }
        return ((LcdMode)mode, r[2] == 1);
    }

    /// <summary>DF GetDisplay: current dashboard element bitmask and interval.</summary>
    public (LcdDisplayElements Elements, int Interval) GetDisplay()
    {
        var r = ReadCommand(Opcode.GetDisplay, [], 4);
        var elements = LcdDisplayElements.None;
        for (int i = 0; i < 8; i++)
        {
            if ((r[1] & (1 << i)) != 0)
            {
                elements |= (LcdDisplayElements)(1u << i);
            }
        }
        return (elements, r[2]);
    }

    /// <summary>
    /// F4 GetLoop: current carousel modes and interval. Reads banks 1..5, each
    /// returning up to 3 (mode+1) entries plus the interval in byte 0.
    /// </summary>
    public (IReadOnlyList<int> Modes, int Interval) GetLoop()
    {
        var modes = new List<int>();
        int interval = 0;
        for (byte bank = 1; bank <= 5; bank++)
        {
            var r = ReadCommand(Opcode.GetLoop, [bank], 8);
            for (int j = 0; j < 3; j++)
            {
                if (r[1 + j] != 0)
                {
                    modes.Add(r[1 + j] - 1);
                }
            }
            interval = r[0];
        }
        return (modes, interval);
    }

    /// <summary>Read the full panel status in one call.</summary>
    public LcdStatus GetStatus()
    {
        var (mode, on) = GetMode();
        var (elements, interval) = GetDisplay();
        var (loopModes, loopInterval) = GetLoop();
        return new LcdStatus
        {
            FirmwareVersion = GetFirmwareVersion(),
            Mode = mode,
            IsOn = on,
            DisplayElements = elements,
            DisplayInterval = interval,
            CarouselModes = loopModes,
            CarouselInterval = loopInterval,
        };
    }

    private static byte High(int v) => (byte)((v >> 8) & 0xFF);

    private static byte Low(int v) => (byte)(v & 0xFF);

    private static byte Clamp8(int v) => (byte)Math.Clamp(v, 0, 255);

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
