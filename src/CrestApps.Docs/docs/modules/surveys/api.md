---
sidebar_label: API
title: Surveys API
description: "The Surveys JSON API on the Api authentication scheme: list surveys, read a definition, start and answer responses, list responses."
user_manual:
  - user-manual/surveys/share-and-invite
---

With **Surveys API** on, these endpoints are under `/api/surveys` on the `Api` authentication scheme. Errors are Problem Details responses. Answer keys never leave the server.

| Endpoint | Permission | Does |
| --- | --- | --- |
| `GET /api/surveys` | Manage own surveys | The surveys the caller may manage |
| `GET /api/surveys/{id}/definition?collector={token}` | Respond to surveys | What a respondent sees of the published version |
| `GET /api/surveys/{id}/responses?skip=&take=` | View responses (of the survey) | A page of responses; identity needs Export survey responses with respondent identity |
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
