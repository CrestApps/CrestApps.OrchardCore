---
sidebar_label: Data sets
title: Survey Data Sets
description: "The Surveys data source for Data Pipelines and the Report Builder: its static data sets, one wide data set per survey, fields, access and identity rules."
user_manual:
  - user-manual/surveys/export
---

With Data Pipelines Core on, the **Surveys** data source (`Surveys`) offers these data sets to pipelines and the Report Builder. Data set and field names never change once shipped.

| Data set | Rows | Fields |
| --- | --- | --- |
| `Surveys` | One per survey | `Id`, `Name`, `Title`, `Kind`, `Status`, `Owner`, `PublishedVersion`, `Created`, `Modified` |
| `SurveyVersions` | One per published version | `Id`, `SurveyId`, `Number`, `PublishedUtc`, `PublishedBy`, `Notes` |
| `SurveyQuestions` | One per question per version | `SurveyId`, `VersionNumber`, `QuestionId`, `Name`, `ControlType`, `Title`, `Page`, `Position`, `Points`, `Category` |
| `SurveyResponses` | One per response | `Id`, `Number`, `SurveyId`, `SurveyName`, `VersionNumber`, `Status`, `Grading`, `Respondent`, `UserName`, `ContextKind`, `ContextId`, `Collector`, `Attempt`, `StartedUtc`, `CompletedUtc`, `DurationSeconds`, `Score`, `MaxScore`, `Percent`, `Passed`, `Ending`, `IsTest`, `ModifiedUtc`, plus the fields of every `ISurveyResponseFieldProvider` |
| `SurveyAnswers` | One per answer value of a completed response | `ResponseId`, `SurveyId`, `VersionNumber`, `QuestionId`, `QuestionName`, `RowId`, `ChoiceId`, `ChoiceValue`, `ChoiceText`, `NumberValue`, `DateValue`, `TextValue`, `Score`, `MaxPoints`, `CompletedUtc` |
| `SurveyCategoryScores` | One per score category of each completed response | `ResponseId`, `SurveyId`, `Category`, `Score` |
| `SurveyPageVisits` | One per page visit | `ResponseId`, `SurveyId`, `PageName`, `EnteredUtc`, `LeftUtc`, `Seconds` |
| `SurveyInvitations` | One per invitation (Invitations feature) | `Id`, `SurveyId`, `CollectorId`, `Recipient`, `Status`, `SentUtc`, `Reminders`, `ResponseId` |
| `SurveyResponses_{surveyName}` | One per response of a published survey | Response fields (`Id`, `Number`, `VersionNumber`, `Status`, `StartedUtc`, `CompletedUtc`, `Score`, `Percent`, `Passed`, `ContextKind`, `ContextId`, `UserId`, `Respondent`) and a column per question export column of every version, joined by question id and named after the newest version |

`SurveyResponses` reads by its modified time, so incremental pipeline runs only read what changed. The wide `SurveyResponses_{surveyName}` data sets are listed at run time, one per published survey: use them for "export my survey to Excel every Monday".

A person reads only the surveys whose responses they may view, and sees who responded on the identified ones ([who responded](./results-and-reports.md#who-responded)): identity fields (respondent, user name, user id, invitation recipient and response) are always empty for anonymous surveys. The wide data set also has a `Respondent` field with the display name (*Guest*, *Deleted user*, or *Anonymous* on an anonymous survey).

Rows of an anonymous survey follow the small-group rule of [Results and reports](./results-and-reports.md#anonymous-surveys): they wait until the completed responses reach the minimum group size, and each status waits until that many responses share it; a group that never does gives no rows. The check counts the responses that pass the conditions Report Builder passes down (the date range, keys, equals and lists, text matches, number and date ranges), so a filter on an answer can't narrow a survey to one person. Filters Report Builder applies only after reading, and groupings in a view, can still narrow further, which is why anonymous rows also carry no context id, no response number (the `Number` field holds the start of the random id) and show their times as the start of their day in the site's time zone.
