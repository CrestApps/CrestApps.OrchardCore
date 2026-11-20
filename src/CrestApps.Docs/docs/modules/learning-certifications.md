---
sidebar_label: Learning Certifications
title: Learning Certifications
description: "Certification paths for the CrestApps Learning module: a Skills certification earned from courses and assessments, issued with a number and a verification page, renewed in a window as a new cycle, lapsed and revoked."
user_manual:
  - user-manual/learning/certifications
  - user-manual/learning/competencies
---

**Learning Certifications** (`CrestApps.OrchardCore.Learning.Certifications`, with `Learning.Certifications.Abstractions` and `Learning.Certifications.Core`) lets people earn a [Skills](./skills) certification through [Learning](./learning). It depends on Learning and on **Skills: Issued Certificates**; with [Learning Assessments](./learning-assessments) on, a passed quiz can be a requirement too.

The certification and every certificate belong to Skills. Learning adds how a certificate is earned and renewed, so a certificate earned in a course is at once a certificate everywhere: Workforce scheduling and the Dispatch candidate finder read the same records.

## Certification paths

A **certification path** (`CertificationPath`, one per Skills certification) is edited on the certification editor, in the **Earned through learning** card:

| Setting | Meaning |
| --- | --- |
| Requirements needed | All of them, or any one. |
| First certificate | The requirements of the first cycle. |
| Renewal | The requirements of a renewal; empty means the first-certificate requirements. |
| Requirement kinds | `CourseCompleted` (a course completed, with an optional minimum grade; a completion whose required item's result was reported by the browser, such as a learning package's own score, counts only when the item allows it) and `AssessmentPassed` (a quiz passed, with an optional minimum percent; only attempts the site scored count). Modules add kinds with `services.AddCertificationRequirement<T>()` and `ICertificationRequirement`. |
| Expiry counted from | The day the requirements are met; the previous expiry (the anniversary is kept); or a fixed day of the year, with the shortest first period. The length comes from the certification's **Valid for (months)**. |
| Renewal opens | How many days before expiry renewal opens. |
| Grace period | How many days after expiry the renewal requirements still renew the certificate. After it, the person starts over with the first-certificate requirements. |
| Meeting a requirement starts the path | Otherwise a manager starts people by hand (or a workflow does). Completions before the path existed never start it on their own. |
| Enroll on renewal | When renewal opens, enroll the holder again in the renewal courses (a new enrollment linked to the earlier one). |

The path is saved with its certification: a certification that fails to save never leaves a path behind.

## Progress and cycles

Each person on a path has a `CertificationPathProgress`: the state (in progress, certified, renewal open, lapsed, revoked), the cycle number, the current certificate, its expiry and the day renewal opens, and **every cycle** with the evidence of each requirement (the enrollment or attempt, when, the grade) and the certificate it earned. Nothing of an earlier cycle is reset or deleted.

`ICertificationPathService`:

- **EvaluateAsync** checks the current cycle's requirements and, when they are met, calls the Skills `ICertificateIssuer` (**IssueAsync** for a first certificate, **RenewAsync** for a renewal, which creates a new record linked to the old one). Evidence of a renewal counts only from the start of its cycle.
- **StartAsync** starts a person, or a new first cycle after a revocation or a lapse past the grace period, and checks at once.
- **OpenRenewalAsync** opens renewal on its day: a new cycle, the renewal enrollments and the observers.

Every change to a person's progress runs under one lock per path and person and is committed inside it. The certificate's source id is the progress id and the cycle number, and the Skills issuer is idempotent by certification, source and source id, so a completion delivered twice, or the upkeep checking someone already certified, issues one certificate.

### Events, and what catches up when one is lost

A completed enrollment (`IEnrollmentObserver`) and a passed, graded or regraded attempt (`IAttemptObserver`) check the paths that ask for that course or quiz. Observers run after the commit and log their failures, so an event can be lost. The hourly **Learning certification upkeep** background task therefore:

1. opens the renewals whose day has come, from a leased work queue (`CertificationRenewalWork`), and any renewal the queue missed;
2. checks again everyone who met a path's requirement since the last look (from the path's creation the first time) and everyone still working on a cycle.

Skills tells Learning when a certificate expires (`ICertificationExpiryObserver`: the progress lapses) and when it is revoked (`ICertificationRecordObserver`: the progress is revoked).

### Who holds the certificate

`CertificateHolderResolver` issues the certificate to the learner's staff record when Staff is on and the user has one, so Workforce and Dispatch see it, and otherwise to the user. The name printed is the staff name or the user name.

## Reviews and revocation

When a completed enrollment is flagged for review (an attempt voided or regraded lower after completion), its review card in **Learning > Enrollments** offers **Revoke the certificate** for each certificate whose cycle counted that enrollment or an attempt made in it. Revoking is immediate on the verification page; the review stays open until it is cleared with a note. Other modules add actions to the review card with `IEnrollmentReviewActionProvider` (Learning.Abstractions).

## Pages

| Page | Route | Permission |
| --- | --- | --- |
| People on a path | `/Admin/learning/certification-paths/{pathId}/people` | View enrollments; Manage enrollments to start someone or check again |
| My certifications | `/learning/certifications` | Signed in; an entry of the storefront account menu, framed by it |
| My competencies | `/learning/competencies` | Signed in, with the Learning Competencies feature; an entry of the storefront account menu, framed by it |
| Verification page and certificate PDF | `/certificates/verify/{code}` (Skills) | Public, rate limited |

## Education credits

Education credits are kept in the module too. An **education credit type** (`EducationCreditType`), such as CEU, CPD hours or PDU, has a name, a unit and whether people can record external learning of it. **Learning > Education Credits** lists and edits them (`ManageEducationCredits`).

- The **Education Credits** part (`EducationCreditsPart`, attachable, put on the Course type by the migration) lists the credits a content item awards: a type, an amount and how many months they count. A completed course awards them once per enrollment, and only when the completion supports what is issued on it (`CompletionEvidence.SupportsIssuing`): a failed completion, one under review, or one resting on a required item's result reported by the browser (a learning package's own status, unless the item allows it) earns no credits, since credits can meet a credits requirement. With [Learning Programs](./learning-programs) on, a learning path with the part awards them once per path progress, under the same rule for its courses.
- Credits are posted to the education credit ledger, a quantity ledger (`IQuantityLedger<TLedger>`) of its own collection (`LearningCreditLedger`): the account is the user, the bucket the credit type, and the idempotency key the source, its record and the type.
- **External learning** (`ExternalLearningSubmission`) is learning done elsewhere that a person records on **My education credits** (`/learning/credits`, an entry of the storefront account menu) with an optional evidence file, kept in the protected file area `ExternalLearningEvidence`. A person with **Accept or reject external learning** reviews it under **Learning > External Learning**; accepting posts the credits.
- The **Education credits earned** requirement (`CreditsEarned`) asks for a number of credits of a type earned in the cycle: its target is the credit type and its minimum the number. Posting credits checks the paths that ask for the type at once, and the hourly upkeep catches up with `ListAccountsSinceAsync`.
- `IEducationCreditService` reads balances, sums and entries, awards credits and handles external learning; `IEducationCreditObserver` hears awards, submissions and reviews (`services.AddEducationCreditObserver<T>()`).
- The **Record external learning** data pipeline step records the external learning of each row as accepted and posts its credits; rows that fail go to its Rejected output with the reason.
- The `LearningCreditTypes` recipe and deployment steps move credit types; the workflow events Education Credits Awarded and External Learning Submitted and the task Award Education Credits start or award from workflows; the data source gains the Education credits and External learning data sets; credits and external learning are exported and deleted with a person's personal data.

## Competencies

The **Learning Competencies** feature (`CrestApps.OrchardCore.Learning.Competencies`, in this module) maps competencies onto [Skills](./skills): a competency is a skill and a competency framework is the skill's `Category`, so Skills needed no new record. It depends on Learning and Skills only, not on certification paths.

- The **Competencies** part (`CompetenciesPart`, attachable) lists the skills a content item grants (`SkillIds`) and the level (`Proficiency`, 1 to 5, default 3). Its migration (`CompetenciesMigrations`) puts it on the Course type, and on the Learning Path type when that type exists. Its editor is a card with a selectpicker of the enabled skills grouped by category.
- `CompetencyEnrollmentObserver` grants a completed course's competencies, and `LearningPathCompetenciesObserver` (Learning Programs, registered by `PathCompetenciesStartup` when this feature is on) a completed path's. Both grant only when the completion supports issuing (`CompletionEvidence.SupportsIssuing`, `ILearningPathService.SupportsIssuingAsync`), the rule credentials and credits follow.
- `CompetencyGrants.GrantAsync` gives each skill through `ISkillAssignmentService.AssignAsync` as `SkillSubject.User`, from the completion day in the site's time zone. It never lowers a level the learner holds, raises a lower one keeping its start day, restarts one that ended at the higher of the two levels, and lets Skills refuse a disabled or deleted skill. Granting again changes nothing, so a completion delivered twice is harmless. `AssignedBy` is `Learning: <title>` (`CompetencyGrants.Actor`).
- **My competencies** (`/learning/competencies`, `MyCompetenciesController`, an entry of the storefront account menu framed by it) lists `ISkillAssignmentService.GetEffectiveAsync` for the user today, by category.
- Register the observer outside the feature with `services.AddLearningCompetenciesCore()`.

The frameworks are loaded with the Skills import through ContentTransfer (a Moodle competency framework export) or the `Skills` recipe step, and the Skills **Skill holders** report shows who holds which competency; see [Skills](./skills#import-skills-from-a-file).

## Integrations

| | |
| --- | --- |
| Notifications | Certificate issued (and renewed), Certificate expiring (from the Skills reminder days), Certificate renewal open, Certificate lapsed, Certificate revoked. |
| Workflows | Certification Renewal Opened and Certification Lapsed events; Start Certification Path task. Skills adds Certificate Issued, Renewed, Revoked, Expiring and Expired. |
| Reports | **Certification compliance**: who is valid, renewal open, expiring within 30 days, lapsed and in progress, per certification. |
| Data source | `LearningCertifications` with the `LearningCertificationProgress` data set. |
| Recipes and deployment | `LearningCertificationPaths`: paths matched to their certification by id or name. Progress is not exported. |
| Personal data | Progress with every cycle is exported; anonymizing deletes it (Skills keeps issued certificates without the name). |
| Telemetry | `crestapps.learning.certification.changes` on the Learning meter, by change: issued, renewed, renewal_opened, lapsed, revoked. |
