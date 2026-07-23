using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace AorusLcd.Gui.Services;

/// <summary>Per-user Windows Run autostart with <c>--minimized</c>; other platforms report unsupported for now.</summary>
public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AorusLcd";
    public const string MinimizedArg = "--minimized";

    /// <summary>Whether autostart is supported on the current OS.</summary>
    public static bool IsSupported => OperatingSystem.IsWindows();

    /// <summary>True if the app is currently registered to start with the OS.</summary>
    public static bool IsEnabled()
        => IsSupported && OperatingSystem.IsWindows() && ReadWindows() is not null;

    /// <summary>Enable or disable autostart. No-op (returns false) if unsupported.</summary>
    public static bool SetEnabled(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }
        return WriteWindows(enabled);
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadWindows()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) as string;
    }

    [SupportedOSPlatform("windows")]
    private static bool WriteWindows(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (key is null)
        {
            return false;
        }
        if (enabled)
        {
            key.SetValue(ValueName, $"\"{ExecutablePath()}\" {MinimizedArg}");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        return true;
    }

    private static string ExecutablePath()
    {
        // The real host .exe, not the managed dll (Environment.ProcessPath is the exe).
        return Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "AorusLcd.Gui.exe";
    }
}
