---
sidebar_label: Designing surveys
title: Designing Surveys
description: "The survey definition: pages, questions, choices and rows, pools, branches, endings, variables, score categories and bands, quotas, and the presentation, navigation, scoring, review and timing settings, and how the designer edits them."
user_manual:
  - user-manual/surveys/create-a-survey
  - user-manual/surveys/add-questions
---

A survey's content is a `SurveyDefinition`. The draft (`Survey.Draft`) and every published version hold one. The designer only edits the draft; respondents see a definition only once it is published ([Versions](./versions.md)).

## Templates

A new survey starts from a template. Templates are `SurveyTemplate` catalog records (name, title, description, kind, icon, position and a `SurveyDefinition`), managed under **Surveys > Templates** (permission `ManageSurveyTemplates`). A new survey copies the template's definition with new ids; **Save as a template** on a survey's row menu copies its draft into a new template.

The built-in `ISurveyTemplateProvider` offers **Blank survey** (from code, always there) and every stored template. Surveys seeds **Customer satisfaction**, **Net Promoter Score**, **Employee engagement**, **Event feedback**, **Quiz** and **Poll** through a migration that runs `Migrations/survey-templates.recipe.json` when the feature is first enabled; their names are constants in `SurveyTemplateNames`. Learning Surveys seeds **Course feedback** (`LearningSurveysConstants.CourseFeedbackTemplate`) the same way from its own migration recipe. A module adds its own templates with a `SurveyTemplates` recipe step in a migration, or offers templates from code with `services.AddSurveyTemplateProvider<T>()` ([Extending](./extending.md)). Code that starts from a template that was removed gets a blank survey.

## The definition

| Member | Holds |
| --- | --- |
| `Title`, `Introduction` | What respondents see on the start page (sanitized HTML) |
| `Pages` | The pages in designed order; each has `Id`, `Name` (code), `Title`, `Description`, `Visibility`, `Questions`, `Pools`, `QuestionOrder` (`AsDefined` or `Shuffle`), `Branches`, `AllowBack` and `TimeLimitSeconds` |
| `Variables` | Hidden variables (from the consumer, the link, the collector or a default) and calculated ones (a formula) |
| `Endings` | Messages or redirects at the end; one is the default ([Logic](./logic.md)) |
| `ScoreCategories`, `ScoreBands` | Categories question points add to, and result messages by percent |
| `Quotas` | Counts of completed responses with an action once full ([Collecting](./collecting.md)) |
| `Presentation` | Title, question numbers, page titles, progress bar, questions per screen (0 keeps the pages; 1 shows one question at a time), page order, skip the start page, CSS class, redirect after the ending |
| `Navigation` | Back, forward only, auto-advance, resume, change after finishing, confirm before finishing |
| `Scoring`, `Review`, `Timing` | See [Scoring and grading](./scoring-and-grading.md) |
| `Translations` | Translated texts ([Translations](./translations.md)) |
| `Properties` | Versioned settings of consumers |

## Questions

A `SurveyQuestion` has a stable `Id`, a `Name` (the code used by piping, formulas and exports), a `ControlType` (a registered question type, see [Question types](./controls.md)), `Title`, `Description`, `Placeholder`, `Required`, `Pinned`, `PoolId`, `Visibility`, `RequiredWhen`, `EnabledWhen`, `Choices`, `ShuffleChoices`, carried-forward choices (`ChoicesFrom`, `ChoicesFromMode`), `Rows`, `Scoring` (the key, never sent to respondents), `Validation` rules, `Settings` (the question type's settings as JSON) and `Properties`.

A choice has an `Id`, an export `Value`, `Text`, `ImageUrl`, `IsOther` (shows a text box), `IsExclusive` (clears the others), `Pinned`, `Retired` (hidden from new respondents but kept for results) and its own `Visibility`.

Validation rules are `MinLength`, `MaxLength`, `Min`, `Max`, `Regex` (checked with a 100 ms time limit), `Email`, `Url` and `Formula`.

## Pools

A question pool on a page shows each respondent `DrawCount` of its questions (0 shows them all), optionally shuffled, and optionally **evenly presented**: least-presented questions are drawn first, with counters kept under a lock. A pool may also draw from question banks by bank, tags and difficulty; those draws are copied into the version when it is published ([Versions](./versions.md#bank-questions)).

## The designer

The designer page (`admin/surveys/{id}/design`) has an outline of pages and questions on the left and an editor panel on the right.

- Outline changes (add page, add question, move, duplicate, delete) post small JSON commands to `admin/surveys/{id}/draft/commands`; the server applies them to the draft and returns the outline.
- Pages are reordered by drag and drop or with the move buttons.
- The question editor is built with `IDisplayManager<SurveyQuestion>`, so any module adds settings to any question with a display driver. The built-in drivers edit the common fields, choices, rows, validation and scoring; each question type adds its own settings card ([Question types](./controls.md)).
- The survey settings are built with `IDisplayManager<SurveyDefinition>`: General, Presentation, Navigation, Variables, Endings, Score categories and bands, Scoring and review, Timing and Quotas. Their drivers place shapes on the tabs `General;1`, `Scoring;2`, `Endings;3`, `Variables;4` and `Quotas;5`; a module's driver joins a tab by name or adds its own with a later position.
- The question and page editors save through their own button without leaving the page. **Save draft** first saves the open question or page when it has changes, then posts the survey settings; **Save and publish** (`submit.Publish`) does the same and then redirects to the publish page. Leaving the designer, or opening another item, with unsaved changes asks first.
- The rule builder edits a `ConditionRule` with nested groups against the questions of earlier pages; the server checks every rule again.
- Draft saves use optimistic concurrency: when someone else saved the draft meanwhile, the editor asks to reload.
- **Duplicate** copies a survey with new ids everywhere and a new name.

The access settings (`admin/surveys/{id}/access`) and the logic overview (`admin/surveys/{id}/logic`, the rules in plain words with the problems to fix) are pages of their own.
