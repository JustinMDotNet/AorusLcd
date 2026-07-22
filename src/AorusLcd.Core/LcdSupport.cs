namespace AorusLcd.Core;

/// <summary>
/// LCD-capable Gigabyte GPU subsystem IDs (SSIDs), from ucVga.dll's
/// <c>GvLcdApi.IsSupportLcd</c>. Used to confirm a card has an Edge View panel.
/// </summary>
public static class LcdSupport
{
    private static readonly HashSet<int> SupportedSsids =
    [
        16576, 16573, 16693, 16571, 16526, 16450, 16513, 16549, 16446, 16489,
        16542, 16465, 9010, 9003, 9001, 9002, 16445, 16512, 16449, 16721,
        16750, 16760, 16763, 16776, 16780, 16791, 16793, 16821, 16841, 16842,
    ];

    /// <summary>True if the given SSID is a known LCD-capable Aorus card.</summary>
    public static bool IsSupported(int ssid) => SupportedSsids.Contains(ssid);
}
