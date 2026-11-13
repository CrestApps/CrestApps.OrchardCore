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
| **Menu** | Learning > Courses |
| **Permission** | Manage courses; Manage own courses for a course's instructors and assistants |
| **Feature** | Learning; Secure Media to protect lesson files |

<AskYourAdmin />

## Create a course

1. Go to **Learning > Courses** and create a **Course**.
2. Enter the **Title**, a description in the body and, if you like, a **Cover** picture.
3. On the **Course** card, set a **Course code** such as *SAFE-101*, a **Summary** shown in the catalog, a **Level** and the **Estimated effort (minutes)**.
4. Choose **How learners enroll**:
   - **Closed**: only staff enroll people.
   - **Open**: anyone signed in enrolls at once.
   - **Key**: people enter the **Enrollment key** you set. The key is stored only as a hash, so keep a copy to share; type a new key to replace it.
   - **Approval**: people ask, and a training manager approves or rejects them.
5. Set **Access length (days)** to end a self enrollment's access that many days after enrolling, or leave it empty for no end. Choose the **Prerequisite courses** people must have completed first.
6. Under **Completion and grade**, set the rules a learner must meet besides completing every required item: a **Minimum course grade (%)**, a **Minimum time (minutes)**, a **Final assessment to pass**, or that a member of the course staff approves each completion.
7. Select **Publish**.

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

## Order the outline

Save the course first, then add lessons with its list. On the **Outline** card, enter a **New section** title, then give each lesson a **Section** and a **Position in the section**. Untick **Required for completion** for a lesson that does not count toward completing the course. Lessons that are in the course but **Not in the outline** are listed so you can place them. Clear a section title to remove it; its lessons leave the outline but stay in the course. Learners see only published lessons.

## Course staff

On the **Course staff** card, choose people for each role:

- **Instructors** and **Assistants** edit and publish this course and its lessons, when they also have **Manage own courses**, and see its learners' records.
- **Graders** mark work and approve completions.
- **Observers** see the course and its learners' progress without changing anything.

## Lesson files

Files attached to a lesson are shown only to people who may open the lesson; someone who is not enrolled who opens the address of the file gets *Not found*. Your administrator turns this on with the **Learning setup** recipe and, if the rest of the media library must stay public, also runs **Learning public media**.

## The catalog

People find courses by searching at */search/courses* once your administrator has run the **Learning course catalog** recipe. Only published courses are listed.
