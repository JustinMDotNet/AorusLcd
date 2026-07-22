using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AorusLcd.Gui.ViewModels;
using AorusLcd.Gui.Views;

namespace AorusLcd.Gui;

public partial class App : Application
{
    private MainWindow? _window;
    private MainViewModel? _viewModel;
    private bool _exiting;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Keep the app alive when the window is hidden to the tray.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _viewModel = new MainViewModel();
            _window = new MainWindow { DataContext = _viewModel };
            _window.Closing += OnWindowClosing;
            desktop.MainWindow = _window;
            _window.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        // Closing the window hides it to the tray instead of quitting.
        if (!_exiting)
        {
            e.Cancel = true;
            _window?.Hide();
        }
    }

    private void OnTrayShow(object? sender, EventArgs e) => ShowWindow();

    private void OnTrayExit(object? sender, EventArgs e)
    {
        _exiting = true;
        _viewModel?.Shutdown();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private void ShowWindow()
    {
        if (_window is null)
        {
            return;
        }
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }
}
