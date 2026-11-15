---
sidebar_label: Quizzes
title: Quizzes
description: "Write questions in banks, add a quiz to a course with fixed and random questions, a time limit, attempts and a pass mark, and give a learner an override."
technical_manual:
  - modules/learning-assessments
---

A quiz is a course item with questions. Questions live in question banks, so the same question can serve many quizzes, and a quiz can draw questions at random from a bank.

| | |
| --- | --- |
| **Menu** | Learning > Question banks; a course's outline for the quiz |
| **Permission** | Manage question banks to write questions; Manage courses or Manage own courses to add the quiz |
| **Feature** | Learning Assessments |

<AskYourAdmin />

## Write questions

1. Go to **Learning > Question banks** and create a bank, such as *Fire safety*.
2. In the bank, add questions. Choose the type: single choice, multiple choice, true or false, short text, numeric, matching, ordering, fill in the blanks or essay.
3. Enter the **Prompt**, the **Points**, a **Difficulty** and **Tags** (random draws can pick by tag), the answer, and any feedback shown on review. An essay has word limits and **Grader guidance**, which learners never see.

To write many questions at once, use **Content transfer** with a spreadsheet: each row is a question, with a column for each field. For a choice question, list the options in one cell separated by `|` and start each right option with `*`, such as `Foam|*CO2|Water`.

## Add a quiz to a course

1. Open the course outline and add a **Quiz**.
2. Choose fixed **Questions** and, or instead, **Random draws**: how many questions to take from a bank, optionally only with a tag.
3. Set the **Time limit**, when the quiz **Opens** and **Closes** (in your time zone), how many **Attempts** are allowed and the **Delay** between them, the **Pass mark** and how attempts combine: the highest, the latest or the average.
4. Choose the **Navigation**: free, or one question at a time (the learner answers questions in order and cannot go back or read ahead).
5. Choose when learners may **review** their answers: right away with the score only, once graded, once the quiz closes, or never.
6. Optionally set an **Access code** learners type to start, such as a code given in the exam room, and an **Honor statement** they confirm. The code is kept only as a hash; after five wrong codes a learner waits 15 minutes.

## Taking a quiz

The learner opens the quiz in the course player and starts an attempt. Answers are saved as they go. The time left is kept by the site, not the browser: when the time is up, the attempt is submitted on its own. Questions scored automatically show the result at once; an essay waits for a grader.

## Give a learner an override

From the quiz, open **Overrides** and add one for a learner: extra minutes, extra attempts, or another window, with the reason. The override is recorded in the Audit Trail.
