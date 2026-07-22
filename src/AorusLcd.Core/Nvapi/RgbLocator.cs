using System.Runtime.Versioning;
using AorusLcd.Core.Rgb;

namespace AorusLcd.Core.Nvapi;

/// <summary>
/// Locates the Aorus GPU RGB controller by scanning candidate I2C addresses on
/// GPU port 1. Prefers an address returning the RGB Fusion 2 <c>0xAB</c>
/// handshake; otherwise falls back to an address that ACKs the query write
/// (the RTX 5090 Master answers writes at 0x75 but does not reply to reads).
/// </summary>
[SupportedOSPlatform("windows")]
public static class RgbLocator
{
    private static readonly byte[] CandidateAddresses = [0x71, 0x72, 0x73, 0x74, 0x75];

    /// <summary>Find the best GPU/address for RGB control, or null if none respond.</summary>
    public static (RgbFusion2Controller Controller, string GpuName, byte Address, bool Handshake)? Locate()
    {
        (IntPtr Gpu, string Name, byte Address)? writeOnly = null;

        foreach (var gpu in NvApi.EnumPhysicalGpus())
        {
            string name = NvApi.GetFullName(gpu);
            foreach (byte addr in CandidateAddresses)
            {
                var (present, handshake, _) = Probe(gpu, addr);
                if (handshake)
                {
                    return (new RgbFusion2Controller(new NvApiI2cBus(gpu, addr, 1)), name, addr, true);
                }
                if (present)
                {
                    writeOnly ??= (gpu, name, addr);
                }
            }
        }

        if (writeOnly is { } w)
        {
            return (new RgbFusion2Controller(new NvApiI2cBus(w.Gpu, w.Address, 1)), w.Name, w.Address, false);
        }
        return null;
    }

    /// <summary>Describe every candidate address probe for diagnostics.</summary>
    public static IEnumerable<(string GpuName, byte Address, bool Present, bool Handshake, string Detail)> Survey()
    {
        foreach (var gpu in NvApi.EnumPhysicalGpus())
        {
            string name = NvApi.GetFullName(gpu);
            foreach (byte addr in CandidateAddresses)
            {
                var (present, handshake, response) = Probe(gpu, addr);
                string detail = handshake
                    ? $"RGB Fusion 2 handshake (response {Convert.ToHexString(response)})"
                    : present
                        ? "write ACK (no read-back) — usable for control"
                        : "no response";
                yield return (name, addr, present, handshake, detail);
            }
        }
    }

    private static (bool Present, bool Handshake, byte[] Response) Probe(IntPtr gpu, byte addr)
        => new RgbFusion2Controller(new NvApiI2cBus(gpu, addr, port: 1)).Detect();
}
