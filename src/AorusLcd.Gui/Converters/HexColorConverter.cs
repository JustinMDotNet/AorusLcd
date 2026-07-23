using System;
using System.Globalization;
using AorusLcd.Core.Rgb;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AorusLcd.Gui.Converters;

/// <summary>
/// Two-way converter between a bare <c>RRGGBB</c> hex string (the view-model's
/// representation) and an Avalonia <see cref="Color"/> for <c>ColorPicker</c>.
/// Invalid hex falls back to black rather than throwing, matching the rest of
/// the UI's forgiving colour handling.
/// </summary>
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
