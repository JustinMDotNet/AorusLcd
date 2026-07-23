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
    /// <summary>
    /// Upper bound on decoded frames. A 320x170 panel needs nothing close to
    /// this; the cap stops a pathological multi-thousand-frame GIF from
    /// exhausting memory (each frame is decoded, RLE-compressed, and chunked).
    /// </summary>
    public const int MaxFrames = 256;

    // Mitchell cubic resampling gives clean downscales to 320x170 instead of the
    // aliased result of nearest-neighbour (SKSamplingOptions.Default).
    private static readonly SKSamplingOptions Downscale = new(SKCubicResampler.Mitchell);

    public static (List<byte[]> Frames, List<int> DelaysMs) DecodeLe565(string path)
    {
        using var codec = SKCodec.Create(path)
            ?? throw new InvalidOperationException("Could not decode GIF (unsupported or corrupt file).");

        int count = Math.Max(1, codec.FrameCount);
        if (count > MaxFrames)
        {
            throw new InvalidOperationException(
                $"GIF has {count} frames; the panel supports at most {MaxFrames}. Use a shorter GIF.");
        }

        var frameInfos = codec.FrameInfo;
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var scaledInfo = new SKImageInfo(Panel.Width, Panel.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

        var frames = new List<byte[]>(count);
        var delays = new List<int>(count);

        using var full = new SKBitmap(info);
        using var scaled = new SKBitmap(scaledInfo);
        for (int i = 0; i < count; i++)
        {
            // Reuse the previously composited frame (already in `full`) when this
            // frame depends on it - the common case - so SkiaSharp doesn't
            // re-decode the whole preceding chain (otherwise ~O(n²) work).
            int required = i < frameInfos.Length ? frameInfos[i].RequiredFrame : -1;
            var options = (i > 0 && required == i - 1)
                ? new SKCodecOptions(i, i - 1)
                : new SKCodecOptions(i);

            var result = codec.GetPixels(info, full.GetPixels(), options);
            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            {
                throw new InvalidOperationException($"GIF frame {i} failed to decode ({result}).");
            }

            full.ScalePixels(scaled, Downscale);
            frames.Add(ToLe565(scaled));

            int delayMs = i < frameInfos.Length ? frameInfos[i].Duration : 100;
            delays.Add(delayMs > 0 ? delayMs : 100);
        }
        return (frames, delays);
    }

    private static byte[] ToLe565(SKBitmap bgra)
        => Rgb565Encoder.EncodeBgra(bgra.GetPixelSpan());
}
