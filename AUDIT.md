# AorusLcd — Full Repository Audit

**Date:** 2026-07-22
**Scope:** Whole repository (`src/AorusLcd.Core`, `src/AorusLcd.Gui`, `src/AorusLcd.Service`, `tests/`, CI, docs, project/solution config)
**Lenses:** correctness, concurrency, security, performance, UI/UX, .NET/Avalonia best practices, CI/CD, docs, licensing
**Reviewer:** GitHub Copilot CLI (Claude Opus 4.8), with an independent background verification pass (see §6)

---

## 1. Executive summary

This is a **high-quality, well-engineered codebase**. It is thoroughly documented,
uses modern C# idioms correctly, has meaningful byte-parity tests for the tricky
encoders/protocol, and shows careful attention to real hardware constraints
(bus pacing, a cross-process bus lock, presence-probe-before-write, size-bounded
image/GIF decoding, NativeAOT-clean interop). Baselines all pass:

| Check | Result |
| --- | --- |
| `dotnet build -c Release` | ✅ 0 warnings, 0 errors |
| `dotnet test -c Release` | ✅ 21/21 pass |
| `dotnet publish` service (NativeAOT, win-x64) | ✅ links a native exe, 0 AOT/trim warnings |
| CI action versions (checkout@v7, setup-dotnet@v6, gh-release@v3, msvc@1.13.0) | ✅ all valid/current |

Most findings are **hygiene, hardening, and polish**. There are **no Critical
issues**, but an independent verification pass surfaced **one High-severity
functional bug I initially missed**: on the project's flagship (and only tested)
card — the **Aorus RTX 5090 Master** — the RGB Fusion 2 path almost certainly sends
the **wrong I2C protocol** (see **H0**). The other high-impact gaps are non-code: a
**missing license**, an **untested release path**, and **no enforced static
analysis** — all three addressed by the PRs in §3.

> **Correction note:** my first-pass review trusted the README's "byte-exact
> OpenRGB port" claim and rated RGB as sound. A second-opinion agent flagged the
> 0x75 protocol; I verified it against **OpenRGB's current source** and confirmed
> the port is of a **superseded** protocol. This is exactly why the verification
> pass (§6) exists.

Nothing in the repository flashes firmware or touches persistent GPU state beyond
the panel's own image memory, consistent with the project's stated safety posture.

---

## 2. How findings are ranked

Each finding carries a **Severity** (impact if left unaddressed) and a **Priority**
(ROI = value ÷ effort). The table in §4 is ordered by Priority, which is what you
asked to optimize for. Items marked **PR** already have a pull request; the rest
are documented recommendations you can pick up (or ask me to implement).

---

## 3. Pull requests opened (High-priority tier)

| PR | Title | Addresses | Risk |
| --- | --- | --- | --- |
| [#4](https://github.com/JustinMDotNet/AorusLcd/pull/4) | Add MIT LICENSE (repo shipped with no license) | H1 | None (docs/metadata) |
| [#5](https://github.com/JustinMDotNet/AorusLcd/pull/5) | ci: validate NativeAOT service publish on every push/PR | H2 | None (CI only) |
| [#6](https://github.com/JustinMDotNet/AorusLcd/pull/6) | build: enable .NET analyzers + warnings-as-errors (`Directory.Build.props`) | H3 | Low (verified green: build + tests + AOT) |

Each PR is on its own branch off `main` (`audit/add-license`, `audit/ci-validate-aot-publish`,
`audit/build-analyzers`), independent and separately mergeable.

> **One decision needs your confirmation** (PR #4): I chose **MIT** to match the
> upstream `albancreton/aorus-master-linux` this project ports, with copyright
> holder `JustinMDotNet` / year `2026`. Change any of these if you prefer.

### Filed as an issue (hardware-blocked, not a blind PR)

| Issue | Title | Addresses |
| --- | --- | --- |
| [#7](https://github.com/JustinMDotNet/AorusLcd/issues/7) | RGB Fusion 2 sends the legacy 8-byte protocol to the 5090 Master's 64-byte "Blackwell" controller | H0 |

**H0 is a High**, but the fix is a hardware protocol port that must be validated on
an actual 5090 Master — shipping an untested I2C protocol as a *merged* PR would be
irresponsible. It is filed as a GitHub **issue** with a complete, code-level
remediation plan (packet layouts included). I can turn it into a draft PR on request.

---

## 4. Findings, ranked by ROI

| # | Finding | Severity | Priority | Status |
| --- | --- | --- | --- | --- |
| H0 | **RGB Fusion 2 uses the wrong protocol on the RTX 5090 Master** — sends legacy 8-byte packets to the 64-byte "Blackwell" controller at `0x75`; detection false-positives on write-ACK | **High** | ★★★★☆ | Issue (§5) |
| H1 | No `LICENSE` file — "open-source" project defaults to *all rights reserved* | **High** | ★★★★★ | PR #4 |
| H2 | CI never compiles the NativeAOT service; release path only tested at tag time | Medium | ★★★★★ | PR #5 |
| H3 | No repo-wide analyzers / warnings-as-errors; defects & regressions land silently | Medium | ★★★★☆ | PR #6 |
| M1 | No single-instance guard (autostart + manual launch → duplicate tray icons + bus contention) | Medium | ★★★☆☆ | Documented |
| M2 | Service log (`%ProgramData%\AorusLcd\service.log`) grows unbounded (no rotation) | Low–Med | ★★★☆☆ | Documented |
| M3 | `Global\AorusLcdBusLock` mutex ACL = **Everyone: FullControl** (local DoS surface) | Low–Med | ★★★☆☆ | Documented |
| M7 | Installer grants `Users:Modify` on the LocalSystem service-path root — conditional local privilege-escalation (rename-parent) | Medium | ★★★☆☆ | Documented |
| M4 | Enabling the sensor dashboard with the service not installed gives no immediate panel feedback | Low | ★★★☆☆ | Documented |
| M5 | `SendImageAsync` re-decodes the file from disk instead of reusing the already-decoded preview | Low | ★★☆☆☆ | Documented |
| M6 | `FeedWorker` TOCTOU: a config change between load and watcher-arm can be missed until the next change | Low | ★★☆☆☆ | Documented |
| L1 | Dead code from the removed CLI: `NvApiPanelLocator.Survey`, `RgbLocator.Survey`, `HardwareService.ConnectRgbAsync` | Low | ★★★☆☆ | Documented |
| L2 | `SystemBusLock` (a `Mutex`) is never disposed by `HardwareService`/`MainViewModel` | Low | ★★☆☆☆ | Documented |
| L3 | No `.gitattributes` / `.editorconfig` (CRLF↔LF normalization warnings observed on commit) | Low | ★★★☆☆ | Documented |
| L4 | Hard-coded dark palette + `RequestedThemeVariant="Dark"`; no light-theme support | Low | ★★☆☆☆ | Documented |
| L5 | `GifPayload.Build` calls `frameDelaysMs.Average()` which throws on an empty list (no guard) | Low | ★★☆☆☆ | Documented |
| L6 | RGB tab shows no connection status / which controller address was found | Low | ★★☆☆☆ | Documented |

---

## 5. Detail & recommendations

### H0 — RGB Fusion 2 sends the wrong protocol to the RTX 5090 Master *(High / [issue #7](https://github.com/JustinMDotNet/AorusLcd/issues/7))*
**This is the most important functional finding, and I missed it on the first pass.**
The README says the RGB path is a "byte-exact port of OpenRGB's `RGBFusion2GPUController`"
and that the 5090 Master's RGB controller "answers at `0x75`." That was true of an
*early* OpenRGB workaround, but **OpenRGB's current source has moved this exact card
to a different protocol.**

Verified against OpenRGB `master`
(`Controllers/GigabyteRGBFusion2BlackwellGPUController/`):

- The **Aorus RTX 5090 Master** (and 5080 Master, 5090 Master ICE, 5090 D V2, plus
  Gaming/Eagle/Aero 50-series) is registered to a dedicated
  **`GigabyteRGBFusion2BlackwellGPUController` at `0x75`** which speaks a **64-byte**
  protocol: probe = write 64 B `10 01 00…`, read 4 B, expect `01 01 01 ..`;
  set = 64 B `[type(0x12) 01 mode speed brightness R G B 00 zone numColors …colors]`;
  save = 64 B `13 01 00…`.
- The **legacy** 8-byte `GigabyteRGBFusion2GPUController` registers the 5090 Master
  only at **`0x71`** (`88`/`40`/`AB`/`AA`, 8-byte packets).

**AorusLcd** (`Rgb/RgbFusion2Controller.cs`, `Nvapi/RgbLocator.cs`) probes `0x71`
then `0x75` but drives **both** with the **legacy 8-byte** protocol. On a real 5090
Master (which answers at `0x75`), `RgbFusion2Controller.Detect()` writes an 8-byte
`AB` query and treats the mere I2C **write-ACK** as "present" — so the Blackwell
controller ACKs it, AorusLcd reports success, and then sends 8-byte `0x88`/`0x40`
commands to a controller that expects 64-byte `0x12`/`0x13` commands. **Net effect:
RGB control is almost certainly non-functional (or wrong) on the flagship card,
while the UI shows it connected.** (The LCD panel path is unaffected — this is
RGB-only.)

**Why an issue, not a merged PR:** the fix is to port OpenRGB's Blackwell 64-byte
controller (a new `RgbFusion2BlackwellController` selected when the 64-byte probe
succeeds, keeping the legacy path for older cards). That is a hardware I2C protocol
change that **must be validated on an actual 5090 Master** — merging an untested
protocol would be irresponsible. Filed with the full packet spec above; I can supply
a **draft PR** on request. *Confidence: High that the protocols differ (primary
source); High that AorusLcd's current RGB is therefore incorrect on this card,
pending a hardware confirmation only you can do.*

### M7 — Installer grants Users Modify on the LocalSystem service-path root *(Medium, security)*
`ServiceControl.InstallAsync` runs (elevated) `icacls "%ProgramData%\AorusLcd"
/grant *S-1-5-32-545:(OI)(NP)M`, so the LocalSystem service's binary
(`…\AorusLcd\bin\AorusLcd.Service.exe`) lives **under a directory tree where standard
users have Modify**. I **empirically tested** the resulting ACLs:

- ✅ `bin\` and the service `.exe` do **not** inherit the grant (`(NP)` + the folder
  pre-exists) → a standard user **cannot overwrite the binary or plant a DLL in
  `bin`**. *(This refutes the "DLL-planting in bin" escalation — the bare-name
  `nvapi64.dll`/`nvml.dll` loads are safe as long as `bin` stays non-user-writable.)*
- ⚠️ Users **do** get Modify on the `%ProgramData%\AorusLcd` folder **itself**, which
  includes DELETE of that folder; combined with "create subfolder" on `%ProgramData%`
  (Authenticated Users), a non-admin could, **while the service is stopped** (e.g. the
  boot window before auto-start), rename `AorusLcd\` and substitute their own
  `AorusLcd\bin\AorusLcd.Service.exe` → LocalSystem code execution. Conditional, but
  a real local-EoP surface.

**Recommended fix:** don't make the binary's ancestor user-writable. Put the exe in an
admin-only location (e.g. `%ProgramFiles%\AorusLcd\`), or keep it in ProgramData but
(a) grant Users write only to a dedicated **config** subdir (move `feed.json`/`service.log`
there) instead of the root, and (b) explicitly harden `bin` with
`icacls "…\bin" /inheritance:r /grant:r SYSTEM:(OI)(CI)F Administrators:(OI)(CI)F "*S-1-5-32-545:(OI)(CI)RX"`.
Changing the config path touches `FeedConfig.DefaultPath`, the GUI, the service, and
the installer, so it's documented rather than PR'd blind — happy to implement.

### H1 — Missing LICENSE *(High / PR #4)*
The README repeatedly calls the project "open-source" and "community software" and
describes it as a Windows port of the MIT-licensed
[`albancreton/aorus-master-linux`](https://github.com/albancreton/aorus-master-linux),
yet the repo shipped **no `LICENSE` file**. Without one, default copyright law makes
it *all rights reserved* — nobody may legally reuse, redistribute, or contribute,
which contradicts the entire premise.

**Fix (PR #4):** add an MIT `LICENSE` (matching upstream) plus a README *License*
section with third-party notices that make the existing "protocol facts only, no
vendor code" position explicit for the OpenRGB (GPL-2.0) constants and the
decompiled `GvLcdApi` surface. Protocol facts (register/opcode values) are generally
not copyrightable, so MIT here is defensible — **please confirm the choice.**

### H2 — Release-critical NativeAOT path is never built in CI *(Medium / PR #5)*
`ci.yml` only runs `dotnet build`/`dotnet test` (JIT). The AOT service is compiled
**only** by the tag-triggered `release.yml`. An AOT/trimming/interop regression —
e.g. in the NVAPI `Marshal.GetDelegateForFunctionPointer` delegates or the NVML
struct marshalling — would stay invisible until a release is cut and fails.
**Fix (PR #5):** an `aot-publish` CI job that compiles the service exactly like
Release (MSVC linker via `msvc-dev-cmd`). Verified locally: currently clean.

### H3 — No enforced static analysis *(Medium / PR #6)*
Each `.csproj` enabled `Nullable`/`ImplicitUsings`, but nothing turned on the .NET
analyzers or failed the build on a warning. **Fix (PR #6):** a `Directory.Build.props`
enabling `EnableNETAnalyzers` (pinned `10.0-recommended`), `TreatWarningsAsErrors`,
and `Deterministic`, with targeted suppressions (CA1822/CA1859 by design, CA1707 for
tests) and three real fixes the analyzer surfaced (CA1852 seal, CA1806 observe
`nvmlShutdown` return, CA1826 index instead of LINQ). Verified: build 0-warnings,
21/21 tests, AOT still clean.

### M1 — No single-instance guard *(Medium)*
`Program.cs`/`App` never checks for an existing instance. With "Start with Windows"
enabled, launching the app manually yields **two processes, two tray icons**, both
contending for the I2C bus lock. Recommend a named-mutex single-instance check that
signals the running instance to show its window (or simply exits) on a second launch.
*File: `src/AorusLcd.Gui/Program.cs`, `App.axaml.cs`.*

### M2 — Unbounded service log *(Low–Med)*
`FeedWorker.Log` appends to `service.log` with no size cap or rotation
(`src/AorusLcd.Service/FeedWorker.cs:158`). Normal operation logs only a few lines,
but repeated hardware-not-ready retries or feed errors accumulate forever. Recommend
size-based truncation/rotation (e.g. roll at ~1 MB, keep one `.old`).

### M3 — World-writable bus-lock mutex *(Low–Med)*
`SystemBusLock` creates the `Global\` mutex with `WorldSid: FullControl`
(`src/AorusLcd.Core/SystemBusLock.cs:29-32`). The permissive ACL is deliberate (the
LocalSystem service and the interactive user must share it), but *any* local process
can then hold it indefinitely and block all panel/RGB operations (local DoS), and
`AbandonedMutexException` is (correctly) swallowed. Low real-world risk for a niche
local tool; consider scoping the ACL to `Authenticated Users`/`Users` instead of
*Everyone* as defense-in-depth.

### M4 — Sensor dashboard has no immediate feedback without the service *(Low, UX)*
`ApplySensorsAsync` only writes the shared `FeedConfig` and asks the background
service to drive the panel; if the service isn't installed, checking widgets +
"Apply sensors" changes nothing visible (the status line explains why, but the
panel stays blank). By contrast, *disabling* sensors pushes to the panel directly.
Consider a one-shot `E1`+`E3` push on apply for instant feedback, or gating the
"Apply sensors" button on service-installed and nudging the user to the Device tab.
*File: `src/AorusLcd.Gui/ViewModels/MainViewModel.cs`.*

### M5 — Image decoded twice *(Low, perf)*
`BrowseImageAsync` decodes the file for the preview; `SendImageAsync` then calls
`PanelImage.LoadLe565(path)`, which **re-opens and re-decodes the same file**. For
the common "browse then send" flow this is redundant I/O + decode. Minor; the send
path is already off the UI thread. Could reuse the decoded preview bitmap via
`PanelImage.ToLe565(bitmap)`. *File: `src/AorusLcd.Gui/ViewModels/MainViewModel.cs`.*

### M6 — FeedWorker config-change race *(Low)*
In `FeedWorker.ExecuteAsync`, when the config is disabled it calls
`WaitForConfigChangeAsync`, which reads the config, then creates the
`FileSystemWatcher` and waits. A config write landing in the tiny window *between*
the read and `EnableRaisingEvents` would be missed, leaving the service asleep until
the next change. Recommend re-loading the config once immediately after arming the
watcher. *File: `src/AorusLcd.Service/FeedWorker.cs`.*

### L1 — Dead code from the removed CLI *(Low)*
`NvApiPanelLocator.Survey`, `RgbLocator.Survey`, and `HardwareService.ConnectRgbAsync`
have no callers (leftovers from the `probe`/`status` CLI that was removed). Safe to
delete for clarity. *Note:* `GetLoop`, `SetImageTemplate`, and `PowerOffMode` are
also currently unused **but are intentional** — the README states the goal is a
complete `GvLcdApi` surface — so leave those (they're test-covered).

### L2 — Bus lock never disposed *(Low)*
`HardwareService` holds `Lazy<SystemBusLock>` (a `Mutex`) and neither it nor
`MainViewModel` disposes it; the OS reclaims it at process exit, so impact is
negligible, but implementing `IDisposable` on `HardwareService` and disposing in
`MainViewModel.ShutdownAsync` would be tidier.

### L3 — No `.gitattributes` / `.editorconfig` *(Low)*
Commits emit `CRLF will be replaced by LF` warnings — line endings aren't normalized.
Add a `.gitattributes` (`* text=auto eol=crlf` for a Windows-first repo, or `eol=lf`)
and an `.editorconfig` to lock in the (already consistent) style across contributors.

### L4 — Dark theme only *(Low)*
`App.axaml` forces `RequestedThemeVariant="Dark"` and `MainWindow.axaml` hard-codes
card/background colors (`#1E1E24`, `#15151A`, …). A light-theme user gets an
always-dark window. Consider theme-resource brushes if light mode matters.

### L5 — `Average()` on possibly-empty list *(Low)*
`GifPayload.Build` computes `frameDelaysMs.Average()`; an empty list throws
`InvalidOperationException`. The only caller (`GifDecoder`) always supplies ≥1 entry,
so it's unreachable today — but a one-line guard (default to 100 ms) hardens the
public API. *File: `src/AorusLcd.Core/GifPayload.cs`.*

### L6 — RGB tab lacks connection status *(Low, UX)*
The Device tab shows GPU/firmware/mode, but the RGB tab never surfaces which
controller/address was detected (0x71 vs 0x75) or whether RGB is reachable —
`ConnectRgbAsync` exists but isn't wired up. A small "RGB: <gpu> @ 0x75" line would
help users confirm hardware before applying effects.

---

## 6. Independent verification pass

The trickiest correctness/concurrency/security claims were re-checked by reading each
full code path end-to-end and by running the actual toolchain (build, tests, and a
real NativeAOT publish). Verdicts:

| Claim | Verdict | Evidence |
| --- | --- | --- |
| **Mutex thread-affinity** — is `ReleaseMutex` always on the acquiring thread? | ✅ Correct (no bug) | Every acquire/release pair sits inside one synchronous block: `HardwareService.With*Async` run the whole `using(AcquireBus()) {…}` inside a single `Task.Run` lambda; `SensorFeedLoop.Enter()` and `FeedWorker` acquire+dispose with **no `await` in between**. `.NET` mutex affinity is honored. |
| **Elevated install → privilege escalation?** — can a standard user overwrite the LocalSystem service exe? | ⚠️ Partly (see M7) | **Empirically tested:** the `(OI)(NP)M` grant does **not** reach `bin\` or the exe (users can't overwrite the binary or plant a DLL there ✅), but users **do** get Modify on the `%ProgramData%\AorusLcd` **root**, enabling a conditional rename-the-parent EoP while the service is stopped. Quoted paths from `AppContext.BaseDirectory` + fixed ProgramData ⇒ low cmd-injection risk. |
| **NVAPI struct + NativeAOT interop** | ✅ Healthy | `dotnet publish -c Release -r win-x64` reached "Generating native code" and linked a native exe with **0 AOT/trim warnings**, even under `TreatWarningsAsErrors`. `NV_I2C_INFO_V3` sequential layout + `NV_STRUCT_VERSION` match the native x64 ABI (8-aligned pointers; `byte portId` + `uint bIsPortIdSet` padding matches). |
| **RGB zone math / OOB** | ✅ No OOB | `_zoneColor[4]` writes are guarded by `if (zone < 4)`; zone 4 falls through to `config.Colors[0]`; `bank = 0xB0 + zone*4` matches the *legacy* OpenRGB layout. *(But the legacy protocol itself is the wrong one for this card — see H0.)* |
| **RGB protocol vs OpenRGB (H0)** | ❌ **Wrong protocol (High)** | **Verified against OpenRGB `master`:** the Aorus 5090 Master is registered to the 64-byte `GigabyteRGBFusion2BlackwellGPUController` at `0x75`; AorusLcd sends the legacy 8-byte protocol there and detects via write-ACK only. My first-pass "faithful port = fine" was **wrong**; the second-opinion agent was right. |
| **FeedWorker config race (M6)** | ⚠️ Confirmed (Low) | Real TOCTOU between `LoadAsync` and `EnableRaisingEvents`; low probability, self-heals on the next change. Recommend a re-load after arming the watcher. |
| **Atomic config write / concurrent `.tmp`** | ⚠️ Minor | `SaveAsync` is atomic per writer, but two GUI instances would share one `…feed.json.tmp` name — a theoretical collision whose real root cause is the missing single-instance guard (**M1**). |
| **GIF decode O(n²)** | ✅ Acceptable | Only non-sequential-dependency frames trigger a full re-decode; bounded by `MaxFrames=256`, so worst case is small and the common case is O(n). |

**Missed High/Critical bugs:** **one — H0** (wrong RGB protocol on the 5090 Master),
found by the verification pass and confirmed against OpenRGB's current source. No
Critical issues. The LCD panel path (the project's primary feature) has no confirmed
High-severity bug.

_Note: a separate automated reviewer agent was dispatched as a second opinion. It ran
long, but on being asked to wrap up it **did** surface the RGB-protocol issue (H0) and
the install-ACL concern (M7). I did **not** take either at face value — I confirmed H0
against OpenRGB's live source and empirically tested the ACL semantics, which upgraded
H0 to a real High and right-sized the ACL claim from "High DLL-planting" down to a
conditional Medium (M7)._

---

## 7. What was checked and found solid (positives)

- **Concurrency:** the bus-lock mutex is acquired and released on the same thread in
  every path (`Task.Run` lambdas, the synchronous feed loop) — no cross-thread
  `ReleaseMutex`. `RunAsync`/`IsBusy` gating avoids overlapping hardware ops.
- **Safety:** presence probe (`EB 03`) before any LCD write; locators gate to the
  verified Aorus GPU. *(Caveat: the RGB detection's write-ACK-only check is what
  false-positives on the 5090's Blackwell controller — see H0.)*
- **Atomic config:** `FeedConfig.SaveAsync` writes a temp file then `File.Move(overwrite)`
  so the service's watcher never sees a half-written file.
- **Performance:** images/GIFs are decoded size-constrained (never full-res for a
  320×170 panel); GIF frames are bounded (`MaxFrames=256`) and composited to avoid
  O(n²) re-decode; upload chunking avoids a full combined buffer via `CopyLogical`.
- **Interop/AOT:** the NativeAOT service compiles clean; the `NV_I2C_INFO_V3` managed
  layout and `NV_STRUCT_VERSION` match the native ABI.
- **Tests:** byte-parity coverage of RLE, GIF table offsets, RGB565, frame builders,
  and the `E1`/`E3`/`EA` command encoders.
- **Docs:** `README.md` and `docs/PROTOCOL.md` are unusually thorough and honest about
  what's confirmed vs experimental.

---

## 8. Suggested follow-up order

1. **H0 (RGB Blackwell protocol)** — highest functional impact; port the 64-byte
   controller and validate on your 5090 Master. Ask me for the draft PR.
2. Merge **#4 (LICENSE)** — confirm license choice first.
3. Merge **#5 (CI AOT)** and **#6 (analyzers)** — both verified green.
4. Security hardening: **M7** service-path ACL, **M3** bus-lock mutex ACL.
5. Quick wins: **M1** single-instance, **L3** `.gitattributes`/`.editorconfig`,
   **L1** dead-code removal, **L5** `Average()` guard.
6. Then **M2** log rotation, **M6** watcher re-check, **M4/L6** RGB/sensor UX polish.

_I can open PRs (or a draft PR for H0) for any of these on request._
