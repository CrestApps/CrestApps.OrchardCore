# Learning: a learning management system built from the building blocks

Status: proposed 2026-11-13, revised the same day after a duplication audit against every Orchard Core module (4.0.0-preview-19219, 101 modules) and every module in this repository (124 manifests). Section 18 lists what the audit changed. Nothing is built. Section 15 lists the decisions that need an answer before work starts, each with a recommendation. Section 16 lists facts that must be checked first.

## 1. Why, and what "done" means

We want a learning management system (LMS) in the class of Moodle, Canvas, Open edX and Totara Learn: courses, enrollment, lessons, exams, learning paths, instructor-led classes, reports, and above all **certification**: a person completes courses and exams, receives a certificate, and renews it before it expires.

Orchard Core and this solution already own most of the concepts an LMS needs: content, media, users, roles, tenants, sign-in, workflows, notifications, search, data pipelines, staff, teams, accounts, skills and certifications, calendars, scheduling, products, carts, orders, subscriptions, reports and AI. So the LMS is not one module, and it is mostly not new code. It is a set of modules that compose what exists and add only what has no owner: the course, the enrollment, the learner's progress, the assessment, the learning path, and the rules that turn completed learning into a certificate.

**The first rule of this plan: nothing is built twice.** Sections 4.1 and 4.2 map every concept to its owner in Orchard Core or in this solution. Where an owner lacks an extension point, the plan adds it to the owner (section 5). The plan adds **one** new building block (PDF documents) and no other shared module.

The plan has three goals:

1. **Certification first.** After phase 3 a site can publish a course with an exam, enroll people, grade the exam on the server, issue a numbered certificate with a public verification page and a PDF, remind the holder before it expires, and run the renewal. This is the minimum product.
2. **Industry standard, without the known mistakes.** Section 2 records what the leading products do and which of their design problems we avoid.
3. **Secure and private by design.** Learner records are confidential, scores are computed on the server, and every module ships personal-data export and erasure, retention and audit from its first commit (section 9).

Every module in the set also meets the standing rules: recipe step, deployment step and CrestApps Recipes schemas for every record type; workflow events and tasks; an Orchard Core data source for Data Pipelines and the Report Builder; admin lists on `<admin-list>`; forms with `action-bar-bottom`; local times through `ILocalClock` (section 10).

## 2. What the market does

Studied in depth: Moodle 5.3 LTS (October 2026), Canvas (Core, Plus and Next tiers, and Canvas Career), Open edX (Verawood, 2026) and Totara 20 (January 2026). Studied more briefly for corporate and credential features: Docebo, SAP SuccessFactors Learning, Absorb, D2L Brightspace, LearnDash, Credly and Accredible. Sources are in section 17. Product names appear in this engineering document only; code, comments, identifiers and the public documentation use generic terms.

### 2.1 Capabilities and what we take from each

| Area | What the leaders do | What we take |
| --- | --- | --- |
| Course structure | All four use course > section > activity. Canvas and Open edX have course runs and terms as first-class records. Moodle and Totara copy or reset a course for a new offering. | Course > section > course item, with items as typed, extensible content. Optional **offerings** (runs) so a course is never copied or reset. |
| Item types | Pages, video, files, quizzes, assignments, forums, SCORM, H5P and LTI tools. Moodle activities and Open edX XBlocks are plug-in types. | Course item kinds are Orchard content types. A site or another module adds a kind with a content part and a registration call. |
| Completion and release | Activity completion rules feed course completion. Release conditions use date, grade, completion and group. | Per-item completion modes, course completion criteria, and open release rules. |
| Enrollment | Moodle has plug-in methods (manual, self with key, cohort, payment). Totara adds dynamic, rule-based audiences and assignment by position, organization and manager with relative due dates. | Open enrollment sources. Rule-based audiences and due dates as in Totara, because required training depends on them. |
| Assessment | Question banks with versions, about 15 question types, random draws, attempts, time limits, per-user overrides with a recorded reason, rubrics, weighted gradebooks. Canvas has run two quiz engines side by side for years, and users report that migration between them is unreliable. | One assessment engine. Random draws, server-side timing and grading, overrides with a reason, item analysis. |
| Learning paths | Totara programs are ordered course sets joined by THEN, AND and OR. Moodle keeps programs in its paid Workplace edition. | Learning paths as ordered course sets with the same three joins. |
| Certification | Totara has the reference model: an original path and a recertification path, an active period, a recertification window, a reference date (completion, expiry or fixed), extension requests, and states (in progress, certified, window open, expired). Moodle keeps certificates in plug-ins, Canvas in its Catalog product. | The same settings (section 7.8), built on the certification records the **Skills** module already owns. |
| Certificates and digital credentials | Templates, a verification code or QR, public verification without sign-in. Canvas Credentials, Credly and Accredible issue Open Badges 3.0 as W3C Verifiable Credentials. | Certificate templates, PDF, unguessable verification code, public verification page, revocation. Open Badges 3.0 in the standards phase. |
| Instructor-led training | Only Totara has it in core: events with sessions, rooms, facilitators, sign-up periods, manager approval, automatic waitlist promotion, attendance. | Classes on the existing **Scheduling** module, extended for dated classes, automatic waitlist promotion and attendance detail. |
| Compliance and credits | None of the four ships CEU/CPD credits or 21 CFR Part 11 e-signatures in core. | An education credit ledger in phase 4. Part 11 is not in this plan. |
| Reports | Moodle and Totara have report builders with schedules. Open edX turns tracking events into xAPI statements. | Data sets for the existing Report Builder and Data Pipelines, and coded reports under **Reports**. |
| Permissions | Moodle and Totara resolve capabilities over nested contexts. Canvas has account and course roles. | Orchard permissions and roles, plus an authorization handler that scopes by course role, staff hierarchy and account. |
| Multi-tenancy | Totara has tenants in core. Moodle sells them in Workplace. | Orchard tenants with feature profiles, and TenantHierarchy; or one tenant with Accounts as client organizations. |
| Commerce | Moodle has basic paid enrollment. Canvas Catalog adds promotion codes, bulk seat purchase and waitlists. Open edX deprecated its e-commerce service. | A course is a product. Selling uses Products, Carts, Checkout and Orders with no private payment path. |
| AI | Moodle has an AI subsystem that is off by default, with per-course switches and a usage policy. Canvas publishes "AI nutrition facts" per feature, and its grading assistance only suggests. | The existing AI modules, off by default per course, with suggestions that a person confirms. |

### 2.2 Standards

| Standard | Current state | For this LMS |
| --- | --- | --- |
| SCORM 1.2 (ADL) | Still the most common package format. | Phase 8, first. |
| SCORM 2004 (ADL) | 3rd and 4th editions are in use. | Phase 8. Single-SCO and simple multi-SCO packages; full sequencing later. |
| xAPI | 1.0.3 is widely deployed; 2.0 is IEEE 9274.1.1-2023. | A conformant Learning Record Store (LRS) in phase 8. |
| cmi5 (ADL) | The xAPI profile for content an LMS launches. | Phase 8, on the LRS. |
| LTI 1.3 + Advantage (1EdTech) | Deep Linking 2.0, Names and Role Provisioning 2.0, Assignment and Grade Services 2.0. | Phase 8, as a platform, on Orchard Core's OpenID server. |
| QTI (1EdTech) | 3.0 is final; 2.1 is still common in exchange. | Import only, as a ContentTransfer file format, in phase 8. |
| Open Badges 3.0 (1EdTech) | Final since May 2024, on the W3C Verifiable Credentials Data Model 2.0. | Phase 8. Issue 3.0. |
| CLR 2.0, CASE, OneRoster 1.2, Caliper, Common Cartridge, H5P | Academic exchange and rostering formats. | Not in this plan (section 14). |
| SSO and provisioning | OIDC and SAML 2.0 for sign-in; SCIM 2.0 for provisioning. | OIDC and the external sign-in providers come from Orchard Core. SAML and SCIM are not in this plan. |
| WCAG 2.2 AA | EN 301 549 v4.1.1 (September 2026) moves to WCAG 2.2 AA. The US ADA Title II rule requires WCAG 2.1 AA from April 2027 or April 2028 by population. | The learner pages and the exam player meet WCAG 2.2 AA. |

### 2.3 Design problems to avoid

- **Destroying data on renewal.** Totara resets course data when the recertification window opens. We never reset: each cycle is a new enrollment and a new certification record.
- **Expiry drift.** A completion-based reference date moves the anniversary each cycle. We offer the expiry-anchored and fixed-date rules from the start.
- **Deferred processing.** Totara applies due-date changes only on a later job. We evaluate on the triggering event, show when each rule last ran, and offer **Run now**.
- **Two engines for one job.** One assessment engine, one gradebook, one certificate store, one seat engine, one catalog listing.
- **Essential features as plug-ins.** Certificates, programs and tenants are outside Moodle core. Here they are first-party modules.
- **Trusting the browser.** ADL's own analysis says the SCORM runtime cannot be secured on the client. No score that the browser reports can issue a certificate by default.
- **Missing permission checks.** Several 2026 Moodle advisories are missing capability checks on grading and report endpoints. Every endpoint here checks a permission and the record's scope.
- **A certificate PDF as proof.** Proof is a server lookup by an unguessable code, or a signed credential with a revocation status.

## 3. Design principles

1. **Reuse first, and say so.** Each concept maps to its owner (section 4). The pull request for each phase says which step of the reuse ladder it took for each concept.
2. **Authored material is content; learner records are documents.** Courses, course items, questions, learning paths, rubrics, certificate templates and credential definitions are Orchard content items, so they get everything Orchard Core gives content (section 4.2). Enrollments, progress, attempts, registrations and awards are CrestApps.Core catalog documents with YesSql indexes, because they are many, they change often and they are personal data.
3. **A learner is a user.** There is no learner record. Learning data hangs on the user id, and names are resolved through `IDisplayNameProvider` at read time, never copied.
4. **History is append-only and self-contained.** A completed enrollment, a submitted attempt and an issued certification record are never edited in place. A retake or a renewal creates a new record that links to the old one. A record that is evidence copies what it needs (the question as shown, its key and points; the course outline at completion), because Orchard Core's version pruning can delete an old content version.
5. **The server decides.** Timing, scoring, completion and eligibility are computed on the server. Client-reported results are stored with a "self-reported" mark and are excluded from certification rules unless an administrator allows them for a given item.
6. **Open kinds, no consumer names.** Course item kinds, enrollment sources, question types, release rules, completion criteria and certification requirements are open strings with registration helpers. The core never names the modules that build on it.
7. **High-churn data stays off shared documents.** Progress writes go to their own document, never to the enrollment, the course, the staff member or the account.
8. **Optional integrations are gated.** Every integration with another feature lives in a `[RequireFeatures]` startup or in its own module, with feature IDs as constants.
9. **Names do not collide.** Section 6.1 fixes the vocabulary, chosen so that no Learning term repeats a term another module owns.
10. **Privacy by default.** Least data, least access, defined retention, and an export and erasure path for every record that holds personal data.

## 4. Reuse map

### 4.1 Concepts owned by this solution

| LMS concept | Owner | How Learning uses it | Step |
| --- | --- | --- | --- |
| Learner | Orchard Users + **Users** | User id on every learner record; `IDisplayNameProvider`, `IUserSearchService`, the `UserPicker` view component; `EmailCodeSignIn` for invited learners without a password | Use |
| Instructor, grader, training manager | **Staff** | `AddStaffCapability("Instructor", ...)`, `IStaffAccountService.EnsureAsync`, `IStaffDirectory`, the `StaffPicker`; a **Learning** section on the staff profile through `AddPageSection`; `IStaffHomeLinkProvider` | Use, extend |
| Manager's view of a team's learning | **Staff** | `IStaffHierarchy`, `IStaffScopeService` | Use |
| Rule-based group of people | **Users** user audiences (`IUserAudienceProvider`), with **Staff** (`StaffQuery`), **Teams** (`ITeamMembershipService`) and **Accounts** as rule sources | Section 5.3 | Add extension points to the owner |
| Client organization, its members, admins and invitations | **Accounts** (+ Members), **Accounts.B2B** | `IAccountMemberService`; licence pools owned by a `PartyRef` of kind Account; organization pages under `account/business`; B2B order approval and budgets apply to seat purchases unchanged | Use, extend (5.4) |
| Owner of a purchase or a pool | **Parties** | `PartyRef`, `IPartyDirectory` | Use |
| **Certification definition and a person's certificate** | **Skills** | `Certification`, `CertificationRecord`, `ICertificationRecordService`, `ICertificationExpiryObserver`. Learning never stores certificates. | Add extension points to the owner (5.1) |
| Skills a course teaches | **Skills** | `ISkillAssignmentService.AssignAsync` on completion; a skills card on the course editor | Use |
| **Classes: seats, holds, confirmation, waitlist, no-show** | **Scheduling** | `AppointmentType` with group `Capacity`, `IAppointmentService`, `WaitlistEntry`, `IAppointmentObserver`. Learning adds no seat engine. | Add extension points to the owner (5.5) |
| Class dates on calendars, reminders, .ics invitations with RSVP, feeds, rooms, instructor busy time | **Calendars** (Reminders, Invitations, Feeds, Resources) | Scheduling already puts a group class on one calendar event with attendees. Learning sends no class e-mail of its own. `ICalendarEventSource` shows due dates. | Use |
| A staff class on the shift plan; paid training time | **Workforce**, **TimeTracker** | A `ShiftSegment` with the seeded Training activity code; `ITimeTrackerService.SyncAsync` (the `TimeOffTimeTrackerSync` pattern) | Use (bridge startup) |
| Wall-clock time, time zones | **Calendars** | `CalendarTime`, `ILocalClock` for display | Use |
| Class location | **Addresses** | `NamedAddress` | Use |
| Course catalog with facets and search (when courses are sold) | **Products** (Listing + search providers) | `ProductListingPart`, `IProductSearchService`, `IProductSearchDocumentContributor` for course fields; `ProductSalesMode.ListingOnly` for a free course | Use, extend |
| Selling a course, a path, attempts or seats | **Products**, **Carts**, **Checkout**, **Orders** | A content part on the product, `ICartLineHandler`, `OrderLineGrantHandlerBase<TPart>` (the Memberships pattern) | Extend |
| Enrollment vouchers and discounts | **Checkout** `Coupon`, `CouponCode` | Single-use generated codes with `MaxRedemptions` are the prepaid vouchers | Use |
| Access by subscription; seats by subscription | **Subscriptions** | `ISubscriptionEntitlementApplier` with a Learning kind; `SubscriptionLine.Quantity` sets the pool size | Extend |
| Taxes, receipts, installments, markets, vendors, member pricing | **Taxation**, **Receipts**, **Transactions**, **Markets**, **Marketplace**, **Memberships** | They apply to a course as to any product | Use |
| Course ratings | **Reviews** | A sold course is a product, so it has reviews | Use |
| A learner's account area | **Commerce** account menu (`CommerceConstants.Account.MenuName`, the `CommerceAccountPage` shape); **Workforce** `IMyWorkLinkProvider` | **My learning** and **My certificates** are entries in the existing menus (section 11) | Extend |
| Private files that only entitled people may download | **Downloads** (`IDigitalFileStore`, the token endpoint) and `FileStorage.Core` | Section 5.6 | Extract from the first copy |
| A quantity balance with dated, idempotent entries | **Workforce** time-off ledger (`TimeOffLedgerEntry`) | Section 5.7 | Extract from the first copy |
| Reports and exports | **Reports** | `IndexedReportDataSet`, `IReport` + `AddReport<T>`, report subscriptions, dashboard widgets | Extend |
| Importing questions (QTI, spreadsheets) | **ContentTransfer** | Questions are content items: `IContentTransferFileFormatProvider`, `IContentImportHandler` | Extend |
| People and data from an HR or student system | **DataConnectors** | A connection and an imported data set feed required-learning rules | Use |
| Personal data export and erasure | **PersonalData** | One `IPersonalDataContributor` per module | Extend |
| Bulk announcements | **Outreach** | An `IOutreachAudienceSource` over a course's learners | Extend |
| Outbound integration events | **Webhooks** | `AddWebhookEvent`, `IWebhookPublisher` with stable event ids | Extend |
| Document numbers and codes | `INumberSequence`, `ReadableCodes` | Certificate numbers; `ReadableCodes.Generate` for the verification code | Use |
| Durable timed work, retention, locks | `ILeasedWorkQueue<T>`, `BackgroundTaskTelemetry`, `IRetentionPolicy`, `DistributedLockExtensions` | Attempt deadlines, renewal windows, due-date reminders, purges | Use |
| Status machines | `TransitionTable<TEnum>` | Enrollment, attempt and certification path states | Use |
| Catalog records and their admin pages | CrestApps.Core catalogs, `DocumentCatalog`, `CatalogAdminController<T>`, `AuditedCatalogEntryHandlerBase` | Every Learning catalog record | Use |
| Recipe and deployment steps | `NamedCatalogRecipeStep`, `NamedCatalogDeploymentSource`, `CatalogDeploymentSerializer` | Section 10.1 | Use |
| Schema.org `Course` data | **Seo** | The vocabulary has `Course`; a `ContentPartStructuredDataProvider<CoursePart>` adds `hasCourseInstance` and `offers` | Extend |
| Course evaluation forms, enrollment questions | **Wizard**, Orchard Forms | Used as they are. The Wizard is a data-collection stepper, not the lesson player or the exam engine. | Use |
| Child tenants for a training provider | **TenantHierarchy** | One child tenant per client, set up from a Learning recipe | Use |
| AI (section 7.14) | **AI**, **AI.Chat**, **AI.Prompting**, **AI.DataSources**, **AI.Documents**, **AI.Memory**, **AI.Agent**, **AI.Mcp**, **NLWeb** | Tools with `AddCoreAITool`, a profile template, prompts in `Templates/Prompts/*.md`, the chat widget, per-user memory, usage metering, the MCP server | Use, extend |

### 4.2 Capabilities owned by Orchard Core

The pinned version is 4.0.0-preview-19219. Learning uses these features as they are and never builds its own version.

| Need | Orchard Core feature | Use in Learning |
| --- | --- | --- |
| Content types, versions, drafts, preview | Contents, ContentTypes, ContentPreview | Courses, course items, questions, paths, rubrics, templates |
| Lesson body, title, URL | Html (`HtmlBodyPart`), Markdown, Title, Autoroute, Alias, Shortcodes, Liquid, Templates | Page lessons and course pages; no private editor |
| Containment | Lists (`ListPart`, `ContainedPart`), Flows (`BagPart`) | Course to course items; question bank to questions |
| Categories and topics | Taxonomies (+ ContentsAdminList) | Course categories, question tags |
| Translated courses | ContentLocalization, Localization | A translation is its own content item in a localization set (decision 15) |
| Timed publication and retirement | PublishLater, ArchiveLater | A course opens and closes on a date with no Learning code |
| **Lesson files and videos, protected** | Media, Media.Security (**Secure Media**), Media.Tus, `MediaField` with the Attached editor, `YoutubeField` | Secure Media authorizes a file attached to a content item by `ViewContent` on that item, so Learning's content access handler (section 8) also gates the files. No private lesson file store, endpoint or signed link. |
| Upload safety | `FileCreationService` with `IFileEventHandler`, Antivirus (ClamAV), `ITempDirectoryProvider` | Every Learning upload passes `FileCreationService` before it is stored; packages are extracted in a tenant temporary folder |
| Catalog search and listing (without commerce) | Search, Indexing, Lucene / Elasticsearch / AzureAI, Queries, Media.Indexing.Pdf and .OpenXML | An index profile and a query drive the catalog page; lesson documents are searchable for enrolled people |
| Sitemaps, feeds, meta tags | Sitemaps, Feeds, Seo (`SeoMetaPart`) | Public course pages |
| Sign-in, registration, two-factor, external sign-in | Users (UserRegistration, TwoFactorAuthentication, ExternalAuthentication), OpenId.Client, Microsoft, Google, GitHub | Learners sign in the way the site already does |
| Learner profile fields, time zone | Users.CustomUserSettings, Users.TimeZone | Extra learner data without a Learning record; local times |
| Roles and permissions | Roles, Security | Section 8. Recipes create the suggested roles. |
| Notices to a person | Notifications (+ Email, Sms): `INotificationService`, `AddNotificationType`, per-type user preferences, `INotificationManager` | Every learner notice. The notification center is an admin page, so the learner pages list a learner's notices with `INotificationManager`. |
| Automation, approval routing, custom e-mail | Workflows (+ Http, Timers), Email, Sms | Events and tasks per module (section 10.2). Learning has no approval engine and no e-mail templates. |
| Audit of admin actions | AuditTrail, Users.AuditTrail | A second view of sensitive changes. Its retention defaults to 10 days and an admin can switch events off, so the Learning records are the compliance record (section 9.1). |
| **Reading and writing data in bulk** | DataPipelines (+ Core, Ftp, Sftp): data sources, **destination steps** (`IDataPipelineStepType`, `AddDataPipelineStepType<T>`), file formats (CSV, XLSX, JSON), preview, run history, schedules, shared links | Data sources per module; **Enroll learners**, **Award education credits** and **Record external learning** destination steps. No private CSV import page. |
| Calculations | The Data Pipelines formula language (`ExpressionCompiler`, `IExpressionFunctionCatalog`) | Any configurable grade calculation |
| Sharing an export with a named person | Shared links (`ISharedLinkManager`) | Transcript and learner-record exports. They need sign-in, so they are not the public certificate page. |
| **API sign-in, clients and scopes** | OpenId (Server, Management, Validation), the `Api` authentication scheme | An external tool and an LRS client are OpenID applications with roles and scopes. No private credential table. |
| **Keys and secrets** | Secrets (`ISecretManager`, `RsaKeySecret`, `X509Secret`, `TextSecret`), Secrets.AzureKeyVault, OpenId.Secrets | Signing keys for external tools and digital credentials; tool and client secrets |
| **Rate limits** | RateLimits (`AddRouteRateLimit`, `AddGroupRateLimit`, `[RateLimitGroup]`), ReCaptcha | The public verification page, enrollment keys, exam access codes, the LRS |
| API description | OpenApi | Every Learning API |
| Hiding types from GraphQL | Apis.GraphQL (`GraphQLContentTypeSettings.Hidden`) | Question types |
| Dashboard | AdminDashboard | Overdue learning and expiring certificates, as report widgets |
| Widgets by rule | Layers, Rules (`AddRule`) | An "In audience" condition, so a widget can target an audience |
| Moving configuration and content | Recipes, Deployment, Deployment.Remote, Contents deployment features | Section 10.1; courses move between sites with the existing steps |
| Tenants | Tenants, Tenants.FeatureProfiles | A feature profile limits which Learning features a client tenant may enable |
| Background work, caching, locks | BackgroundTasks, Redis (Lock, Bus), DynamicCache | Through the platform helpers in 4.1 |

### 4.3 What Learning owns

Nothing in Orchard Core or in this solution models a course outline, an enrollment, a learner's progress, a question, an assessment attempt, a grade, a learning path, required learning with a due date, a licence pool, a learning package, an LTI launch or a digital credential. Both audits searched for these and found only: Skills certifications, the Schema.org `Course` vocabulary entry, Workforce's Training activity code, and a note in `docs/engineering/commerce/commerce-parity-plan.md` that lists classes and courses as future work.

Four small things are also new and are placed with their owners, not in Learning: certificate issuing (Skills), invitations to an account (Accounts), rule audiences (Users), and dated classes with waitlist promotion and attendance (Scheduling).

### 4.4 Patterns this plan must not copy

- Copied person names (`Membership.UserName`, `CertificationRecord.SubjectName`): store the id only.
- A private instructor record, notification channel, reminder loop, `ListDueAsync` polling task, CSV import or export page, analytics page, e-mail template system or approval engine.
- Adding or removing roles directly: use `IRoleGrantService` with an `IRoleGrantSource`.
- The after-payment grant loop, or reading the latest product version after payment: use `OrderLineGrantHandlerBase<TPart>`.
- Random or max+1 document numbers; hand-rolled data protectors for links (Downloads and Dispatch each have one).
- Closed enums that name consumers; Learning fields on `Order`, `StaffMember`, `Account`, `Appointment` or `Certification`.
- A third seat engine (Scheduling and Bookings exist), a third waitlist (Scheduling and Workforce time off exist), a second listing and search engine (Products Listing exists), a second private file server (Downloads exists).

## 5. Changes to existing building blocks

These changes belong to the owners. Each lands in the phase that first needs it, in the same pull request as its first consumer, with `docs/engineering/building-blocks.md` updated and the existing copies migrated.

### 5.1 Skills: issue certificates, not only record them (phase 3)

Skills today records certificates that someone else issued. For the LMS, the site itself is the issuer. Nothing else in the solution issues a numbered document with public verification (Receipts, Fundraising receipts and Memberships have no public page; Taxation's `ExemptionCertificate` is a customer's tax record), so Skills is the owner.

| Gap (checked in code) | Change in Skills |
| --- | --- |
| A record has no link to what earned it | Add `Source` and `SourceId` (open strings) to `CertificationRecord`, and an evidence summary the issuer supplies. |
| Renewal overwrites the dates of one record | Keep that path for hand-recorded certificates. Add `RenewedByRecordId` and `PreviousRecordId`, so an issued renewal is a **new** record. The daily check skips reminders for a record that has a successor. |
| Numbers are typed by hand | Generate the number from `INumberSequence` when the certification says so. |
| No issuing contract | `ICertificateIssuer.IssueAsync(...)` and `RevokeAsync`, which save through `ICertificationRecordService`. |
| `RevokeAsync` writes the reason into `Notes` | Add revocation fields (reason, actor, time). |
| No public proof | New feature **Skills: Issued Certificates**: a random verification code per issued record from `ReadableCodes.Generate` (at least 80 bits, separate from the number, never a `SignedTokenService` token because those expire), a public verification page behind Orchard Core rate limits, the certificate document (5.2), and revocation that the page shows at once. |
| Only expiry events | Add `ICertificationRecordObserver` (issued, renewed, revoked) with workflow events and webhook events. |
| Editors are plain view-model forms | Render the certification editor through `IDisplayManager<Certification>`, so Learning adds its card with a display driver. |
| No report data set, no personal-data contributor | Add a `Skills` data source and an `IPersonalDataContributor`. |
| `DocumentPath` is a public media path | Move certificate copies to the protected file store (5.6). |
| `SubjectName` is a copied name | Resolve the name at read time for user and staff subjects. |

Effect: a certification earned in the LMS is at once a certification everywhere. Workforce's `SkillsRequiredRule` and Dispatch's candidate finder already read these records.

### 5.2 PDF documents: the one new building block (phase 3)

`CrestApps.OrchardCore.Reports.Pdf` builds PDF files with PdfPig, but its layout engine is internal and it only renders a `ReportDocument`. Receipts render HTML only. Orchard Core has no PDF writer. Certificates are the second consumer, which is the trigger to extract.

- New `CrestApps.OrchardCore.Pdf.Abstractions` and `.Core`: `IPdfDocumentRenderer` over a small layout model (page setup, background image, positioned and flowing text with embedded fonts, images, lines, a QR code).
- Reports.Pdf moves its font resolver, text measurement and page primitives onto it in the same phase.
- A **certificate template** is a content item with Liquid fields.
- `PdfLibraryLicenseTests` bans PDFsharp/MigraDoc, iText and QuestPDF, so the block stays on PdfPig. No QR generator exists in the solution; the QR library is a new dependency and needs a permissive licence.
- Without an embedded TrueType font PdfPig shows only Latin-1 text, so the template selects an embedded font.

### 5.3 Users: user audiences that resolve members (phase 4)

Required training needs rule-based groups: "all staff in team X with capability Y, hired more than 30 days ago". The concept already has an owner: `IUserAudienceProvider` in `Users.Abstractions` is "a named group of users", used by the user picker and `IUserSearchService`. Today it can only filter a picker by role. The plan extends it and adds no new module.

- In `Users.Abstractions` and `Users.Core`: member paging (`GetMembersAsync`) and `IsMemberAsync` on the audience contract, and **admin-defined audiences** (a named set of rules joined by all or any) beside the audiences that code registers.
- Rule sources register from their owners in `[RequireFeatures]` startups: roles (Users), team with sub-teams (`ITeamMembershipService`), staff attributes through `StaffQuery` (which gains hire date and employment type), account membership (Accounts), holders of a skill or certification (Skills), learners of a course (Learning).
- Before a rule registry and editor are built, phase 4 spikes whether an audience can be a saved filter over Orchard Core's `Users` data set (`DataCondition`, `DataFilterOperator`) with the owners' data sets joined. Orchard Core's Rules module cannot do this: its `IConditionEvaluator` takes no subject and reads the current request.
- An adapter exposes an audience as an `IOutreachAudienceSource`, and an "In audience" condition is registered with `AddRule` for Layers.
- Membership change events come from the owners' observers (`IStaffLifecycleObserver`, team and role changes), with a nightly reconcile on the leased work queue.
- Workforce targets groups privately today (`LaborRuleSet`, `TimeOffPolicyAssignment`). They move to audiences in the same phase or are recorded in the reusability audit as an accepted gap.

### 5.4 Accounts: invitations (phase 5)

Nothing in the solution invites a person by e-mail. The account member editor refuses an unknown user, and `EmailCodeSignIn` works for existing users only. An account admin must be able to invite a person to a seat, so the invitation (pending record, e-mail, token, expiry, role, accept) is added to **Accounts**, feature Account Members. Learning's seat holder record only refers to it. The invited person signs in with `EmailCodeSignIn` or registers through Orchard Core.

### 5.5 Scheduling: dated classes (phase 6)

Scheduling already runs group sessions: an `AppointmentType` with `Capacity` above 1 gives one `Appointment` per attendee, all on one calendar event, with holds, confirmation, cancel and reschedule rules, completed and no-show, a waitlist record, and capacity checked under a lock. Bookings also sells seats on fixed dates (`BookingKind.Departure`), and Workforce time off has a waitlist that promotes on its own. A new seat block would be the third seat engine and the third waitlist, so classes are built on Scheduling, the slot owner the reusability audit names. Five extension points are missing:

| Gap (checked in code) | Change in Scheduling |
| --- | --- |
| Slots come only from a resource's working hours; `IAppointmentSlotFilter` can drop slots, never add them | A dated session source: a class with a start, an end, its own capacity, a location and a registration window, which exists before its first booking. |
| The waitlist is a concrete Core class with no user id, and promotion only notifies the first person | A waitlist contract in `Scheduling.Abstractions` with a user id, a specific session, and an automatic promotion mode with an offer that expires and passes to the next person. |
| `Appointment.Source` is only the origin (Admin, SelfService, Api) | `Source` and `SourceId` as open strings, so a registration links to its enrollment. |
| Attendance is completed or no-show | Minutes attended, partial attendance, and bulk marking for a roster. |
| One appointment is one date | One registration that covers several meeting dates. |

Bookings is not used for classes: it has no Abstractions project, and the audit already lists its time-slot engine as a duplicate of Scheduling's. A paid class is sold as a Learning product whose grant enrolls the buyer and registers them through Scheduling.

### 5.6 Protected files: extract from Downloads (phase 4)

Lesson files need no new store (Secure Media, section 4.2). Three other kinds of file are private and are not attached to a content item a viewer may see: evidence a learner uploads, coursework submissions, and learning packages. Downloads already has "a private file, served by an endpoint to people who are entitled": `IDigitalFileStore` (which runs the antivirus pipeline), `DownloadEntitlement`, `DownloadService` and a token endpoint, all inside the module with its own data protector. Learning would be the second copy.

- Extract the private store and the authorized serving endpoint into `FileStorage.Core` with a new `FileStorage.Abstractions`, with an open access policy contract.
- Downloads keeps its order entitlement as one policy. Learning supplies its own policies (the uploader, the graders in scope). Skills uses it for certificate copies.
- Files that hold personal data use `EncryptedFileStore`.

### 5.7 A quantity ledger: extract from Workforce (phase 4)

Education credits and licence pools are balances of a quantity with dated entries. `BalanceLedger` in Payments is a money helper (it rounds to a currency and its entries live inside the owner's document), so it is not the owner. The closest shape is Workforce's `TimeOffLedgerEntry`: its own document, a kind, an expiry date, a unique idempotency key and a source reference. Loyalty points keep a third private copy.

- Extract a neutral quantity ledger (entry document, balance read, idempotent post, expiry) into the shared platform libraries, and move the Workforce time-off ledger onto it in the same phase.
- Education credits and licence pools use it. Loyalty points are recorded in the reusability audit as a later migration.

### 5.8 Signing keys (phase 8)

Orchard Core Secrets holds the keys (`RsaKeySecret`), with stores, expiry, and recipe and deployment steps. External tools, digital credentials and AgenticCommerce's `IMerchantSigningKeyService` each need to publish a key set and rotate keys. When the second consumer lands, only that publishing and rotation code moves to a shared library; key storage is never rebuilt.

## 6. The module set

All projects are `CrestApps.OrchardCore.Learning[.X]` with `.Abstractions` and `.Core` beside each module where another module consumes its contracts. Feature IDs are constants in the owning `.Abstractions` project. The admin menu is **Learning** with an icon; reports go under **Reports**.

| Module | Features | Owns | Depends on |
| --- | --- | --- | --- |
| **Learning** | Learning (core), Learner Pages, Self Enrollment | Course, outline, course item kinds, enrollment, progress, completion, content access rules | Users, Resources; Orchard Contents, Lists, Media, Taxonomies |
| **Learning.Assessments** | Assessments, Gradebook | Question types and banks, quiz item, attempts, scoring, overrides, grade items and scales | Learning |
| **Learning.Certifications** | Certification Paths, Education Credits | How a Skills certification is earned and renewed, a person's progress on it, credit types and entries | Learning, Skills (Issued Certificates), Pdf |
| **Learning.Programs** | Learning Paths, Required Learning, Team Learning | Learning path, required learning with due dates, exemptions, manager pages | Learning, Users (audiences) |
| **Learning.Commerce** | Paid Learning, Learning Subscriptions, Licence Pools | Learning product part, order grants, entitlement applier, licence pools and seat holders | Learning, Products, Carts, Orders (Subscriptions, Accounts optional) |
| **Learning.Classes** | Offerings, Classes | Course offering (run), the class course item, the link from a registration to an enrollment | Learning, Scheduling, Calendars |
| **Learning.Coursework** | Coursework, Rubrics | Submission item, submissions, rubrics, grading list | Learning.Assessments (Gradebook) |
| **Learning.Packages** | SCORM, cmi5 | Package store, package item, runtime state | Learning |
| **Learning.Xapi** | Learning Record Store | xAPI statements and document APIs | Learning, OrchardCore.OpenId |
| **Learning.Lti** | External Tools | Tool settings, launch, Deep Linking, roster and grade services | Learning, OrchardCore.OpenId, OrchardCore.Secrets |
| **Learning.Credentials** | Digital Credentials | Credential definitions, awards, signed Open Badges, public endpoints | Learning, OrchardCore.Secrets (Skills optional) |
| **Learning.AI** | Learning Assistant, Authoring Assistant | Tutor profile template, Learning AI tools, the enrollment retrieval filter | Learning, AI |

Each module carries `[RequireFeatures]` startups for Recipes, Deployment, CrestApps Recipes schemas, Workflows, Data Pipelines, Reports, Audit Trail, Notifications, Rate Limits, Webhooks, Staff, Teams, Accounts, Workforce, Commerce and Seo, as `src/Modules/CrestApps.OrchardCore.Staff/Startup.cs` does.

### 6.1 Vocabulary

These names avoid terms other modules own. The right column is the reason.

| Learning term | Not | Because |
| --- | --- | --- |
| **Course item** (`CourseItemPart`, `AddCourseItemKind`); the page, video and file kinds show as "Lesson" | Activity | `OmnichannelActivity`, Workflows activities, Workforce `ActivityKind` and `ActivityCode` |
| **Class**, `ClassRegistration` link | Session | `CheckoutSession`, `WizardSession`, `CallSession`, AI chat sessions; Scheduling's own "session" |
| **Required learning** (`LearningRequirement`) | Assignment | `SkillAssignment`, `VisitAssignment`, `IActivityAssignmentService`. Graded work a learner hands in is **coursework**. |
| **Education credit** (`EducationCreditType`, `EducationCreditEntry`) | Credit | Store credit, `CreditNote`, credit limits |
| **Licence pool**, **seat holder** | Seat pool, seat assignment | Bookings seats are capacity |
| **Certification path** (`CertificationPath`) | Track | Visit tracking, order tracking |
| **Digital credential** (`Learning.Credentials`, `CredentialAward`) | Badge | Staff Badges (`IStaffBadgeService`: employee number and PIN) |
| **Learning statement** | Learning event | `CalendarEvent`, `OmnichannelEvent` |
| **Classes** calendar type | Training | `WorkforceConstants.EventTypes.Training` |
| Kept: enrollment, gradebook, rubric, coursework, course, learning path, offering, attempt, submission, package, audience (it is the Users concept) | | No collision, or the same concept |

## 7. Domain model

### 7.1 Course and outline (Learning)

- **Course** is a content type: `TitlePart`, `AutoroutePart`, `HtmlBodyPart` or `MarkdownBodyPart`, a cover `MediaField`, taxonomy fields for category and topics, `SeoMetaPart`, and `CoursePart`. A site adds its own fields and parts, and other modules add cards through content display drivers.
- `CoursePart`: course code, summary, level, estimated effort, enrollment settings (open, key, approval, closed; access length in days), completion criteria (7.4), prerequisites (other courses, certifications or skills), and the pass mark.
- `CourseOutlinePart`: the ordered sections of the course. A section has an id, a title, a summary, a release rule and an ordered list of course item ids with a "required" flag. The outline is versioned with the course.
- Publication dates use PublishLater and ArchiveLater. A course's language comes from `LocalizationPart`; `CoursePart` has no language field.

### 7.2 Course items (Learning)

- A **course item** is a content item of any type that carries `CourseItemPart`: its kind, completion mode, estimated minutes, whether it is gradable and its weight, whether it is a free preview, and its release rules.
- Each course item is its own content item, contained in the course with Orchard's `ListPart` and `ContainedPart` (check 1). A course with 200 lessons stays light to load, and each lesson has its own draft, version, preview, localization and search entry.
- A kind is registered with `AddCourseItemKind<TPart>(name, ...)`: its display names, the completion modes it supports, whether it produces a grade, and its player shape. `ICourseItemHandler` hears started and completed.
- Kinds in phase 1: **Page** (an Orchard body part with attached files), **Video** (a `MediaField` file or a `YoutubeField`; the player, captions, transcript and watched-percentage tracking are new, because Orchard Core has no player), **File**, **Link**, and **Acknowledgement** (the learner confirms "I have read and understood"; stored as an immutable attestation with the text version).
- Kinds from later modules: Quiz, Coursework, Class, Package, External tool.
- **Release rules** are open (`ICourseItemReleaseRule`): another item completed or passed with a minimum grade, a date, a number of days after enrollment, an offering.
- **Files.** Lesson files are attached media on the course item. With Secure Media on, Orchard Core authorizes each file request by `ViewContent` on the owning item, so the content access handler in section 8 protects the file with no further code. Large videos upload through Media.Tus.

### 7.3 Enrollment (Learning)

`Enrollment` (catalog document): user id, course id, offering id (empty until phase 6), status, enrollment number, started, access ends, due, completed, last activity, progress percentage, course grade, cycle number and previous enrollment id, and `Properties`.

- **One pass, one record.** An enrollment is one pass through a course. Taking the course again creates a new enrollment that links to the earlier one. At most one enrollment per user, course and offering is open at a time.
- **Grants.** An enrollment holds a list of grants `{ Source, SourceId, AccessEndsUtc }`. Access stays while any grant remains, the same rule `IRoleGrantService` uses for roles. Sources register with `AddEnrollmentSource`: Manual, Self, Key, Pipeline, Workflow, and later Order, Subscription, Licence, Requirement, Path, Renewal.
- **States** in a `TransitionTable`: Pending, Active, Suspended, Completed, Expired (access ended before completion), Withdrawn.
- **Approval is a state, not an engine.** A course that needs approval puts the enrollment in Pending. A person with `ManageEnrollments` in scope approves or rejects it from the list. Who is asked, reminders and escalation are Orchard Workflows on the "Enrollment requested" event. Learning adds no approval records, routing or approver rules.
- **Course staff.** A course (and later an offering) lists its staff with an open role: Instructor, Assistant, Grader, Observer. Staff are staff members; the list stores ids only.
- **Bulk enrollment is a data pipeline.** Learning registers an **Enroll learners** destination step. A pipeline reads a CSV or spreadsheet from the media library or an external source, joins the `Users` data set, and writes enrollments, with a preview, a rejected output for rows it cannot match, run history and schedules from Orchard Core. There is no Learning import page.

### 7.4 Progress and completion (Learning)

- `EnrollmentProgress` is a separate document per enrollment: for each course item its state, first and last access, time spent, score, attempts and completion time, and whether the result was self-reported. Progress writes never touch the enrollment document. A map index gives one row per item for reports.
- **Completion modes** per item: viewed, marked done by the learner, minimum time, submitted, graded, passed, attended, or reported by a package.
- **Course completion criteria** (open, `ICourseCompletionCriterion`): all required items; plus optional minimum course grade, a named assessment passed, minimum time, or approval by a member of the course staff.
- On completion the enrollment stores an immutable, self-contained **completion snapshot**: the course title and version, each required item with its title and result, and the grade. It does not depend on old content versions staying in the database.
- **Time spent** is Learning's own data per course item. For staff, a bridge startup can send paid training time to TimeTracker with `ITimeTrackerService.SyncAsync`.
- There is no event log in phases 1 to 7. Reports read enrollments, progress, attempts and the Audit Trail. The xAPI statement store arrives with the LRS in phase 8.

### 7.5 Assessments (Learning.Assessments)

- **Questions are content items** with `QuestionPart` (points, difficulty, tags, general feedback) and a type part. Types in phase 2: single choice, multiple choice, true or false, short text, numeric with tolerance, matching, ordering, fill in the blanks, and essay. A new type is a content type, a part, an editor driver and an `IQuestionScorer`.
- **Question banks** are container content items (`ListPart`) that list their questions. Random draws select by bank, tag and difficulty.
- **An attempt is self-contained.** When the server draws a question, the attempt copies what is shown and what scores it: the prompt, the options in their shuffled order, the key, the points and the feedback, with the question id and version id for reference. Orchard Core's version pruning (`OrchardCore.Contents.VersionPruning`) can delete old versions and has no veto, so an attempt never depends on one. Learning also warns in its settings when a Question or Course type is selected for pruning.
- **Quiz** is a course item kind with `QuizPart`: fixed questions and random-draw rules, shuffle, time limit, number of attempts, delay between attempts, pass mark, grading method (highest, latest, average, first), navigation, availability window, access code, honor statement, and the review policy.
- `AssessmentAttempt` (catalog document): user, enrollment, quiz, number, status (in progress, submitted, needs grading, graded, abandoned, voided), start, server deadline, submit time, the drawn items, the responses, automatic and manual scores, grader, feedback, score and pass flag.
- **Server rules.** The server draws the questions, fixes the deadline, saves each answer as the learner gives it, accepts the submit once, and submits at the deadline through the leased work queue. The key and the feedback are never sent to the browser before the review policy allows.
- **Keeping questions private.** Marking a type securable does not deny anything in Orchard Core: `ContentTypeAuthorizationHandler` only grants. So the Learning access handler explicitly **fails** `ViewContent` on a question for everyone except authors and graders in scope (the way Orchard's media folder handler denies). That one rule covers the display route, the content API, GraphQL and the `Contents` data source. The migration also hides question types from GraphQL, and Learning warns when a question type is added to a search index.
- **A regrade** creates new scores with an actor and a reason and keeps the old ones.
- **Overrides** per person or audience: more time, more attempts, another window. Each needs a reason. This is also the accessibility accommodation.
- **Integrity hooks**: one open attempt per person, an optional IP range rule, and an `IProctoringProvider` contract for a later integration.
- **Item analysis**: difficulty and discrimination per question, as a data set and a report.
- **Importing questions** from spreadsheets (and QTI in phase 8) is a ContentTransfer file format, because questions are content.

### 7.6 Gradebook (Learning.Assessments, feature Gradebook)

- A **grade item** exists for each gradable course item, with a weight. The course grade is the weighted result against the course pass mark.
- `GradeScale` (catalog record) maps percentage bands to letters or to pass and fail.
- A manual grade change needs a reason and keeps the earlier value.
- Grades can be released at once or held until the instructor posts them.
- Categories, drop-lowest and late policies come with Coursework in phase 7. Any calculation a site can configure uses the Data Pipelines formula language, not a new one.

### 7.7 Learning paths and required learning (Learning.Programs)

- **Learning path** is a content type with `LearningPathPart`: ordered course sets, each set joined by all, any or "N of", and sets joined by then, and, or. Enrolling in a path enrolls the person in the courses as each set opens.
- **Required learning** (`LearningRequirement`, catalog record): a target (course, path or certification path), a user audience (5.3) or named people, a due-date rule (a fixed date, N days after it applies, N days after the hire date), reminders before the due date, and escalation to the manager when overdue.
- Each person's state is a record: required, in progress, completed, overdue, exempt. An **exemption** (recognition of prior learning) is an action by a person with the permission, with a reason and optional evidence.
- Rules are evaluated when membership changes and on a nightly reconcile. The page shows when each rule last ran and has **Run now** and a preview of who it covers.
- **Team Learning** gives a manager the state of their direct and indirect reports through `IStaffHierarchy`, with no access to answers or attempt detail.

### 7.8 Certification (Learning.Certifications + Skills)

The certification and each person's certificate belong to Skills (5.1). Learning adds how it is earned.

`CertificationPath` (catalog record, one per Skills certification, shown as a card on the certification editor):

| Setting | Meaning |
| --- | --- |
| Initial requirements | What earns it the first time. |
| Renewal requirements | What renews it. Empty means the initial requirements. |
| Requirement kinds | Joined by all or any: course completed (with a minimum grade), learning path completed, assessment passed (self-reported results never count), education credits earned within a period, evidence accepted, sign-off by a person with a named permission. Open through `ICertificationRequirement`. |
| Reference date | **Completion date**, **expiry date** (the anniversary is kept), or **fixed date** (every certificate expires on the same day of the year). |
| Validity | From the Skills certification (`ValidityMonths`). |
| Renewal window | How many days before expiry renewal opens. |
| Grace period | How many days after expiry the renewal requirements are still allowed. |
| Minimum interval | For the fixed date rule, the shortest first cycle. |
| On window open | Enroll the holder in the renewal courses, notify the holder, notify the manager. |
| Certificate template, digital credential | The template for the PDF and the credential to award. |

`CertificationPathProgress` (per person and path): state (not started, in progress, certified, renewal open, renewal in progress, lapsed, revoked), cycle number, current certification record id, the date the window opens, and the list of past cycles with the evidence (enrollment ids, attempt ids, credit entries).

Flow:

1. A person starts the path (by hand, by purchase or by required learning). The path enrolls them in its courses.
2. When the requirements are met, Learning calls `ICertificateIssuer.IssueAsync`. Skills creates the record with a generated number and a verification code, computes the expiry, renders the certificate, and raises "issued".
3. At expiry minus the window, a leased work item opens the renewal: a new cycle, new enrollments, notices. Nothing from the earlier cycle is reset or deleted.
4. On renewal, Skills creates a new record linked to the old one.
5. Skills' daily check sends the expiry reminders (`ReminderDays`) and marks an unrenewed record expired. Learning hears `ICertificationExpiryObserver` and moves the progress to lapsed.
6. A revocation in Skills shows on the verification page at once.

The public verification page (in Skills) shows the least data that proves the claim: holder name, certification, issuer, issue and expiry dates, and status.

### 7.9 Education credits (Learning.Certifications, phase 4)

- `EducationCreditType` (catalog record): a name such as CEU, CPD hours or PDU, and a unit.
- A course, a path or a class awards credits of a type on completion.
- Entries are posted to the quantity ledger (5.7): person, type, amount, source and source id, date earned, optional expiry.
- A person can submit external learning with evidence (a protected file, 5.6); a person with the permission accepts it into the ledger. Bulk records arrive through the **Record external learning** pipeline step.
- A certification requirement can ask for N credits of a type within the cycle.

### 7.10 Offerings and classes (Learning.Classes, phase 6)

- `CourseOffering` (catalog record): a dated run of a course with a code, start and end, an enrollment window, a capacity, its own staff, and pacing. The enrollment's offering id exists from phase 1.
- **Class** is a course item kind: "attend a class". Its dates are dated sessions in Scheduling (5.5), with instructors as scheduling resources backed by staff members, rooms as calendar resources, a location or a pasted meeting link, capacity, a registration window, confirmation and waitlist settings.
- A learner's registration is a Scheduling appointment whose `Source` and `SourceId` point at the enrollment. Learning stores no seat counts, holds or waitlist.
- The class is one calendar event with its attendees, so reminders, .ics invitations with RSVP, feeds and room conflicts come from Calendars. Due dates appear on a learner's calendar through `ICalendarEventSource`.
- Learning hears `IAppointmentObserver`: attendance completes the course item and can award education credits.
- With Workforce on, a staff class becomes a shift segment with the Training activity code, so the shift plan and adherence see it.
- Nothing in the solution creates online meeting links. If that is wanted, it belongs in Calendars, not in Learning.

### 7.11 Selling (Learning.Commerce, phase 5)

- **A course that is sold is a product.** With Learning.Commerce on, the Course and Learning path types carry `ProductPart` and `LearningProductPart`. A free course uses `ProductSalesMode.ListingOnly`. The catalog is then the Products listing with its facets and search providers; Learning adds course fields to the index through `IProductSearchDocumentContributor`.
- `LearningProductPart` lists what a purchase grants: the course itself, other courses or paths (a bundle), a certification path, extra assessment attempts, or a number of seats, each with an access length.
- `ICartLineHandler` refuses a guest for a personal purchase and asks who the seats are for on a seat purchase.
- `OrderLineGrantHandlerBase<LearningProductPart>` grants on payment and removes the grant on refund or cancellation. The grant's source id is the order line id.
- `ISubscriptionEntitlementApplier` with a Learning kind gives access to a course, a path or the whole catalog while a subscription is active.
- **Licence pools.** `LicensePool`: owner (`PartyRef`, normally an account), product, expiry, with its balance on the quantity ledger (5.7). `SeatHolder`: a user, or a pending account invitation (5.4). An account admin gives seats, invites people and sees the progress of the account's members only, on pages under `account/business`.
- Prepaid vouchers are Checkout coupon codes. B2B order approval, budgets, terms, taxes, receipts and installments apply with no Learning code.

### 7.12 Coursework (Learning.Coursework, phase 7)

- **Coursework** is a course item kind: instructions, due date, allowed file types and size, text or file submission.
- `Submission`: person, files in the protected store (5.6), text, submitted time, late flag, status, grade, feedback, grader.
- **Rubrics** are content items: criteria with levels and points.
- The grading list is a filtered list of submissions and attempts that wait for a grader. It is not a task record; `OmnichannelActivity` stays the only work item.

### 7.13 Standards (phase 8)

- **Packages.** An author with the `ImportLearningPackages` permission uploads a SCORM 1.2, SCORM 2004 or cmi5 package. The upload passes `FileCreationService`, is extracted in an `ITempDirectoryProvider` folder with safe paths, and is stored in the protected store. Package content is untrusted script: it runs in a sandboxed frame on a separate origin and talks to the runtime adapter through messages only. The server stores the runtime state per enrollment and maps it to progress, marked self-reported.
- **LRS.** `Learning.Xapi` stores learning statements and exposes the statements API (with immutability and voiding), the state and profile document APIs and the about resource. An LRS client is an Orchard Core OpenID application whose role holds the LRS permissions, on the `Api` scheme. If xAPI clients need HTTP Basic, a Basic handler reads the same application records.
- **External tools.** A tool is an OpenID application (client id, redirect URIs, key set) plus Learning's own settings record (deployment id, launch URLs, custom parameters, privacy level: anonymous id, name, name and e-mail). The platform's signing key is an `RsaKeySecret`. The roster and grade services use the OpenID server's token endpoint and scopes, validated by OpenId.Validation. Learning builds only what LTI adds: login initiation, the LTI claims in the launch token, validation of a Deep Linking response against the tool's key set, and the service endpoints.
- **Digital credentials.** A credential definition is a content item (image, criteria, description). `CredentialAward` holds an Open Badges 3.0 credential signed with a key from Secrets, with a revocation status.
- **QTI import** is a ContentTransfer file format that creates question content items.

### 7.14 AI (Learning.AI, phase 9)

Reused as it is: AI profiles and profile templates, prompt templates with `ITemplateService`, the front-end chat widget and chat sessions (AI.Chat), per-user memory (AI.Memory), documents on a profile (AI.Documents), content indexing for retrieval (AI.DataSources), content drafting tools (AI.Agent's `createOrUpdateContentItem`), usage metering and chat analytics, AI workflow activities, the MCP server that exposes tools to outside agents, and NLWeb's permission-trimmed answers over content.

New in Learning.AI, and nothing else:

- **An enrollment-scoped retrieval filter.** Today the only retrieval filter is a static string on the data source. The tutor must search only the lessons of the courses the learner is enrolled in, so Learning adds a per-request filter (or uses NLWeb's retrieval, which already trims by `ViewContent` and therefore by Learning's access handler). Question banks are never indexed.
- **Learning tools** through `AddCoreAITool`: my progress, my next lesson, explain my result (only when the review policy allows), who is overdue and who expires soon (with the caller's scope).
- **A tutor profile template** and a per-course switch, off by default.
- **Author prompts**: draft an outline, draft questions from a lesson, draft a rubric, translate. Each result is a draft content item an author reviews.
- **Essay scoring help**: a suggested score and feedback against the rubric. A grader confirms or changes it, and the record says it was AI-assisted. AI never finalizes a grade and never issues a certificate.

## 8. Roles, permissions and access

Permissions are static fields in each `.Abstractions` project, with implied-by chains. Suggested roles are created by a recipe with Orchard Core's Roles step: Learning Administrator, Course Author, Instructor, Grader, Training Manager, Learner.

| Permission | Meaning |
| --- | --- |
| `ManageCourses`, `ManageOwnCourses` | Create and edit courses and course items; "own" covers courses where the user is on the course staff. |
| `ManageQuestionBanks`, `ViewQuestionBanks` | Author questions; see answer keys. |
| `ManageEnrollments`, `ViewEnrollments` | Enroll, approve, suspend, withdraw; see who is enrolled. |
| `ViewLearnerRecords` | See another person's progress, grades and attempts, within scope. |
| `GradeAssessments`, `OverrideGrades`, `ManageAssessmentOverrides` | Grade; change a grade with a reason; give more time or attempts. |
| `ManageLearningPaths`, `ManageRequiredLearning`, `GrantExemptions` | Author paths; require learning of audiences; exempt a person. |
| `ManageCertificationPaths` | Configure how a certification is earned. Issuing and revoking are Skills permissions. |
| `ImportLearningPackages`, `ManageExternalTools` | High trust: package content is script, and tools receive learner data. |
| `ViewLearningReports`, `ExportLearnerRecords` | Reports; exports of personal data. |
| `AccessLearning` | Be a learner: catalog, own learning, own transcript, own certificates. |

Class scheduling and attendance use Scheduling's permissions.

**Scope.** One authorization handler (the pattern of `CalendarEventAuthorizationHandler`) narrows the broad permissions by resource: a member of the course staff gets the instructor's rights on that course only; a manager sees the records of their reports through `IStaffScopeService`; an account admin sees the members of their account; a learner sees only their own records. Callers check the broad permission with the resource, and requests load the record by id, never from a capped list.

**Content access.** A second handler on Orchard's `ViewContent` covers course items and questions. It **succeeds** for the course staff and authors, for an active enrollment within its access dates when the release rules are met, and for anyone when the item is a free preview. Otherwise it **fails** the requirement, because a role that holds a general `ViewContent` permission would otherwise read any lesson or question. Secure Media applies the same decision to attached files. ContentAccessControl is not used, because it restricts by role only.

## 9. Security and privacy

### 9.1 Threats and controls

| Threat | Control |
| --- | --- |
| A learner changes their own score | All scoring, timing and completion on the server. Self-reported results are marked and cannot issue a certificate by default. Submits are idempotent. |
| A learner reads the answer key | The access handler fails `ViewContent` on questions; types are hidden from GraphQL; the key is never in a player response before review. An automated test inspects the exam player, the display route, the content API, GraphQL and the `Contents` data source. |
| A person reads another person's records (IDOR) | Every endpoint checks a permission and the record's owner or scope. A test matrix runs learner A against learner B's enrollment, attempt, submission, certificate and files. |
| A forged certificate | Verification is a server lookup by a random code. Revocation shows at once. Signed credentials come with Open Badges 3.0. |
| Guessing verification codes, enrollment keys or access codes | Orchard Core Rate Limits policies registered in a gated startup, a code of at least 80 bits, uniform responses, and reCAPTCHA as an option on the public page. |
| Script in authored content | Lesson bodies pass Orchard's HTML sanitizer. Package content runs in a sandboxed frame on a separate origin. Uploading packages needs a high-trust permission. |
| Hostile uploads | Every upload passes `FileCreationService` (and so the antivirus when it is on) before it is stored; type and size limits; extraction in a tenant temporary folder with safe paths. |
| Leaked lesson files | Secure Media authorizes each request by the owning item and answers 404 without access. Private files use the protected store's policy endpoint. |
| A forged tool launch or service call | LTI 1.3 only, on Orchard Core's OpenID server with keys in Secrets. Deep Linking responses are validated against the tool's key set. |
| A tool or AI model receives too much personal data | A privacy level per tool. The tutor's retrieval is filtered by enrollment and never includes exam content. |
| Tampering with history, or history lost | Completed enrollments, submitted attempts, issued records, ledger entries and attestations are append-only and self-contained. They are the compliance record. |
| Abuse by staff | Grade changes, overrides, manual completions, exemptions, certificate issue and revoke, and every export of learner records carry an actor and a reason on the record, and are also written to the Audit Trail. The Audit Trail is the second view only: it trims after 10 days by default and its events can be switched off. |
| Duplicate grants from repeated events | Idempotency keys from stable business ids (order line id, requirement id, certification cycle). |
| CSV formula injection | Exports go through Reports, Data Pipelines and `CsvWriter`. |
| Log forging and leaked identifiers | `SanitizeLogValue()` on logged input; no ids, e-mail addresses or names on metric tags. |

### 9.2 Privacy

- **Confidential records.** Grades, attempts, submissions and attendance are visible to the learner, the course staff of that course, and people whose scope covers the learner. Managers see status and dates, not answers.
- **Personal data.** Each module registers an `IPersonalDataContributor` that exports the person's records and anonymizes them on erasure. Aggregates that no longer identify a person stay.
- **Retention.** `IRetentionPolicy` entries, with site settings, for attempt response detail, package runtime state, submissions, evidence files and learning statements. Records that prove compliance (completion snapshots, certification records) have a minimum retention that an erasure request does not shorten without an administrator's decision.
- **Consent and policy.** The Acknowledgement item records acceptance of a policy text with its version. Contact consent for marketing sends stays with Consent and Outreach. Learner notices use Orchard Notifications and the user's own preferences per notification type.
- **Least data.** Attempts keep no device fingerprint. An IP address is kept only where an IP rule is in use.
- **Report runs.** A saved report runs as its owner. A data set returns no schema to a user who cannot read it.
- **Encryption and secrets.** Private files with personal data use `EncryptedFileStore`. Keys and client secrets are Orchard Core secrets.
- **Children.** The first releases are for adult and workplace learning (section 14).
- **Accessibility.** The learner pages and the exam player meet WCAG 2.2 AA: keyboard operation, visible focus, no timing without a way to extend it (overrides), captions and transcripts on video. An automated check runs on the player in CI.

## 10. Rules every module follows

### 10.1 Data portability

Content items (courses, course items, questions, banks, learning paths, rubrics, certificate templates, credential definitions) move with Orchard Core's `ContentDefinition` and `Content` steps and the Contents deployment features, as Accounts do. Roles, secrets, OpenID applications and site settings move with the steps their owners already have. Every Learning catalog record gets a recipe step (`AddRecipeExecutionStep`), a deployment step (`AddDeployment`), an `IRecipeStep` schema and a deployment step schema, the schemas in a startup gated by `CrestAppsRecipesConstants.Feature.Area`. Site settings get an `ISiteSettingsSchemaDefinition`; options bound from configuration get a `ConfigurationSchema.json`.

| Record | Owner | Step | Kind |
| --- | --- | --- | --- |
| Enrollment with its progress | Learning | `LearningEnrollments` | Learner record |
| Grade scale | Assessments | `LearningGradeScales` | Configuration |
| Assessment attempt, assessment override | Assessments | `LearningAttempts` | Learner record |
| Certification, certification record | Skills | `Skills` (exists; gains the new fields) | Both |
| Certification path | Certifications | `LearningCertificationPaths` | Configuration |
| Certification path progress | Certifications | `LearningCertificationProgress` | Learner record |
| Education credit type | Certifications | `LearningCreditTypes` | Configuration |
| Education credit entries | Certifications | `LearningCredits` | Learner record |
| Admin-defined audience | Users | `UserAudiences` | Configuration |
| Required learning, per-person state, exemptions | Programs | `LearningRequirements` | Configuration, learner record |
| Account invitation | Accounts | `AccountInvitations` | Learner record |
| Licence pool, seat holder | Commerce | `LearningLicences` | Learner record |
| Offering | Classes | `LearningOfferings` | Configuration |
| Dated session, registration, waitlist entry | Scheduling | Scheduling's own steps, extended | Both |
| Submission | Coursework | `LearningSubmissions` | Learner record |
| Package (manifest and files) | Packages | `LearningPackages` | Configuration |
| External tool settings | Lti | `LearningExternalTools` (the client and its secret move with OpenID and Secrets) | Configuration |
| Credential award | Credentials | `LearningCredentialAwards` | Learner record |

Rules for the steps:

- **Configuration** steps use `NamedCatalogRecipeStep` and `NamedCatalogDeploymentSource` where the record has a name.
- **Learner record** steps are opt-in deployment steps with options (course, date range), read in pages of 500, need `ExportLearnerRecords`, and record the export on the Audit Trail. They refer to people by user name and report rows they cannot match as recipe errors.
- A step that imports fills cross-references in a second pass, because a file lists records in any order.
- Recipe item ids are generated unique ids, never readable names.

### 10.2 Workflow activities

Registered with `AddActivity` in a startup gated by `OrchardCoreFeatureIds.Workflows`, each with design, thumbnail and edit views and an `IWorkflowActivitySchemaDefinition`. Events fire after the change is saved, with the record id as the correlation id, and events caused by a workflow task carry an actor so a workflow does not trigger itself (the Calendars pattern). The plan only adds activities; it does not change the workflow engine.

| Owner | Events | Tasks |
| --- | --- | --- |
| Learning | Enrollment requested, Learner enrolled, Enrollment status changed, Course started, Course item completed, Course completed, Access expiring, Learner inactive | Enroll user, Approve enrollment, Reject enrollment, Change enrollment status, Extend access or due date, Mark course item complete |
| Assessments | Attempt started, Attempt submitted, Attempt needs grading, Attempt graded, Assessment passed, Assessment failed, Grade changed | Grant attempt override, Void attempt |
| Skills (new, beside Expiring and Expired) | Certificate issued, Certificate renewed, Certificate revoked | Issue certificate, Revoke certificate |
| Certifications | Renewal window opened, Certification progress changed, Education credits awarded, External learning submitted | Start certification path, Award education credits, Accept external learning |
| Programs | Learning required, Required learning due soon, Required learning overdue, Learning path completed, Exemption granted | Require learning, Grant exemption |
| Commerce | Seats purchased, Seat given, Licence pool running low | Give seat |
| Accounts (new) | Invitation sent, Invitation accepted | Invite to account |
| Scheduling (extended) | Its existing events plus Promoted from waitlist and Attendance marked | Its existing tasks plus Mark attendance |
| Coursework | Coursework submitted, Coursework graded | Return for resubmission |
| Packages, Lti, Credentials | Package completed, External score received, Credential awarded | Award credential |

### 10.3 Data pipelines and reports

Each module registers one Orchard Core data source with `AddDataSource<T>()` in a startup gated by `OrchardCoreFeatureIds.DataPipelinesCore`. The same source serves Data Pipelines and the Report Builder. Data set and field names are never renamed. Each date-read index has a database index on the date column and `Id`. Courses and other content are exposed through `ContentDataSourceOptions`, not a custom data set.

| Source | Data sets |
| --- | --- |
| `Learning` | Enrollments, Course item progress, Completions, Course staff |
| `Learning.Assessments` | Attempts, Attempt responses (item analysis), Grades, Overrides |
| `Skills` (new) | Certifications, Certification records, Skill assignments |
| `Learning.Certifications` | Certification paths, Path progress, Education credits |
| `Learning.Programs` | Required learning, State per person, Path progress |
| `Learning.Commerce` | Licence pools, Seat holders |
| `Learning.Classes` | Offerings (class registrations and attendance come from Scheduling's source) |
| `Learning.Coursework` | Submissions |
| `Learning.Xapi` | Learning statements |
| `Learning.Credentials` | Credential awards |

Pipeline **destination steps** (`AddDataPipelineStepType<T>`, each with a rejected output): Enroll learners, Award education credits, Record external learning.

Coded reports (`IReport` + `AddReport<T>`, under **Reports**, each with an export permission, so report subscriptions work with no extra code): Course completion, Learner transcript, Compliance status, Certification status and expiring certificates, Assessment results and item analysis, Class attendance, Education credits earned, Inactive learners, Seat use. Admin Dashboard widgets show overdue learning and expiring certificates.

### 10.4 Other integrations

- **Notifications**: notification types registered with `AddNotificationType` for enrolled, due soon, overdue, graded, certificate issued, certificate expiring, renewal open and seat given, each with `Type`, `ReferenceId` and `Url`. Built-in notices are plain, localized text that a user or a site can switch off per type; a site that wants its own wording uses the workflow events with Orchard's notify and e-mail tasks. Class notices come from Scheduling and Calendars.
- **Audit Trail** (gated by `OrchardCoreFeatureIds.AuditTrail`): the events in section 9.1.
- **Rate Limits** (gated by the Orchard Core feature): section 9.1.
- **Webhooks**: `AddWebhookEvent` for enrollment created, course completed, assessment graded, certificate issued, renewed and revoked, with stable event ids.
- **APIs**: every Learning API uses the `Api` authentication scheme, checks a permission, returns Problem Details, and appears in OpenAPI. It follows the Calendars and Scheduling API pattern.
- **Telemetry**: `CrestAppsOrchardCoreTelemetry` meters and activity sources named `CrestApps.OrchardCore.Learning`, on our own operations only.

### 10.5 User interface and time

- Admin lists render through `<admin-list>` with row drivers for `SummaryAdmin` and `SummaryAdminGrid`; filters are bootstrap-select pickers in the list toolbar. Lists of content (courses, questions, paths) are Orchard's content list with a filter term, as B2B accounts are.
- Admin forms use the `ocat-*` layout in cards and keep their buttons in `action-bar-bottom`. Every admin page declares a breadcrumb.
- Editors render through `IDisplayManager<TModel>` and display drivers.
- Every time shown to a person is local, through `ILocalClock`. Certification issue and expiry dates are pure dates and are not converted.
- Scripts are ES modules built by the asset pipeline, with no jQuery.

## 11. Pages

**Learner.** Learning adds only the pages that do not exist: the course page, the player, the exam player, **My learning**, the transcript. Everything around them is reused:

- The **catalog** is an Orchard Core search or query page over published courses; with Learning.Commerce on it is the Products listing.
- **My learning** and **My certificates** are entries in the storefront account menu (`CommerceConstants.Account.MenuName`) when Commerce is on, a link from `IStaffHomeLinkProvider` and `IMyWorkLinkProvider` for staff, and a link in the user menu otherwise. Learning builds no account area of its own (decision 16).
- **My certificates** and the public **verification page** are Skills pages.
- **Organization pages** for an account admin are sections of `account/business`.
- A learner's notices are listed with `INotificationManager`.
- Class registration uses Scheduling's self-service pages where they fit.

**Admin.** Courses (content list with the outline editor), Question banks, Learning paths, Enrollments, Required learning, Grading list, Offerings, Packages, External tools, Settings. Certification paths are a card on **Skills > Certifications**. Audiences are under **Users**. Classes are under **Scheduling**. A **Learning** section appears on the user editor and on the staff profile page through page sections.

## 12. Order of work

One branch and one pull request per phase, with a commit per slice and a checklist in the pull request. Each phase ships its recipe and deployment steps, schemas, workflow activities, data source, reports, audit events, personal-data contributor, tests and documentation; none of these is deferred. Phases 5 to 9 do not depend on each other and can change order.

| Phase | Scope | Done when |
| --- | --- | --- |
| **0. Decisions and spikes** | Answer section 15. Spikes: outline containment on `ListPart` (check 1), Secure Media with the content access handler (check 2), a certificate page with the PDF renderer (check 3). | The decisions are recorded in this file and the three spikes have a result. |
| **1. Learning core** | Course, outline editor, course item kinds (Page, Video, File, Link, Acknowledgement), release rules, enrollment (manual, self, key, pending and approve), grants, the Enroll learners pipeline step, progress, completion snapshot, the two authorization handlers, Secure Media, learner pages, catalog on Orchard Search, permissions and roles recipe, notification types. | A learner enrolls, works through a course on a phone and on a desktop, and completes it; a person who is not enrolled cannot open a lesson or its files by URL, API or GraphQL; a recipe moves the course and its enrollments. |
| **2. Assessments** | Question types and banks, quiz item, self-contained attempts, deadlines, grading of essays, overrides, regrade, gradebook and scales, item analysis, question import through ContentTransfer. | A timed exam with random draws is taken, auto-submitted at the deadline, graded and reviewed; no route returns the key before review; an attempt still reads correctly after its question version is pruned. |
| **3. Certification** | Skills changes (5.1), the Pdf building block (5.2), certificate templates, certification paths with the three reference dates, path progress, compliance report. Issue is idempotent (one certificate per enrollment and certification, however often the completion event arrives) and the daily upkeep issues what a lost event missed (review round 2 D: observers run after the commit and swallow their failures). | **Minimum product.** A person earns a certificate from a course and an exam, verifies it in public, receives reminders, renews it in the window, and both cycles stay in the history. Workforce and Dispatch see the certification. A test delivers the completion event twice and gets one certificate; a test drops the event and the upkeep issues it. |
| **4. Programs and required learning** | User audiences (5.3), learning paths, required learning with due dates, exemptions, reminders and escalation, team learning, the quantity ledger (5.7), education credits, protected files (5.6) for evidence. | A rule requires a path of new staff of a team with a due date; the manager sees status; a certification renews by credits; Workforce time off runs on the shared ledger. |
| **5. Commerce and organizations** | Course as product, catalog on the Products listing, order grants and refunds, subscription entitlement, account invitations (5.4), licence pools, organization pages, Schema.org `Course`. | A person buys a course; an account buys 50 seats and its admin invites people to them; a refund removes access. |
| **6. Classes** | Scheduling extension points (5.5), offerings, the class course item, registration links, attendance to completion, the Workforce and TimeTracker bridge. | A capped class fills, a cancellation promotes the next person automatically, attendance completes the course item, and a staff class shows on the shift plan. |
| **7. Coursework** | Submissions, rubrics, grading list, gradebook categories and late policy. | A file submission is graded against a rubric and the grade reaches the course grade. |
| **8. Standards** | SCORM 1.2, then SCORM 2004, then the LRS and cmi5; LTI 1.3 with the three Advantage services on OpenID and Secrets; Open Badges 3.0; QTI import. | The public conformance tests for each standard pass, and a sandboxed package cannot reach the learner's session. |
| **9. AI** | The enrollment retrieval filter, Learning tools, tutor template, author prompts, essay scoring help. | The tutor answers from enrolled course content only; a test proves that exam content is never in its context. |

## 13. Tests and checks

- **Test first.** Each service starts with a failing unit test in `tests/CrestApps.OrchardCore.Tests`. State machines, completion evaluation, draw rules, scoring per question type, deadline handling and every expiry rule (completion, expiry and fixed dates, across leap years and month ends) have table-driven tests.
- **Integration tests** in a new `tests/CrestApps.OrchardCore.Learning.IntegrationTests`: enroll to certificate end to end, recipe round trip for every step, refund removes the grant, renewal keeps history, version pruning leaves attempts and snapshots intact.
- **Security tests:** the IDOR matrix; the answer-key scan across the player, the display route, the content API, GraphQL and the `Contents` data source; a lesson file request with and without an enrollment; idempotent submit; rate limits on verification; archive extraction with hostile paths.
- **Data source tests** as the `crestapps-report-data-source` skill requires.
- **Guard tests:** `ModuleLayeringTests`, the migration guard, the public API baselines, feature activation, the PDF licence test, and the JavaScript lint tests for breadcrumbs and the action bar.
- **Accessibility:** an automated WCAG check on the player and the exam player, plus a keyboard pass by hand.
- **Before each handoff:** a full pass in the browser of every new page, in both list layouts and both action modes, at phone width, and with a non-administrator in each role.
- **Documentation in the same change:** module `README.md`, the user and technical manual pages under `src/CrestApps.Docs/docs`, `docs/engineering/building-blocks.md`, and a skill under `.agents/skills/` for adding a course item kind, a question type and a certification requirement.

## 14. Not in this plan

- 21 CFR Part 11 validation and e-signatures. Re-authentication before a sensitive action does not exist in Orchard Core; if it is built, it belongs in the Users module.
- Flows for learners under the digital age of consent, parent and observer accounts.
- SAML sign-in and SCIM provisioning.
- A competency framework in Skills (hierarchy, named levels, CASE exchange), CLR 2.0, OneRoster, Caliper, Common Cartridge, H5P.
- Shared content libraries and blueprint courses; peer review; discussions and forums; gamification.
- Proctoring providers and lockdown browsers (the provider contract exists from phase 2).
- At-risk prediction; a mobile application and offline play.
- Catalog distribution and progress roll-up across child tenants.
- Acting as an LTI tool for another platform.
- Online meeting links (Teams, Zoom, Meet). They belong in Calendars.
- A shared approval engine. Seven modules keep private approvals today; Learning adds none. If routing rules beyond Workflows are needed, that is an extraction with its own plan.
- A shared notification template system. Commerce has `ICommerceEmailTemplates` and `ICustomerOwnerNotifier`; if Learning needs per-notice templates, extract those, do not copy them.
- Reviews of courses that are not sold, and an account area outside Commerce (decision 16).

## 15. Decisions needed

**Accepted 2026-11-13.** Mike declared the plan final, so every recommendation below is the decision. Where a spike or the code later forces the named alternative, section 19 records it.

| # | Decision | Recommendation |
| --- | --- | --- |
| 1 | Name and vocabulary | **Learning**, `CrestApps.OrchardCore.Learning.*`, with the terms in section 6.1. |
| 2 | Storage split | Authored material as content items; learner records as catalog documents. |
| 3 | Where certificates live | In **Skills**, extended to issue them (5.1). Learning keeps no certificate store. |
| 4 | Renewal | A new record per cycle, linked to the old one. |
| 5 | PDF | Extract a Pdf building block on PdfPig and move Reports.Pdf onto it (5.2). The cheaper alternative for a first release is an HTML certificate that the browser prints. |
| 6 | Audiences | Extend the Users audience concept (5.3). No new module. |
| 7 | Classes | Build on Scheduling with the five extension points (5.5). Not Bookings, and no new seat block. |
| 8 | Lesson files | Orchard Core Secure Media with attached media. Its cost: turning the feature on changes media access for the whole tenant, and files stay under `/media`. The alternative is the protected file store (5.6) for lessons too. |
| 9 | Private files and the quantity ledger | Extract both from their first copies (Downloads, Workforce time off) in phase 4 and migrate those copies. The alternative is private copies in Learning, recorded in the reusability audit. |
| 10 | Catalog | Orchard Search in the core; the Products listing when Learning.Commerce is on. The alternative, every course always a product, makes Products a dependency of every Learning site. |
| 11 | Offerings | Reserve the offering id on the enrollment in phase 1; build offerings in phase 6. |
| 12 | Guests | A learner must sign in, except for free preview items and the verification page. |
| 13 | Learner records in recipes | Opt-in deployment steps with a permission and an audit entry. The alternative is configuration steps only, with learner data moving through Data Pipelines. |
| 14 | Self-reported scores | They never count toward a certification unless an administrator allows it on that item. |
| 15 | Translated courses | Each translation is its own course and enrollment. A requirement ("course completed") is met by any course in the same localization set. |
| 16 | The learner's account area | Entries in the existing menus (Commerce account, staff home, user menu). The alternative is to move the account area out of Commerce into a shared owner, which is its own plan. |
| 17 | First release | Phases 0 to 3, one pull request per phase. |

## 16. Checks to make before the build

1. **Outline containment.** That the outline editor can sit on `ListPart` and `ContainedPart`. Accounts found placement traps with `ListPart`.
2. **Secure Media.** That attached media on a course item is authorized by `ViewContent` on that item with the Learning handler in place, which version the check uses, and what the feature changes for the tenant's other media.
3. **PdfPig for certificates.** Background images, embedded fonts for non-Latin names, and a QR code library with a permissive licence.
4. **Pipeline step registration.** `AddDataPipelineStep<TStepType, TDriver>` and `ContentDataSourceOptions` are in the Orchard Core module assembly, not in an abstractions package. Confirm how a module references them within the layering rules.
5. **`CertificationRecord.Properties`.** `CatalogItem` appears to derive from `ExtensibleEntity`; confirm before choosing between `Properties` and typed fields.
6. **LTI on the OpenID server.** That the authorization endpoint accepts an LTI launch (`prompt=none`, the LTI claims through an OpenIddict event handler), that an application can hold a tool's key set, and that the server's key set endpoint is published. This needs a spike at the start of phase 8.
7. **Audience as a data filter.** Whether Orchard Core data source filters can express "hired more than N days ago" and joins to the owners' data sets.
8. **Retrieval filter.** Whether a per-request retrieval filter can be added through the AI completion context handlers, or whether the tutor should use NLWeb's retrieval.
9. **Subscriptions and Orders.** Subscriptions do not pass through Orders today, so access sold by subscription does not appear in order reports.
10. **Feature id literals.** `Staff/AIToolsStartup.cs` uses a string literal for the AI feature id. Learning must use the constant.

## 17. Sources

Market and standards research of 2026-11-13. Items the research could not verify (Totara's LTI Advantage support, Moodle core Open Badges 3.0, cmi5 as a published IEEE standard, the Part 11 claims of SAP and Absorb) are not relied on above.

- Moodle: <https://moodledev.io/general/releases/5.3>, <https://moodledev.io/general/releases/5.2>, <https://docs.moodle.org/en/AI_subsystem>, <https://docs.moodle.org/en/Data_privacy_plugin>, <https://moodle.org/security/index.php>
- Canvas: <https://www.instructure.com/resources/blog/what-we-shared-april-2026-new-next-showcase>, <https://community.canvaslms.com/t5/Canvas-Basics-Guide/What-is-MasteryPaths/ta-p/404483>, <https://community.instructure.com/en/kb/articles/660464-how-do-i-enable-bulk-purchase-enrollment-for-a-catalog>, <https://canvashelp.stanford.edu/hc/en-us/articles/4405462061843-Missing-or-Changed-Functionality-in-New-Quizzes>
- Open edX: <https://docs.openedx.org/en/latest/community/release_notes/verawood/feature_release_notes.html>, <https://docs.openedx.org/projects/edx-credentials/en/latest/>, <https://docs.openedx.org/projects/openedx-aspects/en/latest/technical_documentation/decisions/0002_xapi.html>, <https://discuss.openedx.org/t/deprecation-removal-ecommerce-service-depr-22/6839>
- Totara: <https://totara.help/docs/recertification>, <https://totara.help/docs/what-are-certifications>, <https://totara.help/docs/multi-year-re-certification-sequences>, <https://totara.help/docs/fixed-expiry-date-minimal-interval>, <https://totara.help/docs/certification-completion-editor>, <https://totara.help/docs/wait-listed-events>, <https://totara.help/docs/what-is-multitenancy>
- Standards: <https://standards.ieee.org/ieee/9274.1.1/7321>, <https://www.1edtech.org/lti-security-announcement-and-deprecation-schedule>, <https://standards.1edtech.org/lti/specifications/core/lti-spec>, <https://www.imsglobal.org/spec/ob/v3p0>, <https://www.w3.org/news/2025/the-verifiable-credentials-2-0-family-of-specifications-is-now-a-w3c-recommendation>, <https://www.1edtech.org/standards/qti/versions>
- Accessibility and privacy law: <https://www.deque.com/blog/en-301-549-v4-1-1-is-final-what-changed-what-it-means-and-what-you-should-do/>, <https://www.jdsupra.com/legalnews/doj-extends-title-ii-ada-web-2176958/>, <https://www.federalregister.gov/documents/2025/04/22/2025-05904/childrens-online-privacy-protection-rule>
- Security: <https://demo.adlnet.gov/assets/uploads/scorm_vuln_workaround_20090402.pdf>, <https://scorm.com/blog/scorm-security-some-perspective>, <https://mondoo.com/vulnerability-intelligence/vulnerability/CVE-2026-39415>, <https://db.gcve.eu/vuln/fkie_cve-2022-31158>

## 18. Duplication audit of 2026-11-13

Two independent reviews read the first draft against every Orchard Core module in the pinned version and every manifest in this repository. These are the changes they caused.

**Removed, because an owner exists**

| First draft | Now |
| --- | --- |
| A new **Registrations** building block (seats, holds, waitlist, check-in) | Classes on **Scheduling**, with five extension points (5.5). It would have been the third seat engine and the third waitlist. |
| A new **Audiences** module | An extension of the **Users** audience concept (5.3). |
| A private lesson file store with signed links | Orchard Core **Secure Media** on attached media (4.2, 7.2). |
| A `CsvReader` bulk enrollment page | A **Data Pipelines** destination step (7.3). |
| A shared signing-key library; secrets in settings | Orchard Core **Secrets** (5.8). |
| Private credentials for external tools and LRS clients | Orchard Core **OpenID** applications, roles and scopes (7.13). |
| A scale overload on `BalanceLedger` for seats and credits | A quantity ledger extracted from Workforce time off (5.7); `BalanceLedger` is money only. |
| A Learning catalog page with its own search and filters | Orchard **Search**, or the **Products** listing when courses are sold (7.11, decision 10). |
| A "Learning Portal" account area | Entries in the existing account menus (11). |
| Invitation links owned by Learning | Invitations in **Accounts** (5.4). |
| Enrollment, registration, extension and evidence approvals | A pending state with approve and reject; routing in **Workflows**; class confirmation in Scheduling (7.3). |
| Class e-mails and a Training calendar type | **Calendars** Invitations and Reminders; the calendar type is named Classes (7.10). |
| A learning event log from phase 1 | Deferred to the LRS in phase 8 (7.4). |
| Private stores for packages, submissions and evidence | A protected file store extracted from **Downloads** (5.6). |
| QTI import code in Learning | A **ContentTransfer** file format (7.5). |

**Corrected, because the first draft assumed something Orchard Core does not do**

- Marking a content type securable denies nothing. The access handler must fail `ViewContent` explicitly (7.5, 8).
- Content version pruning can delete the question and course versions an attempt or a completion points to. Attempts and snapshots are now self-contained (3, 7.4, 7.5).
- The Audit Trail trims after 10 days by default and its events can be switched off. The Learning records are the compliance record (9.1).
- The notification center is an admin page. Learner pages list notices through `INotificationManager` (4.2).
- Orchard Core has no video player, no re-authentication step and no invitation flow. The player is new; re-authentication is out of the plan; invitations go to Accounts.

**Renamed, because the name was taken** (section 6.1): Badges to Digital credentials, Session to Class, Activity to Course item, Assignment to Required learning, Credits to Education credits, Seat pool to Licence pool, Track to Certification path, Learning event to Learning statement.

**Confirmed as justified new work**: certificate issuing in Skills, the PDF building block, the content access handler, the Acknowledgement record, and everything in section 4.3.

## 19. Progress

### Phase 0: decisions and spikes (done 2026-11-13, branch `ma/learning-phase-0`)

Section 15 is accepted as written (see the note at its top). The spikes and the checks in section 16 that phase 1 needs:

| Check | Result |
| --- | --- |
| 1. Outline containment on `ListPart` | Works as planned. A contained item gets `ContainedPart.ListContentItemId` from the `ListPart.ContainerId` query value when it is created from the course (`ContainedPartDisplayDriver`), and the course lists its children through `ContainedPartIndex`. Accounts' placement trap is the `ListPartNavigationAdmin` shape, which Accounts and the CRM extend with an alternate; the outline editor is a separate part (`CourseOutlinePart`) that only stores section ids and ordered item ids, so it does not fight the list's own placement. The course editor keeps Orchard's list of children; the outline card orders them into sections. Confirmed in the browser in phase 1. |
| 2. Secure Media with the content access handler | Confirmed in Orchard Core code (`ViewMediaFolderAuthorizationHandler`). A file attached through a `MediaField` with the Attached editor is stored under `mediafields/{ContentType}/{ContentItemId}/`. With Secure Media on, each request for it is authorized by `ViewContent` on the **latest** version of that content item, and a refusal answers 404 (`SecureMediaMiddleware`). So Learning's content access handler gates lesson files with no further code. The tenant-wide effect: files outside `mediafields` and `_Users` need `ViewRootMediaContent` (root folder) or a folder's own `ViewMediaContent_{folder}` permission, so the Anonymous and Authenticated roles lose access to the media library until a role is given one. **Corrected in phase 1:** `ViewMediaContent` (all folders) must never go to Anonymous or Authenticated, because a role that holds it passes Secure Media before the folder check runs and so opens every lesson file (seen in the browser: an anonymous request for a lesson file answered 200 once Anonymous had it). The **Learning setup** recipe turns Secure Media on; the separate **Learning public media** recipe, run only when a site wants a public library, grants `ViewRootMediaContent` and removes `ViewMediaContent` from both roles. The docs say so. |
| 3. PdfPig for certificates | Spike in a scratch console on PdfPig 0.1.16 (the version in `Directory.Packages.props`). An embedded TrueType font (`AddTrueTypeFont`) writes and reads back "Zoë Łukasiewicz Ангелина Παπαδόπουλος" exactly. A background image works as JPEG or as an 8-bit RGB/RGBA PNG; PdfPig's PNG decoder throws on a 1-bit or palette PNG, so the Pdf block accepts JPEG and 8-bit PNG only and says so in its validation message. The QR code comes from **QRCoder** 1.7.0 (MIT): the block reads its module matrix and draws the modules as filled vector squares, so no image decoder is involved and the code stays sharp at any size. QRCoder is the one new dependency; `PdfLibraryLicenseTests` does not deny it. |
| 4. Pipeline step registration | `AddDataPipelineStepType<T>()` is in the `OrchardCore.DataPipelines.Core` library package, and `IDataPipelineStepType` and `DataPipelineStep` are in `OrchardCore.DataPipelines.Abstractions`. Learning registers the type with `AddDataPipelineStepType<T>()` and its editor with `AddDisplayDriver<DataPipelineStep, TDriver>()`, which is all `AddDataPipelineStep<,>` (in the module assembly) does, so no Orchard Core module assembly is referenced. `ContentDataSourceOptions` is in the `OrchardCore.DataPipelines` module package; Learning does not need it in phase 1 because courses and course items are content and the built-in Content items source reads their parts. |
| 5. `CertificationRecord.Properties` | `CatalogItem` derives from `ExtensibleEntity` (CrestApps.Core), so the record has `Properties`. Phase 3 still adds typed fields (`Source`, `SourceId`, `PreviousRecordId`, `RenewedByRecordId`, revocation fields, verification code) because they are indexed and queried. |
| 10. Feature id literals | Learning uses constants for every feature id, including Orchard Core ones through `OrchardCoreFeatureIds`. |

Build environment for this work: SDK 10.0.403 (the repository's `global.json` rolls forward to the installed SDK 11, whose new analyzers fail on `main`'s own code; the local copy of `global.json` is changed to `latestFeature` and marked skip-worktree, never committed), builds through a `subst L:` drive because of path length.

### Phase 1: Learning core (branch `ma/learning-phase-1`)

**Built.** The `Learning.Abstractions`, `Learning.Core` and `Learning` projects: the Course type and the Page, Video, File, Link and Acknowledgement lesson types contained in it through Orchard's List part; the outline editor (`CourseOutlinePart`, sections with ordered item ids); course item kinds (`AddCourseItemKind<TPart>`); release rules (item completed, date, days after enrollment) and completion criteria (minimum grade, minimum time, item passed, staff approval); enrollments with grants, the state machine, history with reasons, cycles and a completion snapshot; manual, open, key (PBKDF2 hash) and approval enrollment; progress (heartbeat capped at 60 s a call, viewed share for videos, mark done, acknowledgement with the confirmed statement, completion by hand with a reason); the content access handler (`ViewContent` for course items, so the display route, content API, GraphQL, data sources and Secure Media follow it), the record handler and the course staff content handler; learner pages (My learning, transcript, player, course page outline) with the `learning-player` script; the enrollment admin list (`<admin-list>`, both layouts) and detail page (sticky action bar); permissions and the Learning roles recipe; the Learning setup, course catalog (Lucene index `courses`, `/search/courses`) and public media recipes; 8 workflow events and 6 tasks with schemas; the `LearningEnrollments` recipe and deployment steps with schemas; the `Learning` data source (4 data sets) and the **Enroll learners** pipeline step; the Course completion, Learner transcript and Inactive learners reports; notification types (enrolled, due soon, overdue); Audit Trail events; webhook events (`learning.enrollment.created`, `learning.course.completed`, stable ids); the personal-data contributor; the daily upkeep (expiry, access ending, due soon, overdue, inactive, each raised once) bound to `CrestApps:Learning:Upkeep`; the Staff integration (Instructor capability, Learning section on the staff profile, My learning on the staff home page); a Learning section on the user editor; My learning in the storefront account menu or the user menu; README, technical and user manual pages, configuration and feature reference, changelog, building-blocks entry and the `crestapps-learning` skill.

**Differences from the plan, and why.**

- Secure Media (check 2) was corrected: `ViewMediaContent` must never be given to Anonymous or Authenticated, because a role that holds it passes Secure Media before the folder check and opens every lesson file. The public media recipe grants `ViewRootMediaContent` and removes `ViewMediaContent`. Seen in the browser: with `ViewMediaContent` on Anonymous, an anonymous request for a lesson file answered 200.
- The course page never renders Orchard's List part (an empty `Course-ListPart.Detail` alternate besides the placement rule): the List part does not authorize each child, and on a site whose theme does not apply the module placement it showed lesson bodies to anonymous visitors (found by the integration test on a Blank site).
- The catalog is an Orchard Core search page (`/search/courses`), as the plan says; it is created by a recipe rather than code, because index profiles are site data.
- The user editor section is a `DisplayDriver<User>` editor shape (read only), because the user editor has no page sections.

**Fixed while verifying.** The course path pattern slugified the slash (`/courses-title`); now `courses/{slug}`, with `UpdateFrom1Async` for sites created earlier (existing items keep their path until saved). The Learning section drivers dropped their async fill (CS4014; seen as empty sections in the browser). A completed enrollment opened course items that are not in the outline (an active one did not); fixed test first. Earlier in the phase: saving a detached enrollment copy made a second document; the enrolled learner on a free preview was not tracked; the course page listed lessons.

**Suites run.**

- `CrestApps.OrchardCore.Tests`, namespace `Learning`: 95 of 95 passed (with the telemetry test added after this section was first written). Honest note: several services were written with their tests in the same slice, so the first failing run was a compile failure rather than an observed red assertion; the later fixes (the detached enrollment copy, the free preview, the completed enrollment opening items outside the outline, the course page leak) each had a test that failed first.
- `CrestApps.OrchardCore.Learning.IntegrationTests`: 7 of 7 passed (lesson file with and without enrollment, display route, player, content API and GraphQL, IDOR on progress and the admin enrollment, course page, recipe round trip).
- Guards, namespaces `Architecture` and `PublicApi` (includes the migration guard, module layering, feature id literals, PDF licence): 468 tests, 2 failed on the first run and were fixed (two views showed a literal `{0}` from `T[...].Value`; the public static enrollment transition table tripped the static-state guard); both re-run green. The public API baselines govern only the Accounts, Contact Center, Telephony and Omnichannel families, so Learning needs none. The feature activation suite is Contact Center specific; Learning features are enabled in a real host by the Learning integration tests.
- JavaScript: `npx vitest run`: 2 failures on the first run (the two enrollment key fields lacked the password-manager ignore attributes), fixed; the lint suites re-run green (21 of 21). ESLint is clean on the Learning assets.

**Browser verification** (test site on `http://localhost:5137`, accounts `admin`, `learner1`, `learner2`, `instructor1`, `manager1`, `author1`, `grader1`, all `Password1!`; pages driven in the browser pane and, while the pane was hidden, over HTTP with a signed-in client):

- Admin: course, sections and lessons created through the editor; enrollment list in List and Grid layouts with Buttons and Menu actions, breadcrumb, sticky action bar on the detail page; the three reports with data; the user editor's Learning section; the Learning setup, catalog and public media recipes run from **Recipes**.
- Learner (learner1, phone and desktop width): enroll, work through the course, mark done, acknowledge, complete, transcript.
- Not enrolled (learner2 before enrolling, anonymous): lesson display route refused, player shows the enroll message only, progress endpoints 404, admin enrollment refused, lesson file 404; the catalog at `/search/courses` lists the course for anonymous visitors.
- Instructor (instructor1, Instructor role, named on the course staff): edits the course (200) and opens its lesson file; refused on another course.
- Key and approval: learner2 refused with a wrong key, enrolled with the right one; asks to enroll in an approval course, sees the waiting message, a Training Manager (manager1) approves it.

**Not done or not verified.**

- In the browser: the video lesson player, the editors of the Learning workflow activities (the 14 activities show in the designer palette with their descriptions; the designer adds an activity by dragging, which was not done), the deployment step editor and the pipeline step in the designer, the staff profile section (no staff member on the test site), and the user menu link (the test site's theme does not render the user menu).
- The docs site was not built.

**Review rounds 1 and 2 (2026-11-14).** Every finding was fixed test first on this branch, or its reason is written down; the review files tick each one with its commit.

| Finding | Fix | Commit |
| --- | --- | --- |
| R1-1 Lessons outside a course | Fail closed (`NotInCourse`); free preview only from the published outline; editors of a lesson may view it. | `89d37262d` |
| R1-2 Staff from a draft | Staff read from the published course; staff, mode and key need Manage courses or an instructor with Manage own courses (`CourseSettingsPolicy`); unknown staff rejected; staff access to records needs Manage own courses. | `855d2a857` |
| R1-3 Double completion, lost writes | `EnrollmentWriteGate`: one lock per enrollment, fresh read, commit inside the lock, observers after the commit. | `0dd01c3db` |
| R1-4 Unbounded time and watch claims | Time bounded by the wall clock since the last access; a video share capped by the time since it opened, or marked self-reported. | `d30809f57` |
| R1-5 Key guessing | Five wrong keys per person and course in 15 minutes lock that person out for 15 minutes. | `9303a8b66` |
| R1-6 Key hash in the content item | Hash moved to a `CourseEnrollmentKey` record; migration step 3 moves old hashes; recipe and deployment carry hashes as `CourseKeys`. | `9303a8b66` |
| R1-7 Read paths that do not ask | Two real leaks closed: the List part RSS feed of a course (now off) and lesson bodies in `Summary` (search results). Tests for the feed, search results of an index holding lessons and the queries API; sitemaps and Liquid documented. The integration site now runs a theme, because Orchard applies no placement without one. | `889ef27aa` |
| R1-8 Recipe imported twice | Values copied onto the tracked enrollment and progress. | `f8c7326f3` |
| R1-9 Staff cannot enroll | Create authorizes against the course (`LearningCourseResource`); the learner must exist; scoped course options. | `3e30c1560` |
| R1-10 English text from services | Refusal codes (`LearningRefusals`) localized in the module (`LearningRefusalText`); no service text through `LocalizedHtmlString`; recipe errors localized. | `18c5a5ea8` |
| R1-11, R2-A Observers before the commit; enrolling outside the lock | Enrolling commits inside its lock; grants and upkeep notices go through the gate; every observer runs after the commit. | `900a146a6` |
| R2-B Settings guard only in the editor | `CourseSettingsGuardHandler` restores staff and mode on create, update and import by a signed-in person who fails the policy. | `85e81858f` |
| R2-C The gate commits the session | Documented in the gate and the technical page, caller by caller. | `e47e9a641` |
| R2-D Lost events | Phase 3 must issue idempotently and catch up in the upkeep, with two tests (plan section 12). Open until Phase 3. | `e47e9a641` |
| R2-E Busy as a literal | Busy is a code; the player retries the opening call and the watched share. | `18c5a5ea8` |

Suites after the rounds: `Learning` unit tests 124 of 124; integration tests 13 of 13; the whole `CrestApps.OrchardCore.Tests` assembly 15,827 of 15,828 with one skipped, where the one failure was a Learning bug from phase 1 (the workflow event editors referenced a partial Razor could not resolve from `Views/Items`; moved to `Views/Shared`, the guard re-run green); JavaScript lint and asset suites 27 of 27. Honest notes: findings 5 and 6 first failed as a compile error (the store and the error code did not exist), not as an observed assertion; the instructor enrollment integration test (R1-9) was written with the fix, while its handler test failed first; the player's busy retry (R2-E) has no automated test. Not re-checked in the browser after the rounds: the course editor with the moved key, the player retry and the instructor's enrollment form.

**Browser pass 2026-11-18.** Fixed test first on this branch, or judged and written down; the red and green counts are in each commit.

| Bug | Fix | Commit |
| --- | --- | --- |
| GET/POST route collisions (with Assessments' override and grade scale saves) | A guard test fails for any routed Learning action without an HTTP method attribute; the enrollment pages say `HttpGet`. Each later phase fixes its own controllers. | `8eca15851` |
| Course staff see only Courses in the Learning menu; instructors miss **Enroll a learner** | The Enrollments item asks for the permission the person holds (View enrollments, else Manage own courses); the button shows to anyone the enroll form accepts. | `4366a0593` |
| "A key is set" after a failed course create | The editor says a key is set only for a course saved before. | `d82f83fb2` |
| Learner erasure works only through a linked customer | Why: only the Customers privacy action runs the personal data service, a customer added by hand has no user id, and Learning finds records by user id. The contributor now adds the user id of the account with the request's e-mail address. | `bd44512cb` |
| Learners get 403 on the notification center | Decision, documented: learners read notices through the Notifications delivery methods (e-mail); the center is an admin page and learners do not get the admin. | `8c75c52cd` |
| Breadcrumb under the navbar on My learning and My transcript | Not changed: The Blog Theme floats its navbar over the page (`#mainNav` is absolutely positioned) and expects every page to fill its `Header` zone with a masthead, as its own templates do. Learner pages, like the other CrestApps storefront pages, render in the theme's content zone; on the Storefront theme they sit below the navbar. | none |

### Phase 2: Assessments (branch `ma/learning-phase-2`, stacked on `ma/learning-phase-1`)

**Built.** The `Learning.Assessments.Abstractions`, `Learning.Assessments.Core` and `Learning.Assessments` projects: question banks (Orchard's List part) and nine question types (single and multiple choice, true/false, short text, numeric, matching, ordering, fill in the blanks, essay) with one `IQuestionScorer` each, registered with `AddQuestionType`; the Quiz course item with fixed questions and random draws by bank and tag, time limit, window, attempts and delay, pass mark, grading method, navigation, review policy, access code and honor statement; `IAssessmentAttemptService`: attempts that copy each question (sanitized) so they never depend on a question version, server-kept deadlines with a leased work queue and a catch-up, answers one question at a time, scoring, grading of essays, regrade, void, overrides of time, attempts and window, all under one lock per attempt with observers after the commit; the exam player (`quiz-player` script), the grading pages and the quiz overrides page; the Gradebook feature (grade scales, course gradebook); the quiz access code in the shared Learning secret store with the wrong-try lockout; workflows (5 events, Void and Regrade tasks, schemas); the `Learning assessments` data source (4 data sets, no keys); the **Quiz results and item analysis** report; the *Quiz graded* notification; Audit Trail events; webhooks; the personal-data contributor; telemetry on the Learning meter; ContentTransfer question import and export; the `LearningGradeScales` and `LearningAttempts` recipe and deployment steps with schemas; README, technical page, Quizzes and Grading user pages, feature reference, changelog, building blocks and the skill.

**Differences from the plan, and why.**

- A void or a lower regrade after completion flags the enrollment for review (Learning, `Enrollment.Review`, list filter, notification, clear with a note) instead of changing the completion, as review round 3 asked; Phase 3 revokes from there.
- Enrollment keys and quiz access codes share one store (`LearningSecretStore`, kinds) and one limiter, instead of a key store per module.
- `LearnerUserLookup` and `LearningNotificationMessage` moved from the Learning module to `Learning.Core`, because Assessments needs them and module code may not use another module's types.
- ContentTransfer import covers the shared question fields and the choice, true/false, short text, numeric and essay parts; matching, ordering and fill-in-the-blanks questions keep their structure in the editor and the recipe steps (QTI import stays in phase 8).
- A sequential quiz shows and accepts only the next unanswered question (review round 3, finding 6).

**Review round 3 (Phase 2 so far).** All six findings fixed test first and ticked with their commits in review-round-3.md: 1 shared secret store and lockout (`3a4949b99`); 2 sanitizing at draw, answers encoded on the grading page (`762adcc30`); 3 grading needs Manage own courses for staff, never one's own attempt, Grader role loses the site-wide permission (`cee1a1008`); 4 review flag after completion (`d101bc96f`); 5 read paths for questions and the data source without keys (`762adcc30`, `4be56f3fc`); 6 sequential navigation (`150c6e407`). Earlier on this branch after the rebase: attempt writes under the per-attempt lock (`0dacd1927`) and the deadline task fix (`293503ca4`).

**Fixed while verifying.** The deadline task failed every minute on SQLite with "database is locked": the work queue claims and settles through sessions of its own while the task's session held the submit's uncommitted write; the attempt gate now commits inside the lock, and after a restart the stuck attempt was submitted at its deadline. The assessments test harness did not list the queue's index provider (a site gets it from DI), so queued deadlines were invisible in tests.

**Suites run.**

- `CrestApps.OrchardCore.Tests`, namespaces `Learning` and `Learning.Assessments`: 195 of 195.
- `CrestApps.OrchardCore.Learning.IntegrationTests`: 16 of 16, now with Assessments on the test site: grading page encodes answers; question keys stay out of the display route, the content API, a bank's feed and search results; no attempt page shows the key before submitting; an attempt reads after its question is rewritten and removed.
- The whole `CrestApps.OrchardCore.Tests` assembly (guards included): 15,900 of 15,901, one skipped, none failed.
- JavaScript lint and asset suites: 27 of 27.
- Honest notes: several Phase 2 integrations (recipe and deployment steps, workflows, audit, notifications, webhooks, personal data, telemetry, item analysis, ContentTransfer) were written with their tests in the same slice, so their first run passed; the review round 3 fixes each failed first (findings 1 and 4 only as compile failures).

**Browser and HTTP verification** (test site on `http://localhost:5137`, rebuilt from this branch; migration steps 3 and 4 applied on the existing data after resetting my dev site's interim step 3):

- A new Key course: the editor says a key is set, the content API JSON carries no hash; learner1 got "The enrollment key is not right" five times, then "Too many wrong keys" even with the right key.
- The quiz's access code: the quiz content API carries no hash; the player asks for the code; learner2, after five wrong codes, sees "Too many wrong codes".
- Grading scope: instructor1 opens the grading list of their course; grader1 is refused until they are named a Grader on the course and their role holds Manage own courses (the roles recipe re-run on the site); learner2 is refused.
- Instructor enrollment form offers only the instructor's own course.
- Review flag: learner2's Workplace enrollment marked completed, their graded attempt voided; the enrollment shows **Needs review** (score 80 → 0, the remaining attempt), appears under the Needs review filter, and is cleared with a note that stays on the card (screenshot in the browser pane).
- The stuck attempt from before was auto-submitted at its deadline after the restart.

**Not done or not verified.**

- In the browser: the exam player with a sequential quiz, the gradebook pages, the workflow activity editors and the ContentTransfer import screen were not walked; they are covered by unit and integration tests only.
- The docs site was not built.

**Browser pass 2026-11-18.** Fixed test first on this branch; the red and green counts are in each commit.

| Bug | Fix | Commit |
| --- | --- | --- |
| Quiz overrides never save; a new grade scale throws `ArgumentNullException('id')` | `GradingController.Overrides` and `GradeScalesController.Edit` (and every other routed page action of Assessments) say `HttpGet`, so the page's admin route no longer takes the form's post. The integration site turns on Gradebook. | `e36ad0946` |
| Grading and Gradebook missing from course staff's menu | Each item asks for the permission the person holds, else Manage own courses. | `4a9a291f6` |
| The quiz player shows "Due 12:00 PM" with no date | The deadline is shown with its date and time. | `d44bdfd3d` |
| Moving a quiz's close date does not move the review of attempts already taken | `QuizCloseDateHandler` and `IAssessmentAttemptService.MoveQuizCloseAsync` move the close date attempts kept, after the commit and under each attempt's lock; an override's own date stays. | `e8334edc4` |

### Phase 3: Certification (branch `ma/learning-phase-3`, stacked on `ma/learning-phase-2`)

**Built.**

- **PDF building block** (5.2): `Pdf.Abstractions` and `Pdf.Core`, `IPdfDocumentRenderer` over `PdfDocumentLayout` (pages, background image, positioned or wrapped and aligned text with a line limit, embedded TrueType fonts found on the host, JPEG and 8-bit PNG images, lines, rectangles, vector QR codes drawn with QRCoder 1.7.0, MIT). Reports PDF draws with the moved primitives (`PdfCanvas`, `PdfFont`, `PdfFontSet`, `PdfFontResolver`, `PdfText`, `PdfColor`).
- **Skills issues certificates** (5.1): `ICertificateIssuer` (issue, renew as a new linked record, revoke, find by code), numbers from `INumberSequence`, a 16-character verification code (80 bits), idempotent per certification, source and source id under a lock with the commit inside it; revocation fields; `ICertificationRecordObserver` after the commit, with workflow events and webhooks; the daily check sends no reminder for a renewed certificate; the certification editor through `IDisplayManager<Certification>`; the `Skills` data source (no codes) and the personal-data contributor.
- **Skills: Issued Certificates** feature: the Certificate Template content type (paper, font family, colors, border, background image, QR code, Liquid text blocks), the certificate PDF, the public verification page behind a rate limit and `noindex`, an admin template preview; an issued certificate is revoked, never edited or deleted.
- **Learning Certifications** module: certification paths on the certification editor (all or any requirements, course completed and assessment passed with minimum grades, expiry from the completion date, the previous expiry or a fixed day with a shortest first period, renewal window, grace period, start on first requirement, enroll on renewal); progress with every cycle and its evidence; issue and renewal through the Skills issuer under one lock per path and person; the renewal leased queue; lapse and revocation from Skills; the hourly upkeep that opens missed renewals and issues what a lost event missed; the people on a path; My certifications; revoking from an enrollment review (`IEnrollmentReviewActionProvider` in Learning.Abstractions); notifications (issued, expiring, renewal open, lapsed); workflows (Renewal Opened, Lapsed, Start Certification Path); the Certification compliance report; the `LearningCertifications` data source; the `LearningCertificationPaths` recipe and deployment steps; the personal-data contributor; telemetry; docs.

**Differences from the plan, and why.**

- The certificate template is chosen on the certification (Skills), not on the path: Skills prints the certificate, also for certificates issued by other modules.
- The template font is an installed font family rather than a media file: the media library does not accept `.ttf` files by default.
- `SubjectName` is still copied when a certificate is issued, not resolved at read time, and certificate copies stay where they are: the protected file store (5.6) is Phase 4.
- Requirement kinds in this phase are course completed and assessment passed; learning path, credits, evidence and sign-off come with Phases 4 and 7.
- Notify the manager on window open waits for teams in Phase 4.
- The certificate goes to the learner's staff record when Staff is on and the user has one, so Workforce and Dispatch (which read staff subjects) see it.
- No Audit Trail events for certificates yet: the Skills record keeps who issued, renewed and revoked it and why, and the workflow and webhook events carry each change.

**Not checked in the browser.** Revoking from an enrollment review (the lookup is unit-tested), the renewal opening on its day and the reminders and lapse on a live site (unit-tested with a moved clock), the workflow editors, the compliance report page, and a certificate template with a background image. The live site has no passed quiz that was not voided, so the live path used "any one" requirement with the course; the course-and-exam path runs in the unit tests.

**Review round 2, D.** Issue is idempotent (source id = progress and cycle; the Skills issuer holds a lock per certification and source and commits inside it) and the upkeep checks again everyone who met a requirement since the last look. `CertificationPathTests.CompletionDeliveredTwice_IssuesOneCertificate` and `CompletionEventLost_TheUpkeepIssuesTheCertificate`.

**Fixed while verifying.**

- The certification path tests caught the YesSql commit trap: the issuer commits, which made the session forget the progress it had read, and saving it again inserted a second copy. The progress is now saved before the issuer runs and read again after it, and after each renewal enrollment.
- A recipe run inside a migration (the certificate template type) never finished on SQLite: the recipe's own scope waits for the write lock the migration holds. The type is defined in code.
- A factory registration made a dependency cycle that recursed until the request hung (certification manager, its handlers, the path service): the catalog handler and the observers resolve the path service when they need it.
- Orchard gives every display driver of a model the model's name as its prefix, so the path card's "Enabled" posted over the certification's: the two added cards build their own prefix.
- The verification page's status badge was invisible on a front-end theme with Bootstrap 5.1 (no `text-bg-*`): the front-end views use classes that exist there.
- An issued certificate's row still linked to the hand-record editor: the link and the editor now refuse it, as the save already did.

**Browser pass 2026-11-18.** Fixed test first on this branch; the red and green counts are in each commit.

| Bug | Fix | Commit |
| --- | --- | --- |
| `CertificationUpkeepBackgroundTask` fails on SQLite (Double to Int64 on `Percent`) | `ListPassedUserIdsSinceAsync` selects only the row id and user id; a typed read of whole index rows breaks when SQLite returns 100 as an integer and 90.91 as a real. Found, not changed: the Reports building block's `ReportIndexPages` maps whole index rows, so the attempts, attempt answers and submissions data sets have the same trap. | `ee8a61366` |
| The certificate background image never prints | The media field is read from the item's content as a `JsonObject`; the dynamic indexer never gave a `JsonArray`. | `ef80e9e4f` |
| Workflow hints show `{ x }` instead of `{{ x }}` | Liquid examples are arguments of the localized hint; a view lint guards every Learning view. | `ab8d4ae85` |
| GET/POST route guard | `CertificationPathsController.People` says `HttpGet`. | `9c2bd7bd3` |

**Fixed after review round 4.** A certificate is revoked from a review only when it rests on that enrollment and the review is open; evidence skips failed and reviewed enrollments and attempts made in none or in such an enrollment; the upkeep pages everyone in a stable order, leaves disabled paths out and reads every completion since the last look; a second revocation changes nothing; progress is revoked under its lock; the certification path actions check the antiforgery token. The last valid day is the same for path and hand-recorded certificates.

### Phase 4: Programs and required learning (branch `ma/learning-phase-4`, stacked on `ma/learning-phase-3`)

**Built.**

- **Quantity ledger** (5.7): `IQuantityLedger<TLedger>` in `CrestApps.OrchardCore.Abstractions` with its YesSql implementation in `YesSql.Core` (one collection per ledger, idempotent posts, balance on a day without entries not started or expired, sums over a period, accounts with entries since a time, erasing an account). Workforce time off moved onto it, with an additive upgrade that copies the entries and keeps the old ones for a rolling upgrade.
- **Protected files** (5.6): `FileStorage.Abstractions` and `FileStorage.Core` with `IProtectedFileStore<TArea>`, signed time-limited links (`IProtectedFileLinks<TArea>`) and access policies that must all allow (`IProtectedFileAccessPolicy<TArea>`). Downloads moved onto it with the same folder; Learning keeps exemption and external learning evidence in it.
- **User audiences** (5.3): `IUserAudienceService` (list, member paging, `IsMemberAsync`, audiences administrators define with rules joined by all or any) and `IUserAudienceRuleSource`, with rule sources registered by their owners: role (Users), team with sub-teams (Teams), hire date and employment type (Staff; `StaffQuery` gained `EmploymentTypes`), skill and certification holders (Skills) and course learners (Learning). The User Audiences feature has the admin list, the rule editor, the members page, recipe and deployment steps and an "In a user audience" rule for layers.
- **Learning Programs** module: learning paths (content type in code, course sets joined by then, and, or with all, any or at least N, stages that enroll as they open, self start, the people on a path), required learning (course, path or certification path targets, audience and named people, due dates fixed or counted from when it is required or from the hire date, reminders, escalation to the staff manager, exemptions with protected evidence, run and refresh on completion, the hourly upkeep that runs each rule daily, preview and Run now), My required learning, Team Learning through `IStaffHierarchy`, notifications, five workflow events and two tasks, the `LearningPrograms` data source, the `LearningRequirements` recipe and deployment steps, personal data.
- **Education credits** (7.9) in Learning Certifications: credit types, the Education Credits part (courses, and paths when Programs is on) posting to the credit ledger once per source, external learning with evidence reviewed by a person with the permission, the `CreditsEarned` requirement within the cycle, My education credits, the Record external learning pipeline step, the `LearningCreditTypes` steps, workflows, data sets and personal data.

**Differences from the plan, and why.**

- Audiences are read through a new `IUserAudienceService` beside `IUserAudienceProvider` rather than by changing the provider contract: pickers keep their role-based providers, and the service pages and checks both kinds.
- The spike chose a rule registry over a saved filter on Orchard Core's Users data set: the data set cannot join the Teams, Staff, Skills and Learning stores, and membership must be checked for one person without querying everyone. Recorded in the reusability audit.
- Hire dates are compared in memory on the active staff linked to a user: the staff index has no hire date column. Employment types are filtered in the index.
- No account-membership rule yet: `IAccountMemberService` lists no members; it comes with the Accounts changes of Phase 5. No Outreach audience adapter: no Learning feature sends bulk messages yet.
- Membership changes are picked up by the daily run of each rule (and Run now), not by observers of every owner: the run reconciles joins and leaves in one place.
- Workforce keeps its private targeting (`LaborRuleSet`, `TimeOffPolicyAssignment`) as an accepted gap in the audit: its rules carry dates and precedence an audience does not.
- The `CreditsEarned` requirement reuses the requirement's minimum for the number of credits, labelled "Minimum grade or credits" in the editor, rather than adding a field.

**Checked in the browser.** User audiences (rules, members), a learning path saved in the editor and its learner page, required learning saved and run (enrollments with due dates, preview, exemption with evidence download, learner and exempt views, admin pages refused to learners), credit types, credits on a course editor and its page, recording external learning with evidence, review and accept, My education credits, credit types in the certification path editor.

Team Learning was checked with a manager whose role holds View team learning and a report linked on the report's staff record: the manager sees the report's required path, state and due date.

The ten new workflow activities are listed in the workflow designer's activity library.

**Not checked in the browser.** The editors of the new workflow activities (their views compile at build), the deployment steps, reminders and escalation on a live clock (unit-tested with a moved clock), the Record external learning step in a live pipeline (the row logic is unit-tested), and the time-off ledger copy on a live site with time-off data.

**Fixed while verifying.**

- The full guard run caught the ledger migration: dropping the old index table and querying documents from the Migrations folder broke the additive-migration guards. The move became an additive copy in Workforce.Core that skips entries already copied.
- Required learning due dates carried ticks that enrollments drop: due dates are now the last whole second of the day.
- Admin lists of new row types need a `<Type>.SummaryAdmin` template; feature audiences showed an empty actions menu.
- Feature id literals in the Programs module were replaced by constants (the feature-id ratchet).
- The education credits editor on a course refused to save: its empty spare rows posted an empty amount into a number that cannot be empty. The amount of a row may now be empty, and empty rows are skipped.
- Accepting external learning marked it accepted before posting its credits, so a request that stopped in between (the browser left the page) kept an accepted submission without credits. The credits are now posted first, once per submission, and the submission is marked after.

**Fixed after review round 5.** The time-off copy runs at each start until it records that it read to the end, skips by id and key, commits in batches, and accrual waits for it. Nobody reviews their own external learning, and accept and reject are locked; awards are keyed by person; pipeline rows are recorded once. Required learning retries refused or interrupted starts and records their result, tolerates duplicate assignments, counts a prior completion, announces overdue once, refreshes on a certificate and saves reminder marks before sending them under a lock. Paths respect course enrollment controls. Audiences have one meaning for "not limited", resolve once per scope, the Team rule uses today's memberships and the Skills rules check one person. Credits can be reversed from a review, the credits evidence records what it rests on and counts only credits still valid. A missing audience fails the run and a used audience cannot be deleted; evidence files are cleaned up and opened only through protected links; the recipe steps read enum names.

**Still open from review round 5.** A learner-record recipe step for credits (10.1), exemptions in the Audit Trail (9.1), and tests of the evidence policies, audience permissions and Team Learning scope.

**Fixed after review round 6.** The time-off copy runs under a distributed lock, so nodes starting together never both copy; an integration test restarts a tenant and finds the old entry copied and the copy recorded complete.

**Left for the release that drops the old time-off table (review round 6, item 3).** The copy still reads every ledger index row of the time-off collection at each start; once the old table is dropped the copy goes away. Until then a large ledger pays one index read per start.

**Browser pass 2026-11-18.** Fixed test first on this branch; the red and green counts are in each commit.

| Bug | Fix | Commit |
| --- | --- | --- |
| Workflow hints show `{ x }` instead of `{{ x }}` (Award Education Credits, Grant Required Learning Exemption, Require Learning) | The Liquid example is an argument of the localized hint. | `c5b40693e` |
| GET/POST route guard | The credit type, external learning, My education credits, learning path people, required learning, My required learning and Team learning pages say `HttpGet`. | `0b81e9d51` |

### Phase 5: Commerce and organizations (branch `ma/learning-phase-5`, stacked on `ma/learning-phase-4`)

**Built.**

- **Account invitations** (5.4) in Accounts, feature Account Members: `IAccountInvitationService` (invite, find by link token, accept, take back), invitations kept with the SHA-256 hash of the token only, accepting refused for another e-mail address, an expired or used invitation, or a person already in another account. The storefront **Members** page lets an account admin invite with a role and take an invitation back; the invitation page signs the person in and accepts. An e-mail with an absolute link and the expiry in the site's time zone, workflow events (sent, accepted) and the Invite to Account task, personal data. `Source` and `SourceId` say what an invitation is for; Learning's seats use them.
- **Learning Commerce** module (`Learning.Commerce`, with `.Abstractions` and `.Core`): the migration makes Course and Learning Path products (`ProductPart` with the product page details on, and `LearningProductPart`); `LearningProductPart` grants an enrollment, a learning path or seats for an account, with an access length; the cart refuses learning to guests and seats to anyone but an account admin; `LearningOrderEventHandler` on `OrderLineGrantHandlerBase<LearningProductPart>` grants each line once and takes it back on a full refund or a cancellation, with a `LearningPurchase` record per line.
- **Licence pools**: a pool per order line for an account, free seats on the quantity ledger (5.7) with one keyed entry per change, seats given to members, held for invited people and assigned when they accept, taken back, and all revoked with their enrollments and pending invitations on a refund. The storefront **Course seats** page (`/account/learning-seats`) shows an account admin the account's pools, free seats, holders with their progress and invited people. Course seat given notification, three workflow events and the Give Course Seat task, the Learning commerce data source (Licence pools, Seat holders), personal data.
- **Schema.org `Course`** in the Learning module with Advanced SEO on, after the product provider so a sold course keeps its offer.

**Differences from the plan, and why.**

- Seats go to the buyer's account (the order's account, or the account the buyer signs in for); the cart does not ask who the seats are for. A person belongs to one account, so there is nothing to choose.
- The seats page is `account/learning-seats` in the storefront account menu, not under `account/business`: the account menu is where Accounts B2B and Members put their pages.
- The catalog is the Products listing because Learning Commerce depends on Product Listing; no `IProductSearchDocumentContributor` adds course fields to the index yet.
- Not built in this phase: the subscription entitlement (Subscriptions has no extension point for a custom entitlement kind), selling certification paths and extra assessment attempts, taking back a purchased learning path (no revoke on path progress), free courses as `ListingOnly` products (a free course keeps self-enrollment), an account-membership audience rule, and a recipe step for licence pools (they are learner records made by orders).

**Checked in the browser.** Learning Commerce enabled with Checkout and Test payments: the course editor's Product and Learning product sections, the course page with its price and Add to cart; a guest refused ("Sign in to buy"); a new user bought the course with the test provider and found it in My learning; a seats product of 50 seats per unit refused to a plain member of an account and bought by its admin; the Course seats page with 50 of 50 seats left; a seat given to a member (49 left, 0% done); two people invited (the seat held, 47 left); the invitation e-mail (SMTP pickup folder) opened, the invited person signed in, accepted, joined the account and found the course in My learning; a partial refund of the seats order kept every seat; the full refund showed the pool as Refunded and took the course from the member and the invited person; a full refund of the personal order took the course from the buyer. A member was refused the seats page.

**Checked again after review round 6, on the rebuilt site.** A new seats order named its pool after the course ("Forklift Safety"); the seats page has its browser title and breadcrumb, stacked forms with labels and a full-width member picker; the invitation e-mail carried an absolute link; the invitation page sent `Referrer-Policy: no-referrer`; accepting with an unconfirmed address was refused with the message to confirm it, and accepted after the administrator confirmed it (member added, course in My learning, seat Assigned); an invitation taken back from the members page gave its held seat back (48 to 49 of 50).

**Review round 7 (commerce module).** The order-paid grant committed inside the checkout's single unit of work. Now `LearningOrderEventHandler` only records each paid line as a `Pending` `LearningPurchase` (with a copy of what it buys) and queues a `LearningFulfillmentWork` item on a leased work queue, both in the checkout's unit of work; `LearningPurchaseFulfillment` runs it right after the request (deferred task) and every two minutes (background task), each purchase in a scope of its own under a lock per purchase with fresh reads. A paid course ignores prerequisites; a busy enrollment or a thrown failure is retried with a back-off; anything else marks the purchase `Failed` with the reason, logs it and adds an order note. A refund queues its request the same way, and the purchase becomes `Revoked` only when no open or expired enrollment still carries its grant. The cart refuses an unpublished course or unmet prerequisites (`CoursePrerequisites.MeetsAsync`, shared with enrollments) and caps a single-person course at one; Carts gained `CartLineEvaluateContext.MaxQuantityReason`, so the cart says why ("Forklift Safety is for you alone, so one is enough.") instead of "Only 1 left". Erasure takes seats back through `RemoveAsync` and clears the person from seats and purchases; purchases are exported. The invitation page offers the confirmation e-mail to a signed-in person whose address is not confirmed. Differences: failures are shown to staff as order notes, not on a purchase records page (none exists); a refunded learning path is still not taken back (no revoke on path progress). Purchases made before this change have no state and read as `Pending`; nothing re-grants them because they have no queued work.

**Checked in the browser after round 7.** The commerce migration's step 2 created the queue tables on the existing site. Adding 3 of a course was refused with the new reason; a purchase with Test payments enrolled the buyer right after the request (purchase `Fulfilled`, queue empty); a full refund made the purchase `Revoked`, removed its grant and left the enrollment of a person who also held a seat active on the seat, and took the course away from a buyer with no other grant. An unconfirmed person on an invitation page saw the notice, sent the confirmation e-mail (in the pickup folder) and came back to the invitation; a confirmed person saw no notice. Not checked live: a failed grant's order note and a busy retry (unit-tested).

**Not checked in the browser.** Cancellation (same path as a refund in `OrderLineGrantHandlerBase`, unit-tested in Orders), the Give Course Seat task and the seat workflow events on a live workflow, the seat notification in the notification center, the data sets in a report, a learning path sold as a product, and the schema.org output on a live page (unit-tested).

**Fixed while verifying.**

- Course pages showed no price or Add to cart: the product page details are a type-part setting (`ProductPageSettings.ShowDetails`) of the Product Listing feature. The migration turns it on for courses and paths, and Learning Commerce depends on Product Listing.
- The invitation e-mail carried a relative link when the site has no base URL: it now falls back to the address of the request that sent it.
- A pool was named after the product sold (such as a team pack) rather than the course; it now takes the course's title.
- The seats page's member picker shrank to one letter in a narrow front-end theme: the forms are stacked with labels. The storefront pages declare breadcrumbs, so their browser titles are set.

**Browser pass 2026-11-18.** Fixed test first on this branch; the red and green counts are in each commit.

| Bug | Fix | Commit |
| --- | --- | --- |
| Security: a key course was bought, and enrolled the buyer, without the key | A course in the Key or Approval mode is not sold, as a course or as seats, unless the product's `EnrollsWithoutKeyOrApproval` says buying it is the way in; the cart refuses it and the fulfilment fails the purchase if the mode changed meanwhile. Closed courses stay sellable (a purchase is one of the sources that mode allows). | `589196d19` |
| Every course requires a price; a priced learning path has no price or Add to cart | Products' `ProductPartSettings.PriceOptional`: an item without a price is not for sale. Courses and paths get it and the product page details through migration step 3, the create step and `LearningPathProductMigrations` (paths made in the same pass or later). | `4d3be56a9` |
| Data sets show raw ids | The pool's course, the accounts and a seat's pool reference what they identify, so reports join them for names. | `3b3aecb49` |
| No `provider` in the course JSON-LD | `CourseProviderStructuredDataProvider` runs after the site provider and refers to the publisher node; Seo.Abstractions gains `StructuredDataProviderOrder.Site` and `StructuredDataNodeIds`. | `e7e6118d9` |
| Workflow hint shows `{ x }` (Give Course Seat) | The Liquid example is an argument of the localized hint. | `bd7fad9bd` |

### Phase 6: Classes (branch `ma/learning-phase-6`, stacked on `ma/learning-phase-5`)

**Built.**

- **Scheduling dated sessions** (5.5): `SchedulingSession` with one or more meetings, its own capacity, location, meeting link, registration window and waitlist mode; one registration (an `Appointment` with `SessionId`, `Source` and `SourceId`) covers every meeting; capacity is checked under a lock per session; the waitlist has user ids and, in the automatic mode, offers a freed place that expires after the offer hours and passes to the next person (the existing holds task runs the expiry); attendance is attended, partial with minutes, or absent, marked for a roster once. Registration, offers, cancellation and attendance raise `IAppointmentObserver` changes. The session is one calendar event with its attendees when its first resource has a calendar.
- **Learning Classes** (`Learning.Classes`, with `.Abstractions` and `.Core`): the **Class** course item kind (content type `LearningClass`, completion mode Attended or Marked done); `ClassPart` holds the appointment type, resources and the defaults of new sessions, and the minimum minutes a partial attendance needs. `IClassRegistrationService` registers an active enrollment in a session of a class of its course (one place per class; the registration's source id is the enrollment and the class item, so Learning keeps no seat counts), puts the learner on the waitlist, accepts an offer and gives a place up. Attendance completes the class item; an enrollment that ends without completing gives its places up so the next person is offered them.
- **Pages**: the learner panel in the player (sessions in local time, places left, register, waitlist, accept or decline an offer, give up a place, attended); the admin Sessions list of a class, the session editor (meetings entered in local time), cancel with a reason, and the roster with bulk attendance and the waitlist (admin lists). Permission **Manage learning classes** (implied by Manage courses) or course staff (instructor, assistant).
- **Workforce bridge**: `IShiftService.PlaceSegmentAsync` and `RemoveSegmentsAsync` (new in Workforce) put a segment with a source on a worker's published shift and take it back; with Workforce on, a staff learner's confirmed place puts a Training segment on their shift for each meeting, and a cancelled place removes them. A reassigned or swapped shift drops the segments placed for the previous worker.

**Differences from the plan, and why.**

- Not built in this phase: `CourseOffering` (dated runs of a course with their own enrollment window and capacity; enrollments keep the existing `OfferingId`), the TimeTracker bridge, learner due dates through `ICalendarEventSource`, a class attendance data set, workflow activities of its own (the Scheduling appointment events already fire for class registrations), and a recipe step for sessions. Class sessions are managed with the Learning permission or as course staff, not with Scheduling's permissions (section 8 says Scheduling's).
- Changing a session's meetings does not move Training segments already placed, and a meeting at a time no published shift covers gets no segment (the person is not working then).

**Checked in the browser.** Learning Classes enabled (Scheduling came with it); a room resource and a Classroom session appointment type made in Scheduling; a class with capacity 1 and a 90-minute minimum added to the Forklift Safety outline, its editor linking to its sessions; a session on 25 November 9:00 to 11:00 added and listed (0 / 1, local time). A learner registered (Full); a second learner joined the waitlist; the first gave up the place and the second was offered it automatically, with the expiry in local time, and accepted it. The roster marked them partly attended for 100 minutes, which completed the class item (and the course, it being the only item); the panel then said they attended part of the class. A learner was refused the admin pages.

**Not checked in the browser.** The Training segment on a shift plan (the test site has no staff or published schedules; covered by Workforce tests on a real store that publish a schedule), an offer that runs out, cancelling a whole session, the meeting link, and the calendar event of a session.

**Fixed while verifying.** The sessions list failed to render its rows (the row views took the row instead of `ShapeViewModel<T>`); an integration test now opens the sessions and roster pages. A learner whose enrollment completed was told to enroll; the panel now uses the newest enrollment when none is open. Three learner pages from Phases 3 and 4 had no breadcrumb, which the view lint (tests/js/lint) refuses.

**Browser pass 2026-11-18.** Fixed test first on this branch; the red and green counts are in each commit.

| Bug | Fix | Commit |
| --- | --- | --- |
| Cancelling a session tells nobody, although the page says it does | `ClassSessionCancelledObserver` sends each learner whose place went with the cancelled session a **Class session cancelled** notification with the reason; a place given up by the learner or an ending enrollment tells nobody. | `c6803c463` |
| Class sessions appear on no attendee's calendar | `ClassSessionCalendarEventSource` (a Calendars event source) shows each meeting of a confirmed or attended place on the learner's own calendar, room or no room. | `5de8929ef` |
| GET/POST route guard | The class session pages say `HttpGet`. | `192b6e824` |
