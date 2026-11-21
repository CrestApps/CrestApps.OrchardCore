---
sidebar_label: Learning packages
title: Learning packages (SCORM and cmi5)
description: "Import SCORM and cmi5 packages made in authoring tools such as Articulate Rise and Storyline 360 into a course, and set up the package content host that packages in several frames need."
technical_manual:
  - modules/learning-packages
---

A learning package is a lesson made in an authoring tool, such as Articulate Rise, Articulate Storyline or Adobe Captivate, and exported as a SCORM 1.2, SCORM 2004 or cmi5 zip file. Learners open it in the course, and the score and status it reports count in their progress.

| | |
| --- | --- |
| **Menu** | A course's outline (add a **Learning package** item); Settings > Learning packages |
| **Permission** | Import learning packages (given with Manage courses) to import; Manage learning package settings to set the package content host |
| **Feature** | Learning SCORM Packages (and Learning cmi5 Packages for cmi5 courses) |

<AskYourAdmin />

## Import a package

1. Add a **Learning package** item to the course and save it.
2. In the item's editor, click **Import a package** and choose the zip file the authoring tool exported.
3. Click **Import**. The package goes into a draft of the item.
4. Publish the item to give the package to learners.

A package may hold pages, scripts, images, cursors, fonts, captions, audio, video, PDF and Office documents (Word, Excel and PowerPoint, without macros). A package with a program, a script a server runs or an Office file with macros is refused, and the message names the file.

## Packages from Articulate Rise and Storyline 360

Rise and Storyline 360 packages run in several frames that must talk to each other. On a site without a **package content host** they show a blank page. When you import one on such a site, the import page warns you, and the import page always says when no content host is set.

Your administrator sets the content host once:

1. Ask whoever runs your site to point a separate host name at the site, for example packages.example.com, with a certificate. It must not share the site's sign-in cookies; a different domain is best.
2. Go to **Settings > Learning packages**.
3. Enter the address in **Package content host** and click **Save**.

From then on, every package opens from that host, and that host shows nothing but package files. Packages that already worked keep working. To go back, empty the field and save.

## IMS content packages

An IMS content package (for example one exported from Moodle) is a zip file of web pages with a manifest but no SCORM tracking. Import it the same way. It plays in the course without reporting a score, and the lesson counts as done when the learner opens it.

## Window, attempts and score

On the package item's **How learners run it** card:

- tick **Open in a new window** to run the package in a window of its own; the course page shows the learner's progress when the window closes;
- set **Attempts allowed**; once a learner has used them, the package opens only to review;
- choose **Score from**: the best, average, first or last attempt, or the attempt in progress for a package that keeps one attempt that resumes.

## When the lesson counts as done

On the item's **Course item** card, choose when the package lesson counts as done:

| Choice | The lesson is done when |
| --- | --- |
| **When the package reports it complete** (the default) | The package says the learner completed it. |
| **When the learner passes** | The package says the learner passed. If it reports a fail, the lesson is failed. A package that reports no pass or fail is compared with the lesson's pass mark. |
| **When the score reaches the pass mark, whatever the package reports** | The score reaches the lesson's pass mark. Set the pass mark too. |
| **When the learner marks it done** | The learner selects **Mark as done** in the player. What the package reports does not complete it. |
| **When the learner opens it** | The learner opens it. An IMS content package gets this choice when you import it. |

Pick **When the learner marks it done** for a package that never reports that it is complete, such as one that ends without telling the course. Learners would otherwise never finish the lesson. The score the package reports still counts in the grade.

## What learners see

Learners open the package with **Launch** in the course player, and **Resume** after they leave a package that saves its place. The package reports its own score and status; the course marks such results as reported by the browser.
