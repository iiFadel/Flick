# Tangerine spec

Source of truth for wheel contents, behaviour and look. We do not have the Tangerine app. Two references, saved in `docs/tangerine/`:

- **Site**: tangerineformac.com saved on 21 Oct 2026 (`site/`). Its interactive demo is driven by `home-BU_Sezi2.js` and `style-DGEuNh-I.css`; every number below marked *site* is read straight from those two files.
- **Recording**: a screen recording of the site's landing video, which shows the real Mac app (`WhatsApp Video 2026-10-21 at 11.25.40 AM.mp4`, 816×416, about 53 fps, 10.8 s, two loops of: drag `sample.wav`, tap Shift, hover AIFF → OPUS → MP3, drop, progress toast, `sample.mp3` appears). Marked *recording*. WhatsApp compression: colours are ±10, timings ±1 frame (≈17 ms).

Tags: **(confirm)** = still a guess. **(our design)** = not shown anywhere, we decide in the same visual language. Where site and recording differ, the recording wins, because it is the real app.

## Interaction

- Start dragging a file, then tap a key. The wheel opens at the cursor and stays open after the key is released, until drop or Escape.
- Shift = Convert wheel. Command (Ctrl on Windows) = Tools wheel. Keys are configurable in Settings.
- **Flick decision (21 Oct 2026): no Actions wheel.** Tangerine's Control wheel is dropped. Flick has two wheels only.
- **Wheel content rule:** the Convert wheel holds only format targets; the Tools wheel holds only the tools listed for that category below. No format conversions in Tools, no item twice, nothing from upstream Filee that is not in this spec (Filee extras like resize, grayscale, 720p stay in code but off the wheels).
- Tools not built yet still appear in their spec position, greyed out with a "coming soon" reason, so the layout matches the spec now and fills in during Phase 3.
- Sources with no Tools wheel (Documents, Slides, Spreadsheets, EPUB, Subtitles, and the other formats marked "none" below): tapping Ctrl does nothing and the Convert wheel stays open.
- A wheel opens only when exactly one wheel key is down (*site*: `K()` returns nothing for two keys at once).
- Tapping another key mid-drag switches wheels. The new wheel replaces the old one in place, same centre, no new opening animation (*site*: the menu element stays shown and only its children are replaced). Not in the recording **(confirm)**.
- Keyboard path: Enter on a file opens the Convert wheel with its first slice selected; Command (Ctrl) switches wheels; arrow keys select (Right/Down = next clockwise, Left/Up = previous, Home/End = first/last, all wrapping); Enter or Space applies; Escape cancels; Tab cancels.
- **Nothing is pre-selected when the wheel opens during a drag.** A slice lights up only while the pointer is over it (*site*: `selected:null` on open; *recording*: no orange until the pointer reaches AIFF). Only the keyboard path (Enter on a file) selects the first slice. This corrects the earlier "first item pre-selected" note.
- Hit test: a slice is under the pointer only between the inner and outer radius and not inside a gap (*site*: `Ke()` + `isPointInFill`). The pointer in the hole or outside the ring selects nothing.
- Drop on a slice runs it. Drop with nothing selected cancels (*site*: `pointerup` → `b()`).
- The wheel is centred where the pointer was when it opened, moved inward if needed so the whole wheel plus 14 px stays on screen (*site*: clamp margin `backdrop + 14` = 139.92 px from the centre).
- A short sound plays on every selection change (*site*: `/sounds/highlight.wav` through Web Audio, up to 6 overlapping). The sound file was not saved **(our design: a soft tick, off by default)**.
- The file's own format is never in its Convert wheel (*site*: `filter(a => a !== e)`; *recording*: WAV source shows no WAV).

## Wheel contents

Order is clockwise from the top slice. Lists are copied from the site's `z()` (Convert) and `et()` (Tools).

### Convert (Shift)

| Source | Targets (own format removed) |
|---|---|
| Images: JPG, PNG, WEBP, HEIC, TIFF, SVG, AVIF, BMP | JPG, PNG, WEBP, HEIC, TIFF, AVIF, BMP, PDF |
| Video: MP4, MOV, MKV, WEBM, AVI, WMV | MP4, MOV, MKV, WEBM, AVI, WMV, GIF, MP3 |
| GIF | MP4, MOV, MKV, WEBM, AVI, WMV |
| Audio: MP3, M4A, WAV, FLAC, OGG, OPUS, AIFF, WMA | MP3, M4A, WAV, FLAC, OGG, OPUS, AIFF, WMA |
| PDF | DOCX, PAGES, PPTX, XLSX, JPG, PNG, EPUB, TXT, MD |
| Slides: PPT, PPTX | PDF, JPG, PNG |
| Spreadsheets: XLSX, XLS, ODS, CSV, TSV | XLSX, ODS, CSV, TSV, PDF |
| Documents: DOCX, PAGES, MD, RTF, RTFD, text clipping | DOCX, PAGES, PDF, TXT, MD, HTML, EPUB, PNG, JPG |
| TXT | DOCX, PAGES, PDF, MD, HTML, EPUB, JPG, PNG |
| EPUB | PDF, DOCX, PAGES, TXT, MD, HTML |
| Subtitles: SRT, VTT | SRT, VTT, TXT |
| Archives: ZIP, TAR, GZ, RAR | ZIP, TAR, GZIP |

RAR, PPT, XLS, RTF, RTFD and text clippings are import-only: never a target. GZ is labelled **GZIP** and written as `.tar.gz`.

*Recording* shows a WAV source with 6 slices (MP3, M4A, FLAC, OGG, OPUS, AIFF), no WMA. The site lists WMA. Probably an older app version in the video **(confirm)**.

### Tools (Command)

One file:

| Source | Tools |
|---|---|
| JPG, PNG, WEBP, HEIC, TIFF | Compress, Metadata, Edit, Annotate, Add BG, Crop, Redact |
| MP3, M4A, WAV, FLAC | Compress, Metadata, Normalize, Visualize, Trim, Channels, Bleep |
| MP4, MOV, MKV | Compress, Metadata, Mute, Trim, Crop, Speed, Snapshot, Split, Redact |
| GIF | Metadata |
| PDF | Compress, Metadata, Split, Organize |
| ZIP, TAR, GZ, RAR | Extract |
| everything else (SVG, AVIF, BMP, OGG, OPUS, AIFF, WMA, WEBM, AVI, WMV, documents, slides, spreadsheets, EPUB, subtitles) | none: Ctrl does nothing |

Several files of the same type (only tools that work on a batch, plus the multi-file tools):

| Source | Tools |
|---|---|
| JPG, PNG, WEBP, HEIC, TIFF | Compress, Metadata, Crop, Create PDF, Collage |
| SVG | Create PDF, Collage |
| MP3, M4A, WAV, FLAC | Compress, Metadata, Normalize |
| MP4, MOV, MKV | Compress, Metadata, Mute, Join |
| GIF | Metadata |
| PDF | Compress, Metadata, Split, Merge PDF |
| Archives | Extract |

That is all 26 tools on the site. Mixed selections and folders are not in the demo **(confirm)**.

Slice title, full name (used in the centre pill for tools and in the toast) and the SF Symbol the app uses:

| Id | Slice | Full name | SF Symbol |
|---|---|---|---|
| compress | COMPRESS | Compress | arrow.down.right.and.arrow.up.left |
| removeMetadata | METADATA | Metadata | tag |
| editImage | EDIT | Edit Photo | slider.horizontal.3 |
| annotateImage | ANNOTATE | Annotate Photo | pencil.and.outline |
| frameImage | ADD BG | Add BG | photo.artframe |
| cropImage / cropVideo | CROP | Crop Image / Crop Video | crop |
| redactImage / redactVideo | REDACT | Redact Photo / Redact Video | eye.slash |
| redactAudio | BLEEP | Bleep Audio | waveform.badge.minus |
| normalizeAudio | NORMALIZE | Normalize Volume | waveform |
| audioToVideo | VISUALIZE | Audio Visualizer | waveform |
| trimVideo / trimAudio | TRIM | Trim Video / Trim Audio | scissors |
| audioChannels | CHANNELS | Convert Audio Channels | hifispeaker.2 |
| muteVideo | MUTE | Remove Audio | speaker.slash.fill |
| changeVideoSpeed | SPEED | Change Video Speed | speedometer |
| videoSnapshots | SNAPSHOT | Video Snapshots | camera |
| splitVideo | SPLIT | Split Video | film |
| joinVideos | JOIN | Join Videos | film.stack |
| splitPDF | SPLIT | Split PDF | rectangle.split.2x1 |
| mergePDF | MERGE PDF | Merge PDFs | doc.on.doc |
| organizePDF | ORGANIZE | Organize PDF | square.grid.2x2 |
| createPDF | CREATE PDF | Create PDF | doc.badge.plus |
| createCollage | COLLAGE | Create Collage | rectangle.3.group |
| extractArchive | EXTRACT | Extract Archive | archivebox |

SF Symbols may only be used on Apple platforms. On Windows we draw the closest Lucide icon (ISC licence), which is also what the site's own 24 px stroke icons look like **(our design)**.

### Actions

Not in Flick (see Interaction). Tangerine's Control wheel had AirDrop, Float, Copy, Move, Copy Path and Terminal.

### Empty wheel

When the Convert wheel has no slices (a format Flick doesn't know), a card shows instead of the ring (*site*): "No conversions for this format." An empty Tools wheel never shows: Ctrl does nothing.

## Tool behaviour buckets

Decides which build phase a tool lands in. The site's `/tools` page has a window screenshot for 22 tools, so those open a window. The 4 without one are instant.

| Bucket | Tools |
|---|---|
| Instant, no window (*site*) | Mute, Split PDF, Extract, Create PDF |
| Opens a window (*site*, from the tool descriptions) | Compress (target size, % or preset, optional resize), Metadata (edit tags, or remove for a batch), Edit (exposure, colour, detail, effects), Annotate (arrows, shapes, text, numbered callouts), Add BG (solid, gradient or image; aspect ratio, spacing, corner radius), Crop image (aspect ratio or exact size), Redact image (local face/text detection, solid or blur), Normalize, Trim audio (waveform handles, auto-remove silence at the ends), Channels (mono/stereo), Visualize (landscape/portrait/square MP4, orange waveform on black or an image), Bleep (mark ranges on a waveform), Trim video (handles, frame stepping), Split video (split points), Crop video, Speed (pitch kept; 0.5×/1×/2× shown), Snapshot (scrub, pick frames, full-res PNG), Redact video, Join (reorder, saves MP4), Merge PDF (selection order), Organize (reorder, rotate, duplicate, remove pages), Collage (grid, row, column, featured; spacing, corners, background) |

Bleep is manual ranges, not automatic detection.

## Output names (*site*, for later phases)

- Conversion: `name.ext`; taken → `name 2.ext`, `name 3.ext`. GZIP → `name.tar.gz`.
- Tools: `name <suffix>.ext` with suffix Compressed, No Metadata, Edited, Annotated (always PNG), Background, Cropped, Redacted, Normalized, Muted, Trimmed, Bleeped, Speed 2x, Snapshot (PNG), Clips (folder), Pages (folder), Organized (PDF), Extracted (folder), Visualizer (MP4), Mono. Compressing WAV gives FLAC. Taken → `name Compressed 2.ext`.

## Visual design

### Wheel geometry (*site*, `m` in `home-BU_Sezi2.js`)

All in DIP, centre of the wheel = (0, 0), canvas 272 × 272.

| Value | Expression in the site | Result |
|---|---|---|
| Petal inner radius | 48 × 0.8 × 1.1 + 8 × 0.8 | **48.64** |
| Petal outer radius | 166 × 0.8 × 0.9 | **119.52** |
| Glass backdrop outer radius | outer + 8 × 0.8 | **125.92** (diameter 251.84) |
| Glass backdrop inner radius (hole) | 48 × 0.8 × 1.1 | **42.24** |
| Gap between petals | 5 × 0.8 | **4.0**, constant width (parallel edges) |
| Outer corner radius | 28 × 0.8 × 0.9 | **20.16** |
| Label radius | 40 + (119.52 − 40) × 0.56 | **84.53** |

- **Slice angles:** centre of slice *i* of *n* = `start − 2π·i/n` (clockwise, maths angle, y up), `start` = 90° (top) for every *n* except **3, where it is 180° (9 o'clock)**. So 1 slice = full ring with its gap at the bottom; 2 = top and bottom halves; 3 = left, upper right, lower right.
- **Gap:** each side of a slice is pulled in by `asin(min(0.95, 2 / r))` at radius *r*, which gives straight edges 4 px apart all the way.
- **Rounded corners:** the outer corners are rounded with a quadratic curve that starts 20.16 px inward along the edge and ends `f = min(0.15 rad, 0.24 × outer span)` along the outer arc. The inner corners use `u = min(0.18 rad, 0.18 × inner span)` along the inner arc, meeting the edge at radius `inner / cos(u) + inner × tan(u)`. Full path: `we()` in the JS, to be ported 1:1 into `DonutGeometry`.

### Wheel layers, back to front (*site*)

1. **Drop shadow** of the petals: offset (0, 5), blur 8 px (σ 4), `#263438` at 14.5 %.
2. **Glass track**: annulus 42.24 → 125.92, fill `#C0C9CD` at 25 %, background blur 16 px with saturation ×1.45, inner hairline 1 px white at 33 %. *Recording*: reads as a light grey ring (≈ `#D8D8D8`–`#E3E3E3` over a `#F2F2F2` desktop).
3. **Petals**: radial gradient per petal (centre of the petal's bounding box, radius 50 % of it): 0 % white at 80 %, 68 % `#F3F6F7` at 58 %, 100 % `#F4F6F6` at 75 %. Stroke 0.75 px `#5D6666` at 22 %. *Recording*: ≈ `#E3E3E3` with a lighter centre.
4. **Selected petal**: fill `#FF5200`, stroke `#C26534` at 44 %. Fill changes in 120 ms. *Recording*: ≈ `#DE5414` at the outer edge to `#F85310` in the middle; slightly shaded, not quite flat.
5. **Labels** (Convert): uppercase format, 14 px, weight 600, letter spacing 0.35 px, black; white on the selected petal. Centred on the label radius.
6. **Labels** (Tools): icon 18 × 18 centred 7.5 px above the label point, black; label 8.5 px, weight 600, letter spacing 0.2 px, centred 10.5 px below the label point. Both turn white on the selected petal.
7. **Centre**: *recording* shows a round light disc filling the hole with a pill inside. Pill (*site*): 70 wide, at least 32 high (36 for tools), fully rounded, white at 72 % with 12 px background blur (saturation ×1.3), 1 px border white at 70 %, top inner highlight 1 px white at 70 %, shadow (0, 2) blur 6 black at 6 %, padding 4 × 6. Text: selected slice's label (format) or full title (tool: 8.5 px weight 600, may wrap), 12 px weight 500, letter spacing 0.35 px, black. Hidden while nothing is selected.
8. No caption under the live wheel. ("Convert to PNG" under the wheel is only on the site's static preview cards: 11 px, `#6C797D`.)

### Empty-wheel card (*site*)

245 wide, padding 24, radius 18, border 1 px `#D9DFDA`, fill white at 95 %, shadow (0, 8) blur 28 `#24323D` at 12.5 %, text 13 px / line height 1.7, `#828782`, centred.

### Fonts

- Wheel: `ui-rounded` = **SF Pro Rounded** (*site*). Licensed for Apple platforms only, so not usable on Windows. Windows choice: open question, see the Phase 2 plan.
- Website text: DM Sans (OFL). Toast in the *recording*: the Mac system font (SF Pro).

### Colours

| Token | Value | Where |
|---|---|---|
| Selected petal | `#FF5200` | *site* wheel |
| Brand orange | `#F35B28` | *site* links, focus rings, spinner, buttons |
| Pressed key orange | `#D55223` on `#FFF0E4` | *site* key caps |
| Text | `#282B29` | *site* body |
| Muted text | `#828782` | *site* |
| Hairline | `#E9ECE8` | *site* |
| Page background | `#F4F4F4` | *site* |

### Motion

- **Open** (*site*): scale 0.45 → 1 and opacity 0 → 1 over 200 ms, `cubic-bezier(.18, .82, .24, 1)`, about the wheel centre. *Recording*: first frame at ≈ 55 % scale, full size about 50 ms later, labels fading in over the next ≈ 50 ms; done ≈ 100 ms after the first frame. Use the site curve with the labels' fade starting ≈ 40 ms in **(confirm against the real app)**.
- **Hover**: petal fill crossfade 120 ms (*site*); *recording* shows it inside one frame.
- **Switch wheels**: no animation (*site*) **(confirm)**.
- **Close on drop** (*recording*): the wheel stays ≈ 50 ms with the selection lit, then disappears with no animation.
- **Escape / cancel**: disappears (*site*), no recording.
- **Reduce motion** (*site*): all animations and transitions drop to 0.01 ms.

### After the drop (*recording*)

- ≈ 70 ms after the wheel disappears, a toast grows in at the drop point (≈ 30 % → 100 % scale with fade, ≈ 120 ms), centred horizontally on the pointer.
- Toast: rounded rectangle, light grey frosted fill, thin border, soft shadow. Row 1: small × button in a circle on the left, then "Converting to MP3". Row 2: grey "sample.wav · 100%", then "1 of 1 saved" when done. Row 3: thin orange progress bar, rounded, full width inside the padding, on a light grey track.
- Disappears ≈ 80 ms after "1 of 1 saved" (a very short file), then the new file appears next to the source.
- *Site* (simulated): the new file appears beside the drop with a 16 px spinner (2 px ring `#B6BFB3`, top segment brand orange, 0.7 s per turn) for 680 ms.

### Not on the site or in the recording (our design)

Dark mode, value popovers, editor windows, Settings window, tray menu, errors in the toast, toasts for several files. Keep them in the same language: orange accent, light frosted greys, soft shadows, round shapes, small uppercase labels. Mark each in this file as **(our design)** when it is decided.

## Not usable in the repo

The saved site (images, icons, JS, CSS) and the recording are Tangerine's, kept for reference only. Numbers and behaviour are written down here; no file from `docs/tangerine/` is copied into `src/`.
