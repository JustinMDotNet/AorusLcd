using System;
using System.Collections.Generic;
using AorusLcd.Core;
using SkiaSharp;

namespace AorusLcd.Gui.Services;

/// <summary>
/// Cross-platform animated-GIF decoding via SkiaSharp (bundled with Avalonia).
/// Produces 320x170 LE-RGB565 frames and per-frame delays for the panel's GIF
/// upload path.
/// </summary>
public static class GifDecoder
{
    public static (List<byte[]> Frames, List<int> DelaysMs) DecodeLe565(string path)
    {
        using var codec = SKCodec.Create(path)
            ?? throw new InvalidOperationException("Could not decode GIF (unsupported or corrupt file).");

        int count = Math.Max(1, codec.FrameCount);
        var frameInfos = codec.FrameInfo;
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

        var frames = new List<byte[]>(count);
        var delays = new List<int>(count);

        using var full = new SKBitmap(info);
        for (int i = 0; i < count; i++)
        {
            var options = new SKCodecOptions(i);
            codec.GetPixels(info, full.GetPixels(), options);

            using var scaled = full.Resize(new SKImageInfo(Panel.Width, Panel.Height, SKColorType.Bgra8888, SKAlphaType.Premul), SKSamplingOptions.Default);
            frames.Add(ToLe565(scaled));

            int delayMs = i < frameInfos.Length ? frameInfos[i].Duration : 100;
            delays.Add(delayMs > 0 ? delayMs : 100);
        }
        return (frames, delays);
    }

    private static byte[] ToLe565(SKBitmap bgra)
    {
        var span = bgra.GetPixelSpan();
        var rgb = new byte[Panel.FramePixels * 3];
        int di = 0;
        for (int i = 0; i + 3 < span.Length && di + 2 < rgb.Length; i += 4)
        {
            rgb[di++] = span[i + 2]; // R
            rgb[di++] = span[i + 1]; // G
            rgb[di++] = span[i];     // B
        }
        return Rgb565Encoder.Encode(rgb);
    }
}
