# Banking

Everything about banking in one module: bank accounts, printed checks, money sent out (disbursements) and direct deposit by ACH bank files, as four features.

## Features

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Banking` | Bank Accounts | CrestApps Resources |
| `CrestApps.OrchardCore.Banking.Checks` | Checks | Bank Accounts |
| `CrestApps.OrchardCore.Banking.Disbursements` | Disbursements | Bank Accounts |
| `CrestApps.OrchardCore.Banking.DirectDeposit` | Direct Deposit (ACH, US dollar accounts) | Disbursements |

Projects: `CrestApps.OrchardCore.Banking.Abstractions`, `CrestApps.OrchardCore.Banking.Core` and `CrestApps.OrchardCore.Banking`, with the namespaces `CrestApps.OrchardCore.Banking` (bank accounts), `.Checks`, `.Disbursements` and `.DirectDeposit`.

### Bank Accounts

- One `BankAccount` record per account, owned by a `PartyRef` (a staff member, a user, an account) or by the business itself.
- Kinds: Checking, Savings, CreditCard, LineOfCredit, Cash and Other. Only checking and savings (`BankAccountKinds.IsDeposit`) send or receive bank files or have checks drawn on them.
- The account number is a `ProtectedValue` (Data Protection, purpose `Banking.AccountNumber`); screens show only its last digits. An account known only by its last digits keeps them in `AccountNumberLastFour`; the site never makes up the rest.
- The business's own accounts can be saved without routing and account numbers until something needs them (the check line on blank stock, a bank file, a disbursement); a payee's account needs both.
- Routing and account numbers are checked per country through `IBankRoutingValidator`; the United States ABA check is built in.
- A status table (Unverified, Verification sent, Verified, Failed, Closed); a changed number always takes the account back to unverified, and only a verified account can be paid.
- `IBankAccountVerifier` for ways to check an account, `IBankAccountObserver` to react to changes.
- **Banking > Bank Accounts** (`ManageBankAccounts`), **Banking > My Bank Accounts** (`ManageOwnBankAccounts`), and an audited **Show the full number** (`RevealBankAccountNumbers`).
- The `BankAccounts` recipe and deployment steps (numbers only with **Include the account numbers**), the Banking data source, the Bank Account Changed workflow event and Audit Trail events.

## Installation

Reference the module (it is in `CrestApps.OrchardCore.Cms.Core.Targets`) and enable **Banking** in **Tools > Features**.

## Usage

```csharp
public sealed class PayAccounts(IBankAccountManager manager, IBankAccountService service)
{
    public async Task<BankAccount> AddAsync(string staffId, string routing, string number)
    {
        var account = await manager.NewAsync();
        account.Name = "Pay";
        account.HolderName = "Pat Doe";
        account.Owner = new PartyRef("Staff", staffId);

        var errors = service.ApplyNumbers(account, routing, number);

        if (errors.Count == 0 && (await manager.ValidateAsync(account)).Succeeded)
        {
            await manager.CreateAsync(account);
        }

        return account;
    }
}
```

## Documentation

- Technical: `src/CrestApps.Docs/docs/modules/banking.md`
- User manual: `src/CrestApps.Docs/docs/user-manual/finance/bank-accounts.md`

## Checks

Check printing for any module that pays by check: paychecks, refunds, vendor and agency payments.

- **Check layouts**: blank or pre-printed stock, three sheet forms, the place of every field, calibration, signature images, a second signature above an amount, and the check line (MICR) in a font the site uploads. Files are kept encrypted in the tenant file store, never in the media library. No MICR font ships with the module.
- **Check register**: numbered per bank account through `INumberSequence` (starting at 1001; **Next numbers** for new stock), with the status table Queued, Issued, Printed, Voided, StopPayment, Cleared, Reissued and Stale. A check is numbered now (`CheckNumbering.Next`), at print on pre-printed stock (`AtPrint`: queued without a number) or recorded as written by hand with its number (`Handwritten`). Voided checks keep their numbers and their void date (`VoidedOn`); a reissue gets a new check and links both; a cleared check can be un-cleared.
- **Printing**: checks with vouchers to one PDF (`ICheckPrinter`, on `CrestApps.OrchardCore.Pdf.Core`), from the register's bulk actions **Print with {layout}** and **Print with the account's default layout** (`BankAccountCheckSettings.DefaultLayoutId`), and **Print queued checks**: a print run numbers an account's queued checks from the first sheet's number and keeps each misprinted sheet as a spoiled, voided check.
- **Extension points**: `ICheckVoucherSource` (the stub of a module's checks, one table or `CheckVoucherSection`s), `ICheckWriteHandler` (takes part in writing every check: completes or refuses it, then posts it in the same unit of work), `ICheckChangeHandler` (asked before a void, reissue, clear, un-clear or edit, may refuse), `ICheckObserver`, `ICheckMicrFormat` (United States built in), `IAmountInWords` (English built in).
- **Positive pay**: the Check Register data set and the `ChecksPositivePay` recipe's draft pipeline. The module writes no bank file itself.
- Recipe and deployment steps `CheckLayouts` and (opt-in) `CheckRegister`; workflow events Check Issued, Printed, Voided and Cleared.

### Installation

Enable **Checks** in **Tools > Features** (it turns on **Banking**). Add a check layout under **Banking > Check Layouts** and upload a MICR E-13B TrueType font you are licensed to use if the layout prints the check line.

### Usage

```csharp
var result = await checkService.IssueAsync(new CheckIssueRequest
{
    BankAccountId = fundingAccountId,
    PayeeName = "Pat Doe",
    Amount = 812.40m,
    CheckDate = payDate,
    SourceKind = "PayStatement",
    SourceId = statement.ItemId,
});
```

Register an `ICheckVoucherSource` for the source kind to print the stub. For pre-printed stock, set `Numbering = CheckNumbering.AtPrint` and print the queued checks in a run; for a check written by hand, set `Numbering = CheckNumbering.Handwritten` and `Number`. Void on a date with `VoidAsync(check, reason, voidedOn)`, and register an `ICheckChangeHandler` with `AddCheckChangeHandler<T>()` to reverse or refuse a void, reissue, clear, un-clear or edit of the checks your module writes. To take part in the register's own **Write a check** (your fields on the page, your source on the check, your posting in the same unit of work), add a `DisplayDriver<CheckRecord>` and an `ICheckWriteHandler` with `AddCheckWriteHandler<T>()`. A check waiting to print is edited with `ICheckService.UpdateAsync(check, details, reason)`.

### Documentation

- Technical: `src/CrestApps.Docs/docs/modules/banking.md` (the Checks section)
- User manual: `src/CrestApps.Docs/docs/user-manual/finance/checks.md`

## Disbursements and Direct Deposit

Money the business sends out, for any module that pays people or businesses: pay deposits, vendor payments, refunds.

- **Disbursements** recorded once by a stable idempotency key, with the status table Pending, Batched, Submitted, Settled, Returned, Failed, Canceled and Reversed. No account number is ever stored on them.
- **Batches** released through a provider: a bank file in the Nacha format (stored encrypted, downloaded or delivered by a data pipeline), a printed check when Checks is on, or paid outside the system.
- **Bank return and change files**: returns mark disbursements returned; notices of change correct the payee's bank account.
- **Prenotes**: a zero-amount test entry that verifies a payee's bank account (an `IBankAccountVerifier`).
- **Who sees which disbursements**: the shared `ISourceAccess` seam, applied to the list, the data set, the export, every action and the batches.
- The **Disbursements** data set; the **Read a disbursement batch file** pipeline step; the `Disbursements` recipe and opt-in deployment step; workflow events Disbursement Submitted, Settled, Returned and Failed, and the Settle Disbursement Batches task.

### Installation

Enable **Disbursements** (and **Direct Deposit** for bank files) in **Tools > Features**. Fill in **Bank file (ACH)** on the business's bank account the files leave from.

### Usage

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

See the [Disbursements and Direct Deposit documentation](https://orchardcore.crestapps.com/docs/modules/banking#disbursements-and-direct-deposit).
