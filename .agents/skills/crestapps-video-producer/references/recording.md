# Recording

## The site to record

Record a local site with realistic but fake data, never a production site:

- Copy the `App_Data` of a demo site into the workspace, and run the solution's `src/Startup/CrestApps.OrchardCore.Cms.Web`
  on a free port (5330 by default, `VIDEO_BASE_URL` otherwise) with `ORCHARD_APP_DATA` pointing at the copy. Block
  outbound calls with `HTTP_PROXY`/`HTTPS_PROXY` set to a dead address, so the copy never calls a phone, SMS or
  payment provider.
- Enable the features the video needs on the copy, and seed its data (reports, settings) with `fetch` calls in a
  page, outside the capture.
- Sign in once outside the capture, and save the session as `state.json` in the workspace (`Recorder setup` with
  `VIDEO_USER_NAME`/`VIDEO_PASSWORD`, or a Playwright `storageState` from a browser that is already signed in to the
  copy).
- A copied site can still hold real names, emails or numbers in old records. Set `VIDEO_INIT_SCRIPT` to a script that
  replaces them with fake ones in every page (text, attributes and input values), and check the result by OCR.
- Use fake names such as "Jamie Chen" and `customer@example.com`; never record real people, accounts, credentials or
  tokens.

### Snapshots

Clips run in order, each one starting from the state the previous one left. Before recording, put the site in the
starting state (features enabled, settings saved) with a `prepare` command that you add to the recorder's `Program.cs`, then copy
its `App_Data` folder and `state.json` aside. To re-record everything, stop the site, restore the copy, and start
it again. To re-record one clip, make sure the site is in the state that clip expects.

## The recorder

`scripts/recorder` is a Playwright for .NET console app. Copy it into the workspace, add your clips to `Scenes.cs`,
and build it there (its output goes to a short temp path, as Playwright's files exceed `MAX_PATH` under deep folders
on Windows).

```bash
export VIDEO_ROOT=<workspace>           # where state.json, durations.json and clips/ are
export VIDEO_BASE_URL=http://localhost:5330
Recorder setup                          # logs in, and saves the session in state.json
Recorder probe /Admin/Features "() => document.title"   # inspects a page (PROBE_SHOT=x.png saves a screenshot)
Recorder record all                     # or: Recorder record 03-create 04-manage
```

In Git Bash on Windows, set `MSYS_NO_PATHCONV=1`, or paths such as `/Admin/Features` are turned into Windows paths.

## The scene API (`Scene.cs`)

A clip method creates a scene, prepares the page, starts the capture, and runs one step per narrated step:

```csharp
await using var scene = await NewSceneAsync(browser, directory, durations);
await scene.OpenAsync("/Admin/reports/designs");   // not recorded
await scene.StartCaptureAsync();                  // recording starts

await scene.StepAsync("create-1", async () =>
{
    await scene.ClickAsync(scene.Page.Locator("button.create"), pauseAfter: 900);
});
```

| Method | Use |
|---|---|
| `OpenAsync(path)` | Opens a page before the capture starts. |
| `StartCaptureAsync()` | Starts recording, after a still moment. |
| `StepAsync(id, actions)` | Marks the start of a step, runs its actions, and holds until its narration ends. Keep the actions shorter than the narration. |
| `MoveToAsync(locator, offsetX, offsetY)` | Moves the cursor smoothly to an element. |
| `ClickAsync(locator)` / `DoubleClickAsync` / `RightClickAsync` | Moves, then clicks, with an amber ripple. |
| `DragToAsync(from, to, offsetX, offsetY)` | Drags an element onto another, such as a field onto a drop zone (HTML5 drag and drop works). |
| `SelectAsync(select, valueOrLabel)` | Picks an option of a native drop-down list. A headless browser draws no open list, so the click shows a ripple and the value changes. |
| `ClickAndWaitForNavigationAsync(locator)` | Clicks something that loads a new page, and keeps capturing it. |
| `WaitForNavigationAsync(action)` | Runs an action that loads a new page (a key press, a form submit). |
| `NavigateAsync(path)` | Goes to a page during the capture. |
| `TypeAsync(locator, text, delay)` | Clicks a field, and types into it, visibly. |
| `ScrollAsync(dx, dy)` / `DragAsync(locator, x, y)` | Scrolls, drags. |
| `HighlightAsync(locator, caption)` / `ClearHighlightsAsync()` | Draws an amber ring, with an optional caption, around what the narration talks about. |
| `SwitchToAsync(page)` | Records another tab, such as one opened by a link with `target="_blank"`. |
| `PauseAsync(ms)` / `HoldAsync(seconds)` | Waits, while frames keep coming. |

## Pitfalls

- **Narrate first.** The recorder reads `durations.json` to hold each step; recording before narrating gives clips
  whose steps are too short. If the narration of a step grows afterwards, record its clip again, unless it still
  ends before the next step starts.
- **Navigation stops the screencast**: always navigate with `ClickAndWaitForNavigationAsync`,
  `WaitForNavigationAsync` or `NavigateAsync`, or the rest of the clip is frozen.
- **Expand menus before `StartCaptureAsync`** when opening them isn't part of the story.
- **Seed data outside the capture**, with `fetch` calls in the page, rather than by clicking through forms on camera.
- **Highlights are fixed-position**: clear them before scrolling.
- **Look at every contact sheet** (`review.py`): an error message or an empty list is easy to miss in the logs.
- **Rebuild the site** after changing the code under it, and stop it before building the solution (it locks DLLs).
- **Keep a step's actions shorter than its narration.** A step that runs longer only adds a pause, but the frame at
  the end of the narration then shows the middle of the actions.
- **Pages redraw.** A builder that re-renders after every change replaces the elements a locator found; the scene
  API waits for them to be shown again, so always use locators, never element handles.
- **A text box changes on blur.** Press Tab after typing a value that only applies when the field loses focus.
- **Clips change the site.** Record from a snapshot (copy `App_Data` aside, restore it before a full take), and
  record in the order the clips depend on each other, which can differ from the order they play in.
