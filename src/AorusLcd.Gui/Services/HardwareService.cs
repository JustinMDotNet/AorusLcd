using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AorusLcd.Core;
using AorusLcd.Core.Nvapi;
using AorusLcd.Core.Rgb;

namespace AorusLcd.Gui.Services;

/// <summary>Raised when no supported hardware/transport is available.</summary>
public sealed class HardwareUnavailableException(string message) : Exception(message);

/// <summary>
/// Async facade over the AorusLcd.Core hardware operations for the GUI. Uses the
/// Windows NVAPI transport; on other platforms it reports that hardware control
/// is not yet available (the Linux i2c-dev backend is a planned drop-in via
/// <see cref="II2cBus"/>).
///
/// Every operation runs on a worker thread and holds the cross-process
/// <see cref="SystemBusLock"/> for its full duration, so a user command (e.g. a
/// multi-second image upload) is never interleaved with the background service's
/// sensor feed on the shared 0x61 bus. The located controllers are cached to
/// avoid re-probing each call.
/// </summary>
public sealed class HardwareService
{
    private readonly Lazy<SystemBusLock> _busLock = new(() => new SystemBusLock());
    private PanelController? _panel;
    private string _gpuName = "-";
    private RgbFusion2Controller? _rgb;
    private byte _rgbAddress;

    public bool IsSupportedPlatform => OperatingSystem.IsWindows();

    public string GpuName => _gpuName;

    // ---- LCD ---------------------------------------------------------------

    public Task<string> ConnectAsync() => WithPanelAsync(_ => _gpuName);

    public Task<LcdStatus> GetStatusAsync() => WithPanelAsync(panel => panel.GetStatus());

    public Task SendImageAsync(byte[] le565, bool clearSensors, bool save, CancellationToken ct = default)
        => SendSingleFrameAsync(le565, Panel.FramebufferStatic, Panel.ModeStatic, clearSensors, save, ct);

    public Task SendTextAsync(byte[] le565, bool clearSensors, bool save, bool rainbowEffect,
        CancellationToken ct = default)
        => SendSingleFrameAsync(le565, Panel.FramebufferText, Panel.ModeText, clearSensors, save, ct,
            applyTextEffect: rainbowEffect);

    /// <summary>Upload one still frame (image or text) to a numbered framebuffer and select its mode.</summary>
    private Task SendSingleFrameAsync(byte[] le565, uint framebuffer, int mode,
        bool clearSensors, bool save, CancellationToken ct, bool applyTextEffect = false)
        => WithPanelAsync(panel =>
        {
            var frames = ProtocolFrames.BuildUpload(ByteOps.Concat(Panel.Descriptor, le565), framebuffer);
            panel.UploadContentAsync(frames, mode, isGif: false, cancellationToken: ct)
                .GetAwaiter().GetResult();
            if (applyTextEffect)
            {
                panel.ApplyTextEffect();
            }
            if (clearSensors)
            {
                panel.SetDisplay(LcdDisplayElements.None, 0);
            }
            if (save)
            {
                panel.Save();
            }
        });

    public Task SendGifAsync(IReadOnlyList<byte[]> le565Frames, IReadOnlyList<int> delaysMs,
        bool save, CancellationToken ct = default)
        => WithPanelAsync(panel =>
        {
            var (payload, count, delayMs) = GifPayload.Build(le565Frames, delaysMs, null);
            var frames = ProtocolFrames.BuildUpload(payload, Panel.FramebufferGif,
                flag: 2, nframes: (ushort)count, delay: delayMs, mode: 2);
            panel.UploadContentAsync(frames, Panel.ModeGif, isGif: true, cancellationToken: ct)
                .GetAwaiter().GetResult();
            if (save)
            {
                panel.Save();
            }
        });

    public Task SetSensorsAsync(LcdDisplayElements elements, int intervalSeconds, bool save)
        => WithPanelAsync(panel =>
        {
            panel.SetDisplay(elements, intervalSeconds);
            if (save)
            {
                panel.Save();
            }
        });

    public Task SetCarouselAsync(IReadOnlyList<int> modes, int intervalSeconds, bool save)
        => WithPanelAsync(panel =>
        {
            panel.SetCarousel(modes, intervalSeconds);
            if (save)
            {
                panel.Save();
            }
        });

    public Task SetPanelPowerAsync(bool on) => WithPanelAsync(panel => panel.OpenLcd(on));

    public Task SetModeAsync(LcdMode mode) => WithPanelAsync(panel => panel.SetMode((int)mode));

    public Task SaveAsync() => WithPanelAsync(panel => panel.Save());

    // ---- RGB ---------------------------------------------------------------

    public Task<(string GpuName, byte Address)> ConnectRgbAsync()
        => WithRgbAsync(_ => (_gpuName, _rgbAddress));

    public Task SetRgbStaticAsync(RgbColor color, byte brightness)
        => WithRgbAsync(rgb => rgb.SetStatic(color, brightness));

    public Task SetRgbEffectAsync(RgbMode mode, RgbColor[] colors, byte speed, byte brightness)
        => WithRgbAsync(rgb => rgb.SetEffect(mode, colors, speed, brightness));

    public Task RgbOffAsync() => WithRgbAsync(rgb => rgb.Off());

    // ---- internals ---------------------------------------------------------

    private Task WithPanelAsync(Action<PanelController> action)
        => WithPanelAsync(panel => { action(panel); return true; });

    private Task<T> WithPanelAsync<T>(Func<PanelController, T> action) => Task.Run(() =>
    {
        using (AcquireBus())
        {
            try
            {
                return action(EnsurePanel());
            }
            catch (NvApiException)
            {
                _panel = null; // force re-locate next time
                throw;
            }
        }
    });

    private Task WithRgbAsync(Action<RgbFusion2Controller> action)
        => WithRgbAsync(rgb => { action(rgb); return true; });

    private Task<T> WithRgbAsync<T>(Func<RgbFusion2Controller, T> action) => Task.Run(() =>
    {
        using (AcquireBus())
        {
            try
            {
                return action(EnsureRgb());
            }
            catch (NvApiException)
            {
                _rgb = null;
                throw;
            }
        }
    });

    /// <summary>
    /// Acquire the cross-process bus lock (Windows only). The returned handle is
    /// disposed on the same worker thread that took it, honouring the mutex's
    /// thread affinity. On unsupported platforms this is a no-op; the subsequent
    /// <see cref="EnsurePanel"/>/<see cref="EnsureRgb"/> call raises the friendly
    /// <see cref="HardwareUnavailableException"/>.
    /// </summary>
    private IDisposable AcquireBus()
        => OperatingSystem.IsWindows() ? _busLock.Value.Acquire() : NoOpDisposable.Instance;

    private PanelController EnsurePanel()
    {
        RequireWindows();
        if (_panel is not null)
        {
            return _panel;
        }
        var located = NvApiPanelLocator.Locate();
        if (located is null)
        {
            throw new HardwareUnavailableException(
                "No Aorus LCD found (no GPU answered the status query at 0x61 on port 1).");
        }
        _gpuName = located.Value.GpuName;
        _panel = new PanelController(located.Value.Bus);
        return _panel;
    }

    private RgbFusion2Controller EnsureRgb()
    {
        RequireWindows();
        if (_rgb is not null)
        {
            return _rgb;
        }
        var located = RgbLocator.Locate();
        if (located is null)
        {
            throw new HardwareUnavailableException(
                "No Aorus RGB controller found (no ACK on 0x71/0x75, port 1).");
        }
        _gpuName = located.Value.GpuName;
        _rgbAddress = located.Value.Address;
        _rgb = located.Value.Controller;
        return _rgb;
    }

    private void RequireWindows()
    {
        if (!IsSupportedPlatform)
        {
            throw new HardwareUnavailableException(
                "Hardware control currently requires Windows (NVAPI). A Linux i2c-dev backend is planned.");
        }
    }
}
