# Accounts

Keeps the businesses and households that contacts, orders, assets and work orders belong to as Account content items, with a picker, a search and an account page that other modules add to.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Accounts | `CrestApps.OrchardCore.Accounts` | Keeps the businesses and households that contacts, orders, assets and work orders belong to as Account content items, with a picker, a search and an account page that other modules add to. |
| Account Members | `CrestApps.OrchardCore.Accounts.Members` | Lets users sign in for an account: who its members are, and which of them manage it for the others. Other modules read the members to know who acts for an account. |
| Account Locations | `CrestApps.OrchardCore.Accounts.Locations` | Keeps the places an account has, such as its head office, its warehouses or the sites where it is serviced, with addresses checked against each country's rules. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.Resources`](../CrestApps.OrchardCore.Resources/README.md)
- `OrchardCore.Contents`
- `OrchardCore.ContentTypes`
- `OrchardCore.Lists`
- `OrchardCore.Title`
- `OrchardCore.Users`
- [`CrestApps.OrchardCore.Addresses`](../CrestApps.OrchardCore.Addresses/README.md)

## Extension points

- `IAccountChildTypeRule` (CrestApps.OrchardCore.Accounts.Abstractions): Decides which content types can belong to an account, and so are offered in an account type's list.
- `IAccountContactFinder` (CrestApps.OrchardCore.Accounts.Abstractions): Finds one of an account's contacts by email.
- `IAccountContentTypeProvider` (CrestApps.OrchardCore.Accounts.Abstractions): Answers which content types are account types and which can belong to an account.
- `IAccountMemberService` (CrestApps.OrchardCore.Accounts.Abstractions): Finds the account a user signs in for.

## Documentation

See [Accounts](../../CrestApps.Docs/docs/modules/accounts.md) in the technical manual.
