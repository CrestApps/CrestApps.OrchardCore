---
sidebar_label: Moving courses from Moodle
title: Moving courses from Moodle
description: "Import Moodle course backups (.mbz) as courses: sections, SCORM packages, quizzes with their questions, files, links and pages, with a report of everything that was not imported."
technical_manual:
  - modules/learning-assessments
---

Moodle can back up a course into one file, a course backup with the `.mbz` extension. Import that file on your site and it becomes a course, with its sections, its SCORM packages, its quizzes and their questions, its files, links and pages. Everything the site cannot bring over is listed in the import's report, course by course, so you know what to finish by hand.

| | |
| --- | --- |
| **Menu** | Content > Bulk Import (or the import page of the **Course** content type) |
| **Permission** | Import the Course content type and the item types (SCORM package, quiz, file, link and page lessons) |
| **Feature** | Learning Moodle Import (it turns on Learning Assessments, Learning SCORM Packages, Course Categories, Content Transfer and Media) |

<AskYourAdmin />

## Back up the course in Moodle

1. In Moodle, open the course and go to **Course reuse > Backup** (in older versions, **Administration > Backup**).
2. Keep **Include activities and resources**, **Include blocks** and **Include files**. You do not need the users or their attempts: learners are not moved by this import.
3. Finish the backup and download the `.mbz` file from **Course backup area**.

Both kinds of `.mbz` file work: the zip archive older Moodle versions write and the tar archive newer versions write.

## Import the backup

1. Go to the import page of the **Course** content type.
2. Choose the `.mbz` file. The page says what a Moodle backup becomes.
3. Choose whether the imported content is published, and start the import.

The import runs in the background. When it ends, its row shows how many items were imported and how many were not; **Download failed rows** gives a file with the reason of each.

## What becomes what

| In Moodle | On your site |
| --- | --- |
| The course, its summary, start date and end date | A course with the same title, summary and dates. Its short name becomes the **Course code** when it differs from the title. |
| The course category | The category of the same name in **Course categories**, added when there is none. A backup knows only the category itself, not the categories above it, so build the tree first if you want the same tree: courses go into the category that has the name. |
| Sections | Sections of the outline, in the same order, with their summaries. |
| The course picture and pictures in the summary | The course's cover, shown in the catalog, and pictures in the media library. |
| Labels | **Text** items, shown in place in the outline, with their pictures. |
| SCORM packages | Learning packages, imported through the same checks as an upload, with their new window, attempts allowed and which attempt grades. |
| IMS content packages | Learning packages that complete when opened. |
| Assignments | **Coursework** (turn on Learning Coursework): due date, points, online text and files, or a grade-only sign-off when learners hand in nothing. |
| Feedback and choice | **Feedback surveys** and **Choices** (turn on Learning Surveys). |
| Quizzes | Quizzes with the same time limit, attempts, grading method, navigation, open and close dates, pass mark, questions per page, immediate feedback and review options. Their questions go into a question bank named after the course and the Moodle question category. A random question becomes a draw from that bank. |
| Files | File lessons with the file attached, shown by its own name. |
| URLs | Link lessons. |
| Pages | Page lessons. |
| Self enrolment with a key | The course takes an enrollment key. **The key itself is not imported**: set one on the course before learners can enroll. |
| Self enrolment without a key | Open enrollment. |
| Manual enrolment only | Closed enrollment: staff enroll learners. |
| An enrolment duration | The **Access length (days)**. |
| Activity completion | How each item completes: a SCORM package with a required score passes at that score, one with a passing grade at the grade to pass, and others when the package reports completed or passed; a quiz with a passing grade when it is passed; files, links and pages when they are opened, or when the learner marks them done if Moodle used manual completion. |
| Course completion | The activities it names are required in the outline and the others optional. A course grade to pass becomes the **Minimum course grade (%)**; manual completion by a role becomes approval by the course staff in that role (a teacher is an instructor, a non-editing teacher an assistant); a course without completion criteria never completes. |
| Cohort sync | The course's cohort sync, with the site's cohort of the same id number or name (turn on Cohorts and Enrollment by Cohort), when the person importing may manage enrollments. Otherwise the report says the cohort was not linked, in the same words whether or not the site has it. |
| Restrictions | An activity open only to people by e-mail address or user name opens only to the site's users with them; an opening date stays. Hidden activities are optional. |

Questions: multiple choice becomes single or multiple choice (or a survey question when no answer is worth points), and true/false, short answer, numerical, essay, matching, drag and drop into text, select missing words and description keep their kind; the choices no blank uses become the extra choices offered.

## What is reported instead

Each of these is a failed row of the import, naming the course and the activity:

- activities the site has no item for: forums, glossaries, databases, MooTyper and others, and assignments, feedback and choices while their feature is off;
- SCORM packages kept outside the backup (from a repository or a web address), and a package the package checks refuse, with the reason;
- questions of a kind the site does not have (embedded answers, calculated, drag and drop onto an image), questions whose text shows a picture, answers with pictures, matching questions with extra answers, and short answers with a wildcard;
- a cohort the site does not have or the importer may not link, guest access and other enrolment methods, and completion on a date, a length of enrolment, unenrolment, self completion or other courses;
- things to finish by hand: the enrollment key to set, a quiz password (not imported: only the learners enrolled in the course can take the quiz), a hidden course or activity (unpublish it if it should stay hidden), an assignment graded on a scale, a feedback item such as a captcha, restrictions on groups, grades or other profile fields, and a completion role no course staff role matches (kept on the course, and kept by the course editor, but nobody approves until you choose who does), and several completion roles (only the first is imported).

## Import a course again

Importing a backup of the same Moodle course again updates the course and its items instead of making new ones: they are recognized by the Moodle site and the Moodle course and activity ids. Activities added in Moodle since are added to the course, in their place in their section. Items and sections you added to the course yourself stay. A SCORM package is imported again each time.

## Before and after

- Turn on **Learning Moodle Import** on the features page. It needs a site with Learning, Learning Assessments and Learning SCORM Packages.
- Large backups: the import takes files up to the upload limit of Bulk Import (1 GB unless your administrator changed it). The media library's allowed file kinds and largest file size apply to the files of file lessons; a file it does not take is reported.
- After the import, look at the report, set the enrollment keys, check the outline and publish what you kept as drafts.
