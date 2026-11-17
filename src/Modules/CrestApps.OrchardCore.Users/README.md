# Users Core Components

Provides user components core services

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Users Core Components | `CrestApps.OrchardCore.Users` | Provides user components core services Enabled by the features that depend on it. |
| User Display Name | `CrestApps.OrchardCore.Users.DisplayName` | Provides a way to change how the user name is displayed. |
| Sign in with an email code | `CrestApps.OrchardCore.Users.EmailCodeSignIn` | Lets people sign in with a code emailed to them instead of a password. Accounts with two-factor sign-in keep using their password. |
| Confirm email ownership | `CrestApps.OrchardCore.Users.EmailOwnership` | Lets a signed-in person confirm, with a code emailed to them, that their email address is theirs, so what was placed with it before, such as guest orders, joins their account. Enabled by the features that depend on it (Sign in with an email code, Customers). |
| User Avatar | `CrestApps.OrchardCore.Users.Avatars` | Provides a way to display an avatar for each user. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- `OrchardCore.ContentFields`
- `OrchardCore.Users`
- `OrchardCore.Email`
- `OrchardCore.Media`

## Extension points

- `IDisplayNameProvider` (CrestApps.OrchardCore.Users.Abstractions): Defines the contract for display name provider.
- `IEmailOwnershipConfirmationPage` (CrestApps.OrchardCore.Users.Core): The address of the "Confirm it's you" page for a user whose email isn't proved yet, for a module that hands over what belongs to an address (Customers links to it for every signed-in user whose address isn't proved).
- `IRoleGrantService` (CrestApps.OrchardCore.Users.Abstractions): Puts users in roles on behalf of the things that grant them, such as a membership or a subscription, and keeps a record of each grant by its source.
- `IRoleGrantSource` (CrestApps.OrchardCore.Users.Abstractions): A kind of thing that grants roles through `IRoleGrantService`, such as memberships or subscriptions.
- `IUserSearchService` (CrestApps.OrchardCore.Users.Abstractions): Finds users for user pickers and other lookups, so every module searches users the same way.
- `IUserCacheService` (CrestApps.OrchardCore.Users.Core): Defines the contract for user cache service.

## Documentation

See [Users](../../CrestApps.Docs/docs/modules/users.md) in the technical manual.
