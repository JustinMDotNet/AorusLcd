using System.Text.Json;
using System.Text.Json.Serialization;

namespace AorusLcd.Core.Sensors;

/// <summary>
/// Persisted configuration for the background sensor feed, shared between the
/// GUI (which writes it) and the Windows service (which reads it). Stored as
/// JSON at <see cref="DefaultPath"/> under ProgramData so it is machine-wide and
/// readable by the service running under its own account.
/// </summary>
public sealed record FeedConfig
{
    /// <summary>Dashboard elements to display (bitmask of <see cref="LcdDisplayElements"/>).</summary>
    public uint DisplayElements { get; init; }

    /// <summary>Rotation interval in seconds for the built-in dashboard.</summary>
    public int IntervalSeconds { get; init; } = 4;

    /// <summary>Whether the background feed is enabled.</summary>
    public bool Enabled { get; init; }

    public LcdDisplayElements Elements => (LcdDisplayElements)DisplayElements;

    /// <summary>Machine-wide config path: <c>%ProgramData%\AorusLcd\feed.json</c>.</summary>
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AorusLcd", "feed.json");

    /// <summary>Load the config, or return a disabled default if absent/unreadable.</summary>
    public static FeedConfig Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize(json, FeedConfigJson.Default.FeedConfig) ?? new FeedConfig();
            }
        }
        catch (Exception)
        {
            // fall through to default
        }
        return new FeedConfig();
    }

    /// <summary>Persist the config, creating the directory if needed.</summary>
    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, FeedConfigJson.Default.FeedConfig));
    }
}

/// <summary>Source-generated JSON context so serialization works under NativeAOT.</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(FeedConfig))]
internal partial class FeedConfigJson : JsonSerializerContext;
