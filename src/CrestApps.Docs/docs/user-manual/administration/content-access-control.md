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
| **Menu** | Design > Content Definition > Content Types (setup); the content item editor (picking roles) |
| **Permission** | Edit content types (setup); the usual permission to edit the content (picking roles) |
| **Feature** | Content Access Control (turns on Enhanced Roles too) |

<AskYourAdmin />

<YouTube video="content-access-control" title="Video overview of restricting content by role" />

<YouTube video="site-administration-walkthroughs" title="Video of the Restrict content setting and the improved role selector picking roles on a page" start={75} end={102} />

## How it works

The restriction uses the **Role Picker** part from [Roles and Permissions](roles.md#let-editors-pick-roles-on-a-content-item). When the part's **Restrict content?** setting is on:

- A person can view the item only if they hold at least one of the roles picked on it.
- An item with no roles picked is not restricted. Everyone who can normally view content can view it.
- Pick **Authenticated** to open an item to everyone who is signed in, whatever their other roles. Pick **Anonymous** to show it to visitors who are not signed in (signed-in people then need another picked role). Pick both to show it to everyone.
- Viewing, previewing and listing the item are limited, including through a content type's own permissions when it has **Securable** ticked, and so are editing, publishing and deleting it: an editor who isn't in one of the item's roles can't open it in the editor, publish it or delete it. Being in one of its roles lets someone view the item; to change it they still need the usual permission to edit, publish or delete. Pick a role your editors are in, or they can't change the item afterwards.
- People in the **Administrator** role can always view every item, restricted or not.
- A person who does not hold a picked role is refused: the page shows *You do not have access to this resource.*
- On the item's editor the picker is labelled **Which roles can see this content**, reads **Any role** while no role is picked, and hints *Leave empty so everyone can see it.*, unless you give the part a name or a hint of your own on the content type.

## Set up a content type (once)

1. Open **Design > Content Definition > Content Types** and edit the content type.
<YouTube video="content-access-control" title="Video of adding the Role Picker to a content type and restricting its content" start={89} end={144} />

1. Open **Content > Content Definition > Content Types** and edit the content type.
2. If it does not have the **Role Picker** part yet, click **Add Parts**, tick **Role Picker**, and save.
3. Click **Add & Configure** next to **Role Picker**, or edit the **Role Picker** part on the content type.
   Leave **Display Name** empty to show editors **Which roles can see this content** above the picker, or give it a name of your own.
4. Tick **Restrict content?**. Its hint reads: *When checked, only users in the selected roles will be allowed to access this content.*
5. Set the picker's other options (**Exclude roles**, **Required?**, **Allow multiple?**, **Hint**). They are described on [Roles and Permissions](roles.md#add-the-role-picker-to-a-content-type).
6. Save.

:::tip
Editors see **Which roles can see this content** unless you name the part, so a name is only needed when you want other wording. Use **Exclude roles** to hide roles that should never be picked, such as **Administrator**, who can view every item anyway.
:::

## Restrict an item

<YouTube video="content-access-control" title="Video of picking the roles that may view an item" start={144} end={195} />

1. Create or edit an item of that content type.
2. In the role picker, search for and tick the roles that may view the item. When more than one role is allowed, **Select All** and **Deselect All** change every role at once.
3. Save or publish the item.

To lift the restriction from one item, untick all its roles and save. To lift it from the whole content type, untick **Restrict content?** on the part.

<YouTube video="content-access-control" title="Video of checking the result by signing in as people with and without the role" start={195} end={224} />

:::note[Check the result]
After you set up a restricted type, sign in as a person who does not hold the picked roles and confirm they cannot open the item. Ask your administrator for a test account if you need one.
:::
