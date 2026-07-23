namespace AorusLcd.Core.Sensors;

/// <summary>Reusable E1/E3 feed loop for GUI/service, acquiring the optional bus lock only around each write, never delays.</summary>
public sealed class SensorFeedLoop(
    PanelController panel, ISensorSource sensors, Func<IDisposable>? acquireBus = null)
{
    /// <summary>Configure E1 then feed E3 every <paramref name="pollIntervalMs"/> until cancelled; caller owns sensor lifetime.</summary>
    public async Task RunAsync(LcdDisplayElements elements, int intervalSeconds,
        int pollIntervalMs, CancellationToken token)
    {
        using (Enter())
        {
            panel.SetDisplay(elements, intervalSeconds);
        }

        while (!token.IsCancellationRequested)
        {
            var sample = sensors.Read(); // read outside the bus lock; only the write needs it
            using (Enter())
            {
                panel.SendSensorFeed(sample);
            }

            try
            {
                await Task.Delay(pollIntervalMs, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private IDisposable Enter() => acquireBus?.Invoke() ?? NoOpDisposable.Instance;
}
