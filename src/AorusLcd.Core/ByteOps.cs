namespace AorusLcd.Core;

/// <summary>Small byte-buffer helpers shared across the protocol builders.</summary>
public static class ByteOps
{
    /// <summary>Concatenate two byte spans into a new array.</summary>
    public static byte[] Concat(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        var result = new byte[a.Length + b.Length];
        a.CopyTo(result);
        b.CopyTo(result.AsSpan(a.Length));
        return result;
    }
}
