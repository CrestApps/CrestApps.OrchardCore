---
sidebar_label: Learning
title: Learning
description: "Courses built from Orchard Core content, an outline of sections and lessons, enrollments with grants, progress and completion, release rules, learner pages, lesson files behind Secure Media, reports, workflows, recipes and webhooks."
user_manual:
  - user-manual/learning/courses
  - user-manual/learning/enrollments
  - user-manual/learning/learner-pages
---

The **Learning** module (`CrestApps.OrchardCore.Learning`, with `Learning.Abstractions` and `Learning.Core`) turns Orchard Core content into courses that people enroll in, work through and complete. A course and its lessons are content items, so they get versions, localization, the media library and every content feature for free; Learning adds what content does not have: who may open a lesson, what a learner has done, and when a course counts as complete.

The admin screens and learner pages are described in the User Manual: [Courses](../user-manual/learning/courses.md), [Enrollments](../user-manual/learning/enrollments.md) and [Learner pages](../user-manual/learning/learner-pages.md).

## Features

| Feature | Id | Depends on |
| --- | --- | --- |
| Learning | `CrestApps.OrchardCore.Learning` | Contents, Lists, Title, Html, Autoroute, Media, Content Fields, Roles, CrestApps Users, CrestApps Resources |
| Learner Pages | `CrestApps.OrchardCore.Learning.LearnerPages` | Learning |
| Self Enrollment | `CrestApps.OrchardCore.Learning.SelfEnrollment` | Learner Pages |

It works with other features when they are on:

| Feature on | What it adds |
| --- | --- |
| Secure Media | Files attached to a lesson are served only to people who may open the lesson. |
| Workflows | Eight events and six tasks (see below). |
| Data Pipelines | The `Learning` data source and the **Enroll learners** destination step. |
| Reports | The Course completion, Learner transcript and Inactive learners reports. |
| Notifications | Enrolled, due soon and overdue notices, each a type a person can switch off. |
| Audit Trail | Changes people make to enrollments by hand, with their reason, and every export of learner records. |
| Webhooks | `learning.enrollment.created` and `learning.course.completed`. |
| Staff | The Instructor capability, a **Learning** section on the staff profile and **My learning** on a staff member's page. |
| Commerce | **My learning** in the storefront account menu (otherwise it is in the user menu). |
| Personal Data | Export and erasure of a person's enrollments and progress. |
| Recipes, Deployment | The `LearningEnrollments` recipe step and deployment step, with their schemas. |

## Content types

The migration creates these types. Each lesson type has the **Course item** part; change its settings to set the kind and the default way an item is completed.

| Type | Parts and fields | Kind |
| --- | --- | --- |
| `Course` | Title, Autoroute (`courses/{title}`), Html body, `Cover` media field, **Course**, **Course outline**, List (of the lesson types) | |
| `PageLesson` | Title, Html body, `Attachments` media field | Page |
| `VideoLesson` | Title, Html body, `Video` (.mp4, .webm) and `Captions` (.vtt) media fields, `YouTube` field, **Video lesson** | Video |
| `FileLesson` | Title, Html body, `Files` media field | File |
| `LinkLesson` | Title, Html body, `Url` link field | Link |
| `Acknowledgement` | Title, Html body, **Acknowledgement** (the statement to confirm) | Acknowledgement |

Lessons are contained in their course through Orchard's List part, so **New** on the course's list creates a lesson in it. The **Course outline** card orders them into sections; an item that is in the course but not in a section is listed under the outline as not yet placed.

### Course part

`Code`, `Summary`, `Level`, `EstimatedMinutes`, `EnrollmentMode` (`Closed`, `Open`, `Key`, `Approval`), the enrollment key (stored as a salted hash, never shown again), `AccessDays` (how long an enrollment gives access; empty for no end), `PassMark`, `Prerequisites` (other courses to complete first), `CompletionCriteria` and `Staff` (a user and a role: `Instructor`, `Assistant`, `Grader` or `Observer`).

### Course item part

`CompletionMode` (`Viewed`, `MarkedDone`, `MinimumTime`, `Watched`, `Acknowledged`), `MinimumMinutes`, `EstimatedMinutes`, `Gradable`, `Weight`, `PassMark`, `FreePreview` (anyone may open it), `AllowSelfReported` and `ReleaseRules`.

## Records

All live in the `Learning` YesSql collection.

- **`Enrollment`**: `UserId`, `CourseId`, `OfferingId`, `Status`, `Number` (`E000001`), `Cycle` and `PreviousEnrollmentId` (a person who takes a course again gets a new enrollment and the old one stays), `StartedUtc`, `AccessEndsUtc`, `DueUtc`, `CompletedUtc`, `LastActivityUtc`, `ProgressPercent`, `Grade`, `Passed`, `Grants`, `History` (every status change with who and why) and `Completion` (a snapshot taken at completion: the course title and version, the items and how each was completed, so the record still reads correctly after the course changes).
- **`EnrollmentGrant`**: why a person has access, `Source` and `SourceId` (`Manual`, `Self`, `Key`, `Pipeline`, `Workflow`, or a source another module adds). Revoking the last grant withdraws the enrollment.
- **`EnrollmentProgress`**: one document per enrollment with an entry per course item: state, first and last access, seconds spent, share watched, score, attempts, when it was completed, by whom and why, and the statement confirmed for an acknowledgement.

Each has its own index table (`EnrollmentIndex`, `EnrollmentGrantIndex`, `EnrollmentProgressIndex`, `CourseItemProgressIndex`); course staff are indexed in `CourseStaffIndex`.

## Enrollment states

| From | To |
| --- | --- |
| Pending | Active, Withdrawn (rejected) |
| Active | Completed, Suspended, Expired, Withdrawn |
| Suspended | Active, Withdrawn |
| Completed, Expired, Withdrawn | (a new enrollment, the next cycle) |

`IEnrollmentService` does every change under a distributed lock per person and course. Completing by hand, suspending, withdrawing and changing dates need a reason, which is kept in the history and the Audit Trail. A person has at most one open enrollment per course and offering, so enrolling twice from the same source changes nothing.

## Progress and completion

`ILearnerProgressService` records what a learner does: opening an item, time spent (a heartbeat from the player, at most 60 seconds a call), the share of a video watched, marking an item done and confirming an acknowledgement. A staff member with the right to manage enrollments can complete an item by hand with a reason.

An item is complete when its completion mode is met. The course is complete when every required item is complete and every completion criterion passes: `MinimumGrade`, `MinimumTime`, `ItemPassed` and `StaffApproval`. Another module adds a criterion with `AddCourseCompletionCriterion<T>()`.

**Release rules** decide when an item opens: `ItemCompleted` (after another item), `Date`, `DaysAfterEnrollment`. Add one with `AddCourseItemReleaseRule<T>()`.

### Writes, locks and commits

Every change to an enrollment or its progress runs under one distributed lock per enrollment (`EnrollmentWriteGate`): the enrollment is read again inside the lock, the session is committed before the lock is let go, and the enrollment observers run after the commit, only for what the call changed. Enrolling takes a lock per learner and course and commits the new enrollment inside it, so two requests never create two enrollments; adding a grant to an open enrollment goes through the enrollment's own lock. The daily upkeep raises its notices the same way. A call that cannot get the lock in time returns `Busy` and changes nothing.

The commit is the whole session's, not only the enrollment's. Code that calls the enrollment or progress services in the middle of a larger unit of work should finish its own writes first, or accept that they are committed with the enrollment:

| Caller | Effect of the commit |
| --- | --- |
| Player, learner pages, enrollment admin | The request has no other pending writes. |
| Workflow tasks (Enroll user, Approve, Reject, Change status, Extend, Mark item complete) | Writes made earlier in the same workflow run are committed with the enrollment. A later failure in the run does not undo them. |
| **Enroll learners** pipeline step | Each row is committed as it is enrolled, so a run that fails part way keeps the rows already done; the step reports each row's result. |
| `LearningEnrollments` recipe step | Does not use the gate; the recipe's own transaction applies. |
| An order or payment handler (another module) | Enroll after the order itself is saved, never inside the order's own save. |

Observers that send something out (webhooks, notifications, workflow events) queue their work as deferred tasks, so it runs after the request. They log and swallow their own failures: an event can be lost after a committed change. A consumer that must not miss one, such as certificate issue, must be idempotent and have a catch-up that finds what was missed.

## Who may open what

Two authorization handlers keep lessons from people who should not see them:

- The **content access handler** answers Orchard's `ViewContent` for course items: allowed for course staff, people who manage courses, free previews, and learners with an active or completed enrollment whose item is released; refused for everyone else. Because the display route, the content API, GraphQL, the content data source and Secure Media all ask `ViewContent`, they all follow it.
- The **record handler** answers for enrollments and progress: a learner sees their own, a manager sees everyone's, a course's staff see their course's.

A course's Instructors and Assistants who hold **Manage own courses** may edit, publish and preview that course and its lessons.

### Pages that do not ask for each item

Some Orchard Core features list or read content without asking `ViewContent` for each item. Learning keeps lesson bodies out of them:

| Path | What a person who is not enrolled gets |
| --- | --- |
| Search results | The lesson's title and link only. Course items render without their body, files, video or link in the `Summary` display, for every content type with the course item part. The **Course catalog** recipe indexes courses only, so lessons are not searchable unless a site adds them to an index. |
| RSS feed of a list | Nothing for a course: Learning turns off the List part feed of courses, because that feed writes the body of each contained item. |
| Queries API | Nothing: a query answers only people granted its own permission, and no Learning role holds one. A query you add that returns lessons hands their full documents to whoever you grant it to. |
| Sitemaps | A lesson's address and title, only if a sitemap is set to list the lesson types. The lesson page itself still refuses people who are not enrolled. |
| Liquid templates | Whatever the template reads. Templates run with the trust of the person who wrote them; a template that loads a lesson by id prints it to everyone who sees the page. |

### Lesson files

Turn on **Secure Media** (the **Learning setup** recipe does). A file attached to a lesson through a media field is stored under `mediafields/{type}/{contentItemId}/`, and each request for it asks `ViewContent` on the lesson, so a person who is not enrolled gets 404.

Secure Media protects the rest of the media library too: files outside lesson folders then need **View media content in the root folder** or a folder's own permission. The **Learning public media** recipe gives the root folder to everyone. Never give **View media content in all folders** to the Anonymous or Authenticated role: a role that holds it passes Secure Media before the folder check runs, which opens every lesson file. The recipe removes it from both roles.

## Learner pages

With **Learner Pages** on:

| Page | URL |
| --- | --- |
| My learning | `/learning` |
| Transcript | `/learning/transcript` |
| Player | `/learning/courses/{courseId}/play/{itemId}` |

The course page itself is the course's content page, with the outline and an **Enroll** or **Continue** button. The player tracks time and video watching with the `learning-player` script and refuses progress for an enrollment that is not the signed-in person's (404).

The **catalog** is Orchard Core search: the **Learning course catalog** recipe creates a Lucene index named `courses` of published courses, searchable by everyone at `/search/courses`.

## Permissions

| Permission | Administrator | Default |
| --- | --- | --- |
| Manage courses | Yes | |
| Manage own courses (as course staff) | Yes | |
| Manage enrollments | Yes | |
| View enrollments | Yes | |
| View learner records | Yes | |
| View learning reports | Yes | |
| Export learner records | Yes | |
| Access learning | Yes | Authenticated |

The **Learning roles** recipe creates Learning Administrator, Course Author, Instructor, Grader, Training Manager and Learner roles.

## Workflows

Events: Enrollment Requested, Learner Enrolled, Enrollment Status Changed, Course Started, Course Item Completed, Course Completed, Access Expiring, Learner Inactive. Each can be limited to some courses, and ignores changes made by a workflow unless told otherwise, so a workflow does not trigger itself.

Tasks: Enroll User, Approve Enrollment, Reject Enrollment, Change Enrollment Status, Extend Access or Due Date, Mark Course Item Complete.

## Data source and reports

The `Learning` data source (with Data Pipelines on) has four data sets: `LearningEnrollments`, `LearningItemProgress`, `LearningCompletions` and `LearningCourseStaff`. Their field names never change, so reports built on them keep working.

The **Enroll learners** pipeline step (`LearningEnrollLearners`) enrolls the user named by a column in a course, with a grant from the pipeline so a second run changes nothing; rows that fail go to its rejected output.

Coded reports, under **Reports**, need **View learning reports**; exporting one needs **Export learner records**:

- **Course completion** (`learning-course-completion`): per course, people who enrolled in the period, in progress, completed, expired or withdrawn, the completion rate and the average grade.
- **Learner transcript** (`learning-transcript`): completions in the period, for one learner (by user name) or everyone.
- **Inactive learners** (`learning-inactive-learners`): active enrollments with no activity for longer than the inactive period.

## Recipe and deployment step

The `LearningEnrollments` step moves enrollments with their progress. People are written by user name, so a recipe works on another site; a row whose user is missing is reported and skipped. Enrollments keep their ids, so running a recipe again updates them.

```json
{
  "name": "LearningEnrollments",
  "Enrollments": [
    {
      "ItemId": "4pj234zj1ypsnvynfa931j3gmx",
      "UserName": "learner1",
      "CourseId": "4pwq1bb2380et59270b871984s",
      "Status": "Active",
      "Number": "E000001",
      "Grants": [ { "Source": "Manual", "SourceId": "admin" } ]
    }
  ]
}
```

The deployment step exports one course or every course, created within a date range, with or without progress, and needs **Export learner records**. Each export is written to the Audit Trail.

## Configuration

The daily upkeep reads `OrchardCore:CrestApps:Learning:Upkeep`:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Learning": {
        "Upkeep": {
          "AccessEndingNoticeDays": 7,
          "InactiveDays": 14,
          "DueSoonDays": 3
        }
      }
    }
  }
}
```

Zero turns a notice off. The upkeep runs every hour, expires enrollments whose access ended, and raises each notice once per enrollment.

## Extending Learning

- `AddCourseItemKind<TPart>(name, ...)` adds a kind of course item.
- `AddEnrollmentSource(name, ...)` adds a grant source, such as an order.
- `AddCourseItemReleaseRule<T>()` and `AddCourseCompletionCriterion<T>()` add rules.
- `AddEnrollmentObserver<T>()` hears about every enrollment change after it is saved.
- `AddCourseItemHandler<T>()` hears about item events.
