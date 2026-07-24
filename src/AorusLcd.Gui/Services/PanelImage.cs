using System.IO;
using AorusLcd.Core;
using Avalonia;
using Avalonia.Media.Imaging;

namespace AorusLcd.Gui.Services;

/// <summary>Avalonia image conversion to the panel 320x170 little-endian RGB565 frame via <see cref="Rgb565Encoder"/>.</summary>
public static class PanelImage
{
    /// <summary>Load an image file and convert it to a 320x170 LE-RGB565 frame.</summary>
    public static byte[] LoadLe565(string path)
    {
        using var stream = File.OpenRead(path);
        // Decode size-constrained at 2x panel width to preserve downscale quality without huge full-photo allocations.
        using var source = Bitmap.DecodeToWidth(stream, Panel.Width * 2, BitmapInterpolationMode.HighQuality);
        return ToLe565(source);
    }

    /// <summary>Render a source bitmap to the exact 320x170 frame that gets sent. Caller owns disposal.</summary>
    public static RenderTargetBitmap Render320(Bitmap source)
    {
        var size = new PixelSize(Panel.Width, Panel.Height);
        var rtb = new RenderTargetBitmap(size);
        using (var ctx = rtb.CreateDrawingContext())
        {
            ctx.DrawImage(source, new Rect(0, 0, Panel.Width, Panel.Height));
        }
        return rtb;
    }

    /// <summary>Convert an already-loaded bitmap to a 320x170 LE-RGB565 frame.</summary>
    public static byte[] ToLe565(Bitmap source)
    {
        using var rtb = Render320(source);
        return PanelRender.ToLe565(rtb);
    }
}
