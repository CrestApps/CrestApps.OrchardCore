---
sidebar_label: Payroll
title: Payroll
description: "Payroll for Orchard Core. Pay Setup owns pay schedules (periods, the overtime week and pay dates) and pay codes (class, open kind, multiplier, premium, export code), shared by Workforce's time clock, with usage checks, recipe and deployment steps, and the migration that moved the time clock's pay periods and pay codes."
user_manual:
  - user-manual/finance/pay-setup
---

The **Payroll** module (`CrestApps.OrchardCore.Payroll`) runs payroll. Its first feature, **Pay Setup**, owns the two records both payroll and time keeping need: pay schedules and pay codes. Workforce's time clock depends on Pay Setup, and Payroll never references Workforce.

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Payroll.PaySetup` | Pay Setup | none (turned on by Workforce Time Clock or Payroll) |

Projects: `CrestApps.OrchardCore.Payroll.Abstractions` (models, managers, `IPaySetupUsage`, `AddPayCodeKind`), `CrestApps.OrchardCore.Payroll.Core` (stores, managers, handlers, index providers and schema, the pay code seeder), `CrestApps.OrchardCore.Payroll` (the module).

## Pay schedules

A `PaySchedule` (catalog entry, `IPayScheduleManager`) says how often people are paid and when:

| Member | Meaning |
| --- | --- |
| `Frequency` | `Weekly`, `Biweekly`, `FourWeekly`, `SemiMonthly`, `Monthly`, `Quarterly`, `Yearly`. |
| `AnchorDate` | A day a period starts on, for weekly, every-two-weeks and every-four-weeks schedules; its month starts quarterly and yearly periods. |
| `SecondPeriodStartDay` | Twice a month: the day the second period starts (2 to 28, 16 by default). |
| `WeekStart` | The day the overtime week starts. |
| `PayDateRule`, `PayDateOffsetDays`, `FirstPayDay`, `SecondPayDay` | Pay a number of days after the period ends, or on fixed days of the month (0 is the last day) for monthly and twice-a-month schedules. |
| `NonBusinessDayShift` | A pay date on a day off moves to the business day `Before` or `After`, or stays (`None`). |
| `ApprovalLeadDays` | Days before the pay date a pay run must be approved. |

`PeriodOn(date)`, `WeekOf(date)` and `PayDateFor(periodEnd, isBusinessDay)` are pure. `PayDateFor` treats Saturdays and Sundays as days off unless the caller passes its own test, such as one that also reads a holiday calendar.

## Pay codes

A `PayCode` (catalog entry, `IPayCodeManager`, codes unique and upper case) has a `Code`, a `Name`, a `Class` (`Earning`, `Deduction`, `EmployerContribution`, `ImputedIncome`, `Reimbursement`, `Memo`), a `Kind`, a `Multiplier` (0 to 10), a `PremiumPerHour`, an `ExportCode` (`PayrollCode` returns it or the code) and `Enabled`.

The kind is an open string. `PayCodeKinds` holds the built-in ones (`Regular`, `Overtime`, `DoubleTime`, `Holiday`, `Weekend`, `PaidTimeOff`, `PredictabilityPay`, `CallBack`, `ShiftDifferential`, `Unpaid`, `Salary`, `Bonus`, `Commission`, `Tips`); a module adds its own:

```csharp
services.AddPayCodeKind("Mileage", LocalizationSource.Create<Startup>("Mileage"));
```

Modules keep their own settings on a code in `Properties`. Workforce keeps a shift differential's window there (`WorkforcePayCodeSettings`: `WindowStart`, `WindowEnd`, `Weekdays`), with its own card on the pay code editor and its own check that a shift differential has a window. The time clock only picks earning and memo codes for lines of hours.

When Pay Setup is turned on, it adds the usual codes (`REG`, `OT`, `DT`, `PTO`, `PP`, `HOL`, `WKND`, `UNPAID`) to a site that has none.

## Records in use are kept

A schedule or code another module uses can't be deleted; turn it off instead. A module tells Pay Setup what it uses:

```csharp
services.AddPaySetupUsage<MyPaySetupUsage>();

public sealed class MyPaySetupUsage : IPaySetupUsage
{
    public Task<IReadOnlyList<string>> GetUsagesAsync(PaySchedule schedule, CancellationToken cancellationToken = default) => ...;

    public Task<IReadOnlyList<string>> GetUsagesAsync(PayCode code, CancellationToken cancellationToken = default) => ...;
}
```

The admin pages list what uses the record, and the stores refuse the delete too. The time clock reports the policies and timesheets that name a schedule, and the time off types and timesheet lines that name a code.

## Pay inputs

Modules hand hours and amounts to payroll through `IPayInputSource` (`AddPayInputSource<T>()`): `GetInputsAsync(PayInputQuery)` returns `PayInput` rows (staff member, pay schedule and period, pay code, day, hours or amount, rate, cost allocation, the record it comes from, and whether a pay run or an export already took it); `MarkConsumedAsync` and `ReleaseAsync` record that a consumer (a pay run, or a data pipeline run) took the inputs of some records, or gave them back. `IPayInputService` reads every source and tells each source about its own records. Workforce's time clock is the first source: one input per locked timesheet, pay code, day, activity and job.

With Data Pipelines on, Pay Setup adds Orchard's **Payroll** data source with the **Pay inputs** data set (`PayInputs`), read only by people with `ExportPayrollData`. It holds the inputs not taken yet; the parameter `IncludeExported=true` adds the others; filters on `PayScheduleId` and `PeriodStart` narrow the read. `Amount` is not rounded (its own amount, or hours times the code's premium, or times the rate and the multiplier; unpaid time pays nothing): sum, then round.

The **Mark pay inputs exported** step (`MarkPayInputsExported`) passes the rows on unchanged and, once the whole run succeeds, records the records they come from (`SourceKind`, `SourceId`) as taken by the run (`pipeline:{runId}`). A preview, a failed or a cancelled run marks nothing. The publisher of the pipeline must have `ExportPayrollData`.

The recipe **Payroll: export pay inputs to a CSV file** (`PayrollPayInputsExport`) adds the draft pipeline **Pay inputs export**: read the pay inputs, mark them, group by person, period, pay code and rate summing the hours and amounts, round, sort, choose the columns (EmployeeId, Name, PeriodStart, PeriodEnd, PayCode, Hours, Rate, Amount) and write a CSV file. Add a delivery step and publish it.

## Admin

**Payroll > Setup > Pay Schedules** and **Pay Codes**, under `ManagePaySetup`. The schedule editor shows the next four periods and their pay dates, counted from today in the site's time zone.

| Permission | Allows |
| --- | --- |
| `ManagePaySetup` (Manage pay setup) | Pay schedules and pay codes. |
| `ExportPayrollData` (Export payroll data) | Reading payroll data in data sets and pipelines. |

## Recipes and deployment

The `PaySchedules` step (`Schedules`, matched by id then name) and the `PayCodes` step (`Codes`, matched by id then code), both described to CrestApps Recipes, and the **Pay schedules** and **Pay codes** deployment steps.

```json
{
  "name": "PayCodes",
  "Codes": [
    { "Code": "NIGHT", "Name": "Night shift", "Class": "Earning", "Kind": "ShiftDifferential", "PremiumPerHour": 2.25,
      "Properties": { "WorkforcePayCodeSettings": { "WindowStart": "22:00:00", "WindowEnd": "06:00:00", "Weekdays": [] } } }
  ]
}
```

Workforce's `WorkforceTimeClock` step still reads its `PayPeriods` and `PayCodes` keys and saves them through these managers, so older recipes keep working. Its deployment step keeps exporting the pay schedules its policies name, and leaves pay codes to the `PayCodes` step.

## Upgrading from the time clock's pay periods and pay codes

Before Pay Setup, the time clock kept `PayPeriodCalendar` and `PayCode` in the Workforce collection. The time clock's migration (`UpdateFrom2`) moves each one to the Payroll collection with the same id, so policies, timesheets and lines keep pointing at them:

- a pay code's old kind number becomes its name, and its window goes into `WorkforcePayCodeSettings`; unpaid codes become `Memo`, the others `Earning`;
- a code Pay Setup seeded a moment before with the same code is replaced by the old one;
- a record already in the Payroll collection with the same id is left as it is;
- the old documents and index tables are removed, so a second run moves nothing;
- roles with **Manage time clock policies** get **Manage pay setup**.
