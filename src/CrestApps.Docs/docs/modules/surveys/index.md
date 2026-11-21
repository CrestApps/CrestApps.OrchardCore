---
sidebar_label: Overview
title: Surveys
description: "The Surveys building block: surveys, polls, forms and quizzes with a designer, extensible question types, logic, immutable versions, collectors, access rules and limits, anonymity, scoring and grading, reports, data sets, recipes, workflows and an API."
user_manual:
  - user-manual/surveys/index
  - user-manual/surveys/create-a-survey
---

**Surveys** (`CrestApps.OrchardCore.Surveys`, with `Surveys.Abstractions` and `Surveys.Core`) is a building block for surveys, polls, forms and quizzes. It owns the survey definitions, their published versions, the question types (controls), logic, randomization, question banks, collectors, access rules and limits, responses, scoring and grading, results, and every integration. It needs no other product module. Other modules build on it: [Learning](../learning.md) runs ([how](./learning.md)) its course surveys and quizzes on Surveys.

## Features

| Feature | Id | Depends on | Adds |
| --- | --- | --- | --- |
| Surveys | `CrestApps.OrchardCore.Surveys` | `OrchardCore.Users`, CrestApps Resources | Surveys, versions, the designer, question types, logic, pools, question banks, web links and kiosks, access rules and limits, responses, scoring, grading, accommodations, reports and the admin pages |
| Survey Invitations | `CrestApps.OrchardCore.Surveys.Invitations` | Surveys, `OrchardCore.Notifications`, Outreach | Personal invitations with their own links, reminders and invitation notices |
| Survey Embedding | `CrestApps.OrchardCore.Surveys.Embedding` | Surveys, `OrchardCore.Contents` | The `SurveyPart`, the `SurveyWidget` content type and the `[survey]` shortcode |
| QTI Question Import | `CrestApps.OrchardCore.Surveys.Qti` | Surveys, `OrchardCore.Media` | QTI 2.1 and 3.0 import into question banks |
| Surveys API | `CrestApps.OrchardCore.Surveys.Api` | Surveys | JSON endpoints on the `Api` authentication scheme ([API](./api.md)) |
| Surveys AI | `CrestApps.OrchardCore.Surveys.AI` (module Surveys AI) | Surveys, CrestApps AI | AI tools, answer summaries, translation and scoring help ([AI](./ai.md)) |

Integrations turn on by themselves when their feature is on: Recipes and Deployment ([recipes](./recipes-and-deployment.md)), CrestApps Recipes (schemas), Workflows ([workflows](./workflows.md)), Reports ([results](./results-and-reports.md)), Data Pipelines ([data sets](./data-pipelines.md)), Audit Trail, Notifications, Webhooks, Rate Limits, reCAPTCHA, Attachments (the File upload question type) and Widgets and Shortcodes (embedding).

## Projects

| Project | Holds |
| --- | --- |
| `CrestApps.OrchardCore.Surveys.Abstractions` | Records (`Survey`, `SurveyDefinition`, `SurveyVersion`, `SurveyResponse`, `QuestionBank`, `BankQuestion`, `SurveyCollector`, `SurveyInvitation`, `SurveyOverride`...), contracts (`ISurveyManager`, `ISurveyResponseService`, `ISurveyControlHandler`, `ISurveyAccessRule`, `ISurveyResponseObserver`...), `SurveysConstants`, `SurveysPermissions` and the `AddSurvey*` registration helpers |
| `CrestApps.OrchardCore.Surveys.Core` | Stores and indexes, the version cache, the control registry and the built-in controls, the validator, differ and publisher, the logic and formula engines, the response runtime and access rules, scoring and grading, the answer index and its SQL aggregates |
| `CrestApps.OrchardCore.Surveys` | The module: admin pages, the runner, drivers and views, recipes, workflows, reports, the data source and the background tasks |
| `CrestApps.OrchardCore.Surveys.AI` | The Surveys AI module |

## Storage

Every record is a catalog record, one YesSql document each. Surveys, versions, banks, collectors, invitations, participation and accommodations live in the `Surveys` collection; responses and their answer index live in the `SurveyResponses` collection, so the large table is apart. Survey and question ids are generated once and never change; names are separate, editable codes.

A survey keeps its editable **draft** (`Survey.Draft`, a `SurveyDefinition`). Publishing makes an immutable `SurveyVersion`, cached in memory by id. A response stores its version id, the order of pages, questions and choices drawn for it (its plan) and its answers only. See [Versions](./versions.md).

Access settings, retention and anonymity (`Survey.Access`, `Survey.Retention`) are live: they apply without a new version.

## Admin pages

The **Surveys** menu holds **Surveys**, **Question banks**, **Grading** and **Templates**. The site settings are under **Settings > Surveys**. Reports are under **Reports > Surveys**. The User Manual walks through the screens: [Surveys](../../user-manual/surveys/index.md).

| Route (under the admin prefix) | Page |
| --- | --- |
| `surveys` | Survey list |
| `surveys/create` | New survey from a template |
| `surveys/templates` | Survey templates |
| `surveys/{id}/design` | Designer |
| `surveys/{id}/publish` | Publish with checks and changes |
| `surveys/{id}/versions` | Versions, compare and restore |
| `surveys/{id}/access` | Access and settings |
| `surveys/{id}/logic` | Logic overview |
| `surveys/{id}/collectors` | Links, kiosks and invitation lists |
| `surveys/{id}/responses` | Responses |
| `surveys/responses/{id}` | One response |
| `surveys/{id}/preview` | Preview of the draft |
| `surveys/{id}/overrides` | Accommodations |
| `surveys/{id}/translations` | Translations |
| `surveys/grading` | Grading queue |
| `surveys/banks` | Question banks |

## Permissions

`SurveysPermissions` lives in `Surveys.Abstractions`, so consumers check it without the core library.

| Permission | Name in the role editor | Implied by |
| --- | --- | --- |
| `ManageSurveys` | Manage all surveys | |
| `ManageOwnSurveys` | Manage own surveys | `ManageSurveys` |
| `PublishSurveys` | Publish, restore and close surveys | `ManageSurveys` |
| `ManageSurveyTemplates` | Manage survey templates | `ManageSurveys` |
| `ViewSurveyResponses` | View responses of all surveys | `ManageSurveys` |
| `ViewOwnSurveyResponses` | View responses of own surveys | `ViewSurveyResponses`, `ManageOwnSurveys` |
| `ExportSurveyResponses` | Export survey responses with respondent identity (security critical) | |
| `DeleteSurveyResponses` | Void and delete survey responses | `ManageSurveys` |
| `GradeSurveyResponses` | Grade survey responses | `ManageSurveys` |
| `ManageSurveyOverrides` | Manage survey accommodations | `ManageSurveys` |
| `ManageQuestionBanks` | Manage question banks | |
| `ViewQuestionBanks` | View question banks | `ManageQuestionBanks` |
| `ManageSurveyInvitations` | Manage survey invitations | `ManageSurveys` |
| `RespondToSurveys` | Respond to surveys | |
| `ManageSurveySettings` | Manage survey settings | |

Defaults: Administrator holds every permission; Editor holds Manage own surveys, View responses of own surveys and View question banks; Authenticated holds Respond to surveys. Public links need no permission.

Checks on one survey go through `ISurveyAuthorizationService`: the global permission, then the owner (`Survey.OwnerId`) for the *own* permissions, then every `ISurveyAccessProvider` ([Extending](./extending.md)).

## Site settings

**Settings > Surveys** (permission `ManageSurveySettings`) sets the defaults of new surveys (retention days, idle hours before an unfinished response is abandoned, minimum group size), the largest upload in MB, and the hosts an ending may redirect to besides the site. The settings travel in Orchard Core's `Settings` recipe step under `SurveySettings`.

## Privacy and security

- Answer keys (`SurveyQuestion.Scoring` and settings marked `[SurveyKey]`) never reach respondents: the runner, the evaluate endpoint and the API only see a respondent projection.
- **Anonymous** surveys store no user, party, IP address or invitation on the response. Limits count an HMAC of the respondent under the survey's secret in a participation record that holds no response id. Reports hide groups smaller than the survey's minimum group size (5 by default).
- A respondent who is not signed in resumes with a resume token (an HttpOnly cookie, hashed at rest).
- Visibility, branching, limits, deadlines, quotas and scores are decided on the server on every post.
- Author HTML is sanitized; piped values are HTML-encoded; redirects go only to the site or an allowed host; validation patterns run with a time limit.
- The personal data contributor exports a person's identified responses and invitations, and its erasure removes who answered, deletes their invitations, participation rows and uploaded files.

## Background tasks

| Task | Runs | Does |
| --- | --- | --- |
| Survey deadlines | Every minute | Submits responses whose time ran out (leased queue) |
| Survey maintenance | Hourly | Abandons idle unfinished responses, applies retention (delete or anonymize), removes old participation rows |
| Survey invitation reminders | Hourly (Invitations feature) | Sends the reminders that are due |
