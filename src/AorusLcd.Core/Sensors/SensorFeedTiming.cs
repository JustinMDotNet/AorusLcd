namespace AorusLcd.Core.Sensors;

/// <summary>
/// Adaptive poll cadence for the sensor feed, shared by the GUI and the service.
/// A single always-on widget refreshes at the floor (~1 s) so its number looks
/// live; a rotating dashboard only needs a fresh value each time a widget
/// appears, so it polls at the rotation interval (clamped to [1 s, 5 s]) to
/// minimise bus traffic.
/// </summary>
public static class SensorFeedTiming
{
    public const int MinPollMs = 1000;
    public const int MaxPollMs = 5000;

    /// <summary>Poll interval (ms) for the given widget count and rotation interval.</summary>
    public static int PollIntervalMs(int widgetCount, int rotationIntervalSeconds)
    {
        if (widgetCount <= 1)
        {
            return MinPollMs;
        }
        return Math.Clamp(rotationIntervalSeconds * 1000, MinPollMs, MaxPollMs);
    }

    /// <summary>Poll interval (ms) for a dashboard element selection.</summary>
    public static int PollIntervalMs(LcdDisplayElements elements, int rotationIntervalSeconds)
        => PollIntervalMs(System.Numerics.BitOperations.PopCount((uint)elements), rotationIntervalSeconds);
}
