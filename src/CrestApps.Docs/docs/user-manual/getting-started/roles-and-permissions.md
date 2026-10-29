---
sidebar_label: Roles & Permissions
sidebar_position: 2
title: Roles and Permissions
description: Understand roles and permissions in plain language, see what the built-in Agent, Supervisor and Administrator roles can do, and ask your administrator for the access you need.
technical_manual:
  - modules/roles
  - feature-reference
---

What you see in the application depends on who you are. This page explains how that works, what each built-in role can do, and how to ask for more access when a menu or a button is missing.

## Roles and permissions in plain language

- A **permission** allows one thing, such as *Use the messaging workspace* or *Manage Contact Center queues*. Each menu item and each button checks for a permission.
- A **role** is a named bundle of permissions, such as *Agent* or *Supervisor*. Your administrator gives each user one or more roles.
- You get every permission of every role you have. A team lead who also takes calls can have both the Agent and the Supervisor role.

<YouTube video="contact-center-roles-and-permissions" title="Video overview of the Agent and Supervisor roles, the soft phone permission, entitlement records, and the recording, voicemail and report permissions" />

A screen also needs its **feature** to be turned on. A feature is one part of the product, such as *Contact Center Supervision & Live Dashboard*. If a feature is off, nobody sees its screens, whatever their role. Only administrators turn features on; see [Features and settings](features-and-settings.md).

## Why menus differ between people

Two people at the same company see different menus because:

1. **Their roles differ.** An agent sees **My workspace** and **Activities**; a manager also sees **Interaction Center > Management**; an administrator also sees **Settings** and **Tools**.
2. **Features differ by site.** A site that does not use the CRM has no **Leads** menu, and a site without the dialer has no **Dialer Profiles**.
3. **Some screens also need a setup record.** The [live dashboard](../live-dashboard.md) and [shared voicemail](../voicemail.md) show only the queues on your own [agent entitlement](../skills-and-entitlements.md) record. Without one, the page opens but is empty, even for an administrator. A supervisor needs one too, with the queues they oversee.

<YouTube video="contact-center-roles-and-permissions" title="Video of why a supervisor needs an agent entitlement record" start={192} end={236} />
4. **Some tabs and buttons need a site setting.** For example, **Pause recording** in the agent workspace only appears when the administrator allows it in [Contact Center settings](../contact-center-settings.md).

## The built-in roles

The product comes with ready-made permission sets for three role names: **Administrator**, **Agent** and **Supervisor**.

- The **Administrator** role has every permission.
- When your administrator creates a role named exactly **Agent** or **Supervisor**, it receives the permissions below automatically, for every feature that is turned on. Turning on a feature later adds that feature's permissions to these roles too.
<YouTube video="contact-center-roles-and-permissions" title="Video of creating the Agent and Supervisor roles" start={55} end={88} />

- There is no built-in *Manager* role. Most companies create one and pick its permissions by hand (see [A role for managers](#a-role-for-managers)).

Your administrator can change any role's permissions at any time, so your site may differ from the lists below.

### Agent

Agents take and place calls, work their activities and answer text messages.

<YouTube video="contact-center-roles-and-permissions" title="Video of what the Agent role gets, and the soft phone permission" start={88} end={130} />

| Area | Permissions the Agent role gets |
| --- | --- |
| Contact center | Sign in to Contact Center queues and campaigns; View interactions; Pause recording on own live interactions; Initiate secure capture on own live interactions |
| Call recordings | Listen to own call recordings (never anyone else's) |
| Activities | List activities; List Contact activities; Complete own activity; Create and edit activities |
| CRM | Convert leads |
| Messaging | Use the messaging workspace |

Agents usually also need, added by the administrator:

- **Use the telephony soft phone**, to open the soft phone in the [phone apps](../phone-apps.md). The Agent role does not get it automatically: open **Access Control > Roles**, edit the Agent role, find the permission with **Search permissions**, tick it and save, and every agent has the soft phone.
- The content permissions for your contact type, such as **List content items**, to see and edit [contacts](../contacts.md).

### Supervisor

Supervisors watch the floor, step into live calls, work the shared voicemail and read reports.

<YouTube video="contact-center-roles-and-permissions" title="Video of what the Supervisor role gets" start={130} end={173} />

| Area | Permissions the Supervisor role gets |
| --- | --- |
| Contact center | Monitor the Contact Center in real time; View interactions; Transfer Contact Center calls externally; View Contact Center reports; Take over, end, transfer and record live Contact Center calls, and set agents' state |
| Call recordings | Listen to anyone's call recordings (which includes listening to their own) |
| Voicemail | Access shared queue voicemail for entitled queues; Manage shared queue voicemail: delete messages and take over other users' claims |
| Messaging | Use the messaging workspace; Send group messages; View all messaging conversations; Manage the messaging workspace |

The Supervisor role does not include signing in to queues. A supervisor who also takes calls needs the Agent role as well, and every supervisor needs an [entitlement record](../skills-and-entitlements.md) with the queues they supervise.

### Administrator

The Administrator role can see and change everything, including **Settings**, **Tools > Features**, users and roles. Keep it for the few people who run the site.

Some permissions go to the Administrator role only, so nobody else has them until an administrator gives them: **Delete call recordings**, **Manage Contact Center queue groups**, **Manage Contact Center queues**, **Manage Contact Center agents**, **Manage Contact Center skills**, **Manage Contact Center business hours**, **Manage the Contact Center dialer**, **Manage the Contact Center voice media library**, **Manage interactions**, **Manage the Contact Center**, **Manage telephony settings**, **Manage telephony extensions** and **Use the telephony soft phone**.

### A role for managers

Managers build the contact center: contacts, subjects, campaigns, queues and so on. A manager role typically gets the **Manage ...** permissions for the screens that person looks after, for example:

| To work on... | Permission |
| --- | --- |
| [Load activities](../load-inventory.md) | Manage activity batches |
| [Manage activities in bulk](../bulk-activities.md) | Manage activities |
| [Subjects and subject flows](../subject-flows.md) | Manage subject flows |
| [Dispositions](../dispositions.md) | Manage dispositions |
| [Campaigns](../campaigns.md) | Manage campaigns; Manage campaign groups |
| [Cadences](../cadences.md) | Manage cadences |
| [Lead statuses and opportunity stages](../leads-accounts-opportunities.md) | Manage lead statuses; Manage opportunity stages |
| [Omnichannel addresses](../channel-endpoints.md) | Manage omnichannel addresses |
| [Queues](../queues.md) and [entry points](../entry-points-and-ivr.md) | Manage Contact Center queues |
| [Queue groups](../queues.md) | Manage Contact Center queue groups |
| [Call recordings](../call-recordings.md) | Listen to own call recordings; Listen to anyone's call recordings; Delete call recordings |
| [Shared voicemail](../voicemail.md) | Access shared queue voicemail for entitled queues; Manage shared queue voicemail |
| [Soft phone](../soft-phone.md) | Use the telephony soft phone |
| [Skills](../skills-and-entitlements.md) | Manage Contact Center skills |
| [Agent entitlements](../skills-and-entitlements.md) and [agent states](../agent-states.md) | Manage Contact Center agents |
| [Business hours](../business-hours.md) | Manage Contact Center business hours |
| [Dialer profiles](../dialer-profiles.md) | Manage the Contact Center dialer |
| [Voice media](../voice-media.md) | Manage the Contact Center voice media library |
| [Extensions](../extensions.md) | Manage telephony extensions |
| [Reports](../reports.md) | View Contact Center reports; View Omnichannel reports |
| [Report Builder](../report-builder/index.md) | Build reports and manage own custom reports and views; Share custom reports publicly and through share links; Manage all custom reports and views |

Each User Manual page names the permission it needs in the table at the top.

### A role that looks after one settings page

A settings page under **Settings** opens for anyone who has the permission listed for it below, together with **Access admin panel**. Your administrator can therefore let someone look after, say, the Stripe connection or the telephony provider without giving them the Administrator role or the permission to change every site setting.

| Settings page | Permission that opens it |
| --- | --- |
| **Settings > Payments > Stripe** | Manage Stripe Settings |
| **Settings > Commerce > Transactions** | Manage transaction settings |
| **Settings > Commerce > Pay Later** | Manage transaction settings |
| **Settings > Commerce > Receipts** | Manage receipt settings |
| **Settings > Subscriptions** | Manage subscriptions settings |
| **Settings > Communication > Telephony** | Manage telephony settings |
| **Settings > Communication > SMS** (the Telnyx section, when Telnyx SMS is on) | Manage SMS Settings |
| **Settings > Contact Center** | Manage the Contact Center |
| **Settings > Artificial Intelligence** | Manage AI profiles. A person with only Manage chat interaction settings, Edit any chat interactions, Manage Copilot Settings, Manage Claude Settings or Manage the MCP Server settings can open the page too, and sees only the sections that permission covers. |
| **Settings > DNC Registries** and **Settings > Content Import** | Manage DNC registry settings |
| **Settings > Phone Number Verifications** | Manage phone number verification settings |
| **Settings > User Display Name** | Manage the user display name settings. |
| **Settings > User Avatars** | Manage the avatar settings. |

On a page that several features share, such as **Settings > Artificial Intelligence**, each section is shown only to people who have that section's permission. The menu item itself follows the permission shown in the table, so someone who has only one of the other permissions may need the page's address from the administrator.

### Give the roles to users

Open **Access Control > Users**, edit a user, tick the roles in the **Roles** section and save. A team lead who also takes calls gets both the Agent and the Supervisor role.

<YouTube video="contact-center-roles-and-permissions" title="Video of giving roles to users" start={173} end={192} />

### Permissions behind recordings, voicemail and reports

<YouTube video="contact-center-roles-and-permissions" title="Video of the recording, shared voicemail and report permissions" start={236} end={280} />

| Screen | Permissions | Default roles |
| --- | --- | --- |
| [Call recordings](../call-recordings.md) | **Listen to own call recordings** | Agent, Supervisor, Administrator |
| | **Listen to anyone's call recordings** | Supervisor, Administrator |
| | **Delete call recordings** | Administrator |
| [Shared voicemail](../voicemail.md) | **Access shared queue voicemail for entitled queues** and **Manage shared queue voicemail** | Supervisor, Administrator |
| [Reports](../reports.md) | **View Contact Center reports** | Supervisor, Administrator |

## Ask your administrator for access

When a menu, a page or a button is missing, look up the page for that task in this manual. The table at the top names the **Menu**, the **Permission** and the **Feature** it needs. Then send your administrator a message like this one, filled in with what you found:

> Hi,
>
> I need access to **[screen or task, for example: Interaction Center > Management > Queues]** for my work on **[what you need to do]**.
>
> According to the User Manual, this needs:
>
> - Permission: **[permission name from the page]**
> - Feature: **[feature name from the page]**
>
> I currently have the **[your role or roles]** role. Could you add the permission to one of my roles, or give me a role that has it? If the feature is turned off on our site, could you let me know whether we plan to use it?
>
> Thank you,
> [Your name]

## For administrators

- To create users and give them roles, see [Users](../administration/users.md).
- To create roles such as Agent, Supervisor and Manager and choose their permissions, see [Roles](../administration/roles.md).
- To turn on the features a role needs, see [Features and settings](features-and-settings.md).
