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

`CourseSurveyPart` holds one property, `SurveyId`. `CourseSurveyPartHandler` creates the survey when the item is first saved, with the owner `ContentItem:{item id}`. The item's editor shows the survey's status, its question count and published version, and links to **Edit questions** (the Surveys designer), **Access and settings** and **Totals** (the Surveys results report for the survey). The designer of an owned survey links back to its course item (`LearningSurveyOwnerLinkProvider`). Learners can answer once the survey is published.

What the earlier `SurveyPart` held is now a setting of the survey:

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
- `LearningSurveyResultObserver` records the item as **Submitted** through `ILearnerProgressService` once a response is completed (`CourseSurveyResultProvider`).
- Removing an item removes its survey: deleted when it has no responses, closed and archived when it has.

## Totals

The totals are the Surveys reports filtered to the survey: **Survey Results** for counts, averages and written answers (with the minimum group size of anonymous surveys), **Survey Responses** for each response. They open to people who may view the survey's responses; course staff reach them through `LearningSurveyAccessProvider`. Learners see the totals on the runner when the survey's results visibility allows it.

## Data and personal data

Responses, their recipe and deployment steps, data sets, workflow events and personal data are those of Surveys; `LearningSurveyResponseFieldProvider` adds `CourseId`, `CourseTitle` and `EnrollmentId` to the `SurveyResponses` data set.
