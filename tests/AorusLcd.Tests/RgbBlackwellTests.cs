using AorusLcd.Core;
using AorusLcd.Core.Nvapi;
using AorusLcd.Core.Rgb;

namespace AorusLcd.Tests;

/// <summary>Byte-layout tests for the Blackwell (RTX 50-series) GPU RGB packets, against OpenRGB's documented format.</summary>
public class RgbBlackwellTests
{
    private static RgbFusion2BlackwellController Controller(MultiCapturingBus bus)
        => new(bus) { WriteDelayMs = 0 };

    [Fact]
    public void SetStatic_SendsSixZonePacketsThenSave()
    {
        var bus = new MultiCapturingBus();
        Controller(bus).SetStatic(new RgbColor(0x11, 0x22, 0x33), 0x0A);

        Assert.Equal(RgbFusion2Blackwell.GamingLayoutZones + 1, bus.Writes.Count);
        Assert.All(bus.Writes, w => Assert.Equal(RgbFusion2Blackwell.PacketSize, w.Length));
        Assert.Equal(RgbFusion2Blackwell.RegSave, bus.Writes[^1][0]); // last packet persists
    }

    [Fact]
    public void SetStatic_EncodesModePacketLayout()
    {
        var bus = new MultiCapturingBus();
        Controller(bus).SetStatic(new RgbColor(0x11, 0x22, 0x33), 0x0A);

        var p = bus.Writes[0]; // first zone packet
        Assert.Equal(RgbFusion2Blackwell.RegMode, p[0]);        // 0x12 mode register
        Assert.Equal(0x01, p[1]);
        Assert.Equal((byte)RgbBlackwellMode.Static, p[2]);
        Assert.Equal(RgbFusion2Blackwell.SpeedNormal, p[3]);
        Assert.Equal(0x0A, p[4]);                                // brightness
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33 }, p[5..8]);  // primary R,G,B
        Assert.Equal(0x00, p[8]);
        Assert.Equal(0x00, p[9]);                                // zone 0
        Assert.Equal(0x00, p[10]);                               // numColors 0 for single-colour mode
    }

    [Fact]
    public void ZoneIndex_IncrementsAcrossPackets()
    {
        var bus = new MultiCapturingBus();
        Controller(bus).SetStatic(RgbColor.Black, 0x0A);

        for (byte zone = 0; zone < RgbFusion2Blackwell.GamingLayoutZones; zone++)
        {
            Assert.Equal(zone, bus.Writes[zone][9]);
        }
    }

    [Fact]
    public void Direct_UsesColorRegister()
    {
        var bus = new MultiCapturingBus();
        Controller(bus).SetEffect(RgbBlackwellMode.Direct, [new RgbColor(1, 2, 3)],
            RgbFusion2Blackwell.SpeedNormal, 0x0A);

        Assert.Equal(RgbFusion2Blackwell.RegColor, bus.Writes[0][0]); // 0x16 for direct
    }

    [Fact]
    public void Breathing_ForcesMaxBrightness()
    {
        var bus = new MultiCapturingBus();
        Controller(bus).SetEffect(RgbBlackwellMode.Breathing, [new RgbColor(1, 2, 3)],
            RgbFusion2Blackwell.SpeedNormal, 0x01);

        Assert.Equal(RgbFusion2Blackwell.BrightnessMax, bus.Writes[0][4]);
    }

    [Fact]
    public void ColorShift_AppendsModeSpecificColors()
    {
        var bus = new MultiCapturingBus();
        RgbColor[] colors = [new(0xAA, 0xBB, 0xCC), new(0x11, 0x22, 0x33)];
        Controller(bus).SetEffect(RgbBlackwellMode.ColorShift, colors,
            RgbFusion2Blackwell.SpeedNormal, 0x0A);

        var p = bus.Writes[0];
        Assert.Equal(2, p[10]); // numColors
        // Mode-specific colours start at offset 11 (validated on the RTX 5090 Master).
        Assert.Equal(11, RgbFusion2Blackwell.ColorDataOffset);
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC }, p[11..14]);
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33 }, p[14..17]);
    }

    [Fact]
    public void Save_EncodesSavePacket()
    {
        var bus = new MultiCapturingBus();
        Controller(bus).SaveConfig();

        Assert.Single(bus.Writes);
        Assert.Equal(RgbFusion2Blackwell.RegSave, bus.Writes[0][0]);
        Assert.Equal(0x01, bus.Writes[0][1]);
    }

    [Fact]
    public void ColorShift_CapsNumColorsToPacketCapacity()
    {
        var bus = new MultiCapturingBus();
        var many = new RgbColor[RgbFusion2Blackwell.MaxColors + 5];
        Array.Fill(many, new RgbColor(1, 2, 3));
        Controller(bus).SetEffect(RgbBlackwellMode.ColorShift, many,
            RgbFusion2Blackwell.SpeedNormal, 0x0A);

        Assert.Equal(RgbFusion2Blackwell.MaxColors, bus.Writes[0][10]); // never advertises more than fits
    }

    [Theory]
    [InlineData("NVIDIA GeForce RTX 5090", RgbControllerKind.Blackwell)]
    [InlineData("NVIDIA GeForce RTX 5080", RgbControllerKind.Blackwell)]
    [InlineData("NVIDIA GeForce RTX 5060 Ti", RgbControllerKind.Blackwell)]
    [InlineData("NVIDIA GeForce RTX 5050", RgbControllerKind.Blackwell)]
    [InlineData("NVIDIA RTX 5000 Ada Generation", RgbControllerKind.Legacy)]
    [InlineData("NVIDIA RTX A5000", RgbControllerKind.Legacy)]
    [InlineData("NVIDIA GeForce RTX 4090", RgbControllerKind.Legacy)]
    [InlineData("NVIDIA GeForce RTX 3080", RgbControllerKind.Legacy)]
    [InlineData(null, RgbControllerKind.Legacy)]
    public void ClassifyByName_DistinguishesBlackwellFromWorkstationAndOlder(string? name, RgbControllerKind expected)
        => Assert.Equal(expected, RgbLocator.ClassifyByName(name));

    private sealed class MultiCapturingBus : II2cBus
    {
        public List<byte[]> Writes { get; } = [];

        public void Write(ReadOnlySpan<byte> data) => Writes.Add(data.ToArray());

        public byte[] Read(int count) => new byte[count];

        public void Dispose()
        {
        }
    }
}
