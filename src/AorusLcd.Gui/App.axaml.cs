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
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplyTrayVisibility(_viewModel.ShowTrayIcon);

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
        if (_exiting)
        {
            return;
        }
        // With a tray icon, closing hides to the tray. Without one there is no way
        // to restore the window, so closing exits the GUI (the service keeps running).
        e.Cancel = true;
        if (_viewModel?.ShowTrayIcon != false)
        {
            _window?.Hide();
        }
        else
        {
            _ = ExitAsync();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ShowTrayIcon) && _viewModel is not null)
        {
            ApplyTrayVisibility(_viewModel.ShowTrayIcon);
        }
    }

    private void ApplyTrayVisibility(bool show)
    {
        var icons = TrayIcon.GetIcons(this);
        if (icons is { Count: > 0 })
        {
            icons[0].IsVisible = show;
        }
    }

    private void OnTrayShow(object? sender, EventArgs e) => ShowWindow();

    private async void OnTrayExit(object? sender, EventArgs e) => await ExitAsync();

    private async System.Threading.Tasks.Task ExitAsync()
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
