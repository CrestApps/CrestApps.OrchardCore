---
sidebar_label: Conversations API
title: AI Conversations API
description: Read, evaluate, analyze again and delete AI conversations, run test conversations, list profile versions and manage contact memory over a REST API.
---

# AI Conversations API

The **AI Chat WebAPI** feature (`CrestApps.OrchardCore.AI.Chat.Api`) maps a JSON API under `/api/ai/v1` for back-office systems: a CRM that shows AI conversations, a test pipeline that runs simulated conversations before a profile change ships, or a privacy tool that deletes a customer's conversations. The endpoints are described in the tenant's OpenAPI document under the **AI Conversations** tag when Orchard Core's OpenAPI feature is on.

## Authentication

Every endpoint needs a bearer token from the tenant's OpenID server; cookies are not used, so no antiforgery token is needed. A server uses the **client credentials flow** as a confidential client, whose **Client Credentials Roles** decide what it may do:

```http
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials&client_id=qa-pipeline&client_secret=YOUR-CLIENT-SECRET
```

Give the client a role with only the permissions it needs:

| Endpoints | Permission |
| --- | --- |
| Everything except deleting | **Manage AI profiles** |
| `DELETE conversations/{sessionId}` | **Delete all chat sessions** |

A call without a token answers `401`, and one whose application lacks the permission answers `403`. Errors are problem details (RFC 9457).

## Endpoints

| Method and path | What it does |
| --- | --- |
| `GET conversations` | Lists conversations, newest activity first. Query: `profileId`, `status` (`Active`, `Closed` or `Abandoned`), `since` (last activity at or after), `skip`, `take` (at most 100). |
| `GET conversations/{sessionId}` | Gets a conversation with its transcript, conversation variables, extracted fields, post-session results, contact and evaluations. |
| `DELETE conversations/{sessionId}` | Deletes a conversation with its transcript, extracted data and evaluations. Answers `204`. |
| `POST conversations/{sessionId}/evaluations` | Scores the conversation against criteria with the utility model and saves the evaluation. Body: `criteria` (list), `passingScore` (0 to 100, default 70), `label`. Answers `409` when there is nothing the customer said or no utility model. |
| `POST conversations/{sessionId}/analysis` | Runs the profile's post-session analysis again with its current tasks, and returns the results by task. Answers `409` when the profile has no analysis. |
| `POST profiles/{profileId}/simulations` | Runs a test conversation with a customer the utility model plays, and evaluates it when criteria are given. Body: `scenario`, `variables`, `maxTurns` (1 to 20, default 8), `criteria`, `passingScore`, `label`. The profile's tools run, with `Simulation` set in their workflow input. |
| `GET profiles/{profileId}/versions` | Lists the saved versions of a profile, newest first. Query: `skip`, `take`. |
| `GET contacts/{contactId}/memory` | Gets what the AI remembers about a contact. |
| `PUT contacts/{contactId}/memory` | Replaces it. Body: `text`, at most 2,000 characters; empty forgets the contact. |

A conversation as listed:

```json
{
  "sessionId": "4d2dz2gjsyhd22s2xanjna934k",
  "profileId": "4ayjq3bkwjhfktsgejaj7kzwnf",
  "profileVersion": 3,
  "title": "Where is my order?",
  "status": "Closed",
  "createdUtc": "2026-10-27T15:02:11Z",
  "lastActivityUtc": "2026-10-27T15:06:40Z",
  "closedAtUtc": "2026-10-27T15:40:00Z",
  "storageMode": "RedactPersonalData"
}
```

`storageMode` is set once the profile's storage mode has been applied to the conversation; see [Speaking style, guardrails and privacy](speaking-style-guardrails-and-privacy.md).

## A test in a pipeline

A deployment pipeline can run the same scenarios against a profile after each change and fail when one doesn't pass:

```http
POST /api/ai/v1/profiles/4ayjq3bkwjhfktsgejaj7kzwnf/simulations
Authorization: Bearer eyJhbGciOi...
Content-Type: application/json

{
  "scenario": "A customer whose order 4471 is late wants to know when it will arrive, and gets impatient.",
  "criteria": ["Looks up the order before answering", "Gives the delivery date", "Stays polite"],
  "passingScore": 80,
  "label": "late order"
}
```

The answer has the `transcript`, whether the customer ended the conversation (`customerEnded`), and the `evaluation` with its `score`, `passed`, criteria and issues.
