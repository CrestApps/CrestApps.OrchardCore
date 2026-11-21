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

Reports need View responses of all surveys or own surveys; respondent names need Export survey responses with respondent identity. Anonymous surveys with fewer responses than their minimum group size show nothing, and smaller segments are hidden. Dates are grouped and shown in local time.
