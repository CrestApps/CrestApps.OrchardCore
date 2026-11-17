---
name: crestapps-payroll
description: How to build on the CrestApps Payroll building block (CrestApps.OrchardCore.Payroll) instead of keeping another pay record. Use when a module hands hours or amounts to payroll, needs pay codes or pay schedules, reacts to a pay run or a pay statement, reads payroll data in a report or a pipeline, adds a tax provider or election fields, pays net pay another way, or limits access to some employers. Covers PaySchedule, PayCode, IPayInputSource, Employer, IPayrollScopeService, PayrollPermissionTemplates, EmployeePayProfile, IPayProfileObserver, the pay engine (IPayCalculationStep, and Taxation's IIncomeTaxProvider for taxes), IPayRunService, IPayRunObserver, IPayStatementObserver, IPayStatementPaymentMethod, the Payroll data source, workflow events and tasks, webhook events, notification types and the opt-in pay history step.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Building on Payroll

Payroll owns employers, pay setup (schedules and codes), pay profiles, the pay engine, pay runs and statements, and how net pay is paid. Never keep another pay rate per run, pay period list, pay code list, statement or year-to-date total. Reference `CrestApps.OrchardCore.Payroll.Abstractions` (and `.Core` only from a `.Core` project that must), never the Payroll module, and put the integration in a `[RequireFeatures(PayrollConstants.Features.PaySetup)]` or `Payroll` startup.

## Hand hours and amounts to payroll

Implement `IPayInputSource` (`SourceKind`, `GetInputsAsync(PayInputQuery)`, `MarkConsumedAsync`, `ReleaseAsync`) and register it with `services.AddPayInputSource<T>()`. Inputs carry the staff member, the pay schedule's period, a pay code id, hours or an amount, and an allocation (department and work site teams). A processed run marks your records consumed under `payrun:{runId}`; a void releases them. Never compute pay in your module: hand the hours, payroll prices them.

## Pay setup

- `IPayScheduleManager`, `PaySchedule.PeriodOn(day)`, `WeekOf(day)` and `PayDateFor(periodEnd)` for periods and pay dates.
- `IPayCodeManager` and `services.AddPayCodeKind(...)` for a new kind of pay; module settings ride on `PayCode.Properties`. Keep a record in use with `IPaySetupUsage`.

## Employers and access per employer

`IEmployerManager`; the `Employer` party kind; every payroll permission has a per-employer form (`PayrollPermissionTemplates.For(permission, employerId)`). Ask `IPayrollScopeService.GetScopeAsync(user, permission)` for the employers a user may act on (`PayrollScope.All` or `EmployerIds`) and filter your queries with it; a clerk of one employer never sees another's people.

## Pay profiles

`IPayProfileManager` (one profile per staff member and employer), `IPayItemManager` (standing pay items), `IPayProfileService` (protected tax id with `SetTaxIdAsync` and `RevealTaxIdAsync`, blockers). Add blockers with `IPayProfileBlockerProvider` and election fields for a jurisdiction with `AddElectionFieldSet`. Hear changes with `IPayProfileObserver`.

## The engine and taxes

Retro pay, the tip credit and the re-pricing of earlier weeks read `PayCalculationContext.History` (the person's final statements of the year); a retro line names its statement in `RetroFor`. Run inputs may be take-home amounts (`IsNet`, grossed up) or earned in earlier weeks (`EarnedFrom`, `EarnedTo`).

Add a calculation step with `IPayCalculationStep` (orders in `PayCalculationOrder`). Taxes on income are Taxation's, never Payroll's: a tax provider implements Taxation's `IIncomeTaxProvider` over the neutral `IncomeTaxRequest`, and the built-in one works out `IncomeTaxRule`s over Taxation's tables. A country pack is a Taxation feature: it adds its withholding formulas as an `IIncomeTaxWithholdingMethod` (`services.AddIncomeTaxWithholdingMethod<T>()`; a rule names it as its method and fills `Tables` and `Parameters`), its election forms with `AddElectionFieldSet`, its categories with `AddIncomeTaxCategory`, and ships its rules as dated recipes whose tables and rules carry valid 26-character UniqueIds (the import replaces any other id and the links break). United States Income Tax (`UnitedStatesFormW4WithholdingMethod`) and Canada Income Tax (`CanadaFederal`, `CanadaProvincial`, `QuebecTP1015`) are the examples. A country's returns and slips stay in Payroll as `IPayrollFilingForm`s (United States and Canada Payroll Filings). A rate an agency assigns each employer (a state unemployment rate, Quebec's health services fund) is the registration's dated rate, reaching the rule through `IncomeTaxRequest.Registrations` when the rule has `RateFromRegistration`. A filing data set never carries a full tax id in clear: it encrypts it with `FileTaxIds.Protect` for the agency's file format, which reads it back with `FileTaxIds.Unprotect`.

## Pay runs and statements

`IPayRunService`: `OpenAsync`, `CalculateAsync`, `SubmitAsync` (to Approvals), `ProcessAsync`, `VoidStatementAsync`, `ReissuePaymentAsync`, `ListStatementsAsync`. Statements never change once final; a void is a reversing statement. Hear steps with `IPayRunObserver` (Opened, Calculated, Submitted, Approved, Rejected, Paid, PartiallyPaid, Failed, Canceled) and statements with `IPayStatementObserver` (Issued, Voided). Approvals, Disbursements and the pay input sources commit the session: re-read a record by id after calling them before saving it.

## Liabilities and filings

`ITaxLiabilityService` derives the taxes owed by deposit period from the final statements and `ITaxDepositManager`'s deposits. A country pack adds its forms with `services.AddPayrollFilingForm<T>()` (`IPayrollFilingForm.PrepareAsync(PayrollFilingContext)`); `IPayrollFilingService` prepares, files, amends and closes a year (`Employer.ClosedYears` stops runs and voids). Never keep another liability or filing record.

## Contractors

A contractor profile has nothing withheld except rules of the `ContractorWithholding` tax category, and only when `BackupWithholding` is on or no tax id is on file. Report contractors on their own form (`Form1099Nec` in the United States pack), never on employees' forms.

## Garnishments

`IGarnishmentOrderManager` keeps garnishment orders (a pay profile, the kind, an amount or percent of disposable earnings, limits, a total owed, the payee). `GarnishmentsPayCalculationStep` applies the limits; `GarnishmentStatementObserver` moves what was withheld and pays the payee through Checks or Disbursements. Never withhold a garnishment as a pay item.

## Paying net pay

Implement `IPayStatementPaymentMethod` (`Method`, `PayAsync`, `VoidAsync`) for a `NetPayMethod`; Direct Deposit pays through Disbursements and Paychecks through Checks, with the statement as the check's voucher (`ICheckVoucherSource`). Disbursements and checks of pay use the source kind `PayStatement` with the employer as access scope.

## Inputs from other modules

A module that has hours or amounts to pay implements `IPayInputSource` in its own project (register it in a startup with `[RequireFeatures(PayrollConstants.Features.PaySetup)]`) and stamps its records when a run takes them, like `TimeOffPayoutPayInputSource`, `PosTipsPayInputSource` and `OrderCommissionPayInputSource`. Balances a stub shows come from `IPayStatementBalanceSource`. Commission rates live on Staff compensation (`StaffPayRates.CommissionRate`), never in Payroll.

## AI tools

Payroll AI tools live in the opt-in `CrestApps.OrchardCore.Payroll.AI` feature (pay goes to the AI provider), run as the signed-in person, check the permission of the page they mirror (`ViewOwnPay`, `ViewPayRuns` per employer) and never return a tax id or account number. Compare statements with `PayStatementComparison`.

## Payroll journal

Never write a journal file or keep a ledger in Payroll: `PayrollJournal.EntriesFor` turns statement lines into debits and credits on the accounts of `PayrollJournalSettings` (per kind, or a mapping by pay code or tax code), and the **Journal entries** data set (`ExportPayrollData` scope) feeds a Data Pipeline that groups and delivers them.

## Reading payroll data

The `Payroll` data source has the data sets Pay inputs, Employees, Pay runs, Pay statements, Earnings, Deductions, Taxes, Employer contributions, Payments of net pay and Year-to-date balances, each limited to the employers a user may see. Reports (Workforce & Payroll): Payroll Register, Payroll Summary, Cash Requirement, Deductions and Contributions, Labor Distribution, Year-to-date Earnings. Never read statements to rebuild these.

## Automation, notices and history

- Workflow events: Pay Run Opened, Calculated, Submitted for Approval, Approved, Rejected, Processed, Paid, Failed; Pay Statement Issued, Voided; Pay Profile Changed. Tasks: Open Pay Run, Add Pay Input, Calculate, Submit, Approve, Reject and Process Pay Run. A returned deposit starts Disbursements' Disbursement Returned event; a changed bank account Banking's Bank Account Changed event.
- Webhook events: `payroll.payrun.approved`, `payroll.payrun.processed`, `payroll.statement.issued`, `payroll.statement.voided` (no person's pay in a statement's payload).
- Notification types: `PayStatementReady`, `PayDetailsChanged`, `DepositReturned`, `BankAccountChanged`.
- The opt-in `PayrollPayHistory` recipe and deployment step imports final statements paid elsewhere into historical runs that can't be changed.
