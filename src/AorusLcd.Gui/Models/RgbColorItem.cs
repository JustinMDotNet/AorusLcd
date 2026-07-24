using AorusLcd.Core.Rgb;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AorusLcd.Gui.Models;

/// <summary>One editable colour in the RGB effect list; exposes a swatch brush for the live preview.</summary>
public partial class RgbColorItem : ObservableObject
{
    public RgbColorItem(string hex) => Hex = hex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Swatch))]
    public partial string Hex { get; set; }

    /// <summary>Solid brush for the per-colour swatch; invalid hex renders as black.</summary>
    public IBrush Swatch => new SolidColorBrush(
        RgbColor.TryParse(Hex, out var c) ? Color.FromRgb(c.R, c.G, c.B) : Colors.Black);
}
