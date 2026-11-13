# Banking

Bank accounts for any module that sends money by bank: the business's own accounts and those of the people and organizations it pays.

## Features

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Banking` | Banking | CrestApps Resources |

- One `BankAccount` record per account, owned by a `PartyRef` (a staff member, a user, an account) or by the business itself.
- The account number is a `ProtectedValue` (Data Protection, purpose `Banking.AccountNumber`); screens show only its last digits.
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
