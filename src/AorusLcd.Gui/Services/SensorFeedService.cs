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
/// but never crash the app. The NVML source is owned by the loop and disposed
/// only after the loop has exited, so shutdown never disposes it mid-read.
/// </summary>
public sealed class SensorFeedService(HardwareService hardware) : IAsyncDisposable
{
    private const int MinPollMs = 1000;
    private const int MaxPollMs = 5000;

    private readonly HardwareService _hardware = hardware;
    private CancellationTokenSource? _cts;
    private Task? _loop;
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

    /// <summary>
    /// Start (or re-tune) the feed. Constructs the NVML source up front so an
    /// unavailable driver surfaces immediately (before the dashboard is trusted).
    /// </summary>
    public void Start(int widgetCount, int rotationIntervalSeconds, uint? pciBusId)
    {
        _pollIntervalMs = ComputePollInterval(widgetCount, rotationIntervalSeconds);
        if (IsRunning)
        {
            return; // cadence updated live; loop picks it up on its next delay
        }
        var sensors = new NvmlSensorSource(pciBusId); // may throw if NVML is unavailable
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(sensors, _cts.Token));
    }

    /// <summary>Stop the feed and await the loop's clean exit (never blocks a thread).</summary>
    public async Task StopAsync()
    {
        if (_cts is null)
        {
            return;
        }
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected on cancellation
            }
        }
        _loop = null;
        _cts.Dispose();
        _cts = null;
    }

    private async Task RunAsync(ISensorSource sensors, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var sample = sensors.Read();
                    await _hardware.SendSensorFeedAsync(sample, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    Error?.Invoke(e.Message);
                }

                try
                {
                    await Task.Delay(_pollIntervalMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            sensors.Dispose();
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
