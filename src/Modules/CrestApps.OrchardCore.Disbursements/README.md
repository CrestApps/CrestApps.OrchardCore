# Disbursements

Money the business sends out, for any module that pays people or businesses: pay deposits, vendor payments, refunds.

## Features

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Disbursements` | Disbursements | Banking |
| `CrestApps.OrchardCore.Disbursements.Nacha` | Disbursements - Bank File (ACH) | Disbursements |

- **Disbursements** recorded once by a stable idempotency key, with the status table Pending, Batched, Submitted, Settled, Returned, Failed, Canceled and Reversed. No account number is ever stored on them.
- **Batches** released through a provider: a bank file in the Nacha format (stored encrypted, downloaded or delivered by a data pipeline), a printed check when Checks is on, or paid outside the system.
- **Bank return and change files**: returns mark disbursements returned; notices of change correct the payee's bank account.
- **Prenotes**: a zero-amount test entry that verifies a payee's bank account (an `IBankAccountVerifier`).
- **Who sees which disbursements**: the shared `ISourceAccess` seam, applied to the list, the data set, the export, every action and the batches.
- The **Disbursements** data set; the **Read a disbursement batch file** pipeline step; the `Disbursements` recipe and opt-in deployment step; workflow events Disbursement Submitted, Settled, Returned and Failed, and the Settle Disbursement Batches task.

## Installation

Enable **Disbursements** (and **Disbursements - Bank File (ACH)** for bank files) in **Tools > Features**. Fill in **Bank file (ACH)** on the business's bank account the files leave from.

## Usage

```csharp
var result = await disbursementService.CreateAsync(new DisbursementRequest
{
    Payee = statement.Payee,
    PayeeName = "Pat Doe",
    Amount = 812.40m,
    Method = DisbursementsConstants.Methods.Ach,
    SourceBankAccountId = fundingAccountId,
    DestinationBankAccountId = payAccountId,
    SourceKind = "PayStatement",
    SourceId = statement.ItemId,
    AccessScope = statement.EmployerId,
});
```

See the [Disbursements documentation](https://orchardcore.crestapps.com/docs/modules/disbursements).
