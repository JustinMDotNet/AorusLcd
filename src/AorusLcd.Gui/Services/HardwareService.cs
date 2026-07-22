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
/// All GPU I2C access is serialized through a single async gate
/// (<see cref="SemaphoreSlim"/>) so the background sensor feed never interleaves
/// with user commands on the shared 0x61 bus — and, because the gate is async,
/// a multi-second upload can hold it across <c>await Task.Delay</c> pacing
/// without blocking any thread. The located controllers are cached to avoid
/// re-probing each call.
/// </summary>
public sealed class HardwareService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private PanelController? _panel;
    private string _gpuName = "—";
    private uint? _panelBusId;
    private RgbFusion2Controller? _rgb;
    private byte _rgbAddress;

    public bool IsSupportedPlatform => OperatingSystem.IsWindows();

    public string GpuName => _gpuName;

    /// <summary>PCI bus id of the located Aorus GPU, for matching the NVML sensor device.</summary>
    public uint? PanelBusId => _panelBusId;

    // ---- LCD ---------------------------------------------------------------

    public Task<string> ConnectAsync() => WithPanelAsync(_ => Task.FromResult(_gpuName));

    public Task<LcdStatus> GetStatusAsync() => WithPanelAsync(panel => Task.FromResult(panel.GetStatus()));

    public Task SendImageAsync(byte[] le565, bool clearSensors, bool save, CancellationToken ct = default)
        => DoAsync(async panel =>
        {
            var frames = ProtocolFrames.BuildUpload(ByteOps.Concat(Panel.Descriptor, le565), Panel.FramebufferStatic);
            await panel.UploadContentAsync(frames, Panel.ModeStatic, isGif: false, cancellationToken: ct);
            if (clearSensors)
            {
                panel.SetDisplay(LcdDisplayElements.None, 0);
            }
            if (save)
            {
                panel.Save();
            }
        }, ct);

    public Task SendTextAsync(byte[] le565, bool clearSensors, bool save, CancellationToken ct = default)
        => DoAsync(async panel =>
        {
            var frames = ProtocolFrames.BuildUpload(ByteOps.Concat(Panel.Descriptor, le565), Panel.FramebufferText);
            await panel.UploadContentAsync(frames, Panel.ModeText, isGif: false, cancellationToken: ct);
            if (clearSensors)
            {
                panel.SetDisplay(LcdDisplayElements.None, 0);
            }
            if (save)
            {
                panel.Save();
            }
        }, ct);

    public Task SendGifAsync(IReadOnlyList<byte[]> le565Frames, IReadOnlyList<int> delaysMs,
        bool save, CancellationToken ct = default)
        => DoAsync(async panel =>
        {
            var (payload, count, delayMs) = GifPayload.Build(le565Frames, delaysMs, null);
            var frames = ProtocolFrames.BuildUpload(payload, Panel.FramebufferGif,
                flag: 2, nframes: (ushort)count, delay: delayMs, mode: 2);
            await panel.UploadContentAsync(frames, Panel.ModeGif, isGif: true, cancellationToken: ct);
            if (save)
            {
                panel.Save();
            }
        }, ct);

    public Task SetSensorsAsync(LcdDisplayElements elements, int intervalSeconds, bool save)
        => DoSyncAsync(panel =>
        {
            panel.SetDisplay(elements, intervalSeconds);
            if (save)
            {
                panel.Save();
            }
        });

    public Task SetCarouselAsync(IReadOnlyList<int> modes, int intervalSeconds, bool save)
        => DoSyncAsync(panel =>
        {
            panel.SetCarousel(modes, intervalSeconds);
            if (save)
            {
                panel.Save();
            }
        });

    public Task SetPanelPowerAsync(bool on) => DoSyncAsync(panel => panel.OpenLcd(on));

    public Task SetModeAsync(LcdMode mode) => DoSyncAsync(panel => panel.SetMode((int)mode));

    public Task SaveAsync() => DoSyncAsync(panel => panel.Save());

    /// <summary>Push one live sensor frame (E3). Used by the background feed.</summary>
    public Task SendSensorFeedAsync(SensorSample sample, CancellationToken ct = default)
        => DoAsync(panel => { panel.SendSensorFeed(sample); return Task.CompletedTask; }, ct);

    // ---- RGB ---------------------------------------------------------------

    public Task<(string GpuName, byte Address)> ConnectRgbAsync()
        => WithRgbAsync(_ => Task.FromResult((_gpuName, _rgbAddress)));

    public Task SetRgbStaticAsync(RgbColor color, byte brightness)
        => DoRgbAsync(rgb => { rgb.SetStatic(color, brightness); return Task.CompletedTask; });

    public Task SetRgbEffectAsync(RgbMode mode, RgbColor[] colors, byte speed, byte brightness)
        => DoRgbAsync(rgb => { rgb.SetEffect(mode, colors, speed, brightness); return Task.CompletedTask; });

    public Task RgbOffAsync() => DoRgbAsync(rgb => { rgb.Off(); return Task.CompletedTask; });

    // ---- internals ---------------------------------------------------------

    private async Task DoAsync(Func<PanelController, Task> action, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Off-load to a worker thread: WaitAsync completes synchronously when
            // uncontended, so the blocking native I2C work must not run inline
            // on the caller (UI) thread.
            await Task.Run(() => action(EnsurePanel()), ct).ConfigureAwait(false);
        }
        catch (NvApiException)
        {
            _panel = null; // force re-locate next time
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private Task DoSyncAsync(Action<PanelController> action)
        => DoAsync(panel => { action(panel); return Task.CompletedTask; });

    private async Task<T> WithPanelAsync<T>(Func<PanelController, Task<T>> action)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(() => action(EnsurePanel())).ConfigureAwait(false);
        }
        catch (NvApiException)
        {
            _panel = null;
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task DoRgbAsync(Func<RgbFusion2Controller, Task> action)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(() => action(EnsureRgb())).ConfigureAwait(false);
        }
        catch (NvApiException)
        {
            _rgb = null;
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<T> WithRgbAsync<T>(Func<RgbFusion2Controller, Task<T>> action)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(() => action(EnsureRgb())).ConfigureAwait(false);
        }
        catch (NvApiException)
        {
            _rgb = null;
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

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
        _panelBusId = located.Value.Bus.PciBusId;
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
