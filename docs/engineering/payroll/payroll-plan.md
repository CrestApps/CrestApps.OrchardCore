# Payroll plan

Status: proposed 2026-11-13, revised the same day after a check against every Orchard Core and CrestApps feature (section 2.1). All nine decisions in section 12 were made on 2026-11-13: the recommended option (in bold) each time. Work is in progress; section 13 records what is built and where it differs from this plan.

## 1. Why

The solution can track who works for the business (Staff), what they are paid an hour (Staff Compensation), when they worked (Time Tracker, Workforce Time Clock) and which hours are approved (Workforce timesheets). It stops there: `IPayrollExportProvider` writes a CSV of hours for an outside payroll system. The Workforce plan put "Payroll calculation (taxes, deductions)" out of scope and the Staff plan named "a full Payroll module (gross-to-net)" as later work on Staff.

This plan adds that module. It must run payroll for a business of five people and for an enterprise with many legal entities, and it must not build again what the solution already owns.

What a survey of the leading payroll platforms and of the governing rules (IRS, SSA, DOL, Nacha, ANSI X9) showed, as design rules:

1. A stateless tax-provider interface is the key seam. Tax tables for every state and locality are a product of their own; the engine, the data model, the workflow and the documents are ours.
2. Earnings, deductions, employer contributions and imputed income are one concept, a pay code, with a class.
3. Tax treatment is a matrix (pay code by tax category), not a "pre-tax" flag. A 401(k) deferral lowers income tax wages but not Social Security wages.
4. Accumulators (period, month, quarter, year to date) drive every wage base, limit, goal and form box. They are derived from pay statement lines and can be rebuilt.
5. A pay statement never changes after it is final. A correction is a new document: a void linked to the original, an off-cycle run or an adjustment.
6. A pay run is a state machine with an approval step and deadlines.
7. Net pay distribution (bank accounts, check, split rules) is separate from calculation.
8. Bank files (Nacha), positive pay files, checks, stubs and tax forms are generated documents. The employer funds the payroll and files the returns. Holding client funds or filing as an agent brings money-transmission and reporting-agent duties and is out of scope.
9. The core is country-neutral. A country pack supplies statutory rules, election forms, tax forms and payment formats.

## 2. What exists and is reused

| Piece | From | Reuse step |
| --- | --- | --- |
| The employee (`StaffMember`, hire and termination dates, manager, teams, `ExternalId`) | Staff | Use |
| The Pay section of the staff profile (`StaffProfileSections.Pay`, "later from Payroll") | Staff + Resources page sections | Extend with `DisplayDriver<StaffMember>` |
| Dated hourly rates, salary, cost rate (`StaffPayRates`) | Staff Compensation | Add to the owner: currency, pay basis, dated salary, history entries (section 4.7) |
| Record scoping by reporting line (`IStaffScopeService`) | Staff | Use, opt-in only (section 6) |
| Self-service home (`IStaffHomeLinkProvider`, Staff > My Profile) | Staff | Extend |
| Departments (`Team` with `TeamKind.Department`) | Teams | Use |
| Approved and locked hours (`Timesheet`, `ITimesheetService`) | Workforce Time Clock | Use through a new input seam (section 3) |
| Pay periods (`PayPeriodCalendar`), pay codes (`PayCode`) | Workforce Time Clock | Move to Payroll (section 3) |
| Export of hours (`IPayrollExportProvider`, CSV column settings) | Workforce Time Clock | Replace with Orchard Data Pipelines (section 3) |
| Time-off balances and payout ledger kind | Workforce Time Off | Use through seams (balances on the stub, payout on a final run) |
| Money precision (`CurrencyScale`, `Money`) | Abstractions, Payments.Abstractions | Use; add `Money.Allocate` (section 4.6) |
| Gap-free numbers (`INumberSequence`) | Abstractions, YesSql.Core | Use; add "set next number" (section 4.6) |
| Bracket, threshold and table math (`ITaxCalculationMethod`, `ProgressiveTaxCalculationMethod`, `TaxTable`) | Taxation | Use; needs a base feature without Commerce (section 5.5) |
| Jurisdiction tree and lookup by address (`TaxJurisdiction`, `ITaxJurisdictionResolver`) | Taxation | Use |
| Addresses, named places | Addresses (`Address`, `NamedAddress`) | Use |
| Business days and holidays for pay dates | BusinessHours (`HoursSchedule`, `IOpenHoursService`) | Use |
| Party references (`PartyRef`, `IPartySource`, Staff party kind) | Parties | Use; Payroll adds an Employer kind |
| CRUD records, admin pages, status tables | CrestApps.Core catalogs, `DocumentCatalog`, `CatalogAdminController<T>`, `AuditedCatalogEntryHandlerBase`, `TransitionTable<TEnum>` | Use |
| Recipe and deployment steps, schemas | `NamedCatalogRecipeStep`, `NamedCatalogDeploymentSource`, `IRecipeStep`, `RecordsRecipeStepSchemaBase` | Use |
| Queued, retried work and locks | `ILeasedWorkQueue<T>`, `BackgroundWorkPacer`, `DistributedLockExtensions.LockAllAsync` | Use |
| Encrypted files | `AddTenantFileStore`, `EncryptedFileStore` | Use (stored stubs and tax forms) |
| Reports and data sets | Reports (`IReport`, `IndexedReportDataSet`), Orchard Data Pipelines (`AddDataSource`) | Use |
| CSV | `CsvWriter`, `CsvReader` | Use |
| Notices to people | Orchard `INotificationService` | Use |
| Audit | Orchard Audit Trail (pattern: `TimeTrackerAuditTrail`) | Use |
| Privacy | `IPersonalDataContributor` | Use |

What does not exist anywhere and this plan adds:

| Gap | Closest code today | Plan |
| --- | --- | --- |
| A PDF of anything but a `ReportDocument` | `Reports.Pdf` internals on PdfPig (`ReportPdfPage`, `ReportPdfFont*`), all `internal` | New `Pdf.Abstractions`/`Pdf.Core` (4.1) |
| Bank account records, protected numbers | `IDataConnectionSecretManager`, `SavedPaymentMethod` (card tokens), `ISecureCaptureTokenSink` (masks, does not store) | New Banking block (4.2) |
| Check printing | Nothing | New Checks block (4.3) |
| Outbound payments, ACH files | Marketplace `IVendorPayoutProvider`, `VendorPayout` (module-private, tied to `Vendor` and `Order`) | New Disbursements block (4.4) |
| A shared approval | Six hand-written copies (timesheet, time off, shift trade, punch correction, predictability pay, B2B order) | New Approvals block (4.5) |
| Protected fields inside a record, masking, reveal audit | `ProtectedSettingExtensions`, `IDataConnectionSecretManager`, private `MaskToLastFour` | Shared helper in Core (4.6) |
| Money split with exact remainder | `DefaultCheckoutDiscountService.Allocate`, `MarketplaceLedger.GetRefundShare` | `Money.Allocate` (4.6) |
| Payroll tax, elections, deductions, garnishments, accumulators, pay runs, statements, filings | Nothing | Payroll (5) |

### 2.1 Platform features this plan uses and does not rebuild

Every Orchard Core module (4.0 preview) and every CrestApps feature was checked against the plan. This table is the rule for the build: if a need is in the left column, the answer is the middle column, and the right column is not written.

| Need | Already provided by | So Payroll does not build |
| --- | --- | --- |
| Separate businesses with separate data | Orchard tenants; Tenant Hierarchy for a parent that sets up child tenants | Any tenant or company isolation. An `Employer` is a legal entity inside one business that shares its staff and reports; a separate business is a tenant |
| Sign-in, users, two-factor sign-in | Orchard Users (authenticator app, e-mail and SMS methods), CrestApps Users | A payroll sign-in, PIN or step-up screen. The docs tell sites to require two-factor for payroll roles |
| Roles and permissions | Orchard Roles, `IPermissionProvider`, `PermissionTemplate`, `IRoleGrantService` | A role model. Sample roles (Payroll Administrator, Payroll Clerk, Payroll Approver) ship as a recipe with Orchard's Roles step |
| Export to a file (CSV, Excel, JSON), delivery by SFTP, FTP, web API, e-mail or a download link, run history | Orchard Data Pipelines (`Create a file`, `Upload to an SFTP server`, `Send to a web API`, `Share a download link`) | Export pages, column pickers, file writers for tabular data, SFTP or HTTP upload code, download-link tokens |
| Import from a file | Data Pipelines `Read a media file` plus a destination step | A CSV import page or parser |
| A file layout Data Pipelines does not have | `IDataFileFormat` with `AddDataFileFormat<T>()` | A second "format provider" interface |
| Running something on a schedule | Orchard Workflows `Timer Event` | A scheduler for pay runs, exports or reminders |
| Multi-stage flows, reminders, escalation | Orchard Workflows | A flow engine inside Approvals |
| Notices by e-mail, SMS and in the admin, with each person's choice per kind of notice | Orchard Notifications (`INotificationService`, `AddNotificationType`, the notification center as an inbox) | An e-mail sender, an inbox page, notification preferences |
| Credentials of an outside service | Orchard Secrets (`SecretInputViewModel`, `<secret-input>`, `GetSecretValueAsync`, Azure Key Vault store) | Its own protected setting for a provider key |
| Change history of who did what | Orchard Audit Trail | An audit log table |
| Moving setup between sites | Orchard Recipes and Deployment, CrestApps Recipes schemas | Another import or export format for setup |
| Reports, charts, exports of a report, e-mailed reports, dashboard tiles | Reports, Report Builder, Report Subscriptions, report widgets | Report pages, a report mailer, a dashboard |
| Outbound events to other systems | Webhooks (`IWebhookEventProvider`, signed and retried) | Its own callback or integration endpoints |
| Connections to outside databases and APIs | Data Connectors | Stored endpoints and credentials for an accounting or HR system |
| AI assistants | AI (`AddCoreAITool`, as Calendars and Agentic Commerce do) | A chat or assistant of its own |
| Dates on a calendar with reminders | Calendars (`AddCalendarType`, Calendar Reminders) | A pay calendar view or a deadline reminder loop |
| Logos and public files | Orchard Media | A file upload for the logo on stubs and checks |
| Changing how a stub or a notice looks | Orchard shapes, Templates and Liquid | A template editor |
| Places people work, departments | Teams (`TeamKind.Site`, `TeamKind.Department`) | A work location or cost center record (4.7) |
| API authentication and documentation, when an API is added | The `Api` scheme with OpenID Connect, Orchard OpenAPI | Token handling or API docs |
| Personal data export and erasure | PersonalData | A privacy export |
| Keys for protected data across servers | ASP.NET Data Protection with Orchard's key-ring features (Azure, Redis) | Key management |

## 3. The Workforce pay functions move to Payroll

Today Workforce > Time Management owns Pay Periods, Pay Codes and Payroll Export, all inside the Time Clock feature. Only Workforce uses them (no other module references `PayPeriodCalendar`, `PayCode` or `PayrollExport*`).

They cannot stay in Workforce. Payroll must run for a business with no time clock (salaried staff only), so it needs pay schedules and pay codes of its own; that would make a second copy of both.

They cannot move as "Payroll extends Workforce" either, because the dependency points the other way. A timesheet is keyed by `(WorkerId, PayPeriodCalendarId, PeriodStart)`, `TimeClockPolicy.PayPeriodCalendarId` is required, and `DefaultTimesheetCalculator` puts a `PayCodeId` on every line. Time Clock needs periods and codes at run time; Payroll does not need Time Clock.

So Payroll becomes the owner, in a small feature that carries no engine, and Time Clock depends on that feature:

| Function | Today | After |
| --- | --- | --- |
| Pay periods | `PayPeriodCalendar` in Workforce.Abstractions | `PaySchedule` in Payroll.Abstractions, feature **Pay Setup**. Same ids, same `PeriodOn`/`WeekOf`. Adds pay date rules, more frequencies and holiday shifting |
| Pay codes | `PayCode` in Workforce.Abstractions (earnings only) | `PayCode` in Payroll.Abstractions, feature **Pay Setup**. Same ids and fields; gains a class so deductions and contributions are pay codes too |
| Shift differential window (`WindowStart`, `WindowEnd`, `Weekdays`) | Fields on `PayCode` | A Workforce card on the pay code editor, stored through `Properties`. It is a time rule, not a pay rule |
| Overtime thresholds, rounding, meals | `TimeClockPolicy` | Stay in Workforce |
| Timesheet submit, approve, lock | `ITimesheetService` | Stay in Workforce; lock gets its own permission |
| Payroll export | `IPayrollExportService`, `IPayrollExportProvider`, `PayrollExportBatch`, CSV column settings in Workforce | No export engine at all. A **Pay Inputs** data set plus a ready-made Data Pipeline (decision 9, below) |
| Pay rate editing in `WorkerTimeClockController` under `ViewPayRates` | Second editor of `StaffPayRates` | Removed; the Staff Compensation card under `ManageStaffPay` is the only editor |

The seam between the two is `IPayInputSource` in Payroll.Abstractions:

- `GetInputsAsync(PayInputQuery { EmployerId, PayScheduleId, PeriodStart, PeriodEnd, StaffMemberIds })` returns `PayInput` rows: staff member, pay code, date, hours or amount, rate override, cost allocation (department team, site team, activity, job), source kind and source id.
- `MarkConsumedAsync` and `ReleaseAsync` tell the source that a pay run or an export took, or gave back, its rows.
- Workforce implements it over Locked timesheets and approved predictability pay (and marks them exported, as `DefaultPayrollExportService` does today). Later sources: Time Tracker entries by category, the Contact Center reconciled timecard, Point of Sale tips, sales commissions from Orders.

The export to an outside payroll system is a Data Pipeline, not Payroll code. Today's export is a file writer with a column picker, a file store and a batch list, and Orchard Data Pipelines already is all of that, with delivery by SFTP, FTP, web API or e-mail and a schedule through a workflow timer:

- The **Pay Inputs** data set (rows from every `IPayInputSource`, filtered by pay schedule and period) is registered with `AddDataSource` in a `[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]` startup of Pay Setup.
- One pipeline step, **Mark pay inputs exported**, calls `MarkConsumedAsync` when the run succeeds (`context.Run.OnSucceeded`), so a failed run leaves the hours to export again.
- A recipe ships the pipeline that writes today's CSV. A migration turns a site's saved `PayrollCsvSettings` columns into a draft pipeline for an administrator to publish.
- `IPayrollExportProvider`, `PayrollCsvSettings` and the export page are removed. Old `PayrollExportBatch` rows stay readable in a list until they age out.
- A pipeline runs with the access of the user who published it, so the data set requires `ExportPayrollData`.

Payroll never references Workforce. Workforce.Abstractions references Payroll.Abstractions (for `PayCode` in `TimesheetCalculationInput`), and the Workforce module references the Payroll module only to carry the feature dependency.

Compatibility:

- A data migration copies each `PayPeriodCalendar` and `PayCode` into Payroll's collection with the same `ItemId`, as the Staff migration did for workers. Stored `PayPeriodCalendarId` and `PayCodeId` values on policies, timesheets and lines stay valid. It is batched and safe to run twice.
- The `WorkforceTimeClock` recipe step keeps reading its `PayPeriods` and `PayCodes` keys and passes them to the Payroll managers; `PayrollCsv` becomes the draft pipeline. New recipes use Payroll's own steps and Orchard's `DataPipelines` step.
- `ExportPayroll` and `ViewPayRates` are stored in roles. A migration grants Payroll's `ExportPayrollData` and `ManagePaySetup` to roles that hold the old ones, then the old ones are removed.
- Admin menu: Pay Schedules and Pay Codes move under **Payroll > Setup**. The export is found under **Tools > Data Pipelines**, with a link from the Timesheets page. Timesheets, Attendance and Predictability Pay stay where they are.

## 4. New building blocks

Each is its own `X.Abstractions` / `X.Core` / module set, has no reference to Payroll, and ships with admin lists, permissions, recipe and deployment steps with schemas, workflow events, a data set, docs and a catalog entry.

### 4.1 PDF documents (`Pdf.Abstractions`, `Pdf.Core`; libraries, no module)

Extracted from `Reports.Pdf`, which becomes the first consumer. Checks and Payroll are the second and third.

- `IPdfDocumentBuilder`: pages with a size and margins, text with measured wrapping, lines and boxes, images, tables that flow across pages, and drawing at an exact position in points (needed for checks and tax forms).
- `IPdfFontSource` with `AddPdfFont(name, embedded resource)`, so a module can ship a font. `ReportPdfFontResolver` only reads installed fonts today.
- Stays on PdfPig. `PdfLibraryLicenseTests` bans PDFsharp, MigraDoc, iText and QuestPDF.

### 4.2 Banking (`CrestApps.OrchardCore.Banking`)

One record for a bank account, for the business's own accounts and for the accounts of people it pays.

- `BankAccount`: owner (`PartyRef`), holder name, kind (checking, savings), country, currency, routing number, account number (protected, with last four digits kept for display), bank name, status (unverified, verification sent, verified, failed, closed), `Properties`.
- `IBankRoutingValidator` per country (US ABA check digit first; Canada transit and institution, IBAN later).
- `IBankAccountVerifier` providers (a prenote through Disbursements is the built-in one).
- Permissions: `ManageBankAccounts`, `ManageOwnBankAccounts`, `RevealBankAccountNumbers`. A reveal writes an Audit Trail event.
- An account whose number changed returns to unverified, and an observer (`IBankAccountObserver`) lets consumers react (Payroll sends the owner a security notice and can ask for approval).

### 4.3 Checks (`CrestApps.OrchardCore.Checks`)

Check printing for any module: paychecks first, then agency and vendor payments, refunds, Marketplace payouts.

- `CheckLayout`: stock (blank or pre-printed), form (check on top, middle or bottom with one or two vouchers; three to a page), the position of every field in points, calibration offsets, signature images with an amount above which a second signature line prints, MICR on or off.
- `CheckRecord` (the register): bank account, number, payee (`PartyRef` and printed name and address), amount, currency, date, memo, source (open kind and id, for example `PayStatement`), status.
- Status on a `TransitionTable`: Issued, Printed, Voided, StopPayment, Cleared, Reissued (links the new check), Stale. A void keeps its number.
- Numbers come from `INumberSequence`, one series per bank account. Loading new stock sets the next number.
- `ICheckMicrFormat`: the US line (ANSI X9.100-160: auxiliary on-us, routing, on-us, amount left blank) first; the Canadian layout later. The E-13B font ships through `AddPdfFont`.
- `IAmountInWords` per culture.
- `ICheckVoucherSource`: the caller supplies the voucher (stub) rows. Payroll supplies earnings, deductions, taxes and year-to-date totals.
- Print runs: choose checks, print to one PDF, confirm what printed, mark spoiled sheets (void and reissue with the next number).
- The bank's positive pay (issue) file is a Data Pipeline over the **Check Register** data set, shipped as a recipe. Checks writes no file itself; a bank's fixed-width layout is an `IDataFileFormat`.
- Signature images are kept in an encrypted tenant file store, not in the media library, which serves files to anyone with the URL.
- Workflow events: Check Issued, Printed, Voided, Cleared.

### 4.4 Disbursements (`CrestApps.OrchardCore.Disbursements`)

Money the business sends out. Marketplace vendor payouts are the first copy of this concept; Payroll net pay is the second.

- `Disbursement`: payee (`PartyRef`), amount and currency, method (open string: `Ach`, `Check`, `Provider`, `Manual`), source and destination bank accounts, source (open kind and id), a stable idempotency key built from business ids, status, trace number, failure.
- Status on a `TransitionTable`: Pending, Batched, Submitted, Settled, Returned, Failed, Canceled, Reversed.
- `DisbursementBatch`: the disbursements sent together, the file, totals, who made and released it.
- `IDisbursementProvider` by name. Built in:
  - **Bank file (Nacha)**, feature `Disbursements.Nacha`: writes the ACH file (PPD credits for people, CCD for businesses, prenotes, optional offset entry). The file has header, batch and control records with hashes and trace numbers, so it is written by this feature and not by a pipeline file format. The business downloads it, or a Data Pipeline delivers it: the feature adds one pipeline step, **Read a disbursement batch file** (files out), which connects to Orchard's SFTP, FTP and web API destinations. Imports the bank's return and change files: a return moves the disbursement to Returned; a change notice updates the bank account.
  - **Check**, when Checks is on: issues a `CheckRecord`.
  - **Manual**: paid outside the system, recorded.
- `IDisbursementObserver` for consumers (Payroll shows a returned deposit on the pay run and reissues it).
- Slice F8 moves Marketplace's Stripe Connect payout onto `IDisbursementProvider` (decision 6).

### 4.5 Approvals (`CrestApps.OrchardCore.Approvals`)

Approvals is a record of a decision, with the rules about who may make it. It is not a flow engine: Orchard Workflows already is one.

- `ApprovalRequest`: subject (open kind and id), title, amount when there is one, requester, policy, status (Pending, Approved, Rejected, Canceled), the decisions made (who, when, note), due time.
- `ApprovalPolicy`: who may approve (a permission, a role, the subject's manager through `IStaffHierarchy`, or named staff) and how many of them must.
- Rules built in: the requester cannot approve their own request; a person cannot decide a request about their own record. Both can be turned off per policy for a one-person business.
- `IApprovalSubjectHandler` per subject kind: called when a request is decided, inside the same unit of work.
- The approver's inbox is Orchard's notification center: an `Approval` notification type (`AddNotificationType`) with a link to the subject, so each person chooses e-mail or SMS for it. One `<admin-list>` page lists open requests.
- Workflow: Approval Requested, Approved and Rejected events; Request Approval, Approve and Reject tasks. A second stage, a reminder or an escalation is a workflow (Approval Requested, a `Timer Event`, `NotifyUserTask`, Request Approval), shipped as sample recipes. Approvals has no stages, timers or background task of its own.
- Payroll uses it for pay runs, pay changes, bank account changes and voids. Moving the six existing approval copies onto it is a follow-up plan (decision 6).

### 4.6 Shared helpers (`CrestApps.OrchardCore.Core`, `Payments.Abstractions`, `Abstractions`)

- **Protected record values**: `ProtectedValue { Cipher, Hint }` with `ISensitiveValueProtector` (Data Protection with a purpose per field), `SensitiveMask.LastFour`, an optional keyed hash for "is this number already used" checks, and a reveal that writes an Orchard Audit Trail event. Banking and Payroll use it, for two things only: a person's tax id and a bank account number. Orchard Secrets is not the place for these: a secret is a named credential an administrator manages, and there would be one for every employee. Credentials of outside services (a tax engine key, a bank's SFTP password) do go in Orchard Secrets.
- **`Money.Allocate(total, weights, currency)`**: an exact split where the last share takes the rounding remainder. The Checkout discount and Marketplace refund-share copies move onto it. Payroll uses it for net pay splits and cost allocation.
- **`INumberSequence`**: add `SetNextAsync(series, next)` and read one series record instead of listing all of them on every call.

### 4.7 Staff and Teams additions (the owners gain what Payroll needs)

- **Work locations are Teams of kind Site.** A site team already has a time zone, a place in the team tree and dated members, and Workforce already keeps an address for it (`WorkforceLocation` in `WorkforceTeamSettings`, as one line of text). Teams gains a structured `Address` on the team; Workforce's copy moves onto it. Payroll adds the tax jurisdictions of a site with a `DisplayDriver<Team>` card. No work location record is added.
- **Cost centers are Teams too.** Payroll's card on a team holds its ledger code. A statement line is allocated to a department team, a site team, an activity code and a job. No cost center record is added.
- **Compensation**: `StaffPayRates` gains a currency, a pay basis (hourly, salary, per unit) and dated salary rows; every change appends a `StaffHistoryEntry` (pay changes are not in the history today); the editor formats numbers with the invariant culture.

## 5. Payroll

### 5.1 Projects and features

`src/Abstractions/CrestApps.OrchardCore.Payroll.Abstractions`, `src/Core/CrestApps.OrchardCore.Payroll.Core`, `src/Modules/CrestApps.OrchardCore.Payroll`, and the country pack `src/Modules/CrestApps.OrchardCore.Payroll.UnitedStates`.

| Feature | Id | Contents | Depends on |
| --- | --- | --- | --- |
| Pay Setup | `CrestApps.OrchardCore.Payroll.PaySetup` | Pay schedules, pay codes, the `IPayInputSource` seam and, with Data Pipelines on, the Pay Inputs data set. Turned on by Time Clock or Payroll | None |
| Payroll | `CrestApps.OrchardCore.Payroll` | Employers, pay profiles, pay items, pay runs, the engine, statements, accumulators, stubs | Pay Setup, Staff, Staff Compensation, Approvals |
| Direct Deposit | `CrestApps.OrchardCore.Payroll.DirectDeposit` | Net pay split across bank accounts and paid through Disbursements | Payroll, Banking, Disbursements |
| Paychecks | `CrestApps.OrchardCore.Payroll.Checks` | Net pay and agency payments by printed check | Payroll, Checks |
| My Pay | `CrestApps.OrchardCore.Payroll.SelfService` | Stubs, tax forms, elections and bank accounts for the employee | Payroll |
| Garnishments | `CrestApps.OrchardCore.Payroll.Garnishments` | Orders, limits, priority, payee payments | Payroll |
| Contractor Payments | `CrestApps.OrchardCore.Payroll.Contractors` | Payments with no withholding, year-end totals | Payroll |
| Tax Liabilities and Filings | `CrestApps.OrchardCore.Payroll.Compliance` | Liability ledger, deposit due dates, filing records, year-end close | Payroll |
| Payroll Journal | `CrestApps.OrchardCore.Payroll.Journal` | General ledger entries from statement lines and the account mapping, read through a data set | Payroll |
| Payroll AI Tools | `CrestApps.OrchardCore.Payroll.AI` | Tools registered with `AddCoreAITool` ("explain my pay statement", "what changed since the last run"). Off unless a site turns it on, because pay data then goes to the site's AI provider | Payroll, AI |
| Payroll Tax Tables | `CrestApps.OrchardCore.Payroll.TaxTables` | The built-in table-driven tax provider | Payroll, Taxation base feature |
| United States Payroll | `CrestApps.OrchardCore.Payroll.UnitedStates` | W-4 elections, federal tables, federal forms and files | Payroll Tax Tables, Compliance |

Optional integrations are `[RequireFeatures]` startups in the Payroll module: Recipes, Deployment, the Recipes schemas, Workflows, Data Pipelines, Reports, Audit Trail, Notifications, Teams, Webhooks (pay run and statement events through `IWebhookEventProvider`) and Calendars (pay dates and approval deadlines as a calendar type managed by code, so Calendar Reminders sends the reminders).

### 5.2 Setup records

| Record | Key fields |
| --- | --- |
| `PaySchedule` (Pay Setup) | Name, frequency (weekly, every two weeks, every four weeks, twice a month with chosen days, monthly, quarterly, yearly), anchor date, overtime week start, pay date rule (days after the period ends, or fixed days), pay date shift on a non-business day (before or after, from an `HoursSchedule`), lead days for approval, enabled |
| `PayCode` (Pay Setup) | Code, name, class (Earning, Deduction, EmployerContribution, ImputedIncome, Reimbursement, Memo), kind (open string; Regular, Overtime, DoubleTime, Holiday, PaidTimeOff, Bonus, Commission, Tips, Salary and those a module registers with `AddPayCodeKind`), multiplier, premium an hour, export code, enabled, `Properties` |
| `PayCode` payroll card (Payroll) | How it is calculated (fixed amount, hours by rate, percent of a set of earnings, percent of disposable earnings, a registered `IPayCodeCalculator`), tax treatment per tax category, supplemental flag, counts in the regular rate, counts as hours worked, limits (a period, a year, a lifetime goal), priority, arrears rule, proration rule, retro rule, form boxes (from the country pack) |
| `Employer` | Name, legal name, tax id, address, logo (a media library file), country pack, currency, funding bank account, signatory, payroll contact, tax registrations (jurisdiction, agency, account id, dated rate, deposit frequency), status. Registered as a party kind |
| Payroll card on a `Team` | Ledger code (the team as a cost center) and, on a site team, its tax jurisdictions |
| `EmployeePayProfile` | Staff member, employer, pay schedule, worker type (employee, contractor), overtime status (exempt, non-exempt), tax id (protected, with a keyed hash to find a number used twice), legal name, home address (`Address`), work site (a site team), dated tax elections (a field set per jurisdiction defined by the country pack), net pay distribution (ordered rows: bank account or check; amount, percent or remainder), default cost allocation, first day retro pay may reach, status |
| `EmployeePayItem` | Profile, pay code, amount or percent, start and end, employer match, goal and paid toward goal, a period and a year maximum, arrears balance |
| `GarnishmentOrder` | Profile, type, case number, agency, payee, amount or percent, cap rule, priority, received date, total owed, status |

`EmployeePayProfile` is a document of its own in the Payroll collection, keyed by staff member and employer. It is not put in `StaffMember.Properties`: every module that loads a staff member would then load tax ids and elections, and a person can work for two employers.

### 5.3 Pay runs and statements

| Record | Key fields |
| --- | --- |
| `PayRun` | Number, employer, pay schedule, type (Regular, OffCycle, Supplemental, Correction, Final, Reversal, Historical, Contractor), period, pay date, which staff, which pay items apply, supplemental tax method, totals, cash needed, warnings and blockers, approval request, status |
| `PayInput` | One-time rows for a run: typed in, read from an `IPayInputSource`, or saved by a Data Pipeline. A file of bonuses or hours is imported with Orchard's `Read a media file` step and Payroll's one destination step, **Save pay inputs** (rows in, rejected rows out, like `Save content items`) |
| `PayStatement` | Number, run, staff member, employer, pay date, period, gross, net, lines, payments, `VoidOf`/`VoidedBy`, a snapshot of the names and addresses printed, year-to-date and leave balances as shown, status (Draft, Final, Paid, Voided) |
| `PayStatementLine` | Pay code or tax, class, hours, rate, amount, jurisdiction, payer (employee or employer), taxable and subject wages, cost allocation, retro period, source rows |

`PayRun` status on a `TransitionTable`: Draft, Calculating, Calculated, PendingApproval, Approved, Processing, Paid, PartiallyPaid, Failed, Canceled.

1. **Open.** A person or a workflow opens a run. A run that opens on a schedule is a workflow (`Timer Event`, then the Open Pay Run task), shipped as a sample recipe; Payroll has no scheduler. It reads pay inputs and shows what is missing as blockers (no tax id, no elections, unverified bank account, unapproved timesheets).
2. **Calculate.** One queued job per employee on `ILeasedWorkQueue`; the run is locked with `LockAllAsync`. Changing an input recalculates only that employee.
3. **Review.** The register (an `<admin-list>` of draft statements), totals, cash needed, differences from the last run, warnings.
4. **Approve.** Through Approvals. A change after approval returns the run to Calculated.
5. **Process.** Statements become Final and immutable, numbers are issued, disbursements and checks are created, sources are told their rows were consumed, accumulators include the run.
6. **Pay.** Disbursement and check results move the run to Paid, PartiallyPaid or Failed.
7. **Correct.** A void creates a reversing statement linked to the original (and voids the check or reverses the deposit). A reissue pays the same statement again. Anything else is an off-cycle or correction run.

A Historical run loads statements paid elsewhere (a move from another system in the middle of a year), so accumulators start right.

### 5.4 The engine

The calculation is a pure function of a `PayCalculationContext` (profile, compensation, inputs, pay items, orders, accumulators, pay date), as `ITimesheetCalculator` is. It is an ordered pipeline of `IPayCalculationStep`s that a module can add to or replace:

1. Earnings: salary for the period (prorated for hire, termination or a pay change), hours by rate from inputs, recurring earnings, retro differences.
2. Regular rate and overtime premium (weighted average over several rates; a later bonus re-prices the overtime of the weeks it was earned in).
3. Deductions taken before tax, with limits from accumulators.
4. Taxable wages for each tax category, from the pay code matrix.
5. Taxes, through `IPayrollTaxProvider`.
6. Deductions taken after tax, by priority.
7. Garnishments on disposable earnings.
8. Net check: when net is below the minimum, the lowest-priority deductions are cut and go to arrears.
9. Reimbursements and employer contributions.
10. Net pay distribution with `Money.Allocate`.

Gross-up (a target net) repeats the pipeline until it converges. Accumulators are a YesSql index over final statement lines (`PayStatementLineIndex`: staff member, employer, pay date, code, class, jurisdiction, amount, hours, taxable wages), summed by period; they hold no state of their own.

### 5.5 Taxes

`IPayrollTaxProvider.CalculateAsync(PayrollTaxRequest)` is stateless. The request carries the employer's registrations, the home and work jurisdictions, the elections, the pay date and frequency, wages per tax category, the supplemental method and the accumulators. The result is tax lines (tax, jurisdiction, payer, taxable wages, amount). A commercial tax engine, a full-service provider and our own tables all fit behind it. A provider's key is an Orchard secret (`<secret-input>`, read with `GetSecretValueAsync`).

- **Payroll Tax Tables** is the built-in provider. A `PayrollTaxRule` (jurisdiction, tax category, payer, method, table, wage base, dated) is calculated with Taxation's `ITaxCalculationMethod`s over dated `TaxTable`s, and jurisdictions come from `ITaxJurisdictionResolver`. Any jurisdiction in any country can be set up by hand.
- Taxation is one feature today and depends on Commerce. It gains a base feature (jurisdictions, tables, calculation methods) with no Commerce dependency; the sales tax pipeline stays in the current feature.
- **United States Payroll** ships the federal rules as dated recipes (income tax by the IRS percentage method, Social Security, Medicare and the additional rate, federal unemployment), the W-4 election field set, and the supplemental flat and aggregate methods.
- State and local taxes: decision 3.

### 5.6 Payments and documents

- Net pay follows the profile's distribution: Direct Deposit creates one `Disbursement` per bank account row; Paychecks creates a `CheckRecord` for a check row or when there is no verified account. Garnishment and benefit payees are paid the same two ways.
- The pay stub is a shape (`PayStatement` display) for the screen, so a site changes it with a theme or Orchard Templates, and a `Pdf.Core` document for download and for the check voucher. Stored PDFs go in an encrypted tenant file store.
- A notice tells the employee the stub is ready and links to it, through `INotificationService` with a registered notification type (Pay statement ready, Deposit returned, Bank account changed, Tax form ready), so each person chooses e-mail, SMS or the notification center. No amount and no attachment is put in a notice.
- Compliance: each tax line adds to a `TaxLiability` with a due date from the employer's deposit schedule; `Filing` records hold the totals of a form for a period with a status and an amendment link. The United States pack writes the quarterly and annual federal worksheets, W-2 and W-3 (PDF and the SSA file) and 1099-NEC. The business files them.
- Payroll Journal: debit and credit rows from statement lines through a mapping (pay code or tax, with the team's ledger code, to account) and a reversing entry for a void. The rows are a data set; a Data Pipeline writes the file an accounting system wants or sends it to its API. Payroll writes no journal file.

### 5.7 My Pay

Staff > My Profile gains a **My pay** link (`IStaffHomeLinkProvider`): pay statements, tax forms, tax elections and bank accounts. A change of elections takes effect on the next run that is not yet approved. A change of bank account sends a security notice, returns the account to unverified and, by setting, needs approval. Later: the employee reviews their coming pay before the run is submitted.

## 6. Security

Payroll data is the most sensitive data in the solution. The rules:

**Permissions** (Administrator gets all; no other role gets any by default except the Staff role, which gets the two "own" permissions):

| Permission | Allows |
| --- | --- |
| `ManagePaySetup` (Pay Setup) | Pay schedules and pay codes |
| `ManagePayrollSettings` | Employers, registrations, ledger codes, tax setup, settings |
| `ViewPayProfiles` / `ManagePayProfiles` | See or change pay profiles, pay items and elections |
| `ViewTaxIds` | See a full tax id (every view is audited) |
| `ManageGarnishments` | Garnishment orders |
| `PreparePayRuns` | Open, enter, import and calculate |
| `ApprovePayRuns` | Decide a run's approval |
| `ProcessPayRuns` | Finalize and pay; make bank files and print checks |
| `VoidPayStatements` | Void and reissue |
| `ViewPayRuns` | See runs and every statement in them |
| `ViewPayrollReports` / `ExportPayrollData` | Reports and data sets; exports and the outside payroll export |
| `ManagePayrollFilings` | Liabilities, deposits, filings, year-end |
| `ViewPayOfManagedStaff` | A manager sees the pay of the staff `IStaffScopeService` gives them. Off for everyone by default |
| `ViewOwnPay` | One's own statements and tax forms |
| `ManageOwnPayDetails` | One's own elections and bank accounts |

- **Per employer.** Every permission but the "own" ones also exists per employer through a `PermissionTemplate` ("Prepare pay runs for {employer}"), so a payroll clerk of one legal entity sees nothing of another. `IPayrollScopeService` answers "which employers may this user act on" for controllers, data sets, reports and exports alike.
- **Separation of duties.** By default the person who prepared a run cannot approve it, and nobody changes their own pay rate, pay items, inputs or garnishments through the admin pages (the Workforce gap SEC-P6, closed here from the first day). A setting allows it for a one-person business.
- **Protected at rest.** A person's tax id and every bank account number are `ProtectedValue`s. Screens show the last four digits. A full value needs its own permission and writes an Audit Trail event.
- **One check on every path.** Controllers authorize each action; data sets take the permission in their constructor and restrict rows with `RestrictAsync`; reports set `Permission` and `ExportPermission`; workflow tasks go through the same services and status table; there is no API in the first version.
- **Data Pipelines.** A published pipeline reads with the access of the user who published it, so every payroll data set requires `ViewPayrollReports` or `ExportPayrollData` of that user, per employer. Orchard's `ManageDataPipelines` is already marked security critical; the docs say to give it to payroll staff only with that in mind.
- **Never leaves by accident.** No tax id or bank number is a data set column. Deployment and recipe export leave out tax ids, bank numbers and pay history unless the step's **Include sensitive data** option is on, which needs `ExportPayrollData`. Nothing sensitive goes in a log (`SanitizeLogValue`, log classifications), a metric tag, a notice, a webhook payload or a search index. AI tools are a feature of their own that is off by default, and each tool checks the same permission as the page it mirrors.
- **Sign-in.** Payroll adds no sign-in step. Orchard's two-factor authentication protects the payroll roles.
- **Audit.** Audit Trail events for profile, election and bank changes, every run status change, voids, reveals, exports and setting changes.
- **Privacy and retention.** The `IPersonalDataContributor` exports a person's pay data; it refuses erasure of records inside the legal retention period (a setting per country pack) and no `IRetentionPolicy` touches statements.

## 7. Orchard integration

**Recipes and deployment.** Every record type has a recipe step, a deployment step, an `IRecipeStep` schema and a deployment step schema:

| Data | Steps |
| --- | --- |
| Pay schedules, pay codes, employers, tax rules, check layouts, approval policies, settings | `NamedCatalogRecipeStep` / `NamedCatalogDeploymentSource` per type |
| Sample roles, workflows (scheduled run, approval reminder) and pipelines (export, positive pay, journal) | Orchard's own `Roles`, `WorkflowType` and `DataPipelines` steps in recipes Payroll ships; no step of ours |
| Bank accounts, pay profiles, pay items, garnishment orders | Steps with **Include sensitive data** (off: structure without numbers) |
| Pay history (final statements), check register, disbursements | One opt-in step each; imported rows are historical and read-only |

**Workflows** (`[RequireFeatures(OrchardCoreFeatureIds.Workflows)]`; activities only, no change to the workflow engine or editor):

| Kind | Activities |
| --- | --- |
| Events | Pay Run Opened, Calculated, Submitted for Approval, Approved, Rejected, Processed, Paid, Failed; Pay Statement Issued, Voided; Deposit Returned; Pay Profile Changed; Bank Account Changed; Tax Liability Due |
| Tasks | Open Pay Run, Add Pay Input, Calculate Pay Run, Submit Pay Run for Approval, Approve Pay Run, Reject Pay Run, Process Pay Run, Generate Pay Statement Documents |

Tasks take Liquid inputs (employer, schedule, type, period, pay date, run id), set outputs (`PayRunId`, counts, totals) and end in `Done` or `Failed`, or `Blocked` when the run has blockers. An approval by a workflow follows the same policy as one by a person.

Payroll adds no activity for what Orchard already has: a schedule is `Timer Event`, a notice is `NotifyUserTask`, an e-mail is `EmailTask`, an export is `Run Data Pipeline Task` and `Data Pipeline Run Completed Event`.

**Data sources.** One `PayrollDataSource` registered with `AddDataSource`, with `IndexedReportDataSet`s that serve Data Pipelines and the Report Builder alike:

| Data set | Rows |
| --- | --- |
| Employees | Pay profiles (employer, schedule, worker type, status; tax id last four only) |
| Pay Runs | Runs with totals and status |
| Pay Statements | One row per statement |
| Earnings, Deductions, Taxes, Employer Contributions | Views of statement lines by class, with pay code, jurisdiction, department and site |
| Payments | Statement payments with method and status |
| Year-to-date Balances | Accumulators by staff member, code and year |
| Tax Liabilities, Garnishments, Pay Inputs, Journal Entries | One row each |

Checks adds Check Register; Disbursements adds Disbursements; Staff adds Compensation History. Every date column is indexed and pay dates are `Date` fields.

The whole plan adds three pipeline steps and no file format: **Mark pay inputs exported**, **Save pay inputs** and **Read a disbursement batch file**. Everything else that leaves as a file is a data set with Orchard's own steps.

**Reports** (category `ReportsConstants.Categories.WorkforcePayroll`): Payroll Register, Payroll Summary, Cash Requirement, Tax Liability, Deductions and Contributions, Labor Distribution, Year-to-date Earnings, Check Register. They export through the Reports formats (CSV, Excel, PDF), are e-mailed with Report Subscriptions, and appear on the dashboard as report widgets (`DefineReportWidgetAsync`).

**Admin.** A top-level **Payroll** menu with an icon: Pay Runs, Employees, Garnishments, Liabilities and Filings, Setup (Employers, Pay Schedules, Pay Codes, Tax Rules). Every list is an `<admin-list>`, every form uses cards and `action-bar-bottom`, every page has a breadcrumb, every time shown goes through `ILocalClock` (pay dates and periods are pure dates and are not converted), amounts are entered as invariant-culture text.

## 8. Slices

Three pull requests, a commit per slice, each slice test-first (decision 7).

**PR 1: building blocks**

| Slice | Scope | Exit gate |
| --- | --- | --- |
| F1 | Protected record values, `Money.Allocate`, `INumberSequence.SetNextAsync`; the two allocation copies moved | Checkout and Marketplace tests pass on the shared helper |
| F2 | `Pdf.Abstractions`/`Pdf.Core`; Reports.Pdf on it | The existing report PDF tests pass unchanged; a fixed-position test page renders |
| F3 | Banking | Accounts with protected numbers, validation, reveal audit, steps and data set |
| F4 | Checks | A layout prints a check with a correct MICR line; void and reissue; the positive pay pipeline recipe writes the issue file |
| F5 | Disbursements with the Nacha, Check and Manual providers and the batch file pipeline step | A balanced ACH file validates and reaches a test SFTP server through a pipeline; a return and a change notice import |
| F6 | Approvals | The policy, the two built-in rules, the notification type, workflow activities, a reminder workflow recipe |
| F7 | Address on a team; Compensation additions; Workforce's rate editor removed | A site team has a structured address; one pay editor; pay changes in history |
| F8 | Marketplace payouts on Disbursements | Stripe test-mode payout, refusal and retry |

**PR 2: Payroll**

| Slice | Scope | Exit gate |
| --- | --- | --- |
| P1 | Pay Setup; Workforce Time Clock on it; id-keeping migration; recipe and permission compatibility | Existing Time Clock tests pass; a seeded site migrates twice with no change |
| P2 | `IPayInputSource` with the Workforce source; Pay Inputs data set; the Mark pay inputs exported step; the export pipeline recipe; Workforce's export code removed | The pipeline writes the same CSV as today from a locked period and marks it exported only when the run succeeds |
| P3 | Employers, team ledger codes and site jurisdictions, permissions per employer, settings, sample roles recipe | A clerk of one employer cannot open another's pages, data sets or pipelines |
| P4 | Pay profiles, pay items, the Pay section cards on the staff profile | Protected tax id, election history, blockers list |
| P5 | Pay code classes, tax treatment, limits, seeds | A deduction with a yearly limit stops at the limit |
| P6 | Engine, accumulator index, tax provider seam, Payroll Tax Tables, Taxation base feature | Golden-file gross-to-net cases |
| P7 | Pay runs: types, status table, inputs, the Save pay inputs pipeline step, review, approval | Two-person approval; a change after approval reopens it; a file of bonuses imports with rejected rows reported |
| P8 | Process and pay: statements, Direct Deposit, Paychecks, stub PDF, notices, void and reissue | An ACH file and a printed check from one run; a void reverses accumulators |
| P9 | My Pay | An employee sees only their own statements |
| P10 | Steps and schemas, workflow activities and the scheduled-run workflow recipe, data sets, reports and widgets, notification types, webhook events, the pay calendar type, Audit Trail, personal data, docs, catalog entries, a `crestapps-payroll` skill | Architecture guards pass; UI pass on a fresh site |

**PR 3: compliance and depth**

| Slice | Scope |
| --- | --- |
| C1 | United States pack: W-4, federal tables, supplemental methods |
| C2 | Garnishments |
| C3 | Regular rate re-pricing, tips and tip credit, retro pay, gross-up, an extra pay period in a year |
| C4 | Liabilities, deposit schedules, filings, W-2 and W-3, year-end close |
| C5 | Contractor payments and 1099-NEC |
| C6 | Payroll Journal with its data set and a sample pipeline |
| C7 | Employee review before submission, time-off payout, tips from Point of Sale, commissions from Orders |
| C8 | Payroll AI Tools |

## 9. Tests and checks

- Engine: golden-file cases per rule (proration, limits, arrears, multi-rate overtime, supplemental methods, garnishment caps, gross-up), built from the published worked examples of the tax agency where one exists.
- Files: an ACH file checked record by record (lengths, hash, totals); a MICR line checked position by position; the SSA file by record length and totals.
- Security: each permission and each employer scope against controllers, data sets, reports, exports and workflow tasks; the two separation rules; reveal audit; deployment output with the sensitive option off.
- Migration: seeded Workforce pay periods and pay codes keep their ids; timesheets still resolve; saved CSV columns become a draft pipeline; running twice changes nothing.
- Pipelines: each shipped pipeline recipe (export, positive pay, journal) runs end to end in a test; a failed run does not mark inputs exported; a user without `ExportPayrollData` cannot read a payroll data set through a pipeline.
- Architecture guards: `RecipeSchemaCoverage`, `WorkflowActivityViews`, `DeploymentStepSerialization`, `MigrationAdditiveOnlyGuard`, `SiteSettingsGroupPermissionRegistration`, `UnboundedCollectionLoad`, `FileSizeRatchet`, layering and feature-id ratchets, `PdfLibraryLicenseTests`; the vitest view lint.
- A full UI pass on a fresh test site (`admin` / `Password1!`): set up an employer, two employees, run a regular payroll to an ACH file and a printed check, void one statement, view My Pay as the employee.

## 10. Out of scope

- Holding or moving the business's money, and filing returns for it.
- State and local tax tables beyond what decision 3 chooses.
- Benefits administration (plans, enrollment, carrier files). Payroll takes the deductions and contributions; a Benefits module on Staff can feed pay items later.
- An accounting ledger. The journal is exported.
- Exchange rates. Every employer pays in one currency with `CurrencyScale`; nothing converts.
- HTML-to-PDF, earned wage access, pay cards. An HTTP API is later work and will use the `Api` scheme with OpenID Connect and Orchard OpenAPI.
- Countries other than the United States (the core stays neutral; a pack per country comes later).

## 11. Things to verify while building

- Whether Taxation's jurisdiction and table stores can run without Commerce, or need more than a manifest split.
- A MICR E-13B font with a license we can ship; otherwise the site uploads its own.
- The exact MICR field positions against the ANSI X9.100-160 standard (the research used secondary sources) and a bank's test of a printed check.
- Current Nacha rules for prenote and change-notice timing.
- Whether `PublicApiApprovalTests` should cover the new Abstractions projects.
- The Data Pipelines step API for a destination that takes rows and for work confirmed when a run succeeds (`context.Run.OnSucceeded`). No CrestApps module has a custom pipeline step yet; these three would be the first.
- No CrestApps module uses Orchard Secrets' `<secret-input>` yet; the tax engine adapter would be the first.
- Whether Orchard's two-factor authentication can be required for chosen roles, or only for everyone.
- Amount in words: no library for it is referenced today. Take a maintained, permissively licensed one (Humanizer) rather than write one per language, if we accept the dependency.
- Workforce still sends notices through its own `IWorkforceNotifier` (the audit lists it as a duplicate of Orchard Notifications). Payroll and Approvals use Orchard Notifications from the start and do not copy it.

## 12. Decisions

Decided 2026-11-13: the option in bold in every row.

| # | Decision | Options |
| --- | --- | --- |
| 1 | Where pay periods and pay codes live | **Move to Payroll's Pay Setup feature; Time Clock depends on it (section 3).** Or leave them in Workforce and give Payroll its own (two copies) |
| 2 | Where payroll data on a person lives | **A separate `EmployeePayProfile` shown in the staff profile's Pay section; pay rates stay in Staff Compensation.** Or everything in `StaffMember.Properties` |
| 3 | State and local taxes | **Ship federal tables; states and localities through an adapter module for a commercial tax engine, with hand-entered rules as the fallback.** Or we maintain tables for every state ourselves (a standing yearly duty). Or federal only for now |
| 4 | Compliance posture | **Calculate, generate documents and files; the business funds and files.** Or integrate a full-service provider behind the same seams |
| 5 | The legal entity | **A Payroll `Employer` record, registered as a party kind.** Or a new shared Legal Entities block now (Commerce's `StoreProfileSettings` would move onto it). Accounts and Teams do not fit: accounts are the business's customers, and a team is a group, with a closed kind list |
| 6 | New building blocks | **Pdf, Banking, Checks, Disbursements, Approvals as in section 4; Marketplace payouts move onto Disbursements in this plan (F8); the six existing approval copies move in a follow-up plan.** Or fewer blocks, kept private to Payroll |
| 7 | Pull requests | **Three (blocks, Payroll, compliance), a commit per slice.** Or one |
| 8 | Countries | **United States pack first, neutral core.** |
| 9 | The export to an outside payroll system | **A data set and a ready-made Data Pipeline; Workforce's export page, column settings and `IPayrollExportProvider` are removed (section 3).** Or keep the page as a thin screen over the same data set. Or move the export code to Payroll unchanged (it then repeats what Data Pipelines does) |
