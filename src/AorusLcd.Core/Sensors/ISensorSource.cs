namespace AorusLcd.Core.Sensors;

/// <summary>A source of live GPU sensor readings for the panel feed.</summary>
public interface ISensorSource : IDisposable
{
    /// <summary>Read the current GPU sensor values.</summary>
    SensorSample Read();
}
