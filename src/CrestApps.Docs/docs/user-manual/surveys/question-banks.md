---
sidebar_label: Question banks
title: Question Banks
description: "Keep reusable questions in banks, import them from a spreadsheet or QTI, and draw them into surveys and quizzes."
technical_manual:
  - modules/surveys/versions
---

A question bank holds questions you reuse. A pool on a survey page can draw questions from a bank by tag and difficulty.

| | |
| --- | --- |
| **Menu** | **Surveys** → **Question Banks** |
| **Permission** | Manage question banks (View question banks to read them) |
| **Feature** | Surveys; QTI Question Import for QTI files |

<AskYourAdmin />

## Create a bank and add questions

1. Go to **Surveys** → **Question Banks** and select **Add question bank**. Enter a **Title**, a **Technical name**, a **Description** and **Tags**, then **Save**.
2. Open the bank and select **Add question**. Choose the type, write the question in the same editor as the designer, and set its **Tags**, **Difficulty** (1 to 5) and **Retired**.
3. Select **Save**. Each save makes a new revision.

Select questions in the list to retire, restore, move them (**Move selected questions to**) or delete them. A retired question is no longer drawn; published surveys keep the questions they drew.

## Import questions

1. In the bank, select **Import**.
2. Choose the **File** and its **Format**. A spreadsheet (CSV or Excel, up to 10 MB) has the columns Id, Type, Question, Choices, Correct, Points, Tags, Difficulty and Feedback.
3. Select **Check the file** to see what can be imported, how many questions it would add and update, and which choices or rows would take the place of earlier ones because their text changed. Then select **Import**. A question whose Id is already in the bank is updated.

For QTI files from other learning and testing tools, see [Import QTI questions](./qti-import.md).

## Draw from a bank

In the designer, open a page, select **Add pool**, choose the **Question bank**, the **Tags** and **Difficulty**, and the number of **Questions drawn**. The questions are copied into the survey when you publish. When a bank question changes, publish the survey again to use the change.
