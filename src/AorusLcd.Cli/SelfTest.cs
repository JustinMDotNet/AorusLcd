using AorusLcd.Core;

namespace AorusLcd.Cli;

/// <summary>
/// Port of the Python <c>run_selftest()</c> encoder self-checks — pure, no
/// hardware. Verifies the .NET port is byte-identical to the reference.
/// </summary>
internal static class SelfTest
{
    /// <summary>Run all checks, printing PASS/FAIL. Returns true if any failed.</summary>
    public static bool Run()
    {
        var checks = new (string Name, Func<bool> Test)[]
        {
            ("cmd_frame", () => Prefix(ProtocolFrames.CmdFrame(0xE5, [0x04]), 6)
                .SequenceEqual(Bytes(0xE5, 0xCB, 0x55, 0xAC, 0x38, 0x04))),
            ("f2_begin", () => Prefix(ProtocolFrames.F2Frame(1), 6)
                .SequenceEqual(Bytes(0xF2, 0xCB, 0x55, 0xAC, 0x38, 0x01))),
            ("f1_fields", () =>
            {
                var h = ProtocolFrames.MakeF1Header(Panel.FramebufferStatic, 426, 0, 0,
                    Panel.Descriptor.Length + Panel.FrameBytes);
                return h[5] == 0x01 && h[6] == 0x30 && h[7] == 0x00 && h[8] == 0x00
                    && BigEndian32(h, 10) == 426 && h[16] == 0 && h[17] == 2;
            }),
            ("f1_delay_clamp", () =>
                ProtocolFrames.MakeF1Header(Panel.FramebufferGif, 1, 1, 999, 100, flag: 2, mode: 2)[16] == 255),
            ("chunk_pad", () =>
            {
                var chunks = ProtocolFrames.ChunkPayload(Repeat((byte)'x', 512));
                return chunks.Count == 3 && chunks[2].SequenceEqual(new byte[256]);
            }),
            ("rle_run", () => RleEncoder.EncodeFrame(Px(0x1234, 0x1234, 0x1234, 0x1234, 0x1234, 0x1234))
                .SequenceEqual(Concat(Bytes(0x06, 0x80), Px(0x1234)))),
            ("rle_literal", () => RleEncoder.EncodeFrame(Px(1, 2, 1, 2, 1, 2))
                .SequenceEqual(Concat(Bytes(0x06, 0x00), Px(1, 2, 1, 2, 1, 2)))),
            ("rle_short_tail", () => RleEncoder.EncodeFrame(Px(7, 7, 7))
                .SequenceEqual(Concat(Bytes(0x03, 0x00), Px(7, 7, 7)))),
            ("gif_table", () => GifPayload.FrameTable([10, 20]).SequenceEqual(Concat(
                Bytes(0x02, 0x00),
                Le32(31), Px(Panel.Width, Panel.Height, 3),
                Le32(51), Px(Panel.Width, Panel.Height, 3)))),
        };

        int failed = 0;
        foreach (var (name, test) in checks)
        {
            bool ok;
            try
            {
                ok = test();
            }
            catch (Exception e)
            {
                ok = false;
                Console.WriteLine($"FAIL  {name} ({e.GetType().Name}: {e.Message})");
                failed++;
                continue;
            }
            Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}");
            if (!ok)
            {
                failed++;
            }
        }
        Console.WriteLine($"\n{checks.Length - failed}/{checks.Length} passed");
        return failed != 0;
    }

    private static byte[] Bytes(params int[] values) => values.Select(v => (byte)v).ToArray();

    private static byte[] Px(params int[] words)
    {
        var buf = new byte[words.Length * 2];
        for (int i = 0; i < words.Length; i++)
        {
            buf[i * 2] = (byte)(words[i] & 0xFF);
            buf[(i * 2) + 1] = (byte)((words[i] >> 8) & 0xFF);
        }
        return buf;
    }

    private static byte[] Le32(uint v) => [(byte)v, (byte)(v >> 8), (byte)(v >> 16), (byte)(v >> 24)];

    private static byte[] Prefix(byte[] data, int n) => data[..n];

    private static byte[] Repeat(byte value, int count)
    {
        var b = new byte[count];
        Array.Fill(b, value);
        return b;
    }

    private static uint BigEndian32(byte[] b, int offset)
        => ((uint)b[offset] << 24) | ((uint)b[offset + 1] << 16) | ((uint)b[offset + 2] << 8) | b[offset + 3];

    private static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(p => p.Length)];
        int i = 0;
        foreach (var p in parts)
        {
            p.CopyTo(result, i);
            i += p.Length;
        }
        return result;
    }
}
