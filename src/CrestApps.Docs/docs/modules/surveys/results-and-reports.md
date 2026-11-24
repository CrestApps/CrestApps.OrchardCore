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

With the Reports feature on, these reports are under **Reports** → **Surveys**:

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

Every row-level output of an anonymous survey follows one small-group rule (`SurveyGroupGate` in `CrestApps.OrchardCore.Surveys.Core.Access`): the Survey Responses report and its CSV and Excel export, the admin responses list, a response's details page, the API's response list, every data set, the grading queue and the Grade page.

- Nothing shows until the completed, non-test responses the narrowing filters select (versions, dates, collector, context, search) reach the minimum, so previews and unfinished responses can't pad a group.
- Each status whose non-test responses are fewer than the minimum is left out, so a lone abandoned, screened-out or in-progress response is never listed next to the completed ones. A status filter (or **Finished**) whose statuses are all left out shows nothing. Test responses are staff previews and are never left out on their own.
- A response's details page and Grade page (and the AI scoring assistant) open a real response only when its status passes the same rule over the whole survey and its joint key (below) isn't hidden (`SurveyAnonymousGroups.ShowsAsync`). Both refusals give the same notice, which doesn't say which group is small.
- The collector, the version and the context kind each row prints are one group, decided on their joint key (`SurveyResponseGroup`, `SurveyAnonymousAttributes`), counted on all of the survey's completed, non-test responses: deciding them one at a time would let a filter on one and a read of another single out a response (collector A and version 2 each large, but only one person answered version 2 through A). When fewer than the minimum share a response's key, all three show as *Other* (the version is left blank), and when all such smaller groups hold fewer than the minimum together, their responses aren't listed at all and their details and Grade pages don't open: a link sent to one person, or a version one person answered through a link, can't point to their row, by itself or by subtracting a filtered list from the whole. The hidden groups leave the query itself (`SurveyAnonymousGroups.HiddenGroupsAsync`, `SurveyResponseGroupFilter`), so the admin list, both grading queues (Surveys and Learning), the API and the Survey Responses report leave them out of their counts and pages too, and the status counts of the rule above leave them out as well. When a filter names a collector, a version or a context kind (the admin list's **Collector** and **Version** filters, the Survey Responses report's collector, version and context kind filters, including its default of the published version), every group that doesn't show as itself leaves the query, masked as *Other* or hidden (`SurveyAnonymousGroups.ConcealedGroupsAsync`): otherwise the filter would tell a masked row's real collector or version (filter on collector A, and the one row with a blank version is the one person who answered version 2 through A). A masked row prints *Other* even where it has no collector or context, so its real "none" doesn't set it apart. Details, the report, the data sets, the API, the queues, and the webhook and workflow payloads all use this one rule. Anonymous rows carry no attempt number, which would tie a person's responses together, and the admin list and the report show how long a response took in whole minutes.
- Data sets count the responses that pass the conditions Report Builder passes down to them (equals and lists, text matches, number and date ranges, not empty), so a filter on an answer or on the status can't narrow a released survey to one person. Conditions on fields a data set doesn't know count nothing. Report Builder shares a read of a Surveys data set between the views of one report only when their filters are the same (`IReportSharedReadPolicy`), so a second view or a dashboard click with more filters is read, and counted, on its own filters. Report Builder applies some filters only after reading (such as "not equal"), and a view can group rows by any field, so a viewer with **View responses** on an anonymous survey can still build small groups there: grant it, and the data sets, only to people trusted with the raw answers.
- Response numbers come from one sequence for the whole site, so an anonymous response takes no number (`SurveyResponse.Number` is empty; an import drops one from the file; responses started before this release keep theirs) and shows a code instead: an HMAC of its id under the survey's anonymity secret (`SurveyAnonymity.ResponseLabel`), which orders nothing and can't be worked out from the id in the response's links (and so in the server's access logs). Lists, the report, the API and the grading queue of an anonymous survey are in random-id order, not start order, and the list's search matches the whole code. Resetting the survey's anonymity secret changes its codes. The audit trail records the code. The public results page applies the minimum to the total and to each question, and the AI summary and `get_survey_results` use the site's floor too. Aggregate reports hide smaller groups and segments. Times of anonymous responses show the day only, and the context of an anonymous response shows its kind only.
- The day of an anonymous response is always its day in the **site's** time zone (`SurveyAnonymousDays`: the site's setting, else the server's), never the viewer's own: with **OrchardCore.Users.TimeZone** on, a viewer who could step their own zone would watch where a response's day flips and learn its hour. One definition serves every output: the data sets, the API, the admin pages (the list, Details, Grade, the grading queue, which render the date as preformatted text so the `DateTime` shape doesn't convert it again), the Survey Responses report, the trend report's buckets, the export, and the webhook and workflow payloads.
- A date filter on an anonymous survey selects whole days in the site's time zone (`SurveyAnonymity.WholeDays`): the Survey Responses report and the other survey reports widen the filter before they query, the activity report does when any survey it lists is anonymous, and the data sets widen the range they read. Stepping a filter's time of day minute by minute can't tell when a response came in.
- Data sets read an anonymous survey apart from the other rows: first its count, over all of its responses that pass the conditions other than dates (an anonymous row's dates show the day only, and a pipeline's watermark must not narrow the count to the few rows changed since its last run), then its rows a page at a time in the order of their response ids (keyset paging, so no row is held in memory and a row's place says nothing of when it came in), written one for one between the other rows so a capped read or a preview still shows some. A row is released once the day it last changed on has ended in the site's time zone and once at least the minimum of the survey's counted completed responses have ended theirs; its **Modified** field reads that moment (the end of its day, or the moment the survey was released, if later). An incremental read (a pipeline that keeps the rows changed after the largest **Modified** it read) never skips an anonymous row this way, however few responses a survey gets a day, and gets a survey's earlier rows once it reaches its minimum; read anonymous surveys incrementally by **Modified**, not by **Completed**, whose day-only value can fall before what the pipeline already read. A condition on **Modified** is read a day further back, so "Modified yesterday" finds the rows shown as changed yesterday. Grading or regrading a response changes its **Modified**, so it waits until that day ends again, but it still counts toward the group. Today's anonymous responses show in the data sets tomorrow; the Surveys reports show them the same day.
- The response deployment export (the SurveyResponses recipe step) writes an anonymous survey's responses after the others, in the order of their ids, with every time cut to the start of the site's day (in UTC) and no time on its answers (`AnsweredUtc`, `CheckedUtc`), and leaves out the number. It never writes an anonymous survey's in-progress responses, even with **Include responses in progress**: cut to the day and without the time each answer was checked, a timed response would look expired or idle on import and its locked answers would unlock. The step's dates are days of the site's time zone, and the audit entry records which surveys and days were exported.
- Webhook payloads and workflow input of an anonymous response leave out the number and the attempt number, give its times by the start of the site's day, and leave out its collector, version and context when its joint key is in a group too small to show (so a workflow event limited to a link sent to one person doesn't fire for them). They are sent when the response changes, so whoever receives them must not record when they arrived. Learning's course item completion (and its CourseItemCompleted event) is recorded for the learner when they submit an anonymous course survey, in the same request: Learning keeps who completed the item and when, by design, so the learner's progress records that they responded, though not what they answered.

The minimum is the survey's own value, never below the site's floor (**Settings** → **Surveys** → **Smallest group size allowed**, 5 by default, at least 1), which also applies to surveys saved before the floor was raised.

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
