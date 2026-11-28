---
sidebar_label: Courses
title: Courses
description: "Build a course from lessons: pages, videos, files, links and acknowledgements, ordered into sections, with enrollment rules, a pass mark, prerequisites, release rules and course staff."
technical_manual:
  - modules/learning
---

A course is a page on your site with lessons in it. Each lesson is a page, a video, a file, a link or a statement to confirm, and the course outline puts them in order, in sections.

| | |
| --- | --- |
| **Menu** | **Learning** → **Courses** |
| **Permission** | Manage courses; Manage the courses you teach for a course's instructors and assistants |
| **Feature** | Learning; Secure Media to protect lesson files |

<AskYourAdmin />

## Create a course

1. Go to **Learning** → **Courses** and create a **Course**.
2. Enter the **Title**, a description in the body and, if you like, a **Cover** picture.
3. On the **Course** card, set a **Course code** such as *SAFE-101*, a **Summary** shown in the catalog, a **Level** and the **Estimated effort (minutes)**.
4. Choose **How learners enroll**:
   - **Closed**: only staff enroll people.
   - **Open**: anyone signed in enrolls at once.
   - **Key**: people enter the **Enrollment key** you set. The key is stored only as a hash, so keep a copy to share; type a new key to replace it.
   - **Approval**: people ask, and a training manager approves or rejects them.
5. On the **Dates** card, set when the course **Starts** and **Ends**, in your time zone, or leave them empty. Before the start, learners see the course and may enroll, but cannot open its lessons; the course page tells them when it starts. The end date does not close the course, as in Moodle: learners keep their access, and the course is shown as ended (**Ended on**) on its card on **My learning**.
6. Set **Access length (days)** to end a self enrollment's access that many days after enrolling, or leave it empty for no end. Choose the **Prerequisite courses** people must have completed first: type part of a course's title to find it.
7. Under **Completion and grade**, set the **Pass mark (%)** of the course grade, then the rules a learner must meet besides completing every required item: a **Minimum course grade (%)**, a **Minimum time (minutes)**, a **Final assessment to pass**, or that a member of the course staff approves each completion. If nothing in the course gives a grade (no item counts toward the course grade, or every one that does is an anonymous survey), learners get no course grade: the minimum course grade is then met without a check, the pass mark decides nothing, and the editor shows a warning.
8. With **Course Categories** on, choose the course's **Category**. Categories come from the **Course categories** taxonomy under **Content** → **Taxonomies**, where you build the tree of categories and sub-categories. The list of courses filters by category with its **Course categories** filter.
9. Select **Publish**.

## Add lessons

Open the course and, in its list of lessons, create the kind of lesson you want:

| Lesson | What the learner does |
| --- | --- |
| Page | Reads it. |
| Video | Watches it. An uploaded video counts when most of it has been watched, 90% unless you change it. |
| File | Opens the attached files. |
| Link | Opens the link. |
| Acknowledgement | Reads the statement and confirms it. The confirmation is kept with the date. |

On the **Course item** card of a lesson:

- The completion setting says when the lesson counts as done: when it is opened, when the learner marks it done, after a **Minimum time (minutes)**, when the video is watched, or when the statement is confirmed.
- **Free preview** lets anyone open the lesson and its files, enrolled or not.
- **Counts toward the course grade** with a **Weight** and **Pass mark (%)** for a graded item.
- **When it opens** holds the lesson back: **Opens on** a date, **Opens days after enrollment**, or **After completing** another lesson, optionally **With a score of at least (%)**.


## Take the lessons in order

By default, learners open the lessons in any order. To make them follow the outline, tick **Learners take the items in order** on the course's **Order of the items** card. Then each required lesson opens only once every required lesson before it in the outline is complete. Optional lessons never hold a learner back, and open once the lessons before them are complete.

Learners see a lock and *Complete "Lesson name" first* beside each lesson that is still locked, in the player and on the course page. **Next** still moves to the next lesson; a locked lesson explains what to finish and has a button to go to it.

The setting is a shortcut for an **After completing** rule on every lesson. Use the lesson's own **When it opens** rules for anything else, such as one lesson that waits for a score on another. Both apply together.

## Grade categories

The course grade is the weighted mean of the graded lessons. To group them, such as homework and exams, add rows to the **Grade categories** card: a **Category** name, its **Weight** and how many of its lowest scores to **Drop lowest**. A category's score is the weighted mean of its lessons after the lowest are dropped, and the course grade is the weighted mean of the categories. Choose each graded lesson's **Grade category** on its **Course item** card. A graded lesson without a category counts as a category of its own, weighted by its lesson weight. Rows without a name are left out. An anonymous quiz or survey has no score, so once the learner finishes it, it is left out of the course grade and the other lessons are weighed without it.

Leave **Pass mark (%)** empty for a course without a grade.

## Order the outline

Save the course first, then add lessons with its list. On the **Outline** card, enter a **New section** title, then give each lesson a **Section** and a **Position in the section**. Untick **Required for completion** for a lesson that does not count toward completing the course. Lessons that are in the course but **Not in the outline** are listed so you can place them. Clear a section title to remove it; its lessons leave the outline but stay in the course. Learners see only published lessons.

## Course staff

On the **Course staff** card, choose people for each role:

- **Instructors** and **Assistants** edit and publish this course and its lessons, when they also have **Manage the courses you teach**, and see its learners' records.
- **Graders** mark work. They approve completions only when they also have **Manage enrollments**.
- **Observers** see the course and its learners' progress without changing anything. Like graders, they approve completions only when they also have **Manage enrollments**.

## Lesson files

Learners see each file under the name it had when you uploaded it. How large a file can be, and which file types are taken, is set by your administrator for the whole media library (30 MB and the usual Office, PDF, picture, audio and video files unless they raise it for training videos, OpenDocument spreadsheets, or caption files). Files attached to a lesson are shown only to people who may open the lesson; someone who is not enrolled who opens the address of the file gets *Not found*. Your administrator turns this on with the **Learning setup** recipe and, if the rest of the media library must stay public, also runs **Learning public media**.

## The catalog

People find courses by searching at */search/courses* once your administrator has run the **Learning course catalog** recipe. Only published courses are listed.

## Text between lessons

Add a **Text** item to a course for text, pictures or a video that learners read in place, such as an introduction to a section. It shows in the course outline and in the player above the next lesson, and it is never completed, graded or required, so it never holds back a learner's progress.

## Who approves completion, and courses that never complete

Under **Completion and grade**, after ticking that a member of the course staff approves each completion, choose **Who approves**: anyone who manages the enrollments, or only the course's instructors, assistants, graders or observers. Approving always needs **Manage enrollments** for the course. Instructors and assistants have it for their own course when they have **Manage the courses you teach**; a grader or an observer approves only when a role also gives them **Manage enrollments**. Tick **No completion criteria: the course never completes** for a course people read without finishing it; their progress is still shown.

## Open an item only to some people

On a lesson's **Who can open it** card, choose **People** or an **Audience** (from **Content** → **User Audiences**, for example everyone of a team or with a profile value). Only those people, and the course staff, open the item; others see that it is open only to some learners.

