---
sidebar_label: Learning Surveys
title: Learning Surveys
description: "The feedback survey and choice course items of Learning: rating, choice and written questions, anonymous answers, a close date, totals for learners and staff, and the response records."
user_manual:
  - user-manual/learning/surveys
---

**Learning Surveys** (`CrestApps.OrchardCore.Learning.Surveys`, with `Learning.Surveys.Abstractions` and `Learning.Surveys.Core`) adds two course items to [Learning](./learning): the **Feedback survey** and the **Choice**. They replace Moodle's Feedback and Choice activities when a Moodle course is moved.

| Feature | Id | Depends on |
| --- | --- | --- |
| Learning Surveys | `CrestApps.OrchardCore.Learning.Surveys` | Learning |

## Why a course item and not Orchard Core Forms

Orchard Core Forms posts a form to a workflow and keeps nothing, so it has no answer per learner, no totals, no completion and no personal data handling. Assessments questions live in banks and are scored. A survey's questions belong to the survey, are never scored, may be anonymous, and are counted, so they are a small record type of their own: `SurveyResponse`, one per learner and survey. The survey reports to the learner's progress through `ILearnerProgressService`, like every other course item.

## The survey items

Both types are defined in code with `TitlePart`, `HtmlBodyPart` (the introduction), `SurveyPart` and `CourseItemPart`. Their course item kind is `Survey`; the kind supports the completion modes **Submitted** (the default: done when the learner answers), **Viewed** and **Marked done**, and produces no grade.

| Type | Content type | `SurveyPartSettings.SingleQuestion` |
| --- | --- | --- |
| Feedback survey | `FeedbackSurvey` | `false`: up to 50 questions. |
| Choice | `Choice` | `true`: one single or multiple choice question. |

`SurveyPart` holds:

| Property | Meaning |
| --- | --- |
| `Questions` | The questions in order: `Id` (kept when the text changes, so earlier answers still belong to the question), `Text`, `Type` (`SingleChoice`, `MultipleChoice`, `Rating` from 1 to 5, or `Text`), `Options` (up to 50, for choice questions) and `Required`. |
| `Anonymous` | The totals page lists written answers without names, and learners are told their answers are anonymous. |
| `AllowChangeAnswer` | A learner may answer again until the survey closes; the new answers replace the earlier ones in the same record. |
| `CloseUtc` | When the survey stops taking answers. The editor takes it in the editor's time zone (`ILocalClock`) and keeps it in UTC. |
| `ResultsVisibility` | When learners see the totals: `Never`, `AfterAnswer`, `AfterClose` or `Always`. |

The editor refuses a survey with no question, a choice question with fewer than two choices, more than 50 questions or choices, and a choice with more than one question.

## Answers

`ISurveyResponseService.AnswerAsync` takes a learner's answers under their own active enrollment in the survey's course, within the enrollment's access dates, once the item's release rules open it and before `CloseUtc`, or under their completed enrollment while they may still review the course (it has a grant and its access has not ended; release rules do not apply then, as for reviewing). The survey panel (`SurveyPartLearnerDisplayDriver`) lets a completed enrollment answer the same way and, when the learner cannot answer, says why (`SurveyPanelViewModel.CannotAnswerReason`: closed, answered and not changeable, pending, suspended, access ended, not open yet). The answers are checked on the server (`SurveyRules.CheckAnswers`): a required question must be answered, a choice must be one of the question's choices (one for a single choice question), a rating is a whole number from 1 to 5, and a written answer is up to 4,000 characters. Answers to questions the survey does not have are dropped. The write runs under a lock per enrollment and survey, saves the response, then reports `Submitted` to the learner's progress.

Refusals are `SurveyErrors`: `NotSurvey`, `NotEnrolled`, `AccessEnded`, `NotAvailableYet`, `Closed`, `AlreadyAnswered`, `Required` and `InvalidAnswer` (with the question id), and `Busy`.

The player posts to `learning/surveys/{surveyId}/answer` (route `LearningSurveyAnswer`), one form field per question named `q.` and the question id; it needs **Access learning**.

## Totals

`SummarizeAsync` counts each choice in the question's order, each rating from 1 to 5 with the average, and lists the written answers. Answers to a choice the author removed since are not counted.

The totals page is `/Admin/learning/surveys/{surveyId}/results` (route `LearningSurveyResults`), linked from the survey's editor as **Totals**. It opens to people with **Manage courses**, and to the course's staff who hold **Manage the courses you teach**. When the survey is not anonymous it names the learner of each written answer (the newest 1,000 responses). Learners see the same totals in the player when `ResultsVisibility` allows it.

## Data and personal data

- **Records.** Responses are kept in the `LearningSurveyResponses` collection with `SurveyResponseIndex` (enrollment, learner, course, survey, date).
- **Personal data.** The export lists a person's responses with every answer. Erasure deletes them: a survey answer is not evidence of training.

## Not in this version

- A recipe and deployment step for responses, a data source for reports, and workflow events. The survey items are content and move with the content steps; the responses stay on the site they were given on.
- A per-learner list of responses for staff beyond the named written answers on the totals page.
- Questions shared between surveys, branching, and pages of questions.
