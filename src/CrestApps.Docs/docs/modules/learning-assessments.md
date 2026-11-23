---
sidebar_label: Learning Assessments
title: Learning Assessments
description: "Quizzes in courses on the Surveys module: each quiz owns a scored survey, attempts are survey responses through the enrollment, plus the grading list, the gradebook and the Moodle import."
user_manual:
  - user-manual/learning/quizzes
  - user-manual/learning/grading
---

**Learning Assessments** (`CrestApps.OrchardCore.Learning.Assessments`, with `Learning.Assessments.Abstractions` and `Learning.Assessments.Core`) adds quizzes to [Learning](./learning) courses. It depends on Learning and on [Surveys](/docs/modules/surveys), which owns every question, question bank, attempt, score and grade: a quiz is a course item whose questions live in a survey the item owns.

## Features

| Feature | Id | Adds |
| --- | --- | --- |
| Learning Assessments | `CrestApps.OrchardCore.Learning.Assessments` | The Quiz course item, the quiz panel of the course player, the course grading list and the quiz result of each enrollment. |
| Learning Gradebook | `CrestApps.OrchardCore.Learning.Assessments.Gradebook` | Grade scales and the gradebook of each course. |
| Learning Moodle Import | `CrestApps.OrchardCore.Learning.Assessments.MoodleImport` | Imports Moodle course backups as courses through Content Transfer (see [Moodle import](#moodle-import)). |

QTI files import into Surveys question banks through the **QTI Question Import** feature of Surveys (`CrestApps.OrchardCore.Surveys.Qti`).

## The quiz course item

**Quiz** (`QuizLesson`) has `TitlePart`, `HtmlBodyPart`, `CourseItemPart` (kind `Quiz`) and `QuizPart`:

| Property | Meaning |
| --- | --- |
| `SurveyId` | The survey the quiz owns. `QuizPartHandler` creates it from the Surveys **Quiz** template (kind `Quiz`, owner `Content:{quiz id}`) when the quiz is first saved, with a signed-in audience and no public web link (`Survey.NoDefaultWebLink`). A copied quiz (`IContentManager.CloneAsync`) gets a copy of the original's survey, published when the original is (`ILearningSurveyService.CopyOwnedSurveyAsync`). |
| `GradingMethod` | How the scores of a learner's attempts combine into the quiz grade: `Highest`, `Average`, `First` or `Latest`. |

Every other setting belongs to the survey, edited in the Surveys designer and on the survey's **Access and settings** page: the questions, question pools that draw from question banks, the time limit, the pass mark, the review options, immediate feedback, questions per page, the attempt limit, the delay between attempts and the opening window. The quiz editor shows the survey's status, its question count and published version, and links to **Edit questions** and **Accommodations**. Learners can attempt the quiz once its survey is published.

## Attempts

An attempt is a Surveys response (`SurveyResponse`). The quiz panel posts to `learning/quizzes/{quizId}/start`; `ILearningSurveyService.StartAsync` (`Learning.Core`) starts or continues a response with the learner's enrollment as its context (`LearningEnrollment:{enrollment id}`) and `CourseId`, `EnrollmentId` and `CourseItemId` in its properties, and the learner answers on the Surveys runner (`/s/r/{response id}`), which links back to the course when the response ends. What Learning adds to Surveys:

- `LearningEnrollmentAccessRule` lets a learner start, resume (`CheckResumeAsync`) and complete (`CheckCompleteAsync`) a response only while they may open the item (enrolled, the item published in its course and released, access not ended), for a response of their own enrollment. Ownership is decided whatever the item's publish state (`ILearningSurveyService.IsOwnedByCourseItemAsync`), so the survey of an unpublished quiz, or of one moved out of its course, refuses everyone instead of opening to anyone with its link or id. The learner is the signed-in user, read from the name identifier claim or, as the Surveys API does, the `sub` claim.
- `LearningSurveyResultObserver` hears completed, graded, rescored, voided and deleted responses and records the course item result through `ILearnerProgressService`. For a quiz, `QuizResultProvider` gives the grade with `QuizGradeCalculator` (the quiz's grading method over the graded responses of the enrollment, voided ones left out) and decides its pass with the survey's own rule, `SurveyPassRule` (`Surveys.Abstractions`): the score band the grade falls in decides when it is marked passing or failing, otherwise the pass mark, and a survey with neither makes no pass decision (no pass mark is never a pass). Surveys sets each response's `Passed` with the same rule, so the attempt, the course item and the quiz passed credential rule never disagree. The result carries `CourseItemResult.PassDecided`, so Learning never falls back to an item pass mark: the quiz kind sets `CourseItemKindDescriptor.DecidesPass`, and the course item editor neither offers nor keeps a pass mark for it.
- The quiz panel (`QuizPartLearnerDisplayDriver`) shows an attempt's percent and pass only when `ISurveyReviewPolicy.GetAllowed` (the review settings of the attempt's version at that moment, and **Show the score to respondents**) includes the score, and its **Review** link only when the review shows anything. Whether **Attempt again** shows, and the attempts left, come from `ILearningSurveyService.CheckStartAsync`, which runs the access rules of a start through `ISurveyStartCheck` (Surveys) with the same request `StartAsync` makes: the limit's counting mode and the accommodation's extra attempts apply as they do on the start itself.
- `LearningSurveyAccessProvider` lets course staff manage the surveys owned by their course items, and `LearningSurveyGradingScope` lets a course's Graders, Instructors and Assistants grade its quiz responses. The scope reads the latest version of every item those courses contain (`ICourseStructureService.GetContainedItemsAsync`), so a quiz saved only as a draft or an item that was unpublished keeps its attempts gradable, and looks up the surveys those items own in one lookup (`ISurveyManager.ListByOwnersAsync`), never once per item. For grading and reading responses the access provider reads the item the same way (`ILearningSurveyService.FindLatestOwnerItemAsync`), so a grader of an unpublished quiz also gets the scoring help, the file answers and the respondents' names.
- `LearningManagerSurveyAccessProvider` (Learning) and `AssessmentsSurveyAccessProvider` give the site-wide Learning roles what they need on the surveys course items own, and nothing on other surveys: **Manage courses** edits and publishes them, **View learner records** reads their responses, **Manage enrollments** grants accommodations, **Grade the work of every course** grades them and **Override grades** voids and deletes their responses. The **Learning roles** recipe therefore grants only the own-survey Surveys permissions.
- `LearningSurveyResponseFieldProvider` adds `CourseId`, `CourseTitle` and `EnrollmentId` to the Surveys `SurveyResponses` data set.
- Removing a quiz removes its survey: deleted when it has no responses, closed and archived when it has.

Grading, regrades, accommodations, score history, workflow events, webhooks, notifications, the audit trail, reports (quiz results and item analysis) and the data sets of attempts are those of Surveys; see [Surveys](/docs/modules/surveys).

## Upgrading from the first release

Version 4 of `AssessmentsMigrations` upgrades a site whose Learning Assessments came from a 3.0 preview built before quizzes moved to Surveys (version 3). `UpdateFrom3Async` drops the old `Learning_AssessmentAttemptIndex`, `Learning_AttemptItemIndex`, `Learning_AssessmentOverrideIndex`, `QuestionSourceIndex` and `LearningAttemptDeadlines` work queue tables; deletes the question bank and question items and removes the `QuestionBank` type, the thirteen question types and their parts (`LegacyQuizUpgrade.RemovedContentTypes` and `RemovedContentParts`); and creates a scored survey for every quiz, carrying the pass mark, time limit, attempt limit (counted on started attempts) and window from the old `QuizPart` (`LegacyQuizUpgrade.ApplySettings`). Questions, attempts, overrides and access codes are not carried over. The migration itself only changes the definitions and drops the tables: deleting the old items and making the surveys run in the background a batch at a time (`LegacyQuizUpgradeStep`, a resumable step), so the migration never reads every item at once. A quiz points at its survey as soon as the survey is made, so a later failure never leaves a survey nothing points at; a quiz whose survey could not be made is logged and gets one when it is next saved.

## The grading list

The **Grading** page (`/Admin/learning/grading`) lists all the work that waits for a grader in the courses the person grades, oldest first. Quiz responses waiting for a grader (from the Surveys grading queue, through `QuizResponseGradingQueueSource`, opening the Surveys grading page) are one source. It finds a course's quizzes from the latest version of every item the course contains (`ICourseStructureService.GetContainedItemsAsync`), so attempts of an unpublished course or quiz stay listed, looks up their surveys in one query (`ISurveyManager.ListByOwnersAsync`), and reads the attempts with at most 1,000 survey ids in each query, under SQL Server's parameter limit. It reads at most the 1,000 oldest attempts for a page, so a page past those shows nothing while the count stays the real total; other modules add theirs through the grading list's extension point in `Learning.Assessments.Abstractions`:

```csharp
public interface IGradingQueueSource
{
    Task<GradingQueuePage> ListWaitingAsync(GradingQueueQuery query, CancellationToken cancellationToken = default);
}

services.AddGradingQueueSource<MyWorkGradingQueueSource>();
```

A source returns the count of its waiting work and its oldest items, at most `query.Take` of them, limited to `query.CourseIds` when that is not `null`. Each `GradingQueueItem` names its kind, the record, the learner, the course, the course item with its title, a short reference, an icon, the submitted time in UTC, and the area, controller, action and route values of the page that grades it. `GradingQueue` asks every source for the items before the page and the page itself, merges them by submitted time and pages across the merged list. [Learning Coursework](./learning-coursework) adds its submissions this way.

## Gradebook

The **Gradebook** page (`/Admin/learning/gradebook`, feature **Learning Gradebook**) shows, for one course, every learner with the score of each gradable item, the score of each grade category and the course grade, in percentages or on a grade scale.

### How the course grade is computed

An item counts in the course grade when it is gradable (**Counts toward the course grade** on the item) and its weight is more than zero. An item the learner has no score on counts as zero. `CourseGradeCalculator` in `Learning.Core` computes the grade wherever Learning stores or shows it (the enrollment's `Grade` and `Passed`, the completion criteria, the gradebook).

- **A course without grade categories**: the course grade is the weighted mean of the items, each weighted by its item weight.
- **A course with grade categories**: each category's score is the weighted mean (item weights) of its items, after its lowest scores are dropped. The course grade is the weighted mean of the category scores, each weighted by its category weight. A category with no item that counts is left out of the course grade.
- **Items without a category** (or with a category the course no longer has) count as one more category, named **No category** in the gradebook, whose weight is the sum of their item weights. It drops nothing. In other words, each such item weighs in the course grade as if it were a category of its own with its item weight, which is also how a course without categories works.

For example, Homework (weight 40) holds two worksheets that scored 100 and 50, so it scores 75; Exams (weight 60) holds a midterm (item weight 2) at 90 and a quiz (item weight 1) at 60, so it scores 80. The course grade is (40 × 75 + 60 × 80) / 100 = 78.

### Grade categories and drop lowest

Grade categories belong to the course: the **Grade categories** card of the course editor lists them, each with a name, a weight (more than zero) and **Drop lowest**, the number of the lowest scores of the category to leave out. **Add a category** and the remove buttons work without a page reload; rows without a name are left out, and a new category gets its id when the course is saved. The categories are stored on `CoursePart.GradeCategories` (`GradeCategory`: `Id`, `Name`, `Weight`, `DropLowest`).

The editor of a gradable course item has a **Grade category** select with the categories of its course (the course that contains the item), shown once the course has categories. The choice is stored on `CourseItemPart.GradeCategoryId`.

Dropping takes the lowest scores first; a missing score (zero) is dropped before a real zero, and items with the same score are dropped in outline order. A category never drops all its items: at least one item always counts, however large **Drop lowest** is.

In the gradebook each category has a column, after the items and before the course grade. A dropped score is struck through and marked **dropped**. `CourseGradeCalculator.ComputeBreakdown` returns the same breakdown (`CourseGradeBreakdown`: the grade, and for each category its score, its items and the items it dropped) for other pages and modules.

The course grade stored on an enrollment is computed again when the learner's progress changes; after the categories or weights of a course change, the stored grade of a learner follows on their next result.

## Permissions

| Permission | Lets a person |
| --- | --- |
| Grade the work of every course (`GradeAssessments`) | Use the grading list for every course, and grade and read the responses of every survey a course item owns (quiz attempts and course surveys), through `AssessmentsSurveyAccessProvider`. It gives nothing on other surveys. |
| Override grades | Change a graded coursework score, and void and delete the responses of every survey a course item owns. A quiz score is changed on the Surveys grading page (**Change score**, a new grade or a regrade, each with a reason and recorded in the score history), which anyone who may grade the quiz can do; this permission does not govern it. |
| Manage grade scales | Create and edit grade scales. |

## Moodle import

The **Learning Moodle Import** feature (`CrestApps.OrchardCore.Learning.Assessments.MoodleImport`, depends on Learning Assessments, Learning SCORM Packages, Course Categories, Content Transfer and Media) imports Moodle course backups (`.mbz`) as courses. It is a [Content Transfer](./content-transfer) file format (`MoodleFileFormatProvider`), import only, offered on the import page of the **Course** type: one backup is one course, with the same background import, progress, failed rows and permissions as a spreadsheet. Its rows may be of the Course, Learning package, Quiz, File lesson, Link lesson, Page lesson and Text types, and, while their features are on, Coursework, Feedback survey and Choice (`MoodleRowTypeProvider`, which offers a type only while its course item kind is registered); the person needs to be allowed to import each.

### Reading the backup

`MoodleBackupReader` (`Learning.Core`) reads both kinds of `.mbz`: the zip archive and the gzip-compressed tar archive Moodle 2.6 and later write. The archive is extracted by the safe archive rules of learning packages (`PackageArchiveExtractor.Extract`, and `ExtractTarGz` with the same checks for tar: every path through `PackagePath`, links, devices and other special entries refused, duplicates refused, entries and bytes counted as read), within `MoodleImportOptions` (configuration section `CrestApps:Learning:MoodleImport`: `MaxEntries` 100,000, `MaxExpandedBytes` 5 GB and `MaxXmlCharacters` 100 million, because a backup keeps the extracted files of its SCORM packages). Every XML file is read with DTDs prohibited, no resolver and that size limit, and the directories the backup names must stay inside it. Read: `moodle_backup.xml` (a course backup only), `course/course.xml`, `course/enrolments.xml`, `course/completion.xml`, `gradebook.xml`, `sections/section_*/section.xml`, `activities/<type>_<id>/` (`module.xml`, `<type>.xml`, `inforef.xml`, `grades.xml`), `questions.xml` (Moodle 4 question bank entries with the latest ready version of each, or Moodle 3 questions) and `files.xml`, whose files are under `files/` by content hash. An enrolment password is never read, only whether there is one.

The extracted backup stays in a temporary folder while the import's reader is open (`MoodleWorkspaces`); every row carries a random token for it, and the handlers act only on rows of a file the Moodle format read. A file that is not a readable course backup is one failed row with the reason.

### Rows and mapping

The course is the first row, then each activity in section order as the item that fits it (`MoodleCourseRows`); `MoodleImportHandler` imports them:

| Moodle | Becomes |
| --- | --- |
| Course | Title, summary (plain text in `CoursePart.Summary`, sanitized HTML in the body), code (the id number, or the short name when it differs from the title), start and end dates, its category (found by name in the course categories, added when missing: a backup keeps only the category's own name), enrolment and completion (below). |
| Course picture | The overview file becomes the course's `Cover` (media library, `mediafields/Course/<id>/`); the pictures of the summary go there too and the summary's links point at them (`MoodleFiles.Rewrite`). |
| Sections | Outline sections `moodle-<section id>` in order, with their summaries; sections and items of the course's own are kept. |
| `scorm` (stored in the backup) | A learning package through `LearningPackageImporter`, with the checks of an upload; a refusal is a failed row with the reason the upload page gives (`PackageImportErrorText`). Its new window (`popup`), attempt limit (`maxattempt`) and grading method (`whatgrade`) are kept (`MoodleScormSettings`). |
| `imscp` | A learning package from the package as it was uploaded (file area `backup`), or rebuilt from the files Moodle extracted (`content`); it plays without the run-time API and completes when viewed. |
| `quiz` | A quiz whose owned survey gets the settings: time limit, attempt limit, navigation, open and close dates, pass mark (grade to pass over the most points), questions per page, immediate feedback for Moodle's immediate feedback and interactive behaviours, and the review options per moment from Moodle's review bits (`MoodleQuizSettings`); the grading method goes on the quiz. Fixed questions in slot order on the survey's page; random slots become question pools that draw from the Surveys question bank of their category, which gets every question of the category. When the import publishes, the survey is published. |
| Questions | `MoodleQuestionMapper` (`Learning.Assessments.Core`) maps them onto the QTI item model, and the Surveys QTI mapper (`QtiQuestionMapper`) turns them into survey questions; a question's id comes from its Moodle source, so importing again keeps the answers given. Each Moodle question category becomes a Surveys question bank (titled after the course and the category). Multiple choice becomes single or multiple choice (partial credit for several right answers); true/false, short answer (the answers worth the full mark, case), numerical (answer and tolerance), essay (grader information as guidance) and matching keep their kind; a multiple choice with no answer worth points becomes a survey question (every answer counts); drag into text and gap select keep their kind, with the choices no blank uses as distractors; a description becomes a description worth nothing. |
| `resource` | A file lesson; the files go to the media library under `mediafields/FileLesson/<item id>/`, shown by their own names, within the media library's allowed extensions and largest size. |
| `url` | A link lesson (http and https only). |
| `page` | A page lesson. |
| `label` | A Text item, with its pictures in the media library. |
| `assign` | Coursework (Learning Coursework): due date, points (a scale is flagged and graded out of 100), online text and/or files or, with neither, a grade-only sign-off, at most 20 files, the largest file, resubmission; it completes on hand-in when Moodle does, else when graded (`MoodleAssignments`). |
| `feedback` | A feedback survey (Learning Surveys) whose owned survey gets the questions: radio, check box, drop-down and rated items become choice questions, text and number items written ones; labels, information and page breaks are left out and anything else (a captcha) is flagged; anonymity, change of answer and close date (`MoodleSurveys`). |
| `choice` | A Choice whose owned survey gets one question (the description), its options, several answers or one, change of answer, close date, anonymity and when the totals show. |

The text of an item (description, page content, label) keeps its pictures: the files of the activity's text areas go to the media library under the item and the `@@PLUGINFILE@@` links point at them; a picture whose file is missing is left out. A restriction by e-mail address or user name (Moodle's profile condition "is equal to") becomes the `People` release rule with the site's users that have it, and an opening date the `Date` rule; other conditions are flagged (`MoodleRestrictions`). A hidden activity is optional in the outline.

**Enrolment:** self enrolment with a key becomes the key mode, and the key is never imported (a failed row says to set one, and the course editor asks for it); self enrolment without a key is open; manual enrolment only is closed; Moodle's `apply` plugin is approval; a self enrolment duration is the access length.

**Completion:** the activities of the course completion criteria are required in the outline and the others optional (without activity criteria, every activity with completion tracking is required). A course grade to pass becomes the **Minimum grade** rule, as a percentage of the course's most points; manual completion by a role becomes **Staff approval** by the matching course staff role (`editingteacher` Instructor, `teacher` Assistant, any other role its short name, which is flagged); a course without completion criteria never completes (`NeverCompletes`). **Cohort sync** becomes the course's cohort sync (Enrollment by Cohort, `LearningCohortsPart`): the site's cohort whose id is the Moodle cohort's id number or id, or whose name is the cohort's or the enrolment's name (`MoodleCohorts.Match`; a backup always keeps the Moodle cohort id in `customint1`, its name and id number only when it carries them); a cohort the site does not have, an importer without **Manage enrollments** (both in the same words, so the report never tells which cohorts exist), or Cohorts turned off, is a failed row; the import links a cohort only for an importer who may manage enrollments and records them as `LearningCohortsPart.LinkedBy`. Per item (`MoodleCompletion`): a SCORM package with a required score completes at that score (**Minimum score**), one with a passing grade passes at the grade to pass, and otherwise completes on the status it reports; manual completion is marked done; a quiz with a passing grade is passed, with a grade graded, otherwise submitted; files, links and pages are viewed.

Each item joins the course (`ContainedPart`) and its place in its section, after the items that come before it in Moodle.

### What is reported

Each is a failed row naming the course and the activity, in the category **Not imported from Moodle** or **Needs attention after the Moodle import**: activity types the site has no item for (forum, glossary, database, MooTyper, book, wiki...), an item whose feature is off (coursework, surveys), SCORM packages kept outside the backup or refused, questions of other types (embedded answers, calculated, drag and drop onto an image...), questions whose text shows a picture, answers with pictures, matching questions with distractors, short answers with a wildcard, questions the backup lacks; a cohort the site does not have or the importer may not link, guest access and other enrolment methods, completion on a date, a duration, unenrolment, self completion or other courses, and "any condition" completion; and what to finish by hand: the enrollment key, a quiz password, hidden courses and activities (imported, optional), an assignment graded on a scale, feedback items such as a captcha, restriction conditions other than people and dates, a completion role no course staff role matches (kept on the course, and kept by the course editor, but nobody approves until you choose who does), and several completion roles (only the first is imported).

### Importing again

`MoodleSourceRowFilter` gives a row the id of the course or item an earlier import made (`LearningSourceIndex`, of the row's type, editable by the person who queued the import), so the import updates it. Activities added since join their section in their place. Bank questions are matched by their source identifier, and survey questions keep ids made from their source. A SCORM package is imported again each time, and its earlier file stays in the protected store.

### Not built

The web-services path (`core_course_get_contents`); pictures in questions; section summary pictures (section summaries are plain text); users, enrolments and attempts; competencies and groups; restrictions on other profile fields, groups, grades and completion; question categories no quiz uses.

## Recipe and deployment steps

| Step | Carries |
| --- | --- |
| `LearningGradeScales` | Grade scales with their bands; a second import updates by id. |

It has CrestApps Recipes schemas. Quizzes move with the content steps; their surveys, versions and responses move with the Surveys recipe steps.
