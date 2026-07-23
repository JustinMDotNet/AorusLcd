using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace AorusLcd.Core;

/// <summary>Global mutex serializing GUI/service access to GPU I2C 0x61; permissive ACL lets service and user process share it.</summary>
[SupportedOSPlatform("windows")]
public sealed class SystemBusLock : IDisposable
{
    private const string MutexName = @"Global\AorusLcdBusLock";
    private readonly Mutex _mutex;

    public SystemBusLock()
    {
        var security = new MutexSecurity();
        security.AddAccessRule(new MutexAccessRule(
            new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            MutexRights.FullControl,
            AccessControlType.Allow));
        _mutex = MutexAcl.Create(initiallyOwned: false, MutexName, out _, security);
    }

    /// <summary>Acquire the bus mutex; dispose returned handle to release, with timeout for multi-second uploads.</summary>
    public IDisposable Acquire(int timeoutMs = 60000)
    {
        bool acquired;
        try
        {
            acquired = _mutex.WaitOne(timeoutMs);
        }
        catch (AbandonedMutexException)
        {
            // The previous owner crashed mid-operation; we now hold the mutex.
            acquired = true;
        }
        if (!acquired)
        {
            throw new TimeoutException("Timed out waiting for the AorusLcd bus lock.");
        }
        return new Releaser(_mutex);
    }

    public void Dispose() => _mutex.Dispose();

    private sealed class Releaser(Mutex mutex) : IDisposable
    {
        private bool _released;

        public void Dispose()
        {
            if (!_released)
            {
                _released = true;
                mutex.ReleaseMutex();
            }
        }
    }
}
