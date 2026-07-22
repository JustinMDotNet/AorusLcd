using System;
using System.Threading;
using System.Threading.Tasks;
using AorusLcd.Core.Sensors;

namespace AorusLcd.Gui.Services;

/// <summary>
/// Background loop that reads GPU sensors (NVML) and pushes the panel's live
/// <c>E3</c> feed while running. This replaces the vendor service's sensor feed
/// with a single lightweight thread that only runs while the sensor dashboard
/// is active.
///
/// The poll cadence is adaptive (see <see cref="ComputePollInterval"/>): a
/// single always-on widget refreshes ~1 s so its number looks live, while a
/// rotating dashboard only needs a fresh value each time a widget appears, so
/// it polls at the rotation interval. Errors are surfaced via <see cref="Error"/>
/// but never crash the app.
/// </summary>
public sealed class SensorFeedService(HardwareService hardware) : IDisposable
{
    private const int MinPollMs = 1000;
    private const int MaxPollMs = 5000;

    private readonly HardwareService _hardware = hardware;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private ISensorSource? _sensors;
    private volatile int _pollIntervalMs = MinPollMs;

    public bool IsRunning => _loop is { IsCompleted: false };

    /// <summary>The current poll cadence in milliseconds.</summary>
    public int PollIntervalMs => _pollIntervalMs;

    /// <summary>Raised (on a background thread) with a human-readable error.</summary>
    public event Action<string>? Error;

    /// <summary>
    /// Adaptive cadence: a single widget stays live at the floor; a rotating
    /// dashboard polls at its rotation interval (clamped to [1s, 5s]).
    /// </summary>
    public static int ComputePollInterval(int widgetCount, int rotationIntervalSeconds)
    {
        if (widgetCount <= 1)
        {
            return MinPollMs;
        }
        int ms = rotationIntervalSeconds * 1000;
        return Math.Clamp(ms, MinPollMs, MaxPollMs);
    }

    public void Start(int widgetCount, int rotationIntervalSeconds)
    {
        _pollIntervalMs = ComputePollInterval(widgetCount, rotationIntervalSeconds);
        if (IsRunning)
        {
            return; // cadence updated live; loop picks it up on its next delay
        }
        _sensors = new NvmlSensorSource();
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            _loop?.Wait(2000);
        }
        catch (AggregateException)
        {
            // cancellation
        }
        _loop = null;
        _cts?.Dispose();
        _cts = null;
        _sensors?.Dispose();
        _sensors = null;
    }

    private async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var sample = _sensors!.Read();
                _hardware.SendSensorFeed(sample);
            }
            catch (Exception e)
            {
                Error?.Invoke(e.Message);
            }

            try
            {
                await Task.Delay(_pollIntervalMs, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    public void Dispose() => Stop();
}
