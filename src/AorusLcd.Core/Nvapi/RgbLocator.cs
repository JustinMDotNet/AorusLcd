using System.Runtime.Versioning;
using AorusLcd.Core.Rgb;

namespace AorusLcd.Core.Nvapi;

/// <summary>Finds the Aorus RGB controller only on the LCD-verified GPU, probing 0x71/0x75 with harmless 0xAB write-ACK.</summary>
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
                var controller = new RgbFusion2Controller(new NvApiI2cBus(gpu, addr, Port));
                if (controller.Detect().Present)
                {
                    return (controller, NvApi.GetFullName(gpu), addr);
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
                    present ? "write ACK - usable for control" : "no response");
            }
        }
    }

    /// <summary>LCD-verified Aorus GPUs whose 0x61 controller answers status; gates RGB writes to the correct card.</summary>
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
