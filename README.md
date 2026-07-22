# AorusLcd

A lightweight Windows/.NET 10 controller for the **Gigabyte Aorus Master RTX 5090**
"LCD Edge View" panel (and, optionally, its RGB lighting) — a low-overhead
replacement for Gigabyte Control Center's LCD/RGB features.

This is a Windows port of the Linux tool
[`albancreton/aorus-master-linux`](https://github.com/albancreton/aorus-master-linux),
whose reverse-engineered `0x61` LCD protocol it reproduces byte-for-byte. It is
**alpha, community software**, tested on a single card (Aorus Master RTX 5090).
Not affiliated with or endorsed by Gigabyte or NVIDIA.

## How it works

The panel hangs off the GPU's internal I2C controller bus, behind the same
controller GCC talks to. On Windows we reach that bus through **NVAPI**
(`nvapi64.dll`): `NvAPI_I2CWriteEx` / `NvAPI_I2CReadEx` on GPU **port 1**, using
raw block writes (no register address) — the same path OpenRGB uses.

- Address `0x61` = LCD controller (all writes target this).
- Address `0x71` = RGB controller (only touched by the optional `rgb` command).
- Panel: 320x170, little-endian RGB565, row-major.
- Upload: `F2(BEGIN)` → `F1` header → 256-byte chunks → `F2(END)`, then `E5` SetMode.

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

# panel power / display mode / carousel
& $cli on
& $cli off
& $cli mode 3            # 3=image 4=text 5=gif 6=chibi
& $cli carousel 0,1,4 --arg 5
```

Experimental (semantics inferred from the decompile, not fully confirmed):
`brightness`, `poweroff-mode`, `raw "aa 01 02"`, `raw-read "eb 03" --len 8`.

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
