---
sidebar_label: Personal Data
title: Personal Data
description: "One personal-data service for access and erasure requests: the subject (user ID, email, phone), the IPersonalDataContributor every module that stores personal data registers, the open export records, and the contributors that ship."
---

Access and erasure requests (GDPR, CCPA) need everything the site keeps about one person, from every module that keeps some. The **Personal Data** building block gives every module one contract for that, so a request reaches commerce, the CRM, Workforce, Scheduling and every other module through one call.

It is a building block, not a feature: there is nothing to enable. A module that stores personal data registers a contributor, and the first registration also registers the service.

| Project | Holds |
| --- | --- |
| `CrestApps.OrchardCore.PersonalData.Abstractions` | `PersonalDataSubject`, `IPersonalDataContributor`, `IPersonalDataService`, `PersonalDataRecord`, `PersonalDataExport`, `PersonalDataAnonymizeResult` and the two contexts. Namespace `CrestApps.OrchardCore.PersonalData`. |
| `CrestApps.OrchardCore.PersonalData.Core` | `DefaultPersonalDataService` and the `AddPersonalData()` and `AddPersonalDataContributor<T>()` registration helpers. |

## The service

```csharp
public interface IPersonalDataService
{
    Task<PersonalDataExport> ExportAsync(PersonalDataSubject subject, CancellationToken cancellationToken = default);

    Task<PersonalDataAnonymizeResult> AnonymizeAsync(PersonalDataSubject subject, string replacementText, CancellationToken cancellationToken = default);

    Task<PersonalDataAnonymizeResult> ExpireAsync(PersonalDataSubject subject, CancellationToken cancellationToken = default);
}
```

The person is a **`PersonalDataSubject`**, made from a user ID, an email and/or a phone number (one is required): `new PersonalDataSubject(userId, email, phoneNumber)`. Emails match without case sensitivity, and phone numbers match by their digits, so `+1 (702) 555-0100` matches `+17025550100`.

1. **Identify.** Every contributor's `IdentifyAsync` may add what its records reveal to the subject. The rounds repeat until a round adds nothing, at most five times. This comes first, because anonymizing in the same pass would remove the very email the next module looks for. The contributors that ship add names only (for scrubbing free text), never an email or a phone number found on a record: a shared inbox (`info@`), a switchboard or a household line would otherwise lead every module to other people's records. The commerce contributors keep following a checkout email to its guest owner and a transaction to its checkout, as they always have.
2. **Export or anonymize.** Every contributor then runs in order of its `Order` (lowest first).

- **`ExportAsync`** returns a `PersonalDataExport`: when it was made, the user IDs, emails and phone numbers the person was found by, the `Records`, and `Warnings` for anything that could not be read.
- **`AnonymizeAsync`** removes or replaces the person's data and returns a `PersonalDataAnonymizeResult` with how many records of each kind changed (`Records`, `Total`) and `Warnings` for anything that must be done by hand. A removed name becomes `replacementText`, or is cleared when it is `null`. Records the business must keep (orders, time worked, amounts, dates and states) stay; only the personal data in them goes.

- **`ExpireAsync`** anonymizes because a retention window ran out, such as the nightly expiry of inactive guests. Only the contributors whose **`TakesPartInRetentionExpiry`** is `true` identify the person and anonymize their records, names are cleared, and `PersonalDataAnonymizeContext.Kind` is `PersonalDataRequestKind.RetentionExpiry` (`Erasure` for `AnonymizeAsync`). `TakesPartInRetentionExpiry` is `false` by default, so only the commerce bridge takes part: a guest's CRM contacts, activities, worker profile, appointments, bookings and the other modules' records are only erased by an explicit erasure request.

Each record is open: a `Kind` the module names (such as `Booking` or `StaffMember`), a `Title`, the record's `Id`, when it was made, and its `Fields` by name. A new module adds records of its own kinds; nothing in the building block lists them.

The **Customers** feature calls the service from its privacy actions: erasing a customer anonymizes their data in every module, and exporting a customer adds a `personalData` section with every record to the download. See [Customers: Privacy](customers#privacy).

## Writing a contributor

```csharp
public sealed class MyPersonalDataContributor : IPersonalDataContributor
{
    public async Task IdentifyAsync(PersonalDataSubject subject, CancellationToken cancellationToken = default)
    {
        // Optional: add the names your records know, so other modules can scrub them from free text. Never add an
        // email or a phone number typed on a record: it may be shared by other people.
        foreach (var record in await FindAsync(subject, cancellationToken))
        {
            subject.AddName(record.Name);
        }
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        foreach (var record in await FindAsync(context.Subject, cancellationToken))
        {
            var exported = context.Add("MyRecord", record.Id, record.Name, record.CreatedUtc);

            exported.Fields["Email"] = record.Email;
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        foreach (var record in await FindAsync(context.Subject, cancellationToken))
        {
            record.Name = context.ReplaceName(record.Name);
            record.Email = null;
            record.Notes = context.Scrub(record.Notes);

            await _store.UpdateAsync(record, cancellationToken);
            context.Result.Count("MyRecord");
        }
    }
}
```

Register it in the startup of the feature that owns the records, so a feature that is not enabled is skipped:

```csharp
services.AddPersonalDataContributor<MyPersonalDataContributor>();
```

- Keep `IdentifyAsync` safe to call more than once, and add only what belongs to the person beyond doubt. Prefer adding nothing but names: an email or a phone number found on a record may belong to several people.
- When you find records by phone number, do not take a number that several people share as the person's. The CRM skips a number more than one contact has and adds a warning to the export or the result instead.
- `context.Scrub(text)` replaces the person's emails, phone numbers and names in free text, each only as a whole word (the name `Lee` leaves `Leeann` alone). Names shorter than three characters are never added to the subject, so they are never scrubbed.
- Clear the fields your records keep personal data in by name, rather than scrubbing a whole document. `subject.ScrubJson(node, replacement)` is there for free-form settings: it scrubs the strings of a JSON document and clears values that are one of the person's phone numbers written another way, but it never touches a value under a property whose name ends with `Id` or `Ids` (`ContentItemId`, `UserIds`, `StatusId`) or a value that looks like an id (a 26-character lowercase id or a GUID).
- Give the contributor a lower `Order` when it finds records through links another contributor erases.
- Leave `TakesPartInRetentionExpiry` at `false` unless the records belong to the retention window the request comes from (commerce records for the guest expiry). Opting in means the nightly expiry anonymizes them without anyone asking.
- Find records through an indexed column. When the index has no column for the person, add one with an additive migration and fill it in for older records with `CatalogIndexBackfill.ReindexAsync<TRecord, TIndex>()` from a deferred task, as Bookings and Point of Sale do.

## Contributors that ship

| Feature | Contributor | What it covers |
| --- | --- | --- |
| Commerce | `CommercePersonalDataContributor` | Runs every commerce contributor through `ICommercePersonalDataService` (see [Commerce: personal data requests](commerce#personal-data-requests)) and turns its export into records: transactions, payments, refunds, installment plans, subscriptions, checkout sessions, coupon redemptions, payment-provider customers, stock alerts, and the records of Fundraising, Memberships, Marketplace and Reviews. It runs first (`Order` `-100`). |
| Omnichannel Activities | `CrmContactPersonalDataContributor` | CRM contacts and leads found by their primary email, or by one of their phone numbers when no other contact has it (a number several contacts share finds none of them and adds a warning, and a converted lead does not count as another contact). It adds nothing to the subject. Exports every field and the AI memory note about the contact. Anonymizing keeps the contact for its activities and clears only its known personal-data fields: the display text and title (replaced with *Anonymized contact*), the `FirstName`, `MiddleName`, `LastName`, `FullName`, `PreferredName` and `Nickname` text fields of any part, the emails and phone numbers of the `ContactMethods` bag, any other field whose whole value is one of the contact's emails or phone numbers, and the AI memory note (when the AI feature is on). Its references (company, owner, status and the other items and users it points at) are never changed. |
| Omnichannel Activities | `CrmActivityPersonalDataContributor` | Activities of those contacts, and activities addressed to the person's email or phone number that belong to no other contact (a shared number finds none). Anonymizing removes the destination and scrubs the notes and instructions; channel, times, outcome and disposition stay. |
| Staff | `StaffPersonalDataContributor` | The staff record of the person's account (workers included). Anonymizing replaces the name and removes the email, phone number, date of birth and home base; the employee number, employment dates and teams stay. |
| Workforce Time Clock | `TimePunchPersonalDataContributor` | The worker's punches. Anonymizing removes the coordinates and the device and scrubs correction reasons; the time and kind stay. |
| Workforce Time Off | `TimeOffRequestPersonalDataContributor` | The worker's time-off requests. Anonymizing removes the reason given and scrubs the manager's note; dates, type, amount and decision stay. |
| Scheduling | `AppointmentPersonalDataContributor` | Appointments by account, email or phone, past and canceled ones included. An appointment found by phone only counts when it was booked by the person's account or email, or by phone alone, so a shared number never reaches another person's appointment. Anonymizing removes the customer's name, email, phone and manage link and scrubs the notes. |
| Bookings | `BookingPersonalDataContributor` | Bookings by account and by the booker's email, and their balances. Anonymizing removes the booker's name and email and the travelers, and scrubs the cancel reason. |
| Marketing | `MarketingPersonalDataContributor` | The purchase counts Marketing keeps for segments, found through the person's customer records (`ICustomerService` and the guest records with their email). Anonymizing removes the email and country; the counts stay. |
| Classifieds | `ClassifiedPersonalDataContributor` | The listings the person posted. Anonymizing expires and unpublishes them, removes the place and coordinates and scrubs the title. |
| Business Buying | `B2BBuyerPersonalDataContributor` | The person's spending limit on the account they buy for and whether punchout signs in as them. Anonymizing removes the limit and turns that punchout off. |
| Point of Sale | `RegisterShiftPersonalDataContributor` | Register shifts the staff member opened or closed and the cash they moved. Anonymizing replaces their name and scrubs notes and reasons; amounts and tallies stay. |

## Upgrading

- The Customers privacy export now adds a `personalData` section (the records of every module) in place of the `commerce` section. The commerce records are in it, each with its `kind`, `title` and `fields`.
- `CommercePersonalDataCustomerPrivacyHandler` is now `PersonalDataCustomerPrivacyHandler`, and it calls `IPersonalDataService`. Erasing a customer calls `AnonymizeAsync`; the guest retention expiry calls `ExpireAsync`, which reaches the commerce records only, exactly as the guest expiry did before. `CustomerPrivacyContext.Action` tells a handler which of the two it is.
- `ICommercePersonalDataContributor.TakesPartInRetentionExpiry` is `true` by default, so the commerce contributors the guest expiry always reached keep taking part in `ICommercePersonalDataService.ExpireAsync`. The Fundraising, Memberships, Marketplace and Reviews contributors return `false`: the guest expiry never reached them, and only an explicit erasure does.
- `ICommercePersonalDataContributor` still works: commerce contributors keep it, and the commerce bridge brings their records into the service. New contributors implement `IPersonalDataContributor`.
- Bookings adds the `UserId` and `CustomerEmail` columns to `BookingIndex`, and Point of Sale adds `OpenedByUserId` and `ClosedByUserId` to `RegisterShiftIndex`. Both migrations are additive, and the records saved before them are indexed again in the background after the tenant starts.
