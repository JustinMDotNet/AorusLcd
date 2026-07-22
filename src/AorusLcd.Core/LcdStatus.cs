namespace AorusLcd.Core;

/// <summary>
/// Overlay template configuration for <c>SetImageTpl</c> (ucVga.dll
/// <c>TPL_CFG</c>): a text/data color, the image and data overlay positions,
/// and whether the template is enabled.
/// </summary>
public sealed class LcdTemplate
{
    public LcdTemplateType Type { get; init; } = LcdTemplateType.Image;
    public byte ColorR { get; init; } = 0xFF;
    public byte ColorG { get; init; } = 0xFF;
    public byte ColorB { get; init; } = 0xFF;
    public (int X, int Y) ImagePosition { get; init; }
    public (int X, int Y) DataPosition { get; init; }
    public bool Enabled { get; init; }
}

/// <summary>Read-back of the panel's current state (mode, dashboard, carousel).</summary>
public sealed class LcdStatus
{
    public string FirmwareVersion { get; init; } = "0.0";
    public LcdMode Mode { get; init; }
    public bool IsOn { get; init; }
    public LcdDisplayElements DisplayElements { get; init; }
    public int DisplayInterval { get; init; }
    public IReadOnlyList<int> CarouselModes { get; init; } = [];
    public int CarouselInterval { get; init; }
}
