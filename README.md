# AorusLcd

A lightweight Windows/.NET 10 controller for the **Gigabyte Aorus Master RTX 5090**
"LCD Edge View" panel **and its RGB Fusion 2 lighting** — a low-overhead
replacement for Gigabyte Control Center's LCD/RGB features.

This is a Windows port of the Linux tool
[`albancreton/aorus-master-linux`](https://github.com/albancreton/aorus-master-linux),
whose reverse-engineered `0x61` LCD protocol it reproduces byte-for-byte, plus
the RGB Fusion 2 GPU protocol from
[OpenRGB](https://github.com/CalcProgrammer1/OpenRGB). It is **alpha, community
software**, tested on a single card (Aorus Master RTX 5090). Not affiliated with
or endorsed by Gigabyte or NVIDIA.

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
dotnet test                       # 11 encoder/protocol byte-parity tests
dotnet run --project src\AorusLcd.Cli -- selftest   # same checks, no hardware
```

## Usage

```powershell
$cli = "dotnet run --project src\AorusLcd.Cli --"

# find the GPU whose LCD controller answers at 0x61
& $cli probe

# show a static image (resized to 320x170)
& $cli image wallpaper.png

# render text
& $cli text "Hello" --color ff8800 --bg 000000 --size 32

# play an animated gif
& $cli gif animation.gif

# stop / configure the built-in sensor dashboard overlay (E1 SetDisplay)
& $cli sensors off                             # clean image, no TGP/temp overlay
& $cli sensors gtemp,tgp --interval 4          # show only GPU temp + TGP, rotate every 4s
& $cli sensors all                             # show every sensor widget

# panel power / display mode / carousel
& $cli on
& $cli off
& $cli mode 3            # 0-2=built-in stats 3=image 4=text 5=gif 6=chibi 7=carousel
& $cli carousel 0,1,4 --arg 5

# ---- RGB lighting (RGB Fusion 2) ----
& $cli rgb detect                              # find the RGB controller (0x71-0x75)
& $cli rgb static FF6600 --brightness 100      # solid color
& $cli rgb breathing 0000FF --speed 2          # pulsing effect
& $cli rgb cycle --speed 3                      # rainbow color cycle
& $cli rgb flash FF0000 --speed 4
& $cli rgb wave --speed 2
& $cli rgb off
```

Experimental (semantics inferred from the decompile, not fully confirmed):
`poweroff-mode`, `raw "aa 01 02"`, `raw-read "eb 03" --len 8`.

## Panel protocol notes

Opcodes and layouts were confirmed by decompiling Gigabyte's `ucVga.dll`
(`GvLcdApi`) — facts only, no vendor code is included or shipped:

| Opcode | Command | Notes |
| --- | --- | --- |
| `E7` | OpenLcd | byte5 = 1 on / 2 off |
| `E5` | SetMode | byte5 = mode+1; mode 7 → internal 9 |
| `E1` | SetDisplay | 8 sensor-element flags + interval byte (the dashboard overlay) |
| `E3` | Sensor feed | GCC's service pushes live GPU stats here every second |
| `F3` | SetLoop | carousel: interval + (mode+1) list |
| `F2`/`F1` | Upload | begin/header for image/text/gif frames |
| `EB 03` | GetData | status query used by `probe` |

Display modes: `0-2` built-in stat screens, `3` image, `4` text, `5` gif,
`6` chibi, `7` carousel. The rotating TGP/temp overlay is the **E1 sensor
dashboard** (`Display` bitmask GpuTemp=1 … Tgp=0x80), independent of the
display mode — `sensors off` clears it, and `image` clears it by default
(pass `--keep-sensors` to keep it).

## Projects

| Project | Purpose |
| --- | --- |
| `src/AorusLcd.Core` | Protocol frames, RLE + GIF payload, RGB565/GDI+ content, `II2cBus`, `PanelController`, NVAPI transport. |
| `src/AorusLcd.Cli` | Command-line front end. |
| `tests/AorusLcd.Tests` | xUnit byte-parity tests. |

## Safety

Nothing here flashes firmware or touches persistent GPU state beyond the panel's
own image memory. The worst observed failure during development of the reference
tool was stale/black panel content until the next upload or a power cycle —
never a bricked card. Still: use at your own risk.

## Credits

- Protocol reverse engineering: `albancreton/aorus-master-linux`.
- NVAPI I2C approach and struct layout: OpenRGB (`CalcProgrammer1/OpenRGB`).
