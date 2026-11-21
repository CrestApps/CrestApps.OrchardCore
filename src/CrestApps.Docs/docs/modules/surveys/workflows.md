---
sidebar_label: Workflows
title: Survey Workflows
description: "The Surveys workflow events and tasks, their filters, inputs and outcomes."
user_manual:
  - user-manual/surveys/view-results
---

With Workflows on, Surveys adds these activities. Events fire after the change is saved; test responses fire nothing. The correlation id is the response id (the survey id for survey events).

## Events

| Event | Fires when |
| --- | --- |
| Survey Response Started | A response starts |
| Survey Page Completed | A page is completed (filter by page) |
| Survey Response Completed | A response is completed |
| Survey Response Screened Out | A respondent is screened out (filter by ending) |
| Survey Response Abandoned | The maintenance task abandons a response |
| Survey Response Needs Grading | A completed response waits for a grader |
| Survey Response Graded | A response is fully graded |
| Survey Response Passed | A graded response reaches the pass mark |
| Survey Response Failed | A graded response misses the pass mark |
| Survey Response Score Changed | A regrade or a score change |
| Survey Response Matches Condition | A completed response matches a rule (entered as JSON in the activity) |
| Survey Published | A version is published |
| Survey Closed | A survey closes |
| Survey Quota Reached | A quota reaches its limit (filter by quota) |

Response events filter by surveys, kinds, collectors and context kinds, and have an **Include changes made by workflows** option (off by default) so a workflow does not trigger itself.

Inputs include `ResponseId`, `Number`, `Status`, `Grading`, `VersionNumber`, `AttemptNumber`, `UserId`, `Respondent`, `Context`, `ContextId`, `CompletedUtc`, `Score`, `MaxScore`, `Percent`, `Passed`, `Answers` (question name to answer text) and `AnswerValues` (question name to raw values), and for survey events `SurveyId`, `SurveyName`, `SurveyTitle`, `SurveyStatus` and `PublishedVersionNumber`. Score events also have `From`, `To`, `Reason` and `Change`. Identity inputs are empty for anonymous surveys.

## Tasks

| Task | Outcomes | Does |
| --- | --- | --- |
| Create Survey Link | Done, Failed | Makes a personal link for a respondent and a context, for an e-mail or SMS task |
| Send Survey Invitation | Done, Failed | Creates and sends an invitation (Invitations feature) |
| Close Survey | Done, Failed | Closes a survey |
| Open Survey | Done, Failed | Opens a survey again |
| Void Survey Response | Done, Failed | Voids a response with a reason |
| Regrade Survey Response | Done, Failed | Scores a response again with a reason |
| Grant Survey Accommodation | Done, Failed | Grants extra minutes or attempts with a reason |
| Set Survey Response Property | Done, Failed | Writes a value into the response's properties |

## Notifications, audit trail and webhooks

- **Notifications** (Notifications feature): `SurveyResponseGraded` to the identified respondent when a person grades it, and `SurveyNeedsGrading` to the survey's owner. Invitations add `SurveyInvitation` and `SurveyReminder`.
- **Audit Trail**: survey created, published, restored, closed, archived and deleted; access changed; responses exported; response voided, deleted and reopened; score changed; accommodation granted and revoked; responses anonymized.
- **Webhooks**: `survey.published`, `survey.response.completed` and `survey.response.graded`, with the context and properties of the response and no identity for anonymous surveys.
