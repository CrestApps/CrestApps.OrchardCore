---
sidebar_label: Skills
title: Skills
description: "The shared skill catalog: skills held at a 1 to 5 proficiency with effective dates, certifications that expire, certificates, the daily expiry check, the contracts other modules build on, and how Contact Center shares the catalog."
user_manual:
  - user-manual/workforce/skills-and-certifications
  - user-manual/learning/competencies
  - user-manual/learning/certifications
---

The **Skills** module keeps one skill catalog for the site. Contact Center routing and workforce scheduling read the same skills, so a person's skills are set once. It adds:

- **Skills**: a name, a category, a description and an optional required certification.
- **Skill assignments**: a person holds a skill at a proficiency from 1 to 5, optionally between two dates. A person is an Orchard Core user, or a workforce worker who may have no user account.
- **Certifications** (shown as **Certification Types**): a licence that expires after a number of months, with reminder days before expiry.
- **Certification records** (certificates): one person's certificate, with its number, issue and expiry dates, a copy in the media library, and a status (`Pending`, `Valid`, `Expired`, `Revoked`).
- A **daily check** that raises reminders, marks certificates expired, and tells other modules about assignments that start or end that day.

The admin screens are described in the User Manual: [Skills and Certifications](../user-manual/workforce/skills-and-certifications.md).

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Skills` | Skills | `CrestApps.OrchardCore.Users`, `CrestApps.OrchardCore.Resources` |

Projects: `CrestApps.OrchardCore.Skills.Abstractions` (models and contracts), `CrestApps.OrchardCore.Skills.Core` (stores, indexes, services), `CrestApps.OrchardCore.Skills` (the module).

## Issued certificates

The site can issue certificates itself, numbered and provable in public, instead of only recording certificates someone else issued.

- **`ICertificateIssuer`** (`IssueAsync`, `RenewAsync`, `RevokeAsync`, `FindByVerificationCodeAsync`) creates a record with `Source`, `SourceId` and an evidence summary, a number from `INumberSequence` when the certification says so (prefix and six digits, after any hand-recorded number with the same prefix), and a 16-character verification code (80 random bits, separate from the number). Issuing is idempotent: one certificate per certification, source and source id (a revoked one does not count), and one renewal per certificate, under a lock, committed inside it.
- A **renewal** is a new record linked both ways (`PreviousRecordId`, `RenewedByRecordId`); the old one keeps its dates. The daily check sends no reminder for a renewed certificate and expires it quietly.
- **Revocation** records the reason, who and when, without touching the notes. A certificate the site issued is withdrawn, never edited or deleted.
- **`ICertificationRecordObserver`** (issued, renewed, revoked) runs after the commit; workflows (Certificate Issued, Renewed, Revoked events) and webhooks (`skills.certificate.issued`, `.renewed`, `.revoked`, without the code) listen.

The **Skills: Issued Certificates** feature (`CrestApps.OrchardCore.Skills.IssuedCertificates`) prints and proves them:

- The **Certificate Template** content type: paper (Letter or A4, landscape or portrait), an installed font family (embedded in each PDF, for letters beyond Latin), colors, a border, a background image (media field), the QR code position and size, and text blocks in Liquid (`Certificate.HolderName`, `Number`, `IssuedOn`, `ExpiresOn`, `Evidence`, `VerificationCode`, `VerificationUrl`; `Certification.Name`, `Issuer`, `Description`). A certification picks its template on its editor; without one the default design prints.
- The certificate PDF, built with the PDF building block (`IPdfDocumentRenderer` in `CrestApps.OrchardCore.Pdf.Abstractions`): from the verification page, from **My certifications**, and for staff from the certificates list (a revoked one is marked revoked).
- The **public verification page** `/certificates/verify` (by code, `/certificates/verify/{code}`), behind Orchard Core rate limits (30 lookups a minute per visitor when Rate Limits is on), marked `noindex`. It shows the least that proves the claim: holder, certification, issuer, number, dates and status.

The certification editor renders through `IDisplayManager<Certification>`, so modules add cards to it with display drivers (Learning Certifications adds **Earned through learning**). The `Skills` data source (Certifications and Certificates data sets, never the code) and the personal-data contributor (exports skills and certificates; anonymizing keeps issued certificates without the name and evidence) come with the module.

## Storage

Every record is a YesSql document in the `Skills` collection: `Skill`, `SkillAssignment`, `Certification`, `CertificationRecord`, and one `SkillsDailyCheckpoint` that remembers the last day the daily check ran. Day columns in the indexes are stored as `yyyyMMdd` numbers, so a day comparison never shifts with a time zone.

| Index | Columns |
| --- | --- |
| `SkillIndex` | ItemId, Name, NormalizedName, Category, Enabled |
| `CertificationIndex` | ItemId, Name, NormalizedName, Enabled |
| `SkillAssignmentIndex` | ItemId, SubjectKindName, SubjectId, SkillId, Proficiency, EffectiveFrom, EffectiveTo (and the legacy number column `SubjectKind`) |
| `CertificationRecordIndex` | ItemId, SubjectKindName, SubjectId, SubjectName, CertificationId, Number, Status, IssuedOn, ExpiresOn (and the legacy number column `SubjectKind`) |

Names are unique without regard to case or surrounding spaces (`NormalizedName`). A person holds each skill at most once.

## Services

| Contract | What it does |
| --- | --- |
| `ISkillManager` | The skill catalog (`ICatalogManager<Skill>`): validate, create, update, delete, `FindByNameAsync`, `GetEnabledAsync`, `SearchAsync`. |
| `ICertificationManager` | The certification catalog, with the same members. |
| `ISkillAssignmentService` | `GetAssignmentsAsync` (every assignment of a person), `GetEffectiveAsync` (the ones that count on a day: inside their dates, the skill enabled, and the required certification valid that day, including skills held under another subject that names the same person), `SetAsync` (replace a person's set), `AssignAsync`, `RemoveAsync`, `FindHoldersAsync` (who holds a skill at a proficiency on a day) and `HoldsAsync`. |
| `ICertificationRecordService` | `SaveAsync` (works out the expiry date from the certification's validity when a record has an issue date and no expiry date, and refuses a valid record without its copy when the certification requires one), `VerifyAsync`, `RevokeAsync`, `DeleteAsync`, `GetForSubjectAsync`, `IsValidOnAsync` and `PageAsync`. |

A person is a `SkillSubject`: `SkillSubject.User(userId)` or `SkillSubject.Worker(workerId)`. The kind is an open string: `SkillSubjectKind.User` and `SkillSubjectKind.Worker` are the well-known kinds, and a module adds its own with `new SkillSubject("MyKind", id)` (and an `ISkillSubjectLinkProvider` that links it to the user or worker it names) without changing Skills. Records saved while the kind was a number are still read (`SkillSubjectKindJsonConverter` reads `0` as `User` and `1` as `Worker`), and the Skills migration adds the `SubjectKindName` index column and fills it in from the number, so nothing is dropped. Until that re-index has reached a row, the queries match it by the number column (`SubjectKind`: 0 for a user, 1 for a worker), so an older assignment or certification is never hidden and assigning the same skill again updates it rather than adding a second one.

```csharp
var subject = SkillSubject.User(userId);

var result = await assignmentService.AssignAsync(subject, new SkillAssignmentInput
{
    SkillId = spanish.ItemId,
    Proficiency = 4,
    EffectiveFrom = new DateOnly(2026, 12, 1),
}, actor: "hr-import");

var holders = await assignmentService.FindHoldersAsync(spanish.ItemId, minimumProficiency: 3, today, SkillSubjectKind.Worker);
```

## Extension points

| Contract | When it runs | Use it for |
| --- | --- | --- |
| `ISkillSubjectLinkProvider` | Whenever a person's effective skills are read (`GetEffectiveAsync`, `HoldsAsync`). | Telling Skills which other subjects name the same person. Workforce links a worker profile and its user, so a skill Contact Center gives an agent's user counts for the worker in scheduling, coverage and Dispatch matching. When both hold a skill, the higher proficiency counts. |
| `ISkillAssignmentHandler` | In the request, after a person's skills changed and before the session commits; also once a day for each person whose assignment starts or ends that day (`EffectiveDatesOnly`). | Keeping a copy of a person's skills in step, as Contact Center does for agents. |
| `ICertificationExpiryObserver` | From the daily check: `ExpiringAsync` once per reminder offset and expiry date, `ExpiredAsync` once when a certificate is marked expired. | Notifications. A failing observer is logged and doesn't stop the check. |
| `ICatalogEntryHandler<Skill>`, `ICatalogEntryHandler<Certification>` | The catalog managers' lifecycle. | Reacting to catalog changes. |
| `SkillSubjectSkillsDisplayDriverBase<TModel>` (in `CrestApps.OrchardCore.Skills.Core`) | When the editor of a record that holds skills is built and updated. | Adding a Skills section to that editor: one proficiency choice per enabled skill, shown only to users with **Assign skills**. Derive a small driver that says who holds the skills (`GetSubject`, an empty subject hides the section), names the section's shape (`ShapeType`, whose view takes a `SubjectSkillsViewModel`) and may move it (`Location`, `Content:10` by default), pass an `IStringLocalizer` to the base constructor (it titles the section's card "Skills", placed with `CardPosition`, `Location`'s position by default), and register it with `services.AddDisplayDriver<TModel, TDriver>()`. The assignments are written while the editor is updated, so the editor cancels the session when the record isn't saved. Skills uses it for the user editor (`UserSkills_Edit`), and Workforce for the worker editor (`WorkerSkills_Edit`). |

## Daily check

The **Skills and certificates daily check** background task wakes every hour and runs once per day in the site's time zone:

1. Valid certificates whose expiry date is within the largest reminder offset are read. When a certificate reaches a reminder offset, every offset already reached is marked raised and only the nearest is announced, so a certificate entered a week before it expires raises one reminder rather than one per offset. When the expiry date moves (the certificate was renewed), its reminders start again.
2. The day after a certificate's expiry date, its status becomes `Expired`.
3. People whose assignment starts that day or ended the day before are passed to the `ISkillAssignmentHandler`s.

## Workflows

With Workflows on, the module adds **Certificate Expiring** (`DaysBefore` in the input), **Certificate Expired**, **Skills Changed** and the **Assign Skill** task. The events start after the change is committed. Certificate events can be limited to some certifications; their input holds `CertificationRecordId`, `CertificationId`, `CertificationName`, `SubjectKind`, `SubjectId`, `SubjectName`, `Number` and `ExpiresOn`. **Skills Changed** holds `SubjectKind`, `SubjectId`, `SkillIds`, `ChangedSkillIds`, `RemovedSkillIds`, `EffectiveDatesOnly` and `Actor`.

## Recipes and deployment

The `Skills` step imports certifications and skills (by id, or by name when the id is new), then assignments and certificates. A skill can name its certification with `RequiredCertificationName` and an assignment its skill with `skillName`, so a recipe written for one site works on another.

```json
{
  "steps": [
    {
      "name": "Skills",
      "Certifications": [
        { "name": "Gas safety", "issuer": "Gas Board", "validityMonths": 12, "reminderDays": [60, 30, 7], "requiresDocument": true }
      ],
      "Skills": [
        { "name": "Spanish", "category": "Languages" },
        { "name": "Boiler servicing", "category": "Heating", "RequiredCertificationName": "Gas safety" }
      ],
      "Assignments": [
        { "subjectKind": "User", "subjectId": "4x2kf6wqmq7a9y3p0v1d8hn5bt", "skillName": "Spanish", "proficiency": 4 }
      ],
      "CertificationRecords": [
        { "subjectKind": "User", "subjectId": "4x2kf6wqmq7a9y3p0v1d8hn5bt", "subjectName": "John Smith", "certificationName": "Gas safety", "issuedOn": "2026-03-15", "status": "Valid" }
      ]
    }
  ]
}
```

The **Skills and certifications** deployment step exports every skill and certification, and optionally the assignments and certificates.

## Import skills from a file

With [Content Transfer](./content-transfer) on, skills and competency frameworks are a ContentTransfer import target (`SkillsImportTarget`, named `Skills.Skill`, checked by **Manage skills and certifications** (`ManageSkills`); see [Import targets that are not content types](./content-transfer#import-targets-that-are-not-content-types)). They share its background run, progress, pause and resume, and file of failed rows. The **Import** button on **Content** → **Skills** opens the import (`/Admin/import/contents/Skills.Skill`); it shows only when ContentTransfer is on. The columns are `Name`, `Category`, `Description` and `Enabled`. `SkillImportRowReader` (Skills.Core) reads each row and `SkillImporter` saves it:

- A **Moodle competency framework export** (Moodle's *Export competency framework*, known by its `isframework` column): the framework row's short name is the category, and each competency becomes a skill named by its short name (or ID number), its description made plain text. Nested competencies (`parentidnumber`) are imported flat.
- A **plain file** with a `Name` column and optional `Category`, `Description` and `Enabled` columns, in any case.

Skills are matched by name in any case. A new name adds a skill; a known one in the same category, or with none, is updated (an empty cell keeps the value); a known one in another category, and a name repeated in the same batch, fail with the reason, and are listed in the failed-rows file. Importing a file again changes nothing. The framework of a Moodle export is kept on the import between batches, so a resumed import keeps it. Learning shows skills as competencies and categories as competency frameworks; see [Learning Certifications](./learning-certifications#competencies).

## Reports

With Reports on, the **Skill holders** report (`skills-skill-holders`, View skills) shows who holds each enabled skill today: holders and average level per skill, and one row per person and skill with the level, the effective dates and who gave it, up to 5,000 rows. The **Category (framework)** filter (`SkillsReportFilters.Category`) narrows it to one category; the date range counts the skills given or changed in it.

## Permissions

| Permission | Allows |
| --- | --- |
| `ManageSkills` (Manage skills and certifications) | The skill and certification lists. Implies the two below. |
| `AssignSkills` (Assign skills and record certifications) | The Skills section of the user editor, and adding, checking and withdrawing certificates. |
| `ViewSkills` (View skills and certifications) | The certificate list. |

## Contact Center

Contact Center keeps its skills in this catalog: the Contact Center Agents feature depends on Skills, so turning Contact Center on turns Skills on. No migration is needed.

- **Skills**: with the Work Distribution (Queues) feature on, the Contact Center skill store (`IContactCenterSkillStore`) reads and writes the shared catalog. The first time it is read, skills Contact Center kept in its own store before are copied into the catalog with the same ids, so queues and agents that name them keep working. A skill whose name the catalog already has is left out, because Contact Center names skills by their name. Nothing is copied back: the old store is only read for that first copy. A change to a catalog skill clears Contact Center's cached skills.
- **Screens**: skills are edited on the Skills module's pages (`Admin/skills`). Contact Center's menu item **Skills** opens them, and its old addresses (`Admin/contact-center/skills`) redirect there. A role with **Manage Contact Center skills** may also manage and assign the shared skills (`ContactCenterSkillsAuthorizationHandler`), so roles set up before keep their access.
- **Agents**: with the Agents feature on, an agent's routing proficiencies (`AgentProfile.SkillProficiencies`, keyed by skill name) and the user's assignments stay in step both ways. Routing still reads the agent profile, so it does no extra queries. A fingerprint of the proficiencies on the profile means a save that didn't change them (most saves are presence changes) touches nothing. A save that did change them is written to the catalog after it is committed, in a scope of its own (a deferred task), never inside the agent's save: a failed catalog query cancels the whole YesSql session, so a catalog that can't be read, for example because the Skills migration timed out, would otherwise throw away every agent state change. A sync that fails is logged and tried again at the agent's next save. A new agent with no proficiencies takes the user's shared skills the same way, just after it is created. A Contact Center skill name that isn't in the catalog stays only on the agent. The skills are stored on the agent's user; a Workforce worker profile linked to the same user holds them too. The **Contact Center shared skills backfill** background task pushes the agents saved before Skills was turned on, 100 every five minutes, until all are done.
