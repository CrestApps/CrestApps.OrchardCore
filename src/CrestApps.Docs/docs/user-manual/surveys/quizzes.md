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
| **Menu** | **Surveys** → **Surveys**, then **Design** |
| **Permission** | Manage own surveys, or Manage all surveys |
| **Feature** | Surveys |

<AskYourAdmin />

## Turn on scoring

1. Create the survey from the **Quiz** template, or open **Survey settings** → **Scoring** and tick **Score responses**.
2. Set the **Pass mark**, the **Default points**, and **Take points away for wrong choices** if you want penalties.
3. Choose when **Answers are checked**: **At the end**, or **One at a time, then locked**.
4. Save the draft.

With **One at a time, then locked**, a respondent can't change an answer once they check it. They also can't change another answer in a way that would hide a checked answer or skip its page: for example, going back and picking the other track of a quiz after checking answers on the first one. The page then says "You can't change this answer now" on the answer they changed, and nothing is saved until they put it back. This stops a respondent from checking answers on one track and then finishing another to add both.

Your own logic can still hide a checked answer without the respondent doing anything, for example a rule on the date or time. That answer keeps counting toward the score, because the respondent already saw it graded. Its points count toward the most points too, so the percent stays right, and the review and the grading page show it. An answer that was not checked and is hidden later does not count.

The score counts only the pages the respondent went through: a page a branch skipped adds nothing to the most points. The score is worked out when the respondent finishes, before the ending is chosen, so an ending that shows for a score reads the score of this response.

## Set the right answers

In each question's **Scoring** card, set the **Points** (empty for the default; 0 for none), and the right answer: tick the right choices or give each choice its own points, list the **Accepted answers** of a text question, or set the **Right number** and **Tolerance**. **Give part of the points for partly right answers** gives partial credit. Add feedback **When right**, **When wrong** and **For any answer**.

A long text or file upload is scored by a person. Write **Guidance for graders**; respondents never see it. See [Grade responses](./grade-responses.md).

**Score categories** add up points by topic, and **Score bands** give a message by percent, such as *Excellent* from 90%.

## What respondents see

In **What respondents see**, choose what shows **While answering**, **Right after**, **Later, while open** and **After the survey closes**: their **Score**, **Their answers**, **Right answers**, **Answer feedback** and **General feedback**. A response that waits for a grader shows no score until it is graded.

If respondents may change a finished response (**Navigation**), they can't see their score, the right answers or answer feedback **Right after** or **Later, while open**: they could try one answer after another until the score goes up. Show those **After the survey closes**, or turn off changing a finished response. Once the survey closes, on its closing date or when you close it, nobody can change a finished response any more, and answers that were still in progress are submitted as they were saved. A learner whose accommodation closes later can still answer after that, while the others may already see the right answers, so set the closing date after the last accommodation when that matters.

## Time and attempts

- **Time limit**: **Time to finish** in minutes, and **Submit unfinished responses when time runs out**. A page can also have its own **Time on the page**. The time never runs past the survey's closing date (or the link's, when that comes first): a respondent who starts late gets the time left until then, and a last page sent just before the close still counts. A response submitted when time runs out, or when the survey closes, is scored on every page the respondent still had ahead of them, following the branches their answers chose: the pages they never reached score 0, so leaving after the first page doesn't pass the quiz.
- **Attempts**: in **Access and settings** → **Limits**, set **In total** and **Wait between responses**. A voided attempt stays used (and keeps its attempt number); a deleted one is given back.
- **Accommodations**: give a person extra time, extra attempts or another window. Open the survey's menu, select **Accommodations**, then **Grant accommodation**, and enter a **Reason**.
