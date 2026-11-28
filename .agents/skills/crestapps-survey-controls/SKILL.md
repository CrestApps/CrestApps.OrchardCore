---
name: crestapps-survey-controls
description: >
  Adds a survey question type (control) to the CrestApps Surveys building block from another module: registering it
  with AddSurveyControl (descriptor, capabilities, settings type), the handler (SurveyControlHandlerBase<TSettings> or
  SurveyChoiceControlHandlerBase<TSettings>), an optional scorer and analyzer, presets, the settings display driver
  (SurveyControlSettingsDisplayDriver<TSettings>), the Respond/Review/Print/Summary views of the SurveyControl shape,
  client scripts that raise survey:change, the answer key projection rule, and the tests to add. Use when a module needs
  a new kind of survey or quiz question, or when changing how a built-in question type reads, scores or summarizes
  answers.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps survey controls

A question type (in code, a *control*) is registered by name. Everything Surveys does with a question goes through its
handler: no code outside the type's own folder switches on its name (an architecture test enforces it for the built-in
types). A module adds a type without changing Surveys.

Docs: `src/CrestApps.Docs/docs/modules/surveys/controls.md`. Reference types:
`src/Core/CrestApps.OrchardCore.Surveys.Core/Controls/` (one folder per type, such as `Slider/` for a simple type,
`SingleChoice/` for a choice type, `LongText/` for a manually graded type), their registration in
`src/Core/CrestApps.OrchardCore.Surveys.Core/SurveysControlsServiceCollectionExtensions.cs`, their settings drivers in
`src/Modules/CrestApps.OrchardCore.Surveys/Drivers/Controls/` and their views in
`src/Modules/CrestApps.OrchardCore.Surveys/Views/`.

## What to add

| Piece | Required | Where |
| --- | --- | --- |
| `services.AddSurveyControl<THandler>(name, configure)` | Yes | Your startup (reference `Surveys.Abstractions`) |
| Handler deriving from `SurveyControlHandlerBase<TSettings>` | Yes | Your Core or module |
| Settings class (POCO) | When the type has settings | Next to the handler |
| `services.AddDisplayDriver<SurveyQuestion, TDriver>()` with `SurveyControlSettingsDisplayDriver<TSettings>` | When the type has settings | Your module |
| `Views/SurveyControlSettings-{Name}.Edit.cshtml` | When the type has settings | Your module |
| `Views/SurveyControl.Respond-{Name}.cshtml` | Yes (answerable types) | Your module |
| `Views/SurveyControl.Review-{Name}.cshtml` | Yes | Your module |
| `Views/SurveyControl.Print-{Name}.cshtml`, `SurveyControl.Summary-{Name}.cshtml` | No | Your module |
| `AddSurveyControlScorer<T>()` with `ISurveyControlScorer` | When `Scorable` | |
| `AddSurveyControlAnalyzer<T>()` with `ISurveyControlAnalyzer` | When `Aggregatable` | |
| `AddSurveyControlPreset(name, baseControl, configure)` | For a preset of an existing type (like Nps of Rating) | |
| A script that dispatches `survey:change` | When `ClientScript` | Your module's `Assets.json` |

In file names, a dot in the type name stays a dot: the shape alternate replaces `.` with `_`
(`SurveyControlShapes.Encode`), and Orchard Core maps `_` back to `.` in the file name. So `Acme.Thumbs` uses
`SurveyControl.Respond-Acme.Thumbs.cshtml` and `SurveyControlSettings-Acme.Thumbs.Edit.cshtml`.

## Descriptor

`SurveyControlDescriptor`: `Name` (unique, open string), `DisplayName` and `Description` (`LocalizationSource`, never
serialized), `Category` (`SurveyControlCategories`: Choice, Text, Number, Date, Rating, Matrix, Content, Files,
Assessment, Advanced, or your own), `Icon` (Font Awesome class), `Position`, `Capabilities`, `SettingsType`.

### Capabilities (`SurveyControlCapabilities`)

| Flag | Effect |
| --- | --- |
| `Answerable` | Collects an answer (a text block does not) |
| `UsesChoices` | The editor shows the choice list; shuffle, carry forward and choice visibility apply |
| `UsesRows` | The editor shows rows |
| `Scorable` | Has a scorer; the scoring card shows when the survey is scored |
| `ManuallyGraded` | The scorer may return no points, leaving the response Pending a grader |
| `ConditionSource` | Other questions' rules may read its answer |
| `SupportsOther` | An other choice with a text box |
| `SupportsValidation` | The validation card shows |
| `Aggregatable` | Has an analyzer for results |
| `StoresFiles` | Stores uploaded files (attachments owned by the response) |
| `ClientScript` | Ships a script that raises `survey:change` |

## Handler contract (`ISurveyControlHandler`)

`SurveyControlHandlerBase<TSettings>` implements every member; override what differs. `SurveyChoiceControlHandlerBase<TSettings>`
adds choice order (pinned, other and exclusive choices keep their place), carried-forward choices, exclusive and other
handling, and an index row per chosen choice.

| Member | Default in the base | Override when |
| --- | --- | --- |
| `ControlType` | (abstract) | Always |
| `Initialize(question)` | Default settings | New questions need choices or rows |
| `ValidateDefinitionAsync(question, context)` | No checks | Publish-time checks (`context.AddError`/`AddWarning`) |
| `PreparePresentation(question, context)` | Nothing | Per-response order, with the seeded `context.Random` into `context.Plan` |
| `GetRespondentSettings(question)` | Settings minus `[SurveyKey]` properties | Settings hold more secrets |
| `ReadAnswerAsync(context)` | Reads `q.{id}` values into `Answer.Values` | Rows, other text, files, JSON bodies |
| `ValidateAnswerAsync(context)` | Required check and validation rules | Ranges and type checks |
| `IsEmpty(question, answer)` | No non-blank values | Rows, numbers, files |
| `GetConditionValue(question, answer, rowId)` | The values | Rows or computed values |
| `GetFormulaValue`, `GetFormulaType` | Text | Numbers, dates, booleans |
| `FormatAnswer(question, answer, context)` | The values joined | Choice texts, labels |
| `DescribeColumns`, `WriteColumns` | One column `{name}` | Rows (`{name}.{row}`) and other text (`{name}.other`) |
| `GetIndexRows(question, answer)` | One row per value | Numbers, choices, rows; leave out personal data |

Settings are read with `question.GetSettings<T>()` and written with `question.SetSettings(settings)`
(`SurveyQuestionSettingsExtensions`, PascalCase JSON, enums as strings).

## Scorer and analyzer

- `ISurveyControlScorer`: `Score(question, answer, context)` returns `SurveyScoreResult` (`SurveyScoreResult.Of(points, max)`,
  or `SurveyScoreResult.Manual(max)` for a grader); `MaxPoints`; `GetFeedback` (correct, right answer text, feedback
  texts). Use `context.PointsOf(question)` for the question's points. The key is `question.Scoring` (and `Scoring.Key`
  for custom data).
- `ISurveyControlAnalyzer`: `SummarizeAsync(question, context, ct)` reads SQL aggregates from `context.Aggregates`
  (`ISurveyAnswerAggregates`: choice counts per row, number statistics, text samples) into `SurveyQuestionSummary`;
  `ToReportSections` returns Reports sections (bars, tables, metrics). Hide groups smaller than
  `context.MinimumGroupSize`.

## Views and shapes

The runner renders a `SurveyQuestion_Respond` wrapper (number, title, description with id `HintId`) around the
`SurveyControl` shape with display type `Respond`, `Review`, `Print` or `Summary`, alternates from
`SurveyControlShapes.GetAlternates`: `SurveyControl_{DisplayType}__{Type}`, then `__{SurveyName}` and `__{QuestionName}`.

The model is `SurveyControlViewModel` (`Surveys.Core/ViewModels`): `Question` (the respondent projection
`SurveyQuestionView`), `Settings` (`GetSettings<T>()`), `Answer`, `Prefix` (`q.{questionId}`), `Choices` (visible, in
plan order), `Errors`, `HasErrors`, `ReadOnly`, `Feedback`, `Culture`, `InputId`, `HintId`, `ErrorId`, `DescribedBy()`.

A Respond template:

- uses `fieldset`/`legend` for groups and labels every input;
- sets `aria-required`, `aria-invalid` and `aria-describedby`;
- renders its own errors under `ErrorId` (the wrapper does not);
- names inputs `q.{id}`, `q.{id}.{rowId}`, `q.{id}.other`;
- works without JavaScript (plain form post; the form is multipart for files).

The settings editor model is `SurveyControlSettingsViewModel<TSettings>`. The driver passes its localizer to the base
constructor and names its card with `CardName` (`S["Thumbs Settings"]`); the base places the shape in that card, so the
view holds only its `ocat` rows, with no card or fieldset markup.

## Client scripts

A script writes hidden inputs under the prefix, then:

```js
element.dispatchEvent(new CustomEvent('survey:change', { bubbles: true, detail: { questionId } }));
```

The runner listens on the form, evaluates plain rules in the browser and asks the server for formulas and piping. It
raises `survey:page-loaded` when it inserts a new page. No jQuery; ES module bundled by the asset CLI.

## Rules

- The answer key never reaches respondents: never put scoring data in respondent settings unless it is marked
  `[SurveyKey]`; views, the evaluate endpoint and the API only see the projection.
- Never switch on a control name outside its own folder; read descriptor capabilities instead.
- Leave contact details and other personal data out of index rows.
- Registration of a type that needs another feature (like FileUpload with Attachments) goes in a `[RequireFeatures]`
  startup.

## Tests

Add, per type: read, validate, condition value, formula value, format, columns, index rows, score (when scorable) and
summary sections (when aggregatable). Keep the key-leak test green (the projection of every type with a full key must
not contain a key value). Built-in examples: `tests/CrestApps.OrchardCore.Tests/Surveys/Controls/`.
