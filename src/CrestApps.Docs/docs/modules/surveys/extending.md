---
sidebar_label: Extending
title: Extending Surveys
description: "The extension points of Surveys: question types, access rules, observers, context kinds, grading scopes, access providers, import formats, condition sources, response fields, templates and validation contributors."
user_manual:
  - user-manual/surveys/index
---

Surveys never names its consumers. A module plugs in with these registrations from `Surveys.Abstractions` (`SurveysServiceCollectionExtensions`):

| Registration | Contract | For |
| --- | --- | --- |
| `AddSurveyControl<T>(name, configure)` | `ISurveyControlHandler` | A question type ([Question types](./controls.md)) |
| `AddSurveyControlScorer<T>()` | `ISurveyControlScorer` | Scoring a question type |
| `AddSurveyControlAnalyzer<T>()` | `ISurveyControlAnalyzer` | Summarizing a question type in results |
| `AddSurveyControlPreset(name, baseControl, configure)` | | A preset of a type, such as NPS |
| `AddSurveyAccessRule<T>()` | `ISurveyAccessRule` | Deciding whether a response may start or complete |
| `AddSurveyResponseObserver<T>()` | `ISurveyResponseObserver` | Hearing response events |
| `AddSurveyObserver<T>()` | `ISurveyObserver` | Hearing survey events (published, closed, status changed, deleted) |
| `AddSurveyGradingScope<T>()` | `ISurveyGradingScope` | Letting users grade some surveys without the global permission |
| `AddSurveyAccessProvider<T>()` | `ISurveyAccessProvider` | Granting operations on some surveys without global permissions |
| `AddSurveyImportFormat<T>()` | `ISurveyImportFormat` | Reading question bank files |
| `AddSurveyConditionSourceProvider<T>()` | `ISurveyConditionSourceProvider` | Values for `context:{key}` condition sources |
| `AddSurveyResponseFieldProvider<T>()` | `ISurveyResponseFieldProvider` | Fields of the `SurveyResponses` data set |
| `AddSurveyTemplateProvider<T>()` | `ISurveyTemplateProvider` | Survey templates |
| `AddSurveyDefinitionValidationContributor<T>()` | `ISurveyDefinitionValidationContributor` | Publish checks |
| `AddSurveyContextKind(kind, displayName)` | | A record kind responses may belong to, for filters |

## Starting responses for your records

Call `ISurveyResponseService.StartAsync` with a `StartResponseRequest`: the survey (or a collector token), the respondent identity, a `Context` (`RecordRef`, such as `Ticket:{id}`), variables and properties. Responses stay pinned to their version; the context and properties travel to observers, workflows, webhooks and data sets. `ISurveyLinkService` makes public and personal links.

## Observers

Every `ISurveyResponseObserver` method does nothing by default: `StartedAsync`, `PageCompletedAsync`, `CompletingAsync` (before the save; observers may add properties), `CompletedAsync`, `ScreenedOutAsync`, `AbandonedAsync`, `NeedsGradingAsync`, `GradedAsync`, `ScoreChangedAsync`, `VoidedAsync`, `DeletedAsync`, `ReopenedAsync` and `OverrideGrantedAsync`. The methods ending in *-ed* run after the change is saved.

## Access rules

An `ISurveyAccessRule` has a `Name`, an `Order` (the built-in rules use 100 to 1000, see [Collecting](./collecting.md#access-rules)), `CheckStartAsync` and `CheckCompleteAsync`. Return `SurveyAccessDecision.Allow` or `SurveyAccessDecision.Deny(code)`.

## Reading responses

`ISurveyResponseQueries` finds a response by id, pages responses by survey, user, context, status, grading, version and collector, and counts a survey's responses. `ISurveyVersionCache` reads versions, `ISurveyAnswerAggregates` reads aggregates, and `ISurveyManager` creates and changes surveys (including `ListByOwnerAsync` for surveys a record owns).
