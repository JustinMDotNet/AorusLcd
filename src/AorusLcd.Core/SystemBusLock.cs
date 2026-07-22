using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace AorusLcd.Core;

/// <summary>
/// Cross-process lock serializing all access to the GPU I2C bus (address 0x61).
/// Because the background Windows service (LocalSystem) and the desktop GUI (the
/// logged-in user) both talk to the same controller, their writes must never
/// interleave — an E3 sensor frame landing in the middle of an image upload
/// would corrupt it. Both processes acquire this <c>Global\</c> named mutex
/// around every logical bus operation.
///
/// The mutex is created with a permissive ACL (Everyone: full control) so that
/// whichever process creates it first — often the service at boot, before any
/// user logs in — leaves it openable by the other.
/// </summary>
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

    /// <summary>
    /// Acquire the bus. Dispose the returned handle to release. A generous
    /// timeout accommodates multi-second uploads held by the other process.
    /// </summary>
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
