---
sidebar_label: Coursework
title: Coursework
description: "Add coursework that learners hand in as text or files, grade it against a rubric or out of points, take points off late work, and return work to hand in again."
technical_manual:
  - modules/learning-coursework
---

Coursework is a lesson where learners hand in work, as typed text, files or both. A grader scores it against a rubric or out of a number of points, and the grade counts in the course grade.

| | |
| --- | --- |
| **Menu** | A course's outline (add a **Coursework** item); **Submissions** on the coursework's editor; **Learning** → **Grading**; **Content** → **Content Items for rubrics** |
| **Permission** | Manage courses, or Manage the courses you teach, to add coursework; Grade assessments, or Manage the courses you teach for a course's graders, instructors and assistants, to grade; Override grades to grade work again or return graded work |
| **Feature** | Learning Coursework (turns on Learning Gradebook) |

<AskYourAdmin />

## Add coursework

1. In the course's list of lessons, create a **Coursework**. Give it a title and write the instructions in the body.
2. On the **Coursework** card, set the **Due** date in your time zone, or leave it empty for no due date. Work handed in after it is marked late.
3. Choose what **Learners hand in**: **Text or files, or both**, **Text only**, **Files only**, or **Nothing: a grader signs it off**.
4. For files, set the **File types** (such as .pdf, .docx; empty takes any type), the **Largest file (MB)** and the **Most files**. Your site sets the upper limits, 50 MB and 20 files unless your administrator changes them.
5. Tick **Learners may hand the work in again until it is graded** to let a learner replace their work while it waits for a grader. The newest hand-in replaces the earlier one.
6. Under **Grading**, choose a **Rubric**, or **No rubric: grade out of points** and set **Out of (points)**.
7. Save.

A new coursework lesson counts toward the course grade and is done when the work is graded. To make it done as soon as the learner hands it in, choose **When the learner hands in work** on its **Course item** card. The grade given later still counts.

## Late work

The **Late work** card applies only when the coursework has a due date.

- **Late penalty a day (%)**: the share of the grade late work loses for each day late. 0 takes nothing off.
- **Largest late penalty (%)**: the most the penalty can reach.
- **Stop taking work after (days)**: whole days after the due date when the coursework closes. 0 takes no late work. Leave it empty to take late work at any time.

A part of a day late counts as a whole day: 1 minute late and 24 hours late are both 1 day. For example, with 10% a day, work 2 days late that scores 80% gets 64%. The days late are counted from the due date the coursework had when the work was handed in, so moving the due date later does not change them.

After the close date, learners see that the coursework no longer takes work. Work a grader returned can still be handed in again; it is late, and the penalty applies.

## Rubrics

A rubric describes what a grader looks for. Create a **Rubric** in **Content** → **Content Items** and add its criteria with **Add a criterion**. Each criterion has a title, what the grader looks for, and levels, each with a title and points. Rows without a title are left out.

The grader picks one level of each criterion. The grade is the points of the chosen levels out of the best level of every criterion.

## How learners hand in work

A learner enrolled in the course opens the coursework in the course player. It shows the due date, the file rules, the late penalty and the close date. They type their answer or add their files and select **Hand in**.

They then see their work with its state: waiting for a grader, graded with the grade and feedback, or returned to hand in again. A late hand-in says so. When the coursework allows it, or a grader returned the work, **Hand in again** sends a new attempt. The earlier attempt is kept.

## Grade work

Work that waits for a grader shows in **Learning** → **Grading** beside quiz attempts, oldest first. The **Submissions** page of a coursework, linked from its editor, lists every hand-in with the learner, the attempt, when it came, whether it was late and its state.

1. Open a hand-in with **Grade**.
2. Read the text and open the files.
3. Pick a level of each rubric criterion, or enter the **Points**.
4. Write the **Feedback**. The learner sees it with the grade.
5. Select **Grade**.

For late work, the page tells you the days late and the penalty before you grade. Once graded, the learner gets a notification and sees the raw score, the penalty and the final score.

You cannot grade your own work.

## Return work or grade it again

- **Return for resubmission** sends the work back to the learner with your feedback. Its grade leaves the course grade until they hand it in again.
- **Grade again** changes a grade. Give the reason in **Why grade again**. The earlier grade is kept under **Earlier grades**.

Both need **Override grades** once the work is graded. A grader without it sees graded work without the grading form.

## Sign off work with nothing handed in

When **Learners hand in** is **Nothing: a grader signs it off**, learners hand nothing in, as for practical work a mentor watches. On the **Submissions** page, choose the learner under **Sign off a learner** and select **Sign off**. Grade the work you saw them do on the page that opens. See also [Grading](./grading.md#sign-off-coursework-with-nothing-handed-in).
