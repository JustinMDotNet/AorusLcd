using Avalonia;
using System;

namespace AorusLcd.Gui;

sealed class Program
{
    // Avoid Avalonia, third-party APIs, or SynchronizationContext-dependent code before AppMain initializes them.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
