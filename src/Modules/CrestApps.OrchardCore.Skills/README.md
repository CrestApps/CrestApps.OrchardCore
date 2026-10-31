# CrestApps Orchard Core Skills

One skill catalog for the whole site, the base of workforce management.

## Features

| Feature | Id | What it adds |
| --- | --- | --- |
| Skills | `CrestApps.OrchardCore.Skills` | Skills held at a proficiency from 1 to 5 with effective dates, certifications that expire, people's certificates, a daily expiry check, a Skills section in the user editor, workflows, and a `Skills` recipe and deployment step. |

The feature depends on `CrestApps.OrchardCore.Users` and `CrestApps.OrchardCore.Resources` (for the user picker).

## Admin

- **Workforce > Skills**: the skill catalog.
- **Workforce > Certifications**: certifications, their validity and reminder days.
- **Workforce > Certificates**: the certificates people hold, with filters for certification, status and expiring in 60 days.
- **Access Control > Users > Edit**: the user's skills.

## Extending

Inject `ISkillAssignmentService` to give people skills and to find who holds one, `ICertificationRecordService` for certificates, and `ISkillManager` / `ICertificationManager` for the catalogs. Implement `ISkillAssignmentHandler` to react to a person's skills changing, and `ICertificationExpiryObserver` to hear about reminders and expiries.

When Contact Center is on, its skills and agent proficiencies use this catalog with no extra setup.

## Documentation

- User Manual: [Skills and Certifications](../../CrestApps.Docs/docs/user-manual/workforce/skills-and-certifications.md)
- Technical Manual: [Skills](../../CrestApps.Docs/docs/modules/skills.md)
