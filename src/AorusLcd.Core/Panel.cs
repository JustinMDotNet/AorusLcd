namespace AorusLcd.Core;

/// <summary>
/// Physical panel geometry and the fixed framebuffer/mode constants recovered
/// from Gigabyte Control Center captures. The panel is 320x170, little-endian
/// RGB565, row-major.
/// </summary>
public static class Panel
{
    public const int Width = 320;
    public const int Height = 170;
    public const int FramePixels = Width * Height;
    public const int FrameBytes = FramePixels * 2;

    /// <summary>
    /// 12-byte payload descriptor prepended before single-frame pixels. Encodes
    /// 320x170 plus fixed fields; identical in GCC's image and text uploads.
    /// </summary>
    public static readonly byte[] Descriptor =
    [
        0x01, 0x00, 0x0B, 0xA9, 0x01, 0x00, 0x40, 0x01, 0xAA, 0x00, 0x01, 0x00,
    ];

    public const uint FramebufferStatic = 0x01300000;
    public const uint FramebufferText = 0x01320000;
    public const uint FramebufferGif = 0x00000000;

    public const int ModeStatic = 3;
    public const int ModeText = 4;
    public const int ModeGif = 5;
}
