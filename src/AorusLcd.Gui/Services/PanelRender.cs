using System.Runtime.InteropServices;
using AorusLcd.Core;
using Avalonia;
using Avalonia.Media.Imaging;

namespace AorusLcd.Gui.Services;

/// <summary>Copy a 320x170 render target into the panel little-endian RGB565 frame for image/text paths.</summary>
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
