using System.Runtime.Versioning;
using AorusLcd.Core.Rgb;

namespace AorusLcd.Core.Nvapi;

/// <summary>Which GPU RGB Fusion 2 protocol a card speaks: legacy 8-byte (pre-Blackwell) or 64-byte Blackwell (RTX 50-series).</summary>
public enum RgbControllerKind
{
    Legacy,
    Blackwell,
}

/// <summary>Locates the Aorus GPU RGB controller on the verified Aorus card (LCD answers 0x61) at a documented address (0x71/0x75).</summary>
[SupportedOSPlatform("windows")]
public static class RgbLocator
{
    private const byte Port = 1;
    private static readonly byte[] CandidateAddresses = [0x71, 0x75];

    /// <summary>Classify a GPU by name: RTX 50-series speaks the Blackwell protocol, everything older the legacy one.</summary>
    public static RgbControllerKind ClassifyByName(string? gpuName)
    {
        if (string.IsNullOrEmpty(gpuName))
        {
            return RgbControllerKind.Legacy;
        }
        var n = gpuName.ToUpperInvariant();
        return n.Contains("RTX 50") || n.Contains("RTX50")
            ? RgbControllerKind.Blackwell
            : RgbControllerKind.Legacy;
    }

    /// <summary>Find the RGB controller bus on the verified Aorus GPU (generation by name, presence by write-ACK), or null.</summary>
    public static (RgbControllerKind Kind, NvApiI2cBus Bus, string GpuName, byte Address)? LocateBus()
    {
        foreach (var gpu in AorusGpus())
        {
            string name = NvApi.GetFullName(gpu);
            var kind = ClassifyByName(name);
            foreach (byte addr in AddressOrder(kind))
            {
                var bus = new NvApiI2cBus(gpu, addr, Port);
                if (Present(bus, kind))
                {
                    return (kind, bus, name, addr);
                }
                bus.Dispose();
            }
        }
        return null;
    }

    /// <summary>Legacy-only locator kept for the 8-byte controller path and tests.</summary>
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

    // Prefer the address each generation is documented to answer on.
    private static byte[] AddressOrder(RgbControllerKind kind)
        => kind == RgbControllerKind.Blackwell ? [0x75, 0x71] : [0x71, 0x75];

    private static bool Present(NvApiI2cBus bus, RgbControllerKind kind)
        => kind == RgbControllerKind.Blackwell
            ? new RgbFusion2BlackwellController(bus).Detect()
            : new RgbFusion2Controller(bus).Detect().Present;

    /// <summary>GPUs confirmed as the Aorus LCD card (0x61 answers), gating RGB writes to the correct card.</summary>
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
