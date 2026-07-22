using System.Runtime.Versioning;
using AorusLcd.Core;
using AorusLcd.Core.Nvapi;
using AorusLcd.Core.Sensors;
using Microsoft.Extensions.Hosting;

namespace AorusLcd.Service;

/// <summary>
/// Background worker that drives the panel's live sensor feed. It locates the
/// Aorus GPU (NVAPI), reads <see cref="FeedConfig"/>, and — while the feed is
/// enabled — runs the shared <see cref="SensorFeedLoop"/> (E1 dashboard + E3
/// values). The config file is watched, so the GUI can enable/disable or retune
/// the feed live without reinstalling or restarting the service.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FeedWorker : BackgroundService
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AorusLcd", "service.log");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log("service starting");
        while (!stoppingToken.IsCancellationRequested)
        {
            var config = FeedConfig.Load();
            if (!config.Enabled || config.Elements == LcdDisplayElements.None)
            {
                await WaitForConfigChangeAsync(stoppingToken);
                continue;
            }

            try
            {
                await RunFeedAsync(config, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                Log($"feed error: {e.Message}; retrying in 10s");
                await DelaySafe(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
        Log("service stopping");
    }

    /// <summary>
    /// Run the feed for the current config until the config file changes or the
    /// service is stopping, so a live edit takes effect on the next loop.
    /// </summary>
    private async Task RunFeedAsync(FeedConfig config, CancellationToken stoppingToken)
    {
        var located = NvApiPanelLocator.Locate();
        if (located is null)
        {
            Log("no Aorus LCD found; waiting for config change");
            await WaitForConfigChangeAsync(stoppingToken);
            return;
        }

        using var reconfigured = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var watcher = WatchConfig(() => reconfigured.Cancel());

        using var sensors = new NvmlSensorSource(located.Value.Bus.PciBusId);
        using var busLock = new SystemBusLock();
        var panel = new PanelController(located.Value.Bus);
        int pollMs = SensorFeedTiming.PollIntervalMs(config.Elements, config.IntervalSeconds);

        Log($"feeding {config.Elements} interval {config.IntervalSeconds}s poll {pollMs}ms on {located.Value.GpuName}");
        var loop = new SensorFeedLoop(panel, sensors, () => busLock.Acquire());
        try
        {
            await loop.RunAsync(config.Elements, config.IntervalSeconds, pollMs, reconfigured.Token);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            Log("config changed; reloading");
        }
    }

    private async Task WaitForConfigChangeAsync(CancellationToken stoppingToken)
    {
        using var changed = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var watcher = WatchConfig(() => changed.Cancel());
        await DelaySafe(Timeout.InfiniteTimeSpan, changed.Token);
    }

    private static FileSystemWatcher? WatchConfig(Action onChanged)
    {
        var dir = Path.GetDirectoryName(FeedConfig.DefaultPath)!;
        Directory.CreateDirectory(dir);
        var watcher = new FileSystemWatcher(dir, Path.GetFileName(FeedConfig.DefaultPath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        watcher.Changed += (_, _) => onChanged();
        watcher.Created += (_, _) => onChanged();
        watcher.Deleted += (_, _) => onChanged();
        watcher.Renamed += (_, _) => onChanged();
        return watcher;
    }

    private static async Task DelaySafe(TimeSpan delay, CancellationToken token)
    {
        try
        {
            await Task.Delay(delay, token);
        }
        catch (OperationCanceledException)
        {
            // expected when the config changes or the service stops
        }
    }

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"{DateTime.Now:s}  {message}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // logging must never take down the service
        }
    }
}
