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
    public void BuildUpload_BeginHeaderChunksEnd()
    {
        var frames = ProtocolFrames.BuildUpload(new byte[256], Panel.FramebufferStatic);
        Assert.Equal(Opcode.UploadMarker, frames[0][0]);
        Assert.Equal(1, frames[0][5]);
        Assert.Equal(Opcode.UploadHeader, frames[1][0]);
        Assert.Equal(Opcode.UploadMarker, frames[^1][0]);
        Assert.Equal(2, frames[^1][5]);
    }
}
