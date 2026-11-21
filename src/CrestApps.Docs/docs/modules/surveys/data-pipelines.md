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
| `SurveyResponses_{surveyName}` | One per response of a published survey | Response fields (`Id`, `Number`, `VersionNumber`, `Status`, `StartedUtc`, `CompletedUtc`, `Score`, `Percent`, `Passed`, `ContextKind`, `ContextId`, `UserId`) and a column per question export column of every version, joined by question id and named after the newest version |

`SurveyResponses` reads by its modified time, so incremental pipeline runs only read what changed. The wide `SurveyResponses_{surveyName}` data sets are listed at run time, one per published survey: use them for "export my survey to Excel every Monday".

A person reads only the surveys whose responses they may view. Identity fields (respondent, user name, user id, invitation recipient and response) need Export survey responses with respondent identity, and are always empty for anonymous surveys.
