---
sidebar_label: Quizzes
title: Build a Quiz
description: "Score responses with points, right answers, partial credit and penalties, set a pass mark, a time limit and attempts, and choose what respondents see."
technical_manual:
  - modules/surveys/scoring-and-grading
---

A quiz is a survey whose answers earn points.

| | |
| --- | --- |
| **Menu** | Surveys > Surveys, then **Design** |
| **Permission** | Manage own surveys, or Manage all surveys |
| **Feature** | Surveys |

<AskYourAdmin />

## Turn on scoring

1. Create the survey from the **Quiz** template, or open **Survey settings > Scoring** and tick **Score responses**.
2. Set the **Pass mark**, the **Default points**, and **Take points away for wrong choices** if you want penalties.
3. Choose when **Answers are checked**: **At the end**, or **One at a time, then locked**.
4. Save the draft.

With **One at a time, then locked**, a respondent can't change an answer once they check it. That answer keeps counting toward the score even if a later answer hides its question or skips its page, because the respondent already saw it graded. Its points count toward the most points too, so the percent stays right, and the review and the grading page show it. An answer that was not checked and is hidden later does not count.

## Set the right answers

In each question's **Scoring** card, set the **Points** (empty for the default; 0 for none), and the right answer: tick the right choices or give each choice its own points, list the **Accepted answers** of a text question, or set the **Right number** and **Tolerance**. **Give part of the points for partly right answers** gives partial credit. Add feedback **When right**, **When wrong** and **For any answer**.

A long text or file upload is scored by a person. Write **Guidance for graders**; respondents never see it. See [Grade responses](./grade-responses.md).

**Score categories** add up points by topic, and **Score bands** give a message by percent, such as *Excellent* from 90%.

## What respondents see

In **What respondents see**, choose what shows **While answering**, **Right after**, **Later, while open** and **After the survey closes**: their **Score**, **Their answers**, **Right answers**, **Answer feedback** and **General feedback**. A response that waits for a grader shows no score until it is graded.

If respondents may change a finished response (**Navigation**), they can't see their score, the right answers or answer feedback **Right after** or **Later, while open**: they could try one answer after another until the score goes up. Show those **After the survey closes**, or turn off changing a finished response.

## Time and attempts

- **Time limit**: **Time to finish** in minutes, and **Submit unfinished responses when time runs out**. A page can also have its own **Time on the page**.
- **Attempts**: in **Access and settings > Limits**, set **In total** and **Wait between responses**.
- **Accommodations**: give a person extra time, extra attempts or another window. Open the survey's menu, select **Accommodations**, then **Grant accommodation**, and enter a **Reason**.
