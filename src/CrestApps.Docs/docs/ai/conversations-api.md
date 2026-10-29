---
sidebar_label: AI API
title: AI API
description: Manage AI profiles, knowledge and phone numbers, start and control AI calls and texts, read and evaluate conversations, and run tests over a REST API with OpenID tokens or scoped API keys.
---

# AI API

The **AI Chat WebAPI** feature (`CrestApps.OrchardCore.AI.Chat.Api`) maps a JSON API under `/api/ai/v1` for back-office systems: a CRM that starts AI calls and shows their conversations, a test pipeline that runs simulated conversations before a profile change ships, a script that keeps profiles and their knowledge in source control, or a privacy tool that deletes a customer's conversations. The endpoints are minimal API endpoints, so Orchard Core's **OpenApi** feature describes them in the tenant's OpenAPI document (`/swagger/v1/swagger.json`), grouped by tag (**AI Profiles**, **AI Conversations**, **AI Calls and Texts**, **AI Knowledge**, **Phone Numbers**, **AI Tests** and **AI Chat**), with an operation name and summary each, and its **Swagger UI**, **ReDoc** and **Scalar** features show them. To try the endpoints in Swagger UI or Scalar, run Orchard Core's **OpenApiPkce** recipe, which lets the documentation pages sign in with your admin session.

Some groups appear only with their feature: calls and texts need **Omnichannel Management**, live calls need the **Contact Center** supervisor dashboard, numbers need **Omnichannel Addresses**, documents need **AI Profile Documents**, and data sources, file sources and web crawlers need their own features.

## Authentication

Every endpoint takes either a bearer token from the tenant's OpenID server or an **API key**. Cookies are not used, so no antiforgery token is needed. A call without either answers `401`, one that lacks the permission or the scope answers `403`, and errors are problem details (RFC 9457).

### OpenID tokens

A server uses the **client credentials flow** as a confidential client, whose **Client Credentials Roles** decide what it may do:

```http
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials&client_id=qa-pipeline&client_secret=YOUR-CLIENT-SECRET
```

A client with no `ai.` scope may call every area its roles allow, as before. To narrow it, add the scopes below on the OpenID server (**Security → OpenID Connect → Scopes**), give the client the ones it needs and request them with the token; it may then call only those areas.

### API keys

Under **Artificial Intelligence → API keys**, an admin with **Manage AI API keys** makes two kinds of key:

- A **server key** (`cak_...`) is secret. It acts with the permissions of the roles you choose, in the areas (scopes) you tick, and can expire. It is shown once, when it is made; the list shows only its start and when it was last used.
- A **browser key** (`cpk_...`) is public, for a web page. It may only chat with the profiles you choose through `POST /api/ai/completion/chat`, only from the web sites (origins) you list; a request from another site, or with no `Origin` header, is refused. Allow the same sites in a CORS policy (Orchard Core's **CORS** feature) so browsers may call the API.

Send a key in the `X-Api-Key` header, or as `Authorization: Bearer cak_...`:

```http
GET /api/ai/v1/conversations?status=Closed&take=20
X-Api-Key: cak_1a2b3c4d_...
```

### Scopes and permissions

| Scope | Endpoints | Permission |
| --- | --- | --- |
| `ai.profiles` | `profiles`, `profiles/{id}/versions` | **Manage AI profiles** |
| `ai.conversations` | `conversations`, `profiles/{id}/simulations` | **Manage AI profiles**; deleting needs **Delete all chat sessions** |
| `ai.tests` | `tests` | **Manage AI quality** |
| `ai.calls` | `calls`, `texts`, `batches`, `activities` | **Create and edit activities**; reading needs **List activities** |
| `ai.calls` | `live-calls` | **Monitor the Contact Center** to list; **Take over, end, transfer and record live calls** to act; a transfer to a number also needs **Transfer calls externally** |
| `ai.knowledge` | `profiles/{id}/documents`, `data-sources`, `file-sources`, `web-crawlers` | **Manage AI profiles**, **Manage AI data sources**, **Manage file sources**, **Manage web crawlers** |
| `ai.numbers` | `numbers` | **Manage omnichannel addresses** |
| `ai.contacts` | `contacts/{id}/memory` | **Manage AI profiles** |
| `ai.chat` | `api/ai/completion/chat`, `api/ai/completion/utility`, `api/ai/chat/session` | **Query any AI profile** |
| `ai.recordings` | `recordings` | **Listen to anyone's call recordings** |

## Profiles

| Method and path | What it does |
| --- | --- |
| `GET profiles` | Lists profiles. Query: `name` (part of the name), `skip`, `take`. |
| `GET profiles/{profileId}` | Gets a profile with all its settings, in the shape the **AIProfile** recipe step takes. |
| `POST profiles` | Creates a profile from that JSON. The name must be unused; the id is always new. Answers `201`. |
| `PUT profiles/{profileId}` | Changes the settings sent and leaves the rest. The id and name stay. The save is a new version. |
| `DELETE profiles/{profileId}` | Deletes a profile. |
| `GET profiles/{profileId}/versions` | Lists its saved versions, newest first. |
| `POST profiles/{profileId}/versions/{versionId}/restore` | Publishes a saved version again: the profile takes its settings back, saved as a new version noted "Restored from version N over the API". |

Every save is a version, so there is no separate publish step: a change is live once saved, and restoring is how an older version goes live again.

## Calls and texts

| Method and path | What it does |
| --- | --- |
| `POST calls` | Starts an AI phone call. Body: `profileId`, `to` (E.164, such as `+15551234567`), and optionally `fromEndpointId` (one of your numbers), `campaignId`, `contactId`, `subjectContentType`, `variables` (an object of names and values the profile sees) and `scheduledUtc`. Answers `201` with the activity. |
| `POST texts` | Starts an AI text conversation, with the same body. The AI sends the first message. |
| `POST batches` | Starts calls or texts with up to 1,000 customers. Body: `profileId`, `channel` (`call` or `text`), `fromEndpointId`, `campaignId`, `scheduledUtc`, and `items`, each with `to`, `contactId` and `variables`. |
| `GET batches/{batchId}` | Gets each call or text of a batch, with a count by status. |
| `GET activities/{activityId}` | Gets where a call or text stands (`Pending`, `Dialing`, `InProgress`, `Completed`, `Failed`, `Cancelled`), its disposition, and `conversationId` once it started; read the conversation with `GET conversations/{conversationId}`. |
| `POST activities/{activityId}/cancel` | Cancels a call or text that has not started. Answers `409` once it has. |

Each call or text is an automated activity, so it goes through the same opt-out, do-not-call and calling-window checks as any other, and is placed or sent by the automated activities task.

```http
POST /api/ai/v1/calls
X-Api-Key: cak_1a2b3c4d_...
Content-Type: application/json

{
  "profileId": "4ayjq3bkwjhfktsgejaj7kzwnf",
  "to": "+15551234567",
  "contactId": "4bq0d8a7p5yv9xh3ww1n2kzt6c",
  "variables": { "order_number": "4471", "balance_due": "120.00" }
}
```

### Live calls

| Method and path | What it does |
| --- | --- |
| `GET live-calls` | Lists the phone calls an AI is holding now, with how long each has gone on and its `conversationId`, whose transcript grows in `GET conversations/{id}`. |
| `POST live-calls/{activityId}/notes` | Sends the AI a note it acts on at its next turn; the caller does not hear it. Body: `note`. |
| `POST live-calls/{activityId}/transfer` | Transfers the call. Body: `to` (a number) or `agentId`, `mode` (`cold`, `warm` or `screened`), and optionally `briefing`, `introduction`, `ringTimeoutSeconds` (5 to 120, default 30) and `showCallerNumber`. |
| `POST live-calls/{activityId}/profile` | Switches the call to another AI profile. Body: `profileId`. On a realtime call its instructions and tools apply from the AI's next reply, in the same voice; on a turn-based call from the caller's next turn. |
| `POST live-calls/{activityId}/end` | Hangs up. |

Each answers `succeeded` and a `message`, such as "No AI is on this call." when it already ended.

## Conversations

| Method and path | What it does |
| --- | --- |
| `GET conversations` | Lists conversations, newest activity first. Query: `profileId`, `status` (`Active`, `Closed` or `Abandoned`), `since` (last activity at or after), `skip`, `take` (at most 100). |
| `GET conversations/{sessionId}` | Gets a conversation with its transcript, conversation variables, extracted fields, post-session results, contact, evaluations, and its conversation `flow` (the stage it is in and the stages it went through) when it has one. |
| `DELETE conversations/{sessionId}` | Deletes a conversation with its transcript, extracted data and evaluations. Answers `204`. |
| `POST conversations/{sessionId}/evaluations` | Scores the conversation against criteria with the utility model and saves the evaluation. Body: `criteria` (list), `passingScore` (0 to 100, default 70), `label`. Answers `409` when there is nothing the customer said or no utility model. |
| `POST conversations/{sessionId}/analysis` | Runs the profile's post-session analysis again with its current tasks, and returns the results by task. Answers `409` when the profile has no analysis. |
| `POST profiles/{profileId}/simulations` | Runs a test conversation with a customer another model plays, and has a model judge it when criteria are given. Body: `scenario`, `variables`, `maxTurns` (1 to 20, default 8), `criteria`, `passingScore`, `label`, and optionally `customerDeploymentName` and `judgeDeploymentName` (both default to the utility deployment). The profile's tools run, with `Simulation` set in their workflow input. |
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

## Knowledge

| Method and path | What it does |
| --- | --- |
| `GET profiles/{profileId}/documents` | Lists the documents a profile answers from. |
| `POST profiles/{profileId}/documents` | Uploads documents as `multipart/form-data` files. They are chunked, embedded and indexed as from the profile's editor. Answers the documents added and the files that were not, with why. |
| `DELETE profiles/{profileId}/documents/{documentId}` | Removes a document and its chunks from the index. |
| `GET`, `POST data-sources`; `GET`, `PUT`, `DELETE data-sources/{id}` | Data sources, in the shape their recipe step takes. Creating one needs its `Source`. `GET` takes `source` to list one kind. |
| The same on `file-sources` and `web-crawlers` | File sources and web crawlers. |

## Phone numbers

| Method and path | What it does |
| --- | --- |
| `GET numbers` | Lists your phone numbers and other channel addresses. Query: `channel`, such as `phone` or `sms`. |
| `GET numbers/{id}` | Gets one. |
| `POST numbers` | Adds one: `DisplayText`, `Channel`, `Value` (the number in E.164), `ProviderName` and `Capabilities`. Buying the number stays with your carrier; this records it here. |
| `PUT numbers/{id}`, `DELETE numbers/{id}` | Changes or removes one. |

### Carrier numbers (Telnyx)

| Method and path | What it does |
| --- | --- |
| `GET carrier-numbers/available` | Searches the numbers for sale. Query: `countryCode`, `areaCode`, `city`, `region`, `contains`, `sms`, `limit`. |
| `POST carrier-numbers/orders` | Buys up to 20 numbers. Body: `phoneNumbers`, and optionally `connectionId`, `messagingProfileId` and `addAsAddresses` (default true). Billed by Telnyx. |
| `GET carrier-numbers` | Lists the account's numbers, each with `spamRejections` (calls carriers refused as spam, SIP 607 or 608, in the last 30 days) and `lastSpamRejectionUtc`. |
| `DELETE carrier-numbers/{numberId}` | Releases a number. |
| `PUT carrier-numbers/{numberId}/caller-name` | Sets the caller name. Body: `callerName`. |

These need **Manage telephony settings** and the `ai.numbers` scope. A refusal from Telnyx answers `502` with what Telnyx said.

## Tests

| Method and path | What it does |
| --- | --- |
| `GET tests` | Lists the test cases. |
| `POST tests/batches` | Queues test cases to run, by id or tag; see [Conversation quality](../user-manual/ai/conversation-quality.md). |
| `GET tests/batches/{batchId}` | Gets each run of a batch and the pass rate. |

## Recordings

| Method and path | What it does |
| --- | --- |
| `GET recordings?activityId=` | Lists the recordings of an activity's calls, with whether each was bleeped. |
| `POST recordings/{recordingId}/links` | Makes a link to a recording's audio that works without signing in until it expires. Query: `minutes`, at most 7 days; 24 hours by default. |

These need the `ai.recordings` scope and **Listen to anyone's call recordings**, and the **Call recordings** feature.

## Client libraries

The repository's `utilities/ai-sdks/generate-ai-sdks.ps1` generates TypeScript and Python clients (or C#, Java, Go and PHP) for the `/api/ai` paths from a site's OpenAPI document, with [Kiota](https://learn.microsoft.com/openapi/kiota/) pinned in `dotnet-tools.json`. Nothing is published; package and version the clients as you choose.

```bash
pwsh ./utilities/ai-sdks/generate-ai-sdks.ps1 -SiteUrl https://localhost:5001 -Languages TypeScript,Python
```

The clients are written to `artifacts/ai-sdks/typescript` and `artifacts/ai-sdks/python`. When the OpenAPI document needs signing in, save it from Swagger UI and pass `-OpenApiFile` instead of `-SiteUrl`.

## A test in a pipeline

A deployment pipeline can run the same scenarios against a profile after each change and fail when one doesn't pass:

```http
POST /api/ai/v1/profiles/4ayjq3bkwjhfktsgejaj7kzwnf/simulations
X-Api-Key: cak_1a2b3c4d_...
Content-Type: application/json

{
  "scenario": "A customer whose order 4471 is late wants to know when it will arrive, and gets impatient.",
  "criteria": ["Looks up the order before answering", "Gives the delivery date", "Stays polite"],
  "passingScore": 80,
  "label": "late order"
}
```

The answer has the `transcript`, whether the customer ended the conversation (`customerEnded`), and the `evaluation` with its `score`, `passed`, criteria and issues.
