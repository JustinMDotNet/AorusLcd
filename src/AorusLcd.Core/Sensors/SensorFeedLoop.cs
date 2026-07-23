namespace AorusLcd.Core.Sensors;

/// <summary>
/// The reusable panel sensor-feed loop: apply the dashboard element selection
/// (E1) once, then push a live E3 sensor frame at a fixed cadence until
/// cancelled. Transport-agnostic - it drives a <see cref="PanelController"/> with
/// values from an <see cref="ISensorSource"/>, so both the GUI's in-process feed
/// and the background Windows service share identical behaviour.
///
/// <paramref name="acquireBus"/> optionally serializes access to the shared I2C
/// bus with other processes (see <see cref="SystemBusLock"/>): it is acquired
/// only around each individual write, never across the inter-frame delay, so a
/// concurrent multi-second upload in the GUI is never split by a sensor frame.
/// </summary>
public sealed class SensorFeedLoop(
    PanelController panel, ISensorSource sensors, Func<IDisposable>? acquireBus = null)
{
    /// <summary>
    /// Configure the dashboard (E1) then feed E3 frames every
    /// <paramref name="pollIntervalMs"/> until <paramref name="token"/> fires.
    /// The caller owns <paramref name="sensors"/>' lifetime.
    /// </summary>
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
