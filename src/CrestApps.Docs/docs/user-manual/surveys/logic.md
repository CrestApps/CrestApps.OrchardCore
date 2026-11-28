---
sidebar_label: Logic
title: Survey Logic
description: "Show pages, questions and choices only when earlier answers call for them, jump to a later page, end the survey, and use pools and random order."
technical_manual:
  - modules/surveys/logic
---

Logic shows each respondent only what applies to them.

| | |
| --- | --- |
| **Menu** | **Surveys** → **Surveys**, then **Design** |
| **Permission** | Manage own surveys, or Manage all surveys |
| **Feature** | Surveys |

<AskYourAdmin />

## Show a question only when it applies

1. In the question editor, under **Logic**, set **Shows** to **Only when** (or **Unless**).
2. Select **Add condition**, choose an earlier question, a comparison (such as **is**, **is one of**, **is greater than** or **is answered**) and a value.
3. Choose **All of these** or **Any of these**. **Add group** nests a group of conditions.
4. Save the question.

A rule may read only questions on the same page or earlier pages. When an earlier answer changes so that a question hides, its answer is removed.

**Required when** decides when an answer is required. **Can be answered when** decides when the question can be answered; otherwise it shows read-only. A choice has **Show this choice only when**, and a page has **When the page shows**.

## Jump to a later page or an ending

In the page editor, under **Branches**, select **Add branch**, set the rule and **Go to** a later page or an ending. When respondents leave the page, the first branch whose rule holds is used.

## Endings

In **Survey settings** → **Endings**, each ending has a **Title**, a **Message**, an optional **Redirect to**, an **Outcome** (**Completed** or **Screened out**) and **Use this ending when**. After the last page, the first ending whose rule holds is used, else the **Default ending**.

## Pools and random order

A page's **Question order** can be **Shuffled (pinned questions keep their place)**. A pool (**Add pool**) shows each respondent a number of its questions (**Questions drawn**), at random, and can draw from a [question bank](./question-banks.md). **Present evenly** shows the least-shown questions first. Each respondent keeps the same draw when they come back.

## Logic overview

Select **Logic overview** in the survey's menu to read the rules of the draft in plain words, with the **Problems to fix before publishing**.
