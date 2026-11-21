---
sidebar_label: Learning on Surveys
title: How Learning Uses Surveys
description: "How the Learning modules run course surveys, choices and quizzes on Surveys: owned surveys, enrollment contexts, the access rule, access provider, grading scope, result observer and data set fields."
user_manual:
  - user-manual/surveys/quizzes
---

[Learning](../learning.md) keeps one question engine: Surveys. Learning's course surveys, choices and quizzes are Surveys records owned by their course item.

## Owned surveys

A Feedback survey, Choice or quiz course item creates its survey on its first save, with `Owner = Content:{courseItemContentItemId}`. The course item's part holds only the survey id:

| Course item | Part | Holds |
| --- | --- | --- |
| Feedback survey, Choice (Learning Surveys) | `CourseSurveyPart` | `SurveyId`; the questions, close date, anonymity and when learners see the totals are the survey's own settings |
| Quiz (Learning Assessments) | `QuizPart` | `SurveyId` and `GradingMethod` (how the learner's attempts combine into the quiz grade); questions, pools, time limit, attempts, pass mark and review settings are the survey's |

Staff edit the questions in the Surveys designer. The survey's admin pages link back to the course item's editor through an owner link provider (`LearningSurveyOwnerLinkProvider`).

## Attempts and responses

A learner's attempt is a `SurveyResponse` started through the learner's own enrollment, with the context `LearningEnrollment:{enrollmentId}`. Response properties carry the course and enrollment, so workflow events, webhooks and data sets know them.

## What Learning adds to Surveys

Learning Core registers these in `src/Core/CrestApps.OrchardCore.Learning.Core/Surveys/`:

| Extension | Contract | Does |
| --- | --- | --- |
| `LearningEnrollmentAccessRule` | `ISurveyAccessRule` | Only enrolled learners respond to a course item's survey, through their active enrollment, with the item released to them; a public link never opens a course quiz to someone outside the course |
| `LearningSurveyAccessProvider` | `ISurveyAccessProvider` | Course staff manage the surveys owned by their course items |
| `LearningSurveyGradingScope` | `ISurveyGradingScope` | Course staff grade the responses of their course's quizzes |
| `LearningSurveyResultObserver` | `ISurveyResponseObserver` | Records the course item result when a response is completed, graded, rescored or voided (a course survey is submitted; a quiz combines attempts) |
| `LearningSurveyResponseFieldProvider` | `ISurveyResponseFieldProvider` | Adds `CourseId`, `CourseTitle` and `EnrollmentId` to the `SurveyResponses` data set |
| `LearningSurveyOwnerLinkProvider` | owner link provider | Links survey pages back to the course item |

## What Surveys replaces

- Questions, banks, attempts, accommodations, deadlines, grading, regrades and item analysis are Surveys features ([Scoring and grading](./scoring-and-grading.md)).
- Workflow events, webhooks, reports and data sets about attempts and answers are the Surveys ones ([Workflows](./workflows.md), [Data sets](./data-pipelines.md)); filter them by the context kind `LearningEnrollment`. Learning keeps its course-level events.
- QTI import is the Surveys **QTI Question Import** feature.
- AI help for quiz essays is the **Suggest a score with AI** button on the Surveys grading page ([AI](./ai.md)).
