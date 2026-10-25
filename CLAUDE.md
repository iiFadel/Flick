# CLAUDE.md

**Flick**: personal fork of Filee (https://github.com/KnifeLemon/Filee, MIT), reworked to match the interaction and look of Tangerine for Mac. Personal use only. Read `PLAN.md` for the phase we are in before starting any task.

## Read first

- `docs/ARCHITECTURE.md`: trigger, gesture, donut, conversion pipeline, platform split
- `docs/ADDING-A-CONVERTER.md`: the only correct way to add a conversion
- `docs/TANGERINE-SPEC.md`: target behaviour and wheel contents (source of truth for "what should it do")

## Stack

- .NET 10, C#, Avalonia UI. Solution: `Filee.slnx`
- Run: `dotnet run --project src/Filee.App`
- Test: `dotnet test -m:1` (must pass before every commit; one project at a time, parallel projects crash the App test host from memory pressure)
- Optional engines: `pwsh build/fetch-engines.ps1 -Only rhwp,pandoc` (FFmpeg is needed for video/audio work; install it from Settings → Engines)

## Map of the code

- `Filee.App`: `TriggerService` (SharpHook global input) → `GestureDetector` (pure state machine) → `RadialController` → `RadialWindow` / `Controls/DonutMenu.cs` (custom-drawn ring) → `ConversionService` → `ToastWindow`
- `Filee.Core`: `JobQueue`, `RoutePlanner` (Dijkstra over converter edges, max 3 hops), `FormatRegistry`, `Preset`, `ToolbarProfile`, `ProfileSelector`, `OutputPathResolver`, `IPlatformServices`
- `Filee.Engines`: all `IConverter` implementations, registered in `EngineRegistry.CreateAll`
- `Filee.Platform.Windows`: Windows-only code. All OS calls go through `IPlatformServices`
- `DonutGeometry.cs`: pure slice math, unit tested

## Rules

- Never modify or delete source files. Outputs only via `step.Output.Allocate(...)`. Scratch files in `step.WorkDirectory`.
- External programs only through `ProcessRunner.RunAsync` and `EngineEnvironment.FindBundled(...)`. Never call anything from PATH.
- No OS-specific code outside `Filee.Platform.*`. Add a method to `IPlatformServices` instead.
- Keep pure logic (gesture state machine, wheel selection, geometry) free of Avalonia types so it can be unit tested. Every change to `GestureDetector` or `DonutGeometry` needs tests.
- Prefer new files over large edits to upstream files, so `git pull upstream main` stays mergeable. When an upstream file must change, keep the diff small and mark it with `// fork:`.
- The app is called Flick in everything the user sees (window titles, tray, toasts, settings, i18n strings). Do NOT rename namespaces, projects, folders or `Filee.*` identifiers: that would break every upstream merge.
- New user-facing strings go in all eight `Assets/i18n/*.json` files (English text is fine for every language but en; `LocalizationTests` checks the keys match).
- Settings format changes: bump `AppSettings.CurrentSchemaVersion` and add a step to `SettingsMigrations`.
- Respect `Motion.Enabled` (Reduce animations) for every new animation.

## How we work

- One phase per branch (`phase-1-gesture`, `phase-2-reskin`, ...). Start each phase in plan mode and wait for approval.
- You cannot see or feel the UI. After any UI or gesture change, stop and give me a short manual test checklist (what to drag, which key, what I should see). Do not mark UI tasks done until I confirm.
- When unsure how Tangerine behaves, ask me. I will check the real app or the screen recordings. Do not guess the behaviour.
- Small commits with clear messages. No commented-out code, no TODOs left behind without an entry in `PLAN.md`.
