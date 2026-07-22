using System.Runtime.Versioning;

namespace AorusLcd.Core.Nvapi;

/// <summary>
/// Locates the GPU whose internal I2C port answers the LCD controller's EB 03
/// status query at 0x61. Analogous to the Linux tool's bus autodetection, but
/// over NVAPI physical GPUs / ports instead of <c>/dev/i2c-N</c>. Refuses to
/// return a bus that does not answer, so nothing is ever written blindly.
/// </summary>
[SupportedOSPlatform("windows")]
public static class NvApiPanelLocator
{
    /// <summary>
    /// Find an LCD-capable bus. Enumerates physical GPUs and probes the given
    /// <paramref name="port"/> at 0x61. Returns the first that answers, along
    /// with the GPU name, or null if none respond.
    /// </summary>
    public static (NvApiI2cBus Bus, string GpuName)? Locate(byte port = 1)
    {
        foreach (var gpu in NvApi.EnumPhysicalGpus())
        {
            var bus = new NvApiI2cBus(gpu, address: 0x61, port: port);
            if (TryProbe(bus))
            {
                return (bus, NvApi.GetFullName(gpu));
            }
        }
        return null;
    }

    /// <summary>
    /// Describe every physical GPU and whether 0x61 answers on <paramref name="port"/>.
    /// Used by the <c>probe</c> command for diagnostics.
    /// </summary>
    public static IEnumerable<(string GpuName, bool Responds, string Detail)> Survey(byte port = 1)
    {
        foreach (var gpu in NvApi.EnumPhysicalGpus())
        {
            string name = NvApi.GetFullName(gpu);
            var bus = new NvApiI2cBus(gpu, address: 0x61, port: port);
            byte[]? status = null;
            string detail;
            try
            {
                status = new PanelController(bus).Probe();
                detail = $"device present at 0x61 (status {Convert.ToHexString(status)})";
            }
            catch (NvApiException e)
            {
                detail = e.Message;
            }
            yield return (name, status is not null, detail);
        }
    }

    private static bool TryProbe(NvApiI2cBus bus)
    {
        try
        {
            new PanelController(bus).Probe();
            return true;
        }
        catch (NvApiException)
        {
            return false;
        }
    }
}
