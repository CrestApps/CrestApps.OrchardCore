---
sidebar_label: Learning Programs
title: Learning Programs
description: "Learning paths made of ordered course sets, required learning for user audiences with due dates, reminders, escalation and exemptions, and the state of a manager's team."
user_manual:
  - user-manual/learning/required-learning
---

**Learning Programs** (`CrestApps.OrchardCore.Learning.Programs`, with `Learning.Programs.Abstractions` and `Learning.Programs.Core`) adds what sits above a single course in [Learning](./learning): learning paths and required learning. It has three features.

| Feature | Id | Depends on |
| --- | --- | --- |
| Learning Paths | `CrestApps.OrchardCore.Learning.Programs` | Learning |
| Required Learning | `CrestApps.OrchardCore.Learning.Programs.RequiredLearning` | Learning Paths, [User Audiences](./users#user-audiences) |
| Team Learning | `CrestApps.OrchardCore.Learning.Programs.TeamLearning` | Required Learning, [Staff](./staff) |

## Learning paths

A learning path is a content item of the **Learning Path** type (`LearningPath`, defined in code with `TitlePart` and `LearningPathPart`). `LearningPathPart.Sets` holds the ordered course sets:

| Set property | Meaning |
| --- | --- |
| `Connector` | How the set joins the set before it: `Then` starts a new stage that opens once the earlier stages are complete; `And` and `Or` keep it in the same stage. And binds tighter than or, so `A and B or C` is `(A and B) or C`. |
| `Join` | `All` courses, `Any` one, or `AtLeast` `MinimumCount` of them. |
| `CourseIds` | The courses of the set. |

`AllowSelfStart` puts a **Start this path** button on the path's page for signed-in people who can see it.

`ILearningPathService` starts a person on a path (`StartAsync`, idempotent per path and person), works out their progress from their completed enrollments (`EvaluateAsync`, `EvaluateCourseAsync`) and pages the people on a path. Starting a person enrolls them in the courses of the open stage with the enrollment source `Learning.LearningPath`; each completed stage enrolls the next. The rules live in `LearningPathRules` (`Stages`, `IsSetComplete`, `IsStageComplete`, `OpenStage`). A person's progress is a `LearningPathProgress` record in the `LearningPrograms` collection, saved under a lock per path and person before any enrollment, because enrolling commits the session. `ILearningPathObserver.CompletedAsync` hears a completed path; register one with `services.AddLearningPathObserver<T>()`.

The path editor's **People** page (`/Admin/learning/paths/{id}/people`) lists the people on a path with their stage and starts a person by user name.


### Hidden courses

A course that is not published (hidden or deleted) is never enrolled, and an **All** set does not wait for it (`LearningPathRules` takes the available courses, as `DefaultLearningPathService` reads them), so a Moodle learning plan whose sets mix hidden and visible courses completes when its visible courses do. A hidden course never completes anything by being left out: a set whose courses are all hidden waits for them, an **At least** set keeps its minimum, and a path whose courses are all hidden does not complete when someone starts it. A digital credential, education credits or competencies are issued on a completed path only when the path's own rules hold over all its courses, hidden ones included, counting only completions that are evidence (`CompletionEvidence.SupportsIssuing`; `ILearningPathService.SupportsIssuingAsync`). Every stage must be met that way: an **All** set needs each of its courses, hidden ones too, and an **At least** set needs its minimum. A hidden course left out of the progress is no evidence.

## Required learning

A required learning rule (`LearningRequirement`) requires a target of a [user audience](./users#user-audiences) and of named people:

| Setting | Meaning |
| --- | --- |
| Target | A `Course`, a `LearningPath` or, with Learning Certifications on, a `CertificationPath`. Targets are open through `IRequiredLearningTarget` (`Kind`, `GetOptionsAsync`, `GetDisplayTextAsync`, `StartAsync`, `GetStatusAsync`), registered with `services.AddRequiredLearningTarget<T>()`. |
| Audience and people | Everyone in the audience, plus user ids named on the rule. |
| Due | No due date, a fixed day, some days after the learning is required, or some days after the person's hire date on their staff record (or after it is required when there is none). Due at the last second of the day in the site's time zone. |
| Reminders | Days before the due date. When the upkeep runs late, only the nearest reminder is sent and the earlier ones are marked as sent. |
| Escalation | Days after the due date to tell the person's manager, the manager on their staff record. |

Each person under a rule has a `RequiredLearningAssignment`: **Required** (not started), **In progress**, **Completed**, **Overdue**, **Exempt** or **No longer required** (they left the audience before completing it; the record stays). The state follows the target: completing the course or the path, or holding the certificate, completes it through the enrollment and path observers.

`IRequiredLearningService.RunAsync` applies a rule: it requires the learning of people who joined the audience (starting them on the target with the due date, enrollment source `Learning.RequiredLearning`), marks people who left it, and works out every state again. `PreviewAsync` lists who a rule covers without changing anything, `ExemptAsync` and `RemoveExemptionAsync` record exemptions, and `SendRemindersAsync` sends reminders and escalations and marks overdue states. `IRequiredLearningObserver` hears every change; register one with `services.AddRequiredLearningObserver<T>()`.

The **Required learning upkeep** background task runs at a quarter past every hour: it runs each enabled rule that has not run for 20 hours and sends the reminders and escalations that are due. Membership is read when a rule runs, so a person who joins an audience is required within a day, or at once with **Run now**.

An exemption keeps a reason, who granted it and an optional evidence file in the protected file area `RequiredLearningEvidence` (folder `LearningEvidence` of the tenant's `App_Data`). Only the person and people with **Exempt people from required learning** can open it.

## Team learning

**Learning** → **Team Learning** lists the people who report to the signed-in user on their staff records, directly or not (`IStaffHierarchy.GetReportsAsync`), with the state and due date of each piece of their required learning. It shows no answers or attempts.

## Pages

| Page | Address | Permission |
| --- | --- | --- |
| Required learning list, editor, people, preview, exemption | `/Admin/learning/required` | `ManageRequiredLearning`; exempting needs `ExemptFromRequiredLearning` |
| Team learning | `/Admin/learning/team` | `ViewTeamLearning` |
| People on a path | `/Admin/learning/paths/{id}/people` | Edit the path |
| My required learning and paths | `/learning/required` | Signed in; an entry of the storefront account menu, framed by it |

## Integrations

- **Notifications**: Learning required, Required learning due soon, Required learning overdue (to the person, and to their manager on escalation) and Learning path completed.
- **Workflows**: the events Learning Required, Required Learning Due Soon, Required Learning Overdue, Required Learning Exemption Granted and Learning Path Completed; the tasks Require Learning (adds a person to a rule and runs it) and Grant Required Learning Exemption.
- **Data source**: `LearningPrograms`, with the data sets Path progress, Required learning and State per person.
- **Recipes**: the `LearningRequirements` step creates or updates rules, matched by id or name; the **Required learning** deployment step exports them without the states of people.

```json
{
  "name": "LearningRequirements",
  "Requirements": [
    {
      "name": "Safety onboarding",
      "targetKind": "LearningPath",
      "targetId": "<path content item id>",
      "audienceName": "field-veterans",
      "dueRule": "DaysAfterHire",
      "dueDays": 30,
      "reminderDays": [14, 7, 1],
      "escalateAfterDays": 3
    }
  ]
}
```

- **Personal data**: a person's path progress and required learning states, with the exemption reason and evidence name, are exported and deleted with the evidence files.
- **Education credits**: with Learning Certifications on, a learning path with the Education Credits part awards its credits when a person completes it, and only when the path's rule holds with the courses whose completion supports issuing (`ILearningPathService.SupportsIssuingAsync`, built on `CompletionEvidence.SupportsIssuing`): a course that was failed, is under review or rests on a result the browser reported does not count. The path's digital credential follows the same rule, so neither can meet a certification through such a course.
