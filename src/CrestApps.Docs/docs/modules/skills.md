---
sidebar_label: Skills
title: Skills
description: "The shared skill catalog: skills held at a 1 to 5 proficiency with effective dates, certifications that expire, certificates, the daily expiry check, the contracts other modules build on, and how Contact Center shares the catalog."
user_manual:
  - user-manual/workforce/skills-and-certifications
---

The **Skills** module keeps one skill catalog for the site. Contact Center routing and workforce scheduling read the same skills, so a person's skills are set once. It adds:

- **Skills**: a name, a category, a description and an optional required certification.
- **Skill assignments**: a person holds a skill at a proficiency from 1 to 5, optionally between two dates. A person is an Orchard Core user, or a workforce worker who may have no user account.
- **Certifications**: a licence that expires after a number of months, with reminder days before expiry.
- **Certification records** (certificates): one person's certificate, with its number, issue and expiry dates, a copy in the media library, and a status (`Pending`, `Valid`, `Expired`, `Revoked`).
- A **daily check** that raises reminders, marks certificates expired, and tells other modules about assignments that start or end that day.

The admin screens are described in the User Manual: [Skills and Certifications](../user-manual/workforce/skills-and-certifications.md).

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Skills` | Skills | `CrestApps.OrchardCore.Users`, `CrestApps.OrchardCore.Resources` |

Projects: `CrestApps.OrchardCore.Skills.Abstractions` (models and contracts), `CrestApps.OrchardCore.Skills.Core` (stores, indexes, services), `CrestApps.OrchardCore.Skills` (the module).

## Storage

Every record is a YesSql document in the `Skills` collection: `Skill`, `SkillAssignment`, `Certification`, `CertificationRecord`, and one `SkillsDailyCheckpoint` that remembers the last day the daily check ran. Day columns in the indexes are stored as `yyyyMMdd` numbers, so a day comparison never shifts with a time zone.

| Index | Columns |
| --- | --- |
| `SkillIndex` | ItemId, Name, NormalizedName, Category, Enabled |
| `CertificationIndex` | ItemId, Name, NormalizedName, Enabled |
| `SkillAssignmentIndex` | ItemId, SubjectKind, SubjectId, SkillId, Proficiency, EffectiveFrom, EffectiveTo |
| `CertificationRecordIndex` | ItemId, SubjectKind, SubjectId, SubjectName, CertificationId, Number, Status, IssuedOn, ExpiresOn |

Names are unique without regard to case or surrounding spaces (`NormalizedName`). A person holds each skill at most once.

## Services

| Contract | What it does |
| --- | --- |
| `ISkillManager` | The skill catalog (`ICatalogManager<Skill>`): validate, create, update, delete, `FindByNameAsync`, `GetEnabledAsync`, `SearchAsync`. |
| `ICertificationManager` | The certification catalog, with the same members. |
| `ISkillAssignmentService` | `GetAssignmentsAsync` (every assignment of a person), `GetEffectiveAsync` (the ones that count on a day: inside their dates, the skill enabled, and the required certification valid that day), `SetAsync` (replace a person's set), `AssignAsync`, `RemoveAsync`, `FindHoldersAsync` (who holds a skill at a proficiency on a day) and `HoldsAsync`. |
| `ICertificationRecordService` | `SaveAsync` (works out the expiry date from the certification's validity when a record has an issue date and no expiry date, and refuses a valid record without its copy when the certification requires one), `VerifyAsync`, `RevokeAsync`, `DeleteAsync`, `GetForSubjectAsync`, `IsValidOnAsync` and `PageAsync`. |

A person is a `SkillSubject`: `SkillSubject.User(userId)` or `SkillSubject.Worker(workerId)`.

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
| `ISkillAssignmentHandler` | In the request, after a person's skills changed and before the session commits; also once a day for each person whose assignment starts or ends that day (`EffectiveDatesOnly`). | Keeping a copy of a person's skills in step, as Contact Center does for agents. |
| `ICertificationExpiryObserver` | From the daily check: `ExpiringAsync` once per reminder offset and expiry date, `ExpiredAsync` once when a certificate is marked expired. | Notifications. A failing observer is logged and doesn't stop the check. |
| `ICatalogEntryHandler<Skill>`, `ICatalogEntryHandler<Certification>` | The catalog managers' lifecycle. | Reacting to catalog changes. |

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

## Permissions

| Permission | Allows |
| --- | --- |
| `ManageSkills` (Manage skills and certifications) | The skill and certification lists. Implies the two below. |
| `AssignSkills` (Assign skills and record certifications) | The Skills section of the user editor, and adding, checking and withdrawing certificates. |
| `ViewSkills` (View skills and certifications) | The certificate list. |

## Contact Center

When the Skills feature and Contact Center are both on, Contact Center uses the shared catalog. No migration is needed and nothing has to be enabled in a particular order.

- **Skills**: with the Work Distribution (Queues) feature on, the Contact Center skill store reads and writes the shared catalog. The first time it is read, Contact Center skills that aren't in the catalog are copied into it with the same ids. A skill whose name the catalog already has is left out, because Contact Center names skills by their name. Every catalog change is mirrored into the Contact Center store, so turning Skills off later loses nothing.
- **Agents**: with the Agents feature on, an agent's routing proficiencies (`AgentProfile.SkillProficiencies`, keyed by skill name) and the user's assignments stay in step both ways. Routing still reads the agent profile, so it does no extra queries. A fingerprint of the proficiencies on the profile means a save that didn't change them (most saves are presence changes) touches nothing. A Contact Center skill name that isn't in the catalog stays only on the agent. The **Contact Center shared skills backfill** background task pushes the agents saved before Skills was turned on, 100 every five minutes, until all are done.
