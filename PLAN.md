# Plan: Tangerine-style Filee fork

Goal: Filee's conversion engine with Tangerine's interaction and look, on Windows first, then macOS.

Size labels are relative effort, not dates: S (an evening), M (a few evenings), L (a week or more of evenings).

## Gap between Filee and Tangerine

| Area | Filee today | Target (Tangerine) |
|---|---|---|
| Trigger | Hold key, then drag | Start dragging, then tap a key. Key can be released, wheel stays open until drop or Escape |
| Wheels | One donut | Two: Shift = convert, Ctrl/Cmd = tools (Tangerine's actions wheel is dropped). Switch between them mid-drag |
| Keyboard use | No | Arrow keys choose, Enter applies, Escape cancels |
| Tools | Some mixed into the donut (resize, PDF compress/merge/split) | Separate wheel: compress, strip metadata, mute, trim, crop, speed, snapshot, split, redact, annotate, add background, edit |
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

- [x] Flick, not Filee, everywhere the user sees it: window titles, tray, toasts, settings, i18n strings, app icon (namespaces and `Filee.*` identifiers stay)
- [x] Real Flick icon: orange disc, white six-petal wheel drawn with the wheel's own geometry
- [x] Redraw `DonutMenu` to the spec: shape, slice style, icons, labels, hover, centre content (`DonutMenu.Look.cs`, `PetalGeometry`)
- [x] Opening, switching and closing animations matched to the site and recording (`WheelMotion`: a 200 ms bloom, a 120 ms hover fade, no switch or close animation)
- [x] Toast/progress window and settings window restyled to match (drop toast at the drop point, `Styles/Flick.axaml`)
- [x] Theme tokens through `ThemeService` (no hard-coded colours in controls): `Styles/Flick.axaml`

Done when: side by side with the recording at normal speed, you can't immediately tell them apart in motion.

## Phase 3: Tools wheel, no-UI tools (M)

- [x] Replace the Tools wheel stand-in: both drag wheels now show the spec's lists (`WheelCatalog`); tools not built yet are greyed out as "coming soon"
- [x] Add an "operation" concept next to target-format presets: `ToolOperations` builds a same-format preset per tool, never saved (decided 21 Oct 2026: code-only, Tangerine names, instant tools use fixed defaults until Phase 5's windows)
- [x] Metadata (lossless for JPEG, PNG, WebP, GIF, TIFF, HEIC; streams copied for audio and video; PDF), Mute
- [x] Instant tools: Compress (images, video, audio, PDF; never larger than the source), Normalize (-16 LUFS), Extract (archives, `name Extracted`)
- [x] Small value popover (one shared control, `ToolParameter`) and Channels (Mono / Stereo)
- [x] Popover tools (choices decided 22 Oct 2026): Speed 0.25× / 0.5× / 1.5× / 2× / 4×; Split video into 2 / 3 / 4 equal parts at keyframes, no re-encode, `name Clips` folder; Snapshot 1 (middle) / 5 / 10 PNG frames, several in a folder; Visualize landscape / portrait / square; Add BG white / black / orange gradient, ~8% padding, rounded corners
- [x] Tests per operation, inputs generated in the test (see `EngineFixture`)

## Phase 4: Actions wheel (dropped)

Decision 21 Oct 2026: Flick has two wheels only (Shift = Convert, Ctrl = Tools). Remove the Actions wheel, its key binding and its settings.

## Phase 5: Editor tools (L, the biggest phase)

- [x] Shared editor window shell: opens after drop, preview, Apply/Cancel, writes a new file next to the source (`Editors/EditorWindow`, `RadialController.Editor.cs`; the edit goes on the operation's preset and runs through the job queue)
- [x] Crop (image): aspect Free / Original / 1:1 / 4:3 / 16:9 / 9:16, exact size, `name Cropped.ext`
- [x] Trim (video, audio): frame strip or waveform, two handles, frame under the handle, ← → a frame (audio 0.1 s), Shift a second, Tab the other handle, Remove silence at the ends; re-encodes in the source's format, `name Trimmed.ext`
- [x] Crop (video): the Crop editor on the middle frame, sizes in the video's pixels rounded down to even; FFmpeg `crop` first in the filters, `name Cropped.ext`
- [x] Organize (PDF): page cards, drag to move, ← → pick, Shift+← → move, turn left / right, Duplicate, Delete (never empty), `name Organized.pdf`; pages copy as they are, so bookmarks and form fields stay behind
- [x] Redact (image, then video): boxes drawn by hand, Solid (black) or Blur, click to pick, Delete, Ctrl+Z; a video's boxes cover every frame, `name Redacted.ext`
- [x] Annotate (image): Arrow, Box, Ellipse, Text, numbered Callout (renumber after a delete), seven colours (the picked mark takes the colour), Undo / Redo, Delete; drawn by ImageMagick from the same geometry as the window, always `name Annotated.png`
- [x] Edit (image): Exposure, Contrast, Saturation, Warmth, Sharpness with a live preview (the engine's own code on a 1400 px copy), hold Original to compare, Reset; `name Edited.ext` in the picture's format
- [x] Bleep (audio): drag over the waveform to mark ranges (overlapping ones join), click to pick, Delete, Ctrl+Z, Tone (1 kHz) or Silence; FFmpeg `aeval`, `name Bleeped.ext`
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

## Later, only if wanted

- Explorer selection + keyboard shortcut to open the wheel without dragging
- Real backdrop blur behind the wheel's track and pill (Tangerine's glass). Avalonia can't blur behind part of a transparent window, so Flick uses the colours the glass shows over a light desktop; over a dark or busy desktop it looks flatter than Tangerine
- The click-mode donut (hold gesture, Explorer menu) and the Donut toolbar editor still use upstream's toolbar profiles and progress ring; the drag wheels use the spec lists (`WheelCatalog`) and the drop toast. Decide whether click mode should follow
- `EbookTests.Calibre_ranked_first_makes_a4_pdfs_of_e_books` , `HwpxReaderTests.A_hundred_pages_are_read_in_well_under_a_second` and `SingleInstanceTests.Arguments_reach_a_first_instance_that_starts_listening_late` fail in full runs of the whole solution and pass alone (load or timing); look into them
- Engine error texts (password-protected archives, DRM ebooks, CAD), log lines and the `filee` CLI still say Filee
- Compress on a mixed WAV + MP3 selection keeps the WAVs as WAV (one target per job); WAV alone becomes FLAC
- Normalize is one loudnorm pass (dynamic); a two-pass linear normalize would match -16 LUFS more exactly on music with wide dynamics
- Metadata on a HEIC runs without FFmpeg, but its slice still needs FFmpeg installed: the HEIC self-edge belongs to FFmpeg
- Split video cuts at the next keyframe after each equal point: a video with very few keyframes gives uneven or fewer clips (Phase 5's split points could re-encode around the cut)
- Edit has the five basic sliders; Tangerine's "effects" (filters, vignette, ...) and "detail" beyond sharpness are not built
- Redact finds nothing by itself: Tangerine detects faces and text on the device; Flick's boxes are drawn by hand (an ML model would be a big download)
- Redact video hides its boxes for the whole clip; a box that follows something, or shows only for part of the clip, would need time ranges per box
- Watch folders and CLI already exist upstream, keep them working
- Pull upstream regularly: `git fetch upstream && git merge upstream/main`
