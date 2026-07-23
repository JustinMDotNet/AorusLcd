using System.Globalization;
using AorusLcd.Core;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace AorusLcd.Gui.Services;

/// <summary>Avalonia text rendering: centered text on 320x170 canvas converted to LE-RGB565.</summary>
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
        return PanelRender.ToLe565(rtb);
    }
}
