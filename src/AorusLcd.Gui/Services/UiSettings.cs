using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AorusLcd.Gui.Services;

/// <summary>Per-user GUI preferences (tray visibility, etc.), persisted as JSON under the user's roaming app data.</summary>
public sealed class UiSettings
{
    /// <summary>Whether the system-tray icon is shown. When false, closing the window exits the GUI.</summary>
    public bool ShowTrayIcon { get; set; } = true;

    private static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AorusLcd", "ui.json");

    /// <summary>Load preferences, returning defaults if the file is absent or unreadable.</summary>
    public static UiSettings Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize(json, UiSettingsJson.Default.UiSettings) ?? new UiSettings();
            }
        }
        catch (Exception)
        {
            // fall through to defaults
        }
        return new UiSettings();
    }

    /// <summary>Persist preferences, creating the directory if needed. Never throws.</summary>
    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(path, JsonSerializer.Serialize(this, UiSettingsJson.Default.UiSettings));
        }
        catch (Exception)
        {
            // preferences are best-effort; a failed save must not crash the UI
        }
    }
}

/// <summary>Source-generated JSON context so serialization stays trim/analyzer clean.</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(UiSettings))]
internal sealed partial class UiSettingsJson : JsonSerializerContext;
