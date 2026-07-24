using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace AorusLcd.Gui.Converters;

/// <summary>Binds a RadioButton's IsChecked to a string property matching the ConverterParameter, for a single-selection group.</summary>
public sealed class StringEqualityConverter : IValueConverter
{
    public static readonly StringEqualityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value as string, parameter as string, StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? parameter : BindingOperations.DoNothing;
}
