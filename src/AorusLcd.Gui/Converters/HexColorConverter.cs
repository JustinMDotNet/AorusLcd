using System;
using System.Globalization;
using AorusLcd.Core.Rgb;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AorusLcd.Gui.Converters;

/// <summary>Two-way bare <c>RRGGBB</c> ↔ Avalonia <see cref="Color"/> converter; invalid hex falls back to black.</summary>
public sealed class HexColorConverter : IValueConverter
{
    public static readonly HexColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string hex && RgbColor.TryParse(hex, out var c)
            ? Color.FromRgb(c.R, c.G, c.B)
            : Colors.Black;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Color color
            ? $"{color.R:X2}{color.G:X2}{color.B:X2}"
            : null;
}
