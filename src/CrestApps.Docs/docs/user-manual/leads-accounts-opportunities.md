---
sidebar_label: Leads, Accounts and Opportunities
sidebar_position: 11
title: Leads, Accounts and Opportunities
description: Keep raw prospects apart from your contacts as leads, call and text them like contacts, convert the ones that qualify, and group contacts and deals under accounts.
---

A **lead** is a prospect you have not qualified yet, such as a row from a purchased list, a trade-show scan or a web form. You can call it, text it, load it into inventory and retry it through a subject flow, exactly like a contact. It is kept apart from your contacts, though, so your contact list holds only real customers.

When a lead qualifies, you **convert** it. Conversion creates a contact, or merges the lead into a contact who already has its phone number or email. Its history moves with it. It can also put the contact in an **account**, the company or household the person belongs to, and open an **opportunity**, the deal you are working on.

| | |
| --- | --- |
| **Menu** | Interaction Center > Leads, Accounts and Opportunities |
| **Permission** | List and edit permissions for each type. **Convert leads** to convert (anyone who can manage activities can). **Edit converted leads** to change a lead after conversion. |
| **Feature** | Omnichannel CRM (`CrestApps.OrchardCore.Omnichannel.Crm`) |

If you never turn the feature on, or never use accounts, contacts work exactly as before.

## Turn it on (once)

The quickest way is the **Omnichannel CRM starter** recipe:

1. Open **Tools > Recipes** and run **Omnichannel CRM starter**.
2. The recipe turns the feature on and adds three types you can rename or extend:
   - **Lead**, which converts into **Contact**;
   - **Contact**, if you do not have one;
   - **Sales Opportunity**.

The feature itself adds the **Account** type, a starting set of **lead statuses** and a starting set of **opportunity stages**. It never changes a type you already have.

To use your own types instead, turn on **Omnichannel CRM** under **Tools > Features** and attach the parts yourself:

| To make a... | Attach | Notes |
| --- | --- | --- |
| Lead type | **Omnichannel Contact** and **Lead** | In the Lead part's settings, pick the contact type it converts into and the opportunity type offered first. |
| Opportunity type | **Opportunity** | Make one type per kind of deal, for example *Sales Opportunity* and *Renewal*. In the part's settings, pick the stages this type uses; none picked means every stage. |

A type with the Omnichannel Contact part and no Lead part is a contact type, as before.

## Work with leads

**Interaction Center > Leads** lists your open leads. Click **New** followed by the lead type's name to add one. Besides the name and contact methods, a lead has:

| Field | What it is for |
| --- | --- |
| **Lead status** | Where the lead is in qualification, for example *Working - Contacted*. New leads get the default status. |
| **Company** | The company the lead works for. Conversion can find or create an account with this name. |
| **Lead source** | Where the lead came from, for example *Trade show*. |
| **List** | The list or file it arrived in, so a whole list can be loaded, reported on and cleaned up together. |
| **Rating** | *Hot*, *Warm* or *Cold*. |
| **Lead owner** | The user responsible for the lead. |

Use these search terms on the Leads list:

| Term | Matches | Example |
| --- | --- | --- |
| `status:` | a lead status by name | `status:Nurturing` |
| `converted:` | converted or open leads | `converted:true` |
| `source:` | a lead source | `source:"Trade show"` |
| `list:` | a list name | `list:"Spring Import"` |
| `rating:` | a rating | `rating:hot` |
| `owner:` | the lead owner's user name | `owner:alex` |

The `phone:` terms from [Contacts](contacts.md) work here too.

### Call and text leads

Everything you do with a contact works on a lead: **Add Activity**, **List Activities**, calls, texts and automated AI conversations.

- **Inventory loads.** Pick the lead type as the **Contact content type** and a **Lead filters** panel appears. You can filter by status, list, source, rating and owner.
  - **Include closed leads** is off by default.
  - **Skip leads that are already contacts** is on by default, so a customer is not called again as a stranger.
  - Converted leads are never loaded.
  - The load report says how many leads were skipped for each reason. See [Load inventory](load-inventory.md).
- **Inbound calls and texts.** When a number belongs to both a contact and a lead, the contact wins. A converted lead never matches.

## Convert a lead

1. Open the lead and click **Convert Lead**.
2. Choose the **contact**:
   - If a contact already has the lead's phone number or email, **Merge into** is picked for you. The contact keeps its own values and only gains the lead's phone numbers, email addresses and fields it does not have yet.
   - Otherwise pick **Create a new contact** and its type.
3. Choose the **account**: an existing account, a new one (named after the lead's company unless you change it), or none. A contact you merge into keeps the account it already has.
4. Tick **Create an opportunity** to record the deal. Pick its type and optionally enter a name, amount and close date.
5. Click **Convert**.

What conversion does:

- The lead's finished activities move to the contact. Its open activities move too, unless you chose to cancel them.
- Do-not-contact preferences are combined: if either the lead or the contact opted out of calls, texts or email, the contact is opted out.
- The lead takes the *Converted* status and becomes read-only, with a banner that links to the contact. It leaves the Leads list, which shows open leads only.
- Converting the same lead again does nothing.

You cannot convert a lead while a call or message with it is in progress.

### Convert from a disposition

A subject flow can convert a lead when an activity is completed with a certain disposition. For example, *Qualified* can convert the lead and schedule a welcome call on the new contact.

1. Open **Interaction Center > Subject Flows**, and click **Manage Flow** on the subject.
2. Click **Add Action**, then **Convert Lead**, and pick the disposition.
3. Choose what happens to the account, whether to create an opportunity, and what to do with the lead's other open activities.

The **Convert Lead** action runs before the other actions of the same disposition, so a **New Activity** action on that disposition schedules its activity on the contact. It does nothing when the activity belongs to a contact.

Every subject action also has a **Set lead status** field. When the activity belongs to a lead, the lead moves to that status, for example *Working - Contacted* after a no-answer. It has no effect on contacts.

## Accounts

An account is the company or household your contacts and opportunities belong to. Open **Interaction Center > Accounts** to list them.

- **Add a contact or opportunity to an account** with the **Account** picker on its editor, or create it from the account's page.
- **See everything in an account.** The account's page lists its contacts and opportunities. **List Activities** shows the activities of every contact in the account.
- **Deleting an account** leaves its contacts and opportunities in place, outside any account.

Every contact and opportunity type can join an account without extra setup. Leads never join an account; they get one when they are converted.

## Opportunities

**Interaction Center > Opportunities** lists your open opportunities. Each one has a **Stage**, **Amount**, **Probability**, **Close date**, **Owner** and **Primary contact**, and can belong to an account. The stage decides whether the opportunity is open, won or lost. A new opportunity starts with its stage's probability, and a closed stage always sets it.

Use `stage:`, `closed:`, `won:` and `account:` on the Opportunities list, for example `closed:false`.

## Lead statuses and opportunity stages

Manage both lists under **Interaction Center > Management**.

- **Lead Statuses** (permission **Manage lead statuses**): each status has a name, a description, an order, and whether it is the **default** or a **closed** status. Exactly one status is the **converted** status; conversion sets it, and you cannot pick it by hand.
- **Opportunity Stages** (permission **Manage opportunity stages**): each stage has a name, a description, an order, a **probability** from 0 to 100, and whether it is **closed** and **won**. A stage can only be won if it is closed.

## Import and export leads

Import and export leads the same way as [contacts](contacts.md), through Bulk Import and Bulk Export. This needs the Content Transfer feature.

When you import a lead type, the import screen adds:

| Option | What it does |
| --- | --- |
| **Skip numbers that already belong to a contact** | Leaves out rows whose number a contact already has. On by default. |
| **Skip numbers that already belong to an open lead** | Leaves out rows that duplicate a lead you are still working. On by default. |
| **List name**, **Lead source**, **Lead status**, **Lead owner** | Given to every new lead whose row does not set its own. |

The file can also carry `LeadStatus` (by name), `LeadSource`, `LeadList`, `Company`, `Rating` and `LeadOwner` (by user name) columns. Download the template from the import screen for the full list. A converted lead in the file is left unchanged.

With the Do-not-call registry option on, you can also choose what happens to a number found on a registry:

- **Skip the row** leaves it out of the import.
- **Import it marked Do not call** keeps the record, so the number is recognised if it is bought again, and Do not call keeps it out of every call.

When you export a lead type, **Leave out converted leads** is on by default, because a converted lead lives on as its contact. The export includes `IsConverted`, `ConvertedUtc` and `ConvertedContactItemId`.

## Reports

Three reports appear under **Reports** in the CRM and campaigns category. Each one is filtered by date range.

| Report | Shows |
| --- | --- |
| **Lead funnel** | The leads created in the period, by their current status, with the share converted. |
| **Lead conversion by source and list** | Leads, conversions, conversion rate and average time to convert, by lead source and by list. |
| **Opportunity pipeline** | Open opportunities by stage with their amount and weighted amount, and the win rate of those closed in the period. |
