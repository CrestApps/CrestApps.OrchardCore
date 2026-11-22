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

## User Audiences

The **User Audiences** feature (`CrestApps.OrchardCore.Users.Audiences`) lets administrators define audiences, groups of people chosen by rules, that other features target, such as required learning. **Content > User Audiences** lists them beside the audiences modules register in code (`IUserAudienceProvider`, such as `Staff`), shows the people in each one, and edits the ones administrators define. Its permission is `ManageUserAudiences`.

An audience has a technical name that never changes, a name, a description, a join (a person matches every rule, or any one) and rules. A rule is a rule source with the values chosen (a person matches any of them) and, for some sources, a number.

| Rule source | Registered by | Matches |
| --- | --- | --- |
| `Role` | Users | Users in any of the roles |
| `Team` | Teams | Members of the teams today, with their sub-teams |
| `StaffHireDate` | Staff | Active staff linked to a user, hired at least the number of days ago (on the site's day) |
| `StaffEmploymentType` | Staff | Active staff linked to a user, with any of the employment types |
| `SkillHolder` | Skills | People who hold the skills today, themselves or through their staff record |
| `CertificationHolder` | Skills | People with a valid record of the certifications |
| `CourseLearner` | Learning | People enrolled in the courses, active or completed |
| `CohortMember` | Users (Cohorts) | People in any of the cohorts |

Code reads audiences through `IUserAudienceService` (`CrestApps.OrchardCore.Users.Abstractions`), registered by the feature with `services.AddUserAudiences()`:

- `ListAsync()` lists every audience, the module ones and the defined ones.
- `GetMembersAsync(name, skip, take)` pages the user ids of the members, with their count.
- `IsMemberAsync(name, userId)` checks one person without listing the others.
- `FindDefinitionAsync`, `SaveDefinitionAsync` (which returns the reasons a definition is refused) and `DeleteDefinitionAsync` manage the defined ones.

A module adds a kind of rule by implementing `IUserAudienceRuleSource` (`Name`, `DisplayName`, an optional `NumberLabel`, `GetOptionsAsync`, `ResolveUserIdsAsync` and, when it can check one person faster, `IsMatchAsync`) and registering it with `services.AddUserAudienceRuleSource<T>()`. Registering it costs nothing when the feature is off. Membership is worked out when it is read, so it is always current; a feature that reacts to people joining or leaving an audience reconciles on a schedule.

The `UserAudiences` recipe step creates or updates defined audiences by name, and the **User audiences** deployment step exports them:

```json
{
  "name": "UserAudiences",
  "Audiences": [
    {
      "name": "field-veterans",
      "displayName": "Field veterans",
      "join": "All",
      "rules": [
        { "source": "Team", "values": ["<team id>"] },
        { "source": "StaffHireDate", "number": 30 }
      ]
    }
  ]
}
```

## Cohorts

The **Cohorts** feature (`CrestApps.OrchardCore.Users.Cohorts`, depends on User Audiences) keeps cohorts: named groups of people an administrator keeps by hand, such as "2026 new hires", beside the audiences, whose rules decide who is in them. **Content > Cohorts** creates, renames and deletes them, lists their people, adds people with the user picker or many at once (user names or e-mail addresses, one per line), and removes them. Its permission is `ManageCohorts`.

Teams are not cohorts: a team is a part of the organization (a tree with a kind, a time zone and an address) whose members other modules supply, while a cohort is a flat list of any users, staff or not.

Code reads and changes cohorts through `ICohortService` (`CrestApps.OrchardCore.Users.Abstractions`), registered by the feature with `services.AddCohorts()`:

- `PageAsync`, `ListAsync`, `FindByIdAsync` and `FindByNameAsync` read cohorts; names are unique without regard to case.
- `CreateAsync`, `UpdateAsync` and `DeleteAsync` return the reasons a change is refused. A cohort something uses (an `ICohortUsage`, registered with `services.AddCohortUsage<T>()`) cannot be deleted.
- `PageMembersAsync`, `CountMembersAsync`, `ListMemberIdsAsync`, `ListForUserAsync` and `IsMemberAsync` read members.
- `AddMembersAsync` and `RemoveMembersAsync` return the people who actually joined or left, and tell each `ICohortMembershipObserver` (registered with `services.AddCohortMembershipObserver<T>()`) about only them, after the change is flushed. A deleted user leaves every cohort.

Cohorts and their members are documents of the `Cohorts` collection, one per cohort and one per member, with `CohortIndex` and `CohortMemberIndex`, so a cohort of thousands is paged from its index.

The feature registers the `CohortMember` audience rule (**Cohort member**), so every feature that targets audiences (required learning, layer rules, user pickers) can target cohorts. Learning links courses and learning paths to cohorts directly; see [enrollment by cohort](./learning#enrollment-by-cohort).

The `UserCohorts` recipe step creates or updates cohorts, matched by id and then by name, and adds the people it names by user name or e-mail; people already in a cohort stay. The **Cohorts** deployment step exports every cohort with its people by user name:

```json
{
  "name": "UserCohorts",
  "Cohorts": [
    {
      "Id": "new-hires-2026",
      "Name": "2026 new hires",
      "Description": "Everyone who started this year",
      "Members": ["ana", "ben@example.com"]
    }
  ]
}
```

## My account

The **My Account** feature (`CrestApps.OrchardCore.Users.Account`, turned on by the modules that need it, such as Commerce and Tickets) is the site's "my account" area: `/account` (route `CommerceAccount`, signed-in users only), a **My account** link in Orchard Core's user menu, and the `commerce-account` navigation menu that modules add their pages to with a `NamedNavigationProvider` (`UsersConstants.Account.MenuName`). Each page renders the `CommerceAccountPage` shape (`UsersConstants.Account.PageShape`), which frames it with the `CommerceAccountNavigation` menu; the home page shows a tile per page (`AccountOverview` shape). Both list the items in order of their positions (`AccountMenuItems.Arrange`, with Orchard Core's `FlatPositionComparer`), since Orchard Core's navigation manager leaves a named menu in the order its providers were registered. The names come from when the area belonged to Commerce and are kept, so themes and modules that use them keep working.

## Site notifications

With Orchard Core's **Notifications** feature on, the module adds the site's **Notifications** page (`/notifications`, route `UserNotifications`, `UsersConstants.Notifications.RouteName`), where a signed-in user reads their own notifications without the admin, where the notification center is. It needs the Notifications module's **Manage notifications** permission, which signed-in users have by default, and is linked as **Notifications** in the user menu on the site's pages and, with My Account on, in the account menu, framed as an account page.

The page is not a second inbox: it uses the Notifications module's own pieces.

- It reads the user's notifications with `INotificationManager`, newest first, all or unread only, a page at a time, and hides those an `INotificationVisibilityHandler` hides.
- It renders each with the module's `Notification` shape in the `Header` display type, the one of the navbar bell, so a module's display driver or `Notification-{Type}.Header.cshtml` template for its notification type shows there too.
- Each notification's link is the module's open endpoint (`/Notifications/Open/{id}`), which marks it read and goes to its URL.
- **Mark all as read** calls `INotificationManager.MarkAsReadAsync` for the user's unread notifications, which also refreshes the navbar's count.

The unread count and the newest unread notifications are the module's own bell in the navbar, on the site's pages too when the theme renders the `Navbar` shape.

The **Notification preferences** page (`/notifications/preferences`, route `UserNotificationPreferences`, `UsersConstants.Notifications.PreferencesRouteName`), linked from the Notifications page, lets a user choose how notifications reach them without the admin. It edits the Notifications module's own `UserNotificationPreferencesPart`, the one the module's profile editor edits, with its rules (`UserNotificationPreferencesForm`): the methods the user receives at all (a method turned off is in `Optout` and off for every type), and the methods on for each registered notification type, of which `NotificationTypePreferences` keeps only the choices that differ from the type's defaults. Notifications always reach the notification center; with no delivery method (e-mail, SMS) on, the page says there is nothing to choose. It needs **Manage notifications** like the Notifications page, and is framed as an account page with My Account on.

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

Adds **Email me a sign-in code** to the login form (`EmailCodeLoginFormDisplayDriver`) and the pages `sign-in/email` (`EmailSignIn`) and `sign-in/email/code`. `EmailSignInCodes` keeps a SHA-256 hash of each eight-digit code in `IDistributedCache`, per tenant, for ten minutes and five tries, and limits, on the shared attempt limiter, sends to ten per client network and then three per email every fifteen minutes, and ten per account a day; and checks to thirty per client network every fifteen minutes, and ten wrong codes per account a day, after which no code of the account is checked (the right one included) for a day (policies `email-signin-send-network`, `email-signin-send-email`, `email-signin-send-account`, `email-signin-verify-network` and `email-signin-verify-account`, which `CrestApps_Throttling:AttemptPolicies` can change). The per-email count uses the address as Orchard Core's lookup normalizes it (Unicode normalization, then upper case), so every form of an address that finds the same account, such as one written with the Kelvin sign for a K, shares one count; the per-account counts hold whatever address or network asked. With eight digits, five tries a code and ten wrong codes a day, a guesser has about one chance in ten million a day; the daily limits keep that true when Identity's lockout is lenient or off, and stop the account's mailbox from being flooded. A blocked account is told *Too many wrong codes were tried* and can still sign in with its password. Both pages are in the `sign-in-code` rate-limit group. A wrong code counts toward the account's lockout; posting digits when no code is live does not. The request page answers the same way, and does the same work, for any email: an address that no account a code can reach has is counted and given a code under a stand-in id (`EmailSignInCodes.GetStandInId`) that is never mailed, and the code page checks it the same way, so the limiter and cache work (and the time) are the same; the code is mailed after the request ends (`IEmailSignInCodeSender`). One difference is left: a wrong code for a real account also counts toward Identity's lockout, which saves the account, and that happens only for an address a code was just mailed to. The code page is rendered from the post, so the email is never in a URL. Users who are disabled, locked out, have two-factor authentication on, are in a role listed in `CrestApps:Users:EmailSignIn:ExcludedRoles` (Administrator by default, which also stands for the tenant's administrator role when Orchard Core's `SystemAdminRoleName` renamed it), or sign in only through an external provider (external logins and no password) and whose address is proved are not signed in by a code (`EmailSignInEligibility`). An account that signs in only through an external provider and whose address isn't proved gets a code, which offers to take it back (below). With **Disable local login** on in the login settings the pages answer 404 and the login form has no **Email me a sign-in code** link (both check `EmailSignInAvailability`). The code page raises Orchard's login events except `LoggingInAsync`, whose handlers (reCAPTCHA among them) check fields of the password form, so reCAPTCHA does not apply to code sign-in. It depends on Orchard Core Users and Email.

A code is for one account and its own address. The typed address must equal the account's stored address once both are trimmed and upper-cased, compared exactly: a database that compares text loosely (SQL Server's default collation treats `AE` as `Æ`, `TH` as `Þ` and `SS` as `ß`) finds an account for a look-alike address, and such an address gets no code. The code is keyed on the account's id, its hash holds the account's address, so it stops working if the address changes, and it is mailed to the stored address, never to what was typed.

A code proves the address by a round trip. What happens on the first proof of an address (no earlier confirmation link or code for that address, see `UserEmailOwnership`) depends on what the account had:

- **No password and no external login**, such as an account made from an order: the address is confirmed and recorded as proved, and the user is signed in. Recording it is what merges the guest orders placed with it, with the Customers feature on.
- **A password or an external login, on an unconfirmed account of a site that requires confirmed addresses**: Orchard Core refuses every sign-in to such an account, and refuses its password reset, so its password and logins never signed anyone in. The code takes the account back: they are removed, the address is confirmed and recorded as proved as on the confirmation pages below, and the user is signed in and told how to set a new password.
- **External logins and no password**: they may belong to whoever registered with the address through their own external account, which takes any address, or be the owner's own passwordless account that no provider proved. A right code removes nothing by itself: the page names the provider, for example "This account signs in with Google. Remove it and use your email instead?" (`TakeBack` view, posted to `sign-in/email/take-back`, `EmailSignInTakeBack`, with a protected step that names the account, its security stamp, its address and what it signs in with, and works for 15 minutes). An address changed since (Orchard Core's user editor keeps the security stamp) refuses the choice. When only the sign-in methods changed (a passkey or a login was added, which keeps the stamp too), the page is shown again naming what it removes now, ending when the first one did. The login policies (`ILoginFormEvent.ValidatingLoginAsync`, such as Tenant Hierarchy's linked-user check) are asked first, for the account as it will be once proved, so a refusal changes nothing. Only when the user chooses to remove it are the logins removed, the address confirmed and recorded as proved as on the confirmation pages below, and the user signed in and told they can link the provider again. Cancelling leaves the account as it was. A code never signs in to such an account with its logins kept. An account whose provider asserted its address as verified is proved (see below), so it gets no code at all.
- **Passkeys and no password** (Orchard Core's user store keeps passkeys from 4.0.0-preview-19231): a passkey signs in like a password, and whoever registered with the address may have added it. The same choice is offered, naming the passkeys ("This account signs in with a passkey. Remove it and use your email instead?"), and taking the account back removes its passkeys and any external logins, with a notice that a passkey can be added again from the security settings. Once the address is proved, a code signs in to such an account with its passkeys kept, as to one with a password.
- **A password, otherwise** (with or without external logins): they were set before anyone proved the address and may belong to whoever registered with it or changed their email to it. The user is signed in with nothing removed, recorded or confirmed, but every other sign-in of the account ends at once: the security stamp is rotated, a cookie issued before is refused from its next request (`UserCredentialsResets`, as for the proof below), and the account's OpenID authorizations and tokens are revoked (`IUserSignInRevocation`, `EmailOwnershipProof.EndOtherSignInsAsync`). So whoever set the password or linked the login is signed out until they sign in again; the password and logins themselves stay until the owner confirms. The login policies are asked first, so a refused sign-in ends nothing. The session is marked as begun with an e-mailed code (`amr=otp`, `UserCredentialsProof.EmailedCodeMethod`). Every page the user opens (a GET that renders a view, `EmailOwnershipNoticeFilter`) warns that the account has a password or other sign-ins added before the address was confirmed as theirs, and links to **confirm it's you** to remove them, until the address is proved. The warning also shows on every page to every session of an account whose password or external logins were added, changed or removed from such a code session (the user linked their own login or removed one, which signs the browser in again without `amr=otp`): the session carries what the credentials were at the code sign-in (`crestapps_code_session_credentials`, a digest of the security stamp and the logins), and `EmailOwnershipUserEventHandler` records `EmailOwnershipCredentialsChangedInCodeSessionUtc` on the account when a save changes them. Once that mark is saved, every session of the account signed in before it is refreshed at its next request (`UserCredentialsResets.PublishClaimsRefreshAsync`; `CredentialsResetCookieValidation` runs Identity's stamp check at once, which keeps the session signed in and builds its claims again), so a browser that signed in earlier is warned straight away rather than at its next stamp check. This uses the same `IDistributedCache` entry as the credentials reset below, so it costs no extra read per request; sessions issued up to 30 seconds after the mark are refreshed too, since the update commits at the end of the request. It is shown to such a session when the account's address isn't proved and it has a password or an external login the session doesn't show (`UserCredentialsProof.HasCredentialsNotShown`, which counts a credential as shown only while the session's cookie carries the account's current security stamp). Any other account whose address isn't proved and that has more than one way to sign in (a password and a login, or several logins), such as a default external registration with a password, is told once at sign-in instead (`EmailOwnershipLoginFormEvent`), not on every page. To keep it cheap, the feature's `IUserClaimsProvider` (`EmailOwnershipClaimsProvider`) adds `crestapps_credentials_in_doubt` to the claims of a sign-in only to an account so marked, and the filter reads the account only on pages of a session with that claim or `amr=otp`: one user read per such page, nothing for other sessions or requests. Every account that existed before the marker is in this case, and keeps its password and logins.

The confirmation pages, `sign-in/email/confirm` (`EmailSignInConfirm`) and `sign-in/email/confirm/code` (`EmailSignInConfirmCode`, `EmailOwnershipController`), come from the **Confirm email ownership** feature (`CrestApps.OrchardCore.Users.EmailOwnership`), which this feature and the Customers feature depend on, so they are there whenever guest orders can wait for an account. They are for a signed-in user whom a code could sign in, or whose account signs in only through an external provider, since confirming signs nobody in (`EmailSignInEligibility.AllowsConfirmationAsync`). `IEmailOwnershipConfirmationPage` (in `CrestApps.OrchardCore.Users.Core`) gives their address for a user who can use them, or `null`. They mail a second code to the account's address. The mailbox alone doesn't say whose the account's password and external logins are, and neither does one of them: a password can be set again with the mailbox alone (**Forgot your password?**) and a login linked from a code session, while somebody else's stays. So only what the confirmation flow itself shows is kept, and nothing is read from the session's cookie, which can be older than the account's credentials and can be re-issued with the current security stamp (Orchard Core's **Reset authenticator app** calls `RefreshSignInAsync` for any user): the password, when it is typed in the optional field next to the code, even by a session that signed in with it; and an external login, when the user signs in through it again from the page (a **Keep** button named for the provider, such as **Keep Google**, which posts to `sign-in/email/confirm/login`, `EmailSignInConfirmLogin`, challenges that provider bound to the account, and comes back to `sign-in/email/confirm/login/callback`). The login kept is the account's login whose provider and key the provider just returned; a sign-in through another provider account keeps nothing. That step is carried through the code pages protected, with the account's security stamp, address and sign-in methods, for 15 minutes. When it expired or the account changed since, both code pages stop before the code is looked at or anything changes and show the confirmation page again, asking the user to sign in with the provider again (or ask for a code without it), so the code isn't spent and the login isn't removed. The login policies are asked before the code is checked, for the account as it will be once proved; a refusal changes nothing. A wrong password is refused before the code is looked at, so it doesn't spend the code, and counts toward the lockout. Passkeys are never kept: a session doesn't say which passkey it used and the page asks for none, so the page says they are removed and the user adds theirs again afterwards. A passkey session (Orchard Core writes `amr=pop`, or `amr=mfa` when the site requires user verification) never counts as the password, and every count of an account's credentials (the warning on every page, the sign-in notice, the confirmation link, the provider-verified proof below) includes its passkeys; adding or removing a passkey from a session a code began marks the account like linking a login. When the code is right, `EmailOwnershipProof` removes every passkey and every other password and external login (and the tokens a removed login stored), rotates the account's security stamp, records when its credentials were reset (`CredentialsResetUtc` on the user) and records the address as proved, all in one update. If that update fails, the request's session is reset and nothing of it is kept. Then every earlier sign-in ends: `CredentialsResetCookieValidation` wraps the application cookie's `OnValidatePrincipal` and refuses any sign-in cookie issued before the reset on every request, ahead of Identity's own security stamp check, which runs only every `SecurityStampValidatorOptions.ValidationInterval` (30 minutes by default; the reset time and the new security stamp are kept in `IDistributedCache` for that long, after which the stamp check refuses those cookies itself). A cookie keeps its issue time to the second, so in the reset's own second a cookie is let through only when it carries the new stamp. When the cache can't be read, the cookie is left to the stamp check rather than failing the request, and when the reset can't be published the revocations below still run. With the OpenID server on, the account's OpenID authorizations and tokens are revoked (`IUserSignInRevocation`); an access token already issued stays valid until it expires unless token validation checks its entry. This happens on every first proof, whether or not anything was removed. The browser that confirmed is signed in again, which merges the guest orders, and the user is told what the account kept; with nothing kept, to sign in with a code or set a new password through password recovery.

An external sign-in, registration or link proves the address too when its provider asserted that very address as verified (`EmailOwnershipExternalLoginHandler`, an `IExternalLoginEventHandler` that Orchard Core runs before every external sign-in): the provider's email claim matches the account's address and its `email_verified` (or `verified_email`) claim is true, and nothing else signs in to the account: it has no password, no passkey and this login is its only one (matched by the provider's subject), or it was created in the same request, as Orchard Core's external registration does with the password typed there. A provider that sends no verified flag proves nothing: ASP.NET Core's Google, Microsoft Account, Facebook and GitHub handlers map none by default, and Microsoft Entra ID sends none, so their accounts still go through the take-back choice or the confirmation pages. Only add providers whose verified flag you trust.

A followed confirmation link (`IUserEventHandler.ConfirmedAsync`) records the address as proved only when the account has no password or external login. On an account with credentials, Orchard Core still confirms the address but nothing is recorded, so no guest orders move, whatever sign-in the request shows: someone may have registered with another person's address and a password of their own, and that person may follow the "confirm your account" mail. The owner confirms on the pages above, which keep only what that request shows. The link only confirms the address it was sent to (`EmailBoundConfirmationTokenProvider`). Both come from `AddEmailOwnershipConfirmation()` in `CrestApps.OrchardCore.Users.Core`, which the Confirm email ownership and Customers features call.

## User pictures for other modules

`IUserAvatarProvider` gives the address of a user's picture. The User Avatars feature registers one, and the user search (`IUserSearchService`) fills `UserSearchResult.AvatarUrl` from it, so modules show faces (for example the staff organization chart) without knowing where pictures are kept.

An `IUserAudienceProvider` can also leave users out of a search with `GetExcludedUserIdsAsync`, which only receives the users a search found. Staff uses it for the `StaffUnlinked` audience, so the staff editor offers only accounts that aren't linked to a staff member yet.

## Confirm the password before private pages

`IPasswordConfirmation` (Users.Abstractions) asks the signed-in person for their password before a page shows private data, such as pay. Put `[ConfirmPassword]` (Users.Core) on a controller or an action: until the person confirms, a GET is redirected to **Confirm your password** (`~/Admin/confirm-password`) and back, and a form posted after the confirmation ran out goes back to the page it was posted from. A confirmation is a cookie protected for that user only (`HttpOnly`, `SameSite=Strict`), valid for `Window` (10 minutes); a wrong password counts toward lockout.

```csharp
[Admin]
[ConfirmPassword]
public sealed class MyPayController : Controller
{
    // Every action asks first.
}
```

A display driver that shows private data on a page others can open checks `IsConfirmedAsync(user)` itself and renders a prompt that links to `GetConfirmationUrl(returnUrl)`. A page that must ask every time it is opened, such as a person's own pay, checks `IsJustConfirmedAsync(user)` instead: it is true only on the first page the person opens right after typing their password (a second, one-minute cookie that the check deletes), and every caller in that request gets the same answer. Without the Users feature, `[ConfirmPassword]` refuses the page.
