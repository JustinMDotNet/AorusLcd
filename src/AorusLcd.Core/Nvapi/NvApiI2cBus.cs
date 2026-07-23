using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace AorusLcd.Core.Nvapi;

/// <summary>
/// <see cref="II2cBus"/> backed by NVAPI I2C on a specific physical GPU. Writes
/// raw blocks (no register address) to <see cref="Address"/> on GPU port
/// <see cref="Port"/> - the internal controller bus, where port 1 carries the
/// Aorus LCD controller (0x61) and RGB controller (0x71/0x75). Disposal is a
/// no-op because the GPU handle is owned by NVAPI.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class NvApiI2cBus(IntPtr gpuHandle, byte address = 0x61, byte port = 1) : II2cBus
{
    private const uint SpeedDeprecated = 0xFFFF;
    private const uint SpeedDefaultKhz = 0; // NVAPI_I2C_SPEED_DEFAULT

    public byte Address { get; } = address;
    public byte Port { get; } = port;

    /// <summary>PCI bus number of the underlying GPU (for matching to NVML), or null.</summary>
    public uint? PciBusId => NvApi.GetBusId(gpuHandle);

    public void Write(ReadOnlySpan<byte> data)
    {
        var buffer = data.ToArray();
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            var info = BuildInfo(handle.AddrOfPinnedObject(), (uint)buffer.Length);
            int status = NvApi.I2CWriteEx(gpuHandle, ref info);
            if (status != 0)
            {
                throw new NvApiException($"NvAPI_I2CWriteEx (0x{Address:X2} port {Port})", status);
            }
        }
        finally
        {
            handle.Free();
        }
    }

    public byte[] Read(int count)
    {
        var buffer = new byte[count];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            var info = BuildInfo(handle.AddrOfPinnedObject(), (uint)count);
            int status = NvApi.I2CReadEx(gpuHandle, ref info);
            if (status != 0)
            {
                throw new NvApiException($"NvAPI_I2CReadEx (0x{Address:X2} port {Port})", status);
            }
        }
        finally
        {
            handle.Free();
        }
        return buffer;
    }

    private NvI2cInfoV3 BuildInfo(IntPtr dataPtr, uint size) => new()
    {
        Version = NvI2cInfoV3.Version3,
        DisplayMask = 0,
        IsDdcPort = 0,
        I2cDevAddress = (byte)(Address << 1),
        I2cRegAddress = IntPtr.Zero,
        RegAddrSize = 0,
        Data = dataPtr,
        Size = size,
        I2cSpeed = SpeedDeprecated,
        I2cSpeedKhz = SpeedDefaultKhz,
        PortId = Port,
        IsPortIdSet = 1,
    };

    public void Dispose()
    {
        // GPU handles are owned by NVAPI; nothing to release per-bus.
    }
}
