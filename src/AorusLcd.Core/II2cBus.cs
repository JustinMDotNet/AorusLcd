namespace AorusLcd.Core;

/// <summary>Minimal raw I2C block transport (no register byte); callers own and dispose returned buses, though NVAPI disposal is a no-op.</summary>
public interface II2cBus : IDisposable
{
    /// <summary>Write <paramref name="data"/> as a single I2C block to the controller.</summary>
    void Write(ReadOnlySpan<byte> data);

    /// <summary>Read <paramref name="count"/> bytes back from the controller.</summary>
    byte[] Read(int count);
}
