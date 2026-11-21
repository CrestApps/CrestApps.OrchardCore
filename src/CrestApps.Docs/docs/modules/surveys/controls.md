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

The constants are in `SurveyControlNames`. A question whose type is not registered (its module is off) shows a placeholder to staff, is left out for respondents, and is reported when publishing.

## Capabilities

A type declares what it can do with `SurveyControlCapabilities` flags. The designer, the runner and the reports read the flags, never the name.

| Flag | Meaning |
| --- | --- |
| `Answerable` | Collects an answer |
| `UsesChoices` | The editor shows the choice list; shuffle, carry forward and choice visibility apply |
| `UsesRows` | The editor shows rows |
| `Scorable` | Has a scorer |
| `ManuallyGraded` | Its scorer may leave the score to a grader |
| `ConditionSource` | Other questions may test its answer |
| `SupportsOther` | Supports an other choice with a text box |
| `SupportsValidation` | Supports validation rules |
| `Aggregatable` | Has an analyzer for results |
| `StoresFiles` | Stores uploaded files |
| `ClientScript` | Ships a script that raises `survey:change` |

## Building a custom question type

This example adds a star-shaped "Thumbs" question type in a module named `Acme.Surveys`.

### 1. Register the type

```csharp
services.AddSurveyControl<ThumbsControlHandler>("Acme.Thumbs", descriptor =>
{
    descriptor.DisplayName = LocalizationSource.Create<ThumbsControlHandler>("Thumbs up or down");
    descriptor.Category = SurveyControlCategories.Rating;
    descriptor.Icon = "fa-solid fa-thumbs-up";
    descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.ConditionSource;
    descriptor.SettingsType = typeof(ThumbsSettings);
});

services.AddDisplayDriver<SurveyQuestion, ThumbsSettingsDisplayDriver>();
```

Add `AddSurveyControlScorer<T>()` and `AddSurveyControlAnalyzer<T>()` when the type scores or summarizes answers, and `AddSurveyControlPreset(name, baseControl, configure)` for a preset of an existing type.

### 2. Write the handler

Derive from `SurveyControlHandlerBase<TSettings>` (or `SurveyChoiceControlHandlerBase<TSettings>` for choice types) and override only what differs. The base reads `q.{questionId}` into `Answer.Values`, reports a missing required answer, gives the values to conditions, formats the values as text, writes one export column and one index row per value.

```csharp
public sealed class ThumbsControlHandler : SurveyControlHandlerBase<ThumbsSettings>
{
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

Read and write settings with `question.GetSettings<T>()` and `question.SetSettings(settings)`. Mark key data on a settings class with `[SurveyKey]` so the respondent projection leaves it out.

### 3. Add the settings editor

```csharp
public sealed class ThumbsSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<ThumbsSettings>
{
    protected override string ControlType => "Acme.Thumbs";
}
```

The driver renders the shape `SurveyControlSettings` with the alternate for the type, so the view is `Views/SurveyControlSettings-Acme.Thumbs.Edit.cshtml`, with the model `SurveyControlSettingsViewModel<ThumbsSettings>`. Put the fields in an `ocat` card, like the built-in editors.

### 4. Add the views

The runner renders each question with the `SurveyControl` shape in a display type, with alternates for the type (a dot in the name becomes an underscore in the shape name and a dot again in the file name), the survey name and the question name:

| View | Display type |
| --- | --- |
| `SurveyControl.Respond-Acme.Thumbs.cshtml` | Respond: the inputs |
| `SurveyControl.Review-Acme.Thumbs.cshtml` | Review: the answer, read-only, with feedback as the review settings allow |
| `SurveyControl.Print-Acme.Thumbs.cshtml` | Print (optional) |
| `SurveyControl.Summary-Acme.Thumbs.cshtml` | Summary (optional) |

The model is `SurveyControlViewModel`: `Question` (the respondent projection), `Settings`, `Answer`, `Prefix` (the input name, `q.{questionId}`), `Choices` in plan order, `Errors`, `ReadOnly`, `Feedback`, `Culture`, and `InputId`, `HintId` and `ErrorId` for the ARIA attributes. A template uses `fieldset` and `legend` for groups, labels every input, sets `aria-required`, `aria-invalid` and `aria-describedby`, renders its own errors under `ErrorId`, and works without JavaScript. Inputs for rows are named `q.{questionId}.{rowId}` and the other text `q.{questionId}.other`.

### 5. Add a script (optional)

A type with its own UI (drag, draw, slide) sets `ClientScript`, writes hidden inputs under its prefix, and dispatches the change event so the runner re-evaluates the page:

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

`ISurveyControlScorer` has `Score` (a result without points means a grader scores it), `MaxPoints` and `GetFeedback`. `ISurveyControlAnalyzer` has `SummarizeAsync` (aggregates from `ISurveyAnswerAggregates`) and `ToReportSections`; an analyzer hides groups smaller than `context.MinimumGroupSize`.
