using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AorusLcd.Core;
using AorusLcd.Core.Rgb;
using AorusLcd.Gui.Models;
using AorusLcd.Gui.Services;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AorusLcd.Gui.ViewModels;

/// <summary>
/// Main application state: panel status, image upload, the sensor dashboard,
/// and RGB lighting. Hardware calls run on background threads; UI-bound
/// properties are updated back on the UI thread.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly HardwareService _hw = new();
    private readonly SensorFeedService _feed;

    /// <summary>Set by the view to present a file picker (needs a TopLevel).</summary>
    public Func<Task<string?>>? ImagePicker { get; set; }

    public MainViewModel()
    {
        RgbModes = ["Static", "Breathing", "Color Cycle", "Flash", "Wave"];
        SelectedRgbMode = RgbModes[0];
        _feed = new SensorFeedService(_hw);
        _feed.Error += msg => Dispatcher.UIThread.Post(() => StatusMessage = $"Sensor feed: {msg}");
        StatusMessage = _hw.IsSupportedPlatform
            ? "Ready. Click Refresh to connect to the panel."
            : "Hardware control needs Windows (NVAPI) for now; UI is cross-platform.";
    }

    // ---- global ------------------------------------------------------------

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string GpuName { get; set; } = "—";

    [ObservableProperty]
    public partial string Firmware { get; set; } = "—";

    [ObservableProperty]
    public partial string PanelState { get; set; } = "—";

    [ObservableProperty]
    public partial string CurrentMode { get; set; } = "—";

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

    [ObservableProperty]
    public partial bool LiveFeedRunning { get; set; }

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
    public partial string SelectedRgbMode { get; set; }

    [ObservableProperty]
    public partial string RgbColorHex { get; set; } = "FF6600";

    [ObservableProperty]
    public partial int RgbBrightness { get; set; } = 100;

    [ObservableProperty]
    public partial int RgbSpeed { get; set; } = 2;

    public IBrush RgbPreviewBrush => SafeBrush(RgbColorHex);

    partial void OnRgbColorHexChanged(string value) => OnPropertyChanged(nameof(RgbPreviewBrush));

    // ---- commands ----------------------------------------------------------

    [RelayCommand]
    private Task RefreshStatusAsync() => RunAsync("Reading panel status…", async () =>
    {
        var gpuName = await _hw.ConnectAsync();
        var status = await _hw.GetStatusAsync();
        Dispatcher.UIThread.Post(() =>
        {
            GpuName = gpuName;
            Firmware = status.FirmwareVersion;
            PanelState = status.IsOn ? "On" : "Off";
            CurrentMode = $"{(int)status.Mode} ({status.Mode})";
            SetSensorToggles(status.DisplayElements);
            SensorInterval = status.DisplayInterval == 0 ? SensorInterval : status.DisplayInterval;
        });
        return $"Connected: {gpuName} — firmware {status.FirmwareVersion}, mode {status.Mode}.";
    });

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
            var previous = PreviewImage;
            PreviewImage = new Bitmap(path);
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
                await StopFeedAsync(); // the dashboard was cleared; stop feeding it
            }
            return "Image sent" + (clear ? ", sensors off" : "") + (save ? ", saved" : "") + ".";
        });
    }

    [RelayCommand]
    private Task ApplySensorsAsync()
    {
        var elements = CollectSensorFlags();
        int interval = SensorInterval;
        int widgetCount = CountFlags(elements);
        bool save = SaveOnSend;
        return RunAsync("Applying sensor dashboard…", async () =>
        {
            if (elements == LcdDisplayElements.None)
            {
                await StopFeedAsync();
                await _hw.SetSensorsAsync(LcdDisplayElements.None, 0, save);
                return "Sensor dashboard off.";
            }

            // Validate NVML + connection before enabling the dashboard, so we
            // never leave the panel showing widgets we can't feed.
            await _hw.ConnectAsync();
            int pollMs = StartFeed(widgetCount, interval);
            try
            {
                await _hw.SetSensorsAsync(elements, interval, save);
            }
            catch
            {
                await StopFeedAsync();
                throw;
            }
            string cadence = widgetCount <= 1
                ? $"polling {pollMs / 1000.0:0.#}s (live)"
                : $"polling {pollMs / 1000.0:0.#}s (per {interval}s rotation)";
            return $"Sensors: {elements} — live feed running, {cadence}.";
        });
    }

    [RelayCommand]
    private Task SensorsOffAsync()
    {
        ClearAllSensorToggles();
        bool save = SaveOnSend;
        return RunAsync("Disabling sensor dashboard…", async () =>
        {
            await StopFeedAsync();
            await _hw.SetSensorsAsync(LcdDisplayElements.None, 0, save);
            return "Sensor dashboard off.";
        });
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
        return RunAsync("Rendering and sending text…", async () =>
        {
            var le565 = await Task.Run(() => PanelText.RenderLe565(text, size, fg, bg));
            await _hw.SendTextAsync(le565, clear, save);
            if (clear)
            {
                await StopFeedAsync(); // the dashboard was cleared; stop feeding it
            }
            return $"Text \"{text}\" sent" + (save ? ", saved" : "") + ".";
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
            StatusMessage = "Invalid color — use RRGGBB hex.";
            return Task.CompletedTask;
        }
        var mode = SelectedRgbMode;
        byte brightness = (byte)(Math.Clamp(RgbBrightness, 0, 100) * RgbFusion2.BrightnessMax / 100);
        byte speed = (byte)Math.Clamp(RgbSpeed, RgbFusion2.SpeedSlowest, RgbFusion2.SpeedFastest);
        return RunAsync("Applying RGB…", async () =>
        {
            if (mode == "Static")
            {
                await _hw.SetRgbStaticAsync(color, brightness);
            }
            else
            {
                await _hw.SetRgbEffectAsync(MapRgbMode(mode), [color], speed, brightness);
            }
            return $"RGB: {mode} #{RgbColorHex} brightness {RgbBrightness}%.";
        });
    }

    [RelayCommand]
    private Task RgbOffAsync() => RunAsync("Turning RGB off…", async () =>
    {
        await _hw.RgbOffAsync();
        return "RGB off.";
    });

    // ---- helpers -----------------------------------------------------------

    private async Task RunAsync(string busyMessage, Func<Task<string>> action)
    {
        if (IsBusy)
        {
            return;
        }
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

    private int StartFeed(int widgetCount, int rotationIntervalSeconds)
    {
        _feed.Start(widgetCount, rotationIntervalSeconds, _hw.PanelBusId);
        Dispatcher.UIThread.Post(() => LiveFeedRunning = _feed.IsRunning);
        return _feed.PollIntervalMs;
    }

    private async Task StopFeedAsync()
    {
        await _feed.StopAsync();
        Dispatcher.UIThread.Post(() => LiveFeedRunning = _feed.IsRunning);
    }

    private static int CountFlags(LcdDisplayElements elements)
        => System.Numerics.BitOperations.PopCount((uint)elements);

    /// <summary>Stop the background feed cleanly on app shutdown.</summary>
    public async Task ShutdownAsync()
    {
        await _feed.DisposeAsync();
        Dispatcher.UIThread.Post(() => PreviewImage?.Dispose());
    }

    private static RgbMode MapRgbMode(string name) => name switch
    {
        "Breathing" => RgbMode.Breathing,
        "Color Cycle" => RgbMode.ColorCycle,
        "Flash" => RgbMode.Flashing,
        "Wave" => RgbMode.Wave,
        _ => RgbMode.Static,
    };

    private static IBrush SafeBrush(string hex)
        => RgbColor.TryParse(hex, out var c)
            ? new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B))
            : Brushes.Transparent;

    private static Color SafeColor(string hex, Color fallback)
        => RgbColor.TryParse(hex, out var c) ? Color.FromRgb(c.R, c.G, c.B) : fallback;
}
