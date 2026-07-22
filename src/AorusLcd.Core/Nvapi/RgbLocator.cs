using System.Runtime.Versioning;
using AorusLcd.Core.Rgb;

namespace AorusLcd.Core.Nvapi;

/// <summary>
/// Locates the Aorus GPU RGB controller. To avoid writing to unrelated devices,
/// discovery is restricted to the GPU already verified as the Aorus card (its
/// LCD controller answers the <c>EB 03</c> status query at 0x61) and to the two
/// documented RGB addresses (0x71, or 0x75 on the RTX 5090 Master). The 5090
/// Master RGB controller is write-only, so presence is detected by a write-ACK
/// of the harmless <c>0xAB</c> query rather than a read that would wedge the bus.
/// </summary>
[SupportedOSPlatform("windows")]
public static class RgbLocator
{
    private const byte Port = 1;
    private static readonly byte[] CandidateAddresses = [0x71, 0x75];

    /// <summary>Find the RGB controller on the verified Aorus GPU, or null.</summary>
    public static (RgbFusion2Controller Controller, string GpuName, byte Address)? Locate()
    {
        foreach (var gpu in AorusGpus())
        {
            foreach (byte addr in CandidateAddresses)
            {
                if (new RgbFusion2Controller(new NvApiI2cBus(gpu, addr, Port)).Detect().Present)
                {
                    return (new RgbFusion2Controller(new NvApiI2cBus(gpu, addr, Port)), NvApi.GetFullName(gpu), addr);
                }
            }
        }
        return null;
    }

    /// <summary>Describe candidate address probes on verified Aorus GPUs.</summary>
    public static IEnumerable<(string GpuName, byte Address, bool Present, string Detail)> Survey()
    {
        foreach (var gpu in AorusGpus())
        {
            string name = NvApi.GetFullName(gpu);
            foreach (byte addr in CandidateAddresses)
            {
                bool present = new RgbFusion2Controller(new NvApiI2cBus(gpu, addr, Port)).Detect().Present;
                yield return (name, addr, present,
                    present ? "write ACK — usable for control" : "no response");
            }
        }
    }

    /// <summary>
    /// GPUs confirmed to be the Aorus LCD card: the LCD controller at 0x61
    /// answers its status query. This gates RGB writes to the correct card.
    /// </summary>
    private static IEnumerable<IntPtr> AorusGpus()
    {
        foreach (var gpu in NvApi.EnumPhysicalGpus())
        {
            bool isAorus;
            try
            {
                new PanelController(new NvApiI2cBus(gpu, address: 0x61, port: Port)).Probe();
                isAorus = true;
            }
            catch (NvApiException)
            {
                isAorus = false;
            }
            if (isAorus)
            {
                yield return gpu;
            }
        }
    }
}
