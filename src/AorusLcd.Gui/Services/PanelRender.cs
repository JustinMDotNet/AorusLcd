using System.Runtime.InteropServices;
using AorusLcd.Core;
using Avalonia;
using Avalonia.Media.Imaging;

namespace AorusLcd.Gui.Services;

/// <summary>
/// Shared helper for the image and text paths: copy a 320x170 render target's
/// pixels into the panel's little-endian RGB565 frame.
/// </summary>
internal static class PanelRender
{
    public static byte[] ToLe565(RenderTargetBitmap rtb)
    {
        int stride = Panel.Width * 4; // Bgra8888
        var bgra = new byte[stride * Panel.Height];
        var handle = GCHandle.Alloc(bgra, GCHandleType.Pinned);
        try
        {
            rtb.CopyPixels(new PixelRect(0, 0, Panel.Width, Panel.Height),
                handle.AddrOfPinnedObject(), bgra.Length, stride);
        }
        finally
        {
            handle.Free();
        }
        return Rgb565Encoder.EncodeBgra(bgra);
    }
}
