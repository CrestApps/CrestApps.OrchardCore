---
sidebar_label: View results
title: View Results
description: "Read the responses and the survey reports: results per question, quiz results, item analysis, funnel, participation and trends."
technical_manual:
  - modules/surveys/results-and-reports
  - modules/surveys/workflows
---

Read each response, and the totals in the survey reports.

| | |
| --- | --- |
| **Menu** | Surveys > Surveys, then **Responses** or **Results** in the survey's menu; Reports > Surveys |
| **Permission** | View responses of own surveys, or View responses of all surveys |
| **Feature** | Surveys; Reports for the reports |

<AskYourAdmin />

## Responses

Select **Responses** in the survey's menu. Search by number and filter the list. Open a response to see its **Summary**, the **Answers** page by page, the **Score by category**, **Variables**, **Page visits** and **Score history**, in your time zone. On an anonymous survey the list stays empty until the responses you filtered reach the survey's minimum group size, a status that fewer responses than that share is left out of the list (a lone abandoned response, for example), a response opens only once the survey has that many completed responses, and times show the day only. **Void response** and **Delete response** need the Void and delete survey responses permission, or Void and delete responses of own surveys for your own surveys.

### Responses the hourly upkeep stopped trying

Every hour the site abandons unfinished responses left idle too long, submits responses whose time ran out, and deletes or anonymizes old responses as the survey's retention says. If this fails on a response five runs in a row, the site stops trying it. A run that works clears the count.

When that happens, a warning on the **Responses** list says how many responses it stopped trying, and those rows show **Upkeep stopped**. Select **Show them** to list only them (or pick **Upkeep stopped** in the filter). Once the cause is fixed, select **Try again**: the next hourly run tries those responses again. **Try again** needs the Void and delete survey responses permission, or Void and delete responses of own surveys for your own surveys.

## Reports

Select **Results** in the survey's menu, or go to **Reports > Surveys**:

| Report | Shows |
| --- | --- |
| **Survey Results** | How many responded and finished, how long it took, and the answers to each question, optionally compared across the choices of one question |
| **Survey Responses** | Every response with its answers, one row each |
| **Quiz Results** | How scores spread, how many passed, averages by category, each person's best and latest score |
| **Item Analysis** | How hard each quiz question is and how well it tells strong from weak respondents |
| **Completion Funnel** | How many reached and left each page, and how long they spent |
| **Survey Participation** | Who in a user audience completed an identified survey and who has not |
| **Satisfaction Trend** | The average answer to a rating, score or number question by day, week or month |
| **Survey Activity** | Completed responses per survey and the responses waiting for a grader |

Filter by survey, version, link, test responses and more. The dates you pick match when a response was finished, or when it started if it isn't finished yet.

**Survey Responses** lists completed responses unless you pick another **Status**, and shows 100 rows at a time: use **Page** to see the next ones, or export the report to get them all. Scores below the pass mark show in red, failed responses get a red **Passed** cell, and Net Promoter Score answers are red from 0 to 6, amber at 7 and 8 and green at 9 and 10. A note lists the questions left out because their question type is turned off.

### Who you see

- On an identified survey, anyone who may see a survey's responses, or grade them, sees who gave each one: their name, **Guest** when they didn't sign in, or **Deleted user** when the account is gone.
- On an anonymous survey nobody is named; responses show **Anonymous**.
- An anonymous survey shows nothing until enough responses match your filters: its minimum group size. Times show the day only.
