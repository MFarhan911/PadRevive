# PadRevive 0.12.0 Beta 3

## New features

- **Revive AI drift learner** — learn a stick's rest noise and directional travel, preview the tuning, apply it, or undo the last tuning.
- **AI adviser** — optional OpenAI guidance for stick response, timing, vibration and input conflicts, using your own API key.
- **Profile insights** — local checks for mixed pointer/gamepad input, output state and vibration settings.
- **Latency monitor** — app processing and poll-spacing statistics, observed rate, error counters and a live graph.
- **Independent stick sensitivity** — 25%–200% per stick, with individual resets and saved profiles.
- **Interactive Test dock** — controller button glyphs, live press counts and visual previews.

The local tools work offline. AI advice requires an OpenAI API account and explicit summary sharing. AI advice never changes settings automatically. Timing measurements cover PadRevive's work; USB, game and display latency require separate measurement.

## Feature previews

Previews use simulated controller input.

![Revive AI](https://github.com/MFarhan911/PadRevive/releases/download/v0.12.0-beta.3/PadRevive-ReviveAI.png)
![Latency monitor](https://github.com/MFarhan911/PadRevive/releases/download/v0.12.0-beta.3/PadRevive-Latency.png)
![Interactive Test dock](https://github.com/MFarhan911/PadRevive/releases/download/v0.12.0-beta.3/PadRevive-Test.png)

## Get started

Download **PadRevive-0.12.0-beta.3-win-x64.exe**, close an older PadRevive window when convenient, and open the new app. Your saved profiles remain compatible. Find **Revive AI** in **Revive Lab**, and **Latency** in **Workbench**.

Windows x64. Runtime included. ViGEmBus is required separately for Xbox 360 / DualShock 4 virtual output. Physical rumble depends on the controller's supported motor backend. This beta executable is unsigned.

Source code and SHA-256 checksums are included with the release. PadRevive is free and open source under GPL-3.0-or-later.
