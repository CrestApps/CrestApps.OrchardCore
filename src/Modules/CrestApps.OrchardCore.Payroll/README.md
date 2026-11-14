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

## Pay profiles and pay items

- `EmployeePayProfile` per staff member and employer (`IPayProfileManager`): pay schedule, worker type, overtime status,
  the protected tax id with a keyed hash (`IPayProfileService`), dated tax elections (`ElectionOn`, field sets from
  `AddElectionFieldSet`), the split of net pay, and blockers (`IPayProfileBlockerProvider`). Changes and reveals reach
  `IPayProfileObserver`; with Audit Trail on, they are written there.
- `EmployeePayItem` (`IPayItemManager`): standing amounts or percents with dates, goal, limits and an employer match.
- **Payroll > Employees**, the staff profile's **Payroll** card, the **Employees** data set, the `PayrollPayProfiles` and
  `PayrollPayItems` recipe steps and the **Pay profiles** deployment step.
