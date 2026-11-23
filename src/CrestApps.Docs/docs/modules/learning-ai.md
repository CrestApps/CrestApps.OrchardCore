---
sidebar_label: Learning AI
title: Learning AI
description: "AI for Learning: a tutor that answers learners only from the lessons of their own courses and never from exam content, Learning AI tools, author prompts that draft for review, and scoring help for graders."
user_manual:
  - user-manual/learning/learning-assistant
---

**Learning AI** (`CrestApps.OrchardCore.Learning.AI`, with `Learning.AI.Abstractions`) adds AI to [Learning](./learning) on top of the AI building blocks, and reuses them as they are: AI profiles and profile templates, prompt templates rendered with `ITemplateService`, the front-end chat widget and chat sessions of AI Chat, per-user memory, profile documents, AI data sources for retrieval, usage metering (every model call of the module is labelled with the usage purpose `Learning`), chat analytics, the AI workflow activities and the MCP server. It adds no chat interface, memory or metering of its own.

| Feature | Id | Depends on |
| --- | --- | --- |
| Learning Assistant | `CrestApps.OrchardCore.Learning.AI` | Learning, AI |
| Learning Authoring Assistant | `CrestApps.OrchardCore.Learning.AI.Authoring` | Learning Assistant |

Feature ids, tool names and template ids are constants in `LearningAIConstants`; the permissions are in `LearningAIPermissions`.

| Permission | Allows |
| --- | --- |
| `UseLearningAuthoringAssistant` | The author prompts. Each prompt also needs the content permissions of what it drafts (see below). |
| `UseLearningScoringHelp` | Scoring help on the grading pages, for work the person may grade. |

## The tutor

### The tutor profile template

The **Learning tutor** profile template (`Templates/Profiles/learning-tutor.md`, id `learning-tutor`, listed when Learning Assistant is on) is a chat profile with the tools `getMyLearningProgress`, `getMyNextLesson` and `explainMyQuizResult`, and a system message that tells the model to answer only from the course content it is given, never to give or hint at the answers to quiz or exam questions, and to explain a quiz result only with what the result tool returns.

A profile created from the template in the **New AI Profile** flow is marked as a tutor (`LearningTutorTemplateApplicationHandler` stores `LearningTutorProfileMetadata { IsTutor = true }` in the profile's properties). The **Knowledge** tab of every AI profile has a **Learning tutor** checkbox that sets the same mark, so a profile made another way can be marked too. The `CreateAIProfileFromTemplate` recipe step runs the template application handlers, as the New AI Profile flow does, so a tutor it makes from the template is marked; in another recipe step, set `Properties.LearningTutorProfileMetadata.IsTutor` on the profile.

To answer from lessons, give the tutor profile an AI data source over a content index that holds the lesson content types (**Page lesson**, **Video lesson**, **File lesson**). Show it to learners with the AI Chat widget, for example in a layer on the learner pages.

### The course switch

The learning assistant switch (`LearningAssistantPart.Enabled`, on the **Course** type, added by the module's migration) is off on every course until an author turns it on in the course editor. While it is off, a tutor retrieves nothing from the course.

### Enrollment-scoped retrieval

Learning AI registers `LearningRetrievalFilter` through the AI Data Sources result filter extension point (see [the extension point](#the-ai-data-sources-result-filter)), so it trims every data source search, whatever profile or tool runs it:

- **Exam content never reaches a model.** A chunk whose content item is a quiz (`QuizLesson`, `QuizPart`, or the `Quiz` course item kind) is dropped for every profile and every person, including people who may read answer keys. Questions and question banks are Surveys records, not content items, so they are never in a content index. The item is classified from its latest version as well as its published one, so a draft held by an index set to the latest version is classified too, and a course item or exam item with no published version is dropped.
- **A tutor** keeps only chunks of course items whose kind is in `LearningAIOptions.TutorLessonKinds` (page, video and file lessons by default), whose course has the learning assistant on, and that the Learning access decision opens to the person because of an **active enrollment** within its access dates and release rules. Course staff, authors and free previews do not count, and chunks that are not course items (other content, documents) are dropped.
- **Any other profile** keeps course items only when the person may view them (Orchard Core's `ViewContent`, which the Learning access handler decides) and the course's learning assistant is on: the course switch holds for every profile, not only tutors. Everything else stays as before.

Keep quizzes out of AI knowledge base indexes all the same: the filter drops their chunks after the search, so they only take up search results.

## Learning AI tools

The tools are registered with `AddCoreAITool` and can be picked on any AI profile (category **Learning**). Each acts as the signed-in person and refuses anyone who is not signed in. Times are written in the person's time zone through `ILocalClock`.

| Tool | Permission and scope | Returns |
| --- | --- | --- |
| `getMyLearningProgress` | Access learning; the person's own enrollments only | Each course with its status, progress, required items completed, due date, access end, completion and course grade. Argument `courseId` (optional). |
| `getMyNextLesson` | Access learning; own enrollments | The first item, in outline order, of an active course that the learner has not completed and may open now, with its player link. Argument `courseId` (optional). |
| `explainMyQuizResult` | Access learning; own attempts, read by the person's user id | The attempt's score when the quiz's review policy shows it, and the questions, answers, right answers and feedback only when the policy lets the learner review it (`AttemptReviewPolicy`). An attempt waiting for a grader or cancelled has no result. Registered when Learning Assessments is on. Arguments `quiz` (id or part of the title) and `attempt` (optional). |
| `listOverdueLearners` | Scope below | Active enrollments past their due date, soonest due first: learner, course, enrollment number, due date and progress. Never answers or grades. Argument `take` (1 to 50). |
| `listLearnersExpiringSoon` | Scope below | Active enrollments whose access ends within `days` (1 to 90, default 14), soonest first. Argument `take`. |

The scope of the two staff tools is everyone for a person who holds **View learner records** on its own; otherwise the learners of the courses the person is on the staff of, with **Manage the courses you teach** (the rule of the Learning record authorization handler); and, when Staff is on, the people the person manages (`IStaffScopeService`: their reports and the members of teams they manage). A person with none of these is refused. When the scope is a set of people, the tools read at most 1,000 matching enrollments and say when there may be more.

## Author prompts

**Learning > Authoring assistant** (feature Learning Authoring Assistant, permission `UseLearningAuthoringAssistant`) has four author prompts. Each renders a prompt template, calls the site's utility deployment (or the default one), reads the JSON answer and saves **drafts**: nothing is published, and an author reviews and publishes them as usual. The lesson lists offer only lessons the author may edit (`EditContent`).

| Prompt | Template | Needs | Saves |
| --- | --- | --- | --- |
| Draft a course outline | `learning-draft-outline` | Edit content of a new course | A draft course with its outline sections and a draft page lesson per outline item. |
| Draft questions from a lesson | `learning-draft-questions` | Manage question banks (Surveys) and View content of the lesson | Single choice, multiple choice, true or false, short text and essay questions in the chosen Surveys question bank, with their right answers and feedback (the essay's as grader guidance), saved **retired** so no pool draws them until an author reviews them and turns retired off. |
| Draft a rubric | `learning-draft-rubric` | Learning Coursework; Edit content of a new rubric | A draft rubric with criteria and levels. |
| Translate a lesson | `learning-translate` | Edit content of the lesson | A draft copy of the lesson (`IContentManager.CloneAsync`) with the translated title and body; the body passes the HTML sanitizer. The copy is not added to the outline. |

The answer is read strictly (`AuthoringDraftParser`): anything that is not usable JSON or is outside the limits (12 sections of 10 lessons, 20 questions of 8 options, 10 criteria of 6 levels) is left out, a single choice question needs exactly one right option, a short text question an accepted answer, a rubric criterion two levels. The model's text is HTML-encoded into prompts, lesson bodies and options, never taken as markup. When no AI deployment is set up, the page says so, its buttons are disabled, and a request saves nothing. The prompt templates can be overridden like any other prompt template.

## Scoring help

With Learning Authoring Assistant on, the grading page of a coursework submission shows a **Suggest a score** panel in the grade card, to people with `UseLearningScoringHelp` who may grade the work (`GradingScope`) while the grade can be changed. The panel posts to the admin route `/Admin/learning/ai/scoring-help/submissions/{id}` (`LearningAIScoringHelpSubmission`) of `ScoringHelpController`, which renders the `learning-score-essay` template with the task, the coursework's rubric and points, and the learner's text (not the learner's name), and return a suggestion as JSON: points within the work's range, or one level of each rubric criterion that belongs to it, and feedback within the form's length (`EssayScoringParser`). The page fills its own fields with it; it saves nothing. Quiz essay answers are graded on the Surveys grading page, which has its own scoring help.

The grader confirms or changes the suggestion and saves as usual. A grade saved after using the panel records it on the submission (`Submission.AIAssisted`, kept on the earlier grade in `SubmissionGrade.AIAssisted` when the work is graded again), shown as **AI-assisted** on the grading pages. Quiz essays follow the rules of the Surveys grading page ([Surveys AI](/docs/modules/surveys/ai)). For coursework the mark comes from the server: when the panel returns a suggestion, the submission records which grader got it (`Submission.ScoringHelpGivenTo`, `ISubmissionService.RecordScoringHelpAsync`), the next grade that grader saves is AI-assisted, a grade saved again with the same score keeps the mark, and `GradeSubmissionRequest.AIAssisted` is not read. AI never saves a grade, never finalizes an attempt and never issues a certificate or credential: those follow the grade the grader saved, as before. Work with no text (only files) gets no suggestion.

### Adding help to the grading pages

The grading pages render each piece of work through `IDisplayManager<GradingAssistContext>` (in `Learning.Abstractions`; display type `Detail`), whose shape renders nothing unless a module adds a display driver. The context gives the kind (`Submission`, the only kind: quiz answers are graded on the Surveys grading page, whose scoring help is the Surveys AI button), the record id and question index, the most points, whether the grade can be changed, and the names of the form fields for the points, the feedback, the rubric choices and the field that records the help was used. Scoring help is one such driver; another module can add its own.

## The AI Data Sources result filter

`CrestApps.OrchardCore.AI.Core.DataSources` holds the per-request result filter extension point of AI Data Sources, which any module can use:

- `IAIDataSourceResultFilter.FilterAsync(AIDataSourceResultFilterContext, IReadOnlyList<DataSourceSearchResult>)` keeps the chunks a request may receive. The context gives the person (`User`), the completion's resource (an AI profile or a chat interaction), the chat session's profile id and the data source.
- Register a filter with `services.AddAIDataSourceResultFilter<TFilter>()`.
- AI Data Sources wraps every keyed `IDataSourceContentManager` (the search of each provider) in `FilteringDataSourceContentManager` in a startup that runs after the providers' (`ResultFiltersStartup`, order 10,000), so the search before the turn, the search tool, the search tool instances and any other caller of a provider's search pass through the filters. A module that registers its own provider's content manager later calls `services.DecorateDataSourceContentManagers()`, which leaves already wrapped ones alone. CrestApps.Core searches the phrases of a question at once on the same content manager; the index searches stay parallel, but the wrapper runs the filters one at a time, because a filter may read the request's database session, which takes one command at a time (SQL Server without MARS and PostgreSQL refuse a second one).
- With filters registered, the search asks the index for more chunks than the caller wants (`AIDataSourceResultFilterOptions.OverfetchFactor`, 4, at most `MaxCandidates`, 200) and returns the caller's number of what the filters keep. With none, it is a pass-through.

The static filter of a data source or profile still narrows the search inside the index; a result filter decides afterwards, per request, with what the index does not know.
