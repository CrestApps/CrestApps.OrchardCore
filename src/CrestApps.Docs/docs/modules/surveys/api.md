---
sidebar_label: API
title: Surveys API
description: "The Surveys JSON API on the Api authentication scheme: list surveys, read a definition, start and answer responses, list responses."
user_manual:
  - user-manual/surveys/share-and-invite
---

With **Surveys API** on, these endpoints are under `/api/surveys` on the `Api` authentication scheme. Refusals of the access rules, validation errors and the unavailable secret below are Problem Details responses; a survey or response that isn't found answers a bare `404`, and a missing permission a bare `403`. Answer keys never leave the server. Every response carries `Cache-Control: no-store`, so no browser or shared proxy keeps answers, pages or responses. An anonymous survey whose secret this server can't read (Data Protection keys that aren't shared) answers `503` with the problem type `surveyunavailable`. When the Orchard Core **Rate Limits** feature is on, every endpoint is in the `Surveys.Respond` rate-limit group, like the runner (120 requests a minute per IP address).

| Endpoint | Permission | Does |
| --- | --- | --- |
| `GET /api/surveys` | Manage own surveys | The surveys the caller may manage |
| `GET /api/surveys/{id}/definition?collector={token}&responseId={id}` | Respond to surveys | What a respondent sees: of the published version once the access rules allow the caller, or of the caller's own started response |
| `GET /api/surveys/{id}/responses?skip=&take=` | View responses (of the survey) | A page of responses; who responded shows on identified surveys only; an anonymous survey returns `hidden: true` and no item below its minimum group size, days only (`startedUtc` and `completedUtc` at the start of the day) and context kinds only |
| `POST /api/surveys/responses` | Respond to surveys | Starts, or resumes, the caller's response |
| `GET /api/surveys/responses/{responseId}` | Respond to surveys | The current page of the caller's response |
| `PUT /api/surveys/responses/{responseId}/pages/{pageId}` | Respond to surveys | Answers the current page and moves on (or back) |
| `POST /api/surveys/responses/{responseId}/complete` | Respond to surveys | Answers the last page and completes the response |

Start a response:

```json
{
  "SurveyId": "43cxfrt81g8mm52kvve7jp02cq",
  "CollectorToken": null,
  "Context": "Ticket:4qk...",
  "Variables": { "source": "app" },
  "Culture": "en-US"
}
```

Answer a page (answers by question id, in the shape each question type reads; `Direction` is `Next`, `Back` or `Finish`):

```json
{
  "Answers": { "4dfhbt1zbets75mz7j6b1bhkz8": "9" },
  "Direction": "Next"
}
```

## What the API does not trust

A respondent call runs the same access rules as the runner, and the caller cannot pick what the server decides:

- **Context.** `Context` (`Kind:Id`) is accepted only when its kind is registered with `AddSurveyContextKind` and a registered `ISurveyContextOwnershipCheck` of that kind says the caller owns the record (`AddSurveyContextOwnershipCheck<T>()`). Anything else is refused with `403` before a response is started, so made-up contexts cannot split attempt limits or start workflows with a fake context kind. Learning registers a check for its `LearningEnrollment` (the learner's own enrollment) and `LearningCourse` (a course the caller is enrolled in) kinds, for surveys a course item owns.
- **Variables.** `Variables` are treated like query string values: only hidden variables with **Accept from URL** take them (cut to 500 characters), and the collector's values and the defaults are kept for the others.
- **Definition.** Without `responseId`, the survey must be open and the caller must pass its access rules (audience, password, invitation, enrollment, CAPTCHA, window...), as when starting a response, the attempt limits, the most responses and quotas included: reading the questions is not an attempt, but a respondent with no attempt left can't read a quiz they can no longer take. A survey behind a password, an invitation or a course enrollment therefore needs a started response: pass `responseId` of the caller's own response in progress, and the definition is its version with only the questions its plan drew, in its choice order. Without a response, pooled questions are left out, and the choices of question types whose answer key is an order (`ISurveyControlScorer.AnswerKeyIsOrder`, such as Ranking) come shuffled on every call. Author HTML (introduction, page descriptions, question titles and descriptions) is sanitized.
