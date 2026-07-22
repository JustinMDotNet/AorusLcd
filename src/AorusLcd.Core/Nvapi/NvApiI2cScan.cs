using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace AorusLcd.Core.Nvapi;

/// <summary>
/// Low-level NVAPI I2C diagnostics for discovering the working port/address/
/// framing of a controller. Returns raw NVAPI status codes instead of throwing.
/// </summary>
[SupportedOSPlatform("windows")]
public static class NvApiI2cScan
{
    public readonly record struct Result(int GpuIndex, byte Port, byte Address, bool WithRegister, int Status)
    {
        public bool Ok => Status == 0;
    }

    /// <summary>
    /// Try a single write of <paramref name="data"/> to (port, address). When
    /// <paramref name="withRegister"/> is true, the first byte is sent as the
    /// I2C register and the remainder as SMBus block data (length-prefixed).
    /// </summary>
    public static Result TryWrite(int gpuIndex, byte port, byte address, byte[] data, bool withRegister)
    {
        var gpus = NvApi.EnumPhysicalGpus();
        if (gpuIndex >= gpus.Length)
        {
            return new Result(gpuIndex, port, address, withRegister, -100);
        }
        int status = withRegister
            ? WriteWithRegister(gpus[gpuIndex], port, address, data)
            : WriteRaw(gpus[gpuIndex], port, address, data);
        return new Result(gpuIndex, port, address, withRegister, status);
    }

    /// <summary>
    /// Sweep ports 0..7 and both framings for the given addresses, writing
    /// <paramref name="data"/> and collecting the NVAPI status of each attempt.
    /// </summary>
    public static IEnumerable<Result> Sweep(byte[] addresses, byte[] data)
    {
        var gpus = NvApi.EnumPhysicalGpus();
        for (int g = 0; g < gpus.Length; g++)
        {
            foreach (byte addr in addresses)
            {
                for (byte port = 0; port < 8; port++)
                {
                    yield return new Result(g, port, addr, false, WriteRaw(gpus[g], port, addr, data));
                    yield return new Result(g, port, addr, true, WriteWithRegister(gpus[g], port, addr, data));
                }
            }
        }
    }

    private static int WriteRaw(IntPtr gpu, byte port, byte address, byte[] data)
    {
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var info = BaseInfo(port, address);
            info.Data = handle.AddrOfPinnedObject();
            info.Size = (uint)data.Length;
            return NvApi.I2CWriteEx(gpu, ref info);
        }
        finally
        {
            handle.Free();
        }
    }

    private static int WriteWithRegister(IntPtr gpu, byte port, byte address, byte[] data)
    {
        // SMBus block data: register = data[0], payload = data[1..], length-prefixed.
        byte register = data[0];
        var payload = data[1..];
        var block = new byte[payload.Length + 1];
        block[0] = (byte)payload.Length;
        payload.CopyTo(block, 1);

        var regHandle = GCHandle.Alloc(new[] { register }, GCHandleType.Pinned);
        var dataHandle = GCHandle.Alloc(block, GCHandleType.Pinned);
        try
        {
            var info = BaseInfo(port, address);
            info.I2cRegAddress = regHandle.AddrOfPinnedObject();
            info.RegAddrSize = 1;
            info.Data = dataHandle.AddrOfPinnedObject();
            info.Size = (uint)block.Length;
            return NvApi.I2CWriteEx(gpu, ref info);
        }
        finally
        {
            regHandle.Free();
            dataHandle.Free();
        }
    }

    private static NvI2cInfoV3 BaseInfo(byte port, byte address) => new()
    {
        Version = NvI2cInfoV3.Version3,
        DisplayMask = 0,
        IsDdcPort = 0,
        I2cDevAddress = (byte)(address << 1),
        I2cRegAddress = IntPtr.Zero,
        RegAddrSize = 0,
        I2cSpeed = 0xFFFF,
        I2cSpeedKhz = 0,
        PortId = port,
        IsPortIdSet = 1,
    };
}
