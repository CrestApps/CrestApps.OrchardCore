---
sidebar_label: Disbursements
title: Disbursements
description: "Money the business sends out, for any module that pays people or businesses: disbursements recorded once by a stable key, batches released through a provider (bank file in the Nacha format, printed check, paid outside the system), bank return and change files, prenotes that verify bank accounts, who sees which disbursements, the Disbursements data set, the Read a disbursement batch file pipeline step, recipe and deployment steps and workflow activities."
user_manual:
  - user-manual/finance/disbursements
---

The **Disbursements** module records the money the business sends out and sends it in batches through a provider. Payroll net pay is the first consumer; Marketplace vendor payouts move onto it next. It never holds the business's money: it writes the bank file, issues the checks or records a payment made elsewhere.

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Disbursements` | Disbursements | [Banking](./banking.md) |
| `CrestApps.OrchardCore.Disbursements.Nacha` | Disbursements - Bank File (ACH) | Disbursements |

Projects: `CrestApps.OrchardCore.Disbursements.Abstractions` (models and contracts), `CrestApps.OrchardCore.Disbursements.Core` (stores, rules, the manual and check providers, the Nacha writer and reader, the prenote verifier, the encrypted file store), `CrestApps.OrchardCore.Disbursements` (the module).

## The disbursement

A `Disbursement` (YesSql document in the `Disbursements` collection, `DisbursementIndex`) is one payment: `Payee` (a `PartyRef`), `PayeeName`, `IsBusinessPayee`, `Amount`, `Currency`, `Method` (open string: `Ach`, `Check`, `Manual`, or a provider's own), `Provider`, `SourceBankAccountId` (one of the business's accounts), `DestinationBankAccountId` (the payee's account, for a bank file), `SourceKind`, `SourceId` and `AccessScope` (the record it pays and who may see it), `Memo`, `IdempotencyKey`, `IsPrenote`, `Status`, `BatchId`, `EffectiveDate`, `TraceNumber`, `CheckId`, `FailureCode`, `FailureReason`, the dates of each change and `IsHistorical` for imported ones. It never holds an account number; the index holds no amount.

`DisbursementStatuses.Transitions`:

| From | To |
| --- | --- |
| `Pending` | `Batched`, `Submitted`, `Settled`, `Failed`, `Canceled` |
| `Batched` | `Pending` (its batch was cancelled), `Submitted`, `Settled`, `Failed`, `Canceled` |
| `Submitted` | `Settled`, `Returned`, `Failed`, `Canceled` |
| `Settled` | `Returned`, `Reversed` |
| `Returned`, `Failed`, `Canceled`, `Reversed` | none |

A `DisbursementBatch` holds the disbursements sent together: provider, source account, effective date, ids, count, total, currency, status (`Open`, `Released`, `Settled`, `Canceled`), the stored file, who made and released it, and `Sources` (the source kind and scope of each disbursement, so it is shown only to people who may see all of them).

## The rules: `IDisbursementService`

| Member | Does |
| --- | --- |
| `CreateAsync(DisbursementRequest)` | Records a disbursement, or returns the one its idempotency key already gave (`AlreadyExisted`). The key is the request's, or `SourceKind:SourceId:Method:DestinationBankAccountId`. Refuses: an unknown method, no payee name, an amount not more than zero (a prenote carries zero), a source account that is not the business's, closed, failed or without numbers, and for a bank file a payee account without numbers, not verified (a prenote may go to an unverified one) or in another currency. |
| `CancelAsync` | Cancels a pending disbursement. |
| `CreateBatchAsync(provider, ids, effectiveDate)` | Batches pending disbursements of the provider's method from one source account and currency; the others are listed as problems. |
| `ReleaseBatchAsync` | Hands the batch to its provider: the file is stored encrypted (`IDisbursementFileStore`, purpose `Disbursements.Files`), trace numbers and check ids are kept, statuses move. A refusal leaves the batch open. |
| `CancelBatchAsync`, `SettleBatchAsync`, `SettleReleasedBatchesAsync(ageInDays)` | Cancel an open batch (its disbursements are pending again); record a released batch as paid; settle every released batch older than an age. |
| `MarkSettledAsync`, `MarkClosedAsync` | Move one disbursement. |
| `ApplyReturnsAsync`, `ApplyChangeNoticesAsync` | A return marks the disbursement returned with its code; a notice of change (C01, C02, C03, C05, C06, C07) corrects the payee's bank account, which is unverified again. |

The service checks no permission; the caller does. Observers (`IDisbursementObserver`, `AddDisbursementObserver<T>()`) are told every change with who made it.

## Providers

| Provider | Feature | Does |
| --- | --- | --- |
| **Bank file (ACH)** (`Nacha`, method `Ach`) | Bank File (ACH) | Writes the file in the Nacha format: a PPD batch for people and a CCD batch for businesses, prenotes (codes 23 and 33, no amount), an optional offset debit per batch, entry hashes, totals, trace numbers (originating bank and a sequence), and lines of nines to fill the block of ten. The file id modifier letters the files of the business's day (A, B...). United States dollars only. Each account number is read once per file, as a Banking reveal the Audit Trail records. |
| **Printed check** (`Check`) | Disbursements, when Checks is on | Issues one check per disbursement for the same record and access scope, dated the effective date. A cleared check settles the disbursement, a voided or stopped one cancels it, and a reissue moves it onto the new check. |
| **Paid outside the system** (`Manual`) | Disbursements | Releasing records the payments as settled. |

A module adds a provider with `AddDisbursementProvider<T>()` (`IDisbursementProvider`: name, display name, method, `SubmitAsync`).

The bank file settings (`NachaOriginSettings`: immediate destination and origin with names, company name and identification, entry description, offset) are kept on one of the business's bank accounts, in its properties, and edited in the **Bank file (ACH)** card of the account page.

### Prenotes

With Bank File (ACH) on, Banking's **Check with** offers **Test entry (prenote)** (`NachaPrenoteVerifier`, an `IBankAccountVerifier`). It records a zero-amount disbursement from the first business account set up for bank files and puts the account in **Verification sent**. When the batch settles without a return the account is **Verified**; when the bank returns the entry it is **Failed** (`NachaPrenoteObserver`). A new number gets a new test entry.

### Return and change files

**Disbursement Batches > Import a return file** reads the bank's file (`NachaReturnFileReader`): each return addenda (type 99) marks its disbursement returned, each notice of change (type 98) corrects the payee's account.

## Who sees which disbursements

Like Checks, Disbursements applies the shared source access seam (`ISourceAccess` with `AddSourceAccess<T>()`, `ISourceAccessService`, in `CrestApps.OrchardCore.Abstractions` and `.Core`): the module that writes disbursements, such as Payroll for its pay statements, decides who may see (`View`) or act on (`Manage`) the disbursements of each access scope. It applies to the list (`IDisbursementStore.ListPageAsync`, a condition on `DisbursementIndex.AccessKey`), the data set, the export, every action by id (not found) and the batches (a batch is listed and acted on only when every one of its disbursements is allowed). Disbursements written by hand, and source kinds without a source access, are visible to anyone the permissions allow.

## Admin screens and permissions

| Permission | Allows |
| --- | --- |
| `ManageDisbursements` | **Record a disbursement**, batch (the list's bulk action **Batch to send by {provider}** with the arrival date), cancel, **Banking > Disbursement Batches** (release, download the bank file, mark paid, cancel, import a return file). Security critical. |
| `ViewDisbursements` | **Banking > Disbursements**, filtered by method and status and searched by payee or trace number. |

A bank file is downloaded with `Cache-Control: no-store`. A bank account money was sent from or to can't be deleted (`DisbursementBankAccountUsage`).

## Data pipelines and reports

With Data Pipelines on, the **Disbursements** data source has a **Disbursements** data set (requires `ViewDisbursements`, restricted by source access, date column the indexed `ModifiedUtc`): payee, amount, currency, method, provider, status, test entry, accounts' ids, source, batch, trace number, return code, memo and dates. No account number is a column.

The Bank File (ACH) feature adds one pipeline step, **Read a disbursement batch file** (`ReadDisbursementBatchFile`, a source of files): the bank files of the batches released since the last successful run, for Orchard's **Upload to an SFTP server**, FTP, web API or e-mail steps. The publisher needs `ManageDisbursements`, and a batch is read only when the publisher may act on all its disbursements. The watermark is saved only when the run succeeds, so a failed delivery sends the same files again.

## Recipes and deployment

- `Disbursements` (recipe step, and opt-in deployment step **Disbursements**): imported disbursements are history (`IsHistorical`), never batched or changed; one whose idempotency key exists is skipped. The export needs `ViewDisbursements` when run from a request and writes only what the person may see; run without a signed-in person it writes only the disbursements no source restricts. No account number is written.

Both steps have schemas for CrestApps Recipes.

## Workflows

Events (category Disbursements): **Disbursement Submitted**, **Settled**, **Returned** and **Failed** (also cancelled and reversed). Input: `DisbursementId`, `PayeeName`, `Amount`, `Currency`, `Method`, `Status`, `Change`, `SourceKind`, `SourceId`, `BatchId`, `TraceNumber`, `FailureCode`, `FailureReason`, `IsPrenote`, `EffectiveDate`, `Actor`. Never an account number.

Task: **Settle Disbursement Batches** (`AgeInDays`, 3 by default) records as paid the released batches older than the age that the bank did not return, which also verifies the accounts of their test entries. Run it from a Timer Event once a day; it writes the count to the workflow output `SettledDisbursements`.
