---
sidebar_label: "CRM: leads, accounts, opportunities"
sidebar_position: 5
title: Omnichannel CRM — leads, accounts and opportunities
description: How the Omnichannel CRM feature models leads, accounts and opportunities with parts on ordinary content types, how conversion works, and the extension points for modules.
---

| | |
| --- | --- |
| **Feature Name** | Omnichannel CRM |
| **Feature ID** | `CrestApps.OrchardCore.Omnichannel.Crm` |
| **Depends on** | Omnichannel Management, `OrchardCore.Lists`, `OrchardCore.Title` |

The Omnichannel CRM feature adds Salesforce-style **leads**, **accounts** and **opportunities** to the Omnichannel CRM. None of them is a new storage model. Each is an ordinary content type, recognised by the parts it carries. For a step-by-step walkthrough, see [Leads, Accounts and Opportunities](../user-manual/leads-accounts-opportunities.md) in the user manual.

With the feature off, nothing changes: every type with `OmnichannelContactPart` is a contact, as before.

## Record kinds

A type's parts decide what it is. The rules live in `OmnichannelRecordKinds`.

| Kind | Parts | Notes |
| --- | --- | --- |
| Contact | `OmnichannelContactPart` without `LeadPart` | Unchanged. |
| Lead | `OmnichannelContactPart` and `LeadPart` | Reachable like a contact; listed under **Leads**, not **Contacts**. |
| Account | `AccountPart` | Holds its children through Orchard's `ListPart`. |
| Opportunity | `OpportunityPart` | Attach it to one type per kind of deal. |

Contacts and opportunities are **account children**: they can belong to an account. Leads never do.

Because a lead keeps `OmnichannelContactPart`, every channel works on it with no second code path. That covers the dialer, SMS, do-not-contact checks, time zones and phone verification. The same compliance checks apply to leads and contacts.

## What the feature creates

The feature's migration runs once per tenant and never changes a type that already exists.

- The `LeadPart`, `AccountPart` and `OpportunityPart` definitions.
- An `Account` type, only if none exists. It has `TitlePart`, `AccountPart` and a `ListPart` with its header shown.
  - Its list contains every account-child type.
  - A contact or opportunity type added later joins the list automatically.
  - An administrator can remove a type from the list, and it is not added back.
- The `LeadIndex` and `OpportunityIndex` tables.
- `ContentType` and `IsConverted` columns on `OmnichannelContactIndex`, backfilled from existing rows.
- A seeded **Lead Status** catalog and **Opportunity Stage** catalog.

The **Omnichannel CRM starter** recipe (`OmnichannelCrmStarter`) turns the feature on and adds a `Lead` type, a `Contact` type and a `SalesOpportunity` type.

## Parts and settings

### `LeadPart`

Holds the lead's qualification state:
- status, closed flag, source, list name, company, rating (Hot, Warm or Cold) and owner;
- the conversion audit: when, by whom, and the contact, account and opportunity the lead produced.

| Setting | Purpose |
| --- | --- |
| **Converts into** (`TargetContactContentType`) | The contact type conversion creates. Empty means the person converting chooses. |
| **Opportunity type** (`DefaultOpportunityContentType`) | The opportunity type offered first on conversion. |

### `OpportunityPart`

Holds stage, open/closed/won flags, probability, amount, close date, owner, source, campaign, primary contact and the lead it came from. The account is the item's `ContainedPart`.

| Setting | Purpose |
| --- | --- |
| **Stages** (`StageIds`) | The stages this opportunity type uses, from the Opportunity Stage catalog. Empty means every stage. |

### `AccountPart`

A marker. Its setting records which types the feature added to the account's list, so a type an administrator removed is not added again.

## Caller matching

Inbound voice and SMS resolve a number in tiers:
1. contacts;
2. open leads;
3. converted leads, which never match.

A number shared by a contact and a lead resolves to the contact on both channels.

## Conversion

`ILeadConversionService.ConvertAsync` converts one lead. The conversion screen and the **Convert Lead** subject action both call it.

1. **Guards.** A lead that is already converted returns its earlier result. A lead with a live activity (reserved, dialing or in progress) cannot be converted, except for the activity being completed.
2. **Contact.** The lead merges into the chosen existing contact, or a new contact is created.
   - A merge copies only the parts and fields the contact lacks.
   - Phone numbers and email addresses are added when the contact does not have them.
   - Do-not-contact flags are combined so that any opt-out wins.
3. **Account.** The modes are none, a new account, an existing account, or *automatic*. *Automatic* finds the one account named like the lead's company, or creates it. A merged contact keeps the account it already has.
4. **Opportunity.** It is created optionally, with the contact as primary contact, in the account.
5. **Activities.** Finished activities move to the contact and keep `ConvertedFromLeadItemId`. Open activities move, or are cancelled.
6. **Re-pointers and handlers** run, and the lead is closed with the converted status.

## Extension points

| Interface | Use it to |
| --- | --- |
| `ILeadConversionHandler` | Run code before (`ConvertingAsync`) and after (`ConvertedAsync`) a conversion, for example to copy extra data or notify another system. |
| `ILeadConversionRepointer` | Move records your module keeps against the lead's id to the contact. Messaging conversations use this. |
| `ISubjectActionHandler` | Add a subject action type that runs on a disposition. `Order` decides when it runs among the actions of the same disposition; **Convert Lead** uses `-100` so it runs first. |

## Recipes and deployment

Both catalogs have a recipe step and a deployment step, matched by name on import:

```json
{
  "name": "OmnichannelLeadStatus",
  "LeadStatuses": [
    { "Name": "Working - Contacted", "Order": 2 },
    { "Name": "Nurturing", "Order": 3 }
  ]
}
```

```json
{
  "name": "OmnichannelOpportunityStage",
  "OpportunityStages": [
    { "Name": "Negotiation", "Order": 4, "Probability": 70 }
  ]
}
```

## Permissions

| Permission | Allows |
| --- | --- |
| **Convert leads** | Converting a lead. Implied by **Manage activities**. |
| **Edit converted leads** | Changing a lead after it was converted. Without it, a converted lead is read-only. |
| **Manage lead statuses** | Editing the Lead Status catalog. |
| **Manage opportunity stages** | Editing the Opportunity Stage catalog. |

Listing and editing leads, accounts and opportunities uses the ordinary content permissions of each type.

## Reports

The feature adds **Lead funnel**, **Lead conversion by source and list** and **Opportunity pipeline** to the Reports area. They read the lead and opportunity indexes and are filtered by date range only.
