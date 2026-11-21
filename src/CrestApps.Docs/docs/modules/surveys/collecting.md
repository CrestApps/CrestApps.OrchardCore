---
sidebar_label: Collecting responses
title: Collecting Responses
description: "Public links, kiosks, personal invitations, embedding, the respondent routes, access rules, respondent limits, anonymity, quotas and the runner."
user_manual:
  - user-manual/surveys/share-and-invite
---

## Collectors

A collector (`SurveyCollector`) is a way in: a **web link**, a **kiosk**, an **invitation** list, or a kind of a consumer's own. Each has its own token (the public URL `/s/{token}`), opening and closing times, most responses, password (stored as a hash), hidden variable values and culture. The first publish creates the survey's default web link. A collector's QR code is served as SVG at `admin/surveys/collectors/{id}/qr.svg`.

A **kiosk** shows the ending for a few seconds, then returns to the start for the next person; it clears the resume cookie and never resumes a response.

## Respondent routes

| Route | Does |
| --- | --- |
| `GET /s/{token}` | The start page (password or CAPTCHA when needed), or the first page when the survey skips it |
| `POST /s/{token}/start` | Starts a response |
| `GET`/`POST /s/r/{responseId}` | The current page; posts Next, Back or Finish |
| `POST /s/r/{responseId}/save` | Autosave without checks |
| `POST /s/r/{responseId}/evaluate` | Visibility and piping for answers not yet posted |
| `POST /s/r/{responseId}/check/{questionId}` | Immediate feedback |
| `GET /s/r/{responseId}/done` | The ending |
| `GET /s/r/{responseId}/review` | The review, as the review settings allow |
| `POST /s/r/{responseId}/reopen` | Reopens a finished response when changes are allowed |
| `GET /s/i/{token}` | A personal invitation link |
| `GET /s/{token}/results` | Totals of all responses, when the survey shows them |
| `GET /surveys/my` | The signed-in user's own responses |

Posts check the antiforgery token. When Rate Limits is on, the respondent routes are in the rate-limit group `Surveys.Respond`. A response is owned by the signed-in user who started it, or by whoever holds its resume token (an HttpOnly cookie `sr_{surveyId}` on path `/s`, hashed at rest).

## Access rules

`ISurveyAccessRule`s decide whether a response may start, and run again when it completes (under a lock, so two tabs cannot pass a limit). The first refusal wins.

| Order | Rule | Checks |
| --- | --- | --- |
| 100 | `Status` | The survey is open |
| 200 | `AvailabilityWindow` | The survey's and the collector's opening and closing times, and accommodations' windows |
| 300 | `WeeklySchedule` | The days and hours of the weekly schedule, in the site time zone |
| 400 | `Audience` | Anyone, signed-in users with Respond to surveys, members of user audiences, or invited people only |
| 500 | `Password` | The collector's password |
| 600 | `Captcha` | A CAPTCHA for respondents who are not signed in (reCAPTCHA feature) |
| 700 | `Invitation` | One response per invitation |
| 800 | `RespondentLimits` | Total, per day, per week (from the site's first day of the week), per month, and the wait between responses; accommodations add attempts |
| 900 | `MaxResponses` | The survey's and the collector's most completed responses |
| 1000 | `Quota` | Full quotas (on completion) |

Limits count completed or started responses per respondent, recognized by the user account, the invitation, a browser cookie (weak: it can be cleared) or not at all. Test responses (preview) skip limits and windows.

## Anonymity

**Identified** responses store the user and party (and the IP address when the survey collects it). **Anonymous** responses store none of these and no invitation: limits use an HMAC of the respondent under the survey's secret, in a participation record (`SurveyParticipation`, per day) that holds no response id. Switching a survey between the two applies to new responses only. Reports of anonymous surveys hide groups smaller than the minimum group size.

## Invitations

With **Survey Invitations** on, an invitation collector invites users of the site (by user name), a user audience, a cohort, or e-mail addresses. Each invitation (`SurveyInvitation`) has a personal link `/s/i/{token}`; the token is an HMAC of the invitation id under the survey's secret, and only its hash is stored. Users get a notification (type `SurveyInvitation`); other people get an e-mail rendered from the collector's Liquid template, which can use `Survey.Title`, `Invitation.DisplayName` and `Link`. Links may expire.

Reminders (type `SurveyReminder`) go every N days, up to a set number, to people who have not completed the survey; they stop on completion. An anonymous invitation is marked completed from its participation count, since its response does not name it.

`ISurveyInvitationService` creates and sends invitations from code, and `ISurveyLinkService.CreateLinkAsync` makes a personal link for a respondent and a context (used by the **Create Survey Link** workflow task).

Invitations are sent by the invitation service and its hourly task, not through Outreach runs, so contact opt-outs are not checked for them.

## Embedding

With **Survey Embedding** on:

- `SurveyPart` (attachable) picks an open survey and one of its links, and shows the survey in the page, as a button or as a link. The `SurveyWidget` content type holds the part.
- The shortcode `[survey name="customer-satisfaction" collector="..." display="link"]` renders a button (or a link) that opens the survey (with Shortcodes on).

## Quotas

A quota counts completed responses that match its rule. When the count reaches the limit, new matching respondents are screened out with the quota's ending, or the survey closes, or nothing happens (count only). Counters change only under the survey lock, and the **Survey Quota Reached** workflow event fires.

## Responses

A response moves through `InProgress`, `Completed`, `ScreenedOut`, `Abandoned` and `Voided`. A completed response can be reopened only when the survey allows changes after finishing, and keeps its version. Unfinished responses idle longer than the survey's limit (168 hours by default) are abandoned by the maintenance task.

Consumers start responses for their own records through `ISurveyResponseService.StartAsync` with a `Context` (a `RecordRef`), their own variables and properties.
