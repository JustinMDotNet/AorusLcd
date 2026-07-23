namespace AorusLcd.Core;

/// <summary>
/// A disposable that does nothing, for the "no lock required" path (e.g. on
/// platforms without the cross-process bus lock). Shared so callers don't each
/// declare their own throwaway type.
/// </summary>
public sealed class NoOpDisposable : IDisposable
{
    public static readonly NoOpDisposable Instance = new();

    private NoOpDisposable()
    {
    }

    public void Dispose()
    {
    }
}
