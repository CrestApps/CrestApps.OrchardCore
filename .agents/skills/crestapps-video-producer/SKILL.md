---
name: crestapps-video-producer
description: Produces narrated CrestApps videos (module overviews, feature walkthroughs, pull request demos) with one unified look and specification - 1080p, the CrestApps logo, colors and Inter font, the same neural voice (Ava), captions, and a small H.264 encode. Use when asked to record, narrate, or produce a video or screencast of a CrestApps module, or to add one to the docs site or a pull request.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Video Producer

Every CrestApps video looks and sounds the same: the same resolution, branding, layout, voice, captions, and
encoding. This skill holds that specification and the scripts that apply it. **Don't change the specification for a
single video.** If it must change, change it here, in `scripts/brand.py`, `scripts/narrate.py`, `scripts/assemble.py`
and `scripts/recorder/Scene.cs`, and in `references/specs.md`, so every later video follows.

The worked example in `examples/report-builder/` is the storyboard, slides and recorded scenes of the Report Builder
video (`src/CrestApps.Docs/static/img/docs/report-builder.mp4`). Read it before writing a new one.

## The specification, in short

| | |
|---|---|
| Output | 1920x1080, 30 fps, H.264 (`-preset slow -crf 30 -tune stillimage`), AAC 48 kbps mono, `+faststart`; under 25 MB, and linked rather than attached to a pull request when over 10 MB |
| Recording | Chromium at 1536x864 CSS pixels, device scale 1.25, light color scheme, en-US, shown at 1728x972 in an amber frame |
| Branding | Navy background with an amber and a steel blue glow, the CrestApps logo (`src/CrestApps.Docs/branding/CrestAppsMainLogo.png`), Inter from `src/CrestApps.Docs/branding/fonts` |
| Colors | Amber `#EAA429` (accents, frame, pills, highlights, clicks), navy `#081B26` (background and text on amber), steel blue `#2A81BB`, silver `#BEC3C8`, mist `#EDF4FA` |
| Voice | `en-US-AvaMultilingualNeural`, rate `-5%`, through `edge-tts`: the same woman's voice in every video |
| Structure | Opening card, numbered chapters (chapter card + slides or recordings), closing card |
| Captions | WebVTT and SRT, from the narration, at most about 13 words each |

The full specification, with every value and the reasons behind them, is in `references/specs.md`.

## Prerequisites

- Python 3.11+ with `pip install pillow pygments imageio-ffmpeg edge-tts` (ffmpeg comes with `imageio-ffmpeg`).
- .NET 10 SDK, for the recorder (Playwright for .NET; run `pwsh <bin>/playwright.ps1 install chromium` once).
- A local site with realistic, fake data to record: see `references/recording.md`.
- Internet access for the voice (`edge-tts` calls the Microsoft Edge speech service).

## Workflow

### 1. Create a workspace outside the repository

Use the session's scratchpad or a temp folder, never the repository: frames and audio are large, and the recorder
must not pick up the repository's build settings.

```
<workspace>/
  storyboard.json   the chapters, clips and narration (see references/storyboard.md)
  slides.py         the slides, if the storyboard has "slide" clips
  recorder/         a copy of scripts/recorder, with your scenes in Scenes.cs
  audio/            generated: one mp3 per step
  durations.json    generated: the length of each step's narration
  clips/<clip>/     generated: the frames and timeline.json of each recording
  build/            generated: intermediate segments, and master.mp4
  <output>.mp4/.vtt/.srt   the result
```

Start from `examples/report-builder/`, and copy `scripts/recorder` into the workspace.

### 2. Write the storyboard

Read `references/storyboard.md` first. In short:

- Write for the long term: say "the X module", never "the new X", "now", "in this release", or version numbers, so
  the video stays accurate for years.
- One idea per step, one to three sentences, spoken in the present tense. A step is what the viewer sees while it plays.
- Chapters: why it exists, how it is organized, each task in the order a user does them, how to extend it.
- Name UI elements exactly as they're labeled on screen. Check the labels in the code or on the page first.

### 3. Generate the narration

```bash
python .agents/skills/crestapps-video-producer/scripts/narrate.py <workspace>
```

It writes `audio/` and `durations.json`. Run it again after any change to the text: only the changed steps are
generated again. **Record after narrating**, since the recorder holds each step for the length of its narration.

If the voice mispronounces a word, add it to `SPOKEN` in `scripts/narrate.py` (for every video) or to `"spoken"` in
the storyboard (for this one). The captions keep the written form. "CrestApps" is already spoken as "Crest Apps".
Check a spelling with `scripts/pronounce.py`, which shows the words the voice speaks and the gaps between them.

### 4. Record the clips

Write one method per "clip" in `recorder/Scenes.cs`, with one `scene.StepAsync("<step id>", ...)` per step, in the
storyboard's order. See `references/recording.md` for the site to record, the scene API, and the pitfalls. Then:

```bash
cd <workspace>/recorder && dotnet build
export VIDEO_ROOT=<workspace> VIDEO_BASE_URL=http://localhost:5330
export VIDEO_INIT_SCRIPT=<workspace>/mask.js   # optional: replaces leftover real names in a copied site
<bin>/Recorder setup          # logs in, once (or save state.json from a signed-in browser)
<bin>/Recorder record all     # or the names of some clips
python .agents/skills/crestapps-video-producer/scripts/review.py <workspace> <clip>
```

Look at every contact sheet (`shots/<clip>.jpg`) before assembling: the frame at the end of each step must show what
its narration describes, with no error message, empty list, or real person's data.

### 5. Assemble

```bash
python .agents/skills/crestapps-video-producer/scripts/assemble.py <workspace>
```

It prints the length and size. If it's over 25 MB, shorten the video or split it into parts; don't raise the CRF, which blurs small text. Extract a few frames with ffmpeg
and look at them: a recording, a slide with code, and a card.

### 6. Check for private data

Before publishing, OCR the video at one frame per second and search the text for real names, email addresses, phone
numbers and tokens. Fix the site or the mask, and record again; never publish a video that shows real data.

### 7. Publish

- **Documentation**: put `<name>.mp4`, `<name>.vtt` and a poster (`build/00-intro.png` saved as JPEG) in
  `src/CrestApps.Docs/static/img/docs/`, and embed it under the introduction of both the technical page and the
  user manual page:

  ```html
  <video controls preload="metadata" width="100%" poster="/img/docs/<name>.jpg" aria-label="Video overview of ...">
    <source src="/img/docs/<name>.mp4" type="video/mp4" />
    <track kind="captions" src="/img/docs/<name>.vtt" srcLang="en" label="English" default />
  </video>
  ```

  Introduce it with one sentence that says what it covers, without "new". Write `srcLang`, not `srclang`: the docs
  are MDX. Build the docs site to check the page.
- **Pull request**: GitHub only accepts videos through its web uploader, up to 10 MB on free plans. Attach shorter
  videos; link longer ones from the docs page.

### 8. Clean up

Stop the site you recorded (by its process ID, never by image name), and keep the workspace until the video is
accepted, in case a step must be re-recorded.

## References

- `references/specs.md`: the full specification.
- `references/storyboard.md`: the storyboard format, and how to write the narration and the slides.
- `references/recording.md`: the site to record, the recorder, the scene API, and pitfalls.
- `scripts/brand.py`: the branding, the layout, and the slide building blocks.
- `examples/report-builder/`: a complete storyboard, slides and scenes.
