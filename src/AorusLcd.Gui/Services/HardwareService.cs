using System;
using System.Collections.Generic;
using System.Threading;
using AorusLcd.Core;
using AorusLcd.Core.Nvapi;
using AorusLcd.Core.Rgb;

namespace AorusLcd.Gui.Services;

/// <summary>Raised when no supported hardware/transport is available.</summary>
public sealed class HardwareUnavailableException(string message) : Exception(message);

/// <summary>
/// Facade over the AorusLcd.Core hardware operations for the GUI. Uses the
/// Windows NVAPI transport; on other platforms it reports that hardware control
/// is not yet available (the Linux i2c-dev backend is a planned drop-in via
/// <see cref="II2cBus"/>).
///
/// All GPU I2C access is serialized through a single gate so the background
/// sensor feed never interleaves with user commands on the shared 0x61 bus.
/// The located panel/RGB controllers are cached to avoid re-probing each call.
/// </summary>
public sealed class HardwareService
{
    private readonly Lock _gate = new();
    private PanelController? _panel;
    private string _gpuName = "—";
    private RgbFusion2Controller? _rgb;
    private byte _rgbAddress;

    public bool IsSupportedPlatform => OperatingSystem.IsWindows();

    public string GpuName => _gpuName;

    // ---- LCD ---------------------------------------------------------------

    public string Connect()
    {
        lock (_gate)
        {
            EnsurePanel();
            return _gpuName;
        }
    }

    public LcdStatus GetStatus()
    {
        lock (_gate)
        {
            return EnsurePanel().GetStatus();
        }
    }

    public void SendImage(byte[] le565, bool clearSensors, bool save) => Do(panel =>
    {
        var payload = Concat(Panel.Descriptor, le565);
        var frames = ProtocolFrames.BuildUpload(payload, Panel.FramebufferStatic);
        panel.UploadContent(frames, Panel.ModeStatic, isGif: false);
        if (clearSensors)
        {
            panel.SetDisplay(LcdDisplayElements.None, 0);
        }
        if (save)
        {
            panel.Save();
        }
    });

    public void SendText(byte[] le565, bool clearSensors, bool save) => Do(panel =>
    {
        var payload = Concat(Panel.Descriptor, le565);
        var frames = ProtocolFrames.BuildUpload(payload, Panel.FramebufferText);
        panel.UploadContent(frames, Panel.ModeText, isGif: false);
        if (clearSensors)
        {
            panel.SetDisplay(LcdDisplayElements.None, 0);
        }
        if (save)
        {
            panel.Save();
        }
    });

    public void SendGif(IReadOnlyList<byte[]> le565Frames, IReadOnlyList<int> delaysMs, bool save) => Do(panel =>
    {
        var (payload, count, delayMs) = GifPayload.Build(le565Frames, delaysMs, null);
        var frames = ProtocolFrames.BuildUpload(payload, Panel.FramebufferGif,
            flag: 2, nframes: (ushort)count, delay: delayMs, mode: 2);
        panel.UploadContent(frames, Panel.ModeGif, isGif: true);
        if (save)
        {
            panel.Save();
        }
    });

    public void SetSensors(LcdDisplayElements elements, int intervalSeconds, bool save) => Do(panel =>
    {
        panel.SetDisplay(elements, intervalSeconds);
        if (save)
        {
            panel.Save();
        }
    });

    public void SetCarousel(IReadOnlyList<int> modes, int intervalSeconds, bool save) => Do(panel =>
    {
        panel.SetCarousel(modes, intervalSeconds);
        if (save)
        {
            panel.Save();
        }
    });

    public void SetPanelPower(bool on) => Do(panel => panel.OpenLcd(on));

    public void SetMode(LcdMode mode) => Do(panel => panel.SetMode((int)mode));

    public void Save() => Do(panel => panel.Save());

    /// <summary>Push one live sensor frame (E3). Used by the background feed.</summary>
    public void SendSensorFeed(SensorSample sample) => Do(panel => panel.SendSensorFeed(sample));

    // ---- RGB ---------------------------------------------------------------

    public (string GpuName, byte Address) ConnectRgb()
    {
        lock (_gate)
        {
            EnsureRgb();
            return (_gpuName, _rgbAddress);
        }
    }

    public void SetRgbStatic(RgbColor color, byte brightness)
        => DoRgb(rgb => rgb.SetStatic(color, brightness));

    public void SetRgbEffect(RgbMode mode, RgbColor[] colors, byte speed, byte brightness)
        => DoRgb(rgb => rgb.SetEffect(mode, colors, speed, brightness));

    public void RgbOff() => DoRgb(rgb => rgb.Off());

    // ---- internals ---------------------------------------------------------

    private void Do(Action<PanelController> action)
    {
        lock (_gate)
        {
            try
            {
                action(EnsurePanel());
            }
            catch (NvApiException)
            {
                _panel = null; // force re-locate next time
                throw;
            }
        }
    }

    private void DoRgb(Action<RgbFusion2Controller> action)
    {
        lock (_gate)
        {
            try
            {
                action(EnsureRgb());
            }
            catch (NvApiException)
            {
                _rgb = null;
                throw;
            }
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

    private static byte[] Concat(byte[] a, byte[] b)
    {
        var r = new byte[a.Length + b.Length];
        a.CopyTo(r, 0);
        b.CopyTo(r, a.Length);
        return r;
    }
}
