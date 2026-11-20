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

Adds **Email me a sign-in code** to the login form (`EmailCodeLoginFormDisplayDriver`) and the pages `sign-in/email` (`EmailSignIn`) and `sign-in/email/code`. `EmailSignInCodes` keeps a SHA-256 hash of each six-digit code in `IDistributedCache`, per tenant, for ten minutes and five tries, and limits sends to three per email and ten per client address every fifteen minutes. The request page answers the same way for any email, and the code page is rendered from the post, so the email is never in a URL. Users who are disabled, locked out or have two-factor authentication on are not signed in by a code. A successful code confirms the user's email. It depends on Orchard Core Users and Email.

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
