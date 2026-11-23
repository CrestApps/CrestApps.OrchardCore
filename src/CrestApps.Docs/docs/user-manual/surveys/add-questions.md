---
sidebar_label: Add questions
title: Add Questions
description: "Use the designer: add pages and questions, choose the question type, write choices, and set a question's settings."
technical_manual:
  - modules/surveys/designing
  - modules/surveys/controls
---

The designer shows the pages and questions on the left and the editor on the right.

| | |
| --- | --- |
| **Menu** | Surveys > Surveys, then **Design** |
| **Permission** | Manage own surveys, or Manage all surveys |
| **Feature** | Surveys |

<AskYourAdmin />

## Add pages and questions

1. Select **Add page** to add a page.
2. Select the plus button of a page (**Add a question to this page**) and choose a question type.
3. Select a page or a question in **Pages and questions** to edit it on the right. **Save question** or **Save page** saves that item right away; **Save draft** at the bottom of the page saves it too.
4. Drag a question to move it (**Drag to move**), or use **Move up** and **Move down**. **Duplicate** copies it and **Delete** removes it.
5. Select **Save draft**. It saves the survey settings and the question or page open on the right. Respondents see nothing until you [publish](./publish-and-versions.md).

If you leave the designer, or open another question, while some changes are not saved, you are asked first.

## Question types

| Group | Types |
| --- | --- |
| Choice | Single choice, Multiple choice, Yes or no, Ranking |
| Text | Short text, Long text, Multiple text, Contact information |
| Number | Number, Slider, Constant sum |
| Rating | Rating, Net Promoter Score, Customer satisfaction |
| Grid | Matrix, Drop-down matrix |
| Date | Date and time |
| Content | Text block |
| Files | File upload (when attachments are turned on) |
| Quiz | Matching, Fill in the blanks, Choose the missing words |
| Advanced | Hidden value, Calculated value, Signature |

## The question editor

| Field | What it does |
| --- | --- |
| **Text** | The question. Write a code in braces to show the answer to an earlier question. |
| **Code** | Names the answer in exports, formulas and piping. |
| **Description**, **Placeholder** | Help shown with the question. |
| **An answer is required** | The respondent must answer. |
| **Keep its place when the page shuffles** | The question stays put when the page shows questions in a random order. |
| **Question pool** | Puts the question in a pool of the page. |
| **Shows**, **Required when**, **Can be answered when** | See [Logic](./logic.md). |

**Choices** lists the choices: their text, **Export value**, **Image path**, **Other with text**, **Clears the others**, **Pinned**, **Retired** and **Show this choice only when**. **Show the choices in a random order** shuffles them; **Carry forward from** reuses the choices of an earlier question (all, the ones picked, or the ones not picked).

**Validation** adds checks: a length, a number range, a pattern, an e-mail or web address, or a formula, each with an optional **Message**.

Each question type also has its own settings card, such as the lowest and highest value of a slider.

## Survey settings

Select **Survey settings** to change the whole survey: **General** (the title and introduction respondents see), **Presentation** (numbers, page titles, progress bar, **Questions per screen**, **Page order**), **Navigation** (going back, resuming, changing a finished response), **Variables**, **Endings**, **Score categories** and **Score bands**, **Scoring**, **Time limit** and **Quotas**.

A guest who answers without signing in comes back to a finished response in the same browser. While they may still change it, or see their answers later, the browser remembers the response for 30 days. Otherwise it remembers it for one day after the ending shows.
