# AorusLcd

A lightweight, open-source controller for the **Gigabyte Aorus Master RTX 5090**
"LCD Edge View" panel **and its RGB Fusion 2 lighting** — a low-overhead
replacement for Gigabyte Control Center's LCD/RGB features, with a
**cross-platform GUI** and a lightweight background **Windows service**.

This started as a Windows port of the Linux tool
[`albancreton/aorus-master-linux`](https://github.com/albancreton/aorus-master-linux),
whose reverse-engineered `0x61` LCD protocol it reproduces byte-for-byte, plus
the RGB Fusion 2 GPU protocol from
[OpenRGB](https://github.com/CalcProgrammer1/OpenRGB). The full LCD command set
was recovered by decompiling Gigabyte's `ucVga.dll` (facts only — no vendor code
is shipped). It is **alpha, community software**, tested on a single card (Aorus
Master RTX 5090). Not affiliated with or endorsed by Gigabyte or NVIDIA.

## GUI

`AorusLcd.Gui` is an [Avalonia](https://avaloniaui.net) desktop app (Windows /
Linux / macOS) with three tabs — **Device**, **LCD Panel** (image, text, GIF,
built-in screens, carousel, sensor dashboard), and **RGB Lighting**. It
minimizes to the **system tray** and can **start with Windows** (launching
straight to the tray). The panel's live GPU widgets (temp, TGP, clocks…) are
driven by a lightweight background **Windows service** (`AorusLcdFeed`, a
self-contained NativeAOT ~5 MB exe) that reads GPU sensors via NVML and pushes
the feed even when the GUI is closed — the GUI just installs/manages the service
and writes the shared dashboard config it consumes. Hardware control currently
requires Windows (NVAPI); the UI and imaging are cross-platform, with a Linux
i2c-dev backend planned.

```powershell
dotnet run --project src\AorusLcd.Gui
```

### Do I need to leave it running?

No — for a static image, text, GIF, RGB color/effect, or the built-in screens,
set it once (tick **Save to panel** to persist across reboots) and quit; the
panel and RGB controller keep displaying on their own. The **only** exception is
the **live sensor dashboard**: those widgets are just numbers the panel shows, so
something must keep pushing the sensor feed (~1 Hz) or they freeze. That job
belongs to the background **`AorusLcdFeed` service**, not the GUI — install it
once from the **Device** tab and the dashboard keeps updating with the GUI
closed and no user logged in. The GUI and the service coordinate on the shared
I2C bus through a cross-process lock, so an image upload never collides with a
sensor frame.

### Building a distributable

Publish the service first so the GUI can bundle it for the in-app installer
(NativeAOT needs an MSVC toolchain / `vcvars64`):

```powershell
# NativeAOT background service (~5 MB self-contained native exe)
dotnet publish src\AorusLcd.Service -c Release -r win-x64

# GUI — self-contained single exe (~99 MB, no prerequisites);
# picks up the published service exe next to it automatically
dotnet publish src\AorusLcd.Gui -c Release -r win-x64 --self-contained ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:DebugType=none

# framework-dependent (~29 MB, needs the .NET 10 runtime)
dotnet publish src\AorusLcd.Gui -c Release -r win-x64 --no-self-contained ^
  -p:PublishSingleFile=true -p:DebugType=none
```

## How it works

The panel hangs off the GPU's internal I2C controller bus, behind the same
controller GCC talks to. On Windows we reach that bus through **NVAPI**
(`nvapi64.dll`): `NvAPI_I2CWriteEx` / `NvAPI_I2CReadEx` on GPU **port 1**, using
raw block writes (no register address) — the same path OpenRGB uses.

- Address `0x61` = LCD controller (all LCD writes target this).
- Address `0x71`/`0x75` = RGB controller. On the RTX 5090 Master it answers at
  **`0x75`** (write-only — it ACKs writes but does not reply to reads), so the
  tool auto-detects by write-ACK and never issues a read that would wedge the
  I2C engine.
- Panel: 320x170, little-endian RGB565, row-major.
- Upload: `F2(BEGIN)` → `F1` header → 256-byte chunks → `F2(END)`, then `E5` SetMode.
- RGB: 8-byte raw block packets (mode header `0x88`, color `0x40`/`0xB0`/`0xB1`,
  save `0xAA`) across 5 GPU zones.

Before any write, the tool sends the `EB 03` status query and requires a
read-back, so it never writes to a bus that does not answer.

## Requirements

- Windows with an NVIDIA driver (provides `nvapi64.dll`).
- .NET 10 SDK.
- Run from an account able to reach the GPU I2C bus (Administrator recommended).

## Build & test

```powershell
dotnet build
dotnet test                       # encoder/protocol byte-parity tests
```

## Usage

Launch the GUI and drive everything from its three tabs:

```powershell
dotnet run --project src\AorusLcd.Gui
```

- **Device** — connect (Refresh), panel on/off, save-to-NVRAM, "Start with
  Windows", and install/start/stop the background **`AorusLcdFeed`** service.
- **LCD Panel** — send a static image, rendered text, or an animated GIF; pick a
  built-in screen; configure the carousel; and choose which sensor widgets the
  dashboard shows. Tick **Save to panel** to persist across reboots.
- **RGB Lighting** — static color or an effect (breathing, color cycle, flash,
  wave) with brightness/speed.

The live sensor dashboard is pushed by the background service, so enable it once
(Device tab → Install) and the widgets keep updating with the GUI closed.

Opcodes and layouts were confirmed by decompiling Gigabyte's `ucVga.dll`
(`GvLcdApi`) — facts only, no vendor code is included or shipped:

| Opcode | Command | Notes |
| --- | --- | --- |
| `E7` | OpenLcd | byte5 = 1 on / 2 off |
| `E5` | SetMode | byte5 = mode+1; mode 7 → internal 9 |
| `E1` | SetDisplay | 8 sensor-element flags + interval byte (the dashboard overlay) |
| `EA` | SetImageTpl | overlay template: type, color, image/data positions, enable |
| `AA` | Save | persist current LCD config to panel NVRAM |
| `F3` | SetLoop | carousel: interval + (mode+1) list |
| `F2`/`F1` | Upload | begin/header for image/text/gif frames |
| `E3` | Sensor feed | GCC's service pushes live GPU stats here every second |
| `D6`/`DE`/`DF`/`F4` | Get FW / Mode / Display / Loop | read-back commands (used by `status`) |
| `EB 03` | GetImageTpl | read PET template; a valid read-back doubles as the presence probe |

The full LCD command surface (opcodes, byte layouts, mode/element enums, the
LCD-capable SSID list) was recovered from Gigabyte's `ucVga.dll` `GvLcdApi` and
reimplemented from scratch — facts only, no vendor code is included or shipped.

Display modes: `0-2` built-in stat screens, `3` image, `4` text, `5` gif,
`6` chibi, `7` carousel. The rotating TGP/temp overlay is the **E1 sensor
dashboard** (`Display` bitmask GpuTemp=1 … Tgp=0x80), independent of the
display mode — `sensors off` clears it, and `image` clears it by default
(pass `--keep-sensors` to keep it).

## Projects

| Project | Purpose |
| --- | --- |
| `src/AorusLcd.Core` | Protocol frames, RLE + GIF payload, RGB565/GDI+ content, `II2cBus`, `PanelController`, NVAPI transport, NVML sensors, shared feed config + `SystemBusLock`. |
| `src/AorusLcd.Gui` | Avalonia cross-platform GUI (Device / LCD Panel / RGB tabs, tray, service management). |
| `src/AorusLcd.Service` | NativeAOT background Windows service (`AorusLcdFeed`) that pushes the live sensor feed. |
| `tests/AorusLcd.Tests` | xUnit byte-parity tests. |

## Safety

Nothing here flashes firmware or touches persistent GPU state beyond the panel's
own image memory. The worst observed failure during development of the reference
tool was stale/black panel content until the next upload or a power cycle —
never a bricked card. Still: use at your own risk.

## Credits

- Protocol reverse engineering: `albancreton/aorus-master-linux`.
- NVAPI I2C approach and struct layout: OpenRGB (`CalcProgrammer1/OpenRGB`).
