---
sidebar_label: Import QTI questions
title: Import QTI Questions
description: "Import questions into a question bank from a QTI package or item, see which items were left out and why, and know where their images go."
technical_manual:
  - modules/surveys/index
---

QTI is the standard file format that many learning and testing tools use to exchange questions. You can import QTI 2.1 and QTI 3.0 questions into a [question bank](./question-banks.md), then draw them into surveys and quizzes.

| | |
| --- | --- |
| **Menu** | **Surveys** → **Question Banks**, then **Import** in a bank |
| **Permission** | Manage question banks |
| **Feature** | QTI Question Import |

<AskYourAdmin />

## Import a file

1. Go to **Surveys** → **Question Banks** and open the bank.
2. Select **Import**.
3. Under **Format**, pick **QTI package or item (zip or XML)**, and choose the **File**: a package (a `.zip` file with its list of contents) or a single question (an `.xml` file).
4. Select **Check the file**. The page says how many questions can be imported and lists the items that cannot, so nothing is added yet.
5. Select **Import**. The page says how many questions were added and how many were updated.

Importing the same package again into the same bank updates the questions it imported before, instead of adding them a second time. Choices keep matching the answers already given by their text, even when you reorder them or add one at the top. A choice you removed in an earlier import never comes back under another text. When a choice's text changed and the file gives it no identifier, it takes the place of the earlier choice at the same position, with its answers: the import lists each one under **Check these choices and rows**, so you can see whether it still means the same.

## Supported questions

| QTI question | Becomes |
| --- | --- |
| One right choice | **Single choice** |
| Several right choices | **Multiple choice** |
| True or false | **Yes or no** |
| A short typed answer | **Short text** |
| A number, with an allowed margin | **Number** |
| A written answer | **Long text**, scored by a grader |
| Prompts matched to answers | **Matching** |
| Items put in order | **Ranking** |
| Words typed into blanks | **Fill in the blanks** |
| Words dragged into blanks, or a word picked for each blank | **Choose the missing words** |
| Text with no answer | **Text block** |

Each question keeps its points, its feedback, and the feedback of each choice.

## Items that are left out

An item is left out when the bank couldn't keep it as it was written. The page lists each one with its name and the reason, for example:

- it changes while the learner answers, or its values are drawn when it is shown;
- it has no question, several questions of different kinds, or no right answer to score by;
- it is scored in a way the question type can't keep, such as penalties or different points for different answers;
- a choice, an answer or a blank holds an image, a formula or a table where the question keeps plain text;
- it holds math, audio, video or another object;
- it uses an image the package doesn't have, an image that isn't PNG, JPEG, GIF or WebP, or an image larger than the media library accepts.

Fix those items in the tool they came from, or add them by hand in the bank.

A file can be up to 50 MB. A package can list up to 5,000 questions, and each image can be up to 10 MB.

## Images

The images a question uses are copied into the site's **media library**, under `surveys/qti`. Importing the same image again, in the same package or another, uses the copy already there.

:::warning[Images are public]
Files in the media library can be opened by anyone who has their address. This includes images used only in feedback, which can show the right answer. Leave such images out of the package, or replace them after the import, for a graded quiz.
:::
