---
sidebar_label: Learning Record Store
title: Learning Record Store
description: "The xAPI 1.0.3 Learning Record Store of Learning: statements, documents, activities, agents and about, for OpenID applications and launch tokens, never the sign-in cookie."
---

**Learning Record Store** (`CrestApps.OrchardCore.Learning.Xapi`, with `Learning.Xapi.Abstractions` and `Learning.Xapi.Core`) adds an xAPI Learning Record Store (LRS) to [Learning](./learning). It implements xAPI 1.0.3. It accepts requests with the version header `1.0` or `1.0.0` to `1.0.3`, answers with `X-Experience-API-Version: 1.0.3`, and refuses `2.0.0` (IEEE 9274.1.1-2023) with 400: the rules that differ in xAPI 2.0 are not implemented.

| Feature | Id | Depends on |
| --- | --- | --- |
| Learning Record Store | `CrestApps.OrchardCore.Learning.Xapi` | Learning, OpenID Core Components (`OrchardCore.OpenId`), OpenID Management Interface (`OrchardCore.OpenId.Management`) |

[Learning cmi5 Packages](./learning-packages#cmi5) launches cmi5 courses against this LRS and enforces the cmi5 statement rules in it.

## The endpoint

The LRS is at `/learning/xapi/` on the tenant (for example `https://example.com/learning/xapi/`). Its resources:

| Resource | Methods | What it does |
| --- | --- | --- |
| `statements` | PUT, POST, GET, HEAD | Stores and reads statements (see below). |
| `activities/state` | PUT, POST, GET, HEAD, DELETE | State documents of an activity, an agent and an optional registration. |
| `activities/profile` | PUT, POST, GET, HEAD, DELETE | Activity profile documents. |
| `agents/profile` | PUT, POST, GET, HEAD, DELETE | Agent profile documents. |
| `activities` | GET, HEAD | The canonical definition of an activity, joined from the definitions statements carried. |
| `agents` | GET, HEAD | The Person object of an agent. The LRS does not join agents into persons: the person is the agent asked for. |
| `about` | GET, HEAD | The versions the LRS accepts. It needs no credential. |

Every request but `about` needs `X-Experience-API-Version` with a 1.0 version. A parameter a resource does not define is refused with 400. The alternate request syntax works for writes: a POST whose only query parameter is `method` (PUT, POST or DELETE), with the headers (`Authorization`, `X-Experience-API-Version`, `Content-Type`, `If-Match`, `If-None-Match`), the body as `content` and the other parameters in an `application/x-www-form-urlencoded` form. Attachments cannot be sent that way. A GET or HEAD in that syntax is refused with 400: any page can post a form as a top-level navigation, so a read must send `Authorization` and `X-Experience-API-Version` as real headers.

## Credentials

The LRS takes only these credentials. The sign-in cookie never opens it, even for an administrator: a request that has only the cookie gets 401.

- **An OpenID application.** Create an application under **Security → OpenID Connect → Management → Applications** (OpenID Management Interface) and give it a role that holds the LRS permissions below. The application is the LRS client:
  - with a bearer token of Orchard Core's `Api` scheme: enable the OpenID Authorization Server and Token Validation features, allow the client credentials flow on the application, and send `Authorization: Bearer <token>`;
  - with HTTP Basic: the application must be confidential; send `Authorization: Basic base64(client id:client secret)`. The LRS checks the secret with OpenIddict's application manager, on the same application records.
- **A launch token.** A module that launches content for a learner (cmi5) makes a token that opens the LRS for one actor, one registration and one activity, until it expires. It is sent with HTTP Basic and the user name `xapi-launch`.

The `authority` of a statement a client stores is an agent with an account: the site's home page (its base URL, or the request's address when none is set) and the client id.

| Permission | Lets a client |
| --- | --- |
| Write learning statements to the Learning Record Store (`WriteLearningStatements`) | Store statements. |
| Read all learning statements of the Learning Record Store (`ReadAllLearningStatements`) | Read every statement; also opens the admin list. |
| Read the learning statements the client stored (`ReadOwnLearningStatements`) | Read only the statements whose authority is this client. |
| Manage the documents of the Learning Record Store (`ManageLearningDocuments`) | Read, write and delete state and profile documents; read the activities and agents resources. |

The Administrator role holds all four. A launch token reads and writes only the statements of its actor and registration, the states of its activity, actor and registration (a module can mark states such as the cmi5 `LMS.LaunchData` read-only), the agent profiles of its actor, and reads the activity profiles of its activity.

When Orchard Core's Rate Limits feature is on, an address may send 1,200 LRS requests a minute (the rate limit group `LearningXapi`).

### Requests from another origin

Content often runs on another origin, and cmi5 content of a package runs in a sandboxed frame whose origin is opaque. Every LRS response carries `Access-Control-Allow-Origin: *` and exposes `ETag`, `Last-Modified`, `X-Experience-API-Version` and `X-Experience-API-Consistent-Through`; preflights (`OPTIONS`) allow the xAPI methods and the `Authorization`, `Content-Type`, `X-Experience-API-Version`, `If-Match` and `If-None-Match` headers. No response allows credentials. This is safe only because the cookie never authenticates the LRS: a page can reach it only with a credential it was given.

## Statements

- **Validation.** Every statement is checked against xAPI 1.0.3 Part Two: a property xAPI does not define, a property in another case and a `null` value are refused; agents have exactly one identifier (`mbox` as a `mailto:` IRI, `mbox_sha1sum`, `openid`, or an `account` with `homePage` and `name`); anonymous groups list their members and groups never hold groups; verbs, activities and their definitions (interaction types, unique component ids), statement references and sub-statements (no `id`, `stored`, `version`, `authority` or nested sub-statement); results (`scaled` from -1 to 1, `raw` within `min` and `max`, ISO 8601 durations); contexts (`revision` and `platform` only with an activity, `team` a group, RFC 5646 language tags, a UUID registration); timestamps (the zone `-00:00` is refused); `version` 1.0.x; language maps, extension IRIs and attachments. A batch is stored whole or not at all.
- **Immutability.** A statement id that is stored again with the same statement is a no-op (204 for PUT); with another statement it is a conflict (409). The same id twice in one request is refused.
- **What the LRS sets.** `stored` (to the millisecond), `authority`, `version` (`1.0.0` when none was sent) and `timestamp` (the stored time when none was sent).
- **Voiding.** A statement with the verb `http://adlnet.gov/expapi/verbs/voided` and a StatementRef object voids its target: queries do not return the target, `statementId` answers 404 for it, and `voidedStatementId` returns it. A voiding statement cannot be voided (400). Who may void what: a client that reads all statements may void any; another client only a statement it stored itself; a launch token only a statement of its own actor and registration stored by a launch, never one Learning stored with its own authority (such as cmi5's `launched` or `satisfied`). Anything else is refused with 403. A voiding statement stored before its target keeps what its sender could void (`XapiStatementRecord.VoidingRights`: reads every statement, or the launch's actor and registration, with its authority), and voids the target when it arrives only if the same rule allows it; otherwise the target stays valid. A voiding statement stored before Learning kept these rights voids a later target only of its own authority.
- **Queries.** `agent`, `verb`, `activity`, `registration`, `related_activities`, `related_agents`, `since`, `until`, `limit` (100 at most; 0 means 100), `format` (`ids`, `exact`, `canonical`, which keeps one language per language map from `Accept-Language`), `attachments` and `ascending`. A statement that refers to another (StatementRef) matches the filters of the statement it refers to, through any number of references, also when the target arrives later. Results are newest first unless `ascending=true`. When more statements match, `more` is a link (the path from the site root) whose pages do not move when statements are stored meanwhile. Every statements GET has `X-Experience-API-Consistent-Through`.
- **Attachments.** A `multipart/mixed` request carries the statements in the first part and each attachment in a part with `Content-Transfer-Encoding: binary` and its SHA-2 hash in `X-Experience-API-Hash`. The LRS checks the hash of every part, refuses an attachment without a `fileUrl` whose content is missing and a part no attachment names, and keeps the content in a protected file area (`LearningXapiAttachments`); `attachments=true` returns it. .NET has no SHA-224, so an attachment needs SHA-256, SHA-384 or SHA-512.
- **Signed statements.** A signature attachment (`http://adlnet.gov/expapi/attachments/signature`, `application/octet-stream`) must be a JWS in compact serialization with RS256, RS384 or RS512, whose payload is the statement (compared with the statement comparison rules, without the attachments). When its header carries an X.509 certificate (`x5c`), the certificate's key must verify the signature. Without a certificate the LRS cannot know the signer's key, so only the form and the payload are checked.
- **Limits.** A request may carry at most 20 MB, 500 statements of at most 256 KB each and 16 MB of attachments; a document at most 1 MB. A request over a limit gets 413. The limits are `XapiOptions`. Document writes run one at a time per document (a distributed lock), so concurrent merges are not lost and a document is not created twice.

## Documents

State, activity profile and agent profile documents are stored by PUT, joined by POST (when a document exists, both it and the request must be JSON objects, and their top-level properties are merged; anything else is 400), read one at a time (with `ETag` and `Last-Modified`; a document stored as `application/json` comes back as JSON, anything else as an `application/octet-stream` attachment, and every document with `Content-Security-Policy: sandbox`, because its content is whatever the writer sent) or as a list of ids (with `since`), and deleted one at a time or, for states, all states of an activity, agent and registration. `If-Match` and `If-None-Match` are honored on every write (412 when they do not hold); a PUT to an existing activity or agent profile without either header is a conflict (409). States are kept per registration.

## In the admin

**Learning → Learning Statements** lists the statements, newest first, with voided ones marked. Search by actor (a user name, an e-mail address or an account name); filter by verb, activity IRI, registration and the days they were stored, in your time zone. A statement's page shows its facts, the times in your time zone, and the statement as stored.

## Learner records

- **Data source.** The **Learning statements** data source (`Learning.Xapi`, data set `LearningStatements`) serves Data Pipelines and the Report Builder: one row per statement with its id, verb, object, registration, result, timestamp, stored time, voided mark and the client that stored it. It needs View learning reports. It never holds an e-mail address: the actor is an account name and home page, or the SHA-1 hash of the mailbox (as `mbox_sha1sum`), and an agent object is named by its name only.
- **Personal data.** The export lists the statements whose actor is the person's account (on any home page) or mailbox (`mbox` or `mbox_sha1sum` of the person's e-mail addresses, ignoring case), with the statement as stored, and the state and agent profile documents of those agents, an account matched by its name on any home page, as for statements (the document index keeps the account name), so documents written while the site had another address, or none, are found too. Erasure deletes them, with the content of their attachments: a statement cannot be changed, and Learning keeps a completion on its own records.
- **Recipe and deployment.** The `LearningStatements` step moves statements as learner records: the deployment step is opt-in, writes 500 statements a page, needs Export learner records and is recorded; the recipe step imports each statement with its id, `stored`, `authority` and voided mark, skips one already on the site with the same content and reports one that differs. Statements cannot change, so their actors are not rewritten: an account actor keeps the user id of the site the statement came from. The content of attachments stays in the protected file area of the source site.
- **Workflow.** The **Learning Statement Stored** event starts when a statement is stored, for every verb or the verbs listed, with the statement id as the correlation id. Its input holds `StatementId`, `Verb`, `ObjectType`, `ObjectId`, `Registration`, `ActorAccountName`, `ActorAccountHomePage`, `ClientId`, `Voided` and `StoredUtc`; never an e-mail address.

## Extending

A module checks statements and acts on them through `Learning.Xapi.Abstractions`:

- `IXapiStatementFilter` runs around the storing of the statements a client sent, after validation: it can refuse the request with its own status, hold its own lock over the write and act on what was stored. Register one with `AddXapiStatementFilter<T>()`.
- `IXapiStatementObserver` learns of statements once they are stored (`AddXapiStatementObserver<T>()`).
- `XapiStatementService.WriteTrustedAsync` (in `Learning.Xapi.Core`) stores statements the module writes itself, with its own authority; they do not pass the filters. `XapiLaunchTokens` makes launch tokens, and `XapiDocumentStore` holds documents.

## Conformance

Conformance suite / certification: not run. The suite is ADL's LRS Conformance Test Suite (open source; it runs locally against an LRS endpoint), and running it needs a download, which this work did not allow. To run it, a person downloads the suite from ADL's repository, installs its dependencies, enables the Learning Record Store on a test site, creates an OpenID application with a role that holds the four LRS permissions, and points the suite at `https://<site>/learning/xapi` with that client's id and secret (HTTP Basic), then keeps the suite's report. The module implements xAPI 1.0.3 and makes no conformance claim.

## Not built

- xAPI 2.0.0 (IEEE 9274.1.1-2023).
- OAuth 1.0 and the common access cookie credentials of xAPI 1.0.3; joining agents into persons; SHA-224 attachments; a retention policy for statements.
- `canonical` returns the statement's own language maps reduced to one language, not the LRS's canonical activity definitions.
