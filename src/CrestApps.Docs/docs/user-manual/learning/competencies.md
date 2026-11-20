---
sidebar_label: Competencies
title: Competencies
description: "Load competency frameworks, let a course or a learning path grant competencies at a level on completion, see who holds which competency, and let learners see their own."
technical_manual:
  - modules/learning-certifications
  - modules/skills
---

A **competency** is something a learner can show they can do, such as *Active listening* or *Safe lifting*. Competencies are the site's skills: the same list you keep under **Content > Skills**, held at a level from 1 (trainee) to 5 (expert). A **competency framework** is a skill category, so the competencies of one framework share a category.

A course or a learning path can grant competencies. When a learner completes it, they hold each competency at the level you chose.

| | |
| --- | --- |
| **Menu** | Content > Skills (the competencies, and **Import**); the **Competencies** card of a course or learning path; Reports > Skill holders; My competencies in Your account |
| **Permission** | Manage skills and certifications to add and import competencies; Edit the course to choose what it grants; View skills and certifications for the report; Access learning for My competencies |
| **Feature** | Learning Competencies (turns on Skills); Reports for the Skill holders report |

<AskYourAdmin />

## Load your competency frameworks

To bring frameworks over from Moodle:

1. In Moodle, open **Site administration > Competencies > Export competency framework**, choose a framework and save the CSV file. Do this once for each framework.
2. Here, open **Content > Skills** and click **Import**.
3. Choose the file and click **Import**.

The framework becomes the category, and each competency becomes a skill named by its short name, with its description as plain text. The page shows how many skills were added, updated and left out, and why.

You can also import your own file. It needs a **Name** column. The **Category**, **Description** and **Enabled** columns are optional.

:::tip[Importing again is safe]
A skill is matched by its name. A file imported a second time changes nothing that already matches. A name that another framework already holds is left out, because two skills can't share a name: rename one of them in Moodle or in the file, and import again.
:::

## Grant competencies on completion

1. Open the course, or the learning path, and find the **Competencies** card.
2. In **Competencies granted on completion**, pick the competencies. Type to search; they're listed by framework.
3. Choose the **Level granted**.
4. **Publish**.

When a learner completes the course or the path, they hold each competency at that level from the day they completed it. A learner who already holds a competency at that level or higher keeps their level; one who holds it lower moves up.

A completion grants nothing when it doesn't count as evidence: a failed course, a completion under review, or one that rests on a result the learner's browser reported (a learning package's own status, unless the item allows it). The same rule decides digital credentials and education credits.

:::note
A learning path gets the **Competencies** card when Learning Programs was on before Learning Competencies. Otherwise, add the **Competencies** part to the Learning Path content type under **Design > Content Types**.
:::

A Moodle learning-plan template lists one competency per course. Recreate it as a learning path with those courses, and either let each course grant its competency, or let the path grant them all when it's completed.

## See who holds which competency

Open **Reports > Skill holders**. Pick a **Category (framework)**, or leave **Every category**. The report shows:

- how many people hold a competency today, and how many competencies were given or changed in the chosen period;
- each competency with its number of holders and their average level;
- each person, the competency, its level, the day it started and who gave it. A competency granted by learning shows **Learning:** and the course or path.

Export the report to a spreadsheet with the export buttons.

## What learners see

Learners open **My competencies** in **Your account**. It lists the competencies they hold today, by framework, with the level and the day each started. Competencies given by hand show here too.
