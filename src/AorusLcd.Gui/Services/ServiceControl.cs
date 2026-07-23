using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace AorusLcd.Gui.Services;

/// <summary>Installed/running state of the background feed service.</summary>
public enum ServiceState
{
    Unsupported,
    NotInstalled,
    Stopped,
    Running,
    Transitioning,
}

/// <summary>
/// Manages the background <c>AorusLcdFeed</c> Windows service from the GUI:
/// query its state (no elevation), and install / uninstall / start / stop it
/// (each a single elevated <c>sc.exe</c> batch behind one UAC prompt). The GUI
/// itself runs unelevated; only these service operations request elevation.
///
/// The service executable is a self-contained NativeAOT build shipped next to
/// the GUI; on install it is copied to <see cref="InstalledExePath"/> under
/// ProgramData so it keeps running from a stable, machine-wide location.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ServiceControl
{
    public const string ServiceName = "AorusLcdFeed";

    /// <summary>Where the service exe is copied to and run from once installed.</summary>
    public static string InstalledExePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AorusLcd", "bin", "AorusLcd.Service.exe");

    /// <summary>Current service state (safe to call unelevated; never throws).</summary>
    public ServiceState GetState()
    {
        if (!OperatingSystem.IsWindows())
        {
            return ServiceState.Unsupported;
        }
        try
        {
            using var sc = new ServiceController(ServiceName);
            return sc.Status switch
            {
                ServiceControllerStatus.Running => ServiceState.Running,
                ServiceControllerStatus.Stopped => ServiceState.Stopped,
                _ => ServiceState.Transitioning,
            };
        }
        catch (InvalidOperationException)
        {
            return ServiceState.NotInstalled; // no such service
        }
    }

    /// <summary>
    /// Locate the bundled service exe to install from: preferring one shipped
    /// next to the GUI, then a <c>service\</c> subfolder. Returns null if the
    /// service build isn't present alongside the app.
    /// </summary>
    public static string? FindBundledServiceExe()
    {
        string baseDir = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(baseDir, "AorusLcd.Service.exe"),
            Path.Combine(baseDir, "service", "AorusLcd.Service.exe"),
        ];
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    /// <summary>
    /// Copy the bundled exe into place and register + start the service, all in
    /// one elevated batch. Throws if the bundled exe can't be found.
    /// </summary>
    public Task InstallAsync()
    {
        var source = FindBundledServiceExe()
            ?? throw new FileNotFoundException(
                "AorusLcd.Service.exe was not found next to the app. Publish the service and place it beside the GUI.");
        string dir = Path.GetDirectoryName(InstalledExePath)!;
        string dataDir = Path.GetDirectoryName(dir)!; // %ProgramData%\AorusLcd
        // Single elevated batch. Critical steps are chained with && so a failure
        // short-circuits and surfaces as a non-zero exit code (mkdir is allowed to
        // fail when the dir already exists, hence & there).
        //
        // SECURITY: the service runs as LocalSystem, so its binary under bin\ must
        // NOT be writable by standard users (that would be a privilege-escalation
        // vector). We grant the interactive Users group Modify with (OI)(NP) -
        // object-inherit, no-propagate - so it applies only to files created
        // directly in %ProgramData%\AorusLcd (i.e. feed.json, which the unelevated
        // GUI must update) and is NOT inherited into the bin\ subfolder holding the
        // service exe.
        string batch =
            $"mkdir \"{dir}\" 2>nul & copy /y \"{source}\" \"{InstalledExePath}\" && " +
            $"icacls \"{dataDir}\" /grant *S-1-5-32-545:(OI)(NP)M && " +
            $"sc create {ServiceName} binPath= \"{InstalledExePath}\" start= auto DisplayName= \"AorusLcd Sensor Feed\" && " +
            $"sc start {ServiceName}";
        return RunElevatedCmdAsync(batch);
    }

    public Task UninstallAsync()
        => RunElevatedCmdAsync($"sc stop {ServiceName} & sc delete {ServiceName}");

    public Task StartAsync() => RunElevatedCmdAsync($"sc start {ServiceName}");

    public Task StopAsync() => RunElevatedCmdAsync($"sc stop {ServiceName}");

    private static async Task RunElevatedCmdAsync(string batch)
    {
        var psi = new ProcessStartInfo("cmd.exe", $"/c {batch}")
        {
            UseShellExecute = true, // required for the runas verb (UAC elevation)
            Verb = "runas",
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to launch the elevated helper.");
        await process.WaitForExitAsync().ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"The service operation exited with code {process.ExitCode}.");
        }
    }
}
