using System.Runtime.Versioning;
using AorusLcd.Core;
using AorusLcd.Core.Nvapi;
using AorusLcd.Core.Rgb;

namespace AorusLcd.Cli;

/// <summary>
/// Command-line front end mirroring the Linux <c>aorus_lcd.py</c> subcommands,
/// but driving the panel over NVAPI on Windows.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 2;
        }

        try
        {
            return args[0] switch
            {
                "selftest" => SelfTest.Run() ? 1 : 0,
                "probe" => CmdProbe(),
                "on" => WithPanel(p => p.OpenLcd(true), "panel ON (E7 01)"),
                "off" => WithPanel(p => p.OpenLcd(false), "panel OFF (E7 02)"),
                "mode" => CmdMode(args),
                "sensors" => CmdSensors(args),
                "status" => CmdStatus(),
                "save" => WithPanel(p => p.Save(), "saved LCD config to panel NVRAM (AA)"),
                "image" => await CmdImageAsync(args),
                "text" => await CmdTextAsync(args),
                "gif" => await CmdGifAsync(args),
                "carousel" => CmdCarousel(args),
                "brightness" => Fail("'brightness' was removed: 0xE1 is the sensor-dashboard command, not brightness. Use 'sensors' instead."),
                "poweroff-mode" => WithPanel(p => p.PowerOffMode(), "SetPCPowerOffMode (FA)", "poweroff-mode"),
                "raw" => CmdRaw(args),
                "raw-read" => CmdRawRead(args),
                "rgb" => RgbCommand.Run(args[1..]),
                "-h" or "--help" or "help" => PrintUsageReturn(),
                _ => Fail($"unknown command '{args[0]}'"),
            };
        }
        catch (NvApiException e)
        {
            return Fail(e.Message);
        }
        catch (DllNotFoundException)
        {
            return Fail("NVAPI (nvapi64.dll) not found. Install the NVIDIA driver and run on a machine with an NVIDIA GPU.");
        }
        catch (FileNotFoundException e)
        {
            return Fail(e.Message);
        }
        catch (FormatException e)
        {
            return Fail(e.Message);
        }
    }

    private static PanelController OpenPanel()
    {
        var located = NvApiPanelLocator.Locate();
        if (located is null)
        {
            throw new NvApiException("locate Aorus LCD (no GPU answered EB 03 at 0x61 on port 1)", -1);
        }
        Console.WriteLine($"using {located.Value.GpuName} (NVAPI internal bus, port 1)");
        return new PanelController(located.Value.Bus);
    }

    private static int WithPanel(Action<PanelController> action, string message, string? experimental = null)
    {
        if (experimental is not null)
        {
            Experimental(experimental);
        }
        var panel = OpenPanel();
        action(panel);
        Console.WriteLine(message);
        return 0;
    }

    private static int CmdProbe()
    {
        bool any = false;
        foreach (var (name, responds, detail) in NvApiPanelLocator.Survey())
        {
            Console.WriteLine($"{name} @0x61 port 1: {detail}");
            any |= responds;
        }
        if (!any)
        {
            Console.WriteLine("no GPU answered the LCD controller at 0x61 on port 1.");
        }
        return any ? 0 : 1;
    }

    private static int CmdMode(string[] args)
    {
        int mode = RequireInt(args, 1, "mode");
        if (mode is < 0 or > 7)
        {
            return Fail("mode must be 0..7");
        }
        return WithPanel(p => p.SetMode(mode), $"SetMode {mode}");
    }

    private static async Task<int> CmdImageAsync(string[] args)
    {
        string file = RequireArg(args, 1, "image <file>");
        var opts = UploadOptions.Parse(args);
        bool keepSensors = HasFlag(args, "--keep-sensors");
        var pixels = ImageContent.LoadImageLe565(file);
        var payload = ByteOps.Concat(Panel.Descriptor, pixels);
        var frames = ProtocolFrames.BuildUpload(payload, Panel.FramebufferStatic);
        Console.WriteLine($"uploading {file} ({frames.Count} i2c writes) ...");
        var panel = OpenPanel();
        await panel.UploadContentAsync(frames, Panel.ModeStatic, isGif: false,
            setDisplayMode: !opts.NoMode, chunkDelayMs: opts.ChunkDelayMs);
        if (!opts.NoMode && !keepSensors)
        {
            panel.SetDisplay(LcdDisplayElements.None, 0); // clear the sensor dashboard overlay
        }
        if (opts.Save)
        {
            panel.Save();
        }
        Console.WriteLine("done" + (opts.NoMode ? "" : " (SetMode 3, sensors " + (keepSensors ? "kept" : "off") + ")") + (opts.Save ? " (saved)" : ""));
        return 0;
    }

    private static async Task<int> CmdTextAsync(string[] args)
    {
        string text = RequireArg(args, 1, "text <message>");
        var opts = UploadOptions.Parse(args);
        int size = GetIntOption(args, "--size", 28);
        var fg = ParseColor(GetOption(args, "--color", "8b8d8b"));
        var bg = ParseColor(GetOption(args, "--bg", "000000"));
        bool keepSensors = HasFlag(args, "--keep-sensors");

        var pixels = ImageContent.RenderTextLe565(text, size, fg, bg);
        var payload = ByteOps.Concat(Panel.Descriptor, pixels);
        var frames = ProtocolFrames.BuildUpload(payload, Panel.FramebufferText);
        Console.WriteLine($"uploading text \"{text}\" ({frames.Count} i2c writes) ...");
        var panel = OpenPanel();
        await panel.UploadContentAsync(frames, Panel.ModeText, isGif: false,
            setDisplayMode: !opts.NoMode, chunkDelayMs: opts.ChunkDelayMs);
        if (!opts.NoMode && !keepSensors)
        {
            panel.SetDisplay(LcdDisplayElements.None, 0);
        }
        if (opts.Save)
        {
            panel.Save();
        }
        Console.WriteLine("done" + (opts.Save ? " (saved)" : ""));
        return 0;
    }

    private static async Task<int> CmdGifAsync(string[] args)
    {
        string file = RequireArg(args, 1, "gif <file>");
        var opts = UploadOptions.Parse(args);
        int? frameDelay = TryGetIntOption(args, "--frame-delay");

        var (le565Frames, delays) = ImageContent.GifToLe565Frames(file);
        var (payload, count, delayMs) = GifPayload.Build(le565Frames, delays, frameDelay);
        var frames = ProtocolFrames.BuildUpload(payload, Panel.FramebufferGif,
            flag: 2, nframes: (ushort)count, delay: delayMs, mode: 2);
        Console.WriteLine($"uploading {file}: {count} frames, {frames.Count} i2c writes ...");
        var panel = OpenPanel();
        await panel.UploadContentAsync(frames, Panel.ModeGif, isGif: true,
            setDisplayMode: !opts.NoMode, chunkDelayMs: opts.ChunkDelayMs);
        if (opts.Save)
        {
            panel.Save();
        }
        Console.WriteLine("done" + (opts.Save ? " (saved)" : ""));
        return 0;
    }

    private static int CmdStatus()
    {
        var panel = OpenPanel();
        var s = panel.GetStatus();
        Console.WriteLine($"firmware:  {s.FirmwareVersion}");
        Console.WriteLine($"panel:     {(s.IsOn ? "on" : "off")}");
        Console.WriteLine($"mode:      {(int)s.Mode} ({s.Mode})");
        Console.WriteLine($"sensors:   {s.DisplayElements} (interval {s.DisplayInterval})");
        Console.WriteLine($"carousel:  [{string.Join(",", s.CarouselModes)}] (interval {s.CarouselInterval})");
        return 0;
    }

    private static int CmdCarousel(string[] args)
    {
        string list = RequireArg(args, 1, "carousel <modes>");
        var modes = list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse).ToList();
        var bad = modes.Where(m => m is < 0 or > 6).ToList();
        if (bad.Count > 0)
        {
            return Fail($"carousel modes must be 0..6, got {string.Join(",", bad)}");
        }
        int arg = GetIntOption(args, "--arg", 0);
        return WithPanel(p => p.SetCarousel(modes, arg), $"carousel [{string.Join(",", modes)}] arg={arg} (F3)");
    }

    private static int CmdSensors(string[] args)
    {
        if (args.Length < 2)
        {
            return Fail("usage: sensors off | sensors <gtemp,gclock,gusage,fan,rclock,rusage,fps,tgp> [--interval N]");
        }
        var (elements, interval) = ParseSensors(args);
        return WithPanel(p => p.SetDisplay(elements, interval),
            $"SetDisplay {elements} interval {interval} (E1)");
    }

    private static (LcdDisplayElements Elements, int Interval) ParseSensors(string[] args)
    {
        int interval = GetIntOption(args, "--interval", 4);
        string spec = args[1];
        if (spec is "off" or "none")
        {
            return (LcdDisplayElements.None, 0);
        }
        if (spec is "all")
        {
            return (LcdDisplayElements.All, interval);
        }
        var elements = LcdDisplayElements.None;
        foreach (var token in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            elements |= token.ToLowerInvariant() switch
            {
                "gtemp" or "gputemp" or "temp" => LcdDisplayElements.GpuTemp,
                "gclock" or "gpuclock" => LcdDisplayElements.GpuClock,
                "gusage" or "gpuusage" or "usage" => LcdDisplayElements.GpuUsage,
                "fan" or "fanspeed" => LcdDisplayElements.FanSpeed,
                "rclock" or "ramclock" => LcdDisplayElements.RamClock,
                "rusage" or "ramusage" => LcdDisplayElements.RamUsage,
                "fps" => LcdDisplayElements.Fps,
                "tgp" => LcdDisplayElements.Tgp,
                _ => throw new FileNotFoundException($"unknown sensor '{token}'"),
            };
        }
        return (elements, interval);
    }

    private static int CmdRaw(string[] args)
    {
        Experimental("raw");
        var b = ParseHexBytes(RequireArg(args, 1, "raw <hex bytes>"));
        return WithPanel(p => p.WriteFrame(ProtocolFrames.CmdFrame(b[0], b[1..])),
            $"sent 0x{b[0]:x2} params {(b.Length > 1 ? Convert.ToHexString(b[1..]) : "(none)")}");
    }

    private static int CmdRawRead(string[] args)
    {
        Experimental("raw-read");
        var b = ParseHexBytes(RequireArg(args, 1, "raw-read <hex bytes>"));
        int len = GetIntOption(args, "--len", 8);
        var panel = OpenPanel();
        var r = panel.ReadCommand(b[0], b[1..], len);
        Console.WriteLine($"read 0x{b[0]:x2} {Convert.ToHexString(b[1..])} -> {Convert.ToHexString(r)}");
        return 0;
    }

    // ---- argument helpers --------------------------------------------------

    private static string RequireArg(string[] args, int index, string usage)
    {
        if (index >= args.Length || args[index].StartsWith("--"))
        {
            throw new FileNotFoundException($"usage: {usage}");
        }
        return args[index];
    }

    private static int RequireInt(string[] args, int index, string name)
    {
        if (index >= args.Length || !int.TryParse(args[index], out int v))
        {
            throw new FileNotFoundException($"{name}: expected an integer argument");
        }
        return v;
    }

    private static bool HasFlag(string[] args, string flag) => Array.IndexOf(args, flag) >= 0;

    private static string? GetOption(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static string GetOption(string[] args, string name, string fallback)
        => GetOption(args, name) ?? fallback;

    private static int GetIntOption(string[] args, string name, int fallback)
        => TryGetIntOption(args, name) ?? fallback;

    private static int? TryGetIntOption(string[] args, string name)
        => int.TryParse(GetOption(args, name), out int v) ? v : null;

    private static (byte R, byte G, byte B) ParseColor(string s)
    {
        var c = RgbColor.Parse(s);
        return (c.R, c.G, c.B);
    }

    private static byte[] ParseHexBytes(string s)
        => s.Replace(",", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => Convert.ToByte(x, 16)).ToArray();

    private static void Experimental(string what)
        => Console.WriteLine($"note: '{what}' is EXPERIMENTAL — semantics inferred from the decompile, not fully hardware-confirmed.");

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    private static int PrintUsageReturn()
    {
        PrintUsage();
        return 0;
    }

    private readonly record struct UploadOptions(bool NoMode, int ChunkDelayMs, bool Save)
    {
        public static UploadOptions Parse(string[] args)
        {
            bool noMode = Array.IndexOf(args, "--no-mode") >= 0;
            bool save = Array.IndexOf(args, "--save") >= 0;
            int i = Array.IndexOf(args, "--chunk-delay");
            int delay = i >= 0 && i + 1 < args.Length && double.TryParse(args[i + 1], out double sec)
                ? (int)Math.Round(sec * 1000)
                : PanelController.DefaultChunkDelayMs;
            return new UploadOptions(noMode, delay, save);
        }
    }

    private static void PrintUsage() => Console.WriteLine(
        """
        AorusLcd — control the Aorus Master RTX 5090 LCD Edge View + RGB (no GCC).

        LCD commands:
          probe                       find the GPU whose LCD controller answers at 0x61
          status                      read firmware, mode, sensors and carousel state
          on | off                    turn the panel on/off
          mode <0..7>                 0-2=stats 3=image 4=text 5=gif 6=chibi 7=carousel
          image <file> [--keep-sensors] [--save] [--no-mode] [--chunk-delay SEC]
          text <msg> [--size N] [--color RRGGBB] [--bg RRGGBB] [--keep-sensors] [--save] [--no-mode]
          gif <file> [--frame-delay MS] [--save] [--no-mode]
          carousel <m,m,..> [--arg N]
          sensors off | sensors <gtemp,gclock,gusage,fan,rclock,rusage,fps,tgp> [--interval N]
          save                        persist current LCD config to panel NVRAM (AA)
          poweroff-mode               [experimental]
          raw "aa 01 02"              [experimental] send a raw command frame
          raw-read "eb 03" [--len N]  [experimental] send a frame then read back
          selftest                    run encoder self-checks (no hardware)

        RGB commands:
          rgb ...                     run 'rgb' with no args for RGB help
        """);
}
