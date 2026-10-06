---
sidebar_label: Users
title: Users, Display Names and Avatars
description: Add people to the site, give them roles, and choose how their names and pictures appear on screen.
technical_manual:
  - modules/users
---

Everyone who signs in has a **user account**. Orchard Core gives you the user list, where you add people, reset passwords and give them roles. CrestApps adds a **display name**, so screens show *John Smith* instead of *jsmith*, and an **avatar**, a picture for each person. Use them when agents, supervisors and customers' records should show real names rather than sign-in names.

| | |
| --- | --- |
| **Menu** | Access Control > Users; Settings > User Display Name; Settings > User Avatars |
| **Permission** | Manage users (accounts); Manage the user display name settings; Manage the avatar settings |
| **Feature** | Users (Orchard Core); User Display Name; User Avatar |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of enabling User Display Name, choosing a format, and editing a user">
  <source src="/img/docs/users.mp4" type="video/mp4" />
</video>

## Add a user and give them access

1. Open **Access Control > Users** and click **Add User**.
2. Fill in the account details and tick the **roles** the person needs. The roles decide what the person can see and do. See [Roles and Permissions](roles.md).
3. If the display name boxes are shown, fill them in (see [Set a person's name](#set-a-persons-name)).
4. Save the user.

Creating accounts, passwords, sign-in and registration are standard Orchard Core screens. See the [Orchard Core Users documentation](https://docs.orchardcore.net/en/latest/reference/modules/Users/) for those.

:::tip
To change what someone can do later, edit their user and change their roles. You rarely need to change a role itself.
:::

## Choose how names are shown

Open **Settings > User Display Name**. It decides how a person's name is built everywhere the site shows a user.

| Field | What it does |
| --- | --- |
| **Display name format** | How the name is built. **Username** (the default) shows the sign-in name. **Display name** shows a free-text name each person has. **First Middle Last name** shows *John A Smith*. **Last, First Middle name** shows *Smith, John A*. **Custom format** uses a pattern you type in **Liquid format**. |
| **Liquid format** | Only for **Custom format**. A pattern such as `{{ FirstName }} {{ LastName }}`. Ask a developer if you need something more advanced. |
| **Display name** | Whether the user editor has a **Display Name** box: **Don't use**, **Optional** or **Required**. |
| **First name**, **Middle name**, **Last name** | The same three choices for each name box. A middle name is only part of the name when **Middle name** is not set to **Don't use**. |

Click **Save**. When a person has no name filled in, the site shows their user name instead.

Use this when you want full names everywhere: pick **First Middle Last name** and set **First name** and **Last name** to **Required**, so every account you add or edit must carry a full name.

<video controls preload="metadata" width="100%" aria-label="Screencast of configuring the display name, setting a profile, and the content item author badge showing the full name">
  <source src="/img/docs/users-display-name.mp4" type="video/mp4" />
</video>

The screencast sets the format to *First Middle Last name*, fills in the names on a user, and then shows the author badge in the content list changing from the user name to the full name.

### Where the name appears

- The user menu at the top of the admin screens.
- Author and owner badges in content lists.
- User pickers, such as the **Owner Name** box on content items and user fields on content types.
- Searching **Access Control > Users**: typing a name finds people by user name, display name, first, middle or last name.

## Set a person's name

1. Open **Access Control > Users** and edit the user.
2. Fill in the **Display Name**, **First Name**, **Middle Name** and **Last Name** boxes. Only the boxes that are not set to **Don't use** are shown, and boxes set to **Required** must be filled in before you can save.
3. Save the user.

Only people who can edit user accounts see and change these boxes. Someone who edits only their own profile cannot change their name; ask your administrator to update it.

## Pick a content item's owner by name

When a content type shows the owner box, the **Owner Name** box becomes a searchable list of users that shows their display names. Click **Browse users**, type part of a name, and pick the person.

In the content type's Common part settings you can also choose the **Permission Defined Editor**. It shows the owner box only to people who are allowed to publish the item.

## Avatars

Open **Settings > User Avatars** to set how avatars work, then click **Save**.

| Field | What it does |
| --- | --- |
| **Require Avatar** | Every user must have a picture before their account can be saved. |
| **Use default CSS styles** | Uses the built-in look for avatars. Leave it ticked unless a developer has styled avatars for your site. |

To give someone a picture, edit the user and use the **Avatar** box to pick or upload an image from the media library. Only image files are accepted. The box is shown only to people who have the **Manage Media** permission. The avatar appears next to the person's name in places that show who a user is.
