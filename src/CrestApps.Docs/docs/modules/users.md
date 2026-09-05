---
sidebar_label: Users
sidebar_position: 1
title: Users
description: Enhanced user management with display name customization and avatar support for Orchard Core.
---

| | |
| --- | --- |
| **Feature Name** | CrestApps Users Core |
| **Feature ID** | `CrestApps.OrchardCore.Users` |

Extends the Orchard Core Users module by adding functionality to cache users.

:::note Note
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

For example, the [SMS Workspace](../omnichannel/sms-workspace) uses `UserPicker` to choose the agent an SMS number routes inbound messages to.

## User Display Name

| | |
| --- | --- |
| **Feature Name** | User Display Name |
| **Feature ID** | `CrestApps.OrchardCore.Users.DisplayName` |

Provides a way to display a user's display name.

To set the display name format, navigate to **Settings** → **User Display Name**.

The screencast below enables **User Display Name**, selects the *First Middle Last name* format with required first and last names, and shows the matching name fields appearing on the user editor.

<video controls preload="metadata" width="100%" aria-label="Screen cast of enabling User Display Name, choosing a format, and editing a user">
  <source src="/img/docs/users.mp4" type="video/mp4" />
</video>

The next screencast shows the full effect end to end. It configures the *First Middle Last name* format with **First name** and **Last name** set to **Required**, edits the current user's own profile to set those names, then opens the **Content Items** list where the author badge that previously showed only the username now shows the user's full name.

<video controls preload="metadata" width="100%" aria-label="Screen cast of configuring the display name, setting a profile, and the content item author badge showing the full name">
  <source src="/img/docs/users-display-name.mp4" type="video/mp4" />
</video>

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

To change the default settings, navigate to **Settings** → **User Avatars**.
