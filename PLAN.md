# Plan: Tangerine-style Filee fork

Goal: Filee's conversion engine with Tangerine's interaction and look, on Windows first, then macOS.

Size labels are relative effort, not dates: S (an evening), M (a few evenings), L (a week or more of evenings).

## Gap between Filee and Tangerine

| Area | Filee today | Target (Tangerine) |
|---|---|---|
| Trigger | Hold key, then drag | Start dragging, then tap a key. Key can be released, wheel stays open until drop or Escape |
| Wheels | One donut | Three: Shift = convert, Ctrl/Cmd = tools, Alt/Ctrl(mac) = actions. Switch between them mid-drag |
| Keyboard use | No | Arrow keys choose, Enter applies, Escape cancels |
| Tools | Some mixed into the donut (resize, PDF compress/merge/split) | Separate wheel: compress, strip metadata, mute, trim, crop, speed, snapshot, split, redact, annotate, add background, edit |
| Actions | None | Share, Float, Copy, Move, Copy Path, Terminal. Works on folders and mixed selections |
| Look | Filee style | Tangerine style |
| macOS | Preview, platform project not written yet | Feels native |

## Phase 0: Setup and spec (S)

- [ ] Fork on GitHub, clone, add `upstream` remote
- [x] `dotnet run` and `dotnet test` green on Windows
- [ ] Install FFmpeg engine from Settings → Engines
- [ ] Record Tangerine at high frame rate: each wheel opening, switching wheels, hovering slices, dropping, progress, completion, cancel
- [ ] Inspect the Tangerine `.app` bundle: `Contents/Frameworks` and `otool -L` on the binary (which libraries it uses), `Contents/Resources` (reference only, keep out of the repo if it is ever public)
- [ ] Write `docs/TANGERINE-SPEC.md`: per file category, the exact items in each of the three wheels, their order, and what each tool asks for (if anything). Plus measured sizes, colours, animation durations and easing from the recordings

Done when: the app runs from source and the spec file exists. Every later phase builds from the spec.

## Phase 1: Gesture model (M, the most important phase)

- [x] Rewrite `GestureDetector` state machine: drag starts first, a modifier tap opens the matching wheel, releasing the key keeps it open
- [x] Three wheel kinds (Convert, Tools, Actions), tapping another modifier switches wheel without closing
- [x] Escape cancels, arrow keys move selection, Enter applies to the selected slice
- [x] Key bindings configurable in Settings (defaults: Shift, Ctrl, Alt on Windows)
- [x] Unit tests for every transition, including: tap before drag threshold, double tap, key held through drop, drop outside wheel, Escape mid-switch
- [x] Manual check: the Explorer drag never breaks, source file is never moved or deleted

Risk: holding Shift or Ctrl during an Explorer drag changes the drop effect (move/copy/link). Filee already forces Copy on drop, but verify Explorer's cursor and behaviour while the key is held. If Shift-as-trigger fights Explorer, use tap-and-release only and test both.

## Phase 2: Reskin (M)

- [ ] Flick, not Filee, everywhere the user sees it: window titles, tray, toasts, settings, i18n strings, app icon (namespaces and `Filee.*` identifiers stay)
- [ ] Real Flick icon (the rename shipped a placeholder: orange disc, white wheel)
- [ ] Redraw `DonutMenu` to the spec: shape, slice style, icons, labels, hover, centre content
- [ ] Opening, switching and closing animations as springs matched to the recordings
- [ ] Toast/progress window and settings window restyled to match
- [ ] Theme tokens through `ThemeService` (no hard-coded colours in controls)

Done when: side by side with the recording at normal speed, you can't immediately tell them apart in motion.

## Phase 3: Tools wheel, no-UI tools (M)

- [ ] Replace the Tools wheel stand-in (Phase 1 shows the profile's back face there, `RadialViewModel.Wheel.cs`)
- [ ] Add an "operation" concept next to target-format presets (a same-format transform with parameters). Design it in plan mode first: it must fit `Preset`, `ToolbarProfile`, `JobQueue` and `OutputPathResolver` without breaking conversions
- [ ] Instant tools: Compress and Metadata (images, video, audio, PDF), Mute, Normalize, Extract (archives)
- [ ] Small value popover (one shared control), then: Speed, Split (video, PDF), Channels, Snapshot, Visualize, Add BG
- [ ] Tests per operation, inputs generated in the test (see `EngineFixture`)

## Phase 4: Actions wheel (S to M)

- [ ] Fill the Actions wheel (Phase 1 opens it empty with a "later version" note, `RadialViewModel.Wheel.cs`)
- [ ] Copy (files to clipboard), Copy Path, Move (folder picker), Open Terminal here
- [ ] Share: Windows share sheet (DataTransferManager) as the AirDrop equivalent
- [ ] Float: always-on-top preview window for images and PDFs
- [ ] Works on folders and mixed selections, not counted as conversions

## Phase 5: Editor tools (L, the biggest phase)

- [ ] Shared editor window shell: opens after drop, preview, Apply/Cancel, writes a new file next to the source
- [ ] Order: Crop (image) → Trim (video/audio, timeline + preview) → Crop (video) → Organize (PDF pages) → Redact (image, then video) → Annotate → Edit (basic adjustments) → Bleep
- [ ] One tool per branch, each usable on its own before starting the next

## Phase 6: macOS, native Swift app (L, separate repo)

Built from scratch in Swift (SwiftUI + AppKit), not on Avalonia, because the goal is to look and feel exactly like Tangerine, which is itself a native Mac app.

- [ ] Spike first (2 to 3 evenings): menu bar app, global drag detection, tap Shift during a Finder drag, non-activating NSPanel wheel at the cursor that accepts the drop. Nothing else. If this works, the rest is ordinary app work
- [ ] Permission flow (Accessibility / Input Monitoring) with a clear onboarding screen
- [ ] Wheel UI built to `docs/TANGERINE-SPEC.md`, using native materials, SF Symbols and spring animations
- [ ] Native engines first: ImageIO / Core Image (images), AVFoundation (video, audio), PDFKit (PDF render, merge, split), `textutil` (DOCX, RTF, HTML, TXT)
- [ ] Bundled FFmpeg for what AVFoundation can't do (MKV, WebM, AVI, WMV, FLAC, OGG, Opus, GIF)
- [ ] Fallback backend: the fork's `filee` CLI (self-contained .NET publish) for conversions with no native route (PDF → DOCX/PPTX/XLSX, EPUB, spreadsheets)
- [ ] Actions: AirDrop / share sheet, Float (floating NSPanel), Copy, Move, Copy Path, Terminal

## Later, only if wanted

- Explorer selection + keyboard shortcut to open the wheel without dragging
- Engine error texts (password-protected archives, DRM ebooks, CAD), log lines and the `filee` CLI still say Filee
- Watch folders and CLI already exist upstream, keep them working
- Pull upstream regularly: `git fetch upstream && git merge upstream/main`
