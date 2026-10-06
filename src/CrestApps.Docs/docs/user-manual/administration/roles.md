---
sidebar_label: Roles and Permissions
title: Roles and Permissions
description: Understand how roles decide what each person sees, give someone access, and let editors pick roles on content items.
technical_manual:
  - modules/roles
---

Roles decide what each person can see and do. Use this page when someone cannot find a menu or button they need, when a new team member starts, or when you want a group of people, such as supervisors, to share the same access.

| | |
| --- | --- |
| **Menu** | Access Control > Roles; Access Control > Users |
| **Permission** | Manage Roles; Manage users |
| **Feature** | Roles (Orchard Core); Enhanced Roles adds the role picker for content items |

<AskYourAdmin />

## How roles work

- A **permission** is one thing a person may do, for example *Manage Contact Center queues* or *Run 'Phone Number Verifications' Report*.
- A **role** is a named set of permissions, for example *Supervisor*.
- A **user** can hold several roles. They get every permission of every role they hold. Permissions only add up; no role takes a permission away.
- Menus, buttons and settings only appear when one of the person's roles has the permission they need. That is why two people can see a different admin menu.
- A feature's permissions only appear in the role editor once the feature is turned on.

Orchard Core includes some roles out of the box. **Administrator** can do everything. **Authenticated** applies to every signed-in person, and **Anonymous** applies to visitors who are not signed in. When a CrestApps feature is turned on, it usually gives its management permissions to the **Administrator** role.

The User Manual pages list the permission each screen needs in the table at the top. Use those names when you ask for access or grant it.

## Give someone access

Most of the time you only need step 1.

1. **Give the person a role.** Open **Access Control > Users**, edit the user, tick the role that already has the access they need, and save.
2. **Or add the permission to a role.** Open **Access Control > Roles**, edit the role, tick the permission in the list (it is grouped by feature), and save. Everyone who holds that role gets it.
3. **Or create a new role.** On **Access Control > Roles**, click **Add Role**, name it, save it, then edit it to tick its permissions, and give it to the right users.

:::tip
Prefer roles that match job titles, such as *Agent*, *Supervisor* and *Manager*. Changing one role then updates everyone in that job at once.
:::

If the screen still does not appear after you give the permission, the feature may be off. Features are turned on under **Tools > Features**.

For the role editor itself, see the [Orchard Core Roles documentation](https://docs.orchardcore.net/en/latest/reference/modules/Roles/).

## Let editors pick roles on a content item

The **Enhanced Roles** feature adds a **Role Picker** part. Add it to a content type when each item should carry a list of roles, for example to say which teams a page is for. The [Content Access Control](content-access-control.md) feature uses the same picker to limit who can view an item.

<video controls preload="metadata" width="100%" aria-label="Screencast of enabling Enhanced Roles, adding the Role Picker part, and selecting roles on a content item">
  <source src="/img/docs/roles.mp4" type="video/mp4" />
</video>

### Add the role picker to a content type

1. Open **Content > Content Definition > Content Types** and edit the content type.
2. Click **Add Parts**, tick **Role Picker**, and save.
3. Edit the **Role Picker** part on the content type to set the options below, and save.

You need the **Edit content types** permission.

| Field | What it does |
| --- | --- |
| **Exclude roles** | Roles that editors cannot pick. Leave it empty to offer every role. |
| **Required?** | Editors must pick at least one role before they can save the item. |
| **Allow multiple?** | Editors can pick more than one role. Without it, they pick one. |
| **Hint** | Help text shown under the picker. |

### Pick roles on an item

When you edit an item of that type, the picker lists the roles in alphabetical order. Type in its search box to find a role. When more than one role is allowed, use **Select All** or **Deselect All** to change every role at once, and the picked roles are shown with a tick. Save or publish the item.
