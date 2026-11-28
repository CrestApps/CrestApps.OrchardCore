---
sidebar_label: Survey templates
title: Survey Templates
description: "Add, change and remove the templates new surveys start from, and save a survey as a template."
technical_manual:
  - modules/surveys/designing
  - modules/surveys/recipes-and-deployment
---

# Survey Templates

A template is what a new survey starts from under **Start from**: its pages, questions, logic and settings. The site comes with a few templates, and you can add your own, change them or remove them.

| | |
| --- | --- |
| **Menu** | **Surveys** → **Templates** |
| **Permission** | Manage survey templates, or Manage all surveys |
| **Feature** | Surveys |

<AskYourAdmin />

## The templates that come with the site

When Surveys is turned on, the site gets **Customer satisfaction**, **Net Promoter Score**, **Employee engagement**, **Event feedback**, **Quiz** and **Poll**. When Learning surveys are turned on, the site also gets **Course feedback**, which feedback survey course items start from. **Blank survey** is not a template you manage: it is always offered.

You can change or remove any of these. A survey made from a template keeps its questions when the template changes or is removed.

## Save a survey as a template

1. Design a survey the way you want new surveys to start.
2. In **Surveys** → **Surveys**, open the row menu of the survey and select **Save as a template**.
3. Check the **Title**, **Description**, **Kind**, **Icon** and **Position**, then select **Save**.

The template copies the survey's draft. Surveys you create from it get their own copy, with new ids.

## Add or change a template

1. Go to **Surveys** → **Templates** and select **Add template**, or **Edit** on a row.
2. Enter a **Title** and, if you like, a **Technical name** (lowercase letters, digits and dashes) and a **Description**.
3. Pick the **Kind** (**Survey**, **Quiz** or **Poll**), an **Icon** (a Font Awesome class) and a **Position**: templates with a lower position come first.
4. For a new template, pick a survey under **Copy questions from**, or leave it empty to start with an empty page.
5. Select **Save**.

**Definition (JSON)** holds the template's questions as text, for people who copy templates between sites. To change the questions of a template, create a survey from it, design the survey, and save that survey as a template.

To remove a template, select **Delete** in its row menu.
