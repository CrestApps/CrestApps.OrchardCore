---
sidebar_label: Learning Surveys
title: Learning Surveys
description: "The feedback survey and choice course items of Learning, on the Surveys module: each item owns a survey, learners answer it through their enrollment, and the item completes when they answer."
user_manual:
  - user-manual/learning/surveys
---

**Learning Surveys** (`CrestApps.OrchardCore.Learning.Surveys`, with `Learning.Surveys.Abstractions` and `Learning.Surveys.Core`) adds two course items to [Learning](./learning): the **Feedback survey** and the **Choice**. Their questions, answers, anonymity, close date and totals belong to the [Surveys](/docs/modules/surveys) module: each item owns a survey.

| Feature | Id | Depends on |
| --- | --- | --- |
| Learning Surveys | `CrestApps.OrchardCore.Learning.Surveys` | Learning, Surveys |

## The survey items

Both types are defined in code with `TitlePart`, `HtmlBodyPart` (the introduction), `CourseSurveyPart` and `CourseItemPart`. Their course item kind is `Survey`; the kind supports the completion modes **Submitted** (the default: done when the learner answers), **Viewed** and **Marked done**, and produces no grade.

| Type | Content type | `CourseSurveyPartSettings.SingleQuestion` | Survey template |
| --- | --- | --- | --- |
| Feedback survey | `FeedbackSurvey` | `false` | **Course feedback** (kind `Survey`) |
| Choice | `Choice` | `true` | **Poll** (kind `Poll`) |

The **Course feedback** template is a survey template record that this feature seeds from `Migrations/course-feedback-template.recipe.json` when it is first enabled (name `LearningSurveysConstants.CourseFeedbackTemplate`). Administrators change it under **Surveys** → **Templates**; if it is removed, new feedback survey items start blank.

`CourseSurveyPart` holds one property, `SurveyId`. `CourseSurveyPartHandler` creates the survey when the item is first saved, with the owner `Content:{item id}`, a signed-in audience and no public web link; a copied item gets a copy of the original's survey. The item's editor shows the survey's status, its question count and published version, and links to **Edit questions** (the Surveys designer), **Access and settings** and **Totals** (the Surveys results report for the survey). The designer of an owned survey links back to its course item (`LearningSurveyOwnerLinkProvider`). Learners can answer once the survey is published.

What the earlier `SurveyPart` held is now a setting of the survey. On a site from that release, the feature's migration removes `SurveyPart` (now the name of the Surveys embed part) from the two types, moves each item to `CourseSurveyPart`, builds its survey from the old questions (single and multiple choice, rating and text become Single choice, Multiple choice, Rating and Long text; `LegacyCourseSurveyUpgrade`), carries anonymity and the close date, publishes the survey when the item is published, and drops the old `LearningSurveyResponses_SurveyResponseIndex` table. Old responses are not carried over. An item still holding its old questions (an upgrade that could not save them) gives them to its survey on its next save, unless course staff have changed or published that survey since: the old questions never replace a newer draft (`LegacySurveyContent.IsNewerThanLegacy`), and the old part is dropped.

| Earlier | On the survey |
| --- | --- |
| Questions | The survey's pages and questions, with every Surveys question type, logic and validation. |
| Anonymous | **Access and settings** > anonymity. An anonymous survey keeps the course, not the enrollment, as the context of its responses. |
| Allow change answer | Navigation > **Respondents may change a finished response**. |
| Close date | **Access and settings** > closes. |
| Results visibility | Review > when respondents see the totals of all responses. |

## Answers

The survey panel of the course player (`CourseSurveyPartLearnerDisplayDriver`) shows whether the learner answered, why they cannot answer when that is the case, and a button that posts to `learning/surveys/{itemId}/start`. `ILearningSurveyService.StartAsync` (`Learning.Core`) starts or continues the learner's response with their enrollment (or, for an anonymous survey, the course) as its context and `CourseId`, `EnrollmentId` and `CourseItemId` in its properties; the learner answers on the Surveys runner (`/s/r/{response id}`), and the ending page links back to the course.

- `LearningEnrollmentAccessRule` lets a learner start only while they may open the item (enrolled, item released, access not ended).
- `LearningSurveyResultObserver` records the item as **Submitted** through `ILearnerProgressService` once a response is completed (`CourseSurveyResultProvider`). For an anonymous survey the result carries `CourseItemResult.AnonymousDayUtc`, the start of the day in the site's time zone: the item's completion (and a course completion it causes) is recorded at that time, and the learner's last access and last activity do not move, so the progress never gives the time that would link the learner to their anonymous response. A response submitted without the learner's request (at its deadline, or when the survey closes) names nobody, so the observer can't find the learner; the survey panel records it on the learner's next visit to the item (`AnonymousCourseSurveyCompletions`), by the day only: the survey's participation record, kept under the same pseudonymous key the survey's limits count, says the learner completed a response. Participation records are kept 45 days (`SurveyParticipationService.RetentionDays`) unless the survey has a total limit, so a learner who returns later than that completes the item by answering again or with **Mark done**.
- **Change your answers** shows when the version the learner answered allows changing a finished response, as reopening it does; never from the unpublished draft.
- Removing an item removes its survey: deleted when it has no responses, closed and archived when it has. It runs in a deferred task after the request that removed the item commits (`LearningOwnedSurveyContentHandler`), because deleting a survey purges its records a batch at a time, each committed in a scope of its own, which would wait on the request's own writes on SQLite; the task first checks that the item is still gone.

## Totals

The totals are the Surveys reports filtered to the survey: **Survey Results** for counts, averages and written answers (with the minimum group size of anonymous surveys), **Survey Responses** for each response. They open to people who may view the survey's responses; course staff reach them through `LearningSurveyAccessProvider`. Learners see the totals on the runner when the survey's results visibility allows it.

## Data and personal data

Responses, their recipe and deployment steps, data sets, workflow events and personal data are those of Surveys; `LearningSurveyResponseFieldProvider` adds `CourseId`, `CourseTitle` and `EnrollmentId` to the `SurveyResponses` data set.
