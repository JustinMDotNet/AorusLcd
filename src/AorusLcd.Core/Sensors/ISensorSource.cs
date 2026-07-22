namespace AorusLcd.Core.Sensors;

/// <summary>A source of live GPU sensor readings for the panel feed.</summary>
public interface ISensorSource : IDisposable
{
    /// <summary>Read the current GPU sensor values for the given GPU index.</summary>
    SensorSample Read(int gpuIndex = 0);
}
