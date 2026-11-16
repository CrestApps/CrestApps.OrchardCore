---
sidebar_label: Agreements
title: Agreements
user_manual: user-manual/administration/agreements
description: "The shared agreements building block: an agreement with an account (period, status, the hours it covers as a block or a monthly allowance, rounding), the usage providers modules register to count time spent under it, its balance, the observers told when it is about to end or passes 80% and 100% of its hours, the admin pages, the account tab, and the recipe and deployment step."
---

**Agreements** keeps the agreements you have with accounts, such as a support contract or a maintenance contract. One agreement can carry the terms of several modules at once: Work Orders' visit schedules and Tickets' service level sit on the same agreement, each module adding its own panel to the editor.

It is three projects:

| Project | Holds |
| --- | --- |
| `CrestApps.OrchardCore.Agreements.Abstractions` | `Agreement`, `AgreementHours`, `AgreementBalance`, `AgreementUsage`, `IAgreementUsageProvider`, `IAgreementBalanceService`, `IAgreementObserver`, `IAgreementManager`, `AgreementsPermissions`, `AgreementsConstants` |
| `CrestApps.OrchardCore.Agreements.Core` | The YesSql store and `AgreementIndex`, the numbering and rules (`AgreementHandler`), `AgreementBalanceService`, `AgreementLifecycle`, `AgreementPeriods`, `AddAgreementsCore()` |
| `CrestApps.OrchardCore.Agreements` | The **Agreements** feature: the admin list and editor, the account tab, the background task, the recipe and deployment step |

## The agreement

| Value | Meaning |
| --- | --- |
| `Number` | SA-00042, from the locked `Agreements.Agreement` sequence |
| `Name`, `Notes` | What people call it, and notes for the people who work under it |
| `Status` | `Draft`, `Active`, `Paused`, `Expired`, `Canceled`. Only an active agreement in its period is in force (`IsInForceOn`) |
| `AccountId` | The account it is with |
| `StartDate`, `EndDate`, `TimeZoneId` | Its period, counted in its time zone; no end runs until it is canceled |
| `Hours` | `Kind` (`None`, `Block` used until it runs out, `MonthlyAllowance` each calendar month), `Hours`, and `RoundingMinutes` (0, 6, 15 or 30) each entry of time is rounded up to |
| `ExpiringNoticeDays` | How long before its end it is told to be about to end |
| `Properties` | The terms other modules keep on it |

The handler checks the name, the dates, the notice (0 to 365 days), the hours (more than none, at most 100,000, when it covers hours) and the rounding, for the editor and recipes alike. Only a draft can be deleted; a copy starts as a draft.

## Hours used and left

Modules that work under an agreement register an `IAgreementUsageProvider`, which lists the time spent under an agreement in a range (`AgreementUsage`: who, when, how long, on what). `IAgreementBalanceService.GetBalanceAsync` works out the period that holds a day (the agreement's whole period for a block, the calendar month in its time zone for an allowance), asks every provider, rounds each entry up and returns the `AgreementBalance`: covered, used, left, the overage past what is covered (billable: work goes on), the share used and the entries. `ListInForceAsync` finds the agreements of an account in force on a day.

## Life cycle

The **Agreements** background task (hourly) runs `AgreementLifecycle`: an active agreement past its end becomes `Expired`; one within its notice tells every `IAgreementObserver.OnExpiringAsync` once; and one whose used hours pass 80% and 100% of the period's cover tells `OnHoursUsedAsync` once for each, starting again each period (`HoursWarnings`).

## Pages

**Agreements** in the admin menu lists the agreements (the admin list's List and Grid layouts) with their status and hours; the editor has the general values, the **Covered hours** panel with how the hours stand now, and the panels other modules add through display drivers on `Agreement`. An account's page lists its agreements with the hours used, and links to a new agreement for it.

## Permissions

| Permission | Allows |
| --- | --- |
| `ManageAgreements` | Create, change, copy and delete agreements |
| `ViewAgreements` | See agreements and their hours (implied by the one above) |

## Recipe and deployment

The `Agreements` step creates or updates agreements by `ItemId`, with every value above and the `Properties` of other modules; the deployment step exports them all.

```json
{
  "name": "Agreements",
  "Agreements": [
    {
      "Name": "Gold support",
      "Status": "Active",
      "AccountId": "4d33nzx1h8tcms96qw9qsjn9xj",
      "StartDate": "2026-11-01",
      "TimeZoneId": "America/New_York",
      "Hours": { "Kind": "MonthlyAllowance", "Hours": 10, "RoundingMinutes": 15 }
    }
  ]
}
```

## Extending agreements

- Keep your module's terms in `Properties` (`agreement.Put(new MyTerms { ... })`) and add a display driver on `Agreement` for their editor.
- Register an `IAgreementUsageProvider` for the time your module spends under an agreement.
- Register an `IAgreementObserver` to act when an agreement is about to end or passes a share of its hours.
- Register an `IAgreementTermsValidator` to check your terms: the agreement's own rules call every validator, so the editor and recipes check the same set whichever feature carries the terms.

With Notifications on, `AgreementAccountManagerNotifier` sends the account's manager (`AccountTeamPart.OwnerStaffId`, through the staff directory, to their user) a notification when an agreement passes 80% and 100% of its hours and when it is about to end.

Tickets is a consumer: its terms are `TicketAgreementTerms` (the desks and ticket types it covers, the service-level policy of its tickets) with a **Tickets** panel, a new ticket takes the agreement that covers it, and its billable time counts against the hours (see [Tickets](./tickets.md#agreements)).

Work Orders is one consumer: its visit schedules are `WorkOrderAgreementTerms` with a **Visits** panel, and the billable labor of its completed work orders counts against the hours (see [Work Orders](./work-orders.md#service-agreements)).
