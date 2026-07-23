using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AorusLcd.Core;
using AorusLcd.Core.Nvapi;
using AorusLcd.Core.Rgb;
using AorusLcd.Core.Sensors;
using AorusLcd.Gui.Models;
using AorusLcd.Gui.Services;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AorusLcd.Gui.ViewModels;

/// <summary>Main UI state for status, uploads, sensor dashboard, and RGB; service-backed sensor feed keeps running after GUI close.</summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly HardwareService _hw = new();
    private readonly ServiceControl _service = new();

    /// <summary>Set by the view to present a file picker (needs a TopLevel).</summary>
    public Func<Task<string?>>? ImagePicker { get; set; }

    public MainViewModel()
    {
        RgbModes = ["Static", "Breathing", "Color Cycle", "Flash", "Wave",
            "Gradient", "Color Shift", "Dual Flash", "Tricolor"];
        SelectedRgbMode = RgbModes[0];
        RgbBlackwellModes = ["Static", "Direct", "Pulse", "Flash", "Double Flash",
            "Color Cycle", "Wave", "Gradient", "Color Shift", "Dazzle"];
        SelectedRgbBlackwellMode = RgbBlackwellModes[0];
        StartWithWindows = StartupService.IsEnabled(); // reflect current registry state
        RefreshServiceState();
        StatusMessage = _hw.IsSupportedPlatform
            ? "Ready. Click Refresh to connect to the panel."
            : "Hardware control needs Windows (NVAPI) for now; UI is cross-platform.";
    }

    // ---- global ------------------------------------------------------------

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; set; }

    /// <summary>Inverse of <see cref="IsBusy"/>; action buttons bind their enabled state to this.</summary>
    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    public partial string GpuName { get; set; } = "-";

    [ObservableProperty]
    public partial string Firmware { get; set; } = "-";

    [ObservableProperty]
    public partial string PanelState { get; set; } = "-";

    [ObservableProperty]
    public partial string CurrentMode { get; set; } = "-";

    /// <summary>Whether the OS supports launching the app at login (Windows only for now).</summary>
    public bool StartupSupported => StartupService.IsSupported;

    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (StartupService.IsEnabled() != value)
        {
            StartupService.SetEnabled(value);
            StatusMessage = value ? "AorusLcd will start with Windows." : "Autostart disabled.";
        }
    }

    // ---- image -------------------------------------------------------------

    [ObservableProperty]
    public partial string? ImagePath { get; set; }

    [ObservableProperty]
    public partial Bitmap? PreviewImage { get; set; }

    [ObservableProperty]
    public partial bool ClearSensorsOnSend { get; set; } = true;

    [ObservableProperty]
    public partial bool SaveOnSend { get; set; }

    // ---- sensor dashboard --------------------------------------------------

    [ObservableProperty]
    public partial bool ShowGpuTemp { get; set; }

    [ObservableProperty]
    public partial bool ShowGpuClock { get; set; }

    [ObservableProperty]
    public partial bool ShowGpuUsage { get; set; }

    [ObservableProperty]
    public partial bool ShowFanSpeed { get; set; }

    [ObservableProperty]
    public partial bool ShowRamClock { get; set; }

    [ObservableProperty]
    public partial bool ShowRamUsage { get; set; }

    [ObservableProperty]
    public partial bool ShowFps { get; set; }

    [ObservableProperty]
    public partial bool ShowTgp { get; set; }

    [ObservableProperty]
    public partial int SensorInterval { get; set; } = 4;

    // ---- background service -------------------------------------------------

    [ObservableProperty]
    public partial string ServiceStatusText { get; set; } = "-";

    [ObservableProperty]
    public partial bool ServiceInstalled { get; set; }

    [ObservableProperty]
    public partial bool ServiceRunning { get; set; }

    /// <summary>True on platforms where the background service is available (Windows).</summary>
    public bool ServiceSupported => OperatingSystem.IsWindows();

    // ---- built-in modes / text / gif / carousel ----------------------------

    public ModePreset[] ModePresets { get; } =
    [
        new("Faith 1", LcdMode.Faith1),
        new("Faith 2", LcdMode.Faith2),
        new("Faith 3", LcdMode.Faith3),
        new("Chibi Clock", LcdMode.ChibTime),
    ];

    [ObservableProperty]
    public partial string TextInput { get; set; } = "AORUS";

    [ObservableProperty]
    public partial string TextColorHex { get; set; } = "FFFFFF";

    [ObservableProperty]
    public partial string TextBgHex { get; set; } = "000000";

    [ObservableProperty]
    public partial int TextSize { get; set; } = 40;

    /// <summary>Apply the panel's built-in rainbow effect to text (matches GCC's default).</summary>
    [ObservableProperty]
    public partial bool TextRainbowEffect { get; set; } = true;

    [ObservableProperty]
    public partial string? GifPath { get; set; }

    /// <summary>Set by the view to present a GIF file picker.</summary>
    public Func<Task<string?>>? GifPicker { get; set; }

    [ObservableProperty]
    public partial bool CarouselFaith1 { get; set; } = true;

    [ObservableProperty]
    public partial bool CarouselFaith2 { get; set; }

    [ObservableProperty]
    public partial bool CarouselFaith3 { get; set; }

    [ObservableProperty]
    public partial bool CarouselImage { get; set; } = true;

    [ObservableProperty]
    public partial bool CarouselChibi { get; set; }

    [ObservableProperty]
    public partial int CarouselInterval { get; set; } = 5;

    // ---- RGB ---------------------------------------------------------------

    public string[] RgbModes { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMultiColorMode))]
    public partial string SelectedRgbMode { get; set; }

    [ObservableProperty]
    public partial string RgbColorHex { get; set; } = "FF6600";

    /// <summary>Second colour, used only by the multi-colour effects (Color Shift, Tricolor).</summary>
    [ObservableProperty]
    public partial string RgbColorHex2 { get; set; } = "0066FF";

    /// <summary>Third colour, used only by the multi-colour effects (Color Shift, Tricolor).</summary>
    [ObservableProperty]
    public partial string RgbColorHex3 { get; set; } = "00FF66";

    /// <summary>True for effects that blend multiple colours, so the extra pickers show.</summary>
    public bool IsMultiColorMode => SelectedRgbMode is "Color Shift" or "Tricolor";

    [ObservableProperty]
    public partial int RgbBrightness { get; set; } = 100;

    [ObservableProperty]
    public partial int RgbSpeed { get; set; } = 2;

    // ---- RGB (Blackwell / RTX 50-series) -----------------------------------

    /// <summary>True when the connected GPU is RTX 50-series (Blackwell RGB protocol); drives which RGB tab shows.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLegacyRgb))]
    [NotifyPropertyChangedFor(nameof(ShowBlackwellRgb))]
    public partial bool IsBlackwellRgb { get; set; }

    /// <summary>Show the legacy RGB tab until a Blackwell card is detected.</summary>
    public bool ShowLegacyRgb => !IsBlackwellRgb;

    /// <summary>Show the Blackwell RGB tab for RTX 50-series cards.</summary>
    public bool ShowBlackwellRgb => IsBlackwellRgb;

    public string[] RgbBlackwellModes { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBlackwellMultiColorMode))]
    public partial string SelectedRgbBlackwellMode { get; set; }

    /// <summary>True for the Blackwell effects that take multiple colours (Color Shift, Dazzle).</summary>
    public bool IsBlackwellMultiColorMode => SelectedRgbBlackwellMode is "Color Shift" or "Dazzle";

    // ---- commands ----------------------------------------------------------

    [RelayCommand]
    private Task RefreshStatusAsync() => RunAsync("Reading panel status…", async () =>
    {
        var gpuName = await _hw.ConnectAsync();
        var status = await _hw.GetStatusAsync();
        var rgbKind = await DetectRgbKindAsync(gpuName);
        Dispatcher.UIThread.Post(() =>
        {
            GpuName = gpuName;
            IsBlackwellRgb = rgbKind == RgbControllerKind.Blackwell;
            Firmware = status.FirmwareVersion;
            PanelState = status.IsOn ? "On" : "Off";
            CurrentMode = $"{(int)status.Mode} ({status.Mode})";
            SetSensorToggles(status.DisplayElements);
            SensorInterval = status.DisplayInterval == 0 ? SensorInterval : status.DisplayInterval;
        });
        return $"Connected: {gpuName} - firmware {status.FirmwareVersion}, mode {status.Mode}.";
    });

    /// <summary>Prefer the hardware-detected RGB generation; fall back to the GPU name when RGB can't be located.</summary>
    private async Task<RgbControllerKind> DetectRgbKindAsync(string gpuName)
    {
        try
        {
            var (_, _, kind) = await _hw.ConnectRgbAsync();
            return kind;
        }
        catch (Exception)
        {
            return _hw.RgbKind ?? RgbLocator.ClassifyByName(gpuName);
        }
    }

    [RelayCommand]
    private async Task BrowseImageAsync()
    {
        if (ImagePicker is null)
        {
            return;
        }
        var path = await ImagePicker();
        if (string.IsNullOrEmpty(path))
        {
            return;
        }
        ImagePath = path;
        try
        {
            // Decode off the UI thread, size-constrained, to avoid blocking or full-resolution allocations for 320-wide preview.
            var bitmap = await Task.Run(() =>
            {
                using var stream = System.IO.File.OpenRead(path);
                return Bitmap.DecodeToWidth(stream, 640, BitmapInterpolationMode.HighQuality);
            });
            var previous = PreviewImage;
            PreviewImage = bitmap;
            previous?.Dispose();
            StatusMessage = $"Loaded {path}";
        }
        catch (Exception e)
        {
            StatusMessage = $"Could not load image: {e.Message}";
        }
    }

    [RelayCommand]
    private Task SendImageAsync()
    {
        if (string.IsNullOrEmpty(ImagePath))
        {
            StatusMessage = "Pick an image first.";
            return Task.CompletedTask;
        }
        var path = ImagePath;
        bool clear = ClearSensorsOnSend;
        bool save = SaveOnSend;
        return RunAsync("Uploading image…", async () =>
        {
            var le565 = await Task.Run(() => PanelImage.LoadLe565(path));
            await _hw.SendImageAsync(le565, clear, save);
            if (clear)
            {
                await DisableFeedConfigAsync(); // the dashboard was cleared; stop the background feed
            }
            return "Image sent" + (clear ? ", sensors off" : "") + (save ? ", saved" : "") + ".";
        });
    }

    [RelayCommand]
    private Task ApplySensorsAsync()
    {
        var elements = CollectSensorFlags();
        int interval = SensorInterval;
        return RunAsync("Applying sensor dashboard…", async () =>
        {
            if (elements == LcdDisplayElements.None)
            {
                return await DisableSensorsAsync();
            }

            // The service watches this config and drives E1/E3 so the feed continues after the GUI closes.
            await WriteFeedConfigAsync(elements, interval, enabled: true);
            string state = await EnsureServiceRunningForFeedAsync();
            return $"Sensors: {elements} - {state}";
        });
    }

    [RelayCommand]
    private Task SensorsOffAsync()
    {
        ClearAllSensorToggles();
        return RunAsync("Disabling sensor dashboard…", async () => await DisableSensorsAsync());
    }

    /// <summary>Stop feed config and clear the dashboard directly so widgets disappear immediately.</summary>
    private async Task<string> DisableSensorsAsync()
    {
        await WriteFeedConfigAsync(LcdDisplayElements.None, SensorInterval, enabled: false);
        await _hw.SetSensorsAsync(LcdDisplayElements.None, 0, SaveOnSend);
        return "Sensor dashboard off.";
    }

    [RelayCommand]
    private Task PanelOnAsync() => RunAsync("Turning panel on…", async () =>
    {
        await _hw.SetPanelPowerAsync(true);
        return "Panel on.";
    });

    [RelayCommand]
    private Task PanelOffAsync() => RunAsync("Turning panel off…", async () =>
    {
        await _hw.SetPanelPowerAsync(false);
        return "Panel off.";
    });

    [RelayCommand]
    private Task SaveToPanelAsync() => RunAsync("Saving to panel NVRAM…", async () =>
    {
        await _hw.SaveAsync();
        return "Saved to panel NVRAM (survives reboot).";
    });

    [RelayCommand]
    private Task SetModeAsync(ModePreset preset) => RunAsync($"Switching to {preset.Name}…", async () =>
    {
        await _hw.SetModeAsync(preset.Mode);
        if (SaveOnSend)
        {
            await _hw.SaveAsync();
        }
        return $"Mode: {preset.Name}.";
    });

    [RelayCommand]
    private Task SendTextAsync()
    {
        if (string.IsNullOrWhiteSpace(TextInput))
        {
            StatusMessage = "Enter some text first.";
            return Task.CompletedTask;
        }
        string text = TextInput;
        double size = TextSize;
        Color fg = SafeColor(TextColorHex, Colors.White);
        Color bg = SafeColor(TextBgHex, Colors.Black);
        bool clear = ClearSensorsOnSend;
        bool save = SaveOnSend;
        bool effect = TextRainbowEffect;
        return RunAsync("Rendering and sending text…", async () =>
        {
            var le565 = await Task.Run(() => PanelText.RenderLe565(text, size, fg, bg));
            await _hw.SendTextAsync(le565, clear, save, effect);
            if (clear)
            {
                await DisableFeedConfigAsync(); // the dashboard was cleared; stop the background feed
            }
            return $"Text \"{text}\" sent" + (effect ? ", rainbow" : "") + (save ? ", saved" : "") + ".";
        });
    }

    [RelayCommand]
    private async Task BrowseGifAsync()
    {
        if (GifPicker is null)
        {
            return;
        }
        var path = await GifPicker();
        if (!string.IsNullOrEmpty(path))
        {
            GifPath = path;
            StatusMessage = $"Selected GIF: {path}";
        }
    }

    [RelayCommand]
    private Task SendGifAsync()
    {
        if (string.IsNullOrEmpty(GifPath))
        {
            StatusMessage = "Pick a GIF first.";
            return Task.CompletedTask;
        }
        string path = GifPath;
        bool save = SaveOnSend;
        return RunAsync("Decoding and sending GIF…", async () =>
        {
            var (frames, delays) = await Task.Run(() => GifDecoder.DecodeLe565(path));
            await _hw.SendGifAsync(frames, delays, save);
            return $"GIF sent ({frames.Count} frames)" + (save ? ", saved" : "") + ".";
        });
    }

    [RelayCommand]
    private Task ApplyCarouselAsync()
    {
        var modes = new List<int>();
        if (CarouselFaith1) modes.Add((int)LcdMode.Faith1);
        if (CarouselFaith2) modes.Add((int)LcdMode.Faith2);
        if (CarouselFaith3) modes.Add((int)LcdMode.Faith3);
        if (CarouselImage) modes.Add((int)LcdMode.Image);
        if (CarouselChibi) modes.Add((int)LcdMode.ChibTime);
        if (modes.Count == 0)
        {
            StatusMessage = "Select at least one screen for the carousel.";
            return Task.CompletedTask;
        }
        int interval = CarouselInterval;
        bool save = SaveOnSend;
        return RunAsync("Applying carousel…", async () =>
        {
            await _hw.SetCarouselAsync(modes, interval, save);
            return $"Carousel: [{string.Join(",", modes)}] every {interval}s.";
        });
    }

    [RelayCommand]
    private Task ApplyRgbAsync()
    {
        if (!RgbColor.TryParse(RgbColorHex, out var color))
        {
            StatusMessage = "Invalid color - use RRGGBB hex.";
            return Task.CompletedTask;
        }
        var mode = SelectedRgbMode;
        byte brightness = (byte)(Math.Clamp(RgbBrightness, 0, 100) * RgbFusion2.BrightnessMax / 100);
        byte speed = (byte)Math.Clamp(RgbSpeed, RgbFusion2.SpeedSlowest, RgbFusion2.SpeedFastest);
        // The color-bank effects blend several colours; single-colour effects
        // just use the first. Invalid/blank extra colours are simply skipped.
        RgbColor[] colors = IsMultiColorMode ? CollectRgbColors() : [color];
        return RunAsync("Applying RGB…", async () =>
        {
            if (mode == "Static")
            {
                await _hw.SetRgbStaticAsync(color, brightness);
            }
            else
            {
                await _hw.SetRgbEffectAsync(MapRgbMode(mode), colors, speed, brightness);
            }
            string colorText = IsMultiColorMode ? $"{colors.Length} colors" : $"#{RgbColorHex}";
            return $"RGB: {mode} {colorText} brightness {RgbBrightness}%.";
        });
    }

    /// <summary>Ordered list of the valid colours for the multi-colour effects.</summary>
    private RgbColor[] CollectRgbColors()
    {
        var colors = new List<RgbColor>(3);
        foreach (var hex in new[] { RgbColorHex, RgbColorHex2, RgbColorHex3 })
        {
            if (RgbColor.TryParse(hex, out var c))
            {
                colors.Add(c);
            }
        }
        return colors.Count > 0 ? [.. colors] : [RgbColor.Black];
    }

    [RelayCommand]
    private Task RgbOffAsync() => RunAsync("Turning RGB off…", async () =>
    {
        await _hw.RgbOffAsync();
        return "RGB off.";
    });

    [RelayCommand]
    private Task ApplyRgbBlackwellAsync()
    {
        if (!RgbColor.TryParse(RgbColorHex, out var color))
        {
            StatusMessage = "Invalid color - use RRGGBB hex.";
            return Task.CompletedTask;
        }
        var modeName = SelectedRgbBlackwellMode;
        var mode = MapBlackwellMode(modeName);
        byte brightness = (byte)Math.Clamp(
            (int)Math.Round(Math.Clamp(RgbBrightness, 0, 100) / 100.0 * RgbFusion2Blackwell.BrightnessMax),
            RgbFusion2Blackwell.BrightnessMin, RgbFusion2Blackwell.BrightnessMax);
        byte speed = (byte)Math.Clamp(RgbSpeed + 1,
            RgbFusion2Blackwell.SpeedSlowest, RgbFusion2Blackwell.SpeedFastest);
        RgbColor[] colors = IsBlackwellMultiColorMode ? CollectRgbColors() : [color];
        return RunAsync("Applying RGB…", async () =>
        {
            if (mode == RgbBlackwellMode.Static)
            {
                await _hw.SetRgbBlackwellStaticAsync(color, brightness);
            }
            else
            {
                await _hw.SetRgbBlackwellEffectAsync(mode, colors, speed, brightness);
            }
            string colorText = IsBlackwellMultiColorMode ? $"{colors.Length} colors" : $"#{RgbColorHex}";
            return $"RGB: {modeName} {colorText} brightness {RgbBrightness}%.";
        });
    }

    [RelayCommand]
    private Task RgbBlackwellOffAsync() => RunAsync("Turning RGB off…", async () =>
    {
        await _hw.RgbBlackwellOffAsync();
        return "RGB off.";
    });

    // ---- background service commands ---------------------------------------

    [RelayCommand]
    private Task InstallServiceAsync() => RunAsync("Installing background service…", async () =>
    {
        await _service.InstallAsync();
        await RefreshServiceStateAfterDelayAsync();
        return "Background service installed and started.";
    });

    [RelayCommand]
    private Task UninstallServiceAsync() => RunAsync("Removing background service…", async () =>
    {
        await _service.UninstallAsync();
        await RefreshServiceStateAfterDelayAsync();
        return "Background service removed.";
    });

    [RelayCommand]
    private Task StartServiceAsync() => RunAsync("Starting background service…", async () =>
    {
        await _service.StartAsync();
        await RefreshServiceStateAfterDelayAsync();
        return "Background service started.";
    });

    [RelayCommand]
    private Task StopServiceAsync() => RunAsync("Stopping background service…", async () =>
    {
        await _service.StopAsync();
        await RefreshServiceStateAfterDelayAsync();
        return "Background service stopped.";
    });

    // ---- helpers -----------------------------------------------------------

    private Task _currentOperation = Task.CompletedTask;

    private Task RunAsync(string busyMessage, Func<Task<string>> action)
    {
        if (IsBusy)
        {
            return Task.CompletedTask;
        }
        _currentOperation = RunCoreAsync(busyMessage, action);
        return _currentOperation;
    }

    private async Task RunCoreAsync(string busyMessage, Func<Task<string>> action)
    {
        IsBusy = true;
        StatusMessage = busyMessage;
        try
        {
            StatusMessage = await action();
        }
        catch (Exception e)
        {
            StatusMessage = $"Error: {e.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private LcdDisplayElements CollectSensorFlags()
    {
        var e = LcdDisplayElements.None;
        if (ShowGpuTemp) e |= LcdDisplayElements.GpuTemp;
        if (ShowGpuClock) e |= LcdDisplayElements.GpuClock;
        if (ShowGpuUsage) e |= LcdDisplayElements.GpuUsage;
        if (ShowFanSpeed) e |= LcdDisplayElements.FanSpeed;
        if (ShowRamClock) e |= LcdDisplayElements.RamClock;
        if (ShowRamUsage) e |= LcdDisplayElements.RamUsage;
        if (ShowFps) e |= LcdDisplayElements.Fps;
        if (ShowTgp) e |= LcdDisplayElements.Tgp;
        return e;
    }

    private void SetSensorToggles(LcdDisplayElements e)
    {
        ShowGpuTemp = e.HasFlag(LcdDisplayElements.GpuTemp);
        ShowGpuClock = e.HasFlag(LcdDisplayElements.GpuClock);
        ShowGpuUsage = e.HasFlag(LcdDisplayElements.GpuUsage);
        ShowFanSpeed = e.HasFlag(LcdDisplayElements.FanSpeed);
        ShowRamClock = e.HasFlag(LcdDisplayElements.RamClock);
        ShowRamUsage = e.HasFlag(LcdDisplayElements.RamUsage);
        ShowFps = e.HasFlag(LcdDisplayElements.Fps);
        ShowTgp = e.HasFlag(LcdDisplayElements.Tgp);
    }

    private void ClearAllSensorToggles() => SetSensorToggles(LcdDisplayElements.None);

    /// <summary>Persist the dashboard config the background service consumes.</summary>
    private Task WriteFeedConfigAsync(LcdDisplayElements elements, int intervalSeconds, bool enabled)
        => new FeedConfig
        {
            DisplayElements = (uint)elements,
            IntervalSeconds = intervalSeconds,
            Enabled = enabled,
        }.SaveAsync();

    /// <summary>Flip the persisted feed config to disabled without touching the toggles.</summary>
    private async Task DisableFeedConfigAsync()
    {
        var current = await FeedConfig.LoadAsync().ConfigureAwait(false);
        await WriteFeedConfigAsync(current.Elements, current.IntervalSeconds, enabled: false).ConfigureAwait(false);
    }

    /// <summary>Ensure the service can act on the config: start it if stopped, prompt install if missing, and return status text.</summary>
    private async Task<string> EnsureServiceRunningForFeedAsync()
    {
        var state = await Task.Run(_service.GetState);
        switch (state)
        {
            case ServiceState.Running:
                RefreshServiceState();
                return "live feed running via background service.";
            case ServiceState.Stopped:
                await _service.StartAsync();
                await RefreshServiceStateAfterDelayAsync();
                return "background service started; live feed running.";
            case ServiceState.NotInstalled:
                RefreshServiceState();
                return "saved - install the background service (Device tab) to drive the panel.";
            default:
                RefreshServiceState();
                return "saved; background service is changing state.";
        }
    }

    private void RefreshServiceState()
    {
        // GetState opens a ServiceController and queries the SCM - keep it off
        // the UI thread, then marshal the result back to update bound properties.
        _ = Task.Run(() =>
        {
            var state = _service.GetState();
            Dispatcher.UIThread.Post(() => ApplyServiceState(state));
        });
    }

    private void ApplyServiceState(ServiceState state)
    {
        ServiceInstalled = state is ServiceState.Stopped or ServiceState.Running or ServiceState.Transitioning;
        ServiceRunning = state == ServiceState.Running;
        ServiceStatusText = state switch
        {
            ServiceState.Running => "Installed - running",
            ServiceState.Stopped => "Installed - stopped",
            ServiceState.Transitioning => "Installed - changing state…",
            ServiceState.NotInstalled => "Not installed",
            _ => "Not available on this platform",
        };
    }

    private async Task RefreshServiceStateAfterDelayAsync()
    {
        await Task.Delay(800).ConfigureAwait(false); // let the SCM settle after a state change
        RefreshServiceState();
    }

    /// <summary>Await in-flight hardware transfers before shutdown so uploads finish with END; leave service running afterward.</summary>
    public async Task ShutdownAsync()
    {
        try
        {
            // Bounded so a stuck operation can't block exit forever (the bus lock
            // itself times out at 60s); normal uploads finish in a few seconds.
            await _currentOperation.WaitAsync(TimeSpan.FromSeconds(75));
        }
        catch
        {
            // Timed out or the operation faulted - proceed with shutdown anyway.
        }
        Dispatcher.UIThread.Post(() => PreviewImage?.Dispose());
    }

    private static RgbMode MapRgbMode(string name) => name switch
    {
        "Breathing" => RgbMode.Breathing,
        "Color Cycle" => RgbMode.ColorCycle,
        "Flash" => RgbMode.Flashing,
        "Wave" => RgbMode.Wave,
        "Gradient" => RgbMode.Gradient,
        "Color Shift" => RgbMode.ColorShift,
        "Dual Flash" => RgbMode.DualFlashing,
        "Tricolor" => RgbMode.Tricolor,
        _ => RgbMode.Static,
    };

    private static RgbBlackwellMode MapBlackwellMode(string name) => name switch
    {
        "Direct" => RgbBlackwellMode.Direct,
        "Pulse" => RgbBlackwellMode.Breathing,
        "Flash" => RgbBlackwellMode.Flashing,
        "Double Flash" => RgbBlackwellMode.DualFlashing,
        "Color Cycle" => RgbBlackwellMode.ColorCycle,
        "Wave" => RgbBlackwellMode.Wave,
        "Gradient" => RgbBlackwellMode.Gradient,
        "Color Shift" => RgbBlackwellMode.ColorShift,
        "Dazzle" => RgbBlackwellMode.Dazzle,
        _ => RgbBlackwellMode.Static,
    };

    private static Color SafeColor(string hex, Color fallback)
        => RgbColor.TryParse(hex, out var c) ? Color.FromRgb(c.R, c.G, c.B) : fallback;
}
