using System;
using System.Runtime.InteropServices;
using AorusLcd.Core;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AorusLcd.Gui.Services;

/// <summary>
/// Cross-platform image conversion for the panel using Avalonia's own imaging
/// (no System.Drawing), producing the panel's 320x170 little-endian RGB565
/// frame via <see cref="Rgb565Encoder"/>.
/// </summary>
public static class PanelImage
{
    /// <summary>Load an image file and convert it to a 320x170 LE-RGB565 frame.</summary>
    public static byte[] LoadLe565(string path)
    {
        using var source = new Bitmap(path);
        return ToLe565(source);
    }

    /// <summary>Convert an already-loaded bitmap to a 320x170 LE-RGB565 frame.</summary>
    public static byte[] ToLe565(Bitmap source)
    {
        var size = new PixelSize(Panel.Width, Panel.Height);
        using var rtb = new RenderTargetBitmap(size);
        using (var ctx = rtb.CreateDrawingContext())
        {
            ctx.DrawImage(source, new Rect(0, 0, Panel.Width, Panel.Height));
        }

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

        var rgb = new byte[Panel.FramePixels * 3];
        int di = 0;
        for (int i = 0; i < bgra.Length; i += 4)
        {
            rgb[di++] = bgra[i + 2]; // R
            rgb[di++] = bgra[i + 1]; // G
            rgb[di++] = bgra[i];     // B
        }
        return Rgb565Encoder.Encode(rgb);
    }
}
