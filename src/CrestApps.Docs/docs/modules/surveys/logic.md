---
sidebar_label: Logic
title: Survey Logic
description: "Visibility rules for pages, questions and choices, required and enabled rules, branching, endings, piping, formulas, variables and randomization, all decided on the server."
user_manual:
  - user-manual/surveys/logic
---

Logic uses the shared [Conditions](../content-fields.md) model (`ConditionRule`, with nested groups and the full operator list) and Orchard Core's data pipeline formula language. The server decides on every page post; the browser only spares a round trip.

## Visibility

Pages, questions and choices carry a `SurveyVisibility`: `Always`, `ShowWhen` or `HideWhen` with a rule, plus an optional `Formula` that must also hold (evaluated on the server only).

Condition sources:

| Source | Reads |
| --- | --- |
| `{questionId}` | The answer to a question |
| `{questionId}.{rowId}` | One row of a matrix or similar |
| `var:{name}` | A variable |
| `score`, `score:{categoryId}` | The response's score, or a category's. It is set when the response completes, before the ending is chosen, so ending rules read this response's score. While the response is in progress (also after a reopen, which clears the earlier result) it is empty, so page, question and branch rules on it don't see a score |
| `context:{key}` | A value a consumer provides through `ISurveyConditionSourceProvider` |
| `collector` | The collector the response came through |
| `culture` | The response's culture |

A question on a hidden page is hidden, and a hidden question answers nothing, so a chain of rules closes when an early answer changes (`ConditionalVisibility.GetHidden`). After every page post, the answers of questions that are now hidden are removed from the response.

`RequiredWhen` decides whether an answer is required; `EnabledWhen` decides whether the question can be answered (otherwise it shows read-only).

Publishing refuses a rule that reads a question on a later page, a cycle of rules, a source that does not exist, or a choice that does not exist.

### In the browser

The runner script (`survey-runner.js`) evaluates plain rules of the current page with the shared JS evaluator (`crestapps-conditions`). When a rule on the page uses a formula, a calculated variable or piping, it posts the page's answers to `/s/r/{responseId}/evaluate` and shows what the server returns.

## Branching and endings

When a page is completed, its `Branches` are evaluated in order; the first that holds goes to a **later** page or to an ending. Without a branch, the next visible page in the plan follows. Skipped pages leave the page path and their answers are dropped. Going back (when the survey and the page allow it) saves the page without checking it.

After the last page, the response is scored and then the first ending whose condition holds is used, else the default ending, so an ending on `score` reads this response's score. A branch that goes to an ending names it directly. An ending with the outcome **Screened out** ends the response as screened out. Ending messages are Liquid with sanitized HTML; a redirect goes only to the site or a host listed in the site settings.

An ending message renders in this order (`LiquidSurveyEndingRenderer`):

1. The message the author wrote is rendered as Liquid, with `Survey` (`Name`, `Title`), `Response` (`Number`, `Status`, `Percent`, `Passed`), `Answers` (each answer by question name, through the type's `FormatAnswer`) and `Score`. Every value Liquid writes is HTML-encoded, with `{` and `}` encoded too.
2. `{name}` references are piped in, HTML-encoded with their braces encoded.
3. The result is sanitized.

A respondent's text never reaches the Liquid parser: an answer such as `{{ Site.SiteName }}` shows as written. An ending message that holds Liquid is not sanitized when it is saved (the sanitizer would encode its operators); its output is.

## Piping

`{name}` in titles, descriptions, choice texts, ending messages and redirect URLs shows an earlier answer (through the type's `FormatAnswer`), HTML-encoded with `{` and `}` encoded too, so a piped answer never reads as a reference or as Liquid (URL-encoded in redirect URLs). Also `{name.row}`, `{var:name}`, `{score}`, `{score:category}` and `{percent}`. An unknown name shows nothing; publishing checks that names exist and come earlier.

## Formulas and variables

Formulas use Orchard Core's formula language (see the Data Pipelines formula reference). A formula reads `[name]` for a question or a variable (names are unique across both), `[name.row]`, `[score]` and `[score:category]`. Survey formulas also have these functions, which pipeline editors do not list:

| Function | Returns |
| --- | --- |
| `SELECTED([question], value)` | Whether a choice is chosen |
| `COUNTSELECTED([question])` | How many choices are chosen |
| `ANSWERED([question])` | Whether the question has an answer |
| `SCORE('question name')` | The question's score |
| `AGE([date])` | Whole years since a date, on the site's calendar (`NOW()` and `TODAY()` are the site's date and time too, never UTC) |

A module adds functions for survey formulas with an `ISurveyExpressionFunctionProvider` in `Surveys.Core`. Compiled formulas are cached per version. Calculated variables are computed after every page post in dependency order; a cycle is a publish error. A failing formula logs a warning and reads as missing; it never fails the respondent's page.

Hidden variables take their value, in this order, from the consumer that starts the response, the link's query string (only variables marked **From the link**), the collector's values, then the default.

## Randomization

When a response starts, its plan is drawn with a seed and kept on the response, so refresh, back and resume show the same thing:

- page order (shuffled pages keep the first and last in place; a survey with a branch to a page keeps its designed page order, because a branch skips the pages between it and its target, and publishing warns about it with `shuffle-with-branches`);
- per page, the pool draws, then the question shuffle (pinned questions keep their place);
- per question, the choice order (pinned, other and clearing choices keep their place);
- one question per screen when **Questions per screen** is 1.

Draws use a small deterministic generator (`SurveyRandom`, xoshiro128**), so a seed gives the same plan on every runtime.
