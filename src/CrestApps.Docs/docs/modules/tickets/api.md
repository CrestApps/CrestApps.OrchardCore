---
sidebar_label: API
title: Tickets API
description: "The agents' JSON API at api/tickets/v1: list, read, open and change tickets, add replies and notes, with desk permissions, scope and idempotency keys."
---

The **Tickets API** feature maps `api/tickets/v1` with Orchard Core's `Api` authentication scheme (a bearer token). Each call checks the caller's ticket permissions and scope as the pages do; a ticket out of scope is answered `404`. Writes take an `Idempotency-Key` header, so sending one again returns the first answer.

| Endpoint | Does | Needs |
| --- | --- | --- |
| `GET tickets` | Lists tickets, newest change first: `q` (number or subject), `deskId`, `status` (categories, comma separated), `assignee` (`me`, `unassigned` or a user id), `requester`, `accountId`, `skip`, `take` (at most 100) | A view scope |
| `GET tickets/{id}` | A ticket by its id or number | View |
| `GET tickets/{id}/entries` | Its replies, notes and events, newest first, with download addresses for files that work for an hour | View |
| `POST tickets` | Opens a ticket: `type`, `subject`, `body`, `deskId`, `priorityId`, `requester` (`Kind:Id`) or `requesterName` and `requesterEmail`, `accountId`, `attachments` | Reply on the desk |
| `PATCH tickets/{id}` | Changes `subject`, `statusId`, `priorityId`, `deskId`, `escalationLevel` (edit) and `teamId`, `assigneeId` (assign) | Edit or assign |
| `POST tickets/{id}/replies` | A public reply with `body`, `setStatusId` and `attachments` | Reply |
| `POST tickets/{id}/notes` | An internal note with `body` and `attachments` | Note |

Files travel as `{ "fileName", "contentType", "content" }` with the content in base64, under the site's [attachment](../attachments) policy. Tickets opened through the API have the `Api` channel.

This is the agents' API: `entries` returns internal notes to anyone who may view the ticket, and every call is scoped by desk permissions. It is never the requesters' API. A customer portal gets its own endpoints with the requester rule (their own tickets, public entries only) rather than reusing these.
