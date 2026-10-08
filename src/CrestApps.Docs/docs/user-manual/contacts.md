---
sidebar_label: Contacts
sidebar_position: 10
title: Contacts
description: Create a contact type and contacts, find a contact by name or phone number, record do-not-contact preferences, and import or export contacts.
technical_manual:
  - omnichannel/management
  - modules/dnc-registry
  - modules/content-transfer
---

A **contact** is the person you call or text. Contacts are ordinary content items of a content type that has the **Omnichannel Contact** part attached (usually a type called *Contact*). Every activity, call and conversation is linked to a contact.

| | |
| --- | --- |
| **Menu** | Interaction Center > Contacts |
| **Permission** | List content items (to see contacts); the content edit and publish permissions, such as Edit own content, for the contact type (to add and change them) |
| **Feature** | Omnichannel Management |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of creating a contact, finding it by phone number, and opening its activities">
  <source src="/img/docs/um-contacts.mp4" type="video/mp4" />
</video>

## Set up the contact type (once)

An administrator with the **Edit content types** permission usually does this once, when the site is set up. One contact type is usually all you need. Create more than one only when you want to keep different kinds of people apart, for example *Customer* and *Employee*.

1. Open **Content > Content Definition > Content Types** and create a type named *Contact* (or edit your existing one).
2. Add the **Omnichannel Contact** part and any fields your business needs, such as a title for the person's name.
3. Open the part's settings to choose what the contact editor shows:

   | Setting | What it does |
   | --- | --- |
   | **Auto detect time zone** | Works out the contact's time zone from their phone number when none is picked. On by default. |
   | **Require time zone** | Makes the time zone required. Only applies when auto detect is off. |
   | **Use Do not call**, **Use Do not SMS**, **Use Do not email** | Shows that preference on the contact. Only Do not call is on by default. |

The part adds a fixed **Contact methods** list to the contact, where phone numbers and email addresses are stored. Dialers, loads, imports and exports always read numbers from this list, so do not remove or rename it. A phone number is saved in international format together with its country, so the right country flag shows when you edit it again.

The screencast below creates a *Lead* type with the **Omnichannel Contact** and **Title** parts, then adds a lead with a time zone and a cell phone number:

<video controls preload="metadata" width="100%" aria-label="Screencast of creating a contact content type and a contact item">
  <source src="/img/docs/omni-contact-type.mp4" type="video/mp4" />
</video>

## Create a contact

1. Open **Interaction Center > Contacts** and click **New** followed by the contact type's name (for example **New Customer**).
2. Fill in the name and other fields. Under **Contact Methods**, click **Add Item > Phone Number**, type the number and pick its **Type** (for example *Cell*). Pick the **Contact time zone**, or leave *Timezone (Automatic)* to detect it from the number.
3. Tick **Do not call**, **Do not SMS** or **Do not email** if the person asked not to be contacted that way. Dialers, loads and automated messages skip them.
4. Click **Publish**.

## Find a contact

On **Interaction Center > Contacts**, the search box matches a name **or** a phone number, so you can type either one. A plain number finds contacts whose main cell or home number contains those digits. This only happens on the Contacts list; on other content lists the search box matches names only.

You can also use these search terms. The **Filters** menu of the list has a **Phone** card that reminds you of them.

| Term | Matches | Example |
| --- | --- | --- |
| `phone:` | numbers that contain the digits | `phone:702555` |
| `phone-exact:` | the exact number | `phone-exact:7025550123` |
| `phone-starts:` | numbers that begin with the digits | `phone-starts:+1702` |
| `phone-ends:` | numbers that end with the digits | `phone-ends:0123` |

Tips:

- Spaces, brackets and dashes are ignored, so `(702) 555` and `702-555` work too.
- Start with `+` to include the country code. Without it, only the national number is compared, so the search can find contacts in more than one country.
- An exact search finds the number however it was saved: `5555550123`, `15555550123` and `+15555550123` all find the same contact. A ten-digit number is read as a North American number.

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

Use **Content > Export** to download contacts as CSV or Excel, and **Content > Import** to load a file of leads. See [Import and export](administration/import-and-export.md) for the general steps.

### Import options for contacts

When the file is for a contact type, the import page adds these options:

| Option | What it does |
| --- | --- |
| **Ignore duplicate by phone number** | Imports only the first row for a phone number. Later rows with the same number, and numbers that already belong to a record, are skipped and listed in the error file. A row that points at an existing contact is still updated. |
| **Compare with** | Shown when the Omnichannel CRM feature is on: which records count as duplicates. **Every contact** (the default), **Contacts of this type only**, or **Every contact and lead**. Leads are left out by default, so a customer who was once a lead can still be imported. |
| **Lead country** | The country the file's phone numbers belong to. Local numbers are converted to international format with it, before duplicate and do-not-call checks run, and it is used to work out each contact's time zone when the file has none. Required. Keep one country per file, unless every number in the file already starts with `+` and the country code. |
| **Ignore numbers on national do-not-call registries** | Checks each number against the registries you tick under **Select registries to check against**, such as the Local Do Not Call Registry. Registries marked **Enforced**, and this whole option when your administrator enforces it, are always checked. See [Do-not-call lists](administration/do-not-call-lists.md). |
| **When a number is on a registry** | **Skip the row** leaves it out and lists it in the error file. **Import it marked Do not call** keeps the record, so the number is recognised if it is bought or imported again, and Do not call keeps it out of every call. |

The file can also carry each contact's **Do not call**, **Do not SMS** and **Do not email** flags and the dates they were set, and the contact's time zone. A row that points at an existing contact updates that contact.

The import runs in the background. Its entry on the **Import** page ends as *Completed*, or as *Completed with errors* with a count of the rows that were skipped, such as a number on a do-not-call list. In the screencast, four leads are created and the fifth is skipped because its number is on the local list.

### Export contacts with their last activity

When you export a contact type, the export page shows a **CRM last activity** section. Use it to hand someone a single file with each contact's details and their latest call or message on one subject.

| Option | What it does |
| --- | --- |
| **Include last completed activity information** | Adds columns for the contact's most recent completed activity of the chosen subject: the agent's note, when it was completed, who completed it, the disposition, the subject, and each of the subject's fields. |
| **Subject** | Required when the option is on. The subject whose last completed activity is exported. |
| **Include only contacts with a last activity record** | Leaves out contacts that have no completed activity of that subject. When it is off, every contact is exported and those columns are left empty. |

An export with this option always runs in the background, and you download the file when it is ready. The [technical manual](../omnichannel/management.md#export-contacts-with-their-last-activity) lists the exact column names.
