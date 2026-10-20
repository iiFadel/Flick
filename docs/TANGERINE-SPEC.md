# Tangerine spec

Source of truth for wheel contents and look. Transcribed from the interactive demo on tangerineformac.com (20 Oct 2026). Items marked **(confirm)** are inferred from the demo and must be checked in the real app before building.

## Interaction

- Start dragging a file, then tap a key. The wheel opens at the cursor and stays open after the key is released, until drop or Escape.
- Shift = Convert wheel. Command = Tools wheel. Control = Actions wheel. Keys are configurable in Settings.
- Tapping another key mid-drag switches wheels.
- Keyboard path: Enter on a file opens the format menu; Command or Control switches wheels; arrow keys select; Enter applies; Escape cancels.
- The first item (top slice) is pre-selected when the wheel opens.
- The file's own format is not shown in its Convert wheel (JPG source shows no JPG, MP3 source shows no MP3, and so on). **(confirm)**

## Wheel contents

Order is clockwise starting from the top slice.

### Images (demo source: JPG)

- **Shift (7):** PNG, WEBP, HEIC, TIFF, AVIF, BMP, PDF
- **Command (7):** Compress, Metadata, Edit, Annotate, Add BG, Crop, Redact

### Video (demo source: MOV)

- **Shift (7):** MP4, MKV, WEBM, AVI, WMV, GIF, MP3
- **Command (9):** Compress, Metadata, Mute, Trim, Crop, Speed, Snapshot, Split, Redact

### Audio (demo source: MP3)

- **Shift (7):** M4A, WAV, FLAC, OGG, OPUS, AIFF, WMA
- **Command (7):** Compress, Metadata, Normalize, Visualize, Trim, Channels, Bleep

### PDF

- **Shift (9):** DOCX, PAGES, PPTX, XLSX, JPG, PNG, EPUB, TXT, MD
- **Command (4):** Compress, Metadata, Split, Organize

### Documents (demo source: DOCX)

- **Shift (8):** PAGES, PDF, TXT, MD, HTML, EPUB, PNG, JPG
- **Command:** none in demo **(confirm)**

### Slides

- **Shift (3):** PDF, JPG, PNG. With 3 slices, PDF sits on the left (9 o'clock) and is pre-selected; JPG upper right, PNG lower right. **(confirm the layout rule for 3 items)**
- **Command:** none in demo **(confirm)**

### Spreadsheets (demo source: XLSX)

- **Shift (4):** ODS, CSV, TSV, PDF
- **Command:** none in demo **(confirm)**

### EPUB

- **Shift (6):** PDF, DOCX, PAGES, TXT, MD, HTML
- **Command:** none in demo **(confirm)**

### Subtitles (demo source: SRT)

- **Shift (2):** VTT (top half), TXT (bottom half)
- **Command:** none in demo **(confirm)**

### Archives (demo source: ZIP)

- **Shift (2):** TAR (top half), GZIP (bottom half)
- **Command (1):** Extract (single full ring, with a small gap at 6 o'clock)

### Actions (Control, all file types, folders and mixed selections)

AirDrop, Float, Copy, Move, Copy Path, Terminal. Order and look **(confirm)**: not in the screenshots.

### Unknowns

- The site claims 26 tools; the demo shows 18 distinct ones. The rest probably appear for multi-file selections (Merge is mentioned on the site) or other source formats. **(confirm by dragging 2+ PDFs, 2+ images, mixed files, and a folder)**
- Whether Documents, Slides, Spreadsheets, EPUB and Subtitles really have no Command wheel.

## Tool behaviour buckets

Decides which build phase a tool lands in. All are guesses until checked in the app.

| Bucket | Tools |
|---|---|
| Instant (no UI) | Compress (all), Metadata (all), Mute, Normalize, Extract |
| Asks a value (small popover) | Speed, Split (video, PDF), Channels, Snapshot (which frame), Visualize (style?), Add BG (colour) |
| Editor window | Crop (image, video), Trim (video, audio), Redact (image, video), Annotate, Edit, Organize (PDF pages), Bleep |

Bleep is the unknown: either manual time ranges (editor) or automatic profanity detection via speech recognition (instant). Check it first.

## Visual design (from screenshots, measure exactly from the app)

- **Wheel:** circle with a light grey outer track and a soft drop shadow. Slices are separate rounded segments with even gaps between them, inside the track.
- **Selected slice:** solid orange (about `#EB5E2B`, sample the exact value), white text. Unselected slices: very light grey with a subtle radial gradient, near-black text.
- **Center:** white circle (about 30% of wheel diameter) with a white pill showing the selected item's name in small caps.
- **Convert slices:** bold uppercase format name, no icon.
- **Tool slices:** small line icon above a small uppercase label with wide letter spacing.
- **Below the wheel:** grey caption with the action, e.g. "Convert to PNG", "Compress", "Extract Archive".
- **Slice count layouts:** 1 = full ring with a gap at the bottom; 2 = top and bottom halves; 3+ = equal slices with the first item at the top (except the 3-item case above).
- **Font:** a geometric sans close to the website's; check the app bundle's Resources folder for a bundled font, otherwise SF Pro.

## To capture from the real app

- [ ] Exact orange, greys, shadow, wheel diameter, slice gap, corner radius
- [ ] Open, switch, hover and close animations (screen record at 120fps, step through frames)
- [ ] What happens after drop: progress indicator, completion state, where the output file goes and its name
- [ ] Each "asks a value" and editor tool: screenshot of its UI
- [ ] Actions wheel
- [ ] Dark mode
