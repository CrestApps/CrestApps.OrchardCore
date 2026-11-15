---
sidebar_label: Learning Assessments
title: Learning Assessments
description: "Question banks, quizzes in courses, attempts kept on the server, grading, overrides and item analysis, for the CrestApps Learning module."
---

**Learning Assessments** (`CrestApps.OrchardCore.Learning.Assessments`, with `Learning.Assessments.Abstractions` and `Learning.Assessments.Core`) adds quizzes to [Learning](./learning) courses. It depends on Learning.

## Features

| Feature | Id | Adds |
| --- | --- | --- |
| Learning Assessments | `CrestApps.OrchardCore.Learning.Assessments` | Question banks, the nine question types, the Quiz course item, attempts, the player, grading, overrides and access codes. |
| Learning Gradebook | `CrestApps.OrchardCore.Learning.Assessments.Gradebook` | Grade scales and the gradebook of each course. |

## Content types

| Type | What it is |
| --- | --- |
| Question Bank | A list (Orchard's List part) of questions. Banks are private: only people who author or grade questions may view them. |
| Single choice, Multiple choice, True/false, Short text, Numeric, Matching, Ordering, Fill in the blanks, Essay | Questions. Each has `QuestionPart` (prompt, points, difficulty, tags, general feedback) and the part of its type with the key. |
| Quiz (`QuizLesson`) | A course item. `QuizPart` holds the fixed questions and the random draw rules (count of questions from a bank, optionally by tag), the time limit, the window, the attempts allowed and the delay between them, the pass mark, how attempts combine (highest, latest, average), the navigation (free or one question at a time), when learners may review, and the honor statement. |

A question type is registered with `AddQuestionType` and scored by an `IQuestionScorer`; a site can add its own.

## Attempts

`IAssessmentAttemptService` runs an attempt on the server:

- **Start** checks the learner may open the quiz (an active enrollment and a released item, or course staff), the window and any override, the attempts left and the delay, the access code and the honor statement, then draws the questions and **copies** each one into the attempt: prompt, choices, key, points and feedback. An attempt never reads the question again, so editing, pruning or deleting a question version cannot change a submitted attempt. The prompt and feedback are sanitized when they are copied, so a question saved without its editor cannot run script.
- **Deadlines** are kept on the server: the time limit plus any override, capped by the window. A leased work queue submits an attempt when its time is up; the deadline task also catches any attempt the queue missed.
- **Answers** are saved one question at a time. On a quiz answered one question at a time the learner sees and answers only the next unanswered question.
- **Submit** scores every question with its scorer. A question that needs a person (an essay) leaves the attempt **Needs grading**.
- **Grade, regrade, void**: graders score the open questions; a regrade scores the attempt again with the key it kept; a void removes it from the quiz result. Each change keeps who, when and why in the attempt's score history. Nobody grades, regrades or voids their own attempt.

Every write to an attempt runs under one lock per attempt on a fresh read, commits before the lock is let go, and tells the observers (`IAttemptObserver`) after the commit, so a double submit grades once. A write that also records the result on the enrollment takes the enrollment lock inside the attempt lock; enrollment writes never take an attempt lock.

The quiz result reaches the enrollment through `ILearnerProgressService.RecordResultAsync`. When an attempt of a completed enrollment is voided or regraded lower, the enrollment stays completed and is flagged for review (see [Learning](./learning#reviews-after-completion)).

### Access codes

A quiz's access code is kept as a salted hash outside the quiz content item, in the same Learning secret store as course enrollment keys (`LearningSecretStore`, kind `QuizAccessCode`). Five wrong codes for one quiz within 15 minutes lock that learner out of it for 15 minutes.

## Who may see what

- Questions and banks fail Orchard's view permissions for everyone except people who manage or view question banks, so the display route, the content API, GraphQL and the content data source never return a key. Questions have no display shapes besides their title, so search results and list feeds show titles only.
- The attempt page never carries the key while the attempt is open; the review shows it according to the quiz's review policy.
- **Grading**: people with **Grade assessments** grade every course; a course's Graders, Instructors and Assistants grade its attempts only if they also hold **Manage own courses**.

## Permissions

| Permission | Lets a person |
| --- | --- |
| Manage question banks | Create and edit banks and questions. |
| View question banks and answer keys | See banks and questions, with their keys. |
| Grade assessments | Grade, regrade and void attempts of every course. |
| Override grades | Change a graded score. |
| Manage assessment overrides | Give a learner extra time, extra attempts or another window. |
| Manage grade scales | Create and edit grade scales. |

## Workflows

Events: **Quiz Attempt Started**, **Quiz Attempt Submitted**, **Quiz Attempt Needs Grading**, **Quiz Attempt Graded** and **Quiz Score Changed**, each limited to chosen quizzes and, by default, not started by a workflow's own change. Tasks: **Void Quiz Attempt** and **Regrade Quiz Attempt**, with a required reason, as the actor `Workflow`. The input carries the attempt, quiz, course and enrollment ids, the status, score, percent and pass.

## Data source and reports

The **Learning assessments** data source (for Data Pipelines and the Report Builder, behind **View Learning reports**) has four data sets: `LearningAttempts`, `LearningAttemptAnswers` (each answer by the text the learner saw, with points and score), `LearningAttemptScoreChanges` and `LearningQuizOverrides`. No data set carries a key, feedback or grader guidance.

The coded report **Quiz results and item analysis** shows pass rates and mean scores by quiz and, for each question, its difficulty (the mean share of points) and discrimination (the top 27% of graded attempts against the bottom 27%).

## Other integrations

- **Notifications**: *Quiz graded* tells a learner when a person has graded their attempt.
- **Audit Trail**: manual grading, every score change and every quiz override.
- **Webhooks**: `learning.attempt.submitted` and `learning.attempt.graded`, with scores and never answers.
- **Personal data**: attempts with every answer (essays included) and overrides are exported; anonymizing deletes attempts that a completed enrollment does not rest on, and every override.
- **ContentTransfer**: questions can be written in a spreadsheet: prompt, points, difficulty, tags and feedback for every question, `Options` for choice questions (separated by `|`, the right ones starting with `*`), and the answer columns of true/false, short text, numeric and essay questions.
- **Telemetry**: attempts started, submitted, graded and score changes on the Learning meter (see [Observability](../observability)).

## Recipe and deployment steps

| Step | Carries |
| --- | --- |
| `LearningGradeScales` | Grade scales with their bands; a second import updates by id. |
| `LearningAttempts` | Attempts (people by user name), quiz overrides and quiz access codes as hashes; needs **Export learner records** to export. |

Both have CrestApps Recipes schemas.
