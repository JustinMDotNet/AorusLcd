namespace AorusLcd.Core.Sensors;

/// <summary>Adaptive feed cadence: one widget ~1 s, rotating dashboard uses interval clamped to [1 s, 5 s] to reduce bus traffic.</summary>
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
