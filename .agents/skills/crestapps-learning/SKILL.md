---
name: crestapps-learning
description: >
  Skill for extending the CrestApps.OrchardCore Learning module (CrestApps.OrchardCore.Learning, Abstractions and Core)
  from another module: adding a course item kind with services.AddCourseItemKind<TPart> (a content type with the
  CourseItemPart, its completion modes, player shape and whether it produces a grade), reporting results with
  ILearnerProgressService.RecordResultAsync, adding a release rule (ICourseItemReleaseRule, AddCourseItemReleaseRule) or a
  completion criterion (ICourseCompletionCriterion, AddCourseCompletionCriterion), granting access from another record
  with an enrollment source (AddEnrollmentSource, IEnrollmentService.EnrollAsync with Source/SourceId,
  RevokeGrantAsync), reacting after commit with IEnrollmentObserver, and the pitfalls: never gating lessons yourself
  (the content access handler answers ViewContent), never trusting a score the browser reports, keeping one open
  enrollment per person and course, and the Secure Media permission that opens every lesson file. Use this skill
  whenever the request is to "add a lesson type", "add a course item kind", "enroll people when they buy or subscribe",
  "unlock a lesson when", "complete a course when", or integrate a module with Learning.
license: Apache-2.0
metadata:
  author: CrestApps Team
---

# Extending Learning

Learning owns courses, lessons (course items), enrollments, progress and completion. Other modules plug into it; they never keep their own enrollment, seat or course-access records. Reach it through `CrestApps.OrchardCore.Learning.Abstractions` (contracts, models, registration extensions) and `CrestApps.OrchardCore.Learning.Core` (stores and default services). A module never uses a type from the Learning module assembly.

## Add a course item kind

A kind is a content type with the `CourseItemPart` plus a part of its own. Register it in your startup:

```csharp
services.AddCourseItemKind<QuizPart>("Quiz", kind =>
{
    kind.DisplayName = LocalizationSource.Create<Startup>("Quiz");
    kind.LearnerName = LocalizationSource.Create<Startup>("Quiz");
    kind.ContentType = "QuizLesson";
    kind.CompletionModes.Add(CompletionModes.Passed);
    kind.CompletionModes.Add(CompletionModes.Submitted);
    kind.ProducesGrade = true;
    kind.PlayerShape = "QuizPart_Player";
    kind.Icon = "fa-solid fa-circle-question";
});
```

- The first completion mode is the default. A kind that only supports some modes refuses the others in the editor.
- Create the content type in your migration with `CourseItemPart` (settings: `CourseItemPartSettings.Kind`) and make it containable by the course's List part, so **New** on the course creates it.
- The player renders `PlayerShape` for the item, after the content access handler has allowed it.
- Report a result with `ILearnerProgressService.RecordResultAsync(enrollment, new CourseItemResult { ... })`. Compute the score on the server. A result the browser reported (a learning package) sets `SelfReported`; it is shown as such, and counts toward a certification only when the item's `AllowSelfReported` is on.

## Grant access from another record

An order line, a subscription or a class registration grants access through an **enrollment source**:

```csharp
services.AddEnrollmentSource("Order", LocalizationSource.Create<Startup>("Order"));
```

Then call `IEnrollmentService.EnrollAsync(new EnrollmentRequest { UserId, CourseId, Source = "Order", SourceId = orderLineId })`. The same source and source id never enrolls twice. A refund or cancellation calls `RevokeGrantAsync("Order", orderLineId, actor, reason)`; the enrollment is withdrawn when its last grant goes.

## Rules

- **Release rule** (`ICourseItemReleaseRule`, `AddCourseItemReleaseRule<T>()`): `Name` and `EvaluateAsync(ReleaseRuleContext)` returning a `ReleaseDecision` (`ReleaseDecision.Released`, or `ReleaseDecision.Locked(reason, availableFromUtc)`). The rule's settings come from `LearningRuleSettings` on the item or section.
- **Completion criterion** (`ICourseCompletionCriterion`, `AddCourseCompletionCriterion<T>()`): `Name` and `IsMetAsync(CompletionCriterionContext)`. The course completes when every required item is done and every criterion of the course is met.

## React to changes

Implement `IEnrollmentObserver` (every method has a default) and register it with `AddEnrollmentObserver<T>()`. Observers run after the change is saved; send messages from a deferred task (`ShellScope.AddDeferredTask`) so a failed commit sends nothing. `ICourseItemHandler` (`AddCourseItemHandler<T>()`) hears about item events.

## Pitfalls

- Do not gate lessons yourself. The Learning content access handler answers `ViewContent` for course items, so the display route, the content API, GraphQL, data sources and Secure Media all follow it.
- Never give **View media content in all folders** (`ViewMediaContent`) to the Anonymous or Authenticated role: it passes Secure Media before the folder check and opens every lesson file. Give `ViewRootMediaContent` or a folder's permission instead.
- A person has at most one open enrollment per course and offering. Taking a course again is a new enrollment (the next `Cycle`); the old one stays as the record.
- Show times with `ILocalClock`; enrollment dates are stored in UTC.

See `src/CrestApps.Docs/docs/modules/learning.md` for the full reference.
