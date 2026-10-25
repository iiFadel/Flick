# Starting Phase 6 on a Mac

How to begin the native macOS Flick (PLAN.md, Phase 6). It is a separate Swift app in its own repo, not this Avalonia
solution; this repo stays the Windows app and the source of the spec, the geometry and the fallback converter.

## 1. What you need

- A Mac on macOS 14 (Sonoma) or later, Apple silicon or Intel.
- Xcode 16 or later from the App Store. Open it once and let it install its components.
- An Apple ID signed in under Xcode › Settings › Accounts. A free account is enough for the spike; a paid developer
  account (99 USD a year) is only needed later, to notarize a build for other Macs.
- Tangerine installed, to compare against while you build.
- Homebrew (optional), for `brew install ffmpeg git gh`.

## 2. Create the repo and the project

```sh
mkdir -p ~/Projects/flick-mac && cd ~/Projects/flick-mac
git init
```

In Xcode: File › New › Project › macOS › App.

- Product Name: `Flick`, Interface: SwiftUI, Language: Swift, Testing: Swift Testing (or XCTest).
- Save it into `~/Projects/flick-mac`.

Then, in the target's settings:

1. **Signing & Capabilities:** pick your team (your Apple ID). **Remove App Sandbox**: a sandboxed app can't watch
   global mouse and keyboard events or accept drags from anywhere the way Flick needs. Keep **Hardened Runtime**, which
   notarizing needs later.
2. **Info:** add `Application is agent (UIElement)` = `YES` (`LSUIElement`), so Flick lives in the menu bar with no Dock
   icon.
3. **General:** Minimum Deployments = macOS 14.0.

Copy these from this repo into `flick-mac/docs/`, because they are what "the same as Tangerine" means:

- `docs/TANGERINE-SPEC.md`: the wheels' contents, geometry, colours, timings and output names.
- `docs/ARCHITECTURE.md`: the gesture model and the conversion pipeline, to port.
- `PLAN.md`'s Phase 6 section, as the new repo's plan.

## 3. The spike (2 to 3 evenings, nothing else)

The only question the spike answers is: **can a menu bar app open a wheel at the cursor when you tap Shift during a
Finder drag, and take the drop?** Build these five steps in order, and check each one by hand before the next.

1. **Menu bar app.** Replace the window scene with `MenuBarExtra("Flick", systemImage: "circle.hexagongrid.fill") {
   Button("Quit") { NSApp.terminate(nil) } }`. Run it with ⌘R and check the icon is there and Quit works.
2. **Notice a file drag.** Start `NSEvent.addGlobalMonitorForEvents(matching: [.leftMouseDragged, .leftMouseUp])`.
   While the mouse drags, read `NSPasteboard(name: .drag)`. When its `changeCount` changed since the drag began and it
   holds `.fileURL` items, a file drag is on. Log the paths.
3. **Tap Shift.** Add `.flagsChanged` to the monitor. Shift pressed and released within about 300 ms during a file drag
   counts as a tap (port `GestureDetector`'s rules from this repo: the drag starts first, then the tap). Log "open".
4. **The wheel.** Show an `NSPanel` with `styleMask: [.nonactivatingPanel, .borderless]`, `level = .popUpMenu`,
   `isFloatingPanel = true`, `backgroundColor = .clear`, `hasShadow = false`,
   `collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]`, centred on `NSEvent.mouseLocation`. Its content is
   an `NSHostingView` with a placeholder ring of six slices. It must not take focus away from Finder (that is what
   `.nonactivatingPanel` is for).
5. **Take the drop.** Make the panel's content view an `NSView` subclass that calls `registerForDraggedTypes([.fileURL])`
   and implements `draggingEntered` / `draggingUpdated` (light the slice under the cursor, return `.copy`) and
   `performDragOperation` (read the URLs and the slice; for the spike, run `/usr/bin/sips -s format png <file> --out
   <file>.png` for a JPG on the PNG slice). Then close the panel.

If all five work from Finder, on a second display and over a full-screen app, the hard part is done; the rest is
ordinary app work. If step 2 or 5 fails, stop and look again before building anything else.

## 4. Permissions

Global key events need **Accessibility** (and, for some setups, **Input Monitoring**). In the spike:

```swift
let trusted = AXIsProcessTrustedWithOptions([kAXTrustedCheckOptionPrompt.takeUnretainedValue(): true] as CFDictionary)
let listening = CGPreflightListenEventAccess() || CGRequestListenEventAccess()
```

Grant them in System Settings › Privacy & Security, then quit Flick and start it again (macOS only applies them on
launch). Keep one signing identity for debug builds: macOS ties the permission to the signature, and a changed one
means granting it again. If permissions get stuck, `tccutil reset Accessibility <your bundle id>`.

The onboarding screen (PLAN.md, Phase 6, second item) comes after the spike: one window that explains both
permissions, opens the right Settings pane (`x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility`)
and turns each row green once it is granted.

## 5. What to port from this repo

Port these as Swift with their tests. They are pure, so they translate line by line:

| Here (C#) | What it is |
|---|---|
| `Filee.App/Services/Triggers/GestureDetector.cs` | When a drag plus a key tap opens, switches or closes a wheel |
| `Filee.App/Controls/DonutGeometry.cs`, `PetalGeometry.cs` | Slice angles, hit testing, petal shapes |
| `Filee.Core/Wheels/WheelCatalog.cs` | What each wheel holds per file type, in order |
| `Filee.Core/Wheels/ToolOperations.cs` | Which tools run on which formats, and their name suffixes |
| `Filee.Core/Presets/OutputPathResolver.cs` | `name Compressed.jpg`, `name Compressed 2.jpg` |
| `Filee.Core/Editing/*` | The editors' state: crop, trim, pages, boxes, marks, sliders, bleeps |

Engines go native first (PLAN.md, Phase 6): ImageIO and Core Image for pictures, AVFoundation for video and audio,
PDFKit for PDFs, `textutil` for documents. FFmpeg goes in the app bundle for what AVFoundation can't do.

## 6. The fallback converter

For conversions with no native route (PDF → DOCX, EPUB, spreadsheets), call this repo's CLI. On the Mac, from this
repo:

```sh
dotnet publish src/Filee.Cli -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o out/mac
./out/mac/filee-cli --help
```

Use `-r osx-x64` for an Intel Mac. Put the result in the app bundle (`Contents/Resources/bin/`), call it with `Process`,
and sign it along with the app. It is a fallback only: the native engines come first.

## 7. When it works

Tick the spike in PLAN.md, write `flick-mac/docs/MAC-NOTES.md` with what surprised you (permission quirks, how the
drag pasteboard behaved, panel levels), and plan the next item in plan mode as this repo does: one item per branch.
