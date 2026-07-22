using System.Globalization;
using System.Runtime.InteropServices;
using AorusLcd.Core;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace AorusLcd.Gui.Services;

/// <summary>
/// Cross-platform text rendering for the panel using Avalonia (no System.Drawing).
/// Renders centered text on a 320x170 canvas and converts to LE-RGB565.
/// </summary>
public static class PanelText
{
    public static byte[] RenderLe565(string text, double size, Color foreground, Color background)
    {
        var pixel = new PixelSize(Panel.Width, Panel.Height);
        using var rtb = new RenderTargetBitmap(pixel);
        using (var ctx = rtb.CreateDrawingContext())
        {
            ctx.FillRectangle(new SolidColorBrush(background), new Rect(0, 0, Panel.Width, Panel.Height));
            var formatted = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold),
                size,
                new SolidColorBrush(foreground));
            var origin = new Point(
                (Panel.Width - formatted.Width) / 2,
                (Panel.Height - formatted.Height) / 2);
            ctx.DrawText(formatted, origin);
        }
        return CopyLe565(rtb);
    }

    internal static byte[] CopyLe565(RenderTargetBitmap rtb)
    {
        int stride = Panel.Width * 4;
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
