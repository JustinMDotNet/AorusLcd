namespace AorusLcd.Core;

/// <summary>
/// Minimal I2C block transport to the LCD controller (address 0x61). Mirrors
/// the two operations the protocol needs: a raw block write, and a raw block
/// read of a fixed length. No register/command byte is used.
/// </summary>
public interface II2cBus : IDisposable
{
    /// <summary>Write <paramref name="data"/> as a single I2C block to the panel.</summary>
    void Write(ReadOnlySpan<byte> data);

    /// <summary>Read <paramref name="count"/> bytes back from the panel.</summary>
    byte[] Read(int count);
}
