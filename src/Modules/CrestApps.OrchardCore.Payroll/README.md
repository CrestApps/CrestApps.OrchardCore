# Payroll

The `CrestApps.OrchardCore.Payroll` module. Its first feature, **Pay Setup** (`CrestApps.OrchardCore.Payroll.PaySetup`),
owns pay schedules and pay codes; Workforce's time clock depends on it and Payroll builds on it.

## Pay Setup

- `PaySchedule`: how often people are paid (every week, two weeks, four weeks, twice a month with a chosen second
  period start, every month, quarter or year), the overtime week, and the pay date of each period (days after the
  period ends, or fixed days of the month), moved off a day off before or after. `PeriodOn`, `WeekOf` and
  `PayDateFor` are pure.
- `PayCode`: a code, a name, a class (earning, deduction, employer contribution, imputed income, reimbursement, memo),
  an open kind (`PayCodeKinds` and those a module adds with `AddPayCodeKind`), a multiplier, a premium an hour and an
  export code. Modules keep settings in `Properties`, such as Workforce's shift differential window.
- `IPayScheduleManager`, `IPayCodeManager`, and `IPaySetupUsage` (`AddPaySetupUsage<T>()`) so a schedule or code a module
  uses is not deleted.
- Admin: **Payroll > Setup > Pay Schedules** and **Pay Codes** under `ManagePaySetup`.
- Recipe and deployment steps `PaySchedules` and `PayCodes`.

## Pay inputs

- `IPayInputSource` (`AddPayInputSource<T>()`): a module hands hours and amounts to payroll and records what a pay run or
  an export took (`MarkConsumedAsync`, `ReleaseAsync`). `IPayInputService` reads every source. Workforce's locked
  timesheets are the first source.
- With Data Pipelines on: the **Payroll** data source with the **Pay inputs** data set (`ExportPayrollData`), the
  **Mark pay inputs exported** step (marks once the run succeeds), and the recipe **Payroll: export pay inputs to a CSV
  file**, a draft pipeline to publish.

## Payroll (employers and access)

- `Employer` (`IEmployerManager`, `IEmployerUsage`), a party kind; **Payroll > Setup > Employers**; the `PayrollEmployers`
  recipe and deployment steps.
- Per-employer permissions (`PayrollPermissionTemplates.For`) listed per employer in the role editor, and
  `IPayrollScopeService` for pages, data sets and exports. `IPayrollEmployeeDirectory` maps employers to staff.
- A **Payroll** card on teams (`PayrollTeamSettings`: ledger code, jurisdictions) and **Settings > Payroll**
  (`PayrollSettings`).
- The recipe **Payroll: sample roles**.

## Pay runs

- `PayRun` (`IPayRunManager`) with its `PayRunInput`s, statuses on `PayRunStatuses.Transitions`; `IPayRunService`
  calculates draft statements, submits to Approvals' `PayRunApproval` policy and cancels; `PayRunApprovalSubjectHandler`
  and `PayRunSourceAccess`. **Payroll > Pay Runs**; the **Save pay inputs** pipeline step on `PayRunInputImporter`.
- Processing: `IPayRunService.ProcessAsync`, `VoidStatementAsync` and `ReissuePaymentAsync`; payment methods
  (`IPayStatementPaymentMethod`) from **Direct Deposit** (Disbursements) and **Paychecks** (Checks, with
  `PayStatementCheckVoucherSource`); `PayStatementSourceAccess`; the `PayStatementReady` notification; `PayStubDocument`.
- Integration: `IPayRunObserver`; workflow events and tasks; the Payroll data sets and six reports with two dashboard
  widgets; webhook events; the pay date calendar source; audit events; notices of returned deposits and changed bank
  accounts; personal data; the opt-in `PayrollPayHistory` step; the scheduled-run workflow recipe.
- Liabilities and filings (Payroll): tax liabilities by deposit period with recorded deposits, filings prepared by
  country forms (`IPayrollFilingForm`), filed and amended, and year end; the United States pack adds Forms 941, 940 and
  W-2/W-3 and the SSA EFW2 file format.
- Garnishments (`CrestApps.OrchardCore.Payroll.Garnishments`): orders withheld within the law's limits and sent to
  their payees by check or deposit.
- United States Payroll (`CrestApps.OrchardCore.Payroll.UnitedStates`): the Form W-4 election and withholding method; the
  federal tax rules for 2026 as a recipe.
- My Pay (`CrestApps.OrchardCore.Payroll.SelfService`): `MyPayController` (own statements, stubs, elections and net
  pay split through the profile editor's `MyPay` group), `MyPayStaffHomeLinks`, the `PayDetailsChanged` security notice.

## The engine and taxes

- `IPayCalculator` runs the `IPayCalculationStep`s (`PayCalculationOrder`) over a `PayCalculationContext` built by
  `PayCalculationContextFactory`; gross-ups repeat them. `PayStatement` and its lines; accumulators from
  `PayStatementLineIndex` (`IPayAccumulatorService`). **Preview the next pay** on a profile.
- `IPayrollTaxProvider` (stateless); **Payroll Tax Tables** (`PayrollTaxRule`, **Payroll > Setup > Tax rules**, the
  `PayrollTaxRules` recipe and deployment steps) over Taxation's tables.

## Pay code rules

- A **Payroll** card on pay codes (`PayrollPayCodeSettings`, defaults by class and kind through `GetPayrollSettings`):
  calculation, tax treatment per category (`AddPayrollTaxCategory`), flags, period, yearly and lifetime limits,
  priority, arrears, proration, retro, form boxes (`AddPayrollFormBox`).
- `PayLimits` and `PayCodeTaxMatrix` (Payroll.Core), the seeded SAL, BONUS, COMM, TIPS, REIMB and LOAN codes, and
  `PayCodeBaseUsage` keeping a base code of a percent from being deleted.

## Pay profiles and pay items

- `EmployeePayProfile` per staff member and employer (`IPayProfileManager`): pay schedule, worker type, overtime status,
  the protected tax id with a keyed hash (`IPayProfileService`), dated tax elections (`ElectionOn`, field sets from
  `AddElectionFieldSet`), the split of net pay, and blockers (`IPayProfileBlockerProvider`). Changes and reveals reach
  `IPayProfileObserver`; with Audit Trail on, they are written there.
- `EmployeePayItem` (`IPayItemManager`): standing amounts or percents with dates, goal, limits and an employer match.
- **Payroll > Employees**, the staff profile's **Payroll** card, the **Employees** data set, the `PayrollPayProfiles` and
  `PayrollPayItems` recipe steps and the **Pay profiles** deployment step.
