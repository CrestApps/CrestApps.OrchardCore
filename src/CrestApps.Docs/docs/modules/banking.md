---
sidebar_label: Banking
title: Banking
description: "Bank accounts for any module that sends money by bank: the BankAccount record with its account number protected at rest, routing number checks per country, verification providers, the status table, observers, the audited reveal, My bank accounts, recipe and deployment steps with the Include sensitive data option, the Banking data source, the workflow event and the Audit Trail events."
user_manual:
  - user-manual/finance/bank-accounts
---

The **Banking** module keeps one record for a bank account: one of the business's own, such as the account payroll is paid from, or one of a person or organization the business pays, such as an employee's pay account. Modules that send money by bank (Checks, Disbursements, Payroll) build on it instead of keeping their own account numbers.

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Banking` | Banking | CrestApps Resources |

Projects: `CrestApps.OrchardCore.Banking.Abstractions` (the `BankAccount` model and the contracts other modules use), `CrestApps.OrchardCore.Banking.Core` (the store, index, rules and the United States routing check), `CrestApps.OrchardCore.Banking` (the module).

## The bank account

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

### Status

`BankAccountStatuses.Transitions` (a `TransitionTable<BankAccountStatus>`) allows these moves:

| From | To |
| --- | --- |
| `Unverified` | `VerificationSent`, `Verified`, `Failed`, `Closed` |
| `VerificationSent` | `Unverified`, `Verified`, `Failed`, `Closed` |
| `Verified` | `Unverified`, `Failed`, `Closed` |
| `Failed` | `Unverified`, `VerificationSent`, `Verified`, `Closed` |
| `Closed` | none: a closed account never opens again |

Only a `Verified` account can receive payments (`BankAccount.CanReceivePayments()`). A new routing or account number always takes the account back to `Unverified`, so money never goes to a number nobody checked. An account may be stored without numbers (an import without sensitive data writes it that way); it can then be neither checked nor paid until someone enters them.

## The rules: `IBankAccountService`

Write numbers and status through `IBankAccountService`, never by setting the fields:

| Member | Does |
| --- | --- |
| `ApplyNumbers(account, routingNumber, accountNumber)` | Checks both numbers for the account's country, normalizes them and protects the account number. A blank value keeps the stored one; a number equal to the stored one changes nothing. Returns the refused numbers and why. |
| `TrySetStatus(account, status, reason, verificationMethod)` | Moves an unsaved account when the table allows it. |
| `ChangeStatusAsync(...)` | The same, and saves. |
| `StartVerificationAsync(account, verifierName)` | Asks a verifier to check the account and saves the status it returns. |
| `RevealAccountNumberAsync(account, reason)` | Reads the full number, writes a log entry (the account id, who read it and why, never the number) and tells the observers. The log entry is written whether or not the Audit Trail is on; keep the Audit Trail on so each read is also an event people can search. Call it only for a user with **Reveal bank account numbers**, or for a file a payment needs. |
| `GetUsagesAsync(account)` | Why records of other modules point to the account, such as checks drawn on it. |
| `Verifiers` | The verifiers the enabled features registered. |

The service checks no permission; the caller does. `IBankAccountManager` (a catalog manager) stores the accounts and lists them by owner with `ListByOwnersAsync`.

## Extension points

| Contract | Register with | Use |
| --- | --- | --- |
| `IBankRoutingValidator` | `AddBankRoutingValidator<T>()` | The routing and account number checks of one country. The United States check (nine-digit ABA number with its 3-7-1 check digit, account numbers of up to 17 letters and digits) is built in. A country without one gets letters and digits, up to 34 characters. |
| `IBankAccountVerifier` | `AddBankAccountVerifier<T>()` | A way to check an account, such as a zero-amount test entry. The account page offers one **Check with** button per verifier. |
| `IBankAccountObserver` | `AddBankAccountObserver<T>()` | Told after each save (`Created`, `Updated`, `NumberChanged`, `StatusChanged`, `Deleted`) and after each reveal (`NumberRevealed`), with who did it and why. It never receives the number in clear text. Payroll uses it to send a security notice when a pay account changes. |
| `IBankAccountUsage` | `AddBankAccountUsage<T>()` | Tells that a module's records point to an account. An account in use can't be deleted: the handler refuses (`DeletingAsync` throws with the reasons) and the admin page says why and offers to close it instead. Checks registers one for the accounts checks are drawn on. |
| `IUserPartySource` (Parties) | `AddUserPartySource<T>()` | Tells which parties a signed-in user is. Staff registers one, so a person's staff record owns their pay accounts on **My bank accounts**. |

## Admin screens and permissions

| Permission | Allows |
| --- | --- |
| `ManageBankAccounts` | **Banking > Bank Accounts**: add, change, verify, close and delete every account that nothing points to. Full numbers stay hidden. |
| `ManageOwnBankAccounts` | **Banking > My Bank Accounts**: add, change and close one's own accounts (owned by the user or a party the user is). No status, no reveal, nobody else's. |
| `RevealBankAccountNumbers` | **Show the full number** on the account page. Security critical; every reveal is an Audit Trail event. |

Administrators get all three. The owner picker searches every party kind the enabled features offer, through `{admin prefix}/api/banking/owners/search` (route name `CrestApps.Banking.OwnerSearch`, under the site's admin URL prefix, requires `ManageBankAccounts`). The editor saves an owner only of a kind a module provides and that exists. On **My Bank Accounts** a person can own an account only as one of the parties they are, never sets its status, and can't change a closed account. The reveal page is sent with `Cache-Control: no-store`.

## Recipes and deployment

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

`OwnerKind` and `OwnerId` set the owner. Numbers are read in clear text and protected with the importing site's keys, since protected values can't be read on another site. A status is kept only when the numbers come with it; an account without numbers is imported as unverified.

The **Bank accounts** deployment step exports every account without its numbers or status. Run from a request, it needs `ManageBankAccounts`: without it the plan leaves the accounts out and says so, and the step can't be added to a plan. Its **Include the account numbers** option (`IncludeSensitiveData`) writes them in clear text; only a user with `RevealBankAccountNumbers` can turn it on or run a plan that uses it, and each exported number is recorded in the Audit Trail as a reveal. Both steps have schemas for CrestApps Recipes.

## Reports and data pipelines

With Data Pipelines on, the **Banking** data source has a **Bank accounts** data set (requires `ManageBankAccounts`): owner, holder, bank, type, country, currency, status, dates and the last digits of the account number. The routing and account numbers are never columns.

## Workflows and Audit Trail

- **Bank Account Changed** (event, category Banking): input `BankAccountId`, `Name`, `OwnerKind`, `OwnerId`, `Status`, `PreviousStatus`, `Changes`, `Actor`. A reveal does not start it, and the input holds no number.
- Audit Trail category **Banking**: Bank account added, changed, number changed, status changed, removed, and number shown, with the user id and name of who did it. Each event records the masked number only.
