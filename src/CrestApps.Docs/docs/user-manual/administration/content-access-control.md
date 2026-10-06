---
sidebar_label: Content Access Control
title: Restrict Content by Role
description: Limit who can view a content item to the roles you pick on that item.
technical_manual:
  - modules/content-access-control
---

Content Access Control lets an editor say, on each content item, which roles may view it. Use it for pages or documents that only one team should read, for example a supervisor handbook or an internal price list.

| | |
| --- | --- |
| **Menu** | Content > Content Definition > Content Types (setup); the content item editor (picking roles) |
| **Permission** | Edit content types (setup); the usual permission to edit the content (picking roles) |
| **Feature** | Content Access Control (turns on Enhanced Roles too) |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of the Restrict content setting and the improved role selector picking roles on a page">
  <source src="/img/docs/content-access-control.mp4" type="video/mp4" />
</video>

## How it works

The restriction uses the **Role Picker** part from [Roles and Permissions](roles.md#let-editors-pick-roles-on-a-content-item). When the part's **Restrict content?** setting is on:

- A person can view the item only if they hold at least one of the roles picked on it.
- An item with no roles picked is not restricted. Everyone who can normally view content can view it.
- Only viewing is limited. Who can edit, publish or delete the item still follows the normal permissions.

## Set up a content type (once)

1. Open **Content > Content Definition > Content Types** and edit the content type.
2. If it does not have the **Role Picker** part yet, click **Add Parts**, tick **Role Picker**, and save.
3. Edit the **Role Picker** part on the content type.
4. Tick **Restrict content?**. Its hint reads: *When checked, only users in the selected roles will be allowed to access this content.*
5. Set the picker's other options (**Exclude roles**, **Required?**, **Allow multiple?**, **Hint**). They are described on [Roles and Permissions](roles.md#add-the-role-picker-to-a-content-type).
6. Save.

:::tip
Give the part a clear name on the content type, such as *Limit access to selected roles*, so editors know what the picker does. Use **Exclude roles** to hide roles that should never be picked, such as **Administrator**, **Authenticated** and **Anonymous**.
:::

## Restrict an item

1. Create or edit an item of that content type.
2. In the role picker, search for and tick the roles that may view the item. When more than one role is allowed, **Select All** and **Deselect All** change every role at once.
3. Save or publish the item.

To lift the restriction from one item, untick all its roles and save. To lift it from the whole content type, untick **Restrict content?** on the part.

:::note[Check the result]
After you set up a restricted type, sign in as a person who does not hold the picked roles and confirm they cannot open the item. Ask your administrator for a test account if you need one.
:::
