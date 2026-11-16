---
sidebar_label: Users
sidebar_position: 1
title: Users
description: Enhanced user management with display name customization and avatar support for Orchard Core.
user_manual:
  - user-manual/administration/users
---

| | |
| --- | --- |
| **Feature Name** | Users Core Components |
| **Feature ID** | `CrestApps.OrchardCore.Users` |

Extends the Orchard Core Users module by adding functionality to cache users.

<YouTube video="users-roles" title="Video overview of users, roles, names and avatars" />

:::note[Note]
This feature is enabled by dependency only.
:::

## Reusable user picker

The module ships a reusable **`UserPicker`** view component: a searchable user selector you can drop into any admin screen so an operator can find and select one or more users, instead of typing an id. It shows the top matches and filters as you type (server-side search), and it resolves the currently-selected users so their display names appear when the picker first renders.

Invoke it from a Razor view:

```razor
@await Component.InvokeAsync("UserPicker", new
{
    name = Html.NameFor(m => m.OwnerUserId).ToString(),
    selectedValues = new[] { Model.OwnerUserId },
    valueType = "userId",
    multiple = false,
    buttonText = T["Select a user"].Value,
    searchPlaceholder = T["Search users"].Value,
})
```

Parameters:

| Parameter | Purpose |
| --- | --- |
| `name` | The form field name the selection posts under. Use `Html.NameFor(...)` inside a display driver so the value binds back under the driver's prefix. |
| `selectedValues` | The currently-selected values (matching `valueType`), used to pre-populate the picker. |
| `valueType` | What the picker stores and posts: `userId` (default), `userName`, or `normalizedUserName`. |
| `multiple` | Allow more than one user to be selected. |
| `roles` | Restrict the searchable users to these role names. |
| `label`, `buttonText`, `searchPlaceholder` | Optional display text. |

The picker is backed by the shared user-search endpoint (`Admin/api/crestapps/users/search`, which returns the top 50 enabled matches) and renders through the shared **`ItemSelector`** component, so the **CrestApps Resources** feature must be enabled wherever the picker is used.

The endpoint answers only users who hold the **Search users in user pickers** permission (`SearchUsers`, `UserSearchPermissions.SearchUsers` in `CrestApps.OrchardCore.Users.Abstractions`). `AccessAdminPanel` implies it, so every role that can open the admin panel keeps its pickers, while a signed-in storefront customer gets `403 Forbidden`. An access that a handler grants only for one request, such as the worker access to the **My Work** pages, does not include it.

Modules that look up users for their own pickers use the same search through `IUserSearchService` (`CrestApps.OrchardCore.Users.Abstractions`): `SearchAsync(UserSearchQuery)` finds enabled users by user name (and e-mail when `MatchEmail` is set), optionally in some roles, ordered by user name; `GetAsync(userIds)` resolves the users behind stored ids. Register it with `services.AddUserSearchService()` from `CrestApps.OrchardCore.Users.Core`; the service does not authorize, so the endpoint that exposes it checks `SearchUsers` or its own permission first. Calendars, Assets and the report designer use it.

For example, the [Messaging Workspace](../omnichannel/messaging-workspace) uses `UserPicker` to choose the agent an SMS number routes inbound messages to.

## Role grants

`IRoleGrantService` (`CrestApps.OrchardCore.Users.Abstractions`) puts users in roles on behalf of the things that grant them, such as a membership or a subscription. Register it with `services.AddRoleGrants()` from `CrestApps.OrchardCore.Users.Core`; it is safe to call from every module that grants roles.

- `GrantAsync(userId, roleName, source, sourceId)` records the grant and puts the user in the role when they are not in it.
- `RevokeAsync(userId, roleName, source, sourceId)` removes the grant, and takes the user out of the role only when no other source still grants it and a grant put them in it. A role the user was in before any grant is kept. It then updates the user's security stamp.
- `ListAsync(userId)` lists the grants.

Grants are kept on the user's account (`UserRoleGrants`), so no table is added. A module that granted roles before grants were recorded registers an `IRoleGrantSource`, which reports those grants the first time the service touches a user. Memberships and Subscriptions use it.

## User Display Name

| | |
| --- | --- |
| **Feature Name** | User Display Name |
| **Feature ID** | `CrestApps.OrchardCore.Users.DisplayName` |

Provides a way to display a user's display name, built from a display name, first, middle and last name
stored on the user (`UserFullNamePart`), according to the site setting under **Settings** -> **User Display Name**
(`DisplayNameSettings`). The setting page requires the `ManageDisplaySettings` permission (*Manage the user display
name settings*), which is granted to the **Administrator** role by default. The name boxes on the user editor are
shown only to users who hold `EditUsers` for that user.

How administrators choose the format, require name parts and set a person's name is described in the User Manual:
[Users, Display Names and Avatars](../user-manual/administration/users.md).

| Format (`DisplayNameSettings.Type`) | Result |
| --- | --- |
| `Username` (default) | The user name. |
| `DisplayName` | `UserFullNamePart.DisplayName`, or the user name when it is empty. |
| `FirstThenLast` | First, middle (when used) and last name, or the user name when all are empty. |
| `LastThenFirst` | `Last, First Middle`, or the user name when all are empty. |
| `Other` | A Liquid template with `User`, `FirstName`, `MiddleName`, `LastName`, `DisplayName` and `UserName` in scope. |

Each name part (`DisplayName`, `FirstName`, `MiddleName`, `LastName` on `DisplayNameSettings`) is `None`,
`Optional` or `Required`, which controls whether the user editor shows the box and whether it is validated.

If you want to display the user display name in your project via code, you may do so by using the `IDisplayNameProvider` interface.

### Liquid Support

When the "Liquid" feature is enabled, a new helper to display the user's display name will become available (i.e, `display_name`). Here is an example:

```
{{ Model.User | display_name }}
```

### Enhanced User Search

When both the `Users` and `User Display Name` features are enabled, the search functionality within the Users UI will expand to include fields such as display name, first name, middle name, or last name in the search results.

Additionally, when the `UserPickerField` field is used, the display text will show the display name.

### Re-indexing Users

A recipe step is available to re-index users. This step will update all enabled users by default using a batch size of 250. To re-index all of your users run the following recipe:

```json
{
  "steps": [
    {
        "name": "indexUsers",
        "includeDisabledUsers": false,
        "batchSize": 250
    }
  ]
}
```

The `includeDisabledUsers` parameter within the `indexUsers` step is optional and allows for the indexing of disabled users if desired. Moreover, the `batchSize` parameter provides the ability to adjust the update batch size. The default value is set at 250 and can be increased to 1000 if necessary.

## User Avatar

| | |
| --- | --- |
| **Feature Name** | User Avatar |
| **Feature ID** | `CrestApps.OrchardCore.Users.Avatars` |

Provides a way to display a user's avatar.

Avatar settings live under **Settings** -> **User Avatars** (`UserAvatarOptions`: `Required`, `UseDefaultStyle`) and
require the `ManageAvatarSettings` permission (*Manage the avatar settings*), granted to the **Administrator** role by
default. The avatar box on the user editor accepts only image extensions allowed by the media options and is shown
only to users who hold the Orchard Core `ManageMedia` permission. See the
[User Manual](../user-manual/administration/users.md#avatars) for the administrator steps.

## Sign in with an email code

| | |
| --- | --- |
| **Feature Name** | Sign in with an email code |
| **Feature ID** | `CrestApps.OrchardCore.Users.EmailCodeSignIn` |

Adds **Email me a sign-in code** to the login form (`EmailCodeLoginFormDisplayDriver`) and the pages `sign-in/email` (`EmailSignIn`) and `sign-in/email/code`. `EmailSignInCodes` keeps a SHA-256 hash of each six-digit code in `IDistributedCache`, per tenant, for ten minutes and five tries, and limits, on the shared attempt limiter, sends to ten per client network and then three per email, and checks to thirty per client network, every fifteen minutes (policies `email-signin-send-network`, `email-signin-send-email` and `email-signin-verify-network`, which `CrestApps_Throttling:AttemptPolicies` can change). Both pages are in the `sign-in-code` rate-limit group. A wrong code counts toward the account's lockout; posting digits when no code is live does not. The request page answers the same way, and in the same time, for any email: the code is mailed after the request ends (`IEmailSignInCodeSender`). The code page is rendered from the post, so the email is never in a URL. Users who are disabled, locked out, have two-factor authentication on, are in a role listed in `CrestApps:Users:EmailSignIn:ExcludedRoles` (Administrator by default), or sign in only through an external provider (external logins and no password) are not signed in by a code (`EmailSignInEligibility`). With **Disable local login** on in the login settings the pages answer 404. The code page raises Orchard's login events except `LoggingInAsync`, whose handlers (reCAPTCHA among them) check fields of the password form, so reCAPTCHA does not apply to code sign-in. It depends on Orchard Core Users and Email.

A code is for one account and its own address. The typed address must equal the account's stored address once both are trimmed and upper-cased, compared exactly: a database that compares text loosely (SQL Server's default collation treats `AE` as `Æ`, `TH` as `Þ` and `SS` as `ß`) finds an account for a look-alike address, and such an address gets no code. The code is keyed on the account's id, its hash holds the account's address, so it stops working if the address changes, and it is mailed to the stored address, never to what was typed.

A code proves the address by a round trip. What happens on the first proof of an address (no earlier confirmation link or code for that address, see `UserEmailOwnership`) depends on what the account had:

- **No password and no external login**, such as an account made from an order: the address is confirmed and recorded as proved, and the user is signed in. Recording it is what merges the guest orders placed with it, with the Customers feature on.
- **A password or an external login**: they were set before anyone proved the address and may belong to whoever registered with it or changed their email to it. The user is signed in with nothing removed, recorded or confirmed (confirming an unconfirmed address would let that password sign in on a site that requires confirmed addresses, so such an account still needs Orchard's confirmation link there), and a notice offers **confirm it's you**. Every account that existed before the marker is in this case, and keeps its password and logins.

The confirmation pages, `sign-in/email/confirm` (`EmailSignInConfirm`) and `sign-in/email/confirm/code` (`EmailSignInConfirmCode`, `EmailOwnershipController`), are for a signed-in user whom a code could sign in. They mail a second code to the account's address. The mailbox alone doesn't say whose the account's password and external logins are, so they are kept when the same request also shows them (`UserCredentialsProof`): the browser is signed in with the account's password or one of its own external logins (ASP.NET Core Identity's `amr=pwd` or `amr=mfa`, or the provider in `ClaimTypes.AuthenticationMethod`), or the password is typed in the optional field next to the code. A wrong password is refused before the code is looked at, so it doesn't spend the code, and counts toward the lockout. Then the address is recorded as proved and nothing is removed. Otherwise, when the code is right, `EmailOwnershipProof` removes the account's password and external logins, rotates its security stamp, records when its credentials were reset (`CredentialsResetUtc` on the user) and records the address as proved, all in one update. If that update fails, the request's session is reset and nothing of it is kept. Then every earlier sign-in ends: `CredentialsResetCookieValidation` wraps the application cookie's `OnValidatePrincipal` and refuses any sign-in cookie issued before the reset on every request, ahead of Identity's own security stamp check, which runs only every `SecurityStampValidatorOptions.ValidationInterval` (30 minutes by default; the reset time is kept in `IDistributedCache` for that long, after which the stamp check refuses those cookies itself). With the OpenID server on, the account's OpenID authorizations and tokens are revoked (`IUserSignInRevocation`); an access token already issued stays valid until it expires unless token validation checks its entry. The browser that confirmed is signed in again, which merges the guest orders, and the user is told to sign in with a code or set a new password through password recovery.

A followed confirmation link (`IUserEventHandler.ConfirmedAsync`) records the address as proved, without removing anything, when the account has no password or external login, or when the request shows them as above. On an account with credentials followed by someone who isn't signed in to it with them, Orchard Core still confirms the address but nothing is recorded, so no guest orders move: someone may have registered with another person's address and a password of their own, and that person may follow the "confirm your account" mail. The owner confirms on the pages above. The link only confirms the address it was sent to (`EmailBoundConfirmationTokenProvider`). Both come from `AddEmailOwnershipConfirmation()` in `CrestApps.OrchardCore.Users.Core`, which this feature and the Customers feature call.

## User pictures for other modules

`IUserAvatarProvider` gives the address of a user's picture. The User Avatars feature registers one, and the user search (`IUserSearchService`) fills `UserSearchResult.AvatarUrl` from it, so modules show faces (for example the staff organization chart) without knowing where pictures are kept.

An `IUserAudienceProvider` can also leave users out of a search with `GetExcludedUserIdsAsync`, which only receives the users a search found. Staff uses it for the `StaffUnlinked` audience, so the staff editor offers only accounts that aren't linked to a staff member yet.
