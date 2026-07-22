using System.Runtime.Versioning;
using AorusLcd.Core.Nvapi;
using AorusLcd.Core.Rgb;

namespace AorusLcd.Cli;

/// <summary>
/// <c>rgb</c> subcommands: control the Aorus GPU RGB Fusion 2 controller
/// (0x71/0x75) — a lightweight replacement for GCC's RGB features.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class RgbCommand
{
    public static int Run(string[] args)
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
                "detect" => Detect(),
                "static" => Static(args),
                "off" => Simple(c => c.Off(), "RGB off"),
                "breathing" or "pulse" => Effect(args, RgbMode.Breathing),
                "cycle" => Effect(args, RgbMode.ColorCycle),
                "flash" => Effect(args, RgbMode.Flashing),
                "wave" => Effect(args, RgbMode.Wave),
                "-h" or "--help" or "help" => PrintUsageReturn(),
                _ => Fail($"unknown rgb command '{args[0]}'"),
            };
        }
        catch (NvApiException e)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }

    private static int Detect()
    {
        bool any = false;
        foreach (var (name, addr, present, detail) in RgbLocator.Survey())
        {
            if (present)
            {
                Console.WriteLine($"{name} @0x{addr:X2} port 1: {detail}");
            }
            any |= present;
        }
        if (!any)
        {
            Console.WriteLine("no RGB controller found (scanned 0x71/0x75 on the Aorus GPU, port 1).");
        }
        return any ? 0 : 1;
    }

    private static int Static(string[] args)
    {
        if (args.Length < 2)
        {
            return Fail("usage: rgb static <RRGGBB> [--brightness 0..100]");
        }
        var color = RgbColor.Parse(args[1]);
        byte brightness = Brightness(args);
        var (controller, name, addr) = Locate();
        controller.SetStatic(color, brightness);
        Console.WriteLine($"{name} @0x{addr:X2}: static {args[1].TrimStart('#')} brightness {brightness}");
        return 0;
    }

    private static int Effect(string[] args, RgbMode mode)
    {
        var colors = args.Skip(1)
            .Where(a => !a.StartsWith('-') && IsHexColor(a))
            .Select(RgbColor.Parse)
            .ToArray();
        if (colors.Length == 0)
        {
            colors = [new RgbColor(0xFF, 0x00, 0x00)];
        }
        byte brightness = Brightness(args);
        byte speed = Speed(args);
        var (controller, name, addr) = Locate();
        controller.SetEffect(mode, colors, speed, brightness);
        Console.WriteLine($"{name} @0x{addr:X2}: {mode} ({colors.Length} color(s)) speed {speed} brightness {brightness}");
        return 0;
    }

    private static int Simple(Action<RgbFusion2Controller> action, string message)
    {
        var (controller, name, addr) = Locate();
        action(controller);
        Console.WriteLine($"{name} @0x{addr:X2}: {message}");
        return 0;
    }

    private static (RgbFusion2Controller Controller, string Name, byte Address) Locate()
    {
        var located = RgbLocator.Locate();
        if (located is null)
        {
            throw new NvApiException("locate Aorus RGB (no ACK on 0x71/0x75, port 1)", -1);
        }
        if (located.Value.Address == 0x75)
        {
            Console.WriteLine("note: RGB controller at 0x75 is write-only (no read-back); controlling blind.");
        }
        return (located.Value.Controller, located.Value.GpuName, located.Value.Address);
    }

    private static byte Brightness(string[] args)
    {
        int pct = IntOption(args, "--brightness", 100);
        pct = Math.Clamp(pct, 0, 100);
        return (byte)(pct * RgbFusion2.BrightnessMax / 100);
    }

    private static byte Speed(string[] args)
        => (byte)Math.Clamp(IntOption(args, "--speed", RgbFusion2.SpeedNormal),
            RgbFusion2.SpeedSlowest, RgbFusion2.SpeedFastest);

    private static int IntOption(string[] args, string name, int fallback)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int v) ? v : fallback;
    }

    private static bool IsHexColor(string s)
    {
        s = s.TrimStart('#');
        return s.Length == 6 && s.All(Uri.IsHexDigit);
    }

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

    private static void PrintUsage() => Console.WriteLine(
        """
        rgb — Aorus GPU RGB (Fusion 2) control:
          rgb detect                              find the RGB controller (0x71-0x75)
          rgb static <RRGGBB> [--brightness 0..100]
          rgb breathing <RRGGBB>... [--speed 0..5] [--brightness 0..100]
          rgb cycle [--speed 0..5] [--brightness 0..100]
          rgb flash <RRGGBB>... [--speed 0..5] [--brightness 0..100]
          rgb wave [--speed 0..5] [--brightness 0..100]
          rgb off
        """);
}
