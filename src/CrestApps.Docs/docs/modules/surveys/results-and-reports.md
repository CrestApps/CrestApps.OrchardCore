---
sidebar_label: Results and reports
title: Survey Results and Reports
description: "The answer index and SQL aggregates, the Surveys reports, their filters, minimum group size and exports."
user_manual:
  - user-manual/surveys/view-results
  - user-manual/surveys/export
---

## Answer index

When a response is completed (and is not a test), `SurveyAnswerIndex` gets one row per answer value: question, row, choice, number, date, text (cut to 255 characters), score and maximum points, with the completion time, collector and context kind. The rows come from each question type's `GetIndexRows`. `ISurveyAnswerAggregates` counts choices, computes number statistics (with a portable median) and reads text samples in SQL over these rows; reports never load every response.

## Reports

With the Reports feature on, these reports are under **Reports > Surveys**:

| Report | Name | Shows |
| --- | --- | --- |
| Survey Results | `survey-results` | Responses, completion rate, median duration, and a section per question from its type's analyzer (bars, tables, NPS and CSAT, matrix tables, text samples), optionally segmented by a choice question |
| Survey Responses | `survey-responses` | One row per response with a column per question across versions (newest names), to read or export to CSV or Excel |
| Quiz Results | `survey-scores` | Score spread, pass rate, average per category, each person's best and latest score |
| Item Analysis | `survey-item-analysis` | Difficulty and discrimination of each scored question |
| Completion Funnel | `survey-funnel` | How many reached and left each page, and the time spent there |
| Survey Participation | `survey-participation` | Who in a user audience completed an identified survey and who has not |
| Satisfaction Trend | `survey-trend` | The average answer of a rating, score or number question by day, week or month |
| Survey Activity | `survey-activity` | Completed responses per survey and the responses waiting for a grader; also the Survey Activity dashboard widget |

The filter (`ReportFilter.*` query keys) has the survey (`ReportFilter.SurveyId`), the versions (published, all, or one), the collector, the context kind, test responses, and the segment, trend or audience for the reports that use them. The survey list's **Results** action opens `/Admin/Reports/view/survey-results?ReportFilter.SurveyId={id}`.

Reports need View responses of all surveys or own surveys. Dates are grouped and shown in local time. The date range matches a finished response (completed or screened out) on when it finished and an unfinished one on when it started, in every report and in the answer aggregates.

### Who responded

One rule decides who may see respondent identity, everywhere (admin responses list and details, grading, reports, the API and the data sets), implemented once by `SurveyRespondentNames` in `Surveys.Core`:

- On an **anonymous** survey nobody is ever named, whatever the viewer holds; the name shows as *Anonymous*.
- On an **identified** survey, whoever may read the survey's responses (View responses, View own survey responses on their own surveys, or `ISurveyAuthorizationService` granting `ViewResponses`) or grade them sees who responded.
- Names are the user's display name (or user name), *Guest* for a response given without signing in, and *Deleted user* for a user who no longer exists.

Export survey responses with respondent identity still gates the deployment step that exports responses in bulk.

### Anonymous surveys

Every row-level output of an anonymous survey honors its minimum group size: the Survey Responses report and its CSV and Excel export, the admin responses list and a response's details page, the API's response list, and every data set. Nothing shows until the completed, non-test responses the narrowing filters select (versions, dates, collector, context, search) reach the minimum, whatever the status and test filters ask to list, so previews and unfinished responses can't pad a group (a response's details page needs the survey's completed responses to reach it). With no status filter, the admin responses list also leaves out each status with fewer non-test rows than the minimum, so a lone abandoned response is not listed among the completed ones. The public results page applies the minimum to the total and to each question, and the AI summary and `get_survey_results` use the site's floor too. Aggregate reports hide smaller groups and segments. Times of anonymous responses show the day only (data sets give the start of the day in the site's time zone), and the context of an anonymous response shows its kind only.

The minimum is the survey's own value, never below the site's floor (**Settings > Surveys > Smallest group size allowed**, 5 by default, at least 1), which also applies to surveys saved before the floor was raised.

### Survey Responses

- Lists **completed** responses unless the **Status** filter asks for completed or screened out, one status, or every status.
- Shows 100 rows a page; the **Page** filter picks the page. The total comes from a count query, and only the page's documents are read. The export reads every response a page of documents at a time; the Reports framework hands a whole document to the CSV and Excel writers, so the rows of an export are still held in memory.
- An unanswered multiple-choice question leaves its per-choice columns blank instead of *No*.
- Date and time answers are wall-clock values (stored without a time zone) and are shown as typed, never converted as if they were UTC.
- A note lists the questions that have no column because their question type is turned off.

### Color markers

Coded reports color cells with the public `ReportColumnColorizer` of `Reports.Core`, the same engine and `ReportColumnColors` model as the Report Builder: `ReportColumnColorizer.Create(DataFieldType, ReportColumnColors, values, today)` then `StyleOf(value)` per cell, and `HeaderStyleOf(colors)` for the header. The survey reports use built-in rules:

| Report | Column | Rule |
|---|---|---|
| Survey Responses | Score and Percent | red text below the pass mark |
| Survey Responses | Passed | red background on a failed response |
| Survey Responses | a Net Promoter Score question | 0 to 6 red, 7 and 8 amber, 9 and 10 green |
| Quiz Results | Best and Latest | red text below the pass mark |
| Item Analysis | Discrimination | amber below 0.2 |

Rules per question aren't stored with the question: the question model lives in `Surveys.Abstractions`, which can't depend on `Reports.Core` where `ReportColumnColors` lives. For custom rules, design a report on the survey's wide data set in the Report Builder, whose fields are typed and take color rules per field.
