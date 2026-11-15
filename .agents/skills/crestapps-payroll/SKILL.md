---
name: crestapps-payroll
description: How to build on the CrestApps Payroll building block (CrestApps.OrchardCore.Payroll) instead of keeping another pay record. Use when a module hands hours or amounts to payroll, needs pay codes or pay schedules, reacts to a pay run or a pay statement, reads payroll data in a report or a pipeline, adds a tax provider or election fields, pays net pay another way, or limits access to some employers. Covers PaySchedule, PayCode, IPayInputSource, Employer, IPayrollScopeService, PayrollPermissionTemplates, EmployeePayProfile, IPayProfileObserver, the pay engine (IPayCalculationStep, IPayrollTaxProvider), IPayRunService, IPayRunObserver, IPayStatementObserver, IPayStatementPaymentMethod, the Payroll data source, workflow events and tasks, webhook events, notification types and the opt-in pay history step.
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

Add a calculation step with `IPayCalculationStep` (orders in `PayCalculationOrder`) and a tax provider with `IPayrollTaxProvider`. The built-in provider (Payroll Tax Tables) works over Taxation's tables.

## Pay runs and statements

`IPayRunService`: `OpenAsync`, `CalculateAsync`, `SubmitAsync` (to Approvals), `ProcessAsync`, `VoidStatementAsync`, `ReissuePaymentAsync`, `ListStatementsAsync`. Statements never change once final; a void is a reversing statement. Hear steps with `IPayRunObserver` (Opened, Calculated, Submitted, Approved, Rejected, Paid, PartiallyPaid, Failed, Canceled) and statements with `IPayStatementObserver` (Issued, Voided). Approvals, Disbursements and the pay input sources commit the session: re-read a record by id after calling them before saving it.

## Paying net pay

Implement `IPayStatementPaymentMethod` (`Method`, `PayAsync`, `VoidAsync`) for a `NetPayMethod`; Direct Deposit pays through Disbursements and Paychecks through Checks, with the statement as the check's voucher (`ICheckVoucherSource`). Disbursements and checks of pay use the source kind `PayStatement` with the employer as access scope.

## Reading payroll data

The `Payroll` data source has the data sets Pay inputs, Employees, Pay runs, Pay statements, Earnings, Deductions, Taxes, Employer contributions, Payments of net pay and Year-to-date balances, each limited to the employers a user may see. Reports (Workforce & Payroll): Payroll Register, Payroll Summary, Cash Requirement, Deductions and Contributions, Labor Distribution, Year-to-date Earnings. Never read statements to rebuild these.

## Automation, notices and history

- Workflow events: Pay Run Opened, Calculated, Submitted for Approval, Approved, Rejected, Processed, Paid, Failed; Pay Statement Issued, Voided; Pay Profile Changed. Tasks: Open Pay Run, Add Pay Input, Calculate, Submit, Approve, Reject and Process Pay Run. A returned deposit starts Disbursements' Disbursement Returned event; a changed bank account Banking's Bank Account Changed event.
- Webhook events: `payroll.payrun.approved`, `payroll.payrun.processed`, `payroll.statement.issued`, `payroll.statement.voided` (no person's pay in a statement's payload).
- Notification types: `PayStatementReady`, `PayDetailsChanged`, `DepositReturned`, `BankAccountChanged`.
- The opt-in `PayrollPayHistory` recipe and deployment step imports final statements paid elsewhere into historical runs that can't be changed.
