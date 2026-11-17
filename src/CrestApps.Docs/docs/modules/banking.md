---
sidebar_label: Banking
title: Banking
description: "Everything about banking in one module, as four features: Bank Accounts (the BankAccount record with its number protected at rest, routing checks, verification, the audited reveal), Checks (layouts with the check line, the register, printing, void, reissue, stop and stale checks, positive pay), Disbursements (money sent out once by a stable key, in batches through a provider, with returns) and Direct Deposit (ACH bank files in the Nacha format for US dollar accounts, prenotes, return and change files)."
user_manual:
  - user-manual/finance/bank-accounts
  - user-manual/finance/checks
  - user-manual/finance/disbursements
---

The **Banking** module (`CrestApps.OrchardCore.Banking`) is everything about banking: the bank accounts money is paid from and to, printed checks, the money the business sends out, and direct deposit by bank file. Modules that pay people or businesses (Payroll, Marketplace) build on its features instead of keeping their own account numbers, checks or payouts.

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Banking` | Bank Accounts | CrestApps Resources |
| `CrestApps.OrchardCore.Banking.Checks` | Checks | Bank Accounts |
| `CrestApps.OrchardCore.Banking.Disbursements` | Disbursements | Bank Accounts |
| `CrestApps.OrchardCore.Banking.DirectDeposit` | Direct Deposit | Disbursements |

All four are in the Finance category and under the **Banking** admin menu. Projects: `CrestApps.OrchardCore.Banking.Abstractions` (models and contracts), `CrestApps.OrchardCore.Banking.Core` (stores, rules, the United States routing check and check line, English amounts in words, the PDF printer on [`CrestApps.OrchardCore.Pdf.Core`](./reports.md), the manual and check providers, the Nacha writer and reader, the prenote verifier, the encrypted file stores) and `CrestApps.OrchardCore.Banking` (the module). The namespaces are `CrestApps.OrchardCore.Banking` (bank accounts), `.Checks`, `.Disbursements` and `.DirectDeposit`.

Direct Deposit writes ACH files for US dollar accounts. Other countries' bank file formats, such as Canada's CPA Standard 005, are further providers of the same feature (not built yet).

## Bank Accounts

The **Banking** module keeps one record for a bank account: one of the business's own, such as the account payroll is paid from, or one of a person or organization the business pays, such as an employee's pay account. Modules that send money by bank (Checks, Disbursements, Payroll) build on it instead of keeping their own account numbers.

### The bank account

| Field | Notes |
| --- | --- |
| `Name` | The name the account is listed by, such as "Payroll funding". |
| `Owner` | A `PartyRef` (see [Parties](./parties.md)): a staff member, a user, an account or any kind a module adds. Empty for the business's own accounts. |
| `HolderName`, `BankName` | The name on the account as the bank knows it, and the bank's name. |
| `Kind` | `Checking` or `Savings`. |
| `Country`, `Currency` | Two-letter and three-letter ISO codes. The country decides how the numbers are checked. |
| `RoutingNumber` | The bank's number, without spaces or dashes. It names the bank, not the account, and is printed on every check. |
| `AccountNumber` | A `ProtectedValue`: the number protected with the tenant's Data Protection keys (purpose `Banking.AccountNumber`), with at most its last four characters kept in clear text as the hint. Screens show it as `••••6789`. |
| `Status`, `StatusChangedUtc`, `StatusReason`, `VerifiedUtc`, `VerificationMethod` | Where the account is in its verification. |
| `NumberChangedUtc`, `CreatedUtc`, `ModifiedUtc` | When the numbers last changed, and the record's stamps. |
| `Properties` | Other modules' data on the account. |

Each account is a YesSql document in the `Banking` collection, with `BankAccountIndex` (name, owner kind and id, status, country, currency, created). The index holds no number.

#### Status

`BankAccountStatuses.Transitions` (a `TransitionTable<BankAccountStatus>`) allows these moves:

| From | To |
| --- | --- |
| `Unverified` | `VerificationSent`, `Verified`, `Failed`, `Closed` |
| `VerificationSent` | `Unverified`, `Verified`, `Failed`, `Closed` |
| `Verified` | `Unverified`, `Failed`, `Closed` |
| `Failed` | `Unverified`, `VerificationSent`, `Verified`, `Closed` |
| `Closed` | none: a closed account never opens again |

Only a `Verified` account can receive payments (`BankAccount.CanReceivePayments()`). A new routing or account number always takes the account back to `Unverified`, so money never goes to a number nobody checked. An account may be stored without numbers (an import without sensitive data writes it that way); it can then be neither checked nor paid until someone enters them.

### The rules: `IBankAccountService`

Write numbers and status through `IBankAccountService`, never by setting the fields:

| Member | Does |
| --- | --- |
| `ApplyNumbers(account, routingNumber, accountNumber)` | Checks both numbers for the account's country, normalizes them and protects the account number. A blank value keeps the stored one; a number equal to the stored one changes nothing. Returns the refused numbers and why. |
| `TrySetStatus(account, status, reason, verificationMethod)` | Moves an unsaved account when the table allows it. |
| `ChangeStatusAsync(...)` | The same, and saves. |
| `StartVerificationAsync(account, verifierName)` | Asks a verifier to check the account and saves the status it returns. |
| `RevealAccountNumberAsync(account, reason)` | Reads the full number, writes a log entry (the account id, who read it and why, never the number) and tells the observers. The log entry is written whether or not the Audit Trail is on; keep the Audit Trail on so each read is also an event people can search. Call it only for a user with **Reveal bank account numbers**, or for a file a payment needs. |
| `GetUsagesAsync(account)` | Why records of other modules point to the account, such as checks drawn on it. |
| `ApplyLink(account, providerName, result)` | Writes what a link provider read from the bank on an unsaved account: the numbers go through `ApplyNumbers` (so a different number is recorded as `NumberChanged`), then the account moves to the result's status (usually `Verified`) with the provider as `VerificationMethod`. Fills `Kind` and an empty `BankName`. Refuses a closed account, a refused result and a result without numbers. |
| `Verifiers` | The verifiers the enabled features registered. |
| `LinkProviders` | The link providers the enabled features registered. Ask each `IsAvailableAsync` before showing it. |

The service checks no permission; the caller does. `IBankAccountManager` (a catalog manager) stores the accounts and lists them by owner with `ListByOwnersAsync`.

### Extension points

| Contract | Register with | Use |
| --- | --- | --- |
| `IBankRoutingValidator` | `AddBankRoutingValidator<T>()` | The routing and account number checks of one country. The United States check (nine-digit ABA number with its 3-7-1 check digit, account numbers of up to 17 letters and digits) is built in. A country without one gets letters and digits, up to 34 characters. |
| `IBankAccountVerifier` | `AddBankAccountVerifier<T>()` | A way to check an account, such as a zero-amount test entry. The account page offers one **Check with** button per verifier. |
| `IBankAccountLinkProvider` | `AddBankAccountLinkProvider<T>()` | A way for a person to link their bank in the browser so the account is saved with the bank's own numbers, usually verified at once. [Plaid](./plaid) is the first. See [Linking a bank](#linking-a-bank). |
| `IBankAccountObserver` | `AddBankAccountObserver<T>()` | Told after each save (`Created`, `Updated`, `NumberChanged`, `StatusChanged`, `Deleted`) and after each reveal (`NumberRevealed`), with who did it and why. It never receives the number in clear text. Payroll uses it to send a security notice when a pay account changes. |
| `IBankAccountUsage` | `AddBankAccountUsage<T>()` | Tells that a module's records point to an account. An account in use can't be deleted: the handler refuses (`DeletingAsync` throws with the reasons) and the admin page says why and offers to close it instead. Checks registers one for the accounts checks are drawn on. |
| `IUserPartySource` (Parties) | `AddUserPartySource<T>()` | Tells which parties a signed-in user is. Staff registers one, so a person's staff record owns their pay accounts on **My bank accounts**. |

### Linking a bank

A link provider lets a person sign in to their bank in the browser and choose the account, so nobody types the numbers and the account is verified at once. Banking owns the pages; the provider never adds routes to them and Banking never names a vendor.

| Page | Route | Permission |
| --- | --- | --- |
| **My Bank Accounts > Add** shows **Link your bank with \{provider\}** per available provider; linking makes a new account | `banking/my-accounts/link/{provider}` | `ManageOwnBankAccounts` |
| **My Bank Accounts > Edit** shows **Link your bank with \{provider\} instead**; the bank's numbers replace the stored ones | `banking/my-accounts/link/{provider}/{id}` | `ManageOwnBankAccounts`, own accounts only |
| The admin account page shows **Link with \{provider\}** in the action bar's secondary buttons | `banking/accounts/link/{id}/{provider}` | `ManageBankAccounts` |

The flow (`BankAccountLinkFlow` in the module):

1. `StartAsync(context)` returns a `BankAccountLinkStart`: the shape to render and a client payload, such as a short-lived token. Banking renders the shape inside a form that has a hidden `clientResult` field and a `[data-bank-account-link-open]` button.
2. The shape's script opens the provider when the button is clicked (after the form's own checks pass, such as the holder's name of a new account), writes what the provider returned into `clientResult` and submits the form.
3. Banking calls `CompleteAsync(context, clientResult)`, which returns a `BankAccountLinkResult` with the numbers in clear text (in memory only), the kind, the bank's name, the last digits and the status.
4. Banking applies it with `IBankAccountService.ApplyLink`, validates and saves the account. A new account is named from the bank and the last digits unless the person names it.
5. Banking calls `FinishAsync(context, result, saved)` either way, so the provider keeps or lets go of what it holds (for Plaid, the item and its access token).

A closed account can't be linked. A changed number fires `NumberChanged` as a typed one does, so consumers such as Payroll still send their security notice.

### Admin screens and permissions

| Permission | Allows |
| --- | --- |
| `ManageBankAccounts` | **Banking > Bank Accounts**: add, change, verify, close and delete every account that nothing points to. Full numbers stay hidden. |
| `ManageOwnBankAccounts` | **Banking > My Bank Accounts**: add, change and close one's own accounts (owned by the user or a party the user is). No status, no reveal, nobody else's. |
| `RevealBankAccountNumbers` | **Show the full number** on the account page. Security critical; every reveal is an Audit Trail event. |

Administrators get all three. The owner picker searches every party kind the enabled features offer, through `{admin prefix}/api/banking/owners/search` (route name `CrestApps.Banking.OwnerSearch`, under the site's admin URL prefix, requires `ManageBankAccounts`). The editor saves an owner only of a kind a module provides and that exists. On **My Bank Accounts** a person can own an account only as one of the parties they are, never sets its status, and can't change a closed account. The reveal page is sent with `Cache-Control: no-store`.

### Recipes and deployment

The `BankAccounts` recipe step creates accounts, or updates them by `ItemId`:

```json
{
  "name": "BankAccounts",
  "Accounts": [
    {
      "Name": "Payroll funding",
      "HolderName": "Contoso Ltd",
      "Kind": "Checking",
      "Country": "US",
      "Currency": "USD",
      "BankName": "First Bank",
      "RoutingNumber": "021000021",
      "AccountNumber": "123456789",
      "Status": "Verified"
    }
  ]
}
```

`OwnerKind` and `OwnerId` set the owner. Numbers are read in clear text and protected with the importing site's keys, since protected values can't be read on another site. A status is kept only when the numbers come with it; an account without numbers is imported as unverified. `Properties` carries what other features keep on the account, by the name of each settings object, such as the **Bank file (ACH)** settings of a business account (`"NachaOriginSettings": { "Enabled": true, "ImmediateOrigin": "1123456789", "CompanyName": "CONTOSO INC", "CompanyIdentification": "1123456789", "CompanyEntryDescription": "PAYROLL" }`); each one given replaces the stored one.

The **Bank accounts** deployment step exports every account, with its `Properties`, without its numbers or status. Run from a request, it needs `ManageBankAccounts`: without it the plan leaves the accounts out and says so, and the step can't be added to a plan. Its **Include the account numbers** option (`IncludeSensitiveData`) writes them in clear text; only a user with `RevealBankAccountNumbers` can turn it on or run a plan that uses it, and each exported number is recorded in the Audit Trail as a reveal. Both steps have schemas for CrestApps Recipes.

### Reports and data pipelines

With Data Pipelines on, the **Banking** data source has a **Bank accounts** data set (requires `ManageBankAccounts`): owner, holder, bank, type, country, currency, status, dates and the last digits of the account number. The routing and account numbers are never columns.

### Workflows and Audit Trail

- **Bank Account Changed** (event, category Banking): input `BankAccountId`, `Name`, `OwnerKind`, `OwnerId`, `Status`, `PreviousStatus`, `Reason`, `Changes`, `Actor`. A reveal does not start it, and the input holds no number.
- **Bank Account Status Changed** (event, category Banking): starts with Bank Account Changed when the status moves, such as when an account is verified, fails, is closed, or goes back to unverified after a number change. Its **Status** setting limits it to one new status (`Verified`, `Closed`...). Same input.
- Audit Trail category **Banking**: Bank account added, changed, number changed, status changed, removed, and number shown, with the user id and name of who did it. Each event records the masked number only.

## Checks

The **Checks** module prints checks for any module that pays by check: paychecks first, then agency and vendor payments or refunds. It keeps a check register numbered per bank account and never writes a bank file itself: the bank's positive pay file is a data pipeline over the **Check Register** data set.

### Check layouts

A `CheckLayout` (a named catalog record, kept in a document) says how checks print:

| Field | Notes |
| --- | --- |
| `Stock` | `Blank` (the layout prints the payer, the bank and the check line) or `PrePrinted`. |
| `Form` | `CheckOnTop`, `CheckInMiddle`, `CheckOnBottom` (one check and two vouchers on a letter sheet) or `ThreePerPage`. |
| `Fields` | The place of each field (`CheckFieldNames`: Date, Payee, PayeeAddress, Amount, AmountInWords, Memo, Number, Payer, Bank, Signature, SecondSignature) in points from the top left of the check: `X`, `Y`, `Width`, `FontSize`, `Hidden`. A new layout starts with places that stay above the check line's clear band (`CheckLayoutDefaults`). |
| `OffsetX`, `OffsetY` | Calibration in points (at most 72 either way). |
| `PrintMicr`, `MicrCountry`, `UseAuxiliaryOnUs`, `MicrFontSize`, `MicrSymbolCharacters` | The check line: on or off, the country format, business (check number in the auxiliary on-us field) or personal format, the font size, and the four characters the font draws the transit, amount, on-us and dash symbols on (`ABCD` in most fonts). |
| `PayerName`, `PayerAddress` | Printed on blank stock. The address is entered with the shared address editor of the Addresses module (`IAddressEditorService`), in its country's format; the designer previews it. |
| `SecondSignatureAbove` | The amount above which a second signature line prints. |
| `HasSignature`, `HasSecondSignature`, `HasMicrFont` | Whether the files are stored. |

The editor's **Check designer** draws the check at 612 by 264 points with sample values and puts each field where it prints. A field is dragged to move it, its right edge to change its width, and the chosen field is edited in a bar above the check (place, width, text size, left off) or moved with the arrow keys. It shades the check line's clear band and outlines a field that enters it in red, and it follows the paper, payer, check line and calibration as they are typed. The script (`check-layout-designer`, registered by `ChecksResourceManagementOptionsConfiguration`) reads and writes the form's own `Fields[i]` inputs, which stay under **Exact positions**, so the form posts what the designer shows and works without the script.

The signature images (PNG or JPEG, up to 1 MB) and the check line font (TrueType, up to 2 MB) are uploaded on the layout and kept in an encrypted tenant file store (`ICheckFileStore`, `EncryptedFileStore` with the purpose `Checks.Files`, under `App_Data/Sites/{tenant}/Checks`), never in the media library, which serves files to anyone with the URL.

**No check line font ships with the module.** A site uploads a MICR E-13B TrueType font it is licensed to use. Until it does, a layout with **Print the check line** on refuses to print and says why, and the layout list flags it.

### The check register

A `CheckRecord` is one check: `BankAccountId` (one of the business's own accounts, `Owner` empty), `BankAccountName`, `Number`, `Payee` (a `PartyRef`), `PayeeName`, `PayeeAddress` (`Address`), `Amount`, `Currency` (the account's), `CheckDate` (a pure date), `Memo`, `SourceKind` and `SourceId` (what it pays, such as `PayStatement`), `AccessScope` (who within the source may see it, such as the pay group of a paycheck), `Status`, the dates of each change, `ReissuedById`/`ReissueOfId`, and `IsHistorical` for imported checks. It never holds an account number. Checks are YesSql documents in the `Checks` collection with `CheckRecordIndex`, which also holds `AccessKey` (the source kind and access scope) and `ModifiedUtc` (when the check last changed, stamped when it is issued too).

`CheckStatuses.Transitions`:

| From | To |
| --- | --- |
| `Issued` | `Printed`, `Voided`, `Reissued` |
| `Printed` | `Voided`, `StopPayment`, `Cleared`, `Reissued`, `Stale` |
| `StopPayment`, `Stale` | `Voided`, `Reissued`, `Cleared` |
| `Reissued` | `Cleared`: the bank paid the replaced check anyway; the register flags it **Paid after reissue** |
| `Voided`, `Cleared` | none |

Numbers come from `INumberSequence`, one series per bank account (`Checks:{bankAccountId}`), starting at 1001. **Banking > Checks > Next numbers** sets the next number when new stock is loaded and refuses a number a check of the account already has. A voided check keeps its number; a reissue writes a new check with the next number and links both.

### Rules and extension points

`ICheckService` (no permission checks; the caller checks): `IssueAsync`, `GetNextNumberAsync`, `SetNextNumberAsync`, `MarkPrintedAsync` (issued checks only), `MarkReprintedAsync` (a printed check printed again, with a reason; change kind `Reprinted`), `VoidAsync`, `ReissueAsync` (the new check is dated today in the site's time zone), `StopPaymentAsync`, `MarkClearedAsync`, `MarkStaleAsync` and `MarkStaleChecksAsync(ageInDays)` (every printed check dated more than that many days before today, in the site's time zone). Issuing refuses an account another party owns, a closed or failed account, an account without numbers, another currency and an amount that is not more than zero. A historical check is never printed or reissued, but can still be voided, stopped, cleared or go stale.

#### Who sees which checks

A check written by another module carries its `SourceKind` and an `AccessScope`. The module registers an `ISourceAccess` for its source kind (`AddSourceAccess<T>()`, the shared seam in `CrestApps.OrchardCore.Abstractions` that Disbursements applies too), which returns, for a person and a level (`View` or `Manage`), all of its records, none, or those of some access scopes (`SourceAccessScope`). `ISourceAccessService` applies every source access in one place: the register page (`ICheckRecordStore.ListPageAsync` with the person's `SourceAccessFilter`, run as a condition on `CheckRecordIndex` through `SourceAccessExpressions`), the Check Register data set (`RestrictAsync`), the register deployment step (which, run without a signed-in person, writes only the checks no source restricts), and every action on a check by its id (print, reprint, void, stop, cleared, reissue), which answers not found for a check the person may not act on. Checks written by hand, and those of a source kind without a source access, are visible to anyone the permissions allow. Payroll implements it for paychecks.

| Contract | Register with | Use |
| --- | --- | --- |
| `ISourceAccess` | `AddSourceAccess<T>()` | Who may see and act on the checks (and disbursements) of one source kind. |
| `ICheckVoucherSource` | `AddCheckVoucherSource<T>()` | The voucher (stub) of the checks of one source kind: a title, column headings and rows. Payroll supplies earnings, deductions, taxes and year-to-date totals. Without one, the voucher shows the payee, amount and memo. |
| `ICheckObserver` | `AddCheckObserver<T>()` | Told after each issue, print, void, stop, clear, reissue or stale change. |
| `ICheckMicrFormat` | `AddCheckMicrFormat<T>()` | The check line of one country. The United States format is built in. |
| `IAmountInWords` | `AddAmountInWords<T>()` | The legal line in one language, with the currency's name. English is built in ("One thousand two hundred thirty-four and 56/100 Dollars"). |
| `ICheckPrinter` | built in | Prints checks with a layout to one PDF. |

#### The United States check line

`UnitedStatesCheckMicrFormat` places characters by position, counted in 1/8 inch from the right edge of the check, position 1 starting 5/16 inch from the edge (`MicrLine.LeftOf`): the amount field (1 to 12) is left blank for the bank to encode; the account number ends at 15 with the on-us symbol at 14; the routing number sits between transit symbols at 43 and 33; position 44 stays blank; on a business check the check number, on at least six digits, sits between on-us symbols from 45. On a personal check the check number follows the on-us symbol after the account number. The baseline is 1/4 inch above the bottom of the check, inside the 5/8 inch clear band.

A print run lays out the check line of every check before it draws anything, so a routing or account number the line can't hold stops the run with the name of the bank account. Reading the account number is a Banking reveal, done once per bank account per print run: the Audit Trail entry names the check numbers and the print run, whose id the controller creates before printing and stores on the checks (`PrintRunId`).

### Admin screens and permissions

| Permission | Allows |
| --- | --- |
| `ManageCheckLayouts` | **Banking > Check Layouts**. Security critical. |
| `ManageChecks` | **Write a check** (the payee's optional address uses the shared address editor, in its country's format, so Checks depends on the Addresses feature), print (the list's bulk action **Print with** and the layout's name), reprint, void, reissue, stop payment, mark cleared, **Next numbers**. Security critical. |
| `ViewCheckRegister` | **Banking > Checks**, the register, filtered by bank account and status and searched by payee or number. |

A print returns one PDF (`Cache-Control: no-store`) and records the checks as printed in one print run (`PrintRunId`). The bulk print refuses a check already printed: **Reprint** in its menu asks for the layout and a reason, prints it, and records a `Reprinted` change. A spoiled sheet can also be reissued from the check's menu. A new check is dated today in the site's time zone, and **Mark cleared** records today's date in that zone.

### Positive pay and reports

With Data Pipelines on, the **Checks** data source has a **Check register** data set (requires `ViewCheckRegister`): bank account, number, payee, amount, currency, memo, status, a **Void** flag (voided, stopped or reissued), source, and the check, issue, print, void and cleared dates. The data set's date column is the indexed `ModifiedUtc`, so a date filter on **Changed** narrows the read itself. The **Checks: positive pay file** recipe (`ChecksPositivePay`) adds a draft pipeline that reads the checks changed since the last successful run (**Read only new rows** on `ModifiedUtc`) and writes a CSV; add the bank's SFTP or web API step, publish it and schedule it with a workflow timer. A bank whose positive pay layout is fixed-width registers an `IDataFileFormat`; the module writes no file itself.

### Recipes and deployment

- `CheckLayouts` (step and deployment step **Check layouts**): layouts by id or name, without files.
- `CheckRegister` (opt-in deployment step **Check register**): every check the person running the plan may see; run from a request it needs `ViewCheckRegister`, and without it the plan leaves the register out and says so. Imported checks are history (`IsHistorical`): they keep their numbers and are never printed or reissued. A check whose bank account and number already exist is skipped.

Both steps have schemas for CrestApps Recipes.

### Workflows

Events (category Checks): **Check Issued**, **Check Printed** (also a reprint, `Change` = `Reprinted`), **Check Voided**, **Check Stop Payment** and **Check Reissued** (`ReplacedById` is the new check), **Check Stale** (by hand or by Mark Stale Checks) and **Check Cleared**. Input: `CheckId`, `Number`, `BankAccountId`, `PayeeName`, `Amount`, `Currency`, `CheckDate`, `SourceKind`, `SourceId`, `Status`, `Change`, `ReplacedById`, `Reason`, `Actor`.

Task: **Mark Stale Checks** (`AgeInDays`, 180 by default) marks stale every printed check dated more than that many days before today, in the site's time zone, and writes the count to the workflow output `StaleChecks`. Run it from a Timer Event once a day.

A bank account checks are drawn on can't be deleted (`CheckBankAccountUsage`, an `IBankAccountUsage`): close it instead.

## Disbursements and Direct Deposit

The **Disbursements** module records the money the business sends out and sends it in batches through a provider. Payroll net pay and Marketplace vendor payouts (through the Stripe Connect transfer provider) both send money through it. It never holds the business's money: it writes the bank file, issues the checks or records a payment made elsewhere.

### The disbursement

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

### The rules: `IDisbursementService`

| Member | Does |
| --- | --- |
| `CreateAsync(DisbursementRequest)` | Records a disbursement, or returns the one its idempotency key already gave (`AlreadyExisted`). The key is the request's, or `SourceKind:SourceId:Method:DestinationBankAccountId`. Refuses: an unknown method, no payee name, an amount not more than zero (a prenote carries zero), a source account that is not the business's, closed, failed or without numbers, and for a bank file a payee account without numbers, not verified (a prenote may go to an unverified one) or in another currency. One request at a time per key (an Orchard distributed lock from the read to the commit), so two at once never both create. The key is the attempt: the same key with another payee, amount, currency, method or destination is refused, and once its disbursement failed, was cancelled, returned or reversed a new attempt needs a new key (the error says so). |
| `CancelAsync` | Cancels a pending disbursement. |
| `CreateBatchAsync(provider, ids, effectiveDate[, entryDescription])` | Batches pending disbursements of the provider's method from one source account, currency, source kind and access scope (so who may see a batch is one rule the list queries); the others are listed as problems. The arrival date can't be in the past (the site's day). The entry description, such as PAYROLL, is what a bank file prints for the batch instead of the account's own. |
| `ReleaseBatchAsync` | Hands the batch to its provider: the file is stored encrypted (`IDisbursementFileStore`, purpose `Disbursements.Files`), trace numbers and check ids are kept, statuses move. One release at a time per batch (a lock, and the batch read again inside it); the batch is saved as `Releasing` before the provider is called, and a provider uses the batch id (or each disbursement's key) as its idempotency key. A refusal reopens the batch; a throw leaves it `Releasing`, which is never sent again until someone checks with the provider and reopens it (`ReopenReleasingBatchAsync`, **Reopen after checking** on the batch list). |
| `CancelBatchAsync`, `SettleBatchAsync`, `SettleReleasedBatchesAsync(ageInDays)` | Cancel an open batch (its disbursements are pending again, reported as `Unbatched`); record a released batch as paid; settle every released batch whose age, counted in the site's days from the later of its arrival date and its release, is reached. |
| `MarkSettledAsync`, `MarkClosedAsync` | Move one disbursement. |
| `ApplyReturnsAsync`, `ApplyChangeNoticesAsync` | A return marks the disbursement returned with its code (the text is localized where it shows, `NachaReasonCodes`); a notice of change (C01, C02, C03, C05, C06, C07) corrects the payee's bank account, which is unverified again and its observers are told. The overloads take the person's source access filter (a disbursement they may not act on reads as not found) and whether they may manage bank accounts (without it a notice is reported for someone who may). |

Each bank file that leaves the site, downloaded or delivered by the **Read a disbursement batch file** step, is recorded with the batch and who (never a number) in the log and, with Audit Trail on, as a **Bank file sent** event; a delivery also stamps the batch with when and by which pipeline, shown on the batch list. A bank file needs the company entry description, refuses an account number longer than 17 characters instead of cutting it, and refuses a batch whose arrival date has passed.

The service checks no permission; the caller does. Observers (`IDisbursementObserver`, `AddDisbursementObserver<T>()`) are told every change with who made it.

### Providers

| Provider | Feature | Does |
| --- | --- | --- |
| **Bank file (ACH)** (`Nacha`, method `Ach`) | Bank File (ACH) | Writes the file in the Nacha format: a PPD batch for people and a CCD batch for businesses, prenotes (codes 23 and 33, no amount), an optional offset debit per batch, entry hashes, totals, trace numbers (originating bank and a sequence), and lines of nines to fill the block of ten. The file id modifier letters the files of the business's day (A, B...). United States dollars only. Each account number is read once per file, as a Banking reveal the Audit Trail records. |
| **Printed check** (`Check`) | Disbursements, when Checks is on | Issues one check per disbursement for the same record and access scope, dated the effective date. A cleared check settles the disbursement, a voided or stopped one cancels it, and a reissue moves it onto the new check. |
| **Paid outside the system** (`Manual`) | Disbursements | Releasing records the payments as settled. |

A module adds a provider with `AddDisbursementProvider<T>()` (`IDisbursementProvider`: name, display name, method, `SubmitAsync`).

The bank file settings (`NachaOriginSettings`: immediate destination and origin with names, company name and identification, entry description, offset) are kept on one of the business's bank accounts, in its properties, and edited in the **Bank file (ACH)** card of the account page.

#### Prenotes

With Bank File (ACH) on, Banking's **Check with** offers **Test entry (prenote)** (`NachaPrenoteVerifier`, an `IBankAccountVerifier`). It records a zero-amount disbursement from the first business account set up for bank files and puts the account in **Verification sent**. When the batch settles without a return the account is **Verified**; when the bank returns the entry it is **Failed** (`NachaPrenoteObserver`). A new number gets a new test entry.

#### Return and change files

**Disbursement Batches > Import a return file** reads the bank's file (`NachaReturnFileReader`): each return addenda (type 99) marks its disbursement returned, each notice of change (type 98) corrects the payee's account.

### Who sees which disbursements

Like Checks, Disbursements applies the shared source access seam (`ISourceAccess` with `AddSourceAccess<T>()`, `ISourceAccessService`, in `CrestApps.OrchardCore.Abstractions` and `.Core`): the module that writes disbursements, such as Payroll for its pay statements, decides who may see (`View`) or act on (`Manage`) the disbursements of each access scope. It applies to the list (`IDisbursementStore.ListPageAsync`, a condition on `DisbursementIndex.AccessKey`), the data set, the export, every action by id (not found) and the batches (a batch is listed and acted on only when every one of its disbursements is allowed). Disbursements written by hand, and source kinds without a source access, are visible to anyone the permissions allow.

### Admin screens and permissions

| Permission | Allows |
| --- | --- |
| `ManageDisbursements` | **Record a disbursement**, batch (the list's bulk action **Batch to send by** and the provider's name, with the arrival date), cancel, **Banking > Disbursement Batches** (release, download the bank file, mark paid, cancel, import a return file). Security critical. |
| `ViewDisbursements` | **Banking > Disbursements**, filtered by method and status and searched by payee or trace number. |

A bank file is downloaded with `Cache-Control: no-store`. A bank account money was sent from or to can't be deleted (`DisbursementBankAccountUsage`).

### Data pipelines and reports

With Data Pipelines on, the **Disbursements** data source has a **Disbursements** data set (requires `ViewDisbursements`, restricted by source access, date column the indexed `ModifiedUtc`): payee, amount, currency, method, provider, status, test entry, accounts' ids, source, batch, trace number, return code, memo and dates. No account number is a column.

The Bank File (ACH) feature adds one pipeline step, **Read a disbursement batch file** (`ReadDisbursementBatchFile`, a source of files): the bank files of the batches released since the last successful run, for Orchard's **Upload to an SFTP server**, FTP, web API or e-mail steps. The publisher needs `ManageDisbursements`, and a batch is read only when the publisher may act on all its disbursements. The watermark is saved only when the run succeeds, so a failed delivery sends the same files again.

### Recipes and deployment

- `Disbursements` (recipe step, and opt-in deployment step **Disbursements**): imported disbursements are history (`IsHistorical`), never batched or changed; one whose idempotency key exists is skipped. The export needs `ViewDisbursements` when run from a request and writes only what the person may see; run without a signed-in person it writes only the disbursements no source restricts. No account number is written.

Both steps have schemas for CrestApps Recipes.

### Workflows

Events (category Disbursements): **Disbursement Created**, **Disbursement Added to Batch**, **Disbursement Removed from Batch** (its open batch was cancelled or reopened), **Disbursement Submitted**, **Settled**, **Returned**, **Failed**, **Disbursement Canceled** (such as when its check is voided or stopped) and **Disbursement Reversed**. Each change starts exactly one event. Input: `DisbursementId`, `PayeeName`, `Amount`, `Currency`, `Method`, `Status`, `Change`, `SourceKind`, `SourceId`, `BatchId`, `TraceNumber`, `FailureCode`, `FailureReason`, `IsPrenote`, `EffectiveDate`, `Actor`. Never an account number.

Batch events (category Disbursements, from `IDisbursementBatchObserver`): **Disbursement Batch Created**, **Released** (a release the provider paid in full starts Settled too), **Reopened** (a batch stuck in sending; `Reason` says why), **Canceled** and **Settled**. Input: `BatchId`, `Provider`, `SourceBankAccountId`, `SourceBankAccountName`, `EffectiveDate`, `Count`, `TotalAmount`, `Currency`, `Status`, `PreviousStatus`, `SourceKind`, `AccessScope`, `FileName`, `ProviderReference`, `Change`, `Reason`, `Settled` (how many it paid), `Actor`. Never the file or an account number. **Disbursement Returns Applied** starts when a returns file is applied, with `Returned`, `Problems` (a count), `ProblemMessages` and `Actor`; each returned disbursement also starts Disbursement Returned.

Task: **Settle Disbursement Batches** (`AgeInDays`, 3 by default) records as paid the released batches older than the age that the bank did not return, which also verifies the accounts of their test entries. Run it from a Timer Event once a day; it writes the count to the workflow output `SettledDisbursements`.
