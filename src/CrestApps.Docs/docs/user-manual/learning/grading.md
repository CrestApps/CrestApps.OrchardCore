---
sidebar_label: Grading
title: Grading
description: "Grade quiz answers and coursework from one list, regrade or void a quiz attempt with a reason, see the gradebook, and read the quiz reports."
technical_manual:
  - modules/learning-assessments
---

Most quiz questions are scored automatically. Essays and coursework need a person. Quiz attempts are responses to the quiz's survey, so they are graded on the Surveys grading page; the course grading list gathers quiz answers and coursework in one place.

| | |
| --- | --- |
| **Menu** | **Learning** → **Grading**; **Learning** → **Gradebook**. Course staff with Manage the courses you teach see both, for their own courses. |
| **Permission** | Grade the work of every course, or Manage the courses you teach for a course's graders, instructors and assistants; Grade survey responses to grade quiz answers; a course's graders, instructors and assistants grade the quizzes of their own course without it |
| **Feature** | Learning Assessments; Learning Gradebook for the gradebook |

<AskYourAdmin />

## Grade a quiz attempt

1. Go to **Learning** → **Grading**. The list shows the quiz attempts and coursework that wait for a grader in the courses you may grade.
2. Open a quiz attempt. The Surveys grading page shows each question with the learner's answer.
3. Enter the points of each question that needs a grader and an optional comment, add overall feedback if you like, then select **Save grades**. When every question has a score, the attempt is graded and the course records the quiz grade. Without **Override grades** you can't change the points of a question that already has a score (one the site scored, or one another grader graded): its points box is read-only.

## Regrade or void a quiz attempt

- **Score again** scores the attempt with the key of its version, such as after a key was found wrong. Give the reason.
- **Change score** sets the total score, with a reason.
- **Score again**, **Change score**, changing the grades of an attempt that is already graded, and **Regrade versions** (in the survey's menu) need **Override grades**. Without it you see a graded attempt without these buttons.
- To void an attempt, open the survey's **Responses**, choose the response and void it with a reason. A voided attempt does not count in the quiz grade. Voiding or deleting a finished quiz attempt, or a finished response of a course survey that gives a score, needs **Override grades** too, because the grade can change. An attempt still in progress, or one that was abandoned, carries no grade: an instructor or assistant can void or delete it without **Override grades**, and so can they have background upkeep try stuck responses again.
- A workflow's **Regrade Survey Response** task doesn't ask for **Override grades**: whoever sets up workflows decides when it runs.
- An anonymous quiz or course survey counts toward the course as done once the learner finishes it. Its score never goes to the gradebook, and grading it later changes nobody's progress.
- An anonymous quiz is left out of the course grade even when **Counts toward the course grade** is ticked: once the learner finishes it, the other graded items make up the whole grade (until then it counts as 0, like any unfinished item). The quiz editor and the survey's **Access and settings** page warn about this. To grade a quiz, keep its survey identified.

Each change is kept in the attempt's score history and the Audit Trail. If the course was already completed and the change lowers the result, the enrollment stays completed and shows **Needs review**: a training manager opens it, decides what follows and clears the review with a note.

## Sign off coursework with nothing handed in

For practical work a mentor watches, set a coursework lesson's **Learners hand in** to **Nothing: a grader signs it off**, and keep it completed when graded. Learners then hand nothing in. To sign a learner off, open the coursework's **Submissions**, choose the learner under **Sign off a learner** and select **Sign off**; grade the work on the page that opens. A coursework lesson that takes work can also count as done when the learner hands it in, and still be graded afterwards.

Coursework takes up to 20 files a hand-in unless your administrator changes it.

## Gradebook and grade scales

**Learning** → **Grade Scales** maps a percentage to a grade, such as A to F or Pass and Fail. The **Gradebook** of a course shows every learner and the result of each graded item.

The course grade is the weighted mean of the graded items. On the course editor, the **Pass mark (%)** under **Completion and grade** is the course grade a learner needs to pass; leave it empty for a course without a grade. To weigh groups of items, such as homework and exams, add rows to the **Grade categories** card: a **Category**, its **Weight** and how many of its lowest scores to **Drop lowest**. Choose each graded item's **Grade category** on its **Course item** card. A category's score is the mean of its items after the lowest are dropped, and the course grade is the weighted mean of the categories. See [Grade categories](./courses.md#grade-categories).

## Quiz reports

Quiz reports are the Surveys reports under **Reports**: **Quiz Results** shows how scores spread, how many passed and each person's best and latest score; **Item Analysis** shows each scored question's difficulty (higher is easier) and how well it tells strong learners from weak ones. A question that tells them apart poorly, or the wrong way round, may have a wrong key.
