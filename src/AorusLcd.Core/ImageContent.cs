using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace AorusLcd.Core;

/// <summary>
/// Loads/renders image, text and GIF content and converts it to the panel's
/// 320x170 little-endian RGB565 format using GDI+ (System.Drawing). The pure
/// RGB565/RLE encoders live in <see cref="Rgb565Encoder"/> and
/// <see cref="RleEncoder"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ImageContent
{
    private const int GifFrameDelayPropertyId = 0x5100; // PropertyTagFrameDelay (centiseconds)

    /// <summary>Load any image file -> 320x170 LE-RGB565 (high-quality resize).</summary>
    public static byte[] LoadImageLe565(string path)
    {
        using var src = new Bitmap(path);
        using var panel = ResizeToPanel(src);
        return BitmapToLe565(panel);
    }

    /// <summary>
    /// Render <paramref name="text"/> centered on a 320x170 canvas -> LE-RGB565.
    /// Defaults match GCC's text upload (black background, ~#8b8d8b gray text,
    /// which the panel's rainbow effect uses as a luminance mask).
    /// </summary>
    public static byte[] RenderTextLe565(string text, int size,
        (byte R, byte G, byte B) fg, (byte R, byte G, byte B) bg)
    {
        using var bmp = new Bitmap(Panel.Width, Panel.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(bg.R, bg.G, bg.B));
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var font = new Font("Arial", size, FontStyle.Bold, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(Color.FromArgb(fg.R, fg.G, fg.B));
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            g.DrawString(text, font, brush, new RectangleF(0, 0, Panel.Width, Panel.Height), format);
        }
        return BitmapToLe565(bmp);
    }

    /// <summary>Decode an animated GIF -> (LE-RGB565 frames, per-frame delays in ms).</summary>
    public static (List<byte[]> Frames, List<int> DelaysMs) GifToLe565Frames(string path)
    {
        using var img = Image.FromFile(path);
        var dimension = new FrameDimension(img.FrameDimensionsList[0]);
        int count = img.GetFrameCount(dimension);
        int[] delaysCs = ReadFrameDelaysCentiseconds(img, count);

        var frames = new List<byte[]>(count);
        var delays = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            img.SelectActiveFrame(dimension, i);
            using var panel = ResizeToPanel(img);
            frames.Add(BitmapToLe565(panel));
            int cs = i < delaysCs.Length ? delaysCs[i] : 10;
            delays.Add(cs > 0 ? cs * 10 : 100); // GIF centiseconds -> ms
        }
        return (frames, delays);
    }

    private static Bitmap ResizeToPanel(Image src)
    {
        var bmp = new Bitmap(Panel.Width, Panel.Height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(src, new Rectangle(0, 0, Panel.Width, Panel.Height));
        return bmp;
    }

    private static byte[] BitmapToLe565(Bitmap bmp)
    {
        var rect = new Rectangle(0, 0, Panel.Width, Panel.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            int stride = data.Stride;
            var raw = new byte[stride * Panel.Height];
            Marshal.Copy(data.Scan0, raw, 0, raw.Length);

            var rgb = new byte[Panel.FramePixels * 3];
            int di = 0;
            for (int y = 0; y < Panel.Height; y++)
            {
                int row = y * stride;
                for (int x = 0; x < Panel.Width; x++)
                {
                    int p = row + (x * 3);
                    rgb[di++] = raw[p + 2]; // R (GDI stores BGR)
                    rgb[di++] = raw[p + 1]; // G
                    rgb[di++] = raw[p];     // B
                }
            }
            return Rgb565Encoder.Encode(rgb);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    private static int[] ReadFrameDelaysCentiseconds(Image img, int count)
    {
        try
        {
            var property = img.GetPropertyItem(GifFrameDelayPropertyId);
            var bytes = property?.Value;
            if (bytes is null)
            {
                return [];
            }
            var result = new int[count];
            for (int i = 0; i < count && (i * 4) + 3 < bytes.Length; i++)
            {
                result[i] = BitConverter.ToInt32(bytes, i * 4);
            }
            return result;
        }
        catch (ArgumentException)
        {
            return []; // no frame-delay metadata
        }
    }
}
