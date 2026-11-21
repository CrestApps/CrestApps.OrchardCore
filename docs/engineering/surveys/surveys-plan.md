# Surveys: a general-purpose survey, form and quiz platform

Status: proposed 2026-11-20; decisions answered by Mike the same day (section 24). Implemented on branch `ma/surveys-module-31dcec` (section 26). Written for an implementing engineer (or model) working phase by phase. Section 25 lists facts to check in phase 0. Section 26 is the progress log the implementer keeps up to date.

**Ground rules from Mike (2026-11-20):** the LMS was never released or used in production, so Learning can change freely and **nothing is migrated**: no upgrade migrations, no data conversion, no compatibility shims and no breaking-change notes for Learning. Where Mike left a choice open, follow industry standard and state-of-the-art products; the plan records the choice made.

Read first, in this order: `AGENTS.md` (Building Blocks and Reuse, Admin Index pages, Admin Edit pages, Dates and Times), `docs/engineering/building-blocks.md`, `docs/engineering/reusability-audit-2026-11.md`, `docs/engineering/learning/lms-plan.md` (sections 7.5, 10 and 19), `.agents/skills/crestapps-admin-index-views/SKILL.md`, `.agents/skills/crestapps-admin-edit-views/SKILL.md`, `.agents/skills/crestapps-report-data-source/SKILL.md`.

## 1. Why, and what "done" means

The LMS (`d4f7733b9`, #1343) ships two survey-like engines that cannot be used outside a course:

- **Learning Surveys** (`Learning.Surveys`): four question types in a closed enum stored inline in `SurveyPart`, one response per enrollment, answers stored as option **text**. It has no recipe step, deployment step, data source, workflow events or reports.
- **Learning Assessments** (`Learning.Assessments`): 13 question types as content items, an options-based type registry (`AddQuestionType<TScorer>`) that the player, the form reader and the grading view still bypass with hardcoded switches, plus attempts, draws, deadlines, grading, overrides and item analysis.

Surveys have many uses beyond learning: customer satisfaction, NPS, employee engagement, event feedback, registrations, polls, inspections, post-call and post-ticket surveys, certification exams outside a course. The Contact Center report catalog already waits on "survey responses" for CSAT and NPS (`src/CrestApps.Docs/docs/contact-center/report-catalog.md:392`).

**Goal.** One building block, **Surveys** (`CrestApps.OrchardCore.Surveys[.Abstractions|.Core]`), owns survey definitions, versions, question (control) types, logic, randomization, question banks, collection (links, invitations, embeds), access rules and limits, responses, scoring and grading, results, and every integration (recipes, deployment, CrestApps recipe schemas, workflows, reports, Data Pipelines, notifications, webhooks, audit trail, personal data). It works with no LMS feature enabled. Learning Surveys and Learning Assessments are rebuilt on top of it, so the solution keeps **one** question engine.

**Done means:**

1. A site enables **Surveys** only, designs a multi-page survey with conditional logic, randomized questions and a question pool, publishes it, shares a link, collects anonymous or identified responses within daily and weekly limits, and reads the results in Reports and in Data Pipelines.
2. The same site builds a graded quiz with a pass mark, a time limit, attempt limits, manual grading of essays and a review policy.
3. A developer adds a new control type from another module with one registration, one handler, one settings driver and three views, and nothing in Surveys changes (section 8).
4. A survey is edited after responses exist: in-progress and completed responses keep the exact version they started on, and results join versions by stable question ids.
5. Every LMS survey, Choice activity and quiz runs on Surveys; `Learning.Surveys` keeps only the course-item adapter, and `Learning.Assessments` keeps only the LMS-specific parts (course item, course grading scope, gradebook, LMS imports).
6. Docs: technical and user manuals, the feature reference, the changelog and a **Surveys** card on the docs home page.

## 2. What the market does

### 2.1 Capabilities and what we take from each

| Platform | Strength we copy | Notes |
| --- | --- | --- |
| **Qualtrics** | Draft and publish with immutable versions; in-progress respondents stay on the version they started; Survey Flow (blocks, branch, randomizer "present N of M", evenly present, embedded data, end of survey); quotas with an action when met; scoring categories; anonymize responses (no IP, no contact link); export of the display order shown to each respondent | Its prevent-multiple-submissions setting is cookie-only. Scoring applies retroactively. |
| **SurveyMonkey** | Collectors (web link, email, embed), each with a cutoff date, response limit, password, IP allow/block, one or many responses per respondent; feedback by score range; quiz results display toggles; AI response analysis; warnings on editing a survey that has responses | Editing a live survey reloads respondents' pages and loses answers. We avoid that with versions. |
| **Google Forms** | Publish and unpublish states; responder controls by user and group; quiz points, answer key, feedback per answer, release grades now or later; response validation (number, length, regex); section jump by answer; Gemini summaries | No response pinning to a version. |
| **Microsoft Forms** | Start and end dates, one response per person (signed-in), record names, shuffle questions with locked ones, branching per answer (forward only), practice mode, Copilot drafting | |
| **SurveyJS** (open source) | The reference for an **extensible question model**: JSON schema as the model, specialized types (presets of a base type), composite types, custom types, custom properties, `visibleIf`/`enableIf`/`requiredIf`, choice filtering, `{name}` piping, calculated values, triggers, `completedHtmlOnCondition`, expression functions registry | We copy the shape of the model, not the client-side-only execution. |
| **LimeSurvey** | Server-side expression language, quotas, once-only participant tokens | |
| **Jotform** | Unique submission (cookie/IP) versus unique field (by an answer value), kiosk mode, offline mode | |
| **Moodle** (already studied for LMS) | Attempts, grading method, review options matrix, question banks, random draws, item analysis | Already in Learning Assessments; moves here. |

### 2.2 Question types across the market

About 12 primitives cover 90% of real surveys: choice, text, number, date, rating, matrix, ranking, file, boolean, content (HTML), calculated, repeating group. NPS, CSAT, contact info, consent, image choice and constant sum are presets or composites of those. Section 8.6 lists what we build and when.

### 2.3 What we take from an earlier survey module (reference only)

An earlier, private survey module (reviewed while writing this plan; ask Mike for access) is an old content-item design. **Take the ideas, not the design.**

Ideas to keep:

- Visibility rule groups with operators, at page, question and choice level, and three modes: **always visible, show when, hide when**.
- Clearing the answers of hidden questions until nothing changes (a cleared answer can un-trigger other rules). The shared `ConditionalVisibility.GetHidden` already resolves chains; we also drop hidden answers on the server.
- Random pools ("randomly show N of M") and shuffling that is stable for a response across refresh, back and resume.
- Responses pinned to a survey version.
- Daily and weekly per-respondent limits (week start from the site setting) and per-weekday time windows (allow all, prevent all, between two times).
- Anonymous surveys.
- `CompletingAsync` and `CompletedAsync` hooks feeding workflows, notifications and the pipeline change queue.
- One data set per survey with a column per question for Data Pipelines and the Report Builder.
- A "below threshold" alert (an answer under a value raises a workflow event and a dashboard widget). We generalize it as the **Response matches condition** event (section 17).
- Daily retention cleanup of old and incomplete responses.

Design problems **not to repeat** (all seen in that module):

1. **Answers mixed with definitions.** Each response copied whole pages, questions, options and rules. Documents grew and every index walked the tree. We store answers only, and pin an immutable version.
2. **Unstable ids.** BagPart children got new ids on publish, so rules matched sources by display name. Our ids are generated once and never change; names are separate, editable variable codes.
3. **Closed type list in practice.** The rule engine, reset logic, report code and JS mirror switched on known type names. Everything we do with an answer goes through the control handler (section 8).
4. **Rendering as the data layer.** Report cells came from rendering display shapes. Our exports and aggregates come from handler methods and SQL over an answer index.
5. **Reports loading every response at once.** We page by 500 and aggregate in SQL.
6. **A second rule engine in JS with integer operator codes.** We reuse the shared Conditions evaluator and its JS mirror (moved to Resources, section 5.2) with string operators and shared parity tests.
7. **Limits checked only at start** (two tabs exceed a limit) and only for completed responses. We check at start and again at completion under a lock.
8. **"Anonymous" still saving the user id**, and anonymous responses resumable by anyone who has the id. We store no user for anonymous responses and require a resume token.
9. **Trusting browser-reported timings and scores.** Deadlines and scores are server-side.
10. **Back required a valid page.** Back saves without validating.
11. **Restrictions hardcoded in the manager.** Ours are pluggable access rules.
12. Specific bugs: inverted min/max selection check, an index mapping the user id as the interview id, a text index storing the wrong version id.

### 2.4 What we take from Learning (already in this repo)

- From **Assessments**: the scorers for 13 question types (`src/Core/CrestApps.OrchardCore.Learning.Assessments.Core/Scoring/*.cs`), the start/save/check/submit/deadline flow (`DefaultAssessmentAttemptService`), the review options matrix (`QuizReviewOptions`, `ReviewShows`, `ReviewMoment`), immediate feedback mode, overrides with a reason, the score history with actor and reason, regrade, void, the grading queue with a scope, the leased deadline queue (`AttemptDeadlineWork` on `LeasedWorkItemIndexSchema`), `ItemAnalysisCalculator`, the QTI reader, the per-item answer index for item analysis, and the rule that the key and the feedback never reach the browser before the review policy allows.
- From **Learning Surveys**: results visibility for respondents (Never, AfterAnswer, AfterClose, Always), close date, change answer after submit, the single-question "Choice" poll, the reusable answer checks in `SurveyRules`.
- **Not** taken: content items as questions (they needed a handler that **fails** `ViewContent` so keys would not leak through the content API, GraphQL and search), the hardcoded `AttemptFormReader` and `_AttemptQuestion.cshtml` switches, answers stored as option text, and the quiz grading method and navigation read live from the quiz instead of pinned.

### 2.5 Versioning best practice (applied in section 11)

1. Keep the draft separate from immutable published versions.
2. Pin each response to its version when it starts.
3. Give questions and choices stable ids that are never reused.
4. Retire choices instead of deleting them when they have answers.
5. Analyze across versions by joining on stable ids.
6. Warn the author about destructive changes before publishing.
7. Allow re-scoring older versions with a corrected key, with an audit entry.

## 3. Design principles

1. **A building block, not a feature of Learning.** Surveys never names its consumers. Consumers attach through open string kinds, `RecordRef` contexts, a `Properties` bag on records, observers, access rules and display drivers.
2. **Versioned definitions, answer-only responses.** A published version is an immutable document cached in memory. A response stores its version id, its presentation plan and its answers.
3. **Everything about a control goes through its handler.** Reading the form, validation, condition values, piping text, scoring, export columns, aggregates and rendering are per-type extension points. No `switch` on a control type outside its own handler (an architecture test enforces this, section 22).
4. **The server decides.** Visibility, branching, randomization, limits, deadlines, scores and quotas are computed on the server. Client script only spares respondents a round trip; the server re-evaluates on every post.
5. **Reuse the platform.** Conditions, formulas, Users audiences, Parties, Records, Attachments, Notifications, Outreach, Reports, Data Pipelines, Signed tokens, Transition tables, Personal data, catalogs and their recipe and deployment bases (section 4).
6. **Private by design.** Anonymous means no user, no IP, no party and no link between participation and answers. Answer keys are never serialized to respondents.
7. **Fast.** Immutable versions cached by id, aggregates computed in SQL over an answer index written once at completion, pages of 500 everywhere, no rendering in data paths.
8. **Standing rules.** Admin lists on `<admin-list>`, forms with `action-bar-bottom` and `ocat-*` cards, breadcrumbs on every admin page, local times through `ILocalClock`, one public type per file, sealed classes, XML docs, `S["..."]` localization, `IClock` not `DateTime.UtcNow`, `IdGenerator.GenerateId()` ids.

## 4. Reuse map

### 4.1 Concepts owned by this solution

| Concept | Owner | How Surveys uses it |
| --- | --- | --- |
| Stored records and admin CRUD | CrestApps.Core catalogs, `DocumentCatalog<T,TIndex>` (`src/Core/CrestApps.OrchardCore.YesSql.Core/Services/DocumentCatalog.cs`), `ICatalogManager<T>`, `CatalogAdminController<T>`, `AuditedCatalogEntryHandlerBase` | Every Surveys record (section 7) |
| Recipe and deployment steps | `CatalogRecipeStep<T>`, `NamedCatalogRecipeStep<T>`, `CatalogDeploymentSource`, `NamedCatalogDeploymentSource`, `SummaryDeploymentStepDisplayDriver<T>` (`src/Core/CrestApps.OrchardCore.Core/Recipes/`, `Deployments/`); schemas via `IRecipeStep` (`src/Core/CrestApps.OrchardCore.Recipes.Core/IRecipeStep.cs`) and `IDeploymentStepSchemaDefinition` | Section 16. Template: `src/Modules/CrestApps.OrchardCore.Agreements/` |
| "Show when" logic | Platform Conditions (`src/Abstractions/CrestApps.OrchardCore.Abstractions/Conditions/`: `ConditionRule`, `Condition`, `ConditionOperator`, `ConditionValue`, `ConditionEvaluator`, `ConditionalVisibility.GetHidden`/`FindCycle`), `ConditionRuleSchema` (`src/Core/CrestApps.OrchardCore.Recipes.Core/Schemas/ConditionRuleSchema.cs`), JS mirror in `src/Modules/CrestApps.OrchardCore.ContentFields/Assets/js/conditional-fields.js` | Visibility, enable, required, branching, endings, quotas, the condition event. Extended in section 5.1; the JS mirror moves to Resources in section 5.2 |
| Formulas | Orchard Core Data Pipelines formula language (`OrchardCore.DataSources.Expressions`: `ExpressionCompiler`, `IExpressionScope`, `IExpressionFunctionCatalog`, `ExpressionFunction`) | Calculated variables, advanced conditions, score formulas (section 9.4). No new expression language |
| Who responds | **Users** (`IDisplayNameProvider`), audiences (`IUserAudienceService`, `AddUserAudienceRuleSource`), cohorts (`ICohortService`) | Survey visibility by audience, invitations to an audience, the Participation report |
| Respondent who is not a user | **Parties** (`PartyRef { Kind, Id }`, `IPartySource`, `IPartyDirectory`) | `SurveyResponse.Respondent`, invitations to contacts and customers |
| What a response is about | **Records** (`RecordRef { Kind, Id }`, `IRecordSource`, `IRecordDirectory`, `RecordPicker`) | `SurveyResponse.Context` (an enrollment, a ticket, a call, an order) and `Survey.Owner` (a course item) |
| Uploaded files | **Attachments** (`IAttachmentService`, `IAttachmentAccessProvider` per owner kind, signed links) and the `FileDropInput` shape (Resources) | The File upload control; owner kind `SurveyResponse` |
| Signed links | `SignedTokenService` (`CrestApps.OrchardCore.Core`) | Personal invitation links and resume links |
| Status moves | `TransitionTable<TEnum>` | Response status |
| Leased background work | `LeasedWorkItemIndexSchema` and the leased queue in `CrestApps.OrchardCore.YesSql.Core/WorkQueues/` | Deadline auto-submit, invitation reminders, retention |
| Notices to users | **Notifications** (`INotificationService`, `AddNotificationType`) and `ITemplatedEmailSender` for non-users (memory rule: e-mail only for non-users) | Invitations, reminders, graded, needs grading |
| Bulk sends | **Outreach** (`IOutreachEngine`, audience source, channel sender, template renderer, recipient policy) | Invitation batches and reminders (section 13.3) |
| Analytics | **Reports** (`IReport`, `AddReport<T>`, `ReportDocument`, `ReportSection`, `IReportExportFormat`, report filter drivers) | Results, responses, scores, item analysis, funnel, participation, NPS trend. No analytics pages outside Reports (audit rule) |
| Data sets | Orchard Core `IDataSource` with `RecordDataSource` and `IndexedReportDataSet<T,TIndex>` (`src/Core/CrestApps.OrchardCore.Reports.Core/DataSources/IndexedReportDataSet.cs`) | Section 18. Template: `src/Modules/CrestApps.OrchardCore.Approvals/Reports/` |
| CSV and Excel | `CsvWriter`, `CsvReader` (`CrestApps.OrchardCore.Tabular`) | Question bank import, response export (formula-injection guard included) |
| QR codes | QRCoder (already referenced by `CrestApps.OrchardCore.Pdf.Core`) | Collector QR codes as SVG |
| Privacy | **PersonalData** (`IPersonalDataContributor`, `AddPersonalDataContributor`) | Export and erasure of a respondent's responses |
| AI | **AI** (`AddCoreAITool`, `Templates/Prompts/*.md`, `ITemplateService`) | Surveys.AI (phase 8) |
| Telemetry | `CrestAppsOrchardCoreTelemetry` meters and activity sources | Own operations only (memory rule) |

### 4.2 Capabilities owned by Orchard Core

| Capability | Orchard Core | Use |
| --- | --- | --- |
| Workflows | `EventActivity`, `TaskActivity`, `IWorkflowManager.TriggerEventAsync` | Section 17. Only add activities; never change the engine |
| Recipes and deployment | `AddRecipeExecutionStep`, `AddDeployment` | Section 16 |
| Data Pipelines and Report Builder | `AddDataSource<T>()` | Section 18 |
| CAPTCHA | `OrchardCore.ReCaptcha` | The CAPTCHA access rule, behind `[RequireFeatures]` |
| Rate limits | `OrchardCore.RateLimits` route group limits | Public start and answer endpoints |
| Audit trail | `OrchardCore.AuditTrail` | Section 19 |
| Liquid | `ILiquidTemplateManager` | Ending messages, invitation e-mails |
| HTML sanitizing | `IHtmlSanitizerService` | Every author-supplied HTML (descriptions, text blocks, endings) |
| Shortcodes, widgets | `OrchardCore.Shortcodes`, `OrchardCore.Widgets` | Embedding (section 13.4) |
| Admin dashboard | `OrchardCore.AdminDashboard` | Widgets: open grading, recent responses, condition alerts |

### 4.3 What Surveys owns

The survey, its draft, its immutable versions, the control (question type) registry and handlers, logic semantics specific to surveys (pages, branches, endings, pools, quotas, variables, piping), question banks, collectors, invitations, participation records, responses, scoring, grading, overrides, and the survey reports and data sets.

### 4.4 Rejected reuse, with reasons (reviewer: confirm)

- **Wizard** (`IWizardEngine`, `WizardSession`): a wizard runs content-type steps and saves each step's content (`SavedSteps`). Surveys need branching over questions inside versioned documents, randomization plans, answer-level storage, scoring and an answer index. The concepts differ, as the audit already says for setup checklists. Surveys does not hand-roll Wizard; it is a different concept.
- **Orchard Core Forms**: no versions, no answer model, no scoring, no logic. `docs/modules/learning-surveys.md` ("Why a course item and not Orchard Core Forms") already records this.
- **Learning `GradeScale`**: course grade letters stay in the LMS gradebook. Surveys' per-survey **score bands** are result messages ("80% and above: well done") and can be mapped to a grade scale by Learning. They are not a second grade scale.
- **Learning `GradingMethod`** (highest, latest, average, first): it combines attempts for a course item result and is also used by Packages. Surveys reports each response's score and does not combine attempts. Learning keeps combining.

## 5. Changes to existing building blocks

### 5.1 Conditions: more operators and nested groups (phase 1)

Owner: `src/Abstractions/CrestApps.OrchardCore.Abstractions/Conditions/`. Reuse ladder step 3 (add the missing extension point to the owner).

1. **Operators.** Append to `ConditionOperator` (enums are serialized as strings: `ConditionRuleSchema` uses `Enum.GetNames`, and the JS mirror compares strings, so appending is safe): `NotContains`, `StartsWith`, `EndsWith`, `GreaterThanOrEqual`, `LessThanOrEqual`, `Between` (two values, inclusive, numbers or ISO dates), `CountAtLeast` and `CountAtMost` (number of selected values, for multi-select). Implement each in `ConditionEvaluator.IsMet(Condition, ConditionValue)` with the existing `Same` and `Compare` helpers. Dates compare as ISO 8601 invariant.
2. **Nested groups.** Add `public List<ConditionRule> Groups { get; set; } = [];` to `ConditionRule`. Semantics: the rule's `Match` (All or Any) applies over its `Conditions` and its `Groups` together. Update `ConditionRule.IsEmpty` (empty when neither has content), `ConditionEvaluator.IsMet(ConditionRule, ...)` (a group counts as one judged item; a group that cannot be judged is skipped, the same rule as a condition), and `ConditionalVisibility` source discovery used by `FindCycle` (walk nested groups).
3. **Schema.** `ConditionRuleSchema` gains `Groups` (recursive `$ref`) and the new operator names.
4. **Existing users** (`ConditionalFieldsService`, `WorkOrderService`, `UserAudienceCondition`, Learning player) need no change. Add regression tests for them.
5. **Tests.** `tests/CrestApps.OrchardCore.Tests/Conditions/ConditionEvaluatorTests.cs`: every operator, nested groups, withheld values, cycles through nested groups.

### 5.2 Conditions: one JS evaluator in Resources (phase 1)

Reuse ladder step 4: Surveys is the second consumer of the JS mirror that lives privately in ContentFields.

1. Move the pure evaluation functions (`same`, `toNumber`, `conditionMet`, rule evaluation, hidden resolution with withheld values and cycles) from `src/Modules/CrestApps.OrchardCore.ContentFields/Assets/js/conditional-fields.js` into `src/Modules/CrestApps.OrchardCore.Resources/Assets/js/conditions.js` as an ES module with named exports (`isRuleMet`, `isConditionMet`, `getHidden`). Add the new operators and nested groups.
2. Register it in the Resources resource manifest (`crestapps-conditions`). `conditional-fields.js` imports it; the ContentFields DOM code stays where it is.
3. **Parity vectors.** Add `tests/fixtures/conditions/condition-vectors.json` (rule, values, expected). Run it from `ConditionEvaluatorTests` (xUnit, `[Theory]` with a JSON member data source) and from `tests/js/resources/conditions.test.js` (Vitest). A new operator without a vector fails both.

### 5.3 Leased deadline work becomes generic (phase 4)

`AttemptDeadlineWork` in Learning.Assessments is Assessments-specific. Surveys builds `SurveyDeadlineWork` on the same `LeasedWorkItemIndexSchema` collection pattern; after the LMS moves (phase 7), `AttemptDeadlineWork` and `AttemptDeadlineBackgroundTask` are deleted. No change to the YesSql work-queue block.

### 5.4 Building-block catalog and audit (phase 2, updated in phase 7)

- `docs/engineering/building-blocks.md`: replace the "Surveys as course activities" row (line 111) with a **Surveys** row: owner `Surveys.Abstractions`/`.Core`; reuse through `ISurveyManager`, `ISurveyResponseService`, `AddSurveyControl`, `AddSurveyAccessRule`, `ISurveyResponseObserver`, `RecordRef` contexts; "Do not: keep survey questions, answers, quiz attempts or question banks in another module". Add a row for **Question banks** (same owner).
- Add the Conditions changes to the Conditions row (nested groups, operators, `conditions.js`).
- `docs/engineering/reusability-audit-2026-11.md`: note that the two LMS question engines were merged into Surveys.

## 6. The module set

### 6.1 Projects

| Project | Path | Holds |
| --- | --- | --- |
| `CrestApps.OrchardCore.Surveys.Abstractions` | `src/Abstractions/CrestApps.OrchardCore.Surveys.Abstractions/` | Records, value objects, enums, constants and feature ids (`SurveysConstants`), contracts (`ISurveyManager`, `ISurveyResponseService`, `ISurveyControlHandler`, `ISurveyControlScorer`, `ISurveyControlAnalyzer`, `ISurveyAccessRule`, `ISurveyResponseObserver`, `ISurveyGradingScope`, `ISurveyImportFormat`), descriptors and options (`SurveyControlOptions`), `SurveysServiceCollectionExtensions` (`AddSurveyControl`, `AddSurveyControlPreset`, `AddSurveyAccessRule`, `AddSurveyResponseObserver`, `AddSurveyGradingScope`, `AddSurveyImportFormat`, `AddSurveyContextKind`). References only `.Abstractions` projects (`CrestApps.Core.Abstractions`, `CrestApps.OrchardCore.Abstractions`, Parties, Records, Users abstractions as needed) |
| `CrestApps.OrchardCore.Surveys.Core` | `src/Core/CrestApps.OrchardCore.Surveys.Core/` | Stores, indexes and index providers (one public type per file), index schemas, managers and services, the runtime engine, the definition validator, the publisher and differ, the scoring engine, built-in control handlers, scorers and analyzers (no views), the expression scope and function provider, the version cache, permissions (`SurveysPermissions` static class), telemetry, `AddSurveysCore()` |
| `CrestApps.OrchardCore.Surveys` | `src/Modules/CrestApps.OrchardCore.Surveys/` | Manifest, startups, migrations, display drivers, controllers, views (control templates, designer, runner), admin menu, recipe and deployment steps and schemas, workflow activities, reports, data source, notifications, audit trail, webhooks, personal data contributor, background tasks, `Assets/` (designer and runner scripts), `README.md` |
| `CrestApps.OrchardCore.Surveys.AI` | `src/Modules/CrestApps.OrchardCore.Surveys.AI/` (phase 8) | AI tools and prompts (section 19.9). Separate module because it depends on AI |

Register all of them in `CrestApps.OrchardCore.slnx` and the modules in `src/Targets/CrestApps.OrchardCore.Cms.Core.Targets/CrestApps.OrchardCore.Cms.Core.Targets.csproj` (`PrivateAssets="none"`). Copy csproj conventions from `src/Modules/CrestApps.OrchardCore.Learning.Surveys/CrestApps.OrchardCore.Learning.Surveys.csproj` (Razor SDK, `InternalsVisibleTo CrestApps.OrchardCore.Tests`, `OrchardCore.Module.Targets`). Package versions come from `Directory.Packages.props`; add `OrchardCore.DataSources.Core`/`.Abstractions` references to Surveys.Core for formulas (already pinned centrally). TFM is net11.0.

### 6.2 Features

Feature ids are constants in `SurveysConstants.Features`. Category: `Surveys`.

| Feature id | Name | Contains | Depends on |
| --- | --- | --- | --- |
| `CrestApps.OrchardCore.Surveys` | Surveys | Surveys, drafts, versions, designer, controls, logic, pools, question banks, web-link collectors, access rules and limits, responses, scoring, grading, overrides, retention, results reports | `OrchardCore.Users`, CrestApps Resources (Attachments optional: the File upload control appears when it is enabled, decision D6) |
| `CrestApps.OrchardCore.Surveys.Invitations` | Survey Invitations | Invitation collectors, personal links, reminders, invitation notices | Surveys, `OrchardCore.Notifications`, Outreach |
| `CrestApps.OrchardCore.Surveys.Embedding` | Survey Embedding | `SurveyPart`, the Survey widget content type, the `[survey]` shortcode | Surveys, `OrchardCore.Contents` (Widgets and Shortcodes optional through `[RequireFeatures]`) |
| `CrestApps.OrchardCore.Surveys.Qti` | QTI Question Import | QTI 2.1/3.0 import into question banks (moved from Learning) | Surveys, `OrchardCore.Media` |
| `CrestApps.OrchardCore.Surveys.Api` | Surveys API | Headless JSON endpoints on the `Api` authentication scheme, in OpenAPI | Surveys |
| `CrestApps.OrchardCore.Surveys.AI` (module Surveys.AI) | Surveys AI | AI tools and summaries | Surveys, CrestApps AI |

`[RequireFeatures]` startups in the Surveys module (one file each, like `src/Modules/CrestApps.OrchardCore.Staff/Startup.cs`): Attachments (File upload control), Recipes, Deployment, CrestApps Recipes schemas (`CrestAppsRecipesConstants.Feature.Area`), Workflows (`OrchardCoreFeatureIds.Workflows`), Data Pipelines (`OrchardCoreFeatureIds.DataPipelinesCore`), Reports (`ReportsConstants.Feature`), Audit Trail, Notifications, Webhooks, Rate Limits, ReCaptcha, Admin Dashboard, Users Audiences.

### 6.3 Vocabulary

| Term (code) | UI | Not | Because |
| --- | --- | --- | --- |
| `Survey` (with `Kind`: `Survey`, `Quiz`, `Poll`, open string) | Survey, Quiz, Poll | Form | Orchard Core Forms |
| `SurveyVersion` | Version | Revision | Content versions |
| `SurveyQuestion` with a `ControlType` | Question; the type is a "question type". Code calls the type a **control** | Field | Content fields |
| `SurveyPage` | Page | Section, Step | Wizard steps |
| `QuestionPool` | Question pool | Block, Group | |
| `QuestionBank`, `BankQuestion` | Question bank | Library | Media library |
| `SurveyCollector` (`Kind`: `WebLink`, `Invitation`, `Embed`, `Kiosk`, open string) | Collector (link, invitation, embed) | Distribution, Channel | Telephony distribution, omnichannel channels |
| `SurveyInvitation` | Invitation | | `AccountInvitation` exists; always prefixed |
| `SurveyResponse` (with `AttemptNumber`) | Response (Attempt for quizzes) | Session, Interview, Submission | `CallSession`, `WizardSession`, `CheckoutSession`; Coursework submissions |
| `SurveyParticipation` | (internal) | | |
| `SurveyEnding` | Ending | Thank-you page | |
| `SurveyOverride` | Accommodation (extra time, attempts, window) | Exception | |
| `SurveyQuota` | Quota | Limit | Limits are per respondent |

The Learning `SurveyResponse`, `SurveyPart`, `SurveyQuestion` and `SurveyQuestionPart` types (namespace `CrestApps.OrchardCore.Learning.Surveys` / `.Assessments`) coexist until phase 7 deletes them. Never import both namespaces in one file.

## 7. Domain model

All records use `DocumentCatalog<T, TIndex>` stores in the YesSql collection `SurveysConstants.CollectionName = "Surveys"` (responses in `"SurveyResponses"`, so the large table is separate). Writes go through `ICatalogManager<T>` so `ICatalogEntryHandler<T>` handlers run. Every record that may be extended by consumers carries a `JsonObject Properties` with `As<T>()`/`Put<T>()` helpers (use CrestApps.Core's extensible-entity pattern if `CatalogItem` supports it; phase 0 check C3). Dates are UTC and named `...Utc`.

### 7.1 Survey

```csharp
public sealed class Survey : CatalogItem, IAuditedModel
{
    public string Name { get; set; }                 // unique technical name, [a-z0-9-], used in recipes and URLs of the default collector
    public string Title { get; set; }
    public string Description { get; set; }          // admin-only notes
    public string Kind { get; set; }                 // SurveyKinds.Survey | Quiz | Poll, open string
    public SurveyStatus Status { get; set; }         // Draft (never published), Open, Closed, Archived
    public SurveyDefinition Draft { get; set; }      // the editable definition
    public DateTime? DraftModifiedUtc { get; set; }
    public bool HasUnpublishedChanges { get; set; }
    public string PublishedVersionId { get; set; }
    public int PublishedVersionNumber { get; set; }
    public RecordRef Owner { get; set; }             // null for standalone; Content:{courseItemId} for an LMS quiz
    public IList<string> Tags { get; set; } = [];
    public SurveyAccessSettings Access { get; set; } = new();   // live, NOT versioned (7.6)
    public SurveyRetentionSettings Retention { get; set; } = new();
    public string AnonymitySecret { get; set; }      // random 32 bytes base64, for pseudonymous respondent keys (7.9); never exported in plain form (decision D9)
    public JsonObject Properties { get; set; } = [];
    // IAuditedModel: CreatedUtc, Author, OwnerId, ModifiedUtc...
}
```

`SurveyStatus` moves through a `TransitionTable<SurveyStatus>`: Draft → Open (first publish) ↔ Closed → Archived; Archived → Closed (unarchive).

### 7.2 SurveyDefinition (draft and every version)

```csharp
public sealed class SurveyDefinition
{
    public int SchemaVersion { get; set; } = 1;
    public string Title { get; set; }                         // respondent-facing title
    public string Introduction { get; set; }                  // sanitized HTML
    public List<SurveyPage> Pages { get; set; } = [];
    public List<SurveyVariable> Variables { get; set; } = [];
    public List<SurveyEnding> Endings { get; set; } = [];     // at least one default ending
    public List<ScoreCategory> ScoreCategories { get; set; } = [];
    public List<ScoreBand> ScoreBands { get; set; } = [];
    public List<SurveyQuota> Quotas { get; set; } = [];       // phase 6
    public SurveyPresentationSettings Presentation { get; set; } = new();
    public SurveyNavigationSettings Navigation { get; set; } = new();
    public SurveyScoringSettings Scoring { get; set; } = new();
    public SurveyReviewSettings Review { get; set; } = new();
    public SurveyTimingSettings Timing { get; set; } = new();
    public Dictionary<string, Dictionary<string, string>> Translations { get; set; } = []; // culture -> text key -> text (phase 8)
    public JsonObject Properties { get; set; } = [];          // consumer settings that must be versioned (LMS)
}

public sealed class SurveyPage
{
    public string Id { get; set; }                     // stable, IdGenerator
    public string Name { get; set; }                   // unique variable code within the survey, e.g. "page_2"
    public string Title { get; set; }
    public string Description { get; set; }            // sanitized HTML, supports piping
    public SurveyVisibility Visibility { get; set; } = new();
    public List<SurveyQuestion> Questions { get; set; } = [];   // display order; pool members are inline with PoolId
    public List<QuestionPool> Pools { get; set; } = [];
    public QuestionOrder QuestionOrder { get; set; }   // AsDefined | Shuffle (members with Pinned stay in place)
    public List<SurveyBranch> Branches { get; set; } = [];      // evaluated in order when leaving the page
    public bool AllowBack { get; set; } = true;        // AND-ed with Navigation.AllowBack
    public int? TimeLimitSeconds { get; set; }         // phase 6 (should-have)
}

public sealed class SurveyQuestion
{
    public string Id { get; set; }                     // stable forever; answers are keyed by it
    public string Name { get; set; }                   // unique variable code ("q_satisfaction"), used in piping, formulas, exports
    public string ControlType { get; set; }            // registered control name ("SingleChoice")
    public string Title { get; set; }                  // supports {piping}
    public string Description { get; set; }            // sanitized HTML
    public string Placeholder { get; set; }
    public bool Required { get; set; }
    public bool Pinned { get; set; }                   // keeps its place when the page shuffles
    public string PoolId { get; set; }                 // member of a pool on this page
    public SurveyVisibility Visibility { get; set; } = new();
    public ConditionRule RequiredWhen { get; set; }    // optional; overrides Required when set
    public ConditionRule EnabledWhen { get; set; }     // optional; disabled questions keep their value read-only
    public List<SurveyChoice> Choices { get; set; } = [];        // for choice-based controls
    public bool ShuffleChoices { get; set; }
    public string ChoicesFrom { get; set; }            // carry forward: source question id
    public ChoicesFromMode ChoicesFromMode { get; set; } // All | Selected | Unselected
    public List<SurveySubQuestion> Rows { get; set; } = [];      // matrix rows, multiple-text items, matching prompts
    public SurveyQuestionScoring Scoring { get; set; } = new();  // the key; NEVER sent to respondents (8.4)
    public List<SurveyValidationRule> Validation { get; set; } = []; // regex, length, range, formula
    public JsonObject Settings { get; set; } = [];     // control-specific settings, read with question.GetSettings<TSettings>()
    public JsonObject Properties { get; set; } = [];   // consumer extensions (tags, competency, difficulty for banks)
}

public sealed class SurveyChoice
{
    public string Id { get; set; }            // stable
    public string Value { get; set; }         // export code ("1", "very_satisfied"); defaults to position
    public string Text { get; set; }          // supports piping
    public string ImageUrl { get; set; }      // media path for image choices
    public bool IsOther { get; set; }         // shows a text box; answer stored in SurveyAnswer.OtherText
    public bool IsExclusive { get; set; }     // "None of the above": clears the others
    public bool Pinned { get; set; }          // keeps its place when choices shuffle
    public bool Retired { get; set; }         // hidden from new respondents, kept for reporting
    public SurveyVisibility Visibility { get; set; } = new();   // choice filtering
}

public sealed class SurveySubQuestion { public string Id; public string Name; public string Text; public bool Retired; }  // properties, not fields

public sealed class SurveyVisibility
{
    public VisibilityMode Mode { get; set; }  // Always | ShowWhen | HideWhen
    public ConditionRule Rule { get; set; }   // shared Conditions model; sources are question ids, "{questionId}.{rowId}", "var:{name}", "score", "score:{categoryId}", "context:{key}"
    public string Formula { get; set; }       // optional advanced condition (9.4); evaluated on the server only
}

public sealed class QuestionPool
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Title { get; set; }          // admin label
    public int DrawCount { get; set; }         // N of M; 0 = all
    public bool Shuffle { get; set; } = true;
    public bool EvenlyPresent { get; set; }    // phase 6: least-presented first, by counters
    public List<BankDraw> BankDraws { get; set; } = [];   // candidates from question banks (snapshotted at publish, 11.3)
}

public sealed class BankDraw { public string BankId; public IList<string> Tags; public int? Difficulty; public int? MaxCandidates; } // properties

public sealed class SurveyBranch
{
    public string Id { get; set; }
    public ConditionRule Rule { get; set; }
    public string Formula { get; set; }
    public BranchTargetKind TargetKind { get; set; }  // Page | Ending
    public string TargetId { get; set; }               // a LATER page id, or an ending id
}

public sealed class SurveyEnding
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }            // Liquid + sanitized HTML; model: Survey, Response, Answers, Score
    public string RedirectUrl { get; set; }        // optional, supports piping, must be local or allow-listed (9.6)
    public EndingOutcome Outcome { get; set; }     // Completed | ScreenedOut
    public SurveyVisibility Condition { get; set; } = new();   // first ending whose condition holds is chosen when the last page ends
    public bool IsDefault { get; set; }
}

public sealed class SurveyVariable
{
    public string Name { get; set; }               // [a-zA-Z_][a-zA-Z0-9_]*
    public VariableKind Kind { get; set; }         // Hidden (from URL, collector default or context) | Calculated (formula)
    public string Formula { get; set; }            // Calculated
    public string DefaultValue { get; set; }
    public bool AcceptFromUrl { get; set; }        // only declared variables are read from the query string
    public DataFieldType DataType { get; set; }    // Text, Integer, Decimal, Boolean, Date
}
```

Settings classes (all versioned, so a response keeps the rules it started under):

- `SurveyPresentationSettings`: `ShowQuestionNumbers`, `ShowPageTitles`, `ShowProgressBar`, `ProgressBasis` (Pages | Questions), `QuestionsPerPage` (0 = as designed; 1 = one question at a time), `PageOrder` (AsDefined | Shuffle with pinned first and last page), `CssClass`, `ShowTitle`, `CompletedRedirectUrl`.
- `SurveyNavigationSettings`: `AllowBack`, `Sequential` (no back, no skipping; LMS "sequential"), `AutoAdvance` (single-choice pages), `AllowResume`, `AllowEditAfterSubmit`, `ConfirmBeforeSubmit`.
- `SurveyScoringSettings`: `Enabled`, `PassMarkPercent` (decimal?), `DefaultPoints`, `NegativeMarking`, `ShowScoreToRespondent`, `FeedbackMode` (Deferred | Immediate, the LMS "check answer"), `RoundingDecimals`, `ScoreFormula` (optional formula over category scores; default sum).
- `SurveyReviewSettings`: port `QuizReviewOptions` from `src/Abstractions/CrestApps.OrchardCore.Learning.Assessments.Abstractions/Models/QuizReviewOptions.cs` as `ReviewShows During, ImmediatelyAfter, LaterWhileOpen, AfterClose` with `[Flags] ReviewShows { None, Score, Responses, RightAnswers, Feedback, GeneralFeedback, All }`; plus `ResultsVisibility` (Never | AfterAnswer | AfterClose | Always) for aggregate results shown to respondents (polls, Learning "Choice").
- `SurveyTimingSettings`: `TimeLimitMinutes`, `GraceSeconds` (default 5), `AutoSubmitAtDeadline` (true).

`SurveyQuestionScoring`:

```csharp
public sealed class SurveyQuestionScoring
{
    public decimal? Points { get; set; }               // null = DefaultPoints; 0 = unscored
    public bool PartialCredit { get; set; }
    public decimal? PenaltyPerWrong { get; set; }      // negative marking
    public string CategoryId { get; set; }
    public Dictionary<string, decimal> ChoicePoints { get; set; } = [];   // choice id -> points (may be negative)
    public IList<string> CorrectChoiceIds { get; set; } = [];
    public IList<string> AcceptedAnswers { get; set; } = [];
    public bool CaseSensitive { get; set; }
    public decimal? NumberAnswer { get; set; }
    public decimal Tolerance { get; set; }
    public Dictionary<string, string> Pairs { get; set; } = [];           // matching: row id -> choice id; ordering uses Choices order
    public string GeneralFeedback { get; set; }
    public string CorrectFeedback { get; set; }
    public string IncorrectFeedback { get; set; }
    public Dictionary<string, string> ChoiceFeedback { get; set; } = [];  // choice id -> feedback
    public string GraderGuidance { get; set; }         // essays
    public JsonObject Key { get; set; } = [];          // control-specific key data for custom controls
}
```

`ScoreCategory { Id, Name, Title }`; `ScoreBand { Id, MinPercent, Label, Message, Passing? }`; `SurveyValidationRule { Kind (Regex | MinLength | MaxLength | Min | Max | Formula | Email | Url), Value, Message }`.

### 7.3 SurveyVersion

```csharp
public sealed class SurveyVersion : CatalogItem
{
    public string SurveyId { get; set; }
    public int Number { get; set; }                     // 1, 2, 3...
    public SurveyDefinition Definition { get; set; }    // immutable after creation; bank draws resolved (11.3)
    public DateTime PublishedUtc { get; set; }
    public string PublishedBy { get; set; }             // user id
    public string Notes { get; set; }
    public string ContentHash { get; set; }             // SHA-256 of the canonical definition JSON
}
```

A version is never edited or deleted while a response references it (`SurveyVersionDeletingHandler` refuses). The store exposes no update method; the recipe step creates versions only.

### 7.4 Question banks

```csharp
public sealed class QuestionBank : CatalogItem, IAuditedModel { public string Name; public string Title; public string Description; public IList<string> Tags; public RecordRef Owner; public JsonObject Properties; }
public sealed class BankQuestion : CatalogItem, IAuditedModel
{
    public string BankId { get; set; }
    public SurveyQuestion Question { get; set; }   // same model as a survey question, Scoring included
    public IList<string> Tags { get; set; } = [];
    public int Difficulty { get; set; } = 3;       // 1..5
    public string SourceIdentifier { get; set; }   // import de-duplication (QTI identifier, spreadsheet id)
    public bool Retired { get; set; }
    public int Revision { get; set; }              // incremented on every save; a version records the revision it copied
}
```

Banks are catalog records, not content items, so keys never leak through the content API, GraphQL or search, and no access handler that fails `ViewContent` is needed (decision D1).

### 7.5 Collectors

```csharp
public sealed class SurveyCollector : CatalogItem, IAuditedModel
{
    public string SurveyId { get; set; }
    public string Name { get; set; }
    public string Kind { get; set; }              // SurveyCollectorKinds.WebLink | Invitation | Embed | Kiosk | consumer kinds (Learning: "LearningCourseItem")
    public string Token { get; set; }             // 22-char url-safe random; public URL /s/{token}
    public CollectorStatus Status { get; set; }   // Open | Closed
    public DateTime? OpensUtc { get; set; }
    public DateTime? ClosesUtc { get; set; }
    public int? MaxResponses { get; set; }
    public string PasswordHash { get; set; }      // ASP.NET Core PasswordHasher; never exported in plain form
    public Dictionary<string, string> Variables { get; set; } = [];   // hidden variable defaults for this collector
    public string Culture { get; set; }
    public bool IsDefault { get; set; }           // one per survey, created on first publish
    public JsonObject Properties { get; set; } = [];
}
```

### 7.6 Access settings (live, not versioned)

Operational switches change without republishing.

```csharp
public sealed class SurveyAccessSettings
{
    public SurveyAudienceMode Audience { get; set; }        // Anyone | SignedIn | Audiences | InvitationOnly
    public IList<string> AudienceIds { get; set; } = [];     // Users audiences
    public AnonymityMode Anonymity { get; set; }             // Identified | Anonymous (7.9)
    public bool CollectIpAddress { get; set; }               // false by default; ignored when Anonymous
    public DateTime? OpensUtc { get; set; }
    public DateTime? ClosesUtc { get; set; }
    public WeeklySchedule Schedule { get; set; }             // DayOfWeek -> { Mode: AllowAll | PreventAll | Between, From, To } in the site time zone
    public RespondentLimits Limits { get; set; } = new();
    public int? MaxResponses { get; set; }                   // whole survey, completed responses
    public bool RequireCaptcha { get; set; }
    public int IncompleteExpiryHours { get; set; } = 168;    // in-progress responses close as Abandoned after this
}

public sealed class RespondentLimits
{
    public LimitCount Counts { get; set; }      // Completed | Started
    public int? Total { get; set; }             // e.g. quizzes: max attempts
    public int? PerDay { get; set; }
    public int? PerWeek { get; set; }           // week starts on the site's first day of week
    public int? PerMonth { get; set; }
    public int? CooldownMinutes { get; set; }   // LMS "delay between attempts"
    public UniquenessMode Uniqueness { get; set; }   // SignedInUser | Invitation | Cookie | None (anonymous public links)
}
```

### 7.7 SurveyResponse

```csharp
public sealed class SurveyResponse : CatalogItem
{
    public string SurveyId { get; set; }
    public string SurveyVersionId { get; set; }
    public int VersionNumber { get; set; }
    public string Number { get; set; }                  // "R0001234", per tenant sequence (for support); the ItemId is random
    public SurveyResponseStatus Status { get; set; }    // InProgress | Completed | ScreenedOut | Abandoned | Voided
    public GradingStatus Grading { get; set; }          // NotScored | Pending | Graded
    public string UserId { get; set; }                  // null when anonymous or not signed in
    public PartyRef Respondent { get; set; }            // null when anonymous
    public string CollectorId { get; set; }
    public string InvitationId { get; set; }
    public RecordRef Context { get; set; }              // what the response belongs to (LearningEnrollment:{id}, Ticket:{id})
    public int AttemptNumber { get; set; }              // per (survey, respondent key, context)
    public string Culture { get; set; }
    public bool IsTest { get; set; }                    // preview and test responses; excluded from results and limits
    public DateTime StartedUtc { get; set; }
    public DateTime LastActivityUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public DateTime? DeadlineUtc { get; set; }
    public bool AutoSubmitted { get; set; }
    public string CurrentPageId { get; set; }
    public List<string> PagePath { get; set; } = [];    // visited pages in order (back pops)
    public List<SurveyPageVisit> PageVisits { get; set; } = [];   // page id, entered, left; funnel and timing
    public SurveyResponsePlan Plan { get; set; } = new();
    public Dictionary<string, SurveyAnswer> Answers { get; set; } = [];   // key: question id
    public Dictionary<string, string> Variables { get; set; } = [];       // hidden and calculated values
    public decimal? Score { get; set; }
    public decimal? MaxScore { get; set; }
    public decimal? Percent { get; set; }
    public bool? Passed { get; set; }
    public Dictionary<string, decimal> CategoryScores { get; set; } = [];
    public string EndingId { get; set; }
    public string GradedBy { get; set; }
    public DateTime? GradedUtc { get; set; }
    public string Feedback { get; set; }
    public List<ScoreChange> ScoreHistory { get; set; } = [];   // port AttemptScoreChange: ChangedUtc, Actor, Kind, From, To, Reason
    public string IpAddress { get; set; }               // only when CollectIpAddress and Identified
    public string ResumeTokenHash { get; set; }         // SHA-256 of the resume token (anonymous and not-signed-in)
    public JsonObject Properties { get; set; } = [];    // consumer data (LMS: CourseId, EnrollmentId)
}

public sealed class SurveyResponsePlan
{
    public int Seed { get; set; }
    public List<string> PageOrder { get; set; } = [];
    public Dictionary<string, List<string>> QuestionOrder { get; set; } = [];   // page id -> question ids shown (pool draws applied)
    public Dictionary<string, List<string>> ChoiceOrder { get; set; } = [];     // question id -> choice ids in display order
}

public sealed class SurveyAnswer
{
    public List<string> Values { get; set; } = [];         // choice ids, or entered text
    public decimal? Number { get; set; }
    public DateTime? Date { get; set; }
    public Dictionary<string, List<string>> Rows { get; set; } = [];   // row id -> values (matrix, matching, multiple text)
    public string OtherText { get; set; }
    public List<string> AttachmentIds { get; set; } = [];
    public JsonObject Extra { get; set; } = [];             // control-specific data (signature, typing metrics)
    public DateTime AnsweredUtc { get; set; }
    public decimal? AutoScore { get; set; }
    public decimal? ManualScore { get; set; }
    public decimal? MaxPoints { get; set; }
    public string GraderComment { get; set; }
    public bool AIAssisted { get; set; }
    public DateTime? CheckedUtc { get; set; }               // immediate feedback: locked after check
    [JsonIgnore] public decimal? Score => ManualScore ?? AutoScore;
}
```

Status moves (`TransitionTable<SurveyResponseStatus>`): InProgress → Completed | ScreenedOut | Abandoned | Voided; Completed → Voided; Completed → InProgress only when `AllowEditAfterSubmit` (reopen keeps the same version); ScreenedOut → Voided; Abandoned → Voided.

### 7.8 Other records

- `SurveyInvitation : CatalogItem` (phase 6): `SurveyId, CollectorId, Recipient (PartyRef), UserId, Email, Phone, TokenHash, Status (Pending | Sent | Opened | Started | Completed | Bounced | OptedOut | Expired), SentUtc, LastReminderUtc, RemindersSent, ResponseId, ExpiresUtc, Properties`.
- `SurveyParticipation : CatalogItem`: `SurveyId, RespondentKey, ContextKey, Day (DateOnly in the site time zone), StartedCount, CompletedCount, LastStartedUtc`. It holds no response id, so participation (for limits) cannot be joined to answers of an anonymous survey.
- `SurveyOverride : CatalogItem` (port of `AssessmentOverride`): `SurveyId, UserId, AudienceId, Context (RecordRef, optional), ExtraMinutes, ExtraAttempts, OpensUtc, ClosesUtc, Reason (required), Actor, CreatedUtc`.
- `SurveyQuotaCounter` (phase 6): one document per survey: quota id → completed count; changed only under the survey lock.
- `SurveySettings` (site settings, `ISite` group `surveys`): `DefaultRetentionDays`, `DefaultIncompleteExpiryHours`, `MaxUploadMegabytes`, `AllowedRedirectHosts`, `ShowPoweredBy` (false).

### 7.9 Anonymity

- **Identified**: `UserId` and `Respondent` stored; IP stored only when `CollectIpAddress`.
- **Anonymous**: the response stores no `UserId`, `Respondent`, `IpAddress`, `InvitationId` or `Context` that identifies a person (a consumer may still set a non-personal context, such as the course). Limits use `SurveyParticipation.RespondentKey = HMAC-SHA256(Survey.AnonymitySecret, userId or invitation id)`, kept in a separate record without a response id, at day granularity. The respondent's own resume uses the resume token, never the user id. Results reports refuse filters that would isolate fewer than `MinimumGroupSize` (default 5) anonymous responses (decision D10).
- Switching a survey from Anonymous to Identified applies only to new responses; the editor says so. Switching from Identified to Anonymous does not scrub old responses unless the admin runs **Anonymize existing responses** (audited).

### 7.10 Indexes

One file per index and per provider. Every `context.For<TIndex>()` calls `ExclusiveToDocumentType()`; one-row maps add `SingleRecordPerDocument()` (guarded by `IndexDeclarationArchitectureTests`). Each date column read by a data set gets a database index on (date, `Id`).

| Index | Columns | SQL indexes |
| --- | --- | --- |
| `SurveyIndex : CatalogItemIndex` | Name (unique), Title, Kind, Status, OwnerRef (`Kind:Id`, 255), PublishedVersionNumber, ModifiedUtc | Name; Status+Kind; OwnerRef; ModifiedUtc+Id |
| `SurveyVersionIndex` | SurveyId, Number, PublishedUtc | SurveyId+Number (unique) |
| `QuestionBankIndex` | Name, Title, OwnerRef | Name |
| `BankQuestionIndex` | BankId, ControlType, Difficulty, Retired, SourceIdentifier (255), ModifiedUtc | BankId+Retired; SourceIdentifier |
| `BankQuestionTagIndex` (MapIndex, row per tag) | BankQuestionId, BankId, Tag (100) | BankId+Tag |
| `SurveyCollectorIndex` | SurveyId, Kind, Token (unique), Status | Token; SurveyId |
| `SurveyResponseIndex` | SurveyId, SurveyVersionId, VersionNumber, Number, Status, Grading, UserId, RespondentRef, ContextRef, CollectorId, InvitationId, AttemptNumber, IsTest, StartedUtc, LastActivityUtc, CompletedUtc, DeadlineUtc, Percent, Passed, ModifiedUtc | SurveyId+CompletedUtc+Id; UserId+SurveyId; ContextRef+SurveyId; Status+LastActivityUtc; Grading+SurveyId; DeadlineUtc (where InProgress); ModifiedUtc+Id |
| `SurveyAnswerIndex` (MapIndex; rows only for Completed, non-test responses) | ResponseId, SurveyId, VersionNumber, QuestionId, QuestionName (100), RowId, ChoiceId, NumberValue (decimal), DateValue, TextValue (255, truncated), Score, MaxPoints, CompletedUtc | SurveyId+QuestionId+ChoiceId; SurveyId+CompletedUtc |
| `SurveyParticipationIndex` | SurveyId, RespondentKey (64), ContextKey (255), Day, CompletedCount, StartedCount | SurveyId+RespondentKey+Day |
| `SurveyInvitationIndex` | SurveyId, CollectorId, Status, UserId, RecipientRef, SentUtc, ExpiresUtc | SurveyId+Status; TokenHash |
| `SurveyOverrideIndex` | SurveyId, UserId, AudienceId, ContextRef, CreatedUtc | SurveyId+UserId |

The answer index is mapped by `SurveyAnswerIndexProvider`, which loads the version from the version cache (no database read) and asks each control handler for its index rows (`ISurveyControlHandler.GetIndexRows`). It writes rows only when `Status == Completed && !IsTest`, so in-progress saves cost one document write.

## 8. Controls: the extensible question model

This is the part the user called out: **anyone can add a control** from another module, without changing Surveys.

### 8.1 Registration

```csharp
// Surveys.Abstractions
public static IServiceCollection AddSurveyControl<THandler>(this IServiceCollection services, string name, Action<SurveyControlDescriptor> configure = null)
    where THandler : class, ISurveyControlHandler;
public static IServiceCollection AddSurveyControlScorer<TScorer>(this IServiceCollection services) where TScorer : class, ISurveyControlScorer;
public static IServiceCollection AddSurveyControlAnalyzer<TAnalyzer>(this IServiceCollection services) where TAnalyzer : class, ISurveyControlAnalyzer;
public static IServiceCollection AddSurveyControlPreset(this IServiceCollection services, string name, string baseControl, Action<SurveyControlPresetDescriptor> configure);

public sealed class SurveyControlDescriptor
{
    public string Name { get; set; }                       // open string, unique: "SingleChoice", "Acme.Signature"
    [JsonIgnore] public LocalizationSource DisplayName { get; set; }
    [JsonIgnore] public LocalizationSource Description { get; set; }
    public string Category { get; set; }                   // SurveyControlCategories.Choice | Text | Number | Date | Rating | Matrix | Content | Files | Advanced | custom
    public string Icon { get; set; }                       // Font Awesome class shown in the "Add question" picker
    public int Position { get; set; }
    public SurveyControlCapabilities Capabilities { get; set; }
    public Type SettingsType { get; set; }                 // POCO stored in SurveyQuestion.Settings
}

[Flags]
public enum SurveyControlCapabilities
{
    None = 0,
    Answerable = 1,          // collects an answer (Text block does not)
    UsesChoices = 2,         // editor shows the choice list; supports ShuffleChoices, carry-forward, choice visibility
    UsesRows = 4,            // editor shows rows (matrix, matching, multiple text)
    Scorable = 8,            // has an ISurveyControlScorer
    ManuallyGraded = 16,     // scorer may return null (essay)
    ConditionSource = 32,    // other questions may test its answer
    SupportsOther = 64,
    SupportsValidation = 128,
    Aggregatable = 256,      // has an ISurveyControlAnalyzer (results summary)
    StoresFiles = 512,
    ClientScript = 1024,     // ships a script that raises survey:change (8.5)
}

public sealed class SurveyControlPresetDescriptor   // SurveyJS "specialized" types: NPS = Rating 0..10 with labels
{
    public string Name; public string BaseControl; public LocalizationSource DisplayName; public string Category; public string Icon;
    public Action<SurveyQuestion> Initialize;           // fills Settings, Choices, Rows of a new question
}
```

`SurveyControlOptions` holds descriptors and presets; `ISurveyControlRegistry` (Core) resolves handlers, scorers and analyzers by name (keyed lookup built once). An unknown control type in a definition (a module was disabled) renders a "This question type is not available" placeholder for admins, is skipped for respondents (treated as hidden), and is reported by the definition validator.

### 8.2 The handler contract

```csharp
public interface ISurveyControlHandler
{
    string ControlType { get; }

    // Designer
    void Initialize(SurveyQuestion question);                                        // defaults for a new question
    Task ValidateDefinitionAsync(SurveyQuestion question, SurveyDefinitionValidationContext context);  // publish-time checks

    // Respondent
    void PreparePresentation(SurveyQuestion question, SurveyPresentationContext context);  // choice order etc., uses context.Random (seeded)
    Task ReadAnswerAsync(SurveyAnswerReadContext context);       // from IFormCollection (prefix "q.{questionId}") or a JSON body (API)
    Task ValidateAnswerAsync(SurveyAnswerValidationContext context);   // required, ranges, validation rules; adds errors by question id
    bool IsEmpty(SurveyQuestion question, SurveyAnswer answer);

    // Logic and piping
    ConditionValue GetConditionValue(SurveyQuestion question, SurveyAnswer answer, string rowId);   // values for ConditionEvaluator
    object GetFormulaValue(SurveyQuestion question, SurveyAnswer answer, string rowId);              // long, decimal, string, bool, DateTime or null
    DataFieldType GetFormulaType(SurveyQuestion question, string rowId);
    string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context);  // plain text for piping, exports, notifications

    // Data
    IEnumerable<SurveyExportColumn> DescribeColumns(SurveyQuestion question);      // stable column keys: "{name}", "{name}.{row}", "{name}.other"
    void WriteColumns(SurveyQuestion question, SurveyAnswer answer, IDictionary<string, object> row, SurveyFormatContext context);
    IEnumerable<SurveyAnswerIndexRow> GetIndexRows(SurveyQuestion question, SurveyAnswer answer);
}

public interface ISurveyControlScorer
{
    string ControlType { get; }
    SurveyScoreResult Score(SurveyQuestion question, SurveyAnswer answer, SurveyScoringContext context);  // Points null = needs manual grading
    decimal MaxPoints(SurveyQuestion question, SurveyScoringContext context);
    SurveyAnswerFeedback GetFeedback(SurveyQuestion question, SurveyAnswer answer);  // correct/incorrect, right answer text, per-choice feedback
}

public interface ISurveyControlAnalyzer
{
    string ControlType { get; }
    Task<SurveyQuestionSummary> SummarizeAsync(SurveyQuestion question, SurveyAnalysisContext context, CancellationToken cancellationToken);
    // context gives SQL aggregates over SurveyAnswerIndex (counts by choice/row, avg/min/max/median of NumberValue, text samples)
    IEnumerable<ReportSection> ToReportSections(SurveyQuestion question, SurveyQuestionSummary summary, SurveyAnalysisContext context);
}
```

`SurveyControlHandlerBase<TSettings>` implements every method with sensible defaults (text values, one column, `Values` as condition values) so a simple custom control overrides two or three methods. `SurveyChoiceControlHandlerBase<TSettings>` adds choice ordering, carry-forward, exclusive and "other" handling.

### 8.3 Rendering and editing (shapes and drivers)

**Designer (admin).** The question editor is `IDisplayManager<SurveyQuestion>` built with display drivers, so any module can add settings to any question:

- `SurveyQuestionCommonDisplayDriver` (all types): title, name, description, required, required-when, enabled-when, visibility (mode + rule builder), pinned.
- `SurveyChoicesDisplayDriver` (types with `UsesChoices`): choice list editor (SortableJS reorder, value, image, other, exclusive, pinned, retire), shuffle, carry-forward.
- `SurveyRowsDisplayDriver` (`UsesRows`).
- `SurveyValidationDisplayDriver` (`SupportsValidation`).
- `SurveyScoringDisplayDriver` (survey `Scoring.Enabled` and `Scorable`): points, partial credit, penalty, category, correct choices or accepted answers, feedback.
- One settings driver per control: `SurveyControlSettingsDisplayDriver<TSettings>` base (Core) with `CanHandleModel(question) => question.ControlType == Name`. It reads and writes `question.Settings` through `GetSettings<TSettings>()` / `SetSettings(TSettings)`.

Editor views live in the Surveys module: `Views/SurveyQuestion.{Common,Choices,Rows,Validation,Scoring}.Edit.cshtml` and per control `Views/SurveyControlSettings-{ControlType}.Edit.cshtml`.

**Respondent.** The runner builds, per visible question, a `SurveyQuestion_Respond` wrapper shape (number, title, description, required marker, errors, immediate feedback) containing a `SurveyControl` shape with display type `Respond` and alternates:

- `SurveyControl_Respond__{ControlType}` (the control's template)
- `SurveyControl_Respond__{ControlType}__{SurveyName}` and `SurveyControl_Respond__{ControlType}__{QuestionName}` (theme overrides)

The same shape renders with display types `Review` (read-only answer with correct/incorrect marks and feedback, as the review policy allows), `Print` and `Summary` (results). View model: `SurveyControlViewModel { SurveyQuestionView Question (respondent projection, 8.4), TSettings Settings, SurveyAnswer Answer, string Prefix, IReadOnlyList<SurveyChoiceView> Choices (in plan order, filtered by choice visibility), IReadOnlyList<string> Errors, bool ReadOnly, SurveyAnswerFeedback Feedback, string Culture }`.

Every control template must: use `<fieldset>`/`<legend>` for groups, label every input, set `aria-required`, `aria-invalid` and `aria-describedby` for errors and hints, work without JavaScript (plain form post), and use input names under `Prefix` (`q.{id}`, `q.{id}.{rowId}`, `q.{id}.other`).

### 8.4 Respondent projection (keys never leave the server)

`SurveyRespondentProjector` (Core) builds `SurveyQuestionView` from a `SurveyQuestion`, copying only: id, name, control type, title and description (piped and localized), placeholder, required, settings returned by `ISurveyControlHandler.GetRespondentSettings` (default: the settings object minus properties marked `[SurveyKey]`), choices without scoring, and rows. `SurveyQuestion.Scoring` is never copied. The runner views, the evaluate endpoint and the API only see views. A unit test serializes the projection of every built-in control with a full key and asserts that no key value appears in the JSON (section 22).

### 8.5 Client behavior

`survey-runner.js` (Surveys module, ES module, no jQuery):

- Reads standard inputs by name through `FormData`, so most controls need no script.
- A control with custom UI (ranking, signature, slider) sets `ClientScript`, ships its own module, and dispatches `new CustomEvent('survey:change', { bubbles: true, detail: { questionId } })` after it updates its hidden inputs. The runner listens on the form.
- On change (debounced 300 ms): evaluates rules that only use `ConditionRule` sources available on the page with `crestapps-conditions` (`getHidden`) and shows or hides questions and choices; when a visible rule has a `Formula`, uses piping, or reads a calculated variable, it calls `POST /s/r/{responseId}/evaluate` (page answers in, visible ids and piped texts out). The server re-evaluates on every page post anyway.
- Autosave: debounced `POST /s/r/{responseId}/save` (no validation) every 10 seconds of change and on `visibilitychange`.
- Timer: shows the server deadline (`data-deadline-utc`), warns at 5 and 1 minutes, posts the page at zero; the server enforces the deadline regardless.
- Prevents double submit; keeps focus management and announces errors with an `aria-live` region.

### 8.6 Built-in controls

Each is a handler (+ scorer + analyzer where marked) in `Surveys.Core/Controls/{Name}/`, a settings driver and views in the module. "LMS" names the Learning type it replaces.

| Control | Phase | Settings (TSettings) | Scorer | Analyzer | LMS |
| --- | --- | --- | --- | --- | --- |
| `SingleChoice` | 2 | Display (Radio, Buttons, Dropdown, ImageTiles), Columns | correct choice or choice points | counts, % | SingleChoice, Survey (ungraded) |
| `MultipleChoice` | 2 | Display (Checkboxes, ImageTiles, TagBox), MinSelected, MaxSelected | all-or-nothing or partial, penalties | counts, % | MultipleChoice |
| `Boolean` | 2 | Display (YesNo, TrueFalse, Toggle, ConsentCheckbox), labels | key true/false | counts | TrueFalse |
| `ShortText` | 2 | InputType (Text, Email, Url, Phone, Number-like), MaxLength, Mask | accepted answers, case | samples, count | ShortText |
| `LongText` | 2 | Rows, MinWords, MaxWords, RichText (false) | manual (null) | samples, count, word cloud later | Essay |
| `Number` | 2 | Min, Max, Step, Decimals, Unit, Display (Input, Slider) | answer ± tolerance | avg, min, max, median, histogram | Numeric |
| `Date` | 2 | Mode (Date, Time, DateTime), Min, Max | none | min, max, by month | |
| `Rating` | 2 | Min, Max, Style (Stars, Numbers, Smileys), MinLabel, MaxLabel | none (choice points possible) | avg, distribution | Rating (Learning Surveys) |
| `Nps` preset of Rating | 2 | 0..10, labels | none | NPS (% 9–10 minus % 0–6), promoters/passives/detractors | |
| `Csat` preset of Rating | 2 | 1..5 | none | CSAT (% 4–5) | |
| `Matrix` | 2 | Mode (Single, Multiple), columns as Choices, rows as Rows, AllRowsRequired | per row key, partial | row × column table | |
| `Ranking` | 3 | MaxRanked, Display (DragList, Selects) | correct order, partial | average rank | Ordering |
| `Matching` | 4 | rows = prompts, choices = answers, ShuffleAnswers | pairs, partial | | Matching |
| `FillInBlanks` | 4 | Text with `[[blank]]` markers, CaseSensitive | per blank | | FillInBlanks |
| `ChoiceBlanks` | 4 | Text with blanks, Distractors, Display (DragIntoText, GapSelect) | per blank | | DragIntoText, GapSelect |
| `TextBlock` | 2 | Html (sanitized), ImageUrl; not answerable | — | — | Description |
| `FileUpload` | 3 | MaxFiles, MaxMegabytes, AllowedFormats (Attachments `FileFormats`) | manual | count | |
| `Hidden` | 3 | Variable name; not shown | — | counts | |
| `Calculated` | 3 | Formula, Display (Hidden, ShowValue), Format | optional | avg | |
| `ContactInfo` | 6 | Fields (Name, Email, Phone via PhoneNumbers parsing, Company, Address via Addresses), each required or not | — | — | |
| `Slider`, `ConstantSum`, `Signature`, `MultipleText`, `MatrixDropdown`, `ImageHotspot` | 8 | | | | |

### 8.7 Custom control walkthrough (goes into the docs and a skill)

A module `Acme.Surveys.Signature` adds a signature control:

1. `services.AddSurveyControl<SignatureControlHandler>("Acme.Signature", d => { d.DisplayName = new("Signature"); d.Category = SurveyControlCategories.Advanced; d.Icon = "fa-solid fa-signature"; d.Capabilities = Answerable | ClientScript; d.SettingsType = typeof(SignatureSettings); });`
2. `SignatureControlHandler : SurveyControlHandlerBase<SignatureSettings>` overrides `ReadAnswerAsync` (reads `q.{id}` data URL, stores it as an attachment through `IAttachmentService`, keeps the id in `AttachmentIds`), `IsEmpty` and `WriteColumns` (a signed link).
3. `services.AddDisplayDriver<SurveyQuestion, SignatureSettingsDisplayDriver>()` (derives from `SurveyControlSettingsDisplayDriver<SignatureSettings>`) plus `Views/SurveyControlSettings-Acme.Signature.Edit.cshtml`.
4. Views `SurveyControl.Respond-Acme.Signature.cshtml`, `SurveyControl.Review-Acme.Signature.cshtml`, optionally `SurveyControl.Summary-...`.
5. A script that draws on a canvas, writes the hidden input and dispatches `survey:change`.

Write `.agents/skills/crestapps-survey-controls/SKILL.md` with this walkthrough and the contract tables (AGENTS.md: a module that adds extension points ships a skill).

## 9. Logic

### 9.1 Visibility, enabled and required

- Pages, questions and choices carry `SurveyVisibility` (Always | ShowWhen | HideWhen). HideWhen is evaluated as NOT(rule).
- Condition sources: `{questionId}`, `{questionId}.{rowId}`, `var:{name}`, `score`, `score:{categoryId}`, `context:{key}` (values a consumer puts in `Response.Properties` through an `ISurveyConditionSourceProvider`), `collector`, `culture`.
- `SurveyLogicEngine.Evaluate(definition, plan, answers, variables)` builds one rules dictionary (`page:{id}`, `q:{id}`, `c:{questionId}:{choiceId}`), adds the structural rule "a question on a hidden page is hidden" by treating the page as a source, and calls `ConditionalVisibility.GetHidden`. Values come from `ISurveyControlHandler.GetConditionValue`. A hidden question answers nothing (the shared evaluator withholds it), so chains resolve in one call.
- After every page post, answers of questions that are now hidden are **removed** from the response (the earlier module's reset-until-stable idea; one pass suffices because `GetHidden` already resolved the chain). Validation errors of hidden questions are dropped.
- Publish-time validation: a rule may only read questions on the same page or earlier pages in the designed order, never a later page (no forward references); `ConditionalVisibility.FindCycle` must return empty; every source must exist; choice ids in values must exist.

### 9.2 Branching and endings

- When a page is completed, its `Branches` are evaluated in order; the first that holds jumps to a **later** page (validated at publish) or to an ending. Without a branch, the next page in plan order that is visible is next. Skipped pages are not in `PagePath`; their answers are dropped.
- After the last page, the first ending whose `Condition` holds is used, else the default ending. An ending with `Outcome = ScreenedOut` sets the response status to ScreenedOut (disqualified, not counted as completed by limits and quotas unless configured).
- Back pops `PagePath` and saves the current page without validation (`Navigation.AllowBack && page.AllowBack && !Sequential`).

### 9.3 Piping

- Syntax `{name}` in titles, descriptions, choice texts, ending messages and redirect URLs; `{name.rowName}`, `{var:name}`, `{score}`, `{score:category}`, `{percent}`. Resolved by `SurveyPipingRenderer` through `ISurveyControlHandler.FormatAnswer`, HTML-encoded, in the respondent's culture. Unknown names render empty. Validated at publish (names must exist and appear earlier).
- Ending messages and invitation e-mails also support Liquid with a model (`Survey`, `Response`, `Answers` by name, `Score`), rendered with Orchard's Liquid manager and sanitized.

### 9.4 Formulas and variables

- Reuse Orchard Core formulas: `ExpressionCompiler.Compile(formula, scope, catalog, S)` with `SurveyExpressionScope : IExpressionScope` that binds `[name]`, `[name.row]`, `[var:name]` (written `[name]` for variables too; names are unique across questions and variables), `[score]`, `[score:category]`, typed through `ISurveyControlHandler.GetFormulaType`.
- `SurveyExpressionFunctionCatalog : IExpressionFunctionCatalog` composes the built-in catalog (`IExpressionFunctionCatalog` from DI when Data Pipelines Core is enabled, else `ExpressionFunctionCatalog.BuiltIn`) with survey functions from `ISurveyExpressionFunctionProvider`s: `SELECTED([q], 'value')`, `COUNTSELECTED([q])`, `ANSWERED([q])`, `SCORE([q])`, `AGE([date])`. They are not added to the global catalog, so pipeline editors do not list them. Phase 0 check C1 verifies this composition.
- Compiled formulas are cached per version (version id + formula) since versions are immutable.
- Calculated variables are recomputed after every page post in dependency order (cycle = publish error) and stored in `Response.Variables`.
- Hidden variables come, in order of precedence, from the consumer (`StartResponseRequest.Variables`), the query string (only variables with `AcceptFromUrl`), the collector defaults, and the variable default.

### 9.5 Randomization

- At start, `SurveyPlanBuilder` creates the plan with a seed from `RandomNumberGenerator`: page order (if Shuffle, pinned first and last stay), per page the questions (pool draws: from inline members plus resolved bank candidates, take `DrawCount` with Fisher–Yates; then shuffle the page if `QuestionOrder == Shuffle`, keeping pinned questions in place), per question the choice order through `PreparePresentation` (shuffle with pinned and "other/none" at the end).
- Use a small deterministic PRNG of our own (`SurveyRandom`, xoshiro128** seeded by the seed) so tests are reproducible across runtimes; never `System.Random` with a seed. The plan is stored, so the PRNG only matters at creation.
- `Presentation.QuestionsPerPage = 1` splits pages into one-question screens at plan time (LMS "questions per page").
- Evenly present (phase 6): `SurveyPoolCounter` document with per-question presented counts; draws pick least-presented with random tie-break, under the survey lock.

### 9.6 Validation and security of logic

- Regex rules compile with a 100 ms timeout (`RegexOptions.None`, `matchTimeout`) at publish and at run time.
- Redirect URLs must be local or on `SurveySettings.AllowedRedirectHosts`; piped values are URL-encoded.
- Formulas are evaluated with the compiler's checks; failures log a warning and evaluate to missing (never throw to respondents).

## 10. Runtime: taking a survey

### 10.1 Services

```csharp
public interface ISurveyResponseService
{
    Task<SurveyStartResult> StartAsync(StartResponseRequest request, CancellationToken cancellationToken = default);
    Task<SurveyResponse> FindForRespondentAsync(string responseId, SurveyRespondentIdentity identity, CancellationToken cancellationToken = default);
    Task<SurveyPageResult> GetPageAsync(SurveyResponse response, CancellationToken cancellationToken = default);
    Task<SurveyPageResult> SubmitPageAsync(SubmitPageRequest request, CancellationToken cancellationToken = default);   // Next / Back / Finish
    Task<SurveySaveResult> SaveAsync(SavePageRequest request, CancellationToken cancellationToken = default);          // autosave, no validation
    Task<SurveyEvaluateResult> EvaluateAsync(EvaluatePageRequest request, CancellationToken cancellationToken = default);
    Task<SurveyCheckResult> CheckAnswerAsync(CheckAnswerRequest request, CancellationToken cancellationToken = default); // immediate feedback
    Task<SurveyResponseResult> SubmitAtDeadlineAsync(string responseId, CancellationToken cancellationToken = default);
    Task<SurveyResponseResult> ReopenAsync(string responseId, SurveyRespondentIdentity identity, CancellationToken cancellationToken = default); // AllowEditAfterSubmit
    Task<SurveyResponseResult> VoidAsync(string responseId, string actor, string reason, CancellationToken cancellationToken = default);
    Task<SurveyResponseResult> DeleteAsync(string responseId, string actor, CancellationToken cancellationToken = default);
}

public sealed class StartResponseRequest
{
    public string SurveyId { get; set; }        // or CollectorToken
    public string CollectorToken { get; set; }
    public string InvitationToken { get; set; }
    public SurveyRespondentIdentity Identity { get; set; }   // user id, party, resume token, IP, cookie key
    public RecordRef Context { get; set; }      // set by consumers (LMS enrollment)
    public Dictionary<string, string> Variables { get; set; } = [];
    public JsonObject Properties { get; set; }
    public bool IsTest { get; set; }
    public string Password { get; set; }
    public string Culture { get; set; }
}
```

Errors are typed codes (`SurveyErrors`: NotFound, NotOpen, Closed, NotAvailableNow, NotInAudience, SignInRequired, LimitReached, CooldownActive, MaxResponsesReached, PasswordRequired, PasswordInvalid, CaptchaRequired, InvitationInvalid, InvitationUsed, AlreadyInProgress, DeadlinePassed, NotOwner, Busy, InvalidAnswer, QuotaFull), mapped to localized messages in the module.

### 10.2 Start

Under a distributed lock `Surveys:Start:{surveyId}:{respondentKey}`:

1. Load the survey, the published version (version cache), the collector (by token, or the default collector) and the invitation.
2. Return the respondent's open in-progress response for the same survey and context if one exists (one open response per respondent and context), auto-submitting it first if its deadline passed.
3. Run every `ISurveyAccessRule.CheckStartAsync` in `Order` (10.4). First denial wins.
4. Build the plan, set `DeadlineUtc = now + TimeLimitMinutes + override.ExtraMinutes` (capped at the close time), compute `AttemptNumber`, create the response (`InProgress`, `CurrentPageId` = first visible page), create or bump `SurveyParticipation`, issue a resume token for non-signed-in respondents (cookie `sr_{surveyId}` HttpOnly, Secure, SameSite=Lax, path `/s`, plus `?t=` on the resume link), enqueue `SurveyDeadlineWork` when there is a deadline.
5. Raise `ISurveyResponseObserver.StartedAsync`.

### 10.3 Pages, saving and completing

- `SubmitPageAsync(Next)`: read answers of visible questions on the current page through handlers, merge into `Answers`, evaluate logic (9.1), drop hidden answers, validate visible answers (`ValidateAnswerAsync`; required-when respected), recompute variables. If valid: record the page visit, raise `PageCompletedAsync`, pick the next page (9.2). If none, complete (below). If invalid: re-render with errors, save the valid answers.
- `Back`: save without validation, pop `PagePath`.
- `Finish` = Next on the last page.
- **Complete** (under the start lock): re-run `ISurveyAccessRule.CheckCompleteAsync` (limits, max responses, quotas), score (11.4), choose the ending, set `Completed` or `ScreenedOut`, `CompletedUtc`, bump participation `CompletedCount`, write the answer index (through the provider), raise `CompletingAsync` (before save, observers may add properties) and `CompletedAsync` (after save, deferred with `ShellScope.AddDeferredTask` for workflows, notifications and webhooks).
- A save uses optimistic concurrency on the response document; a conflict returns `Busy` and the runner reloads the page.
- Immediate feedback (`FeedbackMode = Immediate`): `CheckAnswerAsync` scores one question, locks it (`CheckedUtc`), returns feedback allowed by `Review.During`.

### 10.4 Access rules

```csharp
public interface ISurveyAccessRule
{
    string Name { get; }
    int Order { get; }
    Task<SurveyAccessDecision> CheckStartAsync(SurveyAccessContext context);
    Task<SurveyAccessDecision> CheckCompleteAsync(SurveyAccessContext context);   // default: Allow
}
// services.AddSurveyAccessRule<T>();
```

Built-in rules (Core), in order: `SurveyStatusRule` (Open), `AvailabilityWindowRule` (survey and collector open/close, overrides' windows), `WeeklyScheduleRule` (site time zone via `ILocalClock`), `AudienceRule` (anyone, signed in, Users audiences via `IUserAudienceService.IsMemberAsync`, invitation only), `PasswordRule` (collector), `CaptchaRule` (`[RequireFeatures("OrchardCore.ReCaptcha")]`, only for not-signed-in respondents), `InvitationRule`, `RespondentLimitsRule` (total, per day, per week, per month, cooldown, using `SurveyParticipation` and the site's first day of week; overrides add attempts), `MaxResponsesRule` (survey and collector), `QuotaRule` (phase 6). Consumers add their own (Learning adds `LearningEnrollmentAccessRule`: enrolled, item released, access not ended). Uniqueness for public anonymous links uses a cookie key and is documented as weak (Qualtrics, SurveyMonkey do the same).

### 10.5 Routes and controllers

Respondent routes (no `admin` prefix; rate-limited group `Surveys.Respond`; anti-forgery on posts):

| Route | Action |
| --- | --- |
| `GET /s/{token}` | Landing (title, introduction, password or CAPTCHA if needed, Start button; or straight to the first page when `Presentation.SkipLanding`) |
| `POST /s/{token}/start` | Start, then redirect to the page |
| `GET /s/r/{responseId}` | Current page (resume). Requires the owner: signed-in user match, or a valid resume token |
| `POST /s/r/{responseId}` | Submit page (`direction` = next, back, finish) |
| `POST /s/r/{responseId}/save` | Autosave (JSON) |
| `POST /s/r/{responseId}/evaluate` | Visibility and piping (JSON) |
| `POST /s/r/{responseId}/check/{questionId}` | Immediate feedback (JSON) |
| `GET /s/r/{responseId}/done` | Ending page (no TempData; reads the response) |
| `GET /s/r/{responseId}/review` | Review, as the review policy allows |
| `GET /s/i/{invitationToken}` | Personal invitation link → start or resume |
| `GET /s/{token}/results` | Aggregate results for respondents when `ResultsVisibility` allows (polls) |

`SurveyRunnerController` stays thin and delegates to `ISurveyResponseService`. Pages render through the `SurveyPage_Respond` shape (layout: progress bar, page title, questions, navigation buttons, timer), overridable by themes (`SurveyPage_Respond__{SurveyName}`).

Responses for the response list "My responses" (signed-in respondent): `GET /surveys/my` lists their identified responses with Continue and Review links (LearnerPages-style), behind the feature's front-end menu.

### 10.6 Overrides and accommodations

`ISurveyOverrideService`: grant, list, revoke (reason required, audited). The start rule and deadline use the most generous override that applies (user, audience membership, context). Learning's "Overrides" page calls this service.

### 10.7 Background work

- `SurveyDeadlineBackgroundTask` (every minute): leases due `SurveyDeadlineWork` items and calls `SubmitAtDeadlineAsync` (grace `Timing.GraceSeconds`). Port tests from `AttemptDeadlineTaskTests`.
- `SurveyMaintenanceBackgroundTask` (hourly, batches of 500): in-progress responses idle longer than `IncompleteExpiryHours` → Abandoned (raise `AbandonedAsync`); retention: delete completed or abandoned responses older than the survey's `Retention.Days` (or anonymize, per `Retention.Action`); delete participation rows older than the largest limit window (45 days).
- `SurveyInvitationReminderTask` (phase 6), through Outreach scheduling where possible.

## 11. Versions and publishing

### 11.1 Editing

- The designer only edits `Survey.Draft`. Saving sets `HasUnpublishedChanges`. Draft saves use optimistic concurrency (two editors: the second gets "changed by someone else, reload").
- Ids are generated when an item is created in the designer and never change. **Duplicate** creates new ids (and new names with a `_copy` suffix).
- Removing a choice that has answers in any version is replaced by **Retire** (the editor offers retire instead of delete once answers exist). Removing a question with answers is allowed (it remains in old versions) but flagged at publish.
- Renaming a question `Name` after responses exist warns that export columns and data set fields follow the newest name.

### 11.2 Publishing

`ISurveyPublisher.PublishAsync(surveyId, notes, actor)`:

1. Validate the draft with `ISurveyDefinitionValidator` (built-in checks: unique ids and names, at least one answerable question, every control registered, conditions and branches per 9.1–9.2, formulas compile, regexes compile, pools draw ≤ candidates, scoring complete when scoring is enabled, endings valid, translations reference existing keys; plus every handler's `ValidateDefinitionAsync` and every `ISurveyDefinitionValidationContributor` from consumers). Errors block publishing; warnings show.
2. Resolve bank draws (11.3).
3. Diff against the published version with `SurveyDefinitionDiffer`: added, removed and changed pages, questions and choices; **destructive** flags: control type changed, question removed with answers, choice removed or retired, scoring key changed (affects comparisons), required added to an existing question. The publish page shows the diff and asks for confirmation when destructive.
4. Create `SurveyVersion` N+1 (immutable), set `PublishedVersionId/Number`, clear `HasUnpublishedChanges`, set `Status = Open` on first publish and create the default web-link collector.
5. Raise `PublishedAsync` (workflow event, audit trail).

In-progress responses keep their version and finish on it. **Restore** copies an old version's definition into the draft (audited). **Compare** shows the diff between any two versions or the draft.

### 11.3 Bank questions in versions

At publish, each pool's `BankDraws` are resolved into concrete candidate questions (non-retired bank questions matching bank, tags and difficulty), copied into the version's pool as pool members (`SurveyQuestion` with its stable bank question id as `Id` and the bank revision in `Properties["bankRevision"]`). The version is therefore self-contained. Limit: `MaxCandidates` per draw (default 500, hard cap 2,000) with a warning. When a bank question changes after publish, the survey list shows "Question bank changed since version N" with a one-click republish (decision D2).

### 11.4 Scoring

`ISurveyScoringEngine.Score(version, response)`:

- For each shown, answered or unanswered answerable question with points: the scorer returns points (null for manual) and max points; unanswered scores 0; hidden questions do not count toward `MaxScore`.
- Category totals, `Score` = sum (or `ScoreFormula` over categories), `Percent`, `Passed = Percent >= PassMarkPercent` when set, score band.
- `Grading = Pending` when any shown question needs manual grading, else `Graded` (or `NotScored` when scoring is off).
- Port scorer logic and its tests from `src/Core/CrestApps.OrchardCore.Learning.Assessments.Core/Scoring/*.cs` and `tests/CrestApps.OrchardCore.Tests/Learning/Assessments/QuestionScorerTests.cs`.

### 11.5 Grading and regrading

- `ISurveyGradingService`: `GradeAsync(responseId, scores by question, comment, actor)`, `RecordScoringHelpAsync` (AI-assisted flag), `RegradeAsync(responseId, actor, reason)`, `RegradeVersionsAsync(surveyId, fromVersionIds, keyVersionId, actor, reason)` (re-score old responses with the key of another version, matched by question id; only questions whose control type is unchanged), `ChangeScoreAsync(responseId, newScore, actor, reason)`. Each change appends a `ScoreChange` and raises `ScoreChangedAsync`.
- Grading queue: `ISurveyGradingQueue` pages responses with `Grading == Pending`, filtered by `ISurveyGradingScope` implementations (Learning adds course staff scope). Admin page **Surveys → Grading** on `<admin-list>`.

## 12. The designer and admin pages

Admin menu **Surveys** (icon `fa-solid fa-clipboard-list`), position after Content: Surveys, Question banks, Grading, Settings (site settings link). Reports appear under **Reports → Surveys**. Every page declares a `<breadcrumb>`; lists use `<admin-list>` with row drivers for `SummaryAdmin` and `SummaryAdminGrid`; forms use `ocat-*` cards and `action-bar-bottom`; times through `ILocalClock`.

| Page | Route (admin) | Notes |
| --- | --- | --- |
| Surveys | `surveys` | `CatalogAdminController<Survey>`-style index. Search in the action bar; toolbar filters: kind, status, owner (standalone or owned), tag. Row actions: Design, Preview, Collect, Results, Responses, Versions, Duplicate, Close/Open, Archive, Delete (only without responses, else archive). Owned surveys (LMS) are hidden by default (filter "Show surveys owned by other features") |
| New survey | `surveys/create` | Name, title, kind, template picker (`ISurveyTemplateProvider`: Blank, Customer satisfaction, NPS, Employee engagement, Event feedback, Course feedback, Quiz, Poll) |
| Designer | `surveys/{id}/design` | Three areas: outline (pages and questions, drag to reorder and move with SortableJS, add page, add question from a picker grouped by category with icons, from a bank, or duplicate), question editor (the `IDisplayManager<SurveyQuestion>` editor loaded by fetch into a side panel, saved by fetch), and tabs for survey-wide settings (`IDisplayManager<SurveyDefinition>` editor groups: General, Presentation, Navigation, Scoring and review, Timing, Endings, Variables, Score categories and bands, Translations). The bottom action bar: Save draft, Preview, Publish (with the diff page), Discard draft (secondary) |
| Access | `surveys/{id}/access` | `IDisplayManager<Survey>` group `Access` (live settings, 7.6), retention, anonymity |
| Logic overview | `surveys/{id}/logic` | Read-only map: per page, visibility rules, branches and endings in plain words; problems highlighted |
| Preview | `surveys/{id}/preview?version=draft` | Runs the draft as a test response (`IsTest`), with a banner, a "jump to page" menu and a "show hidden questions" toggle |
| Versions | `surveys/{id}/versions` | Number, published (local time), by, responses, notes; View (read-only render), Compare, Restore to draft |
| Collectors | `surveys/{id}/collectors` | Web links (URL, copy, QR SVG download, open/close, limits, password, variables), embeds (snippet), kiosk links, invitations (phase 6) |
| Responses | `surveys/{id}/responses` | Number, respondent (or "Anonymous"), version, status, grading, started and completed (local), duration, score, passed; filters: status, version, collector, date range, test responses; bulk: void, delete, export (via the Responses report export). Row: View, Grade, Void, Delete |
| Response | `surveys/responses/{id}` | Answers page by page as the respondent saw them (Review shape, all details for staff), page visits timeline, variables, score breakdown per question and category, score history, grading form when pending |
| Grading | `surveys/grading` | Queue across surveys with scope |
| Overrides | `surveys/{id}/overrides` | Grant and revoke |
| Question banks | `surveys/banks`, `surveys/banks/{id}` | Banks list; bank page lists questions (`<admin-list>`, filters: type, tag, difficulty, retired), add or edit questions with the same question editor, import (CSV/XLSX, QTI), move, retire |
| Settings | Configuration → Settings → Surveys | `SurveySettings` (site settings driver with its permission registered, see `SiteSettingsGroupPermissionRegistration` test) |

Designer scripts (`Assets/js/designer/*.js`, Vite via `Assets.json`, ES modules, no jQuery, SortableJS for ordering, vanilla bootstrap-select): outline operations post small JSON commands (`addPage`, `addQuestion`, `move`, `delete`, `duplicate`) to `admin/surveys/{id}/draft/commands`, which apply them to the draft on the server and return the outline partial. The rule builder (`rule-builder.js`) edits a `ConditionRule` (with nested groups) against a JSON list of available sources (earlier questions with their choices), and writes a hidden input; the server validates it.

## 13. Collecting responses

### 13.1 Web links and kiosk

- Default web-link collector on first publish; more can be added with their own windows, limits, passwords and variables.
- QR: SVG generated with QRCoder on demand (`admin/surveys/collectors/{id}/qr.svg`).
- Kiosk collector: after completion, the ending shows for N seconds then returns to the landing page, clears the resume cookie, and never resumes a previous response.

### 13.2 Embedding (feature Survey Embedding, phase 6)

- `SurveyPart` (attachable): survey picker (`ItemSelector` with a search endpoint, see memory "Reusable UserPicker + ItemSelector"), collector picker, display (Inline, Button that opens the runner, Link). Renders the runner inline in an iframe-free container that posts to the `/s` routes.
- `Survey` widget content type with `SurveyPart`.
- Shortcode `[survey name="customer-satisfaction" collector="..."]`.

### 13.3 Invitations (feature Survey Invitations, phase 6)

- Invitation collector: recipients from users, Users audiences, cohorts, parties (contacts, customers through `IPartySource.GetContactInfoAsync`) or a CSV of e-mails. Each recipient gets a `SurveyInvitation` with a token (signed with `SignedTokenService`, hash stored), an expiry and a personal link `/s/i/{token}`.
- Delivery: users through Notifications (`AddNotificationType("SurveyInvitation")`), non-users by e-mail (`ITemplatedEmailSender`) with a Liquid template edited on the collector; batches and reminders through Outreach (`IOutreachEngine`, its recipient policy handles opt-outs). SMS later through Omnichannel.
- Reminders: up to N, every D days, only to Pending/Sent/Opened/Started; stop on completion.
- One response per invitation (`InvitationRule`); anonymity keeps the invitation id off the response (participation only).

### 13.4 Consumers starting responses for a context

Other modules (LMS, Tickets, Contact Center, Orders) call `ISurveyResponseService.StartAsync` with a `Context` and their own collector kind, or create a personal link with `ISurveyLinkService.CreateLinkAsync(surveyId, context, respondent, expiresUtc)` (used by the **Create survey link** workflow task, section 17). Results can be filtered by context kind.

## 14. Results and reports

Under **Reports → Surveys**, each `IReport` with a filter driver (`SurveyReportFilterDisplayDriver`: survey, version scope (published, a version, all versions joined by question id), date range (`DefaultDateRange` last 30 days), collector, context kind, include test responses) and an export permission:

| Report (`Name`) | Content |
| --- | --- |
| Survey results (`survey-results`) | Metrics (responses, completion rate, median duration, NPS/CSAT when present), then one section per question from its analyzer (`ToReportSections`: bars, tables, NPS breakdown, matrix tables, text samples with "see all" links to Responses filtered). Option: segment by a choice question (simple cross-tab: a column per segment) |
| Survey responses (`survey-responses`) | One row per response, a column per question export column (`DescribeColumns`, union across versions by question id, newest names), plus metadata columns; CSV/Excel export through the Reports export formats (CsvWriter guard) |
| Quiz results (`survey-scores`) | Score distribution, pass rate, average by category, per respondent best and latest |
| Item analysis (`survey-item-analysis`) | Port `ItemAnalysisCalculator` (difficulty, discrimination) over `SurveyAnswerIndex` |
| Completion funnel (`survey-funnel`) | Respondents reaching and leaving each page (from `PageVisits`), median time per page |
| Participation (`survey-participation`) | For an audience: who completed, who has not (identified surveys only; refuses anonymous surveys) |
| Satisfaction trend (`survey-trend`) | NPS/CSAT/rating average by day, week or month (local dates) |

Aggregates are SQL queries over `SurveyAnswerIndex` grouped by question and choice (`SurveyAnswerAggregates` in Core, Dapper over the YesSql connection with table prefix handling, like `AssessmentAttemptStore.ListPassedUserIdsSinceAsync`). Report runs never load all responses; the responses report pages by 500. Anonymous surveys apply `MinimumGroupSize` to segments and filters.

The Survey list's **Results** row action opens `survey-results` with the survey preselected. Admin Dashboard widgets (phase 5): responses in the last 7 days per survey, pending grading, recent "response matches condition" alerts.

## 15. Permissions

`SurveysPermissions` (static class in Surveys.Core, `LocalizationSource` descriptions), provider in the module, default stereotypes (Administrator all; Editor manage own, view responses of own; Authenticated `RespondToSurveys`; Anonymous none except public collectors, which need no permission).

| Permission | Implied by | Grants |
| --- | --- | --- |
| `ManageSurveys` | — | All surveys, collectors, versions, settings of surveys |
| `ManageOwnSurveys` | ManageSurveys | Surveys the user owns (`IAuditedModel.OwnerId`) |
| `PublishSurveys` | ManageSurveys | Publish, restore, close (otherwise the designer can only save drafts) |
| `ViewSurveyResponses` | ManageSurveys | Responses and reports of all surveys |
| `ViewOwnSurveyResponses` | ViewSurveyResponses, ManageOwnSurveys | Responses of own surveys |
| `ExportSurveyResponses` | — (security-critical) | Exports, data sets with respondent identity, recipe export of responses |
| `DeleteSurveyResponses` | ManageSurveys | Void and delete |
| `GradeSurveyResponses` | ManageSurveys | Grading queue (within scopes) |
| `ManageSurveyOverrides` | ManageSurveys | Overrides |
| `ManageQuestionBanks` | — | Banks and bank questions |
| `ViewQuestionBanks` | ManageQuestionBanks | Read banks (pick questions in the designer) |
| `ManageSurveyInvitations` | ManageSurveys | Invitations |
| `RespondToSurveys` | — | Take surveys whose audience is SignedIn or Audiences |
| `ManageSurveySettings` | — | Site settings group |

Resource-based checks go through `ISurveyAuthorizationService` (survey + operation), which asks `ISurveyAccessProvider`s; Learning adds one so course staff manage surveys owned by their course items without global permissions.

## 16. Data portability: recipes, deployment and schemas

Every record type gets a recipe step (`AddRecipeExecutionStep`), a deployment step (`AddDeployment` with step, source, display driver and the three views `Views/Items/{Step}.{Summary,Thumbnail,Fields.Edit}.cshtml`), an `IRecipeStep` schema and an `IDeploymentStepSchemaDefinition`, the schemas in a startup gated by `CrestAppsRecipesConstants.Feature.Area` (pattern `src/Modules/CrestApps.OrchardCore.AI/PersonasRecipeSchemasStartup.cs`). `RecipeSchemaCoverageArchitectureTests` must stay green. Recipe item ids are generated unique ids, never readable names (memory rule).

| Step name | Records | Kind | Base | Notes |
| --- | --- | --- | --- | --- |
| `Surveys` | Survey with draft, access, retention, and its **versions** (array) and **collectors** (array) | Configuration | `NamedCatalogRecipeStep<Survey>` + custom handling of versions | Import matches by `Name`. Versions import by `(SurveyId, Number)`: existing numbers are skipped (immutable), missing ones are created; `PublishedVersionNumber` is set last. Collector password hashes and tokens export only when the deployment step option "Include secrets" is on (needs `ExportSurveyResponses`); otherwise new tokens are generated on import. `AnonymitySecret` is never exported (a new one is generated; decision D9) |
| `SurveyQuestionBanks` | QuestionBank + its BankQuestions | Configuration | `NamedCatalogRecipeStep<QuestionBank>` | Bank questions match by `ItemId`, then `SourceIdentifier` |
| `SurveyResponses` | Responses (opt-in deployment step with options: surveys, date range, include test, include in-progress) | Respondent record | `CatalogStoreRecipeStep<SurveyResponse>` | Pages of 500; needs `ExportSurveyResponses`; audited; people by user name (rows that do not match are recipe errors); anonymous responses export without identity |
| `SurveyOverrides` | Overrides | Respondent record | catalog | By user name |
| `SurveyInvitations` | Invitations (opt-in) | Respondent record | catalog | Token hashes only |
| `SurveySettings` | Site settings | Configuration | site settings | With an `ISiteSettingsSchemaDefinition` |

Imports fill cross-references (collector → survey, bank draws → banks, response → version) in a second pass. The recipe JSON shape of `SurveyDefinition` is documented with a full JSON schema (built by `SurveyDefinitionSchemaBuilder`, which also asks each control descriptor for its settings schema through `ISurveyControlSchemaProvider`, default: reflection over `SettingsType`), so AI tools and authors can write survey recipes. A sample recipe `Recipes/surveys-samples.recipe.json` creates a CSAT survey, an NPS survey, a quiz and a bank.

## 17. Workflows

Startup gated by `OrchardCoreFeatureIds.Workflows`; activities registered with `AddActivity<TActivity, TDriver>()`; each with Design, Edit and Thumbnail views (`WorkflowActivityViewsArchitectureTests`) and an `IWorkflowActivitySchemaDefinition` in the recipe-schema startup. Events fire after the change is saved, deferred with `ShellScope.AddDeferredTask`, correlation id = response id (survey id for survey events), through `SurveysWorkflowObserver : ISurveyResponseObserver`. Events caused by a workflow task carry an actor; every event activity has an **Include changes made by workflows** checkbox (default off) so a workflow does not trigger itself (pattern `src/Modules/CrestApps.OrchardCore.Learning.Assessments/Workflows/AttemptEventActivity.cs`).

Base `SurveyResponseEventActivity : EventActivity` with filters: surveys (multi-select), kinds, collectors, context kinds; outcome `Done`. Input: `Response`, `Survey`, `SurveyVersion`, `Answers` (dictionary by question name → display text), `AnswerValues` (name → raw values), `Score`, `Passed`, `Context`.

| Event | Extra filter | When |
| --- | --- | --- |
| Survey response started | | Start |
| Survey page completed | Pages (by name) | Each valid Next |
| Survey response completed | | Completed |
| Survey response screened out | Endings | ScreenedOut |
| Survey response abandoned | | Maintenance task |
| Survey response needs grading | | Completed with Pending |
| Survey response graded | | Grading done (manual or automatic) |
| Survey response passed / failed | | After scoring with a pass mark |
| Survey response score changed | | Regrade, manual change |
| Survey response matches condition | A `ConditionRule` (rule builder in the activity editor) and an optional formula | Completed, when it holds: generalizes "below threshold" alerts |
| Survey published | | Publish |
| Survey closed | | Close (manual, close date, max responses) |
| Survey quota reached (phase 6) | Quotas | Counter reaches the limit |

| Task | Outcomes | Does |
| --- | --- | --- |
| Create survey link | Done, Failed | Creates a personal link for a respondent (user, party or e-mail) and a context (`RecordRef` from Liquid), outputs `SurveyLink` for e-mail or SMS tasks |
| Send survey invitation | Done, Failed | Creates and sends an invitation (Invitations feature) |
| Close survey / Open survey | Done | |
| Void survey response | Done, Failed | With reason |
| Regrade survey response | Done, Failed | With reason |
| Grant survey override | Done, Failed | User, extra minutes and attempts, reason |
| Set response property | Done | Writes a value into `Response.Properties` (for downstream steps) |

## 18. Data Pipelines and the Report Builder

`SurveysDataSource : RecordDataSource, IDataSource` (`Name = "Surveys"`), registered with `AddDataSource<SurveysDataSource>()` in a startup gated by `OrchardCoreFeatureIds.DataPipelinesCore` (template: `src/Modules/CrestApps.OrchardCore.Approvals/Reports/ApprovalsReportDataSource.cs`). Data set and field names are never renamed once shipped. `RestrictAsync` limits rows to surveys the user may view responses of; identity fields (respondent, user, e-mail, IP) require `ExportSurveyResponses` and are empty for anonymous responses.

| Data set | Base | Rows | Key fields |
| --- | --- | --- | --- |
| `Surveys` | `IndexedReportDataSet<Survey, SurveyIndex>` | One per survey | Id, Name, Title, Kind, Status, Owner, PublishedVersion, Created, Modified |
| `SurveyVersions` | indexed | One per version | SurveyId, Number, PublishedUtc, PublishedBy |
| `SurveyQuestions` | custom (reads versions through the cache) | One per question per version | SurveyId, VersionNumber, QuestionId, Name, ControlType, Title, Page, Position, Points, Category |
| `SurveyResponses` | `IndexedReportDataSet<SurveyResponse, SurveyResponseIndex>` (`DateColumn = ModifiedUtc` for incremental runs) | One per response | Id, Number, SurveyId, SurveyName, VersionNumber, Status, Grading, Respondent, UserName, ContextKind, ContextId, Collector, Attempt, StartedUtc, CompletedUtc, DurationSeconds, Score, MaxScore, Percent, Passed, Ending, IsTest |
| `SurveyAnswers` | `IndexedReportDataSet<..., SurveyAnswerIndex>` | One per answer value (long form) | ResponseId, SurveyId, VersionNumber, QuestionId, QuestionName, RowId, ChoiceId, ChoiceValue, ChoiceText, NumberValue, DateValue, TextValue, Score, MaxPoints, CompletedUtc |
| `SurveyCategoryScores` | custom | One per response and category | ResponseId, Category, Score |
| `SurveyPageVisits` | custom | One per page visit | ResponseId, PageName, EnteredUtc, LeftUtc, Seconds |
| `SurveyInvitations` | indexed (Invitations feature) | One per invitation | SurveyId, Collector, Recipient, Status, SentUtc, Reminders, ResponseId |
| `SurveyResponses_{surveyName}` (one per survey, dynamic) | custom `IRecordDataSet` | One per response, wide | Metadata fields + every `DescribeColumns` column of every version joined by question id (newest name; fields typed through `GetFormulaType`) |

The dynamic per-survey sets are the ones the Report Builder and pipelines use most ("export my survey to Excel every Monday"). Phase 0 check C2 confirms `RecordDataSource` supports data sets created at run time (the earlier module did it with one set per survey version).

## 19. Other integrations

1. **Observers.** `ISurveyResponseObserver` (default no-op methods): `StartedAsync`, `PageCompletedAsync`, `CompletingAsync`, `CompletedAsync`, `ScreenedOutAsync`, `AbandonedAsync`, `NeedsGradingAsync`, `GradedAsync`, `ScoreChangedAsync`, `VoidedAsync`, `DeletedAsync`, `ReopenedAsync`, `OverrideGrantedAsync`. `ISurveyObserver`: `PublishedAsync`, `ClosedAsync`, `DeletedAsync`. Registered with `AddSurveyResponseObserver<T>()`. Workflows, notifications, webhooks, audit trail, telemetry and Learning are observers.
2. **Notifications** (gated): types `SurveyInvitation`, `SurveyReminder`, `SurveyResponseGraded` (to the respondent, identified only), `SurveyNeedsGrading` (to graders in scope), each with `Type`, `ReferenceId` and `Url`, plain localized text, switchable per type.
3. **Audit trail** (gated): survey created, published, version restored, closed, archived, deleted; access settings changed; responses exported; response voided, deleted, reopened; score changed; override granted or revoked; anonymize existing responses.
4. **Webhooks** (gated): `AddWebhookEvent` for `survey.published`, `survey.response.completed`, `survey.response.graded` with stable ids and payloads (identity omitted for anonymous).
5. **Personal data** (`AddPersonalDataContributor`): export a person's identified responses (answers formatted through handlers) and invitations; erasure anonymizes their responses (drops `UserId`, `Respondent`, `IpAddress`, invitation link; optional: blank free-text answers), deletes invitations and participation rows, deletes their uploaded files.
6. **Rate limits** (gated): route group `Surveys.Respond` (start, page post, save, evaluate, check) per IP and per user.
7. **Telemetry**: meter `CrestApps.OrchardCore.Surveys`: responses started, completed, abandoned, screened out; page post duration; scoring duration.
8. **Admin dashboard** widgets (gated), section 14.
9. **AI** (module Surveys.AI, phase 8): tools `list_surveys`, `get_survey_results` (summary through analyzers, permission-checked), `create_survey_draft` (from a prompt or a document, writes a draft definition validated by the JSON schema, never publishes); on-demand **Summarize answers** for a text question in the results report (themes and sentiment, with a prompt template under `Templates/Prompts/`); translation of a survey into another culture into `Translations`; AI-assisted essay scoring moved from `Learning.AI` (`EssayScoringAssistant`) to work on `SurveyAnswer` through `ISurveyGradingService.RecordScoringHelpAsync`.
10. **API** (feature Surveys API): `GET /api/surveys` (manage permission), `GET /api/surveys/{id}/definition` (respondent projection for a collector token), `POST /api/surveys/responses` (start), `PUT .../pages/{pageId}` (answers), `POST .../complete`, `GET /api/surveys/{id}/responses` (view permission, paged). `Api` scheme, Problem Details, OpenAPI descriptions (pattern: Calendars and Scheduling APIs).

## 20. Learning on Surveys (phase 7)

### 20.1 Target state

| Today | After |
| --- | --- |
| `Learning.Surveys`: `SurveyPart` with inline questions, `SurveyResponse` store, `ISurveyResponseService`, results page | `Learning.Surveys` keeps the course item kind "Survey" and the `FeedbackSurvey` and `Choice` content types; `SurveyPart` becomes `CourseSurveyPart { string SurveyId }`. The survey is a Surveys record with `Owner = Content:{courseItemContentItemId}`, created on the first save of the course item. The course item editor shows the survey summary and an **Edit questions** button that opens the Surveys designer (owned surveys get a breadcrumb back to the course). The learner player embeds the runner with `Context = LearningEnrollment:{enrollmentId}`. `LearningCourseItemObserver : ISurveyResponseObserver` records `CourseItemResult { Submitted = true }`. Results come from the Surveys report filtered by context. `Anonymous`, `AllowChangeAnswer`, `CloseUtc`, `ResultsVisibility` map to Surveys settings (Anonymity, AllowEditAfterSubmit, Access.ClosesUtc, Review.ResultsVisibility). "Choice" uses the Poll template (one question) |
| `Learning.Assessments`: 13 question content types, `QuestionBank` content type, `QuizPart` with every quiz setting, `AssessmentAttempt`, `AttemptItemIndex`, overrides, deadline queue, grading queue, regrade, item analysis, QTI and Moodle import, workflow events, data source, gradebook | `QuizLesson` keeps `CourseItemPart{Kind="Quiz"}`; `QuizPart` becomes `{ string SurveyId; GradingMethod GradingMethod; }` (the survey is owned by the course item, `Kind = Quiz`). Every other quiz setting moves to the survey definition or access settings. Attempts are `SurveyResponse`s with the enrollment context; `LearningQuizObserver` reports `CourseItemResult` through `QuizGradeCalculator` (kept) over the enrollment's responses. Question types, banks, attempts, overrides, deadlines, grading queue, regrade, item analysis and QTI live in Surveys. Learning keeps: the quiz course item, `LearningGradingScope : ISurveyGradingScope` (course staff), `LearningEnrollmentAccessRule`, `LearningSurveyAccessProvider : ISurveyAccessProvider`, the gradebook and grade scales, and the Moodle importer (now creating surveys and banks through `ISurveyAuthoringService`) |

### 20.2 Consumers to change (from the inventory)

- **Certifications.Core**: `LearningCertificationsCoreServiceCollectionExtensions.cs:72-75`, `Requirements/AssessmentPassedRequirement.cs` (read `SurveyResponse`s by enrollment context and owned survey), `Services/CertificationObservers.cs` (`ISurveyResponseObserver` instead of `IAttemptObserver`). Module: `Startup.cs:192` (now requires Surveys), `Controllers/CertificationPathsController.cs:162`, `Drivers/CertificationPathDisplayDriver.cs:165`, `Services/CertificationReviewActionProvider.cs:48`.
- **Credentials**: `LearningCredentialsCoreServiceCollectionExtensions.cs:50`, `Rules/CredentialAttemptObserver.cs`, `Rules/QuizPassedRuleKind.cs`, `Startup.cs:87`.
- **Coursework**: `SubmissionGradingQueueSource.cs` (stays an LMS queue source; the Surveys grading queue and the Coursework queue are both shown on the LMS grading page through Learning's `IGradingQueueSource`, which now lives in Learning base or Coursework), `CourseworkGradingController.cs` (permission `SurveysPermissions.GradeSurveyResponses` or a Learning permission), its manifest dependency on `Assessments.Gradebook` (Gradebook stays in Learning.Assessments).
- **Learning.AI**: `AssessmentsAIToolsStartup`, `ExplainMyQuizResultTool`, `QuizResultExplainer` (read responses), `EssayScoringAssistant` (moves to Surveys.AI, Learning.AI keeps a thin LMS-scope wrapper or drops it), `LearningAuthoringAssistant`, `AuthoringDraftBuilder` (build survey definitions, supporting every registered control rather than five types), `AuthoringController`, `LearningRetrievalFilter.cs:126-132` (questions are no longer content; drop that filter branch).
- **Moodle import** in Assessments: `MoodleCourseRows.cs:52-76`, `MoodleImportHandler.cs:203-206`, `MoodleQuestionMapper` (map Moodle question types to Surveys controls).
- **Learning base**: `learning-roles.recipe.json` permission names, `LearningTelemetry.cs:78-107` quiz counters (move to Surveys telemetry or keep LMS counters fed by the observer), Moodle reader models stay.
- **Docs**: `modules/learning-surveys.md`, `modules/learning-assessments.md`, `user-manual/learning/surveys.md`, `quizzes.md`, `grading.md`, `learning-ai.md`.
- **Tests**: every test listed in the inventory under `tests/CrestApps.OrchardCore.Tests/Learning/Assessments/`, `Learning/Surveys/`, `Learning/AI/`, `Learning/Moodle/` and the integration tests (`QuizPagesTests`, `QuizEditorTests`, `QuizScheduleTests`, `AssessmentAdminFormsTests`, `LearningActivitiesTests`, `QtiImportTests`, `MoodleImportTests`, `RecipeRoundTripTests`), plus `tests/js/learning/quiz-*.test.js`. Scorer tests move to `tests/CrestApps.OrchardCore.Tests/Surveys/`.

### 20.3 Workflow events, webhooks and data sets (decided: Surveys replaces them)

Everything about responses, attempts, answers and scores belongs to Surveys, so the Learning copies go away. Learning keeps only events about learning itself (enrollment, course completion, certification and so on).

- **Workflow activities removed** from Learning.Assessments: `AttemptStartedEvent`, `AttemptSubmittedEvent`, `AttemptNeedsGradingEvent`, `AttemptGradedEvent`, `AttemptScoreChangedEvent`, `VoidAttemptTask`, `RegradeAttemptTask`, their drivers, views, schemas and `AssessmentsWorkflowObserver`. The Surveys events and tasks (section 17) replace them. They carry `Context` and `Response.Properties` (`CourseId`, `EnrollmentId`, `CourseItemId`, set by Learning when it starts a response) and can be filtered by context kind `LearningEnrollment`. Learning's course-level events (Course item completed, Course completed) keep firing from progress, unchanged.
- **Webhooks removed**: `AttemptSubmitted` and `AttemptGraded`. Surveys' `survey.response.completed` and `survey.response.graded` replace them; their payload includes `context` and `properties`, so a receiver sees the course and enrollment.
- **Data source removed**: `LearningAssessments` (`LearningAttempts`, `LearningAttemptAnswers`, `LearningAttemptScoreChanges`, `LearningQuizOverrides`) and `QuizItemAnalysisReport`. The Surveys data sets and reports replace them (filter by context kind). To report by course, Learning contributes `CourseId`, `CourseTitle` and `EnrollmentId` fields to the Surveys `SurveyResponses` data set through an `ISurveyResponseFieldProvider` (Surveys.Abstractions: `DescribeFields`, `WriteFields(response, row)`), registered in a Learning startup gated by Surveys and Data Pipelines. If phase 0 check C2 shows fields cannot be contributed, Learning adds one data set `LearningQuizResponses` over `SurveyResponseIndex` filtered by context instead.
- **Notifications, audit trail, telemetry**: `LearningAttemptGraded` and the attempt audit events are removed; Surveys' `SurveyResponseGraded` notification and audit events replace them. `LearningTelemetry` quiz counters are removed (Surveys telemetry covers responses).
- Learning's role recipe (`learning-roles.recipe.json`) grants the Surveys permissions instead of the removed Assessments ones.

### 20.4 No data migration (decided)

The LMS was never in production. Learning code, content types, records, permissions, routes, events, webhooks and data sets change in place:

- Delete the 13 question content types, the `QuestionBank` content type, the old parts and their migrations' create steps; reset the Learning.Assessments and Learning.Surveys migrations so `CreateAsync` builds only the new shape (version 1). Do **not** write `UpdateFromX` steps, converters or compatibility code.
- Delete `AssessmentAttempt`, `AttemptItem`, `AssessmentOverride`, the attempt and override stores and indexes (`AssessmentAttemptIndex`, `AttemptItemIndex`, `AssessmentOverrideIndex`, `QuestionSourceIndex`), `AttemptDeadlineWork`, `AttemptDeadlineBackgroundTask`, the Learning `SurveyResponse` store and `SurveyResponseIndex`, and their recipe and deployment steps (`LearningAttempts`).
- Developers reset their local test sites (fresh setup) after pulling phase 7; say so in the PR description.
- No breaking-change notes for Learning in the changelog (it is unreleased); the 3.0.0 changelog describes the final shape only.

## 21. Security and privacy

| Threat | Control |
| --- | --- |
| Answer keys leak to respondents | Respondent projection (8.4); keys never in views, evaluate or API payloads; test over every control |
| Someone resumes another person's response | Owner check: signed-in user id, or resume token (256-bit, hashed at rest, HttpOnly cookie); response ids are random, not sequential |
| Limit bypass with two tabs | Limits re-checked at completion under a lock |
| Ballot stuffing on public links | Rate limits, CAPTCHA option, cookie uniqueness (documented as weak), max responses, invitations for strict one-per-person |
| Timing and score tampering | Deadlines, scores and hidden-answer clearing on the server; client values ignored |
| XSS through author HTML or piping | `IHtmlSanitizerService` on save for all author HTML; piped values HTML-encoded; Liquid output sanitized |
| Open redirect | Redirect URLs local or allow-listed |
| CSV injection in exports | `CsvWriter` guard |
| ReDoS in validation regexes | 100 ms timeout |
| Malicious uploads | Attachments policy (formats matched by bytes, size, scanning), signed links, access provider checks survey permissions |
| Re-identification of anonymous respondents | No identity stored, participation unlinked, minimum group size in reports, IP off |
| Recipe import of secrets | Tokens and password hashes only with explicit option; anonymity secrets never exported |
| Cross-tenant data | Standard tenant isolation; version cache keyed per tenant shell |
| Personal data | Export and erasure (19.5), retention and incomplete expiry (10.7) |

## 22. Tests and checks

Write tests first (red, then green) per AGENTS.md. Unit tests in `tests/CrestApps.OrchardCore.Tests/Surveys/` (sealed `{Class}Tests`, methods `{Method}_{Scenario}_{Expected}`), in-process integration tests in a new `tests/CrestApps.OrchardCore.Surveys.IntegrationTests/` (copy the fixture pattern of `tests/CrestApps.OrchardCore.Learning.IntegrationTests/LearningSiteFixture.cs`), JS tests in `tests/js/surveys/`.

Must-have tests:

1. **Conditions**: operators, nested groups, parity vectors in C# and JS (5.1–5.2).
2. **Controls**: per built-in control: read, validate, condition value, formula value, format, columns, index rows, score (ported scorer tests), summary.
3. **Projection**: no key value of any control appears in the respondent JSON.
4. **Architecture**: no `switch`/`if` on a control type name outside `Controls/{Name}/` (scan Surveys sources for string literals of registered control names); every registered control has Respond and Review views and a settings driver; every Surveys record type has a recipe step, deployment step and schemas; activity views exist; index declarations.
5. **Logic**: visibility chains, hide-when, choice visibility, branches forward only, cycle detection, hidden answers cleared, piping, formulas, calculated variables order.
6. **Plan**: pools draw N of M, pinned items, deterministic PRNG with a fixed seed, one question per page.
7. **Runtime**: start, resume, back without validation, autosave, finish, ending selection, screened out, deadline auto-submit, immediate feedback lock, reopen.
8. **Access rules**: window, weekly schedule across time zones and DST, audience, password, limits per day, week (site first day of week), month, total, cooldown, completion re-check with two parallel responses, max responses, overrides.
9. **Anonymity**: no identity stored, participation unlinked, resume token required, minimum group size.
10. **Versions**: publish creates an immutable version, in-progress responses keep their version, restore, diff destructive flags, bank snapshot, refuse deleting referenced versions.
11. **Scoring and grading**: partial credit, penalties, categories, pass mark, bands, manual grading, regrade across versions, score history.
12. **Recipes**: round trip of every step (export then import into an empty tenant gives equal records); versions not duplicated on re-import.
13. **Data source**: fields, restrictions, anonymous identity blanking, incremental date column, dynamic per-survey set.
14. **Workflows**: each event fires once with the right input; the condition event; tasks.
15. **Reports**: results sections per control; responses export columns across versions.
16. **Integration (HTTP)**: designer save and publish, respondent flow on a public link, quiz with an essay through grading, LMS quiz and survey course items after phase 7, recipe round trip.
17. **Learning on Surveys** (phase 7): a quiz course item creates and owns its survey; a learner's attempts are responses with the enrollment context; attempt limits, overrides, deadlines and grading scope work; certification and credential rules fire from Surveys observers; course results come from `QuizGradeCalculator` over responses; deleting a course item deletes or archives its owned survey.

Checks before each push (memory rules): full solution build like CI (`dotnet build -c Release -warnaserror /p:TreatWarningsAsErrors=true /p:RunAnalyzers=true`), `npm run rebuild`, `npm run lint`, the full unit test run once at the end, a browser pass on a fresh test site (admin / `Password1!`) covering designer, preview, publish, respond, results, grading, recipes, and the LMS pages after phase 7, docs `npm run check:manuals` and `npm run build`, public API baselines updated.

## 23. Order of work

One branch and one PR for the whole plan, one commit per slice (memory: fewer PRs). Each phase ends green (build, tests, lint) before the next starts. The implementer updates section 26 after each slice.

### Phase 0: checks and decisions

- Decisions are answered (section 24); nothing to ask before starting.
- Run checks C1–C8 (section 25) as small spikes in the scratchpad; record results in section 26.

### Phase 1: building-block changes

1. Conditions operators and nested groups (5.1) with tests.
2. Move the JS evaluator to Resources, ContentFields imports it, parity vectors (5.2).
3. Update `building-blocks.md` Conditions row.

Acceptance: existing ContentFields, Work Orders and audience tests green; new vectors pass in both runners.

### Phase 2: Surveys foundation and designer

1. Projects, slnx, Targets, manifest, `SurveysConstants`, permissions, admin menu.
2. Models (7.1–7.6, 7.8 overrides), stores, indexes and providers, migrations (index schemas), version cache (`ISurveyVersionCache`, `IMemoryCache`, key `{tenant}:{versionId}`).
3. Control registry, base handlers, built-in phase-2 controls (8.6) with settings drivers and views (Respond, Review, Print, Summary).
4. `ISurveyManager` (create, update draft, delete, duplicate, close, archive), definition validator, differ, publisher, restore.
5. Admin pages: Surveys list, create with templates, designer (outline commands, question editor drivers, settings editors), versions, preview (as a test response, needs phase 3 runtime: implement preview at the end of phase 3 if simpler), Access page.
6. `building-blocks.md` Surveys row; module README.

Acceptance: a survey with three pages and the phase-2 controls is designed, validated and published; versions are immutable; architecture tests green.

### Phase 3: runtime

1. Logic engine (9.1–9.3), formulas (9.4), plan builder and PRNG (9.5).
2. Default collector and web-link collectors with QR (13.1).
3. Access rules (10.4) and participation records; anonymity (7.9).
4. `ISurveyResponseService` (10.1–10.3), runner controller and views, `survey-runner.js` (8.5), endings, resume tokens, "My responses".
5. Maintenance task (abandon, retention) and deadline work (10.7).
6. Phase-3 controls (Ranking, FileUpload with Attachments, Hidden, Calculated).
7. Responses admin list and response page; preview.

Acceptance: anonymous public survey with conditional logic, pool and limits works end to end without JavaScript and with it; resume, back, autosave, endings, deadline.

### Phase 4: scoring, quizzes and banks

1. Scoring engine, scorers (ported), score categories and bands, review policy and immediate feedback, quiz templates.
2. Grading service and queue page, regrade, score history, overrides (10.6).
3. Question banks (records, pages, editor reuse), pools with bank draws and publish snapshots (11.3), CSV/XLSX import (`ISurveyImportFormat`).
4. Phase-4 controls (Matching, FillInBlanks, ChoiceBlanks).
5. QTI import moved from `Learning.Assessments.Core/Qti` into feature Surveys.Qti (leave a forwarding registration in Learning until phase 7).

Acceptance: a timed quiz with a bank pool, an essay graded by hand, regrade after a key fix, review policy respected, no key in any respondent payload.

### Phase 5: results and integrations

1. Answer index writes and aggregates; reports (14) with filter driver; dashboard widgets.
2. Data source and data sets (18).
3. Recipe and deployment steps and schemas (16), sample recipe.
4. Workflow events and tasks (17).
5. Notifications, audit trail, webhooks, personal data, rate limits, telemetry (19).

Acceptance: architecture coverage tests green; recipe round trip; pipeline exports a survey's wide data set to CSV; a workflow sends an e-mail when a response matches a condition.

### Phase 6: collecting extras

Invitations and reminders (13.3), embedding (13.2), kiosk mode, quotas and the quota event, evenly present pools, page time limits, poll results for respondents, ContactInfo control, Surveys API (19.10).

### Phase 7: Learning on Surveys

Section 20 in this order: Learning access rule, access provider, grading scope and observers → Learning Surveys adapter → Assessments adapter (quiz course item on Surveys) → consumers (Certifications, Credentials, Coursework, AI, Moodle) → delete replaced Learning code and reset its migrations (20.4: question types, banks, attempts, stores, indexes, deadline task, events, webhooks, data source, reports, QTI copy; no data migration) → update tests and docs.

Acceptance: all Learning integration tests pass on Surveys (rewritten where routes and records changed); a fresh site set up with the Learning recipes works end to end; no Learning project references the deleted types; `building-blocks.md` and the audit updated.

### Phase 8: localization, AI and advanced controls

Translations (definition `Translations`, culture selection, export/import of a translation file as JSON), Surveys.AI module (19.9), advanced controls (Slider, ConstantSum, Signature, MultipleText, MatrixDropdown).

### Phase 9: docs (written with each phase, finished here)

Section 23.1.

### 23.1 Docs site

Under `src/CrestApps.Docs/` (read its `README.md`; CI enforces its rules; no tests or internal plans in the docs site):

- Technical manual `docs/modules/surveys/`: `index.md` (overview, features table with ids and dependencies), `designing.md`, `controls.md` (built-in controls and **Building a custom control**, the 8.7 walkthrough with code), `logic.md` (visibility, branching, piping, formulas, variables), `scoring-and-grading.md`, `versions.md`, `collecting.md` (links, invitations, embedding, limits, anonymity), `results-and-reports.md`, `data-pipelines.md` (data sets and fields), `recipes-and-deployment.md` (step JSON samples and schema), `workflows.md` (events, tasks, inputs), `api.md`, `extending.md` (access rules, observers, context kinds, grading scopes, import formats, condition sources), `learning.md` (how LMS uses Surveys).
- User manual `docs/user-manual/surveys/`: create a survey, add questions, logic, quizzes, question banks, publish and versions, share and invite, view results, grade responses, export. Front matter links each to its technical page (`user_manual` / `technical_manual`), Menu / Permission / Feature tables and `<AskYourAdmin />`.
- Update `modules/learning-surveys.md`, `modules/learning-assessments.md`, `user-manual/learning/surveys.md`, `quizzes.md`, `grading.md`, `learning-ai.md` to point to Surveys.
- `sidebars.js`: a **Surveys** category in `technicalSidebar` and `userManualSidebar` with an icon (`sidebar-icon--clipboard-list`: copy `clipboard-list.svg` from lucide-static into `static/img/icons/` and add the `.sidebar-icon--clipboard-list` rule in `src/css/custom.css`).
- Home page `src/pages/index.js`: add to `AREAS` (near Learning, around line 150):

  ```js
  {
    title: 'Surveys',
    inHero: true,
    icon: 'clipboard-list',
    text: 'Surveys, polls, forms and quizzes with logic, versions, scoring, reports and workflows.',
    user: '/docs/user-manual/surveys',
    technical: '/docs/modules/surveys',
  },
  ```

  and add Surveys to the hero paragraph (around line 175) and the `<Layout description>` (around line 265).
- `docs/feature-reference.md` (every feature), `docs/changelog/3.0.0.md` (new module, Conditions changes, and Learning described in its final shape on Surveys; no breaking-change notes because Learning is unreleased), `.agents/skills/crestapps-survey-controls/SKILL.md`.
- Run `npm run check:manuals` and `npm run build`.

## 24. Decisions (answered 2026-11-20)

Mike: "Go with your recommendations", no LMS migration, Surveys replaces the Learning workflow events, webhooks and data source, and the rest follows industry standard and state-of-the-art products. These are final; do not reopen them during the build.

| # | Question | Decision |
| --- | --- | --- |
| D1 | Store surveys, versions and bank questions as catalog records, or as content items? | **Catalog records** (Mike is fine either way). Stable ids, immutable versions that Orchard's version pruning cannot delete, no key leaks through content API/GraphQL/search, compact responses. Content items reach surveys through `SurveyPart` (Survey Embedding), which references a survey |
| D2 | Bank questions in a version: snapshot at publish, or reference bank revisions? | **Snapshot at publish** (as Qualtrics publishes a self-contained version): simple runtime, cacheable, with a "bank changed, republish" indicator and a candidate cap |
| D3 | Live client-side visibility: JS evaluator, server round trip, or both? | **Both**: shared Conditions JS for plain rules (instant, as SurveyJS and Google Forms behave); the evaluate endpoint for formulas, piping and calculated variables; the server decides on every post |
| D4 | Migrate existing LMS data? | **No.** The LMS was never in production; change it in place with no migrations (20.4) |
| D5 | Replace Learning attempt workflow events, webhooks and the `LearningAssessments` data source with Surveys ones? | **Yes**, everything about responses, answers and scores belongs to Surveys (20.3). No changelog breaking notes (Learning is unreleased) |
| D6 | File upload control: hard dependency on Attachments, or separate? | **Built into Surveys without a hard dependency**: the FileUpload control and its access provider are registered in a startup gated by `[RequireFeatures(AttachmentsConstants.Features.Attachments)]`, so the question type appears as soon as Attachments is on (file upload is a standard type on every platform). Attachments is already in the Targets |
| D7 | Respondent route prefix `/s/...` or `/surveys/...`? | **`/s/{token}`** short shareable links (SurveyMonkey `/r/`, Google `forms.gle`, Microsoft `forms.office.com/r/` all use short random tokens, never guessable names); `/surveys/my` for the respondent's own list |
| D8 | Quotas and invitations: when? | **Phase 6** (after the core runtime, scoring and integrations), as planned |
| D9 | Export `AnonymitySecret` and collector tokens in recipes? | **No** by default; tokens and password hashes only with the explicit "Include secrets" option; anonymity secrets always regenerated on import |
| D10 | Minimum group size for anonymous reporting? | **5** by default (the common anonymity threshold in employee-survey products), configurable per survey; 0 disables |
| D11 | Admin menu: top-level **Surveys**, or under Content? | **Top-level** (a product area like Learning); "shared records under Content" covers shared records such as Teams, not product areas |

## 25. Checks to make in phase 0

| # | Check | Why |
| --- | --- | --- |
| C1 | `ExpressionCompiler.Compile` accepts a custom `IExpressionFunctionCatalog` composing built-ins with survey functions, without registering them globally; `IExpressionScope.TryResolve` supports names like `score:category` or a mapping | Formulas (9.4) |
| C2 | `RecordDataSource` supports data sets created at run time (one per survey) and lets another module contribute fields | Data sets (18), Learning fields (20.3) |
| C3 | Whether CrestApps.Core `CatalogItem` has an extensible `Properties` bag with `As<T>()`; otherwise add `JsonObject Properties` and extension methods in Surveys.Abstractions | Extensibility (7) |
| C4 | `NamedCatalogRecipeStep<T>` can be extended to import child arrays (versions, collectors) in the same step, or whether a custom `CatalogRecipeStep<Survey>` override is needed | Recipes (16) |
| C5 | Attachments: an upload during an anonymous response (no user) is supported by `IAttachmentService` with an owner kind and access provider | FileUpload control |
| C6 | Outreach can send to an explicit recipient list with per-recipient personal links and schedule reminders | Invitations (13.3) |
| C7 | Report sections support bar charts and NPS-style tables from code (`ReportDocument`); Report Builder reads dynamic data sets | Results (14) |
| C8 | YesSql `Dictionary<string, SurveyAnswer>` and `JsonObject Settings` round trip without type-rewrite issues (memory: DictionaryDocument type-rewrite trap; YesSql ExpandoObject metadata) and DateTime kinds (memory: property bag DateTime format) | Storage (7) |

## 26. Progress

The implementer records, per slice: what was built, the commit, deviations from this plan with the reason, and open issues.

### Phase 0: checks (2026-11-20)

All eight checks pass; three need a small change of approach, recorded here.

| # | Result | What we do |
| --- | --- | --- |
| C1 | Pass. `ExpressionCompiler.Compile(string, IExpressionScope, IExpressionFunctionCatalog, IStringLocalizer)` takes any catalog; `IExpressionFunctionCatalog` has one method, `Find(name)`, so a composing catalog is a few lines. `IExpressionScope.TryResolve(key, out ExpressionFieldBinding)` receives the key as written between brackets, so `[score:category]` and `[var:name]` resolve to row slots of our own layout | `SurveyExpressionFunctionCatalog` tries the survey functions first, then the DI catalog (or `ExpressionFunctionCatalog.BuiltIn`). Survey functions are never registered with `AddExpressionFunctionProvider`, so pipeline editors do not list them |
| C2 | Pass, with a different base for the per-survey sets. `RecordDataSource.DataSets` is a synchronous property and cannot list surveys. DataConnectors' `ConnectedDataSource` already implements `IDataSource` directly and lists data sets read from a store at run time | `SurveysDataSource` implements `IDataSource` directly: the static data sets are `IRecordDataSet`s it delegates to, and `SurveyResponses_{name}` sets are built per published survey in `GetDataSetsAsync`. Field contributions (Learning) come through `ISurveyResponseFieldProvider`, which our own `SurveyResponses` data set asks, so no Reports change is needed |
| C3 | Pass. `CatalogItem` derives from CrestApps.Core's `ExtensibleEntity`, which has `Properties` and `As<T>()` / `TryGet<T>()` / `Put<T>()` (used by Calendars) | Surveys records use the inherited bag; no extra `Properties` member is declared on catalog records. `SurveyDefinition`, `SurveyQuestion` and `SurveyAnswer`, which are not catalog items, keep their own `JsonObject` members |
| C4 | Pass. `NamedCatalogRecipeStep<T>` exposes `Preparing(entry, token)`, `CreateAsync`, `CommitUpdateAsync` and `FindExistingAsync` overrides | `SurveysRecipeStep` captures the token's `Versions` and `Collectors` arrays in `Preparing` and writes them after the survey in `CreateAsync` / `CommitUpdateAsync` |
| C5 | Pass. `AttachmentUpload` takes an `Owner` (`RecordRef`) and an optional `UploadedBy` (`PartyRef`); access is decided per owner kind by `IAttachmentAccessProvider` | Owner kind `SurveyResponse`; `UploadedBy` stays empty for anonymous respondents; the access provider allows staff with response permissions and the respondent who holds the response |
| C6 | Pass, partly. Outreach runs page an `IOutreachAudienceSource`, render per recipient through `IOutreachTemplateRenderer`, and apply `IOutreachRecipientPolicy`; it has no reminder schedule of its own | Invitations add an audience source over pending invitations and a renderer that writes each recipient's personal link. Reminders are new runs started by `SurveyInvitationReminderTask` over invitations that are due |
| C7 | Pass. `ReportDocument` sections hold metrics, tables, bars and charts (`ReportChart`, `ReportBar`, `ReportMetric`); the Report Builder reads any registered `IDataSource`, dynamic sets included (as Connected data does) | Analyzers return `ReportSection`s directly |
| C8 | Pass. YesSql serializes documents with System.Text.Json: typed `Dictionary<string, SurveyAnswer>` and `JsonObject` round-trip without type names (the DictionaryDocument trap only affects whole-set catalogs, and we use one document per record). `DateTime` values come back with an unspecified or UTC kind | No `object`-typed members anywhere (ExpandoObject trap). Every displayed time goes through `ILocalClock` after `DateTime.SpecifyKind(value, DateTimeKind.Utc)`. A storage round-trip test covers a response with every answer shape |

### Phase 1: building-block changes (done)

- `9bc7f6f48` Conditions: eight operators (`NotContains`, `StartsWith`, `EndsWith`, `GreaterThanOrEqual`, `LessThanOrEqual`, `Between`, `CountAtLeast`, `CountAtMost`), nested `Groups`, ISO date comparison for the ordering operators, `Sources` and cycle detection through groups, schema with groups four levels deep (deeper levels are plain objects instead of a recursive `$ref`, so the existing schema tests keep validating). The JS evaluator moved to `Resources/Assets/js/conditions.js` (resource `crestapps-conditions`, global `window.CrestAppsConditions`, ES module named exports); ContentFields imports it. Parity vectors in `tests/fixtures/conditions/condition-vectors.json` run in xUnit and Vitest. Deviation: the C# tests stay next to the existing ones in `tests/CrestApps.OrchardCore.Tests/Core/Conditions/`.

### Phase 2: foundation and designer (done)

- `639cd6e0a` Abstractions (models, contracts, constants, permissions, registration helpers), Core storage (stores, indexes, schema, version cache, control registry) and the module skeleton. Deviation: `SurveysPermissions` lives in Surveys.Abstractions (not Core) so consumers such as Learning check permissions without the core library; `ISurveyExpressionFunctionProvider` lives in Core because `ExpressionFunction` is in `OrchardCore.DataSources.Core`.
- `c816230f2`, `8212fdf8c`, `e58476b54`, `2ba48e722`, `2f3607f14` Controls: base handlers, settings driver base, respondent projector, `SurveyControlViewModel`, every control of section 8.6 (all phases) with scorers ported from Learning and analyzers, settings editors, Respond/Review views, `survey-controls.js`, key-leak and architecture tests.
- `9bd18083a`, `d5f46a1c7`, `80847b650`, `405ba288f` Authoring: manager, validator, differ, publisher with bank snapshots, eight templates, authorization service, admin pages (list, create, designer, publish with diff, versions, compare, restore, collectors with QR, access, logic overview, site settings). Deviations: ordering uses native drag and drop with up/down buttons (no SortableJS resource exists in the repository); survey settings are stacked cards instead of tabs; the page editor edits one bank draw per pool; audiences are entered by id.
- Integration fixes: `718358888`, `0f5ecd469`, `e9ce33321`, `a23773300`, `c03dd19a5`.

### Phase 3: runtime (done)

- `f0fbd5692`, `179c1b1b2`, `3f535a14b` Logic engine, piping, formulas (C1 verified by a test), plan builder with `SurveyRandom`, access rules, participation with the HMAC key for anonymous surveys, `SurveyResponseService`, deadline work on the leased queue, maintenance task, runner routes and views, `survey-runner.js`, responses list and response page, preview.

### Phase 4: scoring, quizzes and banks (done)

- `943742165`, `ddedb236a` Scoring engine, review policy, grading service, grading queue, accommodations and their pages.
- `aa322bf9f`, `4c91428b8`, `0477f1013`, `081e6c020` Question banks, spreadsheet import, QTI import (feature Surveys.Qti) and bank pages. Deviation: the package archive helpers moved from Learning.Core to `CrestApps.OrchardCore.Abstractions/Archives` so Surveys can read QTI packages without depending on Learning. Feedback-only QTI images go to the media library.

### Phase 5: results and integrations (done)

- `fceb09021`, `e46fb8de5`, `aed46b64a`, `ffff61f3c` Answer index, SQL aggregates, eight reports (adds `survey-activity` for the dashboard widget), the `Surveys` data source (C2 approach).
- `4f952b6a0`, `0d01a5ffa`, `43cd9dd16` Recipe and deployment steps with schemas and a sample recipe, workflow events and tasks, notifications, audit trail, webhooks, personal data, telemetry. Deviations: `SurveySettings` travels in Orchard's Settings step (with a site settings schema) rather than a step of its own; the recipe steps write the stores directly, so catalog handlers do not run on import.

### Phase 6: collecting extras (done)

- `349af2dd8`, `bf033abea`, `a683a2eca`, `fa6d86d9f`, `f31ba3055`, `43512a23f` Quotas (counter document under the completion lock, screen out or close, quota-reached workflow event), evenly presented pools, page time limits, invitations (personal links `/s/i/{token}` whose token is an HMAC of the invitation id keyed by the survey secret, notifications to users, Liquid e-mails to others, hourly reminders; answered anonymous invitations are found through the participation count of their pseudonymous key), the Survey part, widget and `[survey]` shortcode, the Surveys API, and HTTP integration tests (public link, invitation link, API). Kiosk mode and poll results came with the runtime. Deviations: invitations are sent by the service and a background task rather than through Outreach runs; the invite page takes users, audiences, cohorts and e-mails (parties through the service and the API only); the Survey part picks from a list of open surveys.

### Phase 7: Learning on Surveys (done)

- `e4b307126`, `700051112`, `0bed82c3f`, `a4439f2b6`, `f7e1f709b`, `93a08c5c5`, `d62655554`, `c1632e285` Course surveys and choices (`CourseSurveyPart`), quizzes (`QuizPart { SurveyId, GradingMethod }`), the Learning access rule, access provider, grading scope, result providers, response field provider, owner back-link provider and the `LearningEnrollment` context kind; Certifications, Credentials, Coursework and Learning.AI read survey responses; the Moodle importer creates surveys and banks. The old question content types, banks, attempts, overrides, deadline task, attempt events, webhooks, data source, item analysis report, notification, audit events and the Learning QTI import are deleted, and the Learning.Assessments and Learning.Surveys migrations build only the new shape (version 1). A service cycle between Learning and the response service (found by the integration run) is broken by resolving the response service when a response starts. Removed without replacement: quiz access codes and per-quiz overrides (Surveys accommodations cover overrides).

### Phase 8: localization, AI and advanced controls (done)

- `36ee46468`, `19588e1e2`, `1dff32ba3` Translations (`SurveyTextKeys`, culture from the link, the collector or the request with parent-culture fallback, Translations page with JSON export and import, publish warnings for orphan keys), the Surveys.AI module (tools `list_surveys`, `get_survey_results`, `create_survey_draft`; answer summaries; AI translation; scoring help on the grading page through the `SurveyGradingQuestionTools` shape). The advanced controls came with the controls track. Deviations: text keys are `title:{q}`, `choice:{q}:{c}` and similar (the shapes the projector already read); the answer summary is a page of its own rather than a button in the report.

### Phase 9: docs (done)

- `b30e6a044`, `08f2a9542`, `a628f8822`, `4eddbbe8a`, `d45e45bc3` Technical manual (`docs/modules/surveys/`, 16 pages), user manual (`docs/user-manual/surveys/`, 13 pages), both sidebars with the clipboard-list icon, the Surveys card on the docs home page, the feature reference, the 3.0.0 changelog and the `crestapps-survey-controls` skill. The Learning pages describe Learning on Surveys.

### Fixes found by the browser pass

- `8c7fd4bdb` the question type settings shape was named so no view matched, which broke the question editor; `69ad82525` respondent display names and the report filter key `ReportFilter.SurveyId`; `907000245` ordered and localized picker categories.

### Open issues

- The "matches condition" workflow event takes its rule as JSON; its optional formula is not evaluated yet.
- The override grant form takes a user name rather than the user picker (the picker lives in the CrestApps Users module, which Surveys does not depend on).
- Invitations do not go through Outreach yet, so contact opt-outs are not checked before an e-mail invitation is sent.
- QTI images used only in feedback go to the public media library (Learning kept them in a protected store).
- The Excel reader of bank imports repeats a private reader of ContentTransfer.OpenXml; extracting a shared reader is a follow-up.

## 27. Sources

- Qualtrics: [publishing and versions](https://www.qualtrics.com/support/survey-platform/survey-module/survey-publishing-versions/), [randomizer](https://www.qualtrics.com/support/survey-platform/survey-module/survey-flow/standard-elements/randomizer/), [question randomization](https://www.qualtrics.com/support/survey-platform/survey-module/block-options/question-randomization/), [quotas](https://www.qualtrics.com/support/survey-platform/survey-module/survey-tools/quotas/), [scoring](https://www.qualtrics.com/support/survey-platform/survey-module/survey-tools/scoring/), [survey options](https://www.qualtrics.com/support/survey-platform/survey-module/survey-options/survey-options-overview/), [question types](https://www.qualtrics.com/support/survey-platform/survey-module/editing-questions/question-types-guide/question-types-overview/), [data protection](https://www.qualtrics.com/support/survey-platform/getting-started/data-protection-privacy/).
- SurveyMonkey: [question types](https://help.surveymonkey.com/en/surveymonkey/create/question-types/), [collector options](https://help.surveymonkey.com/en/surveymonkey/send/collector-options/), [editing live surveys](https://help.surveymonkey.com/en/surveymonkey/manage/editing-live-surveys/), [quizzes](https://help.surveymonkey.com/en/create/quizzes/), [accessibility](https://help.surveymonkey.com/en/create/accessibility/).
- Google Forms: [quizzes](https://support.google.com/docs/answer/7032287?hl=en), [publishing](https://support.google.com/docs/answer/2839588?hl=en), [granular responder controls (2025)](https://workspaceupdates.googleblog.com/2025/06/add-granular-control-to-existing-google-forms.html).
- Microsoft Forms: [settings](https://support.microsoft.com/forms/adjust-your-form-or-quiz-settings-in-microsoft-forms), [branching](https://support.microsoft.com/en-us/education/forms/branch-your-form-or-quiz-using-microsoft-forms), [quizzes](https://support.microsoft.com/office/create-a-quiz-with-microsoft-forms-a082a018-24a1-48c1-b176-4b3616cdc83d).
- SurveyJS: [conditional logic](https://surveyjs.io/form-library/documentation/design-survey/conditional-logic), [specialized types](https://surveyjs.io/form-library/documentation/customize-question-types/create-specialized-question-types), [composite types](https://surveyjs.io/form-library/documentation/customize-question-types/create-composite-question-types), [custom properties](https://surveyjs.io/form-library/documentation/customize-question-types/add-custom-properties-to-a-form), [quizzes](https://surveyjs.io/form-library/documentation/design-survey/create-a-quiz).
- LimeSurvey: [question types](https://www.limesurvey.org/manual/Question_types), [quotas](https://www.limesurvey.org/manual/Survey_quotas). Jotform: [unique field](https://www.jotform.com/help/223-how-to-set-form-limits-based-on-a-unique-field), [kiosk mode](https://www.jotform.com/enterprise/features/forms/kiosk-mode/). Moodle: [quiz settings](https://docs.moodle.org/en/Quiz_settings).
- Internal: an earlier private survey module (reference only), `Learning.Surveys` and `Learning.Assessments` in this repository, `docs/engineering/learning/lms-plan.md`.
