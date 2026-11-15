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

## Assessments

- Add a question type with `services.AddQuestionType(...)` and an `IQuestionScorer`; the scorer snapshots the key into the attempt item and scores it there. Never read the question again after the attempt starts.
- React to attempts with `IAttemptObserver` (`services.AddAttemptObserver<T>()`); observers run after the commit.
- Keep any secret a learner types (a code, a key) in `LearningSecretStore` with your own kind, and check it with `LearningSecretVerifier`, which limits wrong tries.
- Write to an attempt or an enrollment only through `IAssessmentAttemptService` / `IEnrollmentService` / `ILearnerProgressService`: they take the per-record lock, read again, commit and then notify.

## Certifications

- A certificate belongs to Skills. Never store one in Learning: call `ICertificateIssuer` (Skills.Abstractions) with a `Source` and a `SourceId` that names what earned it once (Learning paths use `{progressId}:{cycle}`); issuing again with the same three returns the same certificate.
- A renewal is `RenewAsync(previousRecordId, ...)`: a new record linked both ways. Never change the dates of an issued certificate.
- Add a requirement kind with `services.AddCertificationRequirement<T>()` (`ICertificationRequirement`: find the evidence since a time, list who met it since a time so the upkeep can catch up).
- Observers of Learning, Assessments and Skills run after the commit and log their failures: anything that must happen once (issuing) must also be caught up by an upkeep.
- A service that commits (the issuer, `IEnrollmentService.EnrollAsync`) makes the session forget what it read: read a document again by id before saving it after such a call, or YesSql inserts a second copy.
- Resolve a service lazily (`IServiceProvider`) from a handler or observer that the service itself depends on through a factory registration; a cycle through a factory is not detected and recurses until the request hangs.
- Add a card to the certification editor with a `DisplayDriver<Certification>` that overrides `BuildPrefix`: every driver of a model gets the model's name as its prefix.
- Offer an action on an enrollment's review card with `IEnrollmentReviewActionProvider` (Learning.Abstractions).

## Programs and required learning

- A learning path is content (`LearningPathPart`); a person's progress is a `LearningPathProgress` record. Never copy the stage rules: use `LearningPathRules` (then starts a stage, and binds tighter than or).
- Require learning through `IRequiredLearningService`; add a kind of target with `services.AddRequiredLearningTarget<T>()` (`IRequiredLearningTarget`: options, name, start with a due date, status). Starting a target enrolls, which commits: save everything first and read assignments again afterwards.
- Who is in an audience comes from `IUserAudienceService` (Users). Add a kind of audience rule in the module that owns the data with `services.AddUserAudienceRuleSource<T>()`; never keep a private list of target groups.
- Due dates are the last second of a day in the site's time zone, converted with `ILocalClock.ConvertToUtcAsync`; enrollments keep due dates to the second.
- Hear required learning with `IRequiredLearningObserver` and completed paths with `ILearningPathObserver`; both run after the save, and the hourly upkeep runs each rule once a day and sends reminders.

## Education credits

- Credits are entries of the education credit ledger, an `IQuantityLedger<EducationCreditLedger>` in its own collection: award through `IEducationCreditService.AwardAsync` with a source and source id, which posts once per source, record and type. Never keep a second credit balance.
- The `EducationCreditsPart` on a course or path lists what it awards; completed courses and paths award it through observers.
- External learning evidence is a protected file (`IProtectedFileStore<ExternalLearningEvidence>`) opened only through its access policy.
- A certification asks for credits with the `CreditsEarned` requirement: the target is the credit type and the minimum the number of credits in the cycle.

## Commerce

- Sell learning through Products, Orders and Carts: `LearningProductPart` says what a product grants, and `LearningOrderEventHandler` (an `OrderLineGrantHandlerBase<LearningProductPart>`) grants each line once and takes it back on a full refund or a cancellation. Never write a second order listener for learning.
- Purchase enrollments use the source `Learning.Commerce` with the source id `{orderId}:{lineId}`; seat enrollments use `Learning.LicenseSeat` with the seat id. Revoke with `IEnrollmentService.RevokeGrantAsync(source, sourceId)`.
- Seats belong to an account (`ILicensePoolService`); free seats are an `IQuantityLedger<SeatLedger>` with one keyed entry per change. Invite people through `IAccountInvitationService` (Accounts) with `Source = "Learning.LicenseSeat"`; never build a second invitation flow.
