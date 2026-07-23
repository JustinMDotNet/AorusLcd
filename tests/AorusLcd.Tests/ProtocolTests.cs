using AorusLcd.Core;

namespace AorusLcd.Tests;

/// <summary>
/// Byte-parity tests ported from the reference <c>run_selftest()</c>, plus
/// structural checks on the protocol frame builders.
/// </summary>
public class ProtocolTests
{
    [Fact]
    public void CmdFrame_HasOpcodeMagicAndTail()
    {
        var f = ProtocolFrames.CmdFrame(0xE5, [0x04]);
        Assert.Equal(256, f.Length);
        Assert.Equal(new byte[] { 0xE5, 0xCB, 0x55, 0xAC, 0x38, 0x04 }, f[..6]);
    }

    [Fact]
    public void F2Frame_BeginMarker()
        => Assert.Equal(new byte[] { 0xF2, 0xCB, 0x55, 0xAC, 0x38, 0x01 }, ProtocolFrames.F2Frame(1)[..6]);

    [Fact]
    public void F1Header_StaticImageFields()
    {
        var h = ProtocolFrames.MakeF1Header(Panel.FramebufferStatic, 426, 0, 0,
            Panel.Descriptor.Length + Panel.FrameBytes);
        Assert.Equal(new byte[] { 0x01, 0x30, 0x00, 0x00 }, h[5..9]);
        Assert.Equal(426u, ((uint)h[10] << 24) | ((uint)h[11] << 16) | ((uint)h[12] << 8) | h[13]);
        Assert.Equal(0, h[16]);
        Assert.Equal(2, h[17]); // usize >= 20480 -> auto mode 2
    }

    [Fact]
    public void F1Header_ClampsDelayTo255()
        => Assert.Equal(255, ProtocolFrames.MakeF1Header(Panel.FramebufferGif, 1, 1, 999, 100, 2, 2)[16]);

    [Fact]
    public void ChunkPayload_PadsExactMultipleWithFullChunk()
    {
        var chunks = ProtocolFrames.ChunkPayload(new byte[512]);
        Assert.Equal(3, chunks.Count);
        Assert.All(chunks, c => Assert.Equal(256, c.Length));
    }

    [Fact]
    public void BuildUpload_TagsFramesByRole()
    {
        var frames = ProtocolFrames.BuildUpload(new byte[256], Panel.FramebufferStatic);
        Assert.Equal(UploadFrameKind.Begin, frames[0].Kind);
        Assert.Equal(Opcode.UploadMarker, frames[0].Data[0]);
        Assert.Equal(1, frames[0].Data[5]);
        Assert.Equal(UploadFrameKind.Header, frames[1].Kind);
        Assert.Equal(Opcode.UploadHeader, frames[1].Data[0]);
        Assert.Equal(UploadFrameKind.Chunk, frames[2].Kind);
        Assert.Equal(UploadFrameKind.End, frames[^1].Kind);
        Assert.Equal(Opcode.UploadMarker, frames[^1].Data[0]);
        Assert.Equal(2, frames[^1].Data[5]);
    }

    [Fact]
    public void BuildUpload_PayloadAliasingControlOpcodesStaysChunk()
    {
        // Payload full of 0xF1/0xF2 (the header/marker opcodes) must never be
        // misclassified - every middle frame is a Chunk regardless of content.
        var payload = new byte[512];
        Array.Fill(payload, (byte)0xF1);
        var frames = ProtocolFrames.BuildUpload(payload, Panel.FramebufferStatic);
        for (int i = 2; i < frames.Count - 1; i++)
        {
            Assert.Equal(UploadFrameKind.Chunk, frames[i].Kind);
        }
    }

    [Fact]
    public void SetDisplay_EncodesElementFlagsAndInterval()
    {
        var bus = new CapturingBus();
        new PanelController(bus).SetDisplay(LcdDisplayElements.GpuTemp | LcdDisplayElements.Tgp, 4);
        var frame = bus.LastWrite!;
        // E1 CB 55 AC 38, then 8 element flags (GTEMP..TGP), then interval.
        Assert.Equal(Opcode.SetDisplay, frame[0]);
        Assert.Equal(new byte[] { 0xCB, 0x55, 0xAC, 0x38 }, frame[1..5]);
        Assert.Equal(1, frame[5]);  // GpuTemp (bit 0)
        Assert.Equal(0, frame[6]);  // GpuClock
        Assert.Equal(1, frame[12]); // Tgp (bit 7)
        Assert.Equal(4, frame[13]); // interval
    }

    [Fact]
    public void SetDisplay_NoneTurnsEverythingOff()
    {
        var bus = new CapturingBus();
        new PanelController(bus).SetDisplay(LcdDisplayElements.None, 0);
        var frame = bus.LastWrite!;
        Assert.Equal(Opcode.SetDisplay, frame[0]);
        Assert.All(frame[5..14], b => Assert.Equal(0, b));
    }

    [Fact]
    public void Save_SendsAaOpcode()
    {
        var bus = new CapturingBus();
        new PanelController(bus).Save();
        Assert.Equal(Opcode.Save, bus.LastWrite![0]);
        Assert.Equal(new byte[] { 0xCB, 0x55, 0xAC, 0x38 }, bus.LastWrite![1..5]);
    }

    [Fact]
    public void SendSensorFeed_EncodesE3PacketBigEndian()
    {
        var bus = new CapturingBus();
        new PanelController(bus).SendSensorFeed(new SensorSample
        {
            GpuTempC = 48,
            GpuClockMhz = 0x0322,   // 802
            GpuUsagePercent = 5,
            FanSpeed = 0x0102,
            RamClockMhz = 0x0405,
            RamUsagePercent = 9,
            Fps = 0x0060,
            TgpWatts = 0x0037,      // 55
        });
        var f = bus.LastWrite!;
        Assert.Equal(Opcode.SensorFeed, f[0]);
        Assert.Equal(new byte[] { 0xCB, 0x55, 0xAC, 0x38 }, f[1..5]);
        Assert.Equal(48, f[5]);
        Assert.Equal(new byte[] { 0x03, 0x22 }, f[6..8]);   // GPU clock big-endian
        Assert.Equal(5, f[8]);
        Assert.Equal(new byte[] { 0x01, 0x02 }, f[9..11]);  // fan
        Assert.Equal(new byte[] { 0x04, 0x05 }, f[11..13]); // RAM clock
        Assert.Equal(9, f[13]);
        Assert.Equal(new byte[] { 0x00, 0x60 }, f[14..16]); // FPS
        Assert.Equal(new byte[] { 0x00, 0x37 }, f[16..18]); // TGP
    }

    [Fact]
    public void SetImageTemplate_EncodesColorAndPositions()
    {
        var bus = new CapturingBus();
        new PanelController(bus).SetImageTemplate(new LcdTemplate
        {
            Type = LcdTemplateType.Image,
            ColorR = 0x11, ColorG = 0x22, ColorB = 0x33,
            ImagePosition = (0x0102, 0x0304),
            DataPosition = (0x0506, 0x0708),
            Enabled = true,
        });
        var f = bus.LastWrite!;
        Assert.Equal(Opcode.SetImageTpl, f[0]);
        Assert.Equal((byte)LcdTemplateType.Image, f[5]);
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33 }, f[6..9]);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, f[9..13]);  // image X,Y big-endian
        Assert.Equal(new byte[] { 0x05, 0x06, 0x07, 0x08 }, f[13..17]); // data X,Y big-endian
        Assert.Equal(1, f[17]);
    }

    [Fact]
    public void GetMode_ParsesModeAndOnState()
    {
        // array[1] = mode+1 (4 -> Text), array[2] = 1 (on)
        var bus = new ScriptedBus([0x00, 0x05, 0x01, 0x00]);
        var (mode, on) = new PanelController(bus).GetMode();
        Assert.Equal(LcdMode.Text, mode);
        Assert.True(on);
    }

    [Fact]
    public void GetDisplay_ParsesElementBitmask()
    {
        // array[1] = 0x81 (GpuTemp|Tgp), array[2] = 4 (interval)
        var bus = new ScriptedBus([0x00, 0x81, 0x04, 0x00]);
        var (elements, interval) = new PanelController(bus).GetDisplay();
        Assert.Equal(LcdDisplayElements.GpuTemp | LcdDisplayElements.Tgp, elements);
        Assert.Equal(4, interval);
    }

    [Fact]
    public void GetFirmwareVersion_ParsesNibbles()
    {
        // array[1] = 0x13 -> "1.3"
        var bus = new ScriptedBus([0x00, 0x13, 0x00, 0x00]);
        Assert.Equal("1.3", new PanelController(bus).GetFirmwareVersion());
    }

    private sealed class CapturingBus : II2cBus
    {
        public byte[]? LastWrite { get; private set; }

        public void Write(ReadOnlySpan<byte> data) => LastWrite = data.ToArray();

        public byte[] Read(int count) => new byte[count];

        public void Dispose()
        {
        }
    }

    private sealed class ScriptedBus(byte[] readResponse) : II2cBus
    {
        public void Write(ReadOnlySpan<byte> data)
        {
        }

        public byte[] Read(int count)
        {
            var r = new byte[count];
            Array.Copy(readResponse, r, Math.Min(count, readResponse.Length));
            return r;
        }

        public void Dispose()
        {
        }
    }
}
