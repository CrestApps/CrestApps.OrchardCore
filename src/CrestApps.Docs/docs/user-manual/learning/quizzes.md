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
| **Permission** | Manage question banks to write questions; Manage courses or Manage the courses you teach to add the quiz |
| **Feature** | Learning Assessments |

<AskYourAdmin />

## Write questions

1. Go to **Learning > Question banks** and create a bank, such as *Fire safety*.
2. In the bank, add questions. Choose the type: single choice, multiple choice, true or false, short text, numeric, matching, ordering, fill in the blanks, essay, survey, drag into text, select missing words, or description.
3. Enter the **Prompt**, the **Points**, a **Difficulty** and **Tags** (random draws can pick by tag), the answer, and any feedback shown on review. An essay has word limits and **Grader guidance**, which learners never see.

Some types work differently:

- **Survey**: list the options. Every answer is accepted. Leave the points at 0 for an ungraded question, such as an opinion; with points, any answer earns them.
- **Drag into text** and **Select missing words**: write the text with the right word of each blank in double brackets, such as `Fire needs [[heat]] and [[fuel]].`, and list **Wrong words** (one a line) that are offered with the right ones. With drag into text, learners drag the words onto the blanks (or pick them in each blank's list); with select missing words, each blank is a drop-down list. Each blank earns its share of the points.
- **Description**: a text shown between questions, such as instructions or a case to read. It has nothing to answer, no points and no number.

To write many questions at once, use **Content transfer** with a spreadsheet: each row is a question, with a column for each field. For a choice question, list the options in one cell separated by `|` and start each right option with `*`, such as `Foam|*CO2|Water`. Write the pairs of a matching question as `France=Paris|Italy=Rome`, the items of an ordering question in their right order as `Plan|Do|Check|Act`, and the blanks of a fill in the blanks question in the text as `Water boils at [[100]] degrees`. A drag into text or select missing words question has a **Text** column written the same way and a **Distractors** column such as `water|sand`; a survey question has an **Options** column such as `Agree|Neutral|Disagree`. To put a `|` in a value, write `\|`. To import questions of several kinds from one sheet, add a **ContentType** column with the kind of each question, written as its name without spaces, such as MatchingQuestion for a **Matching question**, and choose the **Question bank** they go into on the import page.

## Import QTI questions

Questions written in another system can be imported when that system exports QTI 3.0 or QTI 2.1: a zip package or a single XML file. The **Learning QTI Import** feature must be on.

1. Go to **Bulk Import**, choose any question type, such as *Single choice question*, and upload the package. Each question becomes the kind that fits it: words to drag into gaps become drag into text, a list to pick from in each gap becomes select missing words, and an item with nothing to answer becomes a description.
2. Choose the **Question bank** the questions go into, then **Upload**.
3. When the import finishes, look at it in the import list. Items that could not be imported are counted as **Not imported from QTI**; download the failed rows from **Actions** to read why, such as a kind of question the site does not have (clicking a spot on a picture, or a slider) or a picture missing from the package.
4. Open the bank and check the questions. The editor shows the item and file each question came from.

Importing the same package (the same file name) into the same bank again updates the questions it made before instead of adding them twice. Without a bank, or with another file, the import always adds new questions. Images are copied to the media library, in a folder of the import under **qti-imports**; if your site uses Secure Media, ask your admin to let learners view that folder. A picture that only the feedback shows is kept apart, so learners see it only when their review shows the feedback. Choices and answers are plain text, so an item whose choices are pictures is not imported.

## Add a quiz to a course

1. Open the course outline and add a **Quiz**.
2. Choose fixed **Questions**, and, or instead, rules under **Draw at random**. Find fixed questions by typing part of their title in the picker's search box. New ones go after the others; under **Order**, move a question up or down with its arrows to set the order every attempt asks them in.
   - Select **Add rule** for each draw, and choose the **Question bank**, optionally a tag of that bank (the tag list shows the bank's own tags and can be searched) and a **Difficulty**, then **How many** questions to draw.
   - Beside the count, the editor shows how many questions of the bank match the rule. A rule that asks for more is refused when you save.
   - The trash button removes a rule. Each attempt draws its own questions, and a question is never drawn twice in one attempt.
3. Set the **Time limit**, when the quiz **Opens** and **Closes** (in your time zone), how many **Attempts** are allowed and the **Delay** between them, the **Pass mark** and how attempts combine: the highest, the latest or the average.
4. Choose the **Navigation**: free, or one after another (the learner answers a page, continues, and cannot go back or read ahead). Set **Questions per page**, such as 5; 0 shows every question on one page, or one question a page with one after another.
5. Choose the **Feedback**: after the attempt is submitted, or **immediately**: learners select **Check** under each question to see if they were right, and the answer then cannot change.
6. Choose when learners may **review** their answers: right away with the score only, once graded, once the quiz closes, or never. With **once the quiz closes**, moving **Closes** later or earlier moves the review of attempts already taken too, except for a learner whose override gave them their own close date.
   - For more control, as in Moodle, choose **What the table below allows at each moment** and tick, in the **Review options** card, what learners see (the score, the right answers, the feedback of their answers, the general feedback) during the attempt (for checked questions), right after the attempt (two minutes), later while the quiz is open, and after the quiz closes.
7. Optionally set an **Access code** learners type to start, such as a code given in the exam room, and an **Honor statement** they confirm. The code is kept only as a hash; after five wrong codes a learner waits 15 minutes.

## Taking a quiz

The learner opens the quiz in the course player and starts an attempt. Answers are saved as they go. With several pages, the learner moves with **Next page**, **Previous page** or the page numbers (or **Save and continue** with one after another) and submits on the last page. The time left is kept by the site, not the browser: when the time is up, the attempt is submitted on its own. Questions scored automatically show the result at once; an essay waits for a grader.

## Give a learner an override

From the quiz, open **Overrides** and add one for a learner: extra minutes, extra attempts, or another window, with the reason. The override is recorded in the Audit Trail.
