---
sidebar_label: Quizzes
title: Quizzes
description: "Add a quiz to a course, build its questions in the survey designer with question banks, a time limit, attempts and a pass mark, and give a learner an accommodation."
technical_manual:
  - modules/learning-assessments
---

A quiz is a course item with scored questions. Its questions, question pools, time limit, attempts and pass mark are those of a quiz in [Surveys](/docs/user-manual/surveys): each quiz owns a scored survey, and every attempt of a learner is a response to it.

| | |
| --- | --- |
| **Menu** | A course's outline for the quiz; on the quiz, **Edit questions** and **Accommodations**; Surveys > Question banks |
| **Permission** | Manage courses or Manage the courses you teach to add the quiz; Manage question banks to write bank questions |
| **Feature** | Learning Assessments (with Surveys) |

<AskYourAdmin />

## Add a quiz to a course

1. Open the course outline and add a **Quiz**. Give it a title and save it. Saving makes the quiz's survey.
2. Choose how a learner's attempts combine into their grade under **The quiz grade is**: the highest score, the average score, the first attempt or the latest attempt.
3. Select **Edit questions**. The survey designer opens with a quiz to start from. Add the questions and their right answers and points. To draw questions at random, add a question pool that draws from a question bank.
4. In the designer's settings, set the **Scoring** (the pass mark, immediate feedback) and when learners may review their answers, the score and the right answers. On **Access and settings**, set how many attempts are allowed, the delay between them and when the quiz opens and closes.
5. Select **Publish**. Learners can attempt the quiz only once its survey is published.

A quiz's survey has no public link: only learners enrolled in the course can take it, from the course, while the quiz is published. Copying a quiz copies its questions into a survey of its own. Quiz surveys show in **Surveys > Surveys** only when **Show** is set to **Also surveys owned by other features**.

Questions that many quizzes share are written once in **Surveys > Question banks**. Questions written in another system can be imported into a bank from a spreadsheet or, with the **QTI Question Import** feature, from QTI files. See [Surveys](/docs/user-manual/surveys).

## Taking a quiz

The learner opens the quiz in the course and selects **Start the quiz**. The questions open on the survey page; answers are saved as they go. The time left is kept by the site, not the browser: when the time is up, the attempt is submitted on its own. When the attempt ends, the last page links back to the course, and the quiz shows the score and whether the learner passed, as far as the quiz's review settings allow. An answer that needs a grader, such as an essay, shows **Waiting for a grader** until it is graded. When attempts are left, the quiz offers **Attempt again**.

## Give a learner an accommodation

On the quiz, select **Accommodations** and grant one for a learner: extra minutes, extra attempts, or another window, with the reason. The accommodation is recorded in the Audit Trail.
