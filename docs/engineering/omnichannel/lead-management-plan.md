---
sidebar_label: "Leads, Accounts and Opportunities Plan"
sidebar_position: 7
title: Leads, Accounts and Opportunities — Project Plan
description: Design plan for Salesforce-style leads, accounts and opportunities in the Omnichannel CRM — a separate, callable "dirty" lead record that agents, the dialer and AI work like a contact, converted into a clean contact in an account, optionally with an opportunity.
---

# Leads, Accounts and Opportunities — Project Plan

> **Status: built** in the *Omnichannel CRM* feature (`CrestApps.OrchardCore.Omnichannel.Crm`). Phases 1 to 5 and
> the reports and workflows from phase 6 are in. [Deferred](#deferred) lists what was added later and what was
> decided against. The rest of this page is the design record, written against `df12548ed` (main); where the build
> differs, [As built](#as-built) wins.

## As built

These are the places where the build differs from the design below.

- **Names.** The lead part is `LeadPart`, and its index is `LeadIndex`. Opportunities have `OpportunityIndex`.
- **Record kinds.** There is no enum. `OmnichannelRecordKinds` decides the kind from a type's parts: `IsContact`,
  `IsLead`, `IsAccount`, `IsOpportunity` and `IsAccountChild`.
- **Contact index.** `OmnichannelContactIndex` gained `ContentType` and `IsConverted`, with no `RecordKind` column.
  Migration `UpdateFrom11Async` backfills both columns in SQL, one content type at a time.
- **Lead settings.** `LeadPartSettings` holds only `TargetContactContentType` and `DefaultOpportunityContentType`.
  - Conversion copies the parts and fields that the lead and contact types share by name and kind. There is no
    mapping screen.
  - `ListId` was not built; `ListName` is the list key.
  - There is no `ConvertedStatusId` override; the catalog's converted status is used.
- **Feature dependencies.** The feature depends on Omnichannel Management, `OrchardCore.Lists` and `OrchardCore.Title`.
  Content Transfer is optional: the lead import and export columns and options register only when it is on.
- **Accounts on conversion.** A contact the lead is merged into keeps the account it already has.
  - The chosen account, or the one *Automatic* finds or creates, applies to a new contact and to the opportunity.
  - In *Automatic* mode, the opportunity joins the merged contact's account.
- **Subject actions.** *Convert lead* runs before the other actions of the same disposition, so a follow-up *New
  activity* lands on the contact. *Set lead status* is a field on every action and has no effect on contacts.
- **Channels.** Messaging conversations are re-pointed by `MessagingLeadConversionRepointer`.
  - Open callbacks and unresolved voicemails are re-pointed too. Their indexes have no contact column, so they are
    read by status.
  - Finished callbacks and resolved voicemails stay with the lead.
- **Reports.** There are three reports in the CRM and campaigns category: *Lead funnel*, *Lead conversion by source and
  list* and *Opportunity pipeline*.
  - They read leads and opportunities, not activities, so they declare only the date range through
    `IReportFilterMetadata`.
  - The activity filters (campaign, channel, source, status) are not shown for them.

## Deferred

Built after the first pass:
- **Workflows.** A **Lead Converted** event and a **Convert Lead** task.
- **Callback and voicemail re-pointers.** They move a converted lead's open callbacks and unresolved voicemails, read by status because those indexes have no contact column.
- **`LastScrubbedUtc`.** An import that checks registries now sets it, and the export includes it.
- **Lead status Type menu.** Open, Closed or Converted replaces separate closed and converted flags that could contradict each other. A default status must be open.
- **Unused field removed.** `MatchedContactItemId` was never set, so it was dropped; the Convert screen finds matching contacts live.

Decided against, with the reason:
- **An AI `convertLead` tool.** The AI already dispositions automated conversations, and the *Convert Lead* subject action runs on that disposition. That path keeps the subject flow's settings and audit, while a separate tool would let the model convert outside the flow.
- **Lead status auto-advance.** *Set lead status* on each subject action moves the status per disposition. An automatic rule would compete with it.
- **A load-time registry re-check.** Every call is already screened against the registries when it is dialed, by the dialer eligibility service and the manual call screener. Registries change daily, so a check at load time would only be an older copy of that same check.
- **A field-mapping screen.** Conversion copies the parts and fields both types share by name, so naming fields alike covers it. Revisit if a tenant needs differently named fields mapped.
- **A favorites re-pointer.** A messaging favorite also matches its thread by channel and address, which conversion keeps, so favorites keep working.

## The problem

Today every omnichannel surface works on a **contact**, which is any content item whose type has
`OmnichannelContactPart`. There is no separate notion of a lead. Purchased lists, trade-show scans, web-form
submissions and old spreadsheets go straight into the same records as real customers. That has two effects:

- **Contacts are not clean.** Wrong numbers, duplicates, people who never answered and people who said "not
  interested" sit next to paying customers in the same list, index, reports and inbound caller-ID matches.
- **There is no qualification step.** Nothing records the moment an unknown prospect became a known person. There is
  no conversion rate, no "leads by source" and no "time to convert".

The goal is to follow the Salesforce model. A **Lead** is a separate, disposable record that is still fully
reachable: it can be called, texted, loaded into inventory for a subject, retried by the subject flow and handled by
AI. When it qualifies, it is **converted** into a contact, and its history moves with it.

## How Salesforce handles leads

| Concept | Salesforce behavior | What we take from it |
| --- | --- | --- |
| Separate object | `Lead` is its own object with its own list views, fields, page layouts and permissions. It is not a Contact with a flag. | A separate **Leads** menu and list. Lead types are never shown as contacts. |
| Lead Status | A picklist such as *Open – Not Contacted*, *Working – Contacted*, *Closed – Converted* and *Closed – Not Converted*. One value is marked as the "converted" status. | A **Lead Status** catalog. One status is flagged *Converted*, and some statuses are flagged *Closed*. |
| Lead Source, Rating, Company | Standard fields used for reporting and routing. | They go on the lead part as indexed columns. |
| Owner / queues | Leads are owned by a user or a lead queue, and assignment rules route new leads. | An owner is stored on the lead. Assignment is done through inventory loads, which we already have, rather than a new rules engine. |
| Polymorphic activities | Tasks and events have `WhoId` pointing to a Lead **or** a Contact. Calls are logged against the lead before conversion. | `OmnichannelActivity.ContactContentItemId` + `ContactContentType` already work this way. The activity points at whichever record it is about. |
| Campaign members | A `CampaignMember` can be a lead or a contact. | Campaigns in this system never reference the contact, so nothing needs to change. |
| Compliance | `DoNotCall` and `HasOptedOutOfEmail` exist on both objects. The `Individual` object holds person-level consent shared across a person's lead and contact records. | `OmnichannelContactPart` flags work on leads as they are. `ContactOptOutResolver` already acts like `Individual`: an opt-out on any record at the same number protects every other record at that number. |
| Account | The company. It has many contacts and many opportunities. A contact has one primary `AccountId`, and activities on the account's contacts and opportunities roll up to it. | An **Account** content type whose `ListPart` contains contacts and opportunities. See [Accounts](#accounts). |
| Opportunity | A deal in progress on an account. It has `StageName` (from a picklist carrying probability, `IsClosed` and `IsWon`), `Amount`, `CloseDate`, owner, lead source, primary campaign source and contact roles. | An **Opportunity** content type contained in an account. See [Opportunities](#opportunities). |
| Conversion | *Convert* creates or chooses an Account, creates a Contact inside it or merges into an existing one after a duplicate check, and optionally creates an Opportunity. Custom lead fields are mapped to target fields. Open and closed activities, campaign memberships, notes and files move to the contact. The lead is marked `IsConverted` and stores `ConvertedContactId`, `ConvertedAccountId`, `ConvertedOpportunityId` and `ConvertedDate`. It becomes read-only and disappears from standard views. Conversion cannot be undone. | All of it. |
| Import | The Data Import Wizard imports leads with matching by email or name, assignment rules and a default source and status. | The same ContentTransfer pipeline, with lead columns, a file-level source and list tag, and duplicate checks that know about kinds. |

## How contacts work today

**A contact is a part, not a type.** Nothing hard-codes a contact type name. The
`OmnichannelConstants.ContentTypes.OmnichannelContact` constant is commented out, and the `OmnichannelContact`
stereotype is declared but never read. A type becomes a contact type when `OmnichannelContactPart` is attached.

- `ContentDefinitionOmnichannelContactTypeProvider` (Omnichannel.Core) and the cached `OmnichannelContentTypeProvider`
  (Managements) decide which types are contact types. Messaging, the menu, the batch editors and the list scope all
  ask them.
- `OmnichannelContactDefinitionHandler` injects the system `ContactMethods` bag, which holds `PhoneNumber` and
  `EmailAddress` items, into every such type.
- `OmnichannelContactPart` stores `TimeZoneId` and the `DoNotCall`, `DoNotSms` and `DoNotEmail` flags with their UTC
  timestamps. It has no name, owner or status fields.
- The docs already tell users to model "customers/leads however you want". The contact-type screencast creates a type
  literally named `Lead`.

**Indexes**

- `OmnichannelContactIndex` holds `ContentItemId`, `Published`, `Latest`, `TimeZoneId`, the primary cell and home
  numbers (national digits and E.164) and the primary email. **It has no `ContentType` column.** Every phone lookup
  therefore returns matches from every type that carries the part.
- `OmnichannelContactCommunicationPreferenceIndex` holds the DNC, DNS and DNE flags and their timestamps.
- `OmnichannelActivityIndex` holds `ContactContentItemId`, `ContactContentType`, `PreferredDestination`,
  `SubjectContentType`, `CampaignId` and other columns.

**Admin UI**

- *Interaction Center → Contacts* (`Managements/Services/AdminMenu.cs:50-69`) is the standard Orchard content list,
  with `contentTypeId` set to every contact type.
- On that list, the name-or-phone search engages only when `OmnichannelContactListScope` confirms that every listed
  type is a contact type.
- `OmnichannelContactDisplayDriver` adds the *List Activities* button and the *Activities / Edit / Add Activity* bar.
- `ActivitiesController` hosts the contact's activities page and the outbound and inbound *Add Activity* screens.
- There are no contact-specific permissions. Contacts use Orchard's per-type content permissions.

**Activities, subjects and flows**

- `OmnichannelActivity` points at its record through `ContactContentItemId` + `ContactContentType`, with a resolution
  status of Unknown, Unresolved, Resolved or Ambiguous. `TryResolveContact` accepts any content item.
- The number or address to use is frozen on `PreferredDestination` when the activity is created. The dialer dials
  that value and never re-reads the bag.
- `DefaultSubjectActionExecutor` (Finish, Try Again, New Activity) copies the contact link onto follow-up activities
  and can set DNC, DNS and DNE on the contact. It works from the part and the bag only.

**Inventory loads**

- An `OmnichannelActivityBatch` targets **one** `ContactContentType`, and the picker offers every contact type.
- `DefaultContactActivityBatchLoader` pages over `ContentItemIndex` for that type and applies these filters: created
  range (already labelled "lead created from/to"), published only ("only published leads"), phone, time zone and last
  activity.
- For each record it then checks, in order:
  - the limit;
  - duplicates, meaning an open activity for the same **subject**;
  - opt-out, meaning the record's own flags and then shared-number opt-outs;
  - whether the record has a destination on the channel.
- Each skip reason is counted on the load report. The batch loader is pluggable through `IActivityBatchLoader`.

**Import and export** (ContentTransfer)

- `OmnichannelContactPartContentImportHandler` maps the Email, Cell and Home columns, the time zone and the DNC
  columns. It normalizes phone numbers to E.164 using the file's selected country and infers the time zone from the
  phone number.
- `OmnichannelContactImportRowFilter` handles two checks:
  - **Duplicates by phone:** matched tenant-wide across every contact type and within the file.
  - **National and local DNC registries:** a listed row is **skipped**, not imported with DNC set. The check fails
    closed: a number that cannot be parsed and a registry that cannot be reached both skip the row.
- `DncRegistrySettings.EnforceGlobally` and `EnforcedRegistryKeys` force registry checks on every import.
- Export writes the same columns. It can also add "last completed activity of subject X" columns through
  `ContactActivityExportHandler`.

**DNC by stage**

| Stage | Checks |
| --- | --- |
| Import | Registry check. Listed rows are skipped. |
| Load | The record's flags and shared-number opt-outs. **No registry check.** |
| Dial | Dialer eligibility and the manual call screener check the record's flag, every registry and the calling window from `TimeZoneId`. |
| SMS | STOP sets `DoNotSms` on the conversation's linked record. The SMS channel refuses to send when that flag is set. |

**Channels**

- **Inbound voice** (`InboundContactLookup`) and **inbound SMS** (`SmsMessagingChannel.FindContactIdsAsync`) match
  caller ID against `OmnichannelContactIndex` with no type filter. Neither creates records automatically. For
  resolution, voice treats one match as Resolved and more than one as Ambiguous, while messaging simply takes the
  **first** match.
- `MessagingConversation` stores only `ContactContentItemId` (no type). Its customer key is `contact:{id}`.
- `CallbackRequest`, `SharedVoicemail` and the incoming-call context also store only an id.
  `ScheduleCallbackTask` never sets `ContactContentType`, so callback activities from the workflow have an empty
  type.

### The key conclusion

Because every channel keys off the **part** and never a type name, a lead type that carries `OmnichannelContactPart`
already gets dialing, texting, DNC, time zones, subject flows, retries, AI conversations and inventory loads for
free. What is missing is:

1. A way to tell a lead type from a contact type.
2. Lead state: status, source, owner and conversion fields.
3. Conversion.
4. Lead-aware behavior in the places where the two meet: lists, caller-ID and SMS matching, import duplicate
   checks, inventory filters and reports.

## Design decision: a lead is a contact-capable type with a lead marker

**Recommended.** Keep `OmnichannelContactPart` on lead types, which makes the part mean "a reachable party", and add
an `LeadPart` that marks the type as a lead type and holds lead state.

- Contact types are types with `OmnichannelContactPart` and **without** `LeadPart`.
- Lead types are types with both parts.

**Rejected alternative:** a parallel `LeadPart` with its own methods bag and index, used *instead of* the
contact part. The deep dive found about 60 call sites that key off `OmnichannelContactPart`, the `ContactMethods` bag
or `OmnichannelContactIndex`. They cover the dialer, screener, SMS channel, STOP handling, opt-out resolver, subject
writer, time-zone handler, phone verification, import, export, loader and reports. Every one would need a second code
path, and each duplicated compliance path is a place a DNC check could be missed. Sharing the part means leads get the
same compliance checks as contacts, with no second code path to keep in sync.

### Record kind

A small enum in Omnichannel.Core:

```csharp
public enum OmnichannelRecordKind
{
    Contact,
    Lead,
}
```

- `IOmnichannelContactTypeProvider` and `OmnichannelContentTypeProvider` gain `GetKind(contentType)`,
  `GetContactContentTypesAsync(kind)` and `GetLeadContentTypesAsync()`.
- The existing `GetContactContentTypesAsync()` keeps meaning "every reachable type", so channel code keeps working.
  Callers that mean clean contacts (the Contacts menu, the messaging composer's default search, contact import
  duplicate scope) switch to the `Contact` kind explicitly.

## Data model

### `LeadPart` (Omnichannel.Core)

| Member | Purpose |
| --- | --- |
| `StatusId` | A `LeadStatus` catalog entry. On create it is set to the catalog's default. |
| `SourceId` | The content item id of a `LeadSource` item, such as *Web form*, *Purchased list* or *Trade show*. Lead sources are content items rather than a catalog because nothing in the CRM acts on which source a lead has. |
| `ListId` / `ListName` | The import or list this lead arrived in, so a dirty list can be loaded, reported on and purged as a unit. |
| `Company`, `Rating` | Salesforce parity. Rating is Hot, Warm or Cold. |
| `OwnerId` | The user who owns the lead. It is not the Orchard author. |
| `IsConverted`, `ConvertedUtc`, `ConvertedById`, `ConvertedByUsername` | Conversion audit. |
| `ConvertedContactItemId`, `ConvertedContactType` | The contact the lead became or was merged into. |
| `ConvertedAccountItemId`, `ConvertedOpportunityItemId` | The account the contact was placed in, and the opportunity created at conversion, when there was one. |
| `LastScrubbedUtc` | The last time a registry check cleared this lead's numbers. See [DNC](#dnc-and-compliance). |

Part settings, on the lead content type:

| Setting | Purpose |
| --- | --- |
| `TargetContactContentType` | The contact type that conversion creates. |
| `DefaultOpportunityContentType` | The opportunity type offered first when conversion creates an opportunity. |
| `FieldMappings` | A lead part or field mapped to a contact part or field. |
| `ConvertedStatusId` | Optional override of the catalog's converted status. |

### `LeadStatus` catalog

Stored through `INamedCatalogManager<>`, like dispositions. Each entry has:

- `Name` (fixed after creation) and `Description`;
- `Order`;
- `IsDefault`;
- `IsClosed`: a closed lead is not loaded by default;
- `IsConverted`: exactly one entry has it, and it cannot be picked by hand.

It gets a deployment step, a recipe step and a schema, matching the inventory's portability rule. Seeded values are
*Open – Not Contacted* (default), *Working – Contacted*, *Nurturing*, *Closed – Not Converted* (closed) and
*Converted* (closed, converted).

### Indexes

- **New `LeadIndex`**, a map index over items with `LeadPart`. Columns: `ContentItemId`,
  `ContentType`, `Published`, `Latest`, `StatusId`, `IsClosed`, `IsConverted`, `SourceId`, `ListId`, `OwnerId`,
  `ConvertedContactItemId` and `ConvertedUtc`. It drives the Leads list filters, inventory filters and lead reports.
- **Add `ContentType` and `RecordKind` to `OmnichannelContactIndex`.** Add `IsConverted` too, or join through the lead
  index; denormalizing is simpler for the hot caller-ID path. This is what lets channel lookups rank and exclude
  records.
  - The migration adds nullable columns and backfills them from `ContentItemIndex`.
  - It must follow the SQLite migration rule: probe on the host transaction and never open a second connection
    inside a migration step.
  - It must avoid SQL Server `DROP COLUMN` on defaulted columns.

### Activity and linked records

- `OmnichannelActivity` needs no new link. `ContactContentType` already says whether it points at a lead. Add
  `ConvertedFromLeadItemId` (nullable) so an activity moved at conversion keeps its origin for reporting ("activities
  worked before conversion").
- `MessagingConversation` gains `ContactContentType`, filled on resolve, so the workspace can badge "Lead" without
  loading the item.
- `CallbackRequest` and `SharedVoicemail` do the same.
- Fix `ScheduleCallbackTask` to resolve and set `ContactContentType`. This is an existing bug that leads would make
  more visible.

## Accounts

An account is the company or household that contacts and opportunities belong to. It follows the Account spec: an
Orchard content type and a marker part, with Orchard's `ListPart` holding the children. It has no custom table and no
relationship framework of its own.

### What the migration creates

- An `AccountPart` marker with no properties, and an `Account` type with `TitlePart`, `AccountPart` and `ListPart`.
  - `ListPartSettings.ShowHeader = true`.
  - `ContainedContentTypes` holds every **account-child** type that exists when the migration runs (see below).
- It is create-only. If an `Account` type already exists, the migration leaves it alone, so an administrator's
  changes survive upgrades.
- `AccountPart` is attachable, so a tenant can add more account types (for example `Household`). Everything below
  applies to every type that carries `AccountPart`.

### Account-child types come from parts, not type names

No `Contact` or `Opportunity` type is hard-coded:

- A contact is any type with `OmnichannelContactPart`, and tenants name theirs (`Customer`, `Contact`, `Employee`).
- An opportunity is any type with `OpportunityPart` (see [Opportunities](#opportunities)).

So the list's allowed types are driven by a small set of **account-child parts**. Today that set is
`OmnichannelContactPart` and `OpportunityPart`, and a later CRM part can add itself to the same set.

- **At migration time**, every existing type that carries an account-child part is added to the list of every
  account type.
- **When an account-child part is attached to a type later**, a content-definition event handler adds that type to
  every account type's list, **once**. It writes the stored definition a single time, so an administrator can still
  remove the type afterwards. This is safer than injecting the types at definition build time the way the
  `ContactMethods` bag is injected, which would silently undo an administrator's removal.
- **The same handler covers new account types.** When `AccountPart` is attached to a new type, its list is seeded
  with the current account-child types.
- **Lead types are never contained.** A type carrying `LeadPart` is skipped, and attaching the lead part to
  a type removes it from the account lists. A lead is not yet anyone's contact; it carries the company as text until
  conversion.
- The account page then shows one **Create** button per contained type: *Customer*, *Sales Opportunity*, *Resell
  Opportunity*, and so on.

### Things the spec's text needs adjusted

**`ListPart` alone cannot put an existing contact in an account.** A contained item gets its `ContainedPart` only
when it is created from inside the list. The standard editor has no field for choosing or changing an item's
container. That leaves three gaps:

- A contact created from the Contacts menu has no account.
- A contact cannot be moved to another account.
- Import and conversion cannot use the standard UI to place a contact.

The fix is one small editor on contact and opportunity types: an **Account** picker that reads and writes
`ContainedPart.ListContentItemId`. It reuses Orchard's own `ContainedPart` storage and `ContainedPartIndex`, so it adds
no relationship infrastructure. It is UI only. Conversion and import set `ContainedPart` in code.

**One account per contact.** `ContainedPart` has a single parent, which matches Salesforce's primary `AccountId`.
Salesforce's optional *Contacts to Multiple Accounts* is out of scope.

**Contacts still appear in the Contacts menu.** Orchard's content list shows contained items unless it is given a
list id (`ListPartContentsAdminListFilter`), so placing a contact in an account does not hide it from the Contacts
menu.

### Activities roll up by query, not by containment

Activities are YesSql documents, not content items, so `ListPart` cannot contain them. An account's activities are its
contacts' activities: `OmnichannelActivityIndex.ContactContentItemId` joined to `ContainedPartIndex.ListContentItemId`.
No new column is needed. An **Activities** card on the account's admin page, which is a display driver on
`AccountPart`, lists them. That is the one piece of account UI the spec's "no custom UI unless required" allows.

### Account questions still open

- **Deleting an account that still has contacts or opportunities.** Block the delete, or detach the children.
- **Mixed child list.** The account page shows contacts and opportunities in one list, one Create button per type.
  Salesforce shows separate *Contacts* and *Opportunities* related lists. Separate cards filtered by
  `ContainedPartIndex.ContentType` can come later.

## Opportunities

An opportunity is **any content type that carries `OpportunityPart`**. This is the same pattern as contacts
(`OmnichannelContactPart`) and subjects (`OmnichannelSubjectPart`). A tenant defines as many opportunity types as it
has kinds of deals, for example *Sales Opportunity*, *Resell Opportunity* and *Business Opportunity*. Each is its own
content type with its own fields, and each is contained in an account.

**Content model**

- `OpportunityPart` is attachable, like the contact part. The feature's migration defines the part only. It creates
  no opportunity type, because the types are the tenant's to name. The starter recipe adds a sample
  *Sales Opportunity*.
- An opportunity is contained in an account through `ContainedPart`. The account is optional, so a B2C tenant can
  have deals without accounts.
- Unlike `AccountPart`, `OpportunityPart` cannot be a bare marker. Pipeline reports and conversion need these values
  in code:
  - `StageId`, from an **Opportunity Stage** catalog. Each stage has a fixed name, an order, a default probability
    and `IsClosed` / `IsWon` flags.
  - `Amount`, `CloseDate`, `OwnerId` and `Source`.
  - `CampaignId`: the primary campaign source.
  - `PrimaryContactItemId` and `ConvertedFromLeadItemId`.
- **Stages per opportunity type.** A resell deal and a new-business deal rarely move through the same steps.
  Salesforce handles this with a *Sales Process* per record type. Here, the `OpportunityPart` **part settings** on each
  type choose which catalog stages apply, and in what order. This is the same place subject flows keep their
  settings. The seeded catalog is *Prospecting*, *Qualification*, *Proposal*, *Negotiation*, *Closed Won* and
  *Closed Lost*. A type that picks no stages uses all of them.
- Anything else a type needs (products, term, renewal date) is an ordinary Orchard field on that type.
- Contact roles in v1 are the primary contact plus a multi-select `ContentPickerField` limited to contact types.
  Salesforce's `OpportunityContactRole` with a role per contact can come later.
- An `OpportunityIndex` (content type, stage, is closed, is won, amount, close date, owner, account, campaign) feeds
  the pipeline reports. Every report can be narrowed by opportunity type.

**Links to omnichannel work**

- An activity can optionally be *related to* an opportunity, which is Salesforce's `WhatId`. This is a nullable
  `OpportunityContentItemId` on `OmnichannelActivity` and its index, set when an activity is logged from an
  opportunity or created by conversion.
- **Create opportunity** (of a chosen opportunity type) and **Set opportunity stage** subject actions let a
  disposition move a deal forward.
  For example, *Proposal accepted → stage Closed Won*.

## Admin UI

### Menus and lists

Salesforce gives each record its own tab: Leads, Accounts, Contacts and Opportunities. *Interaction Center* gets the
same four items.

**Menu layout**

```
Interaction Center
├── Activities       (unchanged: the agent's work list, kept first)
├── Leads            (new)
├── Accounts         (new)
├── Contacts         (narrowed to contact-kind types)
├── Opportunities    (new)
└── Management       (unchanged, kept last)
```

- The four record items follow the lifecycle: a lead converts into an account, a contact and an opportunity.
- `AdminMenu` places them today with `PrefixPosition()`, which sorts alphabetically. That would put *Management*
  between *Leads* and *Opportunities*, so these items get explicit positions.

**What each item opens.** Each is the standard Orchard content list with `contentTypeId` set to the types of that
kind, the way Contacts works today:

| Item | Types listed | Default filter |
| --- | --- | --- |
| Leads | Types with `LeadPart` | `converted:false` |
| Accounts | Types with `AccountPart` | |
| Contacts | Types with `OmnichannelContactPart` and without `LeadPart` | |
| Opportunities | Types with `OpportunityPart`, so *Sales*, *Resell* and *Business* opportunities appear in one list | `closed:false` |

**When each item shows**

- An item appears only when at least one type of its kind exists. Today *Contacts* renders with no link when there
  are no contact types; it gets the same treatment.
- The item is gated by *List content*. Orchard's per-type content permissions then decide which rows a user sees.
- **Campaigns stays under Management.** It is a Salesforce tab too, but here a campaign is reporting and grouping
  configuration rather than a record agents work.
- Contact rows show their account. Opportunity lists get `stage:`, `owner:`, `closed:` and `account:` filter terms.
- `OmnichannelContactListScope` keeps engaging name-or-phone search on both lists, because lead types still carry
  the contact part.
- New `IContentsAdminListFilterProvider` terms on lead-scoped lists:
  - `status:`, `source:`, `list:`, `owner:` and `rating:`;
  - `converted:true|false`. The Leads menu link defaults to `converted:false`, which is Salesforce's "converted leads
    disappear from views".
- The same terms appear as cards in the Filters popover, following the pattern of the existing Phone card.
- Summary rows show a status badge, the source and the owner.

### Lead editor and activities page

- The editor has a Status select that hides the converted status, plus Source, List, Company, Rating and Owner (a
  `UserPicker`).
- `OmnichannelContactDisplayDriver` already renders the *Activities / Edit / Add Activity* bar for any part holder.
  Add a **Convert** button, gated by `ConvertLead` and hidden once the lead is converted.
- **A converted lead is read-only.**
  - The driver renders a banner: "Converted to *Jane Doe* on …", linking to the contact.
  - A content handler refuses `UpdateAsync` or publish on a converted lead, except when the conversion service
    does it.
  - *Add Activity* is hidden.
- The activities page, the dialer's agent workspace and the incoming-call screen-pop show a **Lead** badge. The
  screen-pop's "Matched customers" cards order contacts first.

## Conversion

### Entry points

1. **Convert** button on a lead: opens a conversion screen.
2. A **Convert lead** subject action: a new action type next to Finish, Try Again and New Activity. A disposition
   such as *Qualified* on a lead-generation subject converts the lead as part of completing the activity. The action
   runs **first**, so later actions in the same run target the new contact. For example, *Qualified → Convert lead →
   New Activity on "New Customer – Welcome"* schedules the welcome call against the clean contact.
3. **AI**: a `convertLead` tool for automated subjects whose flow allows it. It is the same shape as
   `transferToLiveAgent`: it records the decision on the turn context, and the conclusion runs the conversion. This
   builds on the existing `HandoffOnQualifiedLead` wording and the *qualify leads* templates.
4. **API and workflow**: `ILeadConversionService.ConvertAsync(request)` plus an Orchard Workflows **Convert lead**
   task.

### Conversion screen

- **Account:**
  - *Create a new account* named from the lead's `Company`.
  - *Choose an existing account*. Candidates are accounts whose title matches the company, pre-selected when exactly
    one matches.
  - *No account*, for B2C tenants.
- **Contact:**
  - *Create a new contact* of `TargetContactContentType`, placed in the chosen account.
  - *Merge into an existing contact*. Candidates are contacts at the lead's normalized cell and home numbers or email,
    found through `OmnichannelContactIndex` with `RecordKind = Contact`, the same matching the import filter uses.
    Candidates are shown pre-selected when one matches, as Salesforce does.
- **Opportunity:**
  - *Don't create one* (the default), or *create one*.
  - A new opportunity takes a type (any opportunity type, with a default set in the lead part settings), a name
    (default `{Company} – {date}`), a stage (the type's first open stage), an amount and a close date.
  - It also gets the lead's source, the campaign of the activity that converted it, and the new contact as primary
    contact.
- **Mapped values preview:** the lead value, the contact value and the result.
- **Open activities:**
  - *Move to the contact*, the default and the Salesforce behavior.
  - *Cancel them*: status `Cancelled` with a "lead converted" terminal reason.
- **Resulting lead status:** fixed to the converted status.

### What conversion does

It runs in one YesSql session. Activities are re-pointed in pages, following the rule that a batch is reloaded by id
inside the loop.

1. **Guard.**
   - The lead is not already converted. This makes the operation idempotent: a retry that finds `IsConverted` with a
     `ConvertedContactItemId` returns that contact.
   - None of the lead's activities is `Reserved`, `Dialing` or `InProgress`. A live call or reservation blocks
     conversion with a clear message. The subject-action path is exempt for its own activity, which is completing.
2. **Build the contact.** Either create a new item of the target type, or load the merge target.
   - **Default mapping**, used when no explicit map is set:
     - DisplayText and `TitlePart`.
     - Same-named parts and fields of the same field type.
     - The `ContactMethods` bag. Items are **cloned with fresh `ContentItemId`s**, because duplicate bag ids silently
       drop edits. On a merge, only methods the contact does not already have are added.
   - **Explicit mapping:** `FieldMappings` from the lead part settings.
   - **Compliance flags use OR, never overwrite.** If either record has `DoNotCall`, the result has it, with the
     earliest timestamp. Opt-outs never get lost on conversion. The same applies to `DoNotSms` and `DoNotEmail`.
   - `TimeZoneId` comes from the contact if it has one, otherwise from the lead.
   - **Place it in the account:**
     - For a new account, first create an `Account` item with the company as title.
     - Then set the contact's `ContainedPart`: `ListContentItemId` and `ListContentType`, with `Order` = the next
       order in the list.
     - A merged contact keeps its current account unless the screen chose a different one.
   - **Opportunity**, when requested: an `Opportunity` item contained in the same account, with
     `ConvertedFromLeadItemId` set.
   - `ILeadConversionHandler.ConvertingAsync(context)` lets modules map anything else.
3. **Re-point history.**
   - Every `OmnichannelActivity` for the lead, open and closed: set `ContactContentItemId` and `ContactContentType`,
     and set `ConvertedFromLeadItemId`.
   - `MessagingConversation.ContactContentItemId` and `ContactContentType`, with the customer key rewritten to
     `contact:{newId}` so the thread continues.
   - `MessagingFavorites`, `CallbackRequest` and `SharedVoicemail`.
   - AI chat sessions reach the record through the activity, so they follow automatically.
   - Each store gets an `ILeadConversionRepointer` implementation, registered by the feature that owns the store.
     Messaging and Contact Center register theirs, so Omnichannel does not reference them.
4. **Close the lead.** Set the converted status, `IsConverted`, the converted timestamps and actor, and
   `ConvertedContactItemId`, `ConvertedAccountItemId` and `ConvertedOpportunityItemId`, then publish.
5. **Signal.** Fire `ILeadConversionHandler.ConvertedAsync`, an Orchard Workflows **Lead converted** event and an
   audit log entry.

**No un-convert**, matching Salesforce. An administrator can still edit the contact and the converted lead's history
stays intact.

## Channels and matching

Where a lead and a contact share a number, the rule is: **contacts first, then open leads; converted leads never.**

| Surface | Change |
| --- | --- |
| Inbound voice (`InboundContactLookup`) | Rank contacts, then open leads, and exclude converted leads. Only multiple matches within the **same tier** make the result Ambiguous. A contact plus a lead at the same number resolves to the contact. |
| Inbound SMS (`MessagingContactResolver`, `SmsMessagingChannel.FindContactIdsAsync`) | Same tiers: contacts first, then open leads. Within a tier the current pick is kept, so a tenant without leads sees no change. |
| Messaging composer search, *Send SMS* To prefill | Contacts and open leads, badged. Converted leads excluded. |
| Screen-pop "Matched customers" | Contacts first, each card badged. |
| Shared-number opt-out (`ContactOptOutResolver`) | **Unchanged on purpose.** It keeps crossing kinds. A STOP from a lead's number must protect the contact at that number and the reverse. This is our equivalent of Salesforce's `Individual` consent. Converted leads are included too, since an opt-out is about the person, not the record. |
| Dialer eligibility, manual call screener | Unchanged, because they load the record by id. After conversion they load the contact. |

## Import and export

Lead types carry `OmnichannelContactPart`, so the whole existing pipeline applies unchanged: E.164 normalization with
the lead-country picker, the ContactMethods mapping, time-zone inference, the DNC columns and the registry filter.
What changes:

**Lead columns.** A new `LeadPartContentImportHandler` maps `Status` (by name), `Source`, `List`,
`Company`, `Rating` and `Owner` (by user name). Export writes the same columns plus `IsConverted`, `ConvertedUtc` and
`ConvertedContactItemId`.

**Account column on contact and opportunity imports.**

- The `Account` column matches an account by title and sets `ContainedPart`.
- Add a **Create missing accounts** option, off by default.
- Export writes the account's title.
- Accounts import and export through the standard content columns, `TitlePart` and any fields.

**File-level lead options.** This is an `ImportContent` display driver shown only for lead types, like the existing
contact import options.

- **Lead source for this file** and **List name**: stamped on every row, so a purchased list is one addressable unit.
- **Default status** for rows without a `Status` column.
- **Owner** for rows without an `Owner` column.

**Duplicate scope.** Today duplicates are checked tenant-wide across every type with the part. The check becomes a
choice.

- Contact imports:
  - *All contacts*: the default, and today's behavior, because today every type with the part is a contact.
  - *Same type*: narrower, opt-in.
- Lead imports:
  - *Skip rows whose number belongs to an existing contact* (default on). A customer should not be re-prospected.
    The alternative is *import and link*, which sets a `MatchedContactItemId` hint shown on the lead.
  - *Skip rows whose number belongs to an open lead* (default on).
  - Within-file duplicates stay on.
- This uses the new `RecordKind` column on `OmnichannelContactIndex`.
- Converted leads never count as duplicates of a new lead, because the contact they became already does.

**Registry-listed rows.** Today a registry match always skips the row. Add a **When a number is on a registry** mode:

- **Skip the row**: the default, today's behavior.
- **Import it marked Do not call**: sets `DoNotCall` + `DoNotCallUtc`, keeps the record so it is never bought or
  imported again, and still keeps it out of every call path through the existing flag checks.

`EnforceGlobally` still forces the check. The mode only changes what a match does. The check still fails closed.

**Last scrub.** A successful registry check stamps `LastScrubbedUtc` on the lead.

**Export.** Lead types get the same "last completed activity of subject X" enrichment, which works on any record
through `ContactActivityExportHandler`. Add an **Exclude converted leads** option, on by default.

## Loading activities for leads (inventory loads)

The batch editor already offers every type that has the part, so a lead type can be picked today. The plan makes it
first-class.

**Contact type picker.** It gets two groups, **Contacts** and **Leads**, from the kind-aware provider.

**Lead filters.** A `DisplayDriver<OmnichannelActivityBatch>` shown when the chosen type is a lead type. It stores
its values in the batch's `Properties` and is honored by `DefaultContactActivityBatchLoader` as another filter set
intersected with the existing ones.

- Status (multi-select). The default is non-closed statuses.
- Source.
- List. This is the "load the list I just imported" case.
- Owner.
- Rating.

**Converted leads are always excluded.** This is a hard rule in the loader, not a filter, and gets its own skip
counter, `TotalSkippedAsConverted`, so the load report explains it.

**Optional "skip leads that are already contacts"** (default on). A lead whose number belongs to a contact is counted
as `TotalSkippedAsExistingContact` rather than being called as a stranger.

**Unchanged:** subject-scoped duplicate prevention, opt-out and shared-number skips, the no-destination skip, the
dialer, manual and automatic sources, AI profiles and cadences.

**Subject flow additions**

- The **Convert lead** action type described above.
- A **Set lead status** option on every action type, next to the existing *Show communication preferences* options.
  For example: *No answer → Try Again + status Working – Contacted*, *Not interested → Finish + status Closed – Not
  Converted*, *Wrong number → Finish + status Closed – Not Converted + Do not call*. It applies only when the
  activity's record is a lead.
- Optional **auto-advance**: the first completed activity on a lead whose status is the default moves it to a
  configurable "working" status. This is Salesforce's *Open → Working* convention without manual clicks.

**Example lead-generation flow**

| Disposition | Actions |
| --- | --- |
| No answer | Try Again (max 3, +24 h); set status *Working – Contacted* |
| Call back later | Try Again at the agent's chosen time |
| Not interested | Finish; set status *Closed – Not Converted* |
| Wrong number / DNC request | Finish; set status *Closed – Not Converted*; set Do not call |
| Qualified | **Convert lead**; New Activity on *New Customer – Welcome* (which now targets the contact) |

## DNC and compliance

Summary of what leads get and what changes:

- **Same flags, same checks.** Leads use `OmnichannelContactPart.DoNotCall/Sms/Email`, the load-time opt-out skips,
  dial-time registry checks, calling windows from `TimeZoneId`, STOP handling and the SMS send refusal, all without
  new code paths.
- **Conversion never clears an opt-out.** Flags combine with OR, and the earliest timestamp wins.
- **Shared-number opt-out keeps crossing kinds.**
- **Registry matches at import** can now be recorded as DNC instead of dropped.
- **Gap noticed during the deep dive, not lead-specific:**
  - Registries are checked at import and at dial time but **not at load time**. Automated SMS loads therefore never
    see a registry check, and a list imported months ago is loaded against a stale scrub.
  - Proposal: an optional **Re-check registries when loading** batch option, reusing the import filter's
    `GetRegisteredNumbersAsync` path in batches of 100, skipping `TotalSkippedAsRegistryListed` and stamping
    `LastScrubbedUtc`. Default it on for lead types.
  - Separately, dial-time checks ignore `DncRegistrySettings.EnforcedRegistryKeys` and query every registry. Worth
    reconciling, but out of scope here.

## Permissions

- Lead records use Orchard's per-type content permissions, like contacts. *List content* for the lead type gates the
  Leads menu.
- New permissions:
  - `ConvertLead`: granted to Agent and Administrator, implied by `ManageActivities`.
  - `ManageLeadStatuses`: Administrator.
  - `EditConvertedLead`: Administrator only, and only for correcting audit fields. The read-only guard respects it.

## Reports

These are added to the existing Reports provider, in a new **Leads & Pipeline** category:

| Report | Content |
| --- | --- |
| Lead funnel | Counts by status, over time. |
| Conversion rate | By source, list, campaign and owner. |
| Time to convert | From lead created to converted, and number of activities before conversion. |
| Lead aging | Open leads by age and by days since the last activity. |
| List quality | Per imported list: rows, duplicates skipped, registry-listed, no destination, converted. |
| Pipeline | Open opportunities by stage, amount and weighted amount (by stage probability), and by close month. |
| Win rate | Closed won against closed lost, by source, campaign and owner. |

`ContactTypeWorkload` already splits by `ContactContentType`. Activities moved at conversion report under the contact
type, while `ConvertedFromLeadItemId` lets the conversion reports count them as pre-conversion work.

## Impact on existing contacts

**Rule: an upgrade changes nothing a user can see or rely on for existing contacts.** Everything new is behind the
**Omnichannel CRM** feature. Even with the feature on, an existing contact type stays a contact type, because only
types carrying `LeadPart` are leads, and that part does not exist yet.

**What does change on upgrade, and why it is safe**

| Change | Runs for | Effect on existing data |
| --- | --- | --- |
| `ContentType`, `RecordKind` and `IsConverted` columns on `OmnichannelContactIndex` | Everyone. The index provider lives in Omnichannel Management, so its table must match whether or not the CRM feature is on. | Nullable columns are added and backfilled: `ContentType` from `ContentItemIndex`, `RecordKind = Contact`, `IsConverted = false`. No contact item is rewritten. The migration follows the SQLite and SQL Server rules and gets a feature-activation test on each provider. |
| Nullable `OpportunityContentItemId` and `ConvertedFromLeadItemId` on activities and their index; `ContactContentType` on messaging conversations | Everyone, same reason | Existing rows stay null, or are backfilled for the conversation type. Activities are not re-saved. |
| `ScheduleCallbackTask` sets `ContactContentType` | Everyone | This is a bug fix. New callback activities stop showing as "(Not set)" in the contact-type report. Old ones are unchanged. |

**What is unchanged**

- **Contact type definitions.** The Account migration edits the `Account` type's list settings only. It does not
  touch any contact type.
- **Contact items.** They are never re-saved by the upgrade. A contact gets a `ContainedPart` only when someone puts
  it in an account.
- **Contacts menu.** It lists the same types as before, because every current contact type is contact-kind.
- **Dialing, SMS, DNC, time zones, subject flows, inventory loads, dedupe, reports.** They run the same code. A
  contact is still anything with `OmnichannelContactPart`.
- **Import defaults.**
  - Duplicate scope defaults to *all contacts*, which is today's tenant-wide check, since today every type with
    the part is a contact.
  - A registry match still skips the row.
  - The load-time registry re-check is off for contact types.
- **Caller-ID and SMS matching for contacts.** With no leads in the tenant there is only one tier, so the results are
  today's. Voice still reports multiple matches as ambiguous. SMS keeps its current pick within the contact tier; the
  new ordering only decides between tiers.

**Visible only after the CRM feature is enabled**

- The Leads, Accounts and Opportunities menu items.
- An optional, empty **Account** picker on the contact editor and an empty account column on contact rows.
- A **Lead** badge wherever a lead appears. No existing record gets one.

**The one opt-in that moves records.** A tenant that already models leads as a contact type (the docs screencast
creates a type named `Lead`) keeps treating those records as contacts until an administrator attaches
`LeadPart` to that type. Attaching it is the deliberate step that moves them:

- into the Leads menu;
- out of the account lists;
- behind contacts in caller-ID matching.

Detaching the part moves them back. No data is lost either way, because the lead part only adds state.

## Packaging

- Leads, accounts and opportunities ship **together** as one feature, **Omnichannel CRM**
  (`CrestApps.OrchardCore.Omnichannel.Crm`), in the Managements module.
  - They ship together because conversion is what ties the three, and building conversion before its targets exist
    would mean reworking its API and screen later.
  - The feature depends on Omnichannel Management, Content Transfer and `OrchardCore.Lists`.
- The feature's migrations create the `Account` type, the `AccountPart`, `OpportunityPart` and lead parts, and the
  seeded Lead Status and Opportunity Stage catalogs. They create no opportunity, contact or lead types; those are the
  tenant's, or come from the starter recipe.
- Everything above is registered by that feature. With the feature off, the system behaves exactly as today, and
  lead-marked types fall back to being plain contacts because the provider only honors the marker when the feature
  is on.
- The re-pointers live in the features that own their stores.
- A **Lead management starter** recipe creates:
  - a `Lead` type with `TitlePart`, first and last name, company, `OmnichannelContactPart` and `LeadPart`,
    targeting `Contact`;
  - a `Contact` type, if it does not exist;
  - the seeded statuses;
  - a sample *Sales Opportunity* type;
  - a sample *Lead Generation* subject with the flow in the example above.

  Today nothing ships a contact type at all.

## Phasing

| Phase | Scope | Exit criteria |
| --- | --- | --- |
| **1. Records** | **Accounts:** `AccountPart`, the `Account` type with `ListPart` (header shown), account-child types added to its list at migration and on attach, the Account picker on contacts and opportunities, the account Activities card. **Opportunities:** attachable `OpportunityPart` with per-type stage settings, the Opportunity Stage catalog + recipe/deployment/schema, `OpportunityIndex`. **Leads:** `OmnichannelRecordKind`, the kind-aware providers, `LeadPart` + settings, the `LeadStatus` catalog + recipe/deployment/schema, `LeadIndex`, the `ContentType`/`RecordKind` columns on `OmnichannelContactIndex` + backfill migration. **UI:** the Leads, Accounts and Opportunities menus, the Contacts menu narrowed, the list filters, the lead editor, the starter recipe. | Accounts hold contacts and opportunities. Leads and contacts are in separate lists. Every existing channel works on a lead. Migrations pass on SQLite, SQL Server and Postgres and leave existing types untouched. |
| **2. Conversion** | `ILeadConversionService` and the conversion screen: account (new, existing or none), contact (new or merge) and optional opportunity. Also mapping, compliance OR-merge, activity re-pointing, `ILeadConversionRepointer` for messaging, callbacks and voicemail, the read-only guard, the Workflows task and event, and audit. | Converting a lead with history creates the account, contact and opportunity and moves every activity and thread, and the lead is read-only and hidden. Converting twice is a no-op. Conversion is blocked during a live call. |
| **3. Channels** | Tiered matching for inbound voice and SMS, the composer, *Send SMS* and screen-pop badges, the `ContactContentType` column on conversations, callbacks and voicemail, the `ScheduleCallbackTask` fix. | A number shared by a contact and a lead resolves to the contact on both channels. |
| **4. Import/export** | The lead column handler, file-level source, list, status and owner, the duplicate scope options, the registry "import as DNC" mode, `LastScrubbedUtc`, export columns and the exclude-converted option. | A dirty list imports as one list with duplicates against contacts skipped, and registry numbers are flagged or skipped per the option. |
| **5. Inventory and flows** | Grouped type picker, lead filters, the converted and existing-contact skips + report counters, the **Convert lead** and **Set lead status** subject actions, auto-advance, the optional load-time registry re-check. | Load "List X, status Open" for a lead-generation subject, dial it, and a *Qualified* disposition converts the lead and schedules the welcome activity on the contact. |
| **6. AI and reports** | The `convertLead` AI tool, template updates, the Leads report category, user-manual and docs pages. | An automated SMS or voice qualification converts the lead, and the reports show the funnel. |

Tests follow the existing split: unit tests per service; the feature-activation suite for migrations and
recipe/deployment round trips (`ContactCenterRecipeStepSchemaTests`-style schema completeness for `LeadStatus`); and
the SQLite integration harness for a load → dial → disposition → convert run.

## Open questions

The build settled the first four the recommended way. They are kept here so the choices can be revisited.

1. **Per-type stages.** *Built:* each opportunity type picks its own subset of the stage catalog, and an empty
   selection means every stage.
2. **Open activities on conversion.** *Built:* the default is *move to the contact*. The conversion screen and the
   *Convert lead* subject action can choose *cancel* instead.
3. **Lead rows matching an existing contact at import.** *Built:* skipped by default. *Skip numbers that already
   belong to a contact* can be turned off per import.
4. **Owner semantics.** *Built:* `OwnerId` is informational. It filters the Leads list and inventory loads, but
   assignment still follows the load's users.
5. **Web-to-lead.** Still open. Until it is decided, an Orchard form plus a workflow that creates a lead item covers
   it.
