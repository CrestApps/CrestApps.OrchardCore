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
| Survey Invitations | `CrestApps.OrchardCore.Surveys.Invitations` | Surveys, `OrchardCore.Notifications`, Outreach | Personal invitations to users, user audiences, cohorts and e-mail addresses, with their own links, reminders and an unsubscribe link |
| Survey Embedding | `CrestApps.OrchardCore.Surveys.Embedding` | Surveys, `OrchardCore.Contents` | The `SurveyPart`, the `SurveyWidget` content type and the `[survey]` shortcode |
| QTI Question Import | `CrestApps.OrchardCore.Surveys.Qti` | Surveys, `OrchardCore.Media` | QTI 2.1 and 3.0 import into question banks |
| Surveys API | `CrestApps.OrchardCore.Surveys.Api` | Surveys | JSON endpoints on the `Api` authentication scheme ([API](./api.md)) |
| Surveys AI | `CrestApps.OrchardCore.Surveys.AI` (module Surveys AI) | Surveys, CrestApps AI | AI tools, answer summaries, translation and scoring help ([AI](./ai.md)) |

Survey Invitations depends on Outreach, the building block for bulk sends, so turning on Invitations turns on Outreach too. Every e-mail invitation and reminder is checked against Outreach's recipient policies (the opt-outs and consent every Outreach run honors) before it is sent, carries an unsubscribe link, and one invite reads at most 5,000 recipients; [collecting](./collecting.md#opt-outs-and-unsubscribes) has the details.

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
| `PublishOwnSurveys` | Publish, restore and close own surveys | `PublishSurveys` |
| `ManageSurveyTemplates` | Manage survey templates | `ManageSurveys` |
| `ViewSurveyResponses` | View responses of all surveys | `ManageSurveys` |
| `ViewOwnSurveyResponses` | View responses of own surveys | `ViewSurveyResponses`, `ManageOwnSurveys` |
| `ExportSurveyResponses` | Export survey responses with respondent identity (security critical) | |
| `DeleteSurveyResponses` | Void and delete survey responses | `ManageSurveys` |
| `DeleteOwnSurveyResponses` | Void and delete responses of own surveys | `DeleteSurveyResponses` |
| `GradeSurveyResponses` | Grade survey responses | `ManageSurveys` |
| `GradeOwnSurveyResponses` | Grade responses of own surveys | `GradeSurveyResponses` |
| `ManageSurveyOverrides` | Manage survey accommodations | `ManageSurveys` |
| `ManageOwnSurveyOverrides` | Manage accommodations of own surveys | `ManageSurveyOverrides` |
| `ManageQuestionBanks` | Manage question banks | |
| `ViewQuestionBanks` | View question banks | `ManageQuestionBanks` |
| `ManageSurveyInvitations` | Manage survey invitations | `ManageSurveys` |
| `ManageOwnSurveyInvitations` | Manage invitations of own surveys | `ManageSurveyInvitations` |
| `RespondToSurveys` | Respond to surveys | |
| `ManageSurveySettings` | Manage survey settings | |

Defaults: Administrator holds every permission; Editor holds Manage own surveys, Publish, restore and close own surveys, View responses of own surveys, Void and delete responses of own surveys, Grade responses of own surveys, Manage accommodations of own surveys and View question banks; Authenticated holds Respond to surveys. Public links need no permission.

**Manage own surveys** covers editing the user's own surveys (the draft, settings, collectors and versions) and nothing more. Publishing, voiding and deleting responses, grading, accommodations and invitations each need their own permission, either the global one or its *own* variant on the user's own surveys. Invitations send site e-mail, so no role but Administrator has **Manage invitations of own surveys** by default; grant it on purpose.

Other checks:

- The grading queue opens for users with a grading permission or with a grading scope that gives them a survey (course graders, for one); a survey's queue needs leave to grade that survey.
- The designer lists and attaches question banks only for users with **View question banks**; without it, a pool keeps the banks it already draws from.
- A template copies a survey's draft, answer keys included, only from a survey the user may edit.
- Uploaded files of a response open for staff with View or Void and delete responses, for the respondent, and for anyone the survey authorization service lets view or grade the survey's responses (the owner, course graders); managing them follows Void and delete responses the same way. Nobody opens the files of a deleted response.

Checks on one survey go through `ISurveyAuthorizationService`: the global permission, then the owner (`Survey.OwnerId`) for the *own* permissions, then every `ISurveyAccessProvider` ([Extending](./extending.md)).

## Site settings

**Settings > Surveys** (permission `ManageSurveySettings`) sets the defaults of new surveys (retention days, idle hours before an unfinished response is abandoned, minimum group size), the smallest minimum group size any anonymous survey may use (`MinimumGroupSizeFloor`, 5 by default), the largest upload in MB, the hosts an ending may redirect to besides the site, and the most invitation and reminder e-mails the site sends a day (`InvitationEmailsPerDay`, 2,000 by default, counted from midnight in the site's time zone; invitations over it are held back and the hourly reminder task sends them on a later day, and invitations to users, which go to their notifications, don't count; the limit is exact across servers under a lock per site day, which on a web farm needs the Redis Lock feature; see [Invitations](./collecting.md#invitations)). The settings travel in Orchard Core's `Settings` recipe step under `SurveySettings`.

## Privacy and security

- Answer keys (`SurveyQuestion.Scoring` and settings marked `[SurveyKey]`) never reach respondents: the runner, the evaluate endpoint and the API only see a respondent projection.
- **Anonymous** surveys store no user, party, IP address, invitation, properties or identifying context on the response. Limits count an HMAC of the respondent under the survey's secret, kept as Data Protection ciphertext, in a participation record that holds no response id and no time finer than the day ([Anonymity](./collecting.md#anonymity)). Every output hides groups smaller than the survey's minimum group size, never below the site's floor (5 by default), and shows anonymous times by the day only.
- Who responded shows under one rule everywhere: never on an anonymous survey, and on an identified survey to whoever may read or grade its responses ([who responded](./results-and-reports.md#who-responded)).
- Personal data erasure finds a person's participation under every key it is stored under (`u:{id}` or its hash, the person's invitations, and `a:{hmac}` on each anonymous survey) and clears the context, variables and properties of their responses, and the respondent key an in-progress response is counted under, so a later deadline completion can't write their participation back. Recipe exports leave the respondent key out, like the resume token hash, and imports ignore it. The person's invitations are those sent to their user account and guest invitations sent to one of their e-mail addresses (matched without regard to case), with their `i:` participation keys and the responses linked to them.
- A respondent who is not signed in resumes with a resume token (an HttpOnly cookie, hashed at rest).
- Visibility, branching, limits, deadlines, quotas and scores are decided on the server on every post.
- Author HTML (the introduction, page descriptions, question titles and descriptions, feedback, text blocks and ending messages, with their translations) is sanitized with Orchard Core's HTML sanitizer twice: on save, by `SurveyContentSanitizer` in the survey, version, template and bank question stores, so the editors, recipes, templates, banks, QTI, spreadsheet and Moodle imports and AI drafts all go through it; and again when the start page, the runner, Preview, the review page, the embedded survey and the API show it. Which translations hold HTML follows the key's kind (`survey:introduction`, `page-description:`, `title:`, `description:`, `ending-message:`), so a translation whose source text is empty is sanitized too. The `SurveyContentSanitizingMigrations` migration asks for the `Surveys.SanitizeContent` resumable step (the **Resumable upgrade steps** background task), which re-saves, once and a page at a time, the drafts, published versions, templates and bank questions the sanitizer changes, so the tenant's activation never waits on it. Texts shown encoded (titles of surveys, pages and endings, choices, rows) are kept as written.
- Piped values are HTML-encoded with their braces encoded; an ending renders the author's Liquid first and pipes afterwards, so a respondent's text never runs as Liquid ([Logic](./logic.md#branching-and-endings)); redirects go only to the site or an allowed host; validation patterns run with a time limit.
- The choices of question types whose answer key is an order (`ISurveyControlScorer.AnswerKeyIsOrder`, such as Ranking) always start shuffled, the API definition shuffles them, and the public results page lists them by text, so the designed order (the key) is never shown.
- The personal data contributor exports a person's identified responses and invitations, and its erasure removes who answered, deletes their invitations, participation rows and uploaded files.

## Background tasks

| Task | Runs | Does |
| --- | --- | --- |
| Survey deadlines | Every minute | Submits responses whose time ran out (leased queue) |
| Survey upkeep | Hourly | Abandons idle unfinished responses, applies retention (delete, or anonymize: who responded and every file of the response are removed), submits responses past their deadline, and removes old participation rows in batches until it catches up (up to 30 seconds a run). Each response is handled in its own child shell scope, whose session commits on its own, so a response whose save fails never takes the others' changes with it, and its files, observers and notifications only run after its own commit. A response that fails five runs in a row is logged and no longer tried (`SurveyResponse.UpkeepFailures`); a step that succeeds resets the count, failures from the server's environment (an unreadable anonymity secret, a timeout, a transient database error) aren't counted, and the responses list shows the stopped responses with an **Upkeep stopped** filter and a **Try again** action (`SurveyResponseService.ClearUpkeepFailuresAsync`). |
| Survey invitation reminders | Hourly (Invitations feature) | Sends the reminders that are due. Each invitation list is handled in its own child shell scope that commits on its own, so a list that fails is logged and never takes the other lists' updates with it |

## Known gaps

- The **Survey Response Matches Condition** workflow event takes its rule as JSON, and its optional formula is not evaluated yet.
- The accommodation grant form takes a user name rather than the user picker, because Surveys does not depend on the CrestApps Users module.
- Invitations are sent by Surveys and its reminder task rather than as Outreach runs, so they don't show in Outreach's run history; they honor the same opt-outs ([collecting](./collecting.md#opt-outs-and-unsubscribes)).
- QTI import copies every image of an item, feedback images included, into the public media library (`surveys/qti/{import}`), so anyone with an image's address can open it.
- The Excel reader of bank spreadsheet imports is a private copy of the one in ContentTransfer.OpenXml; a shared reader is still to be extracted.
