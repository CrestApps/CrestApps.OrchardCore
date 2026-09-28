---
sidebar_label: Contacts
sidebar_position: 10
title: Contacts
description: Create a contact type and contacts, find a contact by name or phone number, record do-not-contact preferences, and import or export contacts.
---

A **contact** is the person you call or text. Contacts are ordinary content items of a content type that has the **Omnichannel Contact** part attached (usually a type called *Contact*). Every activity, call and conversation is linked to a contact.

| | |
| --- | --- |
| **Menu** | Interaction Center > Contacts |
| **Permission** | List content (to see contacts); edit permissions for the contact type (to change them) |
| **Feature** | Omnichannel Management (`CrestApps.OrchardCore.Omnichannel.Managements`) |

<video controls preload="metadata" width="100%" aria-label="Screencast of creating a contact, finding it by phone number, and opening its activities">
  <source src="/img/docs/um-contacts.mp4" type="video/mp4" />
</video>

## Set up the contact type (once)

1. Open **Content > Content Definition > Content Types** and create a type named *Contact* (or edit your existing one).
2. Add the **Omnichannel Contact** part and any fields your business needs.
3. Open the part's settings to choose what the contact editor shows:

   | Setting | What it does |
   | --- | --- |
   | **Auto detect time zone** | Works out the contact's time zone from their phone number when none is picked. On by default. |
   | **Require time zone** | Makes the time zone required. Only applies when auto detect is off. |
   | **Use Do not call**, **Use Do not SMS**, **Use Do not email** | Shows that preference on the contact. Only Do not call is on by default. |

The part adds a fixed **Contact methods** list to the contact, where phone numbers and email addresses are stored.

## Create a contact

1. Open **Interaction Center > Contacts** and click **New** followed by the contact type's name (for example **New Customer**).
2. Fill in the name and other fields. Under **Contact Methods**, click **Add Item > Phone Number**, type the number and pick its **Type** (for example *Cell*). Pick the **Contact time zone**, or leave *Timezone (Automatic)* to detect it from the number.
3. Tick **Do not call**, **Do not SMS** or **Do not email** if the person asked not to be contacted that way. Dialers, loads and automated messages skip them.
4. Click **Publish**.

## Find a contact

On **Interaction Center > Contacts**, the search box matches a name **or** a phone number. You can also use these search terms:

| Term | Matches | Example |
| --- | --- | --- |
| `phone:` | numbers that contain the digits | `phone:702555` |
| `phone-exact:` | the exact number | `phone-exact:7025550123` |
| `phone-starts:` | numbers that begin with the digits | `phone-starts:+1702` |
| `phone-ends:` | numbers that end with the digits | `phone-ends:0123` |

Start with `+` to include the country code; otherwise only the national number is compared.

## Work with a contact

On a saved contact, the buttons at the top lead to its work:

- **List Activities** shows the contact's scheduled and completed activities.
- **Add Activity > Outbound** schedules a call or message to the contact.
- **Add Activity > Inbound** logs a call or message the contact started.

See [Activities](activities.md).

## Import and export contacts

<video controls preload="metadata" width="100%" aria-label="Screencast of exporting contacts to CSV, then importing a file of leads that skips a number on the Local Do Not Call Registry">
  <source src="/img/docs/omni-contact-import-export.mp4" type="video/mp4" />
</video>

Use **Content > Export** to download contacts as CSV or Excel, and **Content > Import** to load a file of leads. When importing contacts:

- Pick the **Lead country** the file's phone numbers belong to, so local numbers are converted to international format. It is required.
- Tick **Ignore duplicate by phone number** to skip rows whose number is already on a contact.
- Tick **Ignore numbers on national do-not-call registries** and pick the registries to scrub, such as the Local Do Not Call Registry. Registries your administrator enforces are always checked. See [DNC Registry](../modules/dnc-registry.md).

The import runs in the background. Its entry on the **Import** page ends as *Completed*, or as *Completed with errors* with a count of the rows that were skipped, such as a number on a do-not-call list. In the screencast, four leads are created and the fifth is skipped because its number is on the local list.

Exports can also add each contact's last completed activity for a chosen subject (the **CRM last activity** section). See [Omnichannel Management](../omnichannel/management.md#import-and-export-contact-methods) for the file columns.
