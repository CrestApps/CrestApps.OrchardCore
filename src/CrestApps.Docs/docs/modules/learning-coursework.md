---
sidebar_label: Learning Coursework
title: Learning Coursework
description: "The coursework course item of Learning: work learners hand in as text or files, rubrics, grading with feedback and regrades, and the grade counted in the course grade."
---

**Learning Coursework** (`CrestApps.OrchardCore.Learning.Coursework`, with `Learning.Coursework.Abstractions` and `Learning.Coursework.Core`) adds the **Coursework** course item to [Learning](./learning): work a learner hands in, as typed text, files or both, which a grader scores against a rubric or out of a number of points. The grade counts in the course grade like a quiz score, and the work waits in the same **Grading** list as quiz attempts.

| Feature | Id | Depends on |
| --- | --- | --- |
| Learning Coursework | `CrestApps.OrchardCore.Learning.Coursework` | Learning, [Learning Gradebook](./learning-assessments) |

## The coursework item

Coursework is a content item of the **Coursework** type (`CourseworkLesson`, defined in code with `TitlePart`, `HtmlBodyPart` for the instructions, `CourseworkPart` and `CourseItemPart`). Its course item kind is `Coursework`. A new coursework item is **gradable**, so its grade counts in the course grade with the item's weight, and its completion mode is **Graded**: the item is done once the work is graded, so the course cannot complete before the grade is in. A gradable coursework item cannot use **Submitted** (done once the work is handed in); the editor refuses it. An item that is not gradable may use either. Add it to a course outline like any other course item.

`CourseworkPart` holds:

| Property | Meaning |
| --- | --- |
| `DueUtc` | When the work is due. The editor takes it in the editor's time zone and keeps it in UTC; work handed in after it is marked late. Empty means no due date. |
| `LatePenaltyPercentPerDay`, `MaxLatePenaltyPercent`, `CloseAfterDays` | The late policy (see [Late work](#late-work)). |
| `SubmissionType` | What the learner hands in: `Text`, `Files`, or `TextAndFiles` (text, files or both, the default). |
| `AllowedExtensions` | The file types taken, such as `.pdf, .docx`. Empty takes any type. |
| `MaxFileSizeMb`, `MaxFiles` | The largest file (1 to 50 MB) and the most files (1 to 10). |
| `AllowResubmission` | Whether the learner may hand the work in again until it is graded. A grader can always return work to hand in again. |
| `RubricId` | The published rubric the work is graded against. Empty grades out of `MaxPoints`. |
| `MaxPoints` | The points the work is graded out of without a rubric. |

Once the coursework is saved, its editor links to its **Submissions** page.

## Rubrics

A rubric is a content item of the **Rubric** type (`Rubric`, with `TitlePart` and `RubricPart`), creatable and listable like any content. Its editor lists **criteria**, each with a title, what the grader looks for, and **levels** with a title and points; **Add a criterion**, **Add a level** and the remove buttons work without a page reload, and rows without a title are left out. Criteria and levels keep their ids when the rubric is edited, so a grade that chose a level still finds it.

A grader picks one level of each criterion. The total is the sum of the chosen levels' points, out of the sum of each criterion's best level, and the grade is that share as a percentage, rounded to two decimals (`RubricScoring.Score`). Every criterion needs a level.

## Handing work in

On the coursework item in the learner player, the learner sees the due date and the file rules, their latest hand-in with its state, whether it was late, its attempt number, and, once graded, the grade, the level chosen of each rubric criterion and the feedback. A learner with an active enrollment in the course hands work in with the form below (text and files, `multipart/form-data`); the form appears again when the coursework takes a resubmission or a grader returned the work.

`ISubmissionService.SubmitAsync` accepts the work only under the signed-in learner's own active enrollment in the item's course, found from the item; only the coursework item comes from the form. It refuses work that breaks the rules (wrong file type, a file too large, too many files, text where only files are taken and the other way round) before it keeps anything. The files are kept in the protected file store before the submission is saved, and removed again when the store refuses one (an antivirus scanner, for instance) or the save fails. Work that waits for a grader is refused unless the coursework takes a resubmission, and graded work is refused until a grader returns it. A hand-in again is the next attempt; the earlier submission stays as it was and is no longer current.

The form posts to `/learning/coursework/{courseworkId}/submit`. A file opens at `/learning/coursework/submissions/{id}/files/{fileId}`, through a short-lived protected link: only the learner who handed it in and the people who may grade their work open it.

## Grading

The **Submissions** page of a coursework item (`/Admin/learning/coursework/{courseworkId}/submissions`) lists every hand-in, newest first, with the learner, the attempt, when it came (in the user's time zone), whether it was late and its state. The grading page of a submission (`/Admin/learning/coursework/submissions/{id}`) shows the text and the files, then the rubric with a choice of level for each criterion, or a points box, and the feedback. **Grade** saves the grade; **Return for resubmission** sends the work back to the learner with the feedback.

The points box takes "7.5" and "7,5" alike, in every admin language. Feedback can be up to 10,000 characters and the reason for a regrade up to 500, on the server as on the page.

`ISubmissionService.GradeAsync` computes the percentage and reports it to Learning (`ILearnerProgressService.RecordResultAsync`) as a staff result, so it reaches the learner's progress, the course grade and the gradebook even after the learner's access window ended or a release rule locked the item again. When Learning still refuses it (the enrollment is suspended, ended or withdrawn, or stays busy after a retry) the grading fails with a message and nothing changes. Grading graded work again, or returning it, needs **Override grades**, checked on the fresh read under the lock; a regrade also needs a reason, and the earlier grade is kept in the submission's history with who replaced it, when and why. Returning work takes its grade out of the course grade (the grade stays in the history) and reopens the item until the learner hands it in again. A completed enrollment is a record: a grade that comes after the course was completed does not change it, and a lower grade flags the enrollment for review, as for quiz results.

Every hand-in, grade and return runs under one lock per enrollment and coursework item, on a fresh read of the submission. Nobody grades or returns their own work. A learner hands work in only within their access dates and once the item's release rules open it, as for any other item.

### The grading list

The **Grading** list of [Learning Assessments](./learning-assessments) (`/Admin/learning/grading`) shows the submissions that wait for a grader beside the quiz attempts that do, oldest first, each linking to its own grading page. Coursework adds them through the grading list's extension point, `IGradingQueueSource` (registered with `AddGradingQueueSource<T>()`).

## Late work

The **Late work** card of the coursework editor sets the late policy. It applies only when the coursework has a due date.

- **Late penalty a day (%)** (`LatePenaltyPercentPerDay`): the share of the grade late work loses for each day late. 0, the default, takes nothing off.
- **Largest late penalty (%)** (`MaxLatePenaltyPercent`): the most the penalty can reach, from 0 to 100 (100 by default).
- **Stop taking work after (days)** (`CloseAfterDays`): whole days after the due date after which the coursework takes no more work. 0 takes no late work; empty takes late work at any time.

**Days late** are the time between the due date and the hand-in, a part of a day counting as a whole day: 1 minute late and 24 hours late are both 1 day, 24 hours and 1 minute late are 2 days. Work on time is 0 days late. The **penalty** is the days late times the penalty a day, no more than the largest penalty. The **final score** is the raw score less the penalty, `raw × (1 − penalty / 100)`, rounded to two decimals (`CourseworkLatePolicy`).

When work is first graded, `GradeAsync` keeps the raw score (`RawScore`), the days late (`DaysLate`), the penalty (`LatePenaltyPercent`) and the final score (`Score`) on the submission, and reports the final score to the course grade. The days late are counted from the due date the coursework had when the work was handed in, which the submission keeps (`DueUtc`) and which also set its late flag, so moving the due date afterwards neither excuses late work nor makes work handed in on time late. Work a grader returned and the learner hands in again after the due date is late by its new hand-in time and takes the full penalty, including the days it was with the grader. A regrade applies the same days late and penalty to the new raw score, even when the policy or the due date changed since; the earlier grade keeps its own raw score and penalty in the history.

After the close date, `SubmitAsync` refuses work with `SubmissionErrors.Closed`, and the learner sees that the coursework no longer takes work. There is no extension for one learner. Work a grader returned for resubmission is still taken after the close date (it is late, and the penalty applies to it).

The learner panel shows the penalty a day and the close date with the due date. The grading page tells the grader before grading how many days late the work is and the penalty the grade will take; once graded, the grading page and the learner panel show the raw score, the penalty and the final score. Times are shown in the viewer's time zone.

## Grades shown to learners

A learner sees the grade and the feedback as soon as the work is graded. The Gradebook has no setting yet to hold grades until an instructor posts them; when it gets one, the coursework panel will follow it.

## Workflows

| Activity | Kind | Input or properties |
| --- | --- | --- |
| **Coursework Submitted** | Event | Starts when a learner hands work in; optionally only some coursework items. |
| **Coursework Graded** | Event | Starts when work is graded, or graded again (`Regraded`, `PreviousScore`, `Reason`). |
| **Return Coursework For Resubmission** | Task | `SubmissionId` and `Feedback`, both Liquid. Outcomes Done and Failed. |

The events carry `SubmissionId`, `UserId`, `CourseworkId`, `CourseId`, `EnrollmentId`, `Attempt`, `Status`, `Late`, `Score`, `Points`, `MaxPoints`, `SubmittedUtc` and `Actor`, and run after the change is saved with the submission as the correlation id. The activities are described to CrestApps Recipes when that feature is on.

## Data, recipes and personal data

- **Data source.** The **Learning coursework** data source (`LearningCoursework`, for Data Pipelines and the Report Builder) has a **Submissions** data set: one row per hand-in with the learner's user id, the course, the coursework, the attempt, the state, whether it was late, when it came, the number of files, the grade, the grade before the late penalty, the penalty, the days late, the grader and the number of regrades. It holds no text, files, feedback or e-mail addresses, and needs **View learning reports**. The learner, grader, course and coursework ids reference the Users data set and the Course and Coursework content items, so a report joins them to show names and titles.
- **Recipe and deployment.** Submissions are learner records with the `LearningSubmissions` recipe and deployment step (section 10.1 of the plan). The deployment step is opt-in, can be limited to one coursework item, reads 500 submissions a page, names people by user name and needs **Export learner records**; the export is told to the learner record export observers. The step carries the file names and ids but not the files themselves, which stay in the protected store of the site they were handed in on. Coursework items and rubrics are content and move with the content steps.
- **Personal data.** The export lists a person's submissions with their text, file names, grade and feedback. Erasure deletes their files from the protected store and, as for quiz attempts, deletes every submission that is not evidence of a completed enrollment (Learning deletes those enrollments, so nothing is left in the grading list). A submission of a completed enrollment keeps its state, dates and grade, because the course grade of the enrollment rests on them, with its text, file list and every feedback and reason cleared; the erasure result says how many were kept this way.

## Permissions

Coursework adds no permission of its own. Grading uses the rule of the quiz grading pages (`GradingScope`): everyone with **Grade assessments**, or the graders, instructors and assistants on the staff of the course who also hold **Manage the courses you teach**. Grading graded work again needs **Override grades**; a grader without it sees graded work without the grading form and the **Return for resubmission** button. Learners need **Access learning** to hand work in.

## Pages

| Page | Address | Who |
| --- | --- | --- |
| Submissions of a coursework item | `/Admin/learning/coursework/{courseworkId}/submissions` | Graders of the course |
| Grading page of a submission | `/Admin/learning/coursework/submissions/{id}` | Graders of the learner's course |
| Grading list (attempts and submissions) | `/Admin/learning/grading` | Graders |
| Hand in work | `/learning/coursework/{courseworkId}/submit` (POST) | The enrolled learner |
| A submitted file | `/learning/coursework/submissions/{id}/files/{fileId}` | The learner and their graders |

## Not in this version

- Holding grades until an instructor posts them.
- Notifications, Audit Trail events and webhooks for coursework.
- Group work and peer review.
