namespace AorusLcd.Core;

/// <summary>
/// Minimal I2C block transport to a controller on the GPU bus. Mirrors the two
/// operations the protocol needs: a raw block write, and a raw block read of a
/// fixed length. No register/command byte is used.
///
/// Ownership: whoever obtains an <see cref="II2cBus"/> owns it and must dispose
/// it. A locator that probes several candidate buses disposes the ones it does
/// not return; the caller disposes the one it keeps. On the NVAPI backend
/// disposal is a no-op (GPU handles are owned by NVAPI), but a future Linux
/// i2c-dev backend will use it to close the device file descriptor.
/// </summary>
public interface II2cBus : IDisposable
{
    /// <summary>Write <paramref name="data"/> as a single I2C block to the controller.</summary>
    void Write(ReadOnlySpan<byte> data);

    /// <summary>Read <paramref name="count"/> bytes back from the controller.</summary>
    byte[] Read(int count);
}
