---
sidebar_label: Question types
title: Question Types
description: "The built-in survey question types (controls), what each can do, and how a module adds its own question type with a handler, a settings driver and views."
user_manual:
  - user-manual/surveys/add-questions
---

Every question has a **question type** (in code, a *control*), registered by name. Everything a type does with its questions and answers goes through its handler: reading the form, checking answers, condition values, piping text, scoring, export columns, the answer index and the results summary. No code outside the type switches on its name, so a module adds a type without changing Surveys.

## Built-in question types

| Name | Shown as | Category | Scored | Notes |
| --- | --- | --- | --- | --- |
| `SingleChoice` | Single choice | Choice | Yes | Right choice or points per choice; other choice; carried-forward choices |
| `MultipleChoice` | Multiple choice | Choice | Yes | All or nothing or partial, penalties; exclusive and other choices |
| `Boolean` | Yes or no | Choice | Yes | Yes/no, true/false, toggle or consent box |
| `Ranking` | Ranking | Choice | Yes | Right order, partial credit; drag list script |
| `ShortText` | Short text | Text | Yes | Accepted answers, with or without case |
| `LongText` | Long text | Text | By a grader | Word limits; grader guidance |
| `MultipleText` | Multiple text | Text | No | One text per row |
| `ContactInfo` | Contact information | Text | No | Name, e-mail, phone, company, address; kept out of the answer index |
| `Number` | Number | Number | Yes | Right number with a tolerance |
| `Slider` | Slider | Number | Yes | Script shows the value |
| `ConstantSum` | Constant sum | Number | No | Numbers that must add up to a total |
| `Date` | Date and time | Date | No | Date, time or both, with limits |
| `Rating` | Rating | Rating | No | Stars, numbers or smileys; presets **Net Promoter Score** (`Nps`, 0 to 10) and **Customer satisfaction** (`Csat`, 1 to 5), whose results show the NPS and CSAT scores |
| `Matrix` | Matrix | Matrix | Yes | Rows by columns, one or several per row |
| `MatrixDropdown` | Drop-down matrix | Matrix | No | A drop-down in each cell |
| `Matching` | Matching | Assessment | Yes | Prompts matched to answers |
| `FillInBlanks` | Fill in the blanks | Assessment | Yes | Text with `[[answer\|other]]` blanks |
| `ChoiceBlanks` | Choose the missing words | Assessment | Yes | Drag into text or a list in each blank, with wrong words |
| `TextBlock` | Text block | Content | No | Text and media; nothing to answer |
| `Hidden` | Hidden value | Advanced | No | A variable's value |
| `Calculated` | Calculated value | Advanced | No | A formula over earlier answers |
| `Signature` | Signature | Advanced | No | Drawn on a canvas; saved as a file when Attachments is on |
| `FileUpload` | File upload | Files | By a grader | Registered only when the Attachments feature is on; files are attachments owned by the response |

Files and drawn signatures are kept only when a page is posted forward and passes validation. A live evaluation, an autosave, an immediate-feedback check and going back read the answers in `SurveyAnswerReadMode.Draft`, where a handler ignores new files; a page posted forward reads them in `SurveyAnswerReadMode.Commit`, where a handler hands each file back in `SurveyAnswerReadContext.PendingFiles` and the runtime stores it once the page passes. After the update is saved, the files the response no longer refers to (an unticked file, a replaced signature) are removed; deleting a response removes all of its files. The **Remove survey files no answer kept** background task removes, every hour, the files of responses that no answer has kept for a day, such as those of a request that failed after storing them.

The constants are in `SurveyControlNames`. A question whose type is not registered (its module is off) shows a placeholder to staff, is left out for respondents, and is reported when publishing.

## Capabilities

A type declares what it can do with `SurveyControlCapabilities` flags. The designer, the runner and the reports read the flags, never the name.

| Flag | Meaning | Read by |
| --- | --- | --- |
| `Answerable` | Collects an answer | The runner (a type without it shows no answer area), required checks |
| `UsesChoices` | The editor shows the choice list; shuffle, carry forward and choice visibility apply | The designer, the publish checks, spreadsheet imports, results filters |
| `UsesRows` | The editor shows rows | The designer, spreadsheet imports |
| `Scorable` | Has a scorer | The designer's scoring card, the publish checks |
| `ManuallyGraded` | Its scorer may leave the score to a grader | The scoring card, grading, AI scoring help |
| `ConditionSource` | Other questions may test its answer | The rule builder offers only these questions as rule sources |
| `SupportsOther` | Supports an other choice with a text box | The choices editor |
| `SupportsValidation` | Supports validation rules | The validation card |
| `Aggregatable` | Has an analyzer for results | The registry returns a type's analyzer only with this flag, so the results report summarizes only these types |
| `StoresFiles` | Stores uploaded files | The runner posts a page as `multipart/form-data` only when one of its questions stores files |
| `ClientScript` | Ships a script that raises `survey:change` | The runner loads the type's `ScriptResourceName` at the foot of every page that shows the type |
| `NotShownToRespondents` | Respondents never see it nor answer it (Hidden value) | The respondent's review leaves it out; the admin Details page shows it |

When the tenant starts, Surveys logs a warning for every registration that can't work: a type whose handler reports another `ControlType` (the type would be missing from the picker, the runner and the results), a handler no type is registered for, an analyzer of a type without `Aggregatable`, and a `ClientScript` type without a `ScriptResourceName`.

## Building a custom question type

This example adds a "Thumbs up or down" question type in a module named `Acme.Surveys`. In short:

1. Reference the **CrestApps.OrchardCore.Surveys.Core** package (the handler, settings driver and view model base classes live there, not in Abstractions) and add `CrestApps.OrchardCore.Surveys` to the feature's `Dependencies` in your manifest.
2. Write a settings class. Mark answer-key data `[SurveyKey]`.
3. Write the handler, `ThumbsControlHandler : SurveyControlHandlerBase<ThumbsSettings>`, with an `(IStringLocalizer<ThumbsControlHandler> stringLocalizer) : base(stringLocalizer)` constructor.
4. Register `AddSurveyControl<ThumbsControlHandler>(name, ...)` and `AddDisplayDriver<SurveyQuestion, ThumbsSettingsDisplayDriver>()`, where the driver derives from `SurveyControlSettingsDisplayDriver<ThumbsSettings>`.
5. Add the views `SurveyControlSettings-{Name}.Edit`, `SurveyControl-{Name}.Respond` and `SurveyControl-{Name}.Review`.
6. If the type has its own UI, set `ClientScript` and `ScriptResourceName`, define the script in a resource manifest, and fire `survey:change` from it.

### 1. Reference Surveys and depend on it

```xml
<PackageReference Include="CrestApps.OrchardCore.Surveys.Core" />
```

```csharp
[assembly: Feature(
    Id = "Acme.Surveys",
    Name = "Acme Survey Types",
    Dependencies =
    [
        "CrestApps.OrchardCore.Surveys",
    ]
)]
```

`Surveys.Core` brings `Surveys.Abstractions` (the records, `ISurveyControlHandler`, `SurveyControlDescriptor` and the `AddSurvey*` helpers).

### 2. Register the type

```csharp
services.AddSurveyControl<ThumbsControlHandler>("Acme.Thumbs", descriptor =>
{
    descriptor.DisplayName = LocalizationSource.Create<ThumbsControlHandler>("Thumbs up or down");
    descriptor.Category = SurveyControlCategories.Rating;
    descriptor.Icon = "fa-solid fa-thumbs-up";
    descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.ConditionSource | SurveyControlCapabilities.ClientScript;
    descriptor.ScriptResourceName = "acme-survey-thumbs";
    descriptor.SettingsType = typeof(ThumbsSettings);
});

services.AddDisplayDriver<SurveyQuestion, ThumbsSettingsDisplayDriver>();
```

The name passed to `AddSurveyControl` and the handler's `ControlType` must be the same string. Add `AddSurveyControlScorer<T>()` and `AddSurveyControlAnalyzer<T>()` (with `Aggregatable`) when the type scores or summarizes answers, and `AddSurveyControlPreset(name, baseControl, configure)` for a preset of an existing type.

### 3. Write the handler

Derive from `SurveyControlHandlerBase<TSettings>` (or `SurveyChoiceControlHandlerBase<TSettings>` for choice types) and override only what differs. The base reads `q.{questionId}` into `Answer.Values`, reports a missing required answer, gives the values to conditions, formats the values as text, writes one export column and one index row per value. Its constructor takes the localizer it uses for its messages.

```csharp
using CrestApps.OrchardCore.Surveys.Core.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;

public sealed class ThumbsControlHandler : SurveyControlHandlerBase<ThumbsSettings>
{
    public ThumbsControlHandler(IStringLocalizer<ThumbsControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string ControlType => "Acme.Thumbs";

    public override string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
        => answer?.Values.FirstOrDefault() switch
        {
            "up" => question.GetSettings<ThumbsSettings>().UpLabel,
            "down" => question.GetSettings<ThumbsSettings>().DownLabel,
            _ => string.Empty,
        };
}

public sealed class ThumbsSettings
{
    public string UpLabel { get; set; } = "Yes";

    public string DownLabel { get; set; } = "No";
}
```

Read and write settings with `question.GetSettings<T>()` and `question.SetSettings(settings)`. Mark key data on a settings class with `[SurveyKey]` so the respondent projection leaves it out. A settings property named `Html` holds author HTML: it is sanitized on save, like the text block's.

### 4. Add the settings editor

```csharp
public sealed class ThumbsSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<ThumbsSettings>
{
    public ThumbsSettingsDisplayDriver(IStringLocalizer<ThumbsSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    protected override string ControlType => "Acme.Thumbs";

    protected override LocalizedString CardName => S["Thumbs Settings"];
}
```

The driver renders the shape `SurveyControlSettings_Edit__Acme_Thumbs`, so the view is `Views/SurveyControlSettings-Acme.Thumbs.Edit.cshtml`, with the model `SurveyControlSettingsViewModel<ThumbsSettings>`. The question editor shows it in a card named by `CardName`, after the cards every question has, so the view holds only its `ocat` rows: no card, fieldset or heading markup.

### 5. Add the views

The runner renders each question with the `SurveyControl` shape in a display type, with the alternates `SurveyControl_{DisplayType}__{Name}`, then `__{SurveyName}` and `__{QuestionName}` overrides (a dot or a dash in a name becomes an underscore in the shape name). Name the views **`SurveyControl-{Name}.{DisplayType}.cshtml`**, with the display type last:

| View | Display type |
| --- | --- |
| `SurveyControl-Acme.Thumbs.Respond.cshtml` | Respond: the inputs |
| `SurveyControl-Acme.Thumbs.Review.cshtml` | Review: the answer, read-only, with feedback as the review settings allow |
| `SurveyControl-Acme.Thumbs-customer_feedback.Respond.cshtml` | Respond, only in the survey named `customer-feedback` |

Orchard Core reads the text after the **last** dot of a view name as its display type. The form the built-in views use, `SurveyControl.Respond-Boolean.cshtml`, works only for a name without a dot: `SurveyControl.Respond-Acme.Thumbs.cshtml` would be read as the display type `Thumbs`, and respondents would see "This question type is not available".

The model is `SurveyControlViewModel`: `Question` (the respondent projection), `Settings`, `Answer`, `Prefix` (the input name, `q.{questionId}`), `Choices` in plan order, `Errors`, `ReadOnly`, `Feedback`, `Culture`, and `InputId`, `HintId` and `ErrorId` for the ARIA attributes. A template uses `fieldset` and `legend` for groups, labels every input, sets `aria-required`, `aria-invalid` and `aria-describedby`, renders its own errors under `ErrorId`, and works without JavaScript. Inputs for rows are named `q.{questionId}.{rowId}` and the other text `q.{questionId}.other`.

Surveys shows questions in the Respond and Review display types only; there is no print or results display type to add.

### 6. Add a script (optional)

A type with its own UI (drag, draw, slide) sets `ClientScript` and `ScriptResourceName`, and defines the script in a resource manifest of its module:

```csharp
public sealed class AcmeSurveysResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest = new();

    static AcmeSurveysResourceManagementOptionsConfiguration()
        => _manifest
            .DefineScript("acme-survey-thumbs")
            .SetUrl("~/Acme.Surveys/scripts/thumbs.min.js", "~/Acme.Surveys/scripts/thumbs.js");

    public void Configure(ResourceManagementOptions options)
        => options.ResourceManifests.Add(_manifest);
}

services.AddTransient<IConfigureOptions<ResourceManagementOptions>, AcmeSurveysResourceManagementOptionsConfiguration>();
```

The runner registers the script once at the foot of every page that shows the type, so the Respond view adds no script tag. The script writes hidden inputs under the question's prefix and dispatches the change event so the runner re-evaluates the page:

```js
element.dispatchEvent(new CustomEvent('survey:change', { bubbles: true, detail: { questionId } }));
```

The runner raises `survey:page-loaded` when it inserts a new page, so a script can attach to new content.

## The handler contract

| Member | Called when |
| --- | --- |
| `Initialize(question)` | A question of the type is added |
| `ValidateDefinitionAsync(question, context)` | The survey is published; add errors or warnings |
| `PreparePresentation(question, context)` | A response starts; set the choice order with the seeded `context.Random` |
| `GetRespondentSettings(question)` | The respondent projection is built; return settings without key values |
| `ReadAnswerAsync(context)` | A page is posted (form or JSON) |
| `ValidateAnswerAsync(context)` | A page is posted with Next or Finish |
| `IsEmpty(question, answer)` | Required checks and the answered counts |
| `GetConditionValue(question, answer, rowId)` | Rules read the answer |
| `GetFormulaValue`, `GetFormulaType` | Formulas read the answer |
| `FormatAnswer(question, answer, context)` | Piping, exports, notifications, workflow inputs |
| `DescribeColumns`, `WriteColumns` | Exports and the wide data set |
| `GetIndexRows(question, answer)` | A response is completed; rows feed the SQL aggregates |
| `RemovePersonalData(question, answer)` | A person's data is erased or retention anonymizes a response; return the answer to keep, or `null` to drop an answer that names the person. The default keeps the answer; Contact information, Signature and Hidden drop theirs |

`ISurveyControlScorer` has `Score` (a result without points means a grader scores it), `MaxPoints`, `GetFeedback` and `AnswerKeyIsOrder` (`false` by default). A type whose answer key is an order of its choices, such as Ranking, returns `true`: its choices then always start shuffled in a respondent's plan, the API definition shuffles them and the public results page lists them by text. `ISurveyControlAnalyzer` has `SummarizeAsync` (aggregates from `ISurveyAnswerAggregates`) and `ToReportSections`; an analyzer hides groups smaller than `context.MinimumGroupSize`.
