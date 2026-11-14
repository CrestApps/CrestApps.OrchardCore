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

One pull request for the whole plan, a commit per slice, each slice test-first (decision 7, changed by Mike on 2026-11-14 from three pull requests). The three groups below stay as the order of work and the points where the end-of-group checks run.

**Group 1: building blocks**

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

**Group 2: Payroll**

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

**Group 3: compliance and depth**

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
| 7 | Pull requests | **One pull request for the whole plan, a commit per slice** (Mike, 2026-11-14; first approved as three: blocks, Payroll, compliance). The planning session pushes and opens it once everything is implemented and validated |
| 8 | Countries | **United States pack first, neutral core.** |
| 9 | The export to an outside payroll system | **A data set and a ready-made Data Pipeline; Workforce's export page, column settings and `IPayrollExportProvider` are removed (section 3).** Or keep the page as a thin screen over the same data set. Or move the export code to Payroll unchanged (it then repeats what Data Pipelines does) |

## 13. Progress

What is built, slice by slice, and where it differs from the plan above.

### F1 as built

- `Money.Allocate` is in `Payments.Abstractions`. The two existing "copies" were not moved onto it: the Checkout discount allocator also caps each line by the room it has left and spreads the overflow, and Marketplace's `GetRefundShare` is one proportion, not a split. Neither is the same operation.
- `INumberSequence` gained `SetNextAsync` and `GetLastAsync` (a screen shows the next check number). It still lists the series records on each call: reading one record needs an index and a migration, and a site has tens of series, not thousands.
- The field protector is `ISensitiveValueProtector` with `ProtectedValue` and `SensitiveMask` in `CrestApps.OrchardCore.Abstractions` (`SensitiveData`), implemented by `DataProtectionSensitiveValueProtector` in `CrestApps.OrchardCore.Core` and registered with `AddSensitiveValueProtector()`. The hint never holds more than half of a value. The keyed hash ("is this number used twice") waits for the slice that needs it (P4), and each consumer writes its own Audit Trail event for a reveal.

### F2 as built

- One library, `CrestApps.OrchardCore.Pdf.Core` (namespace `CrestApps.OrchardCore.Pdf`), and no `Pdf.Abstractions`: there is no contract to implement another way, consumers call the library.
- Moved out of `Reports.Pdf` and made public: `PdfCanvas` (was `ReportPdfPage`), `PdfFontFace`, `PdfFontSet` (`GetInstalled(family)`), `PdfText`, `PdfColor`; `PdfFontResolver` stays internal. The report writers (flow, tables, charts, theme) stay in `Reports.Pdf`.
- Added: `PdfDocumentWriter` and `PdfPageSize` (the entry point for documents that place their own content), `PdfCanvas.DrawTextOnBaseline`, `DrawTextRight`, `DrawRectangle` and `DrawImage` (PNG and JPEG), and symbol fonts (`AddSymbolFont`, for a font with digits and symbols only, such as the check line font).
- Not built: `AddPdfFont` and a font catalog. A module that ships a font reads its own embedded resource and calls `AddSymbolFont` or `AddFont`.

### F3 as built

- Projects: `CrestApps.OrchardCore.Banking.Abstractions` (`BankAccount`, `BankAccountStatus` with `BankAccountStatuses.Transitions`, `IBankAccountManager`, `IBankAccountService`, `IBankRoutingValidator`, `IBankAccountVerifier`, `IBankAccountObserver`, `BankingPermissions`, feature id), `CrestApps.OrchardCore.Banking.Core` (store and index, manager, the rules service, the catalog handler, a per-request change tracker, the United States routing check) and the module `CrestApps.OrchardCore.Banking` (feature category Finance, depends on CrestApps Resources for the owner picker).
- The owner is a `PartyRef`; an empty owner is one of the business's own accounts.
- Added to the owner Parties (reuse ladder step 3): `IUserPartySource`, `AddUserPartySource` and `UserParties.GetAsync`, the parties a signed-in user is. Staff implements it (`StaffUserPartySource`), so **My Bank Accounts** shows a person the accounts their staff record owns without Banking naming Staff.
- Numbers and status are written only through `IBankAccountService` (`ApplyNumbers`, `TrySetStatus`/`ChangeStatusAsync`, `StartVerificationAsync`, `RevealAccountNumberAsync`). A new routing or account number takes the account back to Unverified; Closed is final. The catalog handler tells the observers each save's changes (from the change tracker), after the save.
- Deviation: an account may be stored without numbers, as an import without sensitive data writes it; it can be neither checked nor paid until someone enters them. The editors require the numbers.
- Deviation: no built-in verifier yet. The prenote verifier comes with Disbursements (F5); until then an administrator sets the status by hand with a reason. The account page shows one **Check with** button per registered verifier.
- The reveal is a POST that renders the numbers on a page sent with `Cache-Control: no-store`; each reveal, and each number a deployment with **Include the account numbers** writes, is an observer `NumberRevealed` change that the Audit Trail records. Audit and workflow data never hold the number (the masked number only).
- Own accounts are closed, never deleted. An administrator can delete an account; a guard against deleting an account a payment uses waits for the first consumer (F5).
- The deployment step's **Include the account numbers** can be turned on, and takes effect, only for a user with `RevealBankAccountNumbers`; the recipe step protects imported numbers with the importing site's keys, and keeps a status only when the numbers come with it.
- Answer to section 11: `PublicApiApprovalTests` governs only the Accounts, Contact Center, Telephony and Omnichannel families, so the new Abstractions projects are not covered. Left as it is.
- Tests (42, all pass): the ABA check and account number forms, the rules (protection and hint, refusal of wrong numbers, back to unverified on a changed number, same number keeps the status, closed accounts, the status table, observers on create and change, the audited reveal, verification), recipe and deployment round trips with and without sensitive data (and for a user who may not reveal), and the data set columns. The Staff tests and the architecture guards (`RecipeSchemaCoverage`, `WorkflowActivityViews`, `DeploymentStepSerialization`, `MigrationAdditiveOnlyGuard`, `SiteSettingsGroupPermissionRegistration`, `UnboundedCollectionLoad`, `FileSizeRatchet`, layering, feature-id, project reference, editor root view and others) pass; the vitest view lint passes.
- Browser tested on a fresh site (`App_Data_Payroll`, `https://localhost:5761`, Blank recipe, `admin`): add with a wrong routing number (refused with one message) and a right one, the list in List and Grid layouts and in Buttons and Menu actions, edit and verify with a reason, **Show the full number** from the action bar's menu, the Audit Trail entries (added, status changed, number shown), My Bank Accounts (add, close from the row menu, only one's own accounts listed), delete from the admin list, the edit form at phone width, the owner search endpoint, and a user without the permissions refused (403) on every page and on the search. No console errors.
- Still owed: the deployment step's editor and the Bank Account Changed event in the workflow editor were not opened in the browser (unit tested; to check in the end-of-group UI pass).

### F4 as built

- Projects: `CrestApps.OrchardCore.Checks.Abstractions` (`CheckLayout`, `CheckRecord`, `CheckStatuses.Transitions`, `ICheckManager`, `ICheckService`, `ICheckPrinter`, `ICheckVoucherSource`, `ICheckObserver`, `ICheckMicrFormat`, `IAmountInWords`, `ICheckFileStore`, `MicrLine`, `MicrSymbols`, permissions), `CrestApps.OrchardCore.Checks.Core` (register store and index, `CheckService`, `CheckPrinter` on `Pdf.Core`, `UnitedStatesCheckMicrFormat`, `EnglishAmountInWords`, `CheckFileStore` on `EncryptedFileStore`, `CheckLayoutDefaults`), module `CrestApps.OrchardCore.Checks` (Finance, depends on Banking).
- Layouts are a named catalog (a document), like Taxation's types: there are few of them. The register is a YesSql collection with `CheckRecordIndex`.
- Numbers come from `INumberSequence`, series `Checks:{bankAccountId}`, first number 1001, seeded from the highest number in the register; **Next numbers** (`SetNextNumberAsync`) refuses a number a check already has. Numbers are given at issue, not at print: a spoiled sheet is reissued (void and new number in one step).
- MICR: no font is downloaded or shipped. The site uploads a TrueType font (stored encrypted in the tenant file store, never the media library), the layout says which four characters draw the symbols (`ABCD` by default), and printing with the check line on and no font refuses with a clear message; the layout list flags it. Positions follow the plan's reading of ANSI X9.100-160 (amount 1 to 12 blank, on-us symbol at 14 with the account ending at 15, transit symbols at 43 and 33, 44 blank, auxiliary on-us from 45 with the check number on six digits; personal format puts the check number after the account number), counted from 5/16 inch from the right edge, baseline 1/4 inch above the bottom. Tested position by position with Arial standing in for the font.
- Amount in words: `IAmountInWords` per language, English written here (no package), tested.
- Deviation: printing has no separate "confirm what printed" step. A print returns one PDF and records the checks as printed in a print run (`PrintRunId`); a spoiled sheet is reissued from the check's menu, which voids it as Reissued and writes the next number. Printing is the register's bulk action **Print with {layout}**, one per layout.
- Deviation: the positive pay recipe (`ChecksPositivePay`) ships a draft CSV pipeline over the Check Register data set (checks changed since the last successful run, through **Read only new rows** on `ModifiedUtc`). The data set carries the bank account's id and name and never the account number, so a bank that wants its account number or a fixed-width layout needs an `IDataFileFormat` (open item, below).
- The file store passes no Azure configuration section (a section the site has not set logs an error at every start); an Azure Blob option for the layout files is a separate `Checks.Azure` module if one is wanted (open item).
- Also changed: `PartyRef.IsEmpty` is now `[JsonIgnore]`: the computed property was stored on every record holding a party (regression test `PartyRefJsonTests`).
- Bugs found in the browser and fixed with regression tests: `INumberSequence` was not registered (`ChecksRegistrationTests`); the default signature caption printed inside the check line's clear band (`CheckLayoutDefaultsTests`); the middle dot in the voucher printed as "?" in the standard font; the register toolbar overflowed a phone screen (print moved to the bulk actions, **Next numbers** to the action bar).
- Tests (39, all pass): amount in words, the MICR line position by position (business and personal), the clear band, numbering per account, next number, void keeps the number, reissue links and numbers, final statuses refuse moves, issue refusals, printing (missing font message, sheet forms, the module voucher, MICR glyph positions in the PDF and the audited reveal), the layouts deployment without file flags, the register import as history, and that the positive pay recipe reads only fields the data set has. The Banking, Parties and architecture guard tests pass; the vitest view lint and the manuals check pass.
- Browser tested (built-in pane, test site): add a layout (blank stock, payer, second signature above 5000, check line on), the list flags the missing font, upload a font (encrypted on disk), write a check (1001), print it to PDF and read it back (payer, amount, words, address, memo, check line at its positions, two vouchers), reissue (1002, 1003 linked), void, the status filter, next numbers (1002 refused, 20001 accepted), print through the bulk action, a check above 5000 with the second signature line, the register at phone width with no horizontal scroll, and a user without the permissions refused on every page. The Playwright browser was held by another session, so confirmation dialogs were bypassed by posting the row actions directly; the Grid layout of the register was not screenshotted.
- Open items: a MICR E-13B font with a license we can ship; a bank's test of a printed check and the exact positions against the published standard; a bank-specific positive pay layout (`IDataFileFormat`) when a bank needs one; Azure Blob storage for layout files.
- Added after review by the owner: a visual **Check designer** on the layout editor. It draws the check at its real proportions with sample values, fields are dragged on the check (and resized by their right edge), the chosen field is edited in a bar above the check or moved with the arrow keys, the check line's clear band is shaded and a field entering it turns red, and changes to the paper, payer, check line and calibration show at once. It is a module script and stylesheet (`check-layout-designer`, Vite asset group, resource manifest) that reads and writes the form's own field inputs, kept under **Exact positions**, so the post is unchanged and the page works without the script. Tested with vitest (geometry: clamping, moving, the clear band, check line positions) and in the browser (drag, panel, arrow keys, live calibration and paper, save and reload, phone width with no horizontal scroll).

### Review fixes for Banking and Checks

The review of F3 and F4 found 13 items; all are fixed in one commit, test-first. Where the fix differs from what the review proposed, the reason is given.

1. **Checks of one source were visible to everyone with View check register.** Checks.Abstractions gains `ICheckSourceAccess` (one per source kind, `AddCheckSourceAccess<T>()`; returns all, none or some access scopes for a person and a level, `View` or `Manage`) and `ICheckAccessService`, which applies them in one place. `CheckRecord` gains `AccessScope` (copied from `CheckIssueRequest`, kept on reissue, dropped when there is no source kind); `CheckRecordIndex` gains `AccessKey` (source kind and scope). The register page query moved from the controller into `ICheckRecordStore.ListPageAsync(CheckRegisterQuery)`, which runs the filter as a condition on the index (`CheckAccessExpressions`); the Check Register data set applies the same condition in `RestrictAsync`; the register deployment step reads through the same store method; print, reprint, void, stop, cleared and reissue answer not found for a check the person may not act on. Checks written by hand and source kinds without an access stay visible to anyone the permissions allow. Payroll implements `ICheckSourceAccess` for paychecks through `IPayrollScopeService` (P7/P8). Tests: register on SQLite (three people, three outcomes), data set on SQLite, export, and not found on every action.
2. **UTC dates on checks.** `ReissueAsync` dates the new check with the site's local day (`ILocalClock`), **Mark cleared** without a date records the local day, and the **Write a check** editor now starts on the local day (it was empty). Tests at 03:30 UTC with the site five hours behind UTC (the review suggested 23:30 UTC; that hour is the same day in United States zones, so it would not tell the two apart).
3. **Deployment sources skipped their own permission.** Run from a request, the bank accounts step needs `ManageBankAccounts` and the check register step `ViewCheckRegister`: without it the plan leaves the block out and shows a warning. Their editors refuse to add the step for a user without the permission (Orchard lists every step type in the add dialog, so the step can't be hidden; it can't be saved). A plan run without a request, such as a remote deployment, keeps working. The register export honors item 1.
4. **A bad account number crashed the print run.** The printer lays out the check line of every check before drawing anything; a number the line can't hold returns a print error naming the bank account. Test.
5. **One reveal per check.** The account number is read once per bank account per print run; the reveal's reason names the check range and the print run id, which the controller now creates before printing (`ICheckPrinter.PrintAsync` takes it). Test.
6. **Silent reprints.** The bulk print refuses a printed check. **Reprint** in the check's menu opens a page (layout and a required reason, with a warning), prints the check and records `CheckChangeKind.Reprinted` (`ICheckService.MarkReprintedAsync`), which reaches the Check Printed workflow event (`Change` = `Reprinted`). Banking's Audit Trail is not involved: checks have no Audit Trail events of their own yet; the reveal of the account number for the reprint is recorded there. Tests.
7. (a) **Mark Stale Checks** workflow task (`AgeInDays`, default 180; writes the count to the output `StaleChecks`) with views and schema, over `ICheckService.MarkStaleChecksAsync`, which reads printed checks dated before the local cutoff in batches (`ICheckRecordStore.ListPrintedBeforeAsync`). (b) `MarkClearedAsync` sets `ClearedOn` only after the move is allowed. (c) A historical check is refused only for print, reprint and reissue; it can clear, be voided, stopped or go stale. (d) `Reissued` → `Cleared` is allowed, and the register flags such a check **Paid after reissue**. Tests for each.
8. **Positive pay read the whole register.** `CheckRecordIndex.ModifiedUtc` is indexed (additive migration `UpdateFrom1Async`: two nullable columns, a backfill of `ModifiedUtc` from `IssuedUtc`, two indexes), stamped on create as well as update, and is the data set's date column, so a date filter on **Changed** narrows the read. Tests on SQLite, including the condition **Read only new rows** passes on a second run (only the changed check comes back). In the browser the recipe ran, the pipeline published, and the first run **failed** on the test site's SQLite database: `Error parsing column 7 (Amount=6200 - Int64)`. Cause, not from this slice: `ReportIndexPages` pages with `QueryIndex` (`SELECT *` from the index), and SQLite stores a whole decimal as INTEGER and a fractional one as REAL, so Dapper's typed reader fails when one page holds both. It affects every indexed data set with a decimal index column on SQLite (Transactions, Subscriptions, Orders gift cards, Opportunities), never SQL Server, whose columns are typed. The F4 browser test never ran the pipeline, so it was missed. Left as an open item for the Reports owner (proposed as its own task); the second run is owed once it is fixed.
9. **A bank account in use could be deleted.** Banking.Abstractions gains `IBankAccountUsage` (`AddBankAccountUsage<T>()`); Checks registers `CheckBankAccountUsage`. Deviation: the review asked for the check in the handler's deleting step, but the catalog manager logs handler exceptions instead of throwing them, so a handler can't stop a delete. The check is in the store (`BankAccountStore.DeletingAsync`, which throws with the reasons), so no path deletes an account in use, and the admin page asks first (`CanDeleteAsync`, through `IBankAccountService.GetUsagesAsync`) and says to close it instead. Disbursements registers its own usage in F5. Tests.
10. **The admin editor accepted any owner.** The owner must be of a kind `IPartyDirectory` lists and must exist (it has a name). Tests: unknown kind, missing party, malformed value, valid owner.
11. **Missing security tests.** Added `BankingAuthorizationTests` (My Bank Accounts: someone else's account is not found for edit, edit post and close; without the permission, forbidden; a closed account is not changed; the Own editor never saves an owner the person is not, refuses a new account for someone else and ignores a posted status; the admin owner checks; the export permission) and `ChecksAuthorizationTests` (register, export, not found on every action, forbidden without Manage checks, printed checks refused by the bulk print). The data set and the register query are covered on SQLite in `CheckRegisterStoreTests`. The workflow task runs without a user, as workflow tasks do; its only check is the age.
12. **A reveal with the Audit Trail off left no record.** `RevealAccountNumberAsync` always writes a log entry (account id, who, why; never the number). The docs say to keep the Audit Trail on. Test.
13. Small: changes carry `ActorId` and the Audit Trail entry gets the user id; the owner search endpoint is mapped under the site's admin prefix (`AdminOptions.AdminUrlPrefix`) with a route name the view builds its URL from (the user picker endpoint still hard-codes `Admin/`; left for its owner); the reasons "Exported in a deployment plan" and the print run reveal are localized; `IAmountInWords.Write` takes the currency and the English writer writes its name ("... and 00/100 Dollars").

Rules A to E are carried into F5 and later slices: Disbursements gets the source access seam, deployment permission checks, a bank account usage, local dates and authorization tests from the start.

- Also fixed: the full Architecture run found the static-state guard failing on the status tables of Banking and Checks since F3/F4 (`public static TransitionTable<T> Transitions { get; } = new(...)` reads as a static collection to the line scan); they are now `private static readonly` fields behind the property, as the Contact Center lifecycles do.
- Open items for Mike: the positive pay pipeline's second run (owed until the SQLite paging fix is merged; it stays in its own branch, `ma/report-index-pages-sqlite-decimals`, commit a8600b3f6, and is not cherry-picked here); run the **Checks: positive pay file** pipeline twice on a SQLite site after that merge and confirm the second file holds only the changed checks.
- Tests: Banking, Checks and Parties 138, all pass. Architecture 308: 307 passed and the static-state guard failed as above; after the fix its class passes. `npx vitest run tests/js/lint` (21) passes.
- Browser (test site, admin): the register lists through the new store query; the bulk print refuses printed check 20001 with the reprint hint; **Reprint** asks for a reason and returns the PDF (`no-store`); the Audit Trail entry of the account number read carries the user id, and the site log names the check range and the print run; **Mark cleared** on reissued check 1001 shows **Paid after reissue**; **Write a check** starts on today's local date; deleting the bank account checks are drawn on is refused with the reason; the owner picker reads its URL from the route; **Mark Stale Checks** is in the workflow toolbox under Checks; the positive pay recipe ran and published (see item 8 for the run).

### F5 as built

- Disbursements as three projects (`Disbursements.Abstractions`, `.Core`, the module) with two features: **Disbursements** (depends on Banking) and **Disbursements - Bank File (ACH)** (`Disbursements.Nacha`).
- `Disbursement` and `DisbursementBatch` are YesSql documents in the `Disbursements` collection with `DisbursementIndex` and `DisbursementBatchIndex`. The index keeps no amount, on purpose: the SQLite paging bug found in the review fixes cannot reach this data set (a store test reads 6200 and 1234.56 back through the data set).
- `IDisbursementService`: idempotent `CreateAsync` (the request's key, or `SourceKind:SourceId:Method:Destination`), the bank account checks (business source with numbers; a verified destination for a bank file, an unverified one allowed for a prenote), batches per provider, source account and currency (the order given is the order in the file), release through `IDisbursementProvider`, cancel, settle, `SettleReleasedBatchesAsync`, returns, and notices of change C01, C02, C03, C05, C06 and C07 (the account is corrected through `IBankAccountService.ApplyNumbers`, so it is unverified again, as Banking's rule says).
- Providers: **Bank file (ACH)** (`NachaFileWriter`: PPD and CCD batches, prenote codes 23 and 33, optional offset debit 27/37, entry hash, totals, trace numbers, blocking to ten, file id modifier per business day from `ILocalClock`; United States dollars only; one account number read per account per file), **Printed check** (when Checks is on: one check per disbursement for the same source and access scope; `CheckDisbursementObserver` settles on clear, cancels on void or stop, follows a reissue), **Paid outside the system** (settles on release).
- Bank file settings (`NachaOriginSettings`) live on the business's `BankAccount` properties, edited in a **Bank file (ACH)** card (`DisplayDriver<BankAccount>`, business accounts only, never in the person's own group), so they travel with the account in its recipe.
- Prenotes: `NachaPrenoteVerifier` (an `IBankAccountVerifier`; resolves the disbursement services lazily because the bank account service depends on the verifiers) and `NachaPrenoteObserver` (settled verifies, returned fails).
- `NachaReturnFileReader` reads return (99) and notice of change (98) addenda; **Disbursement Batches > Import a return file** applies them.
- Rules A to E built in:
  - A, source access: the review's Checks seam had its second consumer, so it moved to a shared building block (`ISourceAccess`, `AddSourceAccess<T>()`, `ISourceAccessService`, `SourceAccessFilter` in `CrestApps.OrchardCore.Abstractions`; `SourceAccessService` and `AddSourceAccessService()` in `CrestApps.OrchardCore.Core`; `SourceAccessExpressions.Restrict<TIndex>` in `YesSql.Core`). Checks now uses it (its `ICheckSourceAccess`, `ICheckAccessService`, `CheckAccessFilter`, `CheckSourceScope`, `CheckAccessLevel`, `CheckAccessService` and `CheckAccessExpressions` are gone). Payroll registers one `ISourceAccess` per source kind and it applies to checks and deposits alike. Disbursements applies it to the list, the data set, the export, every action by id and the batches (`DisbursementBatch.Sources` keeps each disbursement's kind and scope; a batch is listed and acted on only when all are allowed; the pipeline step skips a batch the publisher may not act on).
  - B: the export needs `ViewDisbursements` in a request (nothing written and a warning without it; the step can't be saved without it). Run without a signed-in person it fails closed and writes only what no source restricts. The planning session's follow-up on Checks is folded in: `CheckRegisterDeploymentSource` now also fails closed with no user (`GetFilterAsync(null, ...)`), with a test.
  - C: `DisbursementBankAccountUsage` keeps an account money was sent from or to from being deleted.
  - D: dates people read come from `ILocalClock` (the default arrival date, the file's date and its day for the modifier, release times on the batch list).
  - E: authorization tests for both controllers, the export, the data set (SQLite) and the pipeline step.
- Data set **Disbursements** (date column the indexed `ModifiedUtc`, restricted by source access, no account number). Pipeline step **Read a disbursement batch file** (`ReadDisbursementBatchFile`, a files source registered with `DataPipelineStepTypeOptions` from `OrchardCore.DataPipelines.Abstractions` only; no editor): released Nacha batches since the watermark, publisher needs `ManageDisbursements`, watermark saved only when the run succeeds.
- Recipe step `Disbursements` (history, never batched; duplicates by key skipped) and the opt-in deployment step, both with schemas. Workflow events Disbursement Submitted, Settled, Returned, Failed and the **Settle Disbursement Batches** task (age in days, output `SettledDisbursements`), with views and schemas.
- Admin: **Banking > Disbursements** (`<admin-list>`, bulk action **Batch to send by {provider}** with the arrival date in the action bar, **Record a disbursement**, cancel) and **Banking > Disbursement Batches** (release, download with `no-store`, mark paid, cancel, import a return file).
- Known limit: the manual form lists verified payee accounts in a select (`GetAllAsync` on bank accounts). Payroll pays through code, not this form; a searchable picker can replace it when lists grow.
- Tests: `NachaFileTests` (record by record: lengths, header, entry, batch and file controls, PPD/CCD, prenote codes, offset, modifier, refusals; return and change file; each correction code), `DisbursementServiceTests` (idempotency, account rules, batching, release by file, check and manual, cancel, settle by age, returns, notices of change, prenote verified and failed, check cleared/voided/reissued, delete refused), `DisbursementsAuthorizationTests`, `DisbursementStoreTests` (SQLite), `ReadDisbursementBatchFileStepTests`. Disbursements, Banking, Checks and Parties: 196 pass. Quick architecture guards (21 classes, 192 tests) pass.
- Browser (test site, admin): both features enabled; the **Bank file (ACH)** card saved on Payroll funding; a payee account created for a user owner (owner check passed) and verified; **Record a disbursement** (ACH, 1250.50) appears Pending with tomorrow's local date as the default arrival; **Batch to send by Bank file (ACH)** made a batch; **Release** wrote a valid file (download with `no-store`: header, PPD batch header with effective date 261116, entry 22 with routing 011000015 and amount 0000125050, batch control and file control totals, padded to ten records with nines; the release time shows in local time); **Import a return file** with an R02 addenda marked it Returned with "Account closed"; **Check with Test entry (prenote)** on an unverified account put it in Verification sent and recorded a zero-amount test entry, which, batched and released (code 23, file modifier B for the second file of the day) and marked paid, verified the account; deleting a bank account money was sent to was refused with the reason; **Read a disbursement batch file** is in the pipeline designer under Sources, and a pipeline of that step alone read 2 files on its first run and 0 on its second (the watermark holds); the four events and the task are in the workflow toolbox.
- Bug found in the browser and fixed with a regression test (`ReleaseBatchAsync_TwoFilesFromOneBank_NeverReuseATraceNumber`): the second file of the day numbered its entries from 1 again, so its trace number equalled the first file's and a return could have matched the wrong disbursement. Trace sequences now come from an `INumberSequence` series per originating bank (`Disbursements:Trace:{first eight digits}`), seven digits, starting over after 9999999.
- Not done in the browser: delivery to an SFTP server. No test SFTP server is available here and nothing may be downloaded to run one; the step's files go to Orchard's own SFTP step unchanged. Open item.
- Open items: a test SFTP delivery of a bank file; a bank's acceptance test of a file (the format follows the published Nacha layout and was checked record by record); current Nacha prenote and change-notice timing rules (the settle task defaults to three days).

### F6 as built

- Approvals as three projects (`Approvals.Abstractions`, `.Core`, the module), one feature **Approvals** (category Workflows). Staff, Notifications and Workflows are used when they are on, never required.
- `ApprovalPolicy` is a named catalog entry (rules `Permission`, `Role`, `Manager`, `Staff`; 1 to 10 required approvals; the two built-in rules, both on by default; optional due hours). `ApprovalRequest` is a YesSql document in the `Approvals` collection with `ApprovalRequestIndex` (no amount, `SourceKind` = subject kind and `AccessKey` for the source access seam).
- `IApprovalService`: `RequestAsync` (asking again while a request about the subject is pending under the policy returns that request), `GetEligibilityAsync`, `DecideAsync` (a person decides once; one rejection rejects; rejecting needs a note), `CancelAsync`, `GetApproverUserIdsAsync`. `IApprovalSubjectHandler` is called when a request is approved, rejected or withdrawn, in the same scope; `IApprovalObserver` after every change. `IApproverDirectory` answers the rules: permission through the authorization service, role through the role claim, manager through `IStaffHierarchy`, named staff through `IStaffDirectory`; for notices, the users of the roles that grant the permission plus administrators.
- Notifications: types `Approval` (**Approvals to decide**) and `ApprovalDecided` (**Approval decisions**) through `AddNotificationType`; `ApprovalNotificationObserver` sends approvers a notice that opens the decide page, deletes the notices of a request once it is closed (`INotificationManager.DeleteAsync` by type and reference id) and tells the requester the decision. Workforce's notifier is not used.
- Workflows: Approval Requested, Approved and Rejected events (correlation id = request id; input includes `ApproverUserNames` for Notify User Task); Request Approval, Approve Request, Reject Request and Get Approval Request tasks with views and recipe schemas. A workflow decides as the person it runs for, under the same policy; one that runs for nobody gets `NotAllowed`. The **Automation: remind approvers** recipe (`ApprovalsReminder`) adds a disabled workflow: Approval Requested, Timer Event (`0 9 * * 1-5`, local time), Get Approval Request, Notify User Task while pending, back to the timer. Approvals has no timer or background task of its own.
- Rules A to E: A, requests carry subject kind and access scope and go through the shared source access seam (list, decide page, every action by id, export, data set, workflow tasks; View to see, Manage to decide or withdraw). B, the **Approval requests** deployment step needs `ViewApprovalRequests` in a request and fails closed without a user. C, a request points only at a policy by id and keeps the policy title and required approvals it was made with, so a policy can be deleted without breaking history (eligibility then says the policy no longer exists). D, every time on the pages comes from `ILocalClock`. E, authorization tests for the controller, the export, the data set (SQLite) and the workflow tasks.
- Data: **Approvals** data source with the **Approval requests** data set (`ModifiedUtc` incremental, hours to decide). Recipe steps `ApprovalPolicies` (named catalog) and `ApprovalRequests` (history only; a pending one is imported withdrawn; no notice, no workflow) and both deployment steps, with schemas.
- Admin: top-level **Approvals** menu: **Requests** (`<admin-list>`, status filter defaulting to waiting, "Yours to decide" badge) and **Policies** (`<admin-list>`, edit page with `action-bar-bottom`). The decide page has Approve and Reject in the bottom bar and Withdraw as a secondary action (the requester's own request, or anyone's with `ManageApprovalRequests`).
- Tests: `ApprovalServiceTests` (11), `ApprovalsAuthorizationTests` (15), `ApproverDirectoryTests` (5, incl. the notification observer), `ApprovalRequestStoreTests` (SQLite, 4). Approvals 34, Banking 56, Checks 73, Disbursements 57 pass; the quick architecture guards (20 classes, 274 tests) pass after the Notifications feature id moved to `OrchardCoreFeatureIds.Notifications`; JS lint passes.
- Browser (test site, admin): feature enabled; a policy saved (role Administrator, due in 24 hours; a non-number due time was refused with its message) and listed; the reminder recipe ran and its workflow opened in the designer with no issues and the Approvals category (7 activities); a test workflow (Timer Event then Request Approval) asked twice and made one request (the pending one was reused); the request showed "Yours to decide", and the notice in the notification center (type **Approvals to decide**) opened the decide page; reject without a note was refused; approve recorded it, and the approver's notice was removed; a second request was withdrawn from the secondary action. The test workflow was deleted afterwards.
- Fixed in the browser: a request made by a workflow showed an empty requester (now "A workflow"), and withdrawing without a reason stored "Withdrawn" as the reason.
- Not covered in the browser: a second approver signing in (no second user on the test site; the two-approver and requester rules are unit tested), adding the deployment steps to a plan (none exists on the site; serialization guard and tests cover them).

### F7 as built

- **Team address.** `Team.Address` (the Addresses building block's `Address`; `Teams.Abstractions` now references `Addresses.Abstractions`). Teams adds an **Address** card to the team editor (`TeamAddressDisplayDriver`, in a startup that needs the Addresses feature) built on `IAddressEditorService` and the `AddressEditor` shape, laid out for the country; optional, and an address that is started is validated for its country. `TeamRecipeImport` copies it and the `Teams` recipe schema describes it, so the `Teams` recipe and deployment steps carry it.
- **Workforce's copy moved.** `WorkforceLocation` keeps only the geofence point (latitude, longitude); its one-line `Address` is gone. The **Workforce** card shows the team's address read only, and the site coordinates are geocoded from it when empty (`WorkforceGeocodeTargets.NeedsGeocoding(team)`, `AddressText`); a point set by hand wins over a later geocode. An existing site's one-line address becomes the address's first line three ways: `WorkforceMigrations.UpdateFrom3` (a deferred task through `ITeamManager`, skipped during setup, so no second connection opens during activation), the very old team move (`WorkforceTeamsMove`), and the `Workforce` recipe step for recipes written before.
- **Compensation.** `StaffPayRates` gains `PayBasis` (`Hourly`, `Salary`, `PerUnit`), `Currency` and dated `Salaries` (`StaffSalary`, `SalaryOn(date)` falling back to the old undated `AnnualSalary`, which the editor turns into a row to date). The **Pay** card is rewritten: amounts are text read and written with the invariant culture (a decimal comma is refused, half rounds away from zero at four places), a three-letter currency, a basis, rate and salary rows, and two rows starting the same day are refused.
- **Pay in the history.** `StaffSavedState.Pay` is the pay on one line (`PaySummary`), so every change writes a `StaffHistoryFields.Pay` entry. Its values are shown only to people with `ManageStaffPay`, and the **Staff history** data set leaves pay rows out for other readers (decision: the history is readable with View staff, pay is not).
- **One editor.** `WorkerTimeClockController.AddRate` and `RemoveRate` are removed with their form; the worker's time clock page lists rates read only (still under `ViewPayRates`) and links to the staff member, whose Pay card under `ManageStaffPay` is the only editor.
- Tests: `StaffCompensationTests` (dated salary, pay summary, invariant parsing and refusals, a pay change in the history through the real handler, the address move with and without an existing address), and the Workforce team, move and geocoding tests updated to the team address. Staff 37, Workforce 214, Geo 23 pass; the quick architecture guards (20 classes, 274 tests) and the JS lint pass.
- Browser (test site, admin): Addresses, Teams, Staff Compensation, Workforce and Time Clock turned on; a new site team showed the Team, Address and Workforce cards; an address with only a street was refused with the United States rules (city, state, ZIP); the complete one saved and came back, and the Workforce card showed it on one line; a staff member's Pay card refused `25,50` (keeping what was typed) and saved `25.50`, `USD` (typed lowercase), cost `38.20` and a dated salary; a raise from July showed as today's rate and as a **Pay** entry in the employment history with both values; the worker's time clock page listed both rates with no form and the link to the staff member.
- Not covered in the browser: geocoding (outbound calls are blocked on the test site; the geocoding test covers it) and the migration on a site with old one-line addresses (none on this site; `TeamAddressMove` tests cover it). Not unit tested: the Staff history data set leaving out pay rows (one `Where` on the index in `RestrictAsync`).

### F8 as built

- `IVendorPayoutProvider` is now a Disbursements provider (`IVendorPayoutProvider : IDisbursementProvider`, a marker for providers that can pay a vendor); its own `PayAsync` is gone. **Marketplace payouts with Stripe Connect** depends on the Disbursements feature (the Marketplace project references the Disbursements module only to carry that dependency; code uses `Disbursements.Abstractions`).
- `StripeConnectPayoutProvider` (name and method `StripeConnect`, **Stripe Connect transfer**) sends each disbursement of a batch as a Transfer to the vendor's account, funded by the order's charge as before; the transfer's idempotency key is the disbursement's. Through and it settles the disbursement with the transfer id as its trace; refused and it fails it with Stripe's reason. One instance answers as both services.
- `MarketplaceService.PayOutAsync` records each (vendor, order) payout as a disbursement (payee kind `MarketplaceVendor`, source kind `MarketplaceOrder` with the order id, access scope the vendor, memo the order number, arrival date today in the site's zone) with the payout's existing idempotency key, sends it in a batch of its own and releases it at once. `VendorPayout` keeps `DisbursementId`, and the transfer from the disbursement's trace. A retry after the transfer went through but the payout's save failed gets the same settled disbursement back and sends nothing; a refused payout still bumps `RefusedPayouts`, so the next try is a new key and a new disbursement. Without Disbursements nothing is paid and the reason says to turn it on; `CanPayOut` is false.
- Source access: payouts are disbursements of an unrestricted source kind, visible to people with View disbursements; finance staff seeing vendor payouts is intended. A Marketplace `ISourceAccess` can narrow it later.
- Tests: `MarketplaceServiceTests` now run over the real `DisbursementService` (the Disbursements test context takes extra providers) with a fake vendor payout provider: a payout is a settled disbursement to the vendor for the order; a retry after a failed save sends once; a refusal fails its disbursement and the retry uses a new key; the provider named in the settings is used; several providers and none named send nothing; no Disbursements sends nothing and says why. Marketplace 27 and Disbursements 57 pass.
- Browser (test site, admin): the Stripe Connect feature lists Disbursements among its dependencies and turned on; **Record a disbursement** lists **Stripe Connect transfer**; a disbursement by it to a payee that is not a vendor was batched and released and came back **Failed** with "The vendor has no Stripe account." (the provider's refusal path, with no call to Stripe).
- Owed (decided earlier): a Stripe test-mode payout and refusal against a real Stripe account, since outbound calls are blocked on the test site and no Stripe test keys are configured here. Open item for Mike.
- Seen, not changed: on the narrow browser pane the disbursement list's bulk Actions button is hidden after ticking a row; the form posts fine. Worth a look in the admin list layout at small widths.

### End-of-group checks, building blocks (2026-11-14)

- Release build of the tests project (every module) with `-warnaserror`: passes after one fix, the F2 PDF test read PdfPig's obsolete `IPdfImage.Bounds` (now `BoundingBox`).
- Full Architecture namespace (Release): 311 pass. Staff 37, Workforce 214, Marketplace 27, Checkout 460, Reports 74 and 5: pass. JS view lint: 21 pass. Manuals check: 161 user and 190 technical pages pass.
- Docs build: passes after two fixes (MDX read `{layout}` and `{provider}` in the Checks and Disbursements permission tables as code). The build needs `SWC_NATIVE_BINDING_CACHE` set to a folder under the profile in this sandbox, a known environment quirk.
- Mike, 2026-11-14: one pull request for the whole plan, a commit per slice (section 8 and decision 7 updated); the planning session pushes and opens it. The SQLite paging fix stays in its own branch and is not cherry-picked.

### Review fixes for Disbursements and Approvals (2026-11-14)

The planning session's review of c38c82027 and e85af38f3, all thirteen items, test-first.

- 1, one disbursement per key: `CreateAsync` holds an Orchard `IDistributedLock` on the key from the read to the commit (`IDisbursementCommitter`, the session's `SaveChangesAsync` inside the lock, as Checkout's tender ledger does). The key is the attempt: the same key with another payee, amount, currency, method or destination is refused; once its disbursement failed, was cancelled, returned or reversed, a new attempt needs a new key and the error says so. F8's retry already follows it (a refusal bumps the key) and P8 will. Tests: two concurrent calls create once; another amount or payee is refused; a retry after cancel says a new key is needed.
- 2, one release per batch: a lock per batch, the batch read again inside it, and a new `Releasing` status saved and committed before the provider is called; the provider gets the batch (its id, or each disbursement's key, is the idempotency key, documented on `IDisbursementProvider`). A refusal reopens the batch; a throw leaves it `Releasing`, which a release refuses until `ReopenReleasingBatchAsync` (**Reopen after checking** on the batch list) after someone checked with the provider. Tests: two concurrent releases call the provider once; a provider that throws leaves the batch Releasing, the next release is refused, and only a reopen sends it again.
- 3, returns page: returns and notices of change apply only to disbursements the person's source access (Manage) allows; the others read as not found. A notice of change also needs `ManageBankAccounts`; without it it is reported for someone who may. The account still goes back to unverified and its observers are told (tested with the Banking observer).
- 4, approval rules frozen: a request copies the policy's approvers and the two built-in rules when it is made (as it did the count) and is decided by its own copy; a request made before falls back to the policy. Policy changes go to the Audit Trail (**Approval policy added, changed, removed**, with the rules). Test: loosening the policy while a request waits does not let the requester approve it. Two earlier tests that loosened the live policy now make a second request under the looser one.
- 5, subject version: `ApprovalSubmission.SubjectVersion` and `ApprovalRequest.SubjectVersion`; a pending request about another version is withdrawn ("Replaced: the record changed after approval was asked", so the subject handler hears it) and replaced; the same version reuses it. A lock per subject keeps two requests from both creating.
- 6, concurrent decisions: `DecideAsync` and `CancelAsync` hold a lock per request and work on a fresh read; the caller's copy is brought up to date. Test: two approvers at once both count.
- 7, settling: counted in the site's days from the later of the arrival date and the release, so a batch released early is not marked paid before the money moves. The earlier settle test now waits until the arrival day plus the age.
- 8, bank file records: each download writes the batch id and who (never a number) to the log and, with Audit Trail on, a **Bank file sent** event (`DisbursementFileRecorder`). The pipeline step stamps each delivered batch with when and by which pipeline once the run succeeds (`Run.OnSucceeded`), shown on the batch list ("file delivered ... by ...").
- 9, batch list: a batch holds the disbursements of one source kind and access scope (others are left pending with a problem), the batch index has `SourceKind` and `AccessKey` (migration `UpdateFrom1`, which re-saves existing batches after activation), and the store filters by source access before counting and paging. SQLite test: Pat sees batch A and the unrestricted ones, Lee only the unrestricted, with matching counts.
- 10, cancelling a batch reports its disbursements as `Unbatched`, a new change kind.
- 11, bank file: the company entry description is required (the setting, or the batch's own: `CreateBatchAsync(..., entryDescription)` prints it, such as PAYROLL); an account number over 17 characters is refused, never cut; an arrival date before the site's today is refused when batching and when releasing. Tests for each.
- 12, reason codes: a return stores its code only (`FailureCode`); the text is localized where it shows (`NachaReasonCodes.Describe`). A prenote that settles or is returned passes no English text to the bank account: the return code, or nothing. Deviation: the bank account's status reason shows the bare code (such as R03), since Banking does not know bank file codes.
- 13, policies: saving one whose rules name fewer people today than it needs shows a warning (`IApproverDirectory.CountPeopleAsync`; a manager rule depends on each request, so it is not counted).
- Tests: `DisbursementReviewFixesTests` (13), `ApprovalReviewFixesTests` (4), the SQLite batch filter. Disbursements 72, Approvals 38, Marketplace 27, Banking 56, Checks 73 pass; the quick architecture guards (274) and the JS lint pass.
- Browser (test site): the batch list reads after the migration; a manual disbursement released by two requests at once settled once; downloading a bank file wrote **Bank file sent** to the Audit Trail; raising Expense approval to three approvals with one eligible person saved with the warning and wrote **Approval policy changed** to the Audit Trail.

### P1 as built

- **Pay Setup** (`CrestApps.OrchardCore.Payroll.PaySetup`, category Payroll) in the new `Payroll.Abstractions`, `Payroll.Core` and `CrestApps.OrchardCore.Payroll` projects. `PaySchedule` keeps the old calendar's members and `PeriodOn`/`WeekOf`, and adds every four weeks, quarterly and yearly, a chosen second period start for twice a month (16 by default, as before), the pay date rule (days after the period, or fixed days with 0 for the month end), the shift off a day off (before, after, none; `PayDateFor` takes an optional business day test so a later slice can pass the hours schedule's holidays) and the approval lead days. `HoursScheduleId` is kept on the record for that. `PayCode` keeps the old fields, gains `Class` and an open string `Kind` (`PayCodeKinds`, `AddPayCodeKind`), and drops the window, which Workforce keeps in `Properties` (`WorkforcePayCodeSettings`, its own editor card and check).
- Every record type: catalog stores and managers with handlers (code unique and upper case, ranges checked), driver-based editors with invariant-culture decimals, `<admin-list>` index pages (SummaryAdmin and SummaryAdminGrid), edit pages with `action-bar-bottom` and breadcrumbs, permissions `ManagePaySetup` and `ExportPayrollData`, recipe steps `PaySchedules` and `PayCodes` with schemas, deployment steps with schemas. Index providers and the index schema live in `Payroll.Core` (`PaySetupIndexProviders`, `PaySetupIndexSchema`) so tests and the migration share them.
- Rule C: `IPaySetupUsage` (`AddPaySetupUsage`). The pages list what uses a record and refuse the delete; the stores refuse it too. Workforce's `TimeClockPaySetupUsage` reports policies and timesheets for a schedule, time off types and timesheet lines for a code (lines are read a page of 200 timesheets at a time, stopping at the first hit).
- Workforce on it: Workforce.Abstractions references Payroll.Abstractions, Workforce.Core references Payroll.Core, the Time Clock feature depends on Pay Setup, and the old models, managers, stores, handlers, indexes, controllers, row drivers, views and menu items are gone. The stored property names (`PayPeriodCalendarId`, `PayCodeId`) stay, so nothing that names a record changes. The calculator only takes earning and memo codes for lines of hours. As with Staff and Teams, the Workforce module carries the feature dependency through the manifest and the solution targets; no module project reference was added.
- Migration (`TimeClockMigrations.UpdateFrom2`, `TimeClockPaySetupMove`): reads the old documents as JSON on the migration's connection, maps an old kind number to its name through `PayCodeKinds.LegacyOrder`, moves the window into the settings, sets the class (unpaid to Memo), saves each in the Payroll collection with the same id, replaces a code Pay Setup seeded a moment before with the same code (found through its index table on the migration's connection, so the guard sees no dynamic query), leaves a record already there, then drops the two old index tables and deletes the old documents; a second run moves nothing. A deferred task grants `ManagePaySetup` to roles holding `ManageTimeClockPolicies` (`RolePermissionMigrations`). The two drops and the delete are in the migration contract register.
- Recipe and deployment compatibility: `WorkforceTimeClock` still reads `PayPeriods` and `PayCodes` (with `windowStart`, `windowEnd` and `weekdays` going into the settings). Deviation: the Workforce deployment step keeps exporting the pay schedules, because its policies name them by name and a step can't count on another step running first; it no longer exports pay codes, which the `PayCodes` step owns. `ExportPayroll` and `ViewPayRates` are left for P2, where the export code goes.
- Tests: `PayScheduleTests` (12: every frequency, the default and chosen second period, pay dates after the period, fixed days with month end, weekend before and after, a caller's holiday, fixed days ignored for weekly), `TimeClockPaySetupTests` (4: unique codes and the differential window; usage of a schedule by a policy and a timesheet and of a code by a timesheet line; the move keeps ids, maps kinds, moves the window, replaces seeded codes and changes nothing on a second run; a record already in Pay Setup stays). Workforce 230 (all, after `TimeClockKit` and the calculator and recipe tests moved to the new types), the full Architecture namespace (313) and the JS view lint (21) pass; the manuals check passes (162 and 191 pages).
- Browser (test site with the time clock on): the update moved the site's 8 pay codes into Pay Setup ("Moved 0 pay periods and 8 pay codes" in the log) and the old pages answer 404; **Payroll > Setup > Pay Codes** lists them with class and kind badges; a shift differential saved without hours shows the error and, with 22:00 to 06:00 on Friday and Saturday, saves and reads back; a new every-two-weeks schedule shows its next four periods and pay dates; a time clock policy takes it, and deleting the schedule then says "It is in use and can't be removed: the time clock policy Hourly staff."
