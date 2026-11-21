---
sidebar_label: Learning Assessments
title: Learning Assessments
description: "Question banks, quizzes in courses, attempts kept on the server, grading, overrides and item analysis, for the CrestApps Learning module."
user_manual:
  - user-manual/learning/quizzes
  - user-manual/learning/grading
---

**Learning Assessments** (`CrestApps.OrchardCore.Learning.Assessments`, with `Learning.Assessments.Abstractions` and `Learning.Assessments.Core`) adds quizzes to [Learning](./learning) courses. It depends on Learning.

## Features

| Feature | Id | Adds |
| --- | --- | --- |
| Learning Assessments | `CrestApps.OrchardCore.Learning.Assessments` | Question banks, the thirteen question types, the Quiz course item, attempts, the player, grading, overrides and access codes. |
| Learning Gradebook | `CrestApps.OrchardCore.Learning.Assessments.Gradebook` | Grade scales and the gradebook of each course. |
| Learning QTI Import | `CrestApps.OrchardCore.Learning.Assessments.QtiImport` | Imports QTI 3.0 and QTI 2.1 assessment items as questions through Content Transfer (see [QTI import](#qti-import)). |
| Learning Moodle Import | `CrestApps.OrchardCore.Learning.Assessments.MoodleImport` | Imports Moodle course backups as courses through Content Transfer (see [Moodle import](#moodle-import)). |

## Content types

| Type | What it is |
| --- | --- |
| Question Bank | A list (Orchard's List part) of questions. Banks are private: only people who author or grade questions may view them. |
| Single choice, Multiple choice, True/false, Short text, Numeric, Matching, Ordering, Fill in the blanks, Essay | Questions. Each has `QuestionPart` (prompt, points, difficulty, tags, general feedback) and the part of its type with the key. |
| Survey (`SurveyQuestion`) | `SurveyQuestionPart`: options and whether several may be chosen. Every answer is accepted: at 0 points the question is ungraded; with points, any answer earns them. |
| Drag into text (`DragIntoTextQuestion`), Select missing words (`GapSelectQuestion`) | Both use `ChoiceBlanksQuestionPart`: a text whose blanks hold their right word as `[[word]]`, distractors no blank takes, and whether the words are shuffled. The learner drags the words onto the blanks, or picks one in each blank's drop-down list. Each blank earns its share. |
| Description (`DescriptionQuestion`) | Only `QuestionPart`: its prompt is a text shown between questions, with nothing to answer, no points and no number. |
| Quiz (`QuizLesson`) | A course item. `QuizPart` holds the fixed questions in their order and the random draw rules (count of questions from a bank, optionally by tag), the time limit, the window, the attempts allowed and the delay between them, the pass mark, how attempts combine (highest, latest, average), the navigation (free or one page after another), the questions per page, the feedback mode (deferred or immediate), what learners see at each moment of the review, and the honor statement. |

A question type is registered with `AddQuestionType` and scored by an `IQuestionScorer`; a site can add its own. `QuestionTypeDescriptor.MayBeUngraded` lets a type be worth 0 points (survey, description) and `Informational` marks a type with nothing to answer (description): the editor hides its points and the attempt refuses answers to it.

The **Order** list under the fixed questions sets the order every attempt asks them in (unless they are shuffled): its up and down buttons move the `<option>` elements of the picker's `<select>` (the `quiz-question-order` script), so the order posted is the order shown, and `QuizPart.QuestionIds` keeps it. The quiz editor picks the fixed questions with the `ItemSelector` component, which searches `/Admin/learning/questions/search?query=` (route `LearningQuestionSearch`, `QuestionPickerController`) for published questions by title, 50 at most, and renders only the chosen ones. The search needs **View question banks** or **Manage the courses you teach** and returns titles and question types only; without **View question banks**, it returns only the questions of the banks the person may view (View content on the bank, which the question privacy handler grants only to people who author or grade questions). Every other multiple select of the Learning modules is a bootstrap-select picker with a search box.

The **Draw at random** card edits `QuizPart.DrawRules` (a bank, an optional tag and difficulty, and a count; the stored format is unchanged). Its rows are added and removed in the page by the `quiz-draw-rules` script, which reads `/Admin/learning/question-banks/{bankId}/draw-summary` (route `LearningQuestionBankDrawSummary`, the same permissions as the question search, and refused for a bank the person may not view): the tags and difficulty of each published question of the bank an attempt can draw. From it the tag picker lists the bank's tags and the row shows how many questions match. Saving refuses a rule without a bank, with a count below one, or asking for more questions than its bank has that match. `QuizDrawRuleMatcher` (Assessments.Core) decides which questions match, for the attempt's draw and for these counts alike: the tag whatever its case, and the difficulty, each only when set.

## Attempts

`IAssessmentAttemptService` runs an attempt on the server:

- **Start** checks the learner may open the quiz (an active enrollment and a released item, or course staff), the window and any override, the attempts left and the delay, the access code and the honor statement, then draws the questions and **copies** each one into the attempt: prompt, choices, key, points and feedback. An attempt never reads the question again, so editing, pruning or deleting a question version cannot change a submitted attempt. The prompt and feedback are sanitized when they are copied, so a question saved without its editor cannot run script.
- **Deadlines** are kept on the server: the time limit plus any override, capped by the window. A leased work queue submits an attempt when its time is up; the deadline task also catches any attempt the queue missed.
- **Pages**: `QuizPart.QuestionsPerPage` is copied onto the attempt when it starts (`AssessmentAttempt.QuestionsPerPage`); without one, a free quiz shows every question on one page and a sequential quiz one question a page. `AttemptPaging` (Assessments.Core) splits the questions, and `IAssessmentAttemptService.GoToPageAsync` moves the learner, keeping the page in `AssessmentAttempt.CurrentPage`: a free quiz moves to any page, a sequential quiz only to the next one.
- **Answers** are saved one question at a time while the learner works. `AttemptNavigation.CanAnswer` decides which questions take an answer: any question of a free quiz, and on a sequential quiz only the questions of the current page, which stay open to change until the learner continues; a page left behind is locked and a later page cannot be read or answered. A description never takes an answer.
- **Immediate feedback** (`QuizPart.FeedbackMode = Immediate`, copied onto the attempt): the player shows a **Check** button under each question, which posts to `learning/attempts/{attemptId}/check/{index}` (route `LearningAttemptCheck`). `IAssessmentAttemptService.CheckAsync` saves the answer, scores it with its scorer and sets `AttemptItem.CheckedUtc`; from then on the answer cannot change (`ItemLocked`), and the question shows what the review options allow **during the attempt**. A quiz that defers feedback refuses a check (`CheckNotAllowed`).
- **Submit** scores every question with its scorer. A question that needs a person (an essay) leaves the attempt **Needs grading**.
- The attempt page asks before submitting in a Bootstrap modal (`quiz-submit-confirm.js`, never `window.confirm`), with the count of unanswered questions: those of the page from its fields, plus those of the other pages without a saved answer (`AttemptPageViewModel.UnansweredElsewhere`). The attempt and result pages link **Back to the course** at the top; the result page's **Continue** opens the next item of the outline (`NextItemId`), or the course completion panel (`LearningCourseCompleted`) once the enrollment is completed. A visitor who is not signed in is sent to sign in (the attempt page answered 404 before, which is what a learner whose sign-in had ended saw).
- **Review after the close**: an attempt keeps the close date it started with (the quiz's, or the learner's override). When a published quiz closes on another date, `QuizCloseDateHandler` moves the close date of every attempt that kept the quiz's previous one (`IAssessmentAttemptService.MoveQuizCloseAsync`, under each attempt's lock, after the commit), so a review that waits for the close opens on the new date; an override's own date stays.
- **Grade, regrade, void**: graders score the open questions; a regrade scores the attempt again with the key it kept; a void removes it from the quiz result. Each change keeps who, when and why in the attempt's score history. Nobody grades, regrades or voids their own attempt.

Every write to an attempt runs under one lock per attempt on a fresh read, commits before the lock is let go, and tells the observers (`IAttemptObserver`) after the commit, so a double submit grades once. A write that also records the result on the enrollment takes the enrollment lock inside the attempt lock; enrollment writes never take an attempt lock.

The quiz result reaches the enrollment through `ILearnerProgressService.RecordResultAsync`. When an attempt of a completed enrollment is voided or regraded lower, the enrollment stays completed and is flagged for review (see [Learning](./learning#reviews-after-completion)).

### Access codes

A quiz's access code is kept as a salted hash outside the quiz content item, in the same Learning secret store as course enrollment keys (`LearningSecretStore`, kind `QuizAccessCode`). Five wrong codes for one quiz within 15 minutes lock that learner out of it for 15 minutes.

## Who may see what

- Questions and banks fail Orchard's view permissions for everyone except people who manage or view question banks, so the display route, the content API, GraphQL and the content data source never return a key. Questions have no display shapes besides their title, so search results and list feeds show titles only.
- The attempt page never carries the key while the attempt is open, except for a question checked with immediate feedback when the review options allow the right answers during the attempt; the review shows what the quiz's review options allow.
- **Review options per moment** (Moodle's review options): `QuizReviewOptions` holds, for each moment, the `ReviewShows` flags (score, right answers, feedback of the answers and grader comments, general feedback). The moments are **during the attempt** (for checked questions), **right after** (within two minutes of the submit, `QuizReviewOptions.ImmediatelyAfterWindow`), **later while the quiz is open**, and **after it closes**. They are copied onto the attempt when it starts. A quiz without its own options (`QuizPart.ReviewOptions` is null) follows its review policy, which maps to options with `QuizReviewOptions.FromPolicy`: *once graded* shows everything at every moment, *once the quiz closes* the score until the close and everything after, *score only* the score, *nothing* nothing. After the submit the review opens once the attempt is graded. `AttemptReviewPolicy` (Assessments.Core) decides: `Shows(attempt)`, `ShowsFor(attempt, item)`, `CanReview` and `CanSeeScore`.
- **Grading**: people with **Grade assessments** grade every course; a course's Graders, Instructors and Assistants grade its attempts only if they also hold **Manage the courses you teach**. The rule is `GradingScope` in `Learning.Assessments.Core`, which other grading pages (coursework) use too.

## The grading list

The **Grading** page (`/Admin/learning/grading`) lists all the work that waits for a grader in the courses the person grades, oldest first. Quiz attempts that need a grader are one source; other modules add theirs through the grading list's extension point in `Learning.Assessments.Abstractions`:

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
- **ContentTransfer**: questions can be written in a spreadsheet: prompt, points, difficulty, tags, feedback and source identifier for every question, `Options` and `OptionFeedback` for choice questions (separated by `|`, the right ones starting with `*`), `Pairs` (`prompt=answer|...`) for matching, `Items` in their right order for ordering, `Text` with `[[answer|other]]` blanks for fill in the blanks, and the answer columns of true/false, short text, numeric and essay questions. A backslash keeps `|`, `*`, `=` or `\` as text. The question types are one group of row types, so a `ContentType` column (such as `MatchingQuestion`) lets one sheet hold questions of every kind. The import page of a question type has a **Question bank** option; a prompt and feedback are sanitized on import. QTI files import through the same pipeline (see [QTI import](#qti-import)).
- **Telemetry**: attempts started, submitted, graded and score changes on the Learning meter (see [Observability](../observability)).

## QTI import

The **Learning QTI Import** feature (`CrestApps.OrchardCore.Learning.Assessments.QtiImport`, depends on Learning Assessments, Content Transfer and Media) imports QTI 3.0 and QTI 2.1 assessment items as questions. It is a [Content Transfer](./content-transfer) file format, so a QTI file goes through the same **Bulk Import** page, background import, progress, failed rows and permissions as a spreadsheet:

- **A content package** (`.zip` with `imsmanifest.xml` at its root): every resource of type `imsqti_item_xmlv3p0` or `imsqti_item_xmlv2p*` is read in the order of the manifest, its `href` under the `xml:base` of the manifest, the resources and the resource.
- **A single item** (`.xml`). It has no package around it, so an item with images imports from a package only.

Open the import page of any question type: each item becomes the question type that fits it (one import holds questions of every kind, through Content Transfer's row content types), and the **Question bank** option puts them in a bank you may edit.

### Safety

The package passes the safe extraction rules of learning packages (`PackageArchiveReader` in `Learning.Core`): every entry is checked before anything is read (no absolute path, drive letter or UNC share, no `..`, colon, control character, device name or trailing dot, no symbolic link, no duplicate ignoring case, at most 10,000 entries) and the bytes read are counted against 200 MB as they are read. Every XML file is read with DTDs refused, no resolver (so no external entity) and at most 10 million characters. Prompts and feedback are written as HTML from an allow-list of elements and attributes, then sanitized by the question import as the editor sanitizes them.

### What is mapped

| QTI | Question |
| --- | --- |
| `choiceInteraction` with one right choice and `maxChoices` 1 | Single choice; `shuffle` kept. |
| `choiceInteraction` with `cardinality="multiple"`, `maxChoices` other than 1, or several right choices | Multiple choice; partial credit when the item uses `map_response`. |
| A single-choice item with exactly two choices *True* and *False* | True or false. |
| One `textEntryInteraction` alone in its block, string base type | Short text: the correct response and the mapped answers worth points; case-insensitive when the map entries say `caseSensitive="false"`. |
| One `textEntryInteraction` with base type `float` or `integer` | Numeric: the correct response, and the tolerance of an `equal` rule (`absolute`, or `relative` as a share of the answer; the narrower bound when the two differ). |
| `textEntryInteraction`s inline in a text, or several | Fill in the blanks: the blocks that hold them become the text with `[[answer\|other answer]]` blanks, the other blocks the prompt. |
| `extendedTextInteraction` | Essay; a rubric block for scorers becomes the grader guidance. |
| `matchInteraction` (two sets, each source matched to one target) and `associateInteraction` (each choice in one pair) | Matching. |
| `orderInteraction` (the correct order of every choice) | Ordering. |
| `gapMatchInteraction` (each gap filled by one word) | Drag into text: the blocks of the interaction become the text, each gap written as its word; the words no gap takes become distractors. |
| `inlineChoiceInteraction`s (one right choice each) | Select missing words: the blocks that hold them become the text, each list written as its right choice; the wrong choices become distractors, and the other blocks the prompt. |
| An item with content and no interaction | Description, worth no points. |

Points come from the `MAXSCORE` outcome's default value, else the `normalMaximum` of `SCORE`, else the mapping (the sum of the positive mapped values of the right answers within `upperBound`, or the best single answer for a text entry), else 1. Response processing must be the `match_correct` or `map_response` template, or inline rules made of the elements those templates use (`responseCondition`, `match`, `mapResponse`, `equal`, `sum` and the like). An item with a correct response and no response processing is read as `match_correct`. Feedback tied to a choice (`feedbackInline` in the choice, or `modalFeedback` whose identifier is a choice's) becomes that option's feedback; a single other `modalFeedback` becomes the general feedback. A candidate rubric block stays in the prompt.

Images in the prompt or the feedback (PNG, JPEG, GIF or WebP files of the package, at most 10 MB each and of a kind the media library accepts) are stored in the media library under `qti-imports/{import id}/` with each path segment kept to letters, digits, dots, dashes and underscores, and the HTML points at them. The handler re-checks each image: one of those four types by extension and by its first bytes, and it reads the `QtiMedia` and `QtiError` columns only for an import the QTI reader read (a `.zip` or `.xml` file), so a spreadsheet row with those columns writes nothing to the media library. Each import has its own folder, so importing a package again writes its images to a new folder and the questions point there; the folder of the earlier import stays until someone deletes it. With **Secure Media** on, give the roles that take quizzes **View media content in folder 'qti-imports'**, or learners see the question without its picture. An image only the general feedback shows (not the prompt) is not put in the media library, where learners could open it before the feedback is shown: it is kept in a protected file area of the tenant's App_Data (`LearningQtiFeedbackImages`) and served at `/learning/assessments/feedback-images/{file id}` to people with **View question banks and answer keys** or **Grade assessments**, to graders, instructors and assistants of a course who hold **Manage the courses you teach**, and to a learner once the review of one of their attempts shows that feedback (`QtiFeedbackImageAccess`, with `AttemptReviewPolicy`). Anyone else gets 404. Each import keeps its own copies.

Each question keeps its QTI identifier (`QuestionPart.SourceIdentifier`) and its source file (`SourceFile`, such as `bank.zip/items/choice.xml`). Importing a row with the same source identifier and the same source file into the same chosen bank again updates that question (`QuestionSourceRowFilter` and the `QuestionSourceIndex`), so a corrected package can be imported over the first one. QTI identifiers such as `item1` repeat across packages, so nothing is matched without a chosen bank, a question from another file is never matched, and the person who queued the import must be allowed to edit the matched question; otherwise the row makes a new question.

### What is not imported

An item that cannot be imported is a failed row of the import, with the reason, and nothing is saved for it:

- Interactions no question type can hold: hotspot, select point, graphic order, graphic associate, graphic gap match, position object, slider, drawing, upload, media, hottext, end attempt, custom and portable custom interactions (PCI).
- Adaptive items and templates (template declarations, processing and blocks).
- Items with interactions of several kinds or more than one block interaction.
- Response processing other than the two templates (`map_response_point`, custom operators and the like), and inline rules beyond the standard shapes (the two templates written out, a mapped response, or a sum of mapped blanks).
- Scoring a question type cannot keep (`UnsupportedScoring`): mapped answers worth different points, negative mapped values or a negative default (penalties), and a single choice with several right answers. These are refused with the reason rather than graded another way.
- Choices, answers or blanks that hold markup the question type keeps as plain text (an image, math, a table); a blank answer with `|`, `[` or `]`.
- Matching distractors (a target that matches no source), a source matched to several targets, an order of only some choices, a gap filled by no word or by several, and an inline choice list with several right choices.
- Math, audio, video, objects and other content outside the HTML allow-list; images outside the package, SVG images, or images the package lacks.

Not kept: `fixed` choices in a shuffled list, `minChoices`, expected lengths and pattern masks, conditional feedback beyond the per-choice and single general feedback, stimuli and shared passages, assessment tests and sections, and item metadata. There is no QTI export.

### Conformance

The import reads the QTI 3.0 and QTI 2.1 assessment item information models as described above. Conformance suite / certification: not run (needs an external account). 1EdTech's QTI certification of an importing platform is run by a 1EdTech member with access to its conformance program: the member registers the product, imports 1EdTech's certification packages and submits the results for review. The tests in this repository use packages written for them from the specifications' text and examples.

## Moodle import

The **Learning Moodle Import** feature (`CrestApps.OrchardCore.Learning.Assessments.MoodleImport`, depends on Learning Assessments, Learning SCORM Packages, Course Categories, Content Transfer and Media) imports Moodle course backups (`.mbz`) as courses. Like the QTI import it is a [Content Transfer](./content-transfer) file format (`MoodleFileFormatProvider`), import only, offered on the import page of the **Course** type: one backup is one course, with the same background import, progress, failed rows and permissions as a spreadsheet. Its rows may be of the Course, Learning package, Quiz, File lesson, Link lesson, Page lesson and Text types, and, while their features are on, Coursework, Feedback survey and Choice (`MoodleRowTypeProvider`, which offers a type only while its course item kind is registered); the person needs to be allowed to import each.

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
| `quiz` | A quiz: time limit, attempts, grading method, navigation, open and close dates, pass mark (grade to pass over the most points), questions per page, immediate feedback for Moodle's immediate feedback and interactive behaviours, and the review options per moment from Moodle's review bits (`MoodleQuizSettings`). Fixed questions in slot order; random slots become draw rules from the bank of their category, which gets every question of the category. |
| Questions | `MoodleQuestionMapper` (`Learning.Assessments.Core`) maps them onto the QTI import's question model, and they are imported through its question rows and column handlers into a bank per Moodle question category (titled after the course and the category, found again by its questions). Multiple choice becomes single or multiple choice (partial credit for several right answers); true/false, short answer (the answers worth the full mark, case), numerical (answer and tolerance), essay (grader information as guidance) and matching keep their kind; a multiple choice with no answer worth points becomes a survey question (every answer counts); drag into text and gap select keep their kind, with the choices no blank uses as distractors; a description becomes a description worth nothing. |
| `resource` | A file lesson; the files go to the media library under `mediafields/FileLesson/<item id>/`, shown by their own names, within the media library's allowed extensions and largest size. |
| `url` | A link lesson (http and https only). |
| `page` | A page lesson. |
| `label` | A Text item, with its pictures in the media library. |
| `assign` | Coursework (Learning Coursework): due date, points (a scale is flagged and graded out of 100), online text and/or files or, with neither, a grade-only sign-off, at most 20 files, the largest file, resubmission; it completes on hand-in when Moodle does, else when graded (`MoodleAssignments`). |
| `feedback` | A feedback survey (Learning Surveys): radio, check box, drop-down and rated items become choice questions, text and number items written ones; labels, information and page breaks are left out and anything else (a captcha) is flagged; anonymity, change of answer and close date (`MoodleSurveys`). |
| `choice` | A Choice survey: one question (the description), its options, several answers or one, change of answer, close date, anonymity and when the totals show. |

The text of an item (description, page content, label) keeps its pictures: the files of the activity's text areas go to the media library under the item and the `@@PLUGINFILE@@` links point at them; a picture whose file is missing is left out. A restriction by e-mail address or user name (Moodle's profile condition "is equal to") becomes the `People` release rule with the site's users that have it, and an opening date the `Date` rule; other conditions are flagged (`MoodleRestrictions`). A hidden activity is optional in the outline.

**Enrolment:** self enrolment with a key becomes the key mode, and the key is never imported (a failed row says to set one, and the course editor asks for it); self enrolment without a key is open; manual enrolment only is closed; Moodle's `apply` plugin is approval; a self enrolment duration is the access length.

**Completion:** the activities of the course completion criteria are required in the outline and the others optional (without activity criteria, every activity with completion tracking is required). A course grade to pass becomes the **Minimum grade** rule, as a percentage of the course's most points; manual completion by a role becomes **Staff approval** by the matching course staff role (`editingteacher` Instructor, `teacher` Assistant, any other role its short name, which is flagged); a course without completion criteria never completes (`NeverCompletes`). **Cohort sync** becomes the course's cohort sync (Enrollment by Cohort, `LearningCohortsPart`): the site's cohort whose id is the Moodle cohort's id number or id, or whose name is the cohort's or the enrolment's name (`MoodleCohorts.Match`; a backup always keeps the Moodle cohort id in `customint1`, its name and id number only when it carries them); a cohort the site does not have, an importer without **Manage enrollments** (both in the same words, so the report never tells which cohorts exist), or Cohorts turned off, is a failed row; the import links a cohort only for an importer who may manage enrollments and records them as `LearningCohortsPart.LinkedBy`. Per item (`MoodleCompletion`): a SCORM package with a required score completes at that score (**Minimum score**), one with a passing grade passes at the grade to pass, and otherwise completes on the status it reports; manual completion is marked done; a quiz with a passing grade is passed, with a grade graded, otherwise submitted; files, links and pages are viewed.

Each item joins the course (`ContainedPart`) and its place in its section, after the items that come before it in Moodle.

### What is reported

Each is a failed row naming the course and the activity, in the category **Not imported from Moodle** or **Needs attention after the Moodle import**: activity types the site has no item for (forum, glossary, database, MooTyper, book, wiki...), an item whose feature is off (coursework, surveys), SCORM packages kept outside the backup or refused, questions of other types (embedded answers, calculated, drag and drop onto an image...), questions whose text shows a picture, answers with pictures, matching questions with distractors, short answers with a wildcard, questions the backup lacks; a cohort the site does not have or the importer may not link, guest access and other enrolment methods, completion on a date, a duration, unenrolment, self completion or other courses, and "any condition" completion; and what to finish by hand: the enrollment key, a quiz password, hidden courses and activities (imported, optional), an assignment graded on a scale, feedback items such as a captcha, restriction conditions other than people and dates, a completion role no course staff role matches (kept on the course, and kept by the course editor, but nobody approves until you choose who does), and several completion roles (only the first is imported).

### Importing again

`MoodleSourceRowFilter` gives a row the id of the course or item an earlier import made (`LearningSourceIndex`, of the row's type, editable by the person who queued the import), so the import updates it. Activities added since join their section in their place. Questions are matched by their source identifier (`QuestionSourceIndex`). A SCORM package is imported again each time, and its earlier file stays in the protected store.

### Not built

The web-services path (`core_course_get_contents`); pictures in questions; section summary pictures (section summaries are plain text); users, enrolments and attempts; competencies and groups; restrictions on other profile fields, groups, grades and completion; question categories no quiz uses.

## Recipe and deployment steps

| Step | Carries |
| --- | --- |
| `LearningGradeScales` | Grade scales with their bands; a second import updates by id. |
| `LearningAttempts` | Attempts (people by user name), quiz overrides and quiz access codes as hashes; needs **Export learner records** to export. |

Both have CrestApps Recipes schemas.
