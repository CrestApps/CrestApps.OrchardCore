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
