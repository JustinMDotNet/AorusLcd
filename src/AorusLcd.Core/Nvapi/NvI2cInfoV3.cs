using System.Runtime.InteropServices;

namespace AorusLcd.Core.Nvapi;

/// <summary>Managed <c>NV_I2C_INFO_V3</c> mirror; sequential x64 layout keeps native offsets, including 8-aligned pointers.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NvI2cInfoV3
{
    public uint Version;
    public uint DisplayMask;
    public byte IsDdcPort;
    public byte I2cDevAddress;
    public IntPtr I2cRegAddress;
    public uint RegAddrSize;
    public IntPtr Data;
    public uint Size;
    public uint I2cSpeed;
    public uint I2cSpeedKhz;
    public byte PortId;
    public uint IsPortIdSet;

    /// <summary>NV_STRUCT_VERSION(NV_I2C_INFO_V3, 3) = (3 &lt;&lt; 16) | sizeof.</summary>
    public static uint Version3 => (3u << 16) | (uint)Marshal.SizeOf<NvI2cInfoV3>();
}
