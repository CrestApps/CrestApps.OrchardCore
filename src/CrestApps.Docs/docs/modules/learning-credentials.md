---
sidebar_label: Digital Credentials
title: Learning Digital Credentials (Open Badges 3.0)
description: "Open Badges 3.0 credentials (issuer) on the W3C Verifiable Credentials Data Model 2.0 for the CrestApps Learning module: credential definitions, awards by rule or by hand, the signed credential, public endpoints, the verification page and revocation through a Bitstring Status List."
user_manual:
  - user-manual/learning/digital-credentials
---

**Learning Digital Credentials** (`CrestApps.OrchardCore.Learning.Credentials`, with `Learning.Credentials.Abstractions` and `Learning.Credentials.Core`) awards digital credentials for learning done in [Learning](./learning). It implements Open Badges 3.0 (issuer) on the W3C Verifiable Credentials Data Model 2.0, with the JSON Web Token proof format and a W3C Bitstring Status List for revocation. It makes no certification or conformance claim (see [Conformance](#conformance)).

| Feature | Id | Depends on |
| --- | --- | --- |
| Digital Credentials | `CrestApps.OrchardCore.Learning.Credentials` | Learning, Secrets (`OrchardCore.Secrets`) |

A digital credential is not a Staff badge (the staff member's employee number and PIN) and not a Skills certificate. A [Learning Certifications](./learning-certifications) certificate is a numbered record with a PDF; a digital credential is a signed, portable statement a learner keeps in a wallet. A definition can align to a Skills certification, and a certification issued can award a credential.

## Credential definitions

A credential definition is a content item of the type **Credential definition** (`CredentialDefinition`), made by the feature's migration, so it gets drafts, versions, the content list and the `Content` recipe step. **Learning → Credential Definitions** opens the content list filtered to the type. Holders of **Manage credential definitions** (`ManageCredentialDefinitions`) get the content permissions on definitions; the type is also securable.

| Field | What it is |
| --- | --- |
| Title | The achievement's name and the credential's `name`. |
| Image | A media field (`Image`) on the type's own part; its public address is the achievement's `image`. |
| Description, Criteria | The achievement's `description` and `criteria.narrative`. Both are required. |
| Achievement type | An Open Badges 3.0 `achievementType`: Badge, Certificate, CertificateOfCompletion, Certification, Course, Competency, LearningProgram, MicroCredential, Award, Achievement, License or Membership. |
| Tags | The achievement's `tag`. |
| Framework alignments | The achievement's `alignment`: name, address (http or https), framework, code and type (such as `ceasn:Competency`). |
| Skill, Certification | With Skills on, a skill (target type `ceasn:Competency`) and a certification (`ceterms:Credential`) of the site, stated as alignments whose address is the achievement's address with a fragment. |
| Valid for (months) | Empty for a credential that does not expire; otherwise the award's `validUntil`. |
| Award rules | Any one awards it (see below). |

The part is `CredentialDefinitionPart`. Rule kinds are open strings registered with `services.AddCredentialRuleKind<T>()` (`ICredentialRuleKind`: the kind, a display name and the targets the editor offers).

## How a credential is awarded

| Rule kind | Awarded when | Needs |
| --- | --- | --- |
| `CourseCompleted` | An enrollment in the course completes, and Learning's completion evidence rules let it count: not failed, not under review, and not resting on a required item's result reported by the browser unless the item allows it (`CompletionEvidence`). | Learning |
| `QuizPassed` | A quiz attempt passes, scored by the site, in an enrollment that counts as evidence. | Learning Assessments |
| `LearningPathCompleted` | A learning path completes, and its rule still holds counting only the courses whose completion supports issuing (`CompletionEvidence.SupportsIssuing`: completed, not failed, not under review, not resting on a browser result). A path completed through any other course earns nothing, though it is completed for the learner (`ILearningPathService.SupportsIssuingAsync`). | Learning Paths |
| `CertificationIssued` | A Skills certificate is issued or renewed for a user, or for a staff member with a user account. | Learning Certifications |
| `Manual` | Staff with **Award and revoke digital credentials** award it under **Learning → Credential Awards → Award a credential**. | |
| `Workflow` | The **Award Credential** workflow task runs. | Workflows |

The rules hear the owners' observers only (`IEnrollmentObserver`, `IAttemptObserver`, `ILearningPathObserver`, `ICertificationRecordObserver`). They queue the award; it runs as a deferred task after the triggering request is saved, so it reads what was saved and a failure never undoes the learner's change (it is logged).

`ICredentialAwardService.AwardAsync` is idempotent per definition, rule kind, source id (the enrollment, attempt, path progress, certificate or workflow source) and recipient, under one distributed lock, and the award is saved before the lock is released. A person who holds a valid (not revoked, not expired) award of a definition gets no second one, whichever rule asks. A revoked award is not made again by the same source; a staff member can award again by hand.

**When what an award rests on goes away** (`CredentialSourceRevoker`, which finds the awards of a rule kind and source id). A revoked certificate revokes the awards its certification issued rule made from it (`ICertificationRecordObserver.RevokedAsync`); a quiz attempt that is voided, or whose score changes so that it is no longer a pass, revokes the awards of its quiz passed rule (`IAttemptObserver.ScoreChangedAsync`). Each revocation records the actor of the change and why. An enrollment flagged for review revokes nothing by itself: its review card offers **Revoke the credential** for each award its completion made (`IEnrollmentReviewActionProvider`), for whoever may manage the enrollment, while the review is open; the review stays open until it is cleared.

The award (`CredentialAward`, YesSql collection `LearningCredentials`, index `CredentialAwardIndex`) keeps the definition's content item version id and a copy of the achievement, so it never depends on a content version Orchard Core may prune; the recipient's user id; a salted SHA-256 hash of the recipient's e-mail address (the address itself is never stored in the award or the credential); issued and expiry times; the revocation (who, when, why); the recipient's choice to show their name; its status list slot; and the credential and its signed token as issued. A person without an e-mail address gets no credential.

## The credential

Each award carries an `OpenBadgeCredential`:

- `@context`: `https://www.w3.org/ns/credentials/v2`, then `https://purl.imsglobal.org/spec/ob/v3p0/context-3.0.3.json`; `type`: `VerifiableCredential`, `OpenBadgeCredential`.
- `id`: `https://<site>/learning/credentials/awards/<code>`, where the code is 128 random bits; `issuer`: a `Profile` with `id` `https://<site>/learning/credentials/issuer`, the site's name and address.
- `validFrom`, `awardedDate`, and `validUntil` when the definition has a validity; `name`.
- `credentialSubject`: an `AchievementSubject` whose `identifier` is an `IdentityObject` (`identityType` `emailAddress`, `hashed` true, `identityHash` `sha256$` and the hexadecimal SHA-256 of the lowercased address followed by the `salt`), and the `achievement` (`id` `https://<site>/learning/credentials/achievements/<definition id>`, `type`, `name`, `description`, `criteria`, and `image`, `achievementType`, `tag`, `alignment` when set). The subject has no `id`, so the token has no `sub`.
- `credentialSchema`: the Open Badges 3.0 achievement credential JSON schema, type `1EdTechJsonSchemaValidator2019`.
- `credentialStatus`: a `BitstringStatusListEntry` with `statusPurpose` `revocation`, the award's `statusListIndex` and the `statusListCredential` `https://<site>/learning/credentials/status/<n>`.

The proof is the Open Badges 3.0 JSON Web Token format: a compact JWS with `alg` `RS256`, `typ` `JWT` and `kid` the address of the public key (`https://<site>/learning/credentials/keys/<kid>`); the payload is the credential with `iss` (the issuer's id), `jti` (the credential's id), `nbf` (from `validFrom`) and `exp` (from `validUntil`). The signing key is the **Learning.Credentials** key set of the shared signing keys (`CrestApps.OrchardCore.SigningKeys`): an RS256 `RsaKeySecret` in Orchard Core Secrets, made at tenant activation, rotated every 365 days, a retired key still published for ten years so the credentials it signed keep verifying.

**Why the Bitstring Status List.** Open Badges 3.0 allows a `credentialStatus`; of the two revocation methods it names, the 1EdTech Revocation List and the W3C Bitstring Status List, this module uses the Bitstring Status List: it is a W3C Recommendation built for the Verifiable Credentials Data Model 2.0 that verifiers of VC 2.0 credentials already read, a verifier fetches a whole list of 131,072 entries without telling the issuer which credential it checks, and each award keeps a fixed slot, so an erased award stays revoked without keeping anything about the person. Slots are picked at random, and a list takes awards until it is half full.

**Data Integrity proofs** (`eddsa-rdfc-2022` and other cryptosuites) are not built; the JWT proof is the only proof.

## Public endpoints

All are anonymous, send `X-Content-Type-Options: nosniff`, `X-Robots-Tag: noindex, nofollow` and `Access-Control-Allow-Origin: *`, and are rate limited (group `LearningCredentials`, 120 requests a minute an address) when Orchard Core **Rate Limits** is on. A credential is found only by its 128-bit code.

| Address | Answers |
| --- | --- |
| `GET /learning/credentials/awards/{code}` | The credential in JSON-LD (`application/ld+json`), as issued. |
| `GET /learning/credentials/awards/{code}/jwt` | The signed credential (`application/jwt`), as issued. |
| `GET /learning/credentials/achievements/{definition id}` | The achievement of a published definition, as it is now. |
| `GET /learning/credentials/issuer` | The issuer profile. |
| `GET /learning/credentials/jwks` | The published keys of the key set (no private parts). |
| `GET /learning/credentials/keys/{kid}` | One public key, the address a token's `kid` names. |
| `GET /learning/credentials/status/{n}` | The `BitstringStatusListCredential`, computed from the awards on each request (`Cache-Control: no-store`); signed as a JWT when the request accepts `application/vc+jwt` or `application/jwt`, or at `/status/{n}/jwt`. |
| `GET /learning/credentials/verify/{code}` | The verification page. |

The base address is the site's **Base URL**. Without it nothing is awarded (`NoIssuerAddress`; the Credential Awards page says so to admins): the address of the request is never used, because its `Host` header is the client's choice, and a credential signed for that host would name an issuer, a key and a status list that outside verifiers fetch from it.

## Verification page

`/learning/credentials/verify/{code}` checks the credential on the server, as a verifier would: the token's signature against the published key its `kid` names, its `jti` and `iss` against the credential as issued, its expiry, and its revocation bit read from the status list. It shows the result (valid, expired, revoked, not valid or not found), the achievement and its image, the issuer, the dates in the site's time zone, links to the credential, the token and the keys, and the recipient's name only when the recipient chose to show it. It never shows the revocation reason.

## Pages

- **My credentials** (`/learning/credentials`, permission Access learning): the learner's awards with downloads of the JSON-LD and the JWT, the share link (the verification page) and **Show my name** / **Hide my name**. It is an entry of the storefront account menu, or of the user menu when the storefront account is off. Both entries are registered only when Learner Pages (`CrestApps.OrchardCore.Learning.LearnerPages`) is on (`LearnerPagesStartup`, `[RequireFeatures]`); without it, no menu links to the page.
- **The course completion panel** (Learning's `ICourseCompletionLinkProvider`): a course completion lists *Your credential: ...* for each award, not revoked, whose source is that enrollment, linking to My credentials.
- **Learning → Credential Awards** (permission **View digital credential awards**, `ViewCredentialAwards`, implied by **Award and revoke digital credentials**, `AwardCredentials`, security critical): an admin list with search by the recipient's user name and filters by credential, status and issue dates (days in your time zone); an award's page with the check and the credential as issued; **Award a credential** (by hand, with a reason) and **Revoke** (with a reason). A revocation shows on the verification page and in the status list at once.

## Data portability, personal data and integrations

- **Recipe and deployment.** The `LearningCredentialAwards` step carries awards as learner records: the deployment step is opt-in, needs **Export learner records**, reads 500 awards a page and records the export for the audit trail; rows name people by user name. The import skips an award whose code is already on the site, reports and skips a row whose recipient is not a user, whose status slot is outside the status lists (a list number below 1, or an index outside the list's 131,072 entries) or is held by another award, checks and takes the slot under the lock awards take and saves it there, and keeps the signed credential as issued. The credential's addresses and signature belong to the site that issued it: on another site, its signature verifies only against the issuing site's keys, which this step does not move. Definitions move with the `ContentDefinition` and `Content` steps.
- **Personal data.** The export lists the person's awards with the credential and the signed token. Erasure revokes each award (reason "Personal data erased"), keeps its status slot revoked, and removes the recipient's user id, the identity hash, the credential, the token and the award reason. It cannot reach copies the person downloaded, put in a wallet or shared: they stay signed by the site, and a verifier that checks the status list sees them revoked.
- **Notifications.** The notification types **Credential awarded** (`LearningCredentialAwarded`) and **Credential revoked** (`LearningCredentialRevoked`) tell the recipient, with a link to My credentials, when Notifications is on.
- **Workflows.** The **Credential Awarded** event (filter by definitions; correlation id the award id; input `AwardId`, `PublicId`, `DefinitionId`, `AchievementName`, `UserId`, `RuleKind`, `SourceId`, `IssuedUtc`, `ExpiresUtc`, never an e-mail address) and the **Award Credential** task (definition id, user id and an optional source id, Liquid allowed; outcomes Done and Failed).
- **Data source.** `Learning.Credentials` with the data set **Credential awards** (`CredentialAwards`): award, definition, credential name, achievement type, recipient user id, rule, source, awarded by, issued, expires, revoked, revoked on, revoked by and whether personal data was erased. It holds no e-mail address and no identity hash. It needs **View digital credential awards**.

## Extending

- `ICredentialAwardService` awards, revokes and reads awards; `ICredentialAwardObserver` (`AddCredentialAwardObserver<T>()`) hears awards and revocations after they are saved.
- `ICredentialRuleKind` (`AddCredentialRuleKind<T>()`) offers a rule kind in the editor; the module that owns what the rule watches calls `AwardAsync` with its own rule kind and source id.
- `ICredentialDefinitionCatalog` reads published definitions.

## Conformance

Conformance suite / certification: not run (needs an external account). The suite is 1EdTech's Open Badges 3.0 issuer certification, which needs a 1EdTech membership account. To run it, a person with that account signs in to 1EdTech's certification site, sets the test site's base URL to an address reachable from the internet, publishes a credential definition, awards a credential to a test user, downloads the signed credential (JWT) and the JSON-LD from My credentials, submits them to the issuer certification tests, and keeps the report. The module implements Open Badges 3.0 (issuer) on the W3C Verifiable Credentials Data Model 2.0 and makes no certification or conformance claim.

## Not built

- Data Integrity proofs (`eddsa-rdfc-2022`, `ecdsa-sd-2023` and others); the JWT proof is the only proof.
- The 1EdTech Revocation List status method; the Bitstring Status List is used instead.
- Open Badges 3.0 as a host or displayer (the Open Badges API, OAuth 2.0 dynamic registration, accepting credentials from other issuers), CLR 2.0, and `EndorsementCredential`.
- Recipient identifiers other than the hashed e-mail address (DIDs, `sourcedId`), and a subject `id`.
- Wallet hand-off protocols (OpenID for Verifiable Credential Issuance, CHAPI) and QR codes; the learner downloads the files.
- JSON-LD processing: the contexts are named, not fetched or expanded.
- Audit Trail events.
