# The CrestApps video specification

Every CrestApps video follows these values. They are applied by `scripts/brand.py`, `scripts/narrate.py`,
`scripts/assemble.py` and `scripts/recorder/Scene.cs`; change them there, and here, never for a single video.

## Output

| Setting | Value | Why |
|---|---|---|
| Resolution | 1920x1080 | Full HD: sharp text on any screen. |
| Frame rate | 30 fps | Smooth cursor movement, small files. |
| Video codec | H.264 High, `yuv420p`, `-preset slow -crf 34 -tune stillimage`, keyframe every 10 s | Plays everywhere; screen content compresses well, and text stays readable at CRF 34. |
| Audio codec | AAC, 48 kbps, 48 kHz, mono | Speech only: higher rates add about 2 MB per 10 minutes and nothing audible. |
| Container | MP4 with `-movflags +faststart` | Starts playing before it's fully downloaded. |
| Size | Under 10 MB for videos up to about 7 minutes (GitHub's attachment limit); under 15 MB for longer overviews | A 10-minute overview is about 13.5 MB. |
| Master | `build/master.mp4`, CRF 16, AAC 192 kbps stereo | Kept in the workspace to encode again without rebuilding. |
| Captions | WebVTT (docs) and SRT, at most about 13 words each, split at sentence ends | Accessibility, and viewing without sound. |

## Layout

- **Background**: navy `#081B26`, with a blurred amber glow in the top right corner and a steel blue one in the
  bottom left.
- **Header** (recordings and slides): the logo, 250 px wide, at x = 96, centered on y = 47; a thin divider; the
  chapter title in Inter SemiBold 34 px; on the right, an amber pill with the chapter label ("CHAPTER 3") in Inter
  Bold 22 px, navy.
- **Recordings**: scaled to 1728x972 (90%), centered horizontally, 14 px from the bottom, with a soft shadow and a
  3 px amber rounded border.
- **Slides**: a heading in Inter Bold 52 px at y = 150, underlined by a 100x6 px amber bar; content from y = 240,
  within 96 px margins.
- **Code panels**: `#05131C` with a window bar (red, yellow and green dots) and the file name; Consolas (or the
  closest monospace font), 22 to 28 px; the syntax colors of the Visual Studio Code dark theme; at most about 20
  lines per panel.
- **Cards**: the opening and closing cards show the full logo (900 px) over the video's title, an amber bar, and the
  subtitle. Chapter cards show the mark (150 px), "CHAPTER N" in amber, the chapter's title and subtitle.

## Branding

- Logo: `src/CrestApps.Docs/branding/CrestAppsMainLogo.png`, used as it is: its silver name and amber mark read well on
  navy. The mark alone is the part left of the name.
- Font: Inter, the font of the docs site, from `src/CrestApps.Docs/branding/fonts` (Regular, Medium, SemiBold, Bold;
  SIL Open Font License in `OFL.txt`).
- Colors (from the logo and `src/CrestApps.Docs/src/css/custom.css`):

| Name | Hex | Use |
|---|---|---|
| Amber | `#EAA429` | Accents, frame, pills, highlight rings, click ripples, captions in the page |
| Dark amber | `#D88500` | |
| Navy | `#081B26` | Background; text on amber (white on amber is too faint) |
| Ink | `#21323C` | |
| Steel blue | `#2A81BB` | The second background glow |
| Silver | `#BEC3C8` | |
| Mist | `#EDF4FA` | Titles and slide text |

## Recording

- Chromium, headless, through Playwright for .NET, captured with the DevTools screencast (JPEG, quality 92).
- Viewport 1536x864 CSS pixels, device scale factor 1.25, light color scheme, `en-US` locale.
- A drawn cursor (white arrow with a dark outline), an amber ripple on each click, amber highlight rings with an
  optional amber caption in navy text, all injected in the page by `Scene.cs`.
- The cursor moves with ease-in-out over 25 steps; typing at 25 to 90 ms per character; each step holds until its
  narration ends, plus 0.45 s.
- The admin menu is collapsed in builder-like screens, so the subject has the whole width.
- Realistic but fake data. Never record real people, accounts, credentials, or tokens: use a copy of a demo site, and
  an init script (`VIDEO_INIT_SCRIPT`) that replaces any leftover real names.

## Timing

| Element | Duration |
|---|---|
| Opening and closing cards | 1.0 s + narration + 1.4 s, fading in and out |
| Chapter cards | 2.6 s, no narration |
| Slides | 0.6 s lead on the first, 0.25 s on the others; 0.5 s between slides; 1.0 s tail on the last |
| Recordings | 0.7 s before the first step, 1.0 s after the last narration, 0.35 s fades |

## Voice

- `en-US-AvaMultilingualNeural` at `-5%` rate, through `edge-tts`: a clear, professional woman's voice, the same in
  every CrestApps video.
- Pronunciation fixes in `SPOKEN` (`scripts/narrate.py`), such as "CrestApps" spoken as "Crest Apps" and "ASP.NET" as
  "A, S, P dot net". The captions keep the written form.
- Acronyms said letter by letter are written with commas ("C, S, V"), so each letter is distinct: with spaces only,
  the letters run together. Check them with `scripts/pronounce.py`.
