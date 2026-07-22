using AorusLcd.Core;

namespace AorusLcd.Gui.Models;

/// <summary>A selectable built-in panel screen (name + underlying LCD mode).</summary>
public record ModePreset(string Name, LcdMode Mode);
