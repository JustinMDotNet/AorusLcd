namespace AorusLcd.Core;

/// <summary>No-op disposable for paths that need no lock, shared so callers do not define throwaway types.</summary>
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
