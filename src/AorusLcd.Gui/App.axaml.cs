using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AorusLcd.Gui.Services;
using AorusLcd.Gui.ViewModels;
using AorusLcd.Gui.Views;

namespace AorusLcd.Gui;

public partial class App : Application
{
    private MainWindow? _window;
    private MainViewModel? _viewModel;
    private bool _exiting;
    private bool _startMinimized;

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

            // For --minimized autostart, initialize MainWindow for tray Show, then hide it immediately to avoid a flash.
            _startMinimized = desktop.Args?.Contains(StartupService.MinimizedArg) == true;
            if (_startMinimized)
            {
                _window.WindowState = WindowState.Minimized;
                _window.ShowInTaskbar = false;
                _window.Opened += OnFirstOpenHideToTray;
            }
            desktop.MainWindow = _window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnFirstOpenHideToTray(object? sender, EventArgs e)
    {
        if (_window is null)
        {
            return;
        }
        _window.Opened -= OnFirstOpenHideToTray;
        _window.Hide();
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

    private async void OnTrayExit(object? sender, EventArgs e)
    {
        _exiting = true;
        if (_viewModel is not null)
        {
            await _viewModel.ShutdownAsync();
        }
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
        _window.ShowInTaskbar = true;
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }
}
