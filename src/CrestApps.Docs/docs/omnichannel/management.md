---
sidebar_label: "Management (CRM)"
sidebar_position: 2
title: CrestApps Omnichannel Management (CRM)
description: Customer Relationship Management (CRM) tools for contacts, subject flows, campaigns, and activity-driven work across communication channels.
user_manual:
  - user-manual/contacts
  - user-manual/subjects
  - user-manual/dispositions
  - user-manual/campaigns
  - user-manual/subject-flows
  - user-manual/channel-endpoints
  - user-manual/cadences
  - user-manual/load-inventory
  - user-manual/automated-ai
  - user-manual/activities
  - user-manual/bulk-activities
  - user-manual/numbers-not-in-service
  - user-manual/reports
---

| | |
| --- | --- |
| **Feature Name** | Omnichannel Management |
| **Feature ID** | `CrestApps.OrchardCore.Omnichannel.Managements` |
| **Headless feature ID** | `CrestApps.OrchardCore.Omnichannel.Activities` |

Provides way to manage Omnichannel Contacts.

The screencast below enables **Omnichannel Management**, opens the **Management** area from the **Interaction Center** menu, and adds a couple of **dispositions** (activity outcomes) to the CRM catalog.

<video controls preload="metadata" width="100%" aria-label="Screen cast of enabling Omnichannel Management and adding dispositions">
  <source src="/img/docs/omnichannel-management.mp4" type="video/mp4" />
</video>

The module ships as two features. `CrestApps.OrchardCore.Omnichannel.Activities` is the headless half: contact, subject, campaign, and activity catalogs, their stores and managers, the content parts and indexes, the migrations, the permissions, and the subject-disposition endpoint. `CrestApps.OrchardCore.Omnichannel.Managements` adds the CRM administration experience on top of it - the screens, display drivers, and admin menus described below - and enabling it brings the headless feature with it.

The split exists so that a headless consumer of the activity model, such as the [Contact Center](../contact-center/index.md), can depend on the work-item data without dragging an administration experience into a tenant that serves no user interface.

## Overview

The `CrestApps.OrchardCore.Omnichannel.Managements` module is a lightweight **Customer Relationship Management (CRM)** experience built on Orchard Core.

It provides the admin tools you need to manage **contacts**, define **subject-level flows**, group work under **campaigns**, and run activity-driven processes (manual or automated) across channels. Activity loads offer the **Phone** and **SMS** channels.

Every admin screen is documented step by step, with screencasts, in the User Manual: [Contacts](../user-manual/contacts.md), [Subjects](../user-manual/subjects.md), [Dispositions](../user-manual/dispositions.md), [Subject flows](../user-manual/subject-flows.md), [Campaigns](../user-manual/campaigns.md), [Omnichannel addresses](../user-manual/channel-endpoints.md), [Cadences](../user-manual/cadences.md), [Load activities](../user-manual/load-inventory.md), [Automated AI](../user-manual/automated-ai.md), [Activities](../user-manual/activities.md), [Managing activities in bulk](../user-manual/bulk-activities.md) and [Numbers not in service](../user-manual/numbers-not-in-service.md). This page covers the data model, the features, and the extension points.

## Core concepts

### Omnichannel address
An **omnichannel address** (stored as `OmnichannelChannelEndpoint`) is an address the business owns, most commonly a phone number, that the platform sends from and receives on. Each address has an **address type** (`OmnichannelAddressTypes.PhoneNumber` today), its normalized value (numbers are stored as international `+<country code><number>`), and a list of **capabilities**: what the address is used for, named by channel (`Phone` for calls, `SMS` for texts). A number used for calls and texts is one address with both capabilities. Capability settings are attached to the address, for example the SMS provider, the [Messaging Workspace](messaging-workspace.md) inbound routing and the agents who dial out from the number.

Addresses are administered under **Interaction Center > Management > Omnichannel Addresses** (requires the **Manage omnichannel addresses** permission, `ManageChannelEndpoints`); see [Omnichannel Addresses](../user-manual/channel-endpoints.md) in the User Manual. This administration is provided by a small **dependency-only** feature:

| | |
| --- | --- |
| **Feature Name** | Omnichannel Channel Endpoints |
| **Feature ID** | `CrestApps.OrchardCore.Omnichannel.ChannelEndpoints` |

The feature is `EnabledByDependencyOnly`: you do not enable it directly. It depends only on the headless **Omnichannel Activities** feature, so a module that just needs addresses (such as the [Messaging Workspace](messaging-workspace.md)) can depend on it and reuse the address administration and services **without** pulling in the full Omnichannel Management CRM screens. Enabling **Omnichannel Management** enables it automatically, and so do **Omnichannel Messaging Workspace**, **Contact Center Voice** and **Contact Center Inbound Entry Points**.

#### Capabilities are contributed by features (extensible)

**Add Address** opens a picker of the address types that have at least one capability on the tenant. The editor shows the type's capabilities as a **Used for** checkbox list, and the settings a feature keeps on an address appear while its capability is ticked. The address type is fixed when the address is created.

A feature contributes a capability from its own startup:

```csharp
services.AddOmnichannelAddressCapability(OmnichannelAddressTypes.PhoneNumber, "SMS", capability =>
{
    capability.DisplayName = S["Text messages (SMS)"];
    capability.Description = S["Texts sent and received on this number."];
});
```

Because the capability is registered by the owning feature, it is only offered while that feature is enabled. A capability whose feature is later disabled stays on the address and comes back with the feature. **Voice calls** (`Phone`) is registered by Contact Center **Voice**; **Text messages** (`SMS`) is registered by the **SMS Messaging Channel** of the [Messaging Workspace](messaging-workspace.md), which also adds the **provider dropdown**. A new address type is registered with `AddOmnichannelAddressType`.

To capture capability settings, add a `DisplayDriver<OmnichannelChannelEndpoint>` that returns a shape for addresses of your type and marks the shape's root element with `data-address-capability="<capability>"`, so the editor shows it only while that capability is ticked. Runtime code checks `endpoint.HasCapability("SMS")`; lookups by number (`IOmnichannelChannelEndpointManager.GetByServiceAddressAsync(channel, address)`) only return an address that has the capability for that channel.

Phone numbers are canonicalized to E.164 and validated by the address handler itself, and a value can be listed only once per address type. For any other address type, register an `IChannelEndpointAddressPolicy` (in `CrestApps.OrchardCore.Omnichannel.Core`) to say how its addresses are normalized and validated, so the stored value matches inbound traffic. The [Messaging Workspace](messaging-workspace.md) registers one that covers every messaging channel.

Addresses saved before capabilities existed carried a single `Channel`. The upgrade gives each one its address type and capability, and merges records that listed the same number once per channel into one address. The merged-away identifiers are kept on the address (`MergedItemIds`), so activities and history that name them still find it, and recipes exported before the change import into one address per number.

### Contact
A **Contact** is any content item that has `OmnichannelContactPart` attached.

This lets you model customers/leads however you want (name, phone, email, account fields, custom fields, etc.). A single contact content type is usually enough; create more than one only to manage different kinds of contacts separately (for example, `Customer` versus `Employee`). Setting up the type in the admin is described in [Contacts](../user-manual/contacts.md#set-up-the-contact-type-once).

When a content type includes `OmnichannelContactPart`, the module enforces two code-controlled omnichannel surfaces:

- `OmnichannelContactPart` stores the contact-level communication compliance flags (`DoNotCall`, `DoNotSms`, `DoNotEmail`) and their UTC timestamps.
- A fixed `ContactMethods` bag part is added automatically and reserved for `ContactMethod` stereotype items so imports, exports, indexing, and activity-batch loading always read phone numbers and email addresses from a known location.

Do not rename or replace the `ContactMethods` bag in custom definitions. Instead, add or extend content types with the `ContactMethod` stereotype (such as `EmailAddress` and `PhoneNumber`) so they can be stored there consistently.

The management feature depends on `OrchardCore.Flows` so the enforced `ContactMethods` bag renders with the standard Orchard bag editor when you edit a contact content item. The bag is injected during Orchard's content-type definition build pipeline, so content types that attach `OmnichannelContactPart` always materialize with the named `ContactMethods` bag even when the stored type definition does not yet include it.

If you use the built-in `PhoneNumberInfoPart`, the `Number` field is a `PhoneField` (from `CrestApps.OrchardCore.ContentFields`) that stores the phone number in E.164 format alongside the ISO country code, so the correct country flag is always displayed when the field is edited again.

`OmnichannelContactPart` has these part settings (`OmnichannelContactPartSettings`), edited in the content-type editor:

| Setting | Default | Effect |
| --- | --- | --- |
| **Auto detect time zone** (`AutoDetectTimeZone`) | On | Detects the contact's time zone from their phone number when one was not selected. |
| **Require time zone** (`RequireTimeZone`) | Off | Forces editors to choose a time zone before saving. Enforced only when auto detect is off. |
| **Use Do not call** (`UseDoNotCall`) | On | Shows the Do not call preference in the contact editor. |
| **Use Do not SMS** / **Use Do not email** (`UseDoNotSms`, `UseDoNotEmail`) | Off | Shows those preferences in the contact editor. |

#### Import and export contact methods

Omnichannel contact imports and exports integrate with **Content Transfer**. The import options a user sees (duplicate handling, lead country, do-not-call registry scrubbing) are described in [Import and export contacts](../user-manual/contacts.md#import-and-export-contacts).

- exports write the first available contact-method entries to `Email`, `Cell Phone`, and `Phone` workbook columns
- exports also write `DoNotCall`, `DoNotCallUtc`, `DoNotSms`, `DoNotSmsUtc`, `DoNotEmail`, and `DoNotEmailUtc`
- imports can recreate those values as contact-method content items inside the `ContactMethods` bag
- imports and exports include `TimeZoneId`, and imports can infer that IANA time zone from the normalized phone number when the file does not provide one explicitly
- imports can populate the same DNC/compliance columns directly onto `OmnichannelContactPart`
- duplicate filtering can ignore rows that repeat a previously imported phone number, while still allowing updates when the imported row already targets the owning `ContentItemId`
- when a row targets an existing `ContentItemId`, the imported column values overwrite the mapped omnichannel fields on the new latest version of that content item
- do-not-call filtering can skip rows whose phone numbers are registered on one or more configured registries
- imports can normalize national-format phone numbers to E.164 by using the selected lead country before duplicate checks, before DNC registry lookups run, and before contact-method storage runs
- channel endpoints normalize valid phone numbers to Orchard Core's international `+<country code><number>` format before saving, so SMS and phone campaigns compare the same canonical value
- contact publish and update operations keep the omnichannel contact indexes in sync automatically

Use **Settings** -> **Content Import** to enforce DNC checks globally for imports, and use **Settings** -> **DNC Registries** to configure provider access for registries such as **USA FTC Registry** and **Canada LNNTE-DNCL Registry**. See [DNC Registry](../modules/dnc-registry.md) for setup details, credential requirements, and extension guidance.

The **Lead country** picker is required, so phone normalization always has region context. It mirrors the Local DNC country list and shows each option as `Country (+calling code)`. Files for content types with `OmnichannelContactPart` should contain leads from one country per file unless every phone number is already expressed in E.164.

#### Export contacts with their last activity

When a contact content type is exported through **Content** -> **Export**, the form shows a **CRM last activity** section that enriches each exported contact with their most recent **completed** activity of a chosen subject. The options are described in [Export contacts with their last activity](../user-manual/contacts.md#export-contacts-with-their-last-activity).

With the option enabled, the export appends these export-only columns to each contact row:

- **`LastActivityNote`** — the agent note recorded on the activity.
- **`LastActivityCompletedUtc`** — the completion date and time, in UTC.
- **`LastActivityCompletedBy`** — the full name of the user who completed the activity (resolved through the display-name provider).
- **`LastActivityDisposition`** — the disposition selected when the activity was completed.
- **`LastActivitySubject`** — the subject's title (falls back to the subject content type's title).
- **One column per subject field** — every exportable field of the selected subject content type, taken from that activity's subject, keeping the field's own column name (type-specific fields are already prefixed with the subject content type name; the generic content-item metadata columns are omitted). If a subject field's name would collide with a contact column, it is prefixed with `Subject` to stay unique.

Choosing this option always runs the export through the background queue (so progress is tracked and larger sets are handled reliably), and you download the finished file from the **Bulk Export** list. The activities are loaded in the same batches the contacts page in, so enriching a large contact list stays efficient. This feature builds on Content Transfer's export-option extension point; see [Contributing export options](../modules/content-transfer.md#contributing-export-options) to add options of your own.

### Subject ("the nature of the interaction")
A **Subject** is any content type that has `OmnichannelSubjectPart` attached.

Subjects are used to describe the nature of the interaction and to define the data you want agents (human or AI) to capture during the interaction. You can add any fields, parts, or custom data to the subject. Creating a subject in the admin is described in [Subjects](../user-manual/subjects.md).

The `OmnichannelSubject` stereotype is not recognized as a subject marker. A subject content type that still carries it must remove it and attach `OmnichannelSubjectPart`.

Because subject content items are authored and completed through the omnichannel subject flow rather than the standard content workflow, the default content editor action buttons Orchard Core injects (**Publish**, **Save Draft**, and **Preview**) are automatically hidden on the editor of any content type that has `OmnichannelSubjectPart` attached. This applies as soon as the part is attached and is reverted automatically when the part is detached, without any placement configuration. On the `OmnichannelSubjectPart` settings screen, the Azure AI Search and Elasticsearch index settings that Orchard Core injects into every part editor are hidden too, because indexing for omnichannel subjects is managed automatically.

### Disposition
A **Disposition** is the outcome of an activity (e.g. `Completed`, `FollowUp`, `DoNotCall`, `Scheduled`, `Sold`).

Dispositions are a key building block for controlling what happens next via subject actions. Disposition names are unique and become fixed after creation so subject-flow mappings stay stable. A disposition's **Outcome** (`DispositionOutcome`: `None`, `NotInService`, `NoAnswer`, `Busy`, `AnsweringMachine`) marks it for automatic use when a call ends in a way nobody chose; see [Dispositions](../user-manual/dispositions.md#outcomes).

### Campaign
A **Campaign** is used for **reporting, grouping, and business outcome tracking**.

Campaigns do not define the interaction type, channel, channel endpoint, or disposition-driven flow logic. Those settings live on the subject flow and the activity load, so different subjects inside the same campaign can behave differently. Campaigns cannot be deleted once created.

Campaign groups let reporting users combine multiple related campaigns without changing activity execution. Activities continue to store the campaign identifier, and reports resolve the campaign's current group when they run. Moving a campaign to another group therefore changes the group used for historical aggregation.

### Subject Flow
A **Subject Flow** defines how a content type with `OmnichannelSubjectPart` behaves. The stable configuration of a subject lives in the **content-type part settings** of `OmnichannelSubjectPart`, edited from the standard Orchard Core content type editor (the same place you attach the part), following the pattern used by parts such as `TitlePart`. There is no separate configure screen; volatile per-run values (campaign, channel, channel endpoint, and interaction type) are chosen when an activity batch is loaded. The **Interaction Center > Management > Subject Flows** list is a read-only overview with shortcuts (**Edit Content Type**, **Edit Settings**, **Manage Flow**); see [Subjects](../user-manual/subjects.md) and [Subject flows](../user-manual/subject-flows.md).

The base part settings store:

- the direction (`Outbound` or `Inbound`), defaulting to `Outbound` for new subjects
- the interaction type (`Manual` or `Automated`) — only shown for inbound subjects
- the communication channel — only shown for inbound subjects
- the channel endpoint used for automated inbound work
- the default campaign association used for reporting and grouping
- whether a disposition is required to complete an activity for the subject

For outbound subjects the interaction type and channel are resolved at load time, so those fields are hidden in the editor to keep the configuration focused. The disposition-driven **subject actions** are still managed separately from the **Manage Flow** screen.

**Default campaign** is not a per-run value. It is applied directly to activities that are created outside an activity batch — manually created activities, inbound activities, and activities moved to this subject by the **Change Subject** bulk action — and it is the fallback an activity batch uses when it does not choose its own campaign. Campaigns remain grouping and reporting metadata only.

**Require a disposition** is enabled by default because the disposition is what triggers the subject flow actions such as retrying, creating a follow-up activity, or updating communication preferences. Clear it only for fire-and-forget notification subjects, such as a one-way SMS alert, where the contact never responds and there is no outcome to record.

When the AI feature is enabled, a second part-settings editor adds an **AI configuration** card with AI-specific settings for:

- the chat AI profile, filtered to profiles with **Start the conversation automatically** enabled
- the subject goal
- AI update permissions for the contact and subject
- phone automation defaults for speech-to-text deployment, text-to-speech deployment, and voice
- SMS automation controls such as no-response timeout, response delay, and opt-out keywords

The editor progressively discloses these fields so only the relevant ones are visible:

| Subject configuration | AI settings shown |
|-----------------------|-------------------|
| Outbound | The **AI configuration** card is hidden, because outbound AI configuration is part of the activity-load process and is controlled by the **Automatic** source rather than the subject. The **Live agent handoff** card is shown. |
| Inbound + Manual | None — both the AI configuration card and the Live agent handoff card are hidden because an inbound manual subject is always handled by an agent |
| Inbound + Automated + Phone | AI profile, subject goal, AI permissions, voice call automation, and the Live agent handoff card |
| Inbound + Automated + SMS | AI profile, subject goal, AI permissions, SMS automation, and the Live agent handoff card |

The **AI configuration** card's visibility is applied when the editor loads and updated live as you change the direction, interaction type, or channel. The **Live agent handoff** card is not toggled live: it reflects the saved configuration, so save the content type after changing the direction or interaction type to show or hide it. Hidden fields keep their stored values, so switching direction back and forth never discards configuration. Leaving a speech selection empty uses the global AI site setting when the automated conversation starts.

The **Live agent handoff** card holds **Allow the AI to hand off to a live agent**, the **Handoff queue** that receives escalated conversations (handoff only happens when it is set), and the **Escalate when** conditions: **The customer asks for a human**, **The customer is a qualified, ready lead**, and **The customer is frustrated or the AI cannot help**. Select at least one condition, or handoff is never triggered.

Activity batches carry only the AI profile per run for outbound automated work loaded through the **Automatic** source; the profile selector appears in the **Activity load settings** card directly under the campaign. Speech-to-text, text-to-speech, and voice fall back to the subject flow and then the global AI site settings.

Any content type with `OmnichannelSubjectPart` is a valid subject. The per-run campaign, channel, and channel endpoint used by each activity are chosen when an activity batch is loaded, so a subject does not need every field set on its part settings before it can be used.

### Subject Action
A **Subject Action** links a disposition to an action type and defines what happens when an activity is completed with that disposition for a given subject type. Actions are managed from **Manage Flow** on the Subject Flows list; see [Subject flows](../user-manual/subject-flows.md) for the screens and a worked example.

Each subject can have multiple actions per disposition, and each action has its own parameters. Subjects without any actions show a **Missing flow** badge in the Subject Flows list.

**Available action types:**

| Type | Description |
|------|-------------|
| **Finish** | Completes the task. No additional actions are taken. |
| **Try Again** | Creates a retry activity with the same details and an incremented attempt count. Configurable parameters include max attempts, urgency level, owner assignment, and default schedule hours. |
| **New Activity** | Creates a brand new activity, optionally targeting a different subject type. The new activity resolves its campaign, interaction type, and channel settings from the target subject flow. Configurable parameters include urgency level, owner assignment, and default schedule hours. |

Action types are registered entries; the **Omnichannel CRM** feature adds **Convert Lead** (see [CRM](crm.md)).

Every action also has **When to choose this disposition**: guidance given to the AI when it dispositions an automated call or message, so it can tell this outcome from the others. It starts as the disposition's own description; edit it to say what the disposition means for this subject.

Actions that create follow-up activities expose an **Assignment type**:

- **Same owner** assigns the follow-up activity to the user who completes the current activity.
- **Specific owner** displays a required user selector and assigns the follow-up activity to that selected user.

**Communication preferences:** Every action type can optionally update the contact's communication preferences when executed. Tick **Show communication preferences** to reveal **Set do not call**, **Set do not SMS**, and **Set do not email**.

### Activity
An **Activity** is a task to be completed for a contact.

- **Manual activity**: A user completes the activity in the UI, adds notes, and selects a disposition.
- **Automated activity**: An AI agent completes the activity through the configured channel.

When an activity is completed, the user selects a disposition and is shown a preview of the subject actions that will execute. Actions that create follow-up activities allow the user to adjust the schedule date and, optionally, enter **Preparation notes** for each result. A preparation note becomes the follow-up activity's instructions, giving the next agent context before they start the work.

Editing an already completed activity does **not** re-run workflow logic. Administrators can correct the saved disposition or notes without creating retry or follow-up activities.

On a contact's **Activities** page, **Add Activity > Outbound** creates a *scheduled* activity for an **outbound** subject, and **Add Activity > Inbound** logs a *completed* activity for an **inbound** subject: it is stored as completed by the current user and the subject flow runs immediately. Each selector lists only subjects of its direction, auto-selects the subject when exactly one exists, and blocks the screen with a warning when none is configured. The agent-side steps, with screencasts, are in [Working activities](../user-manual/activities.md).

When an automated AI conversation completes, the activity stores the AI session identifier, appends the generated call summary as disposition notes, and applies the AI-selected disposition through the same subject-action lifecycle used by agents. Authorized administrators can open **Review AI conversation** from the activity actions to inspect the full transcript.

An automated voice call whose live (speech-to-speech) session is lost partway through is not concluded as the model reads the cut-off transcript. The platform first opens a new session that is given the conversation so far, up to two times. When that does not work, the caller is handed to a live agent if the subject allows handoff. Otherwise the caller hears a short apology and the call ends. The activity then takes the disposition the subject's **Try again** action is wired to, so the contact is called again, and its notes say the conversation was cut short. A subject with no **Try again** action keeps the reviewed disposition. The activity's terminal reason is `ai_session_lost`.

### Load Activities
A **Load Activities** definition (an activity batch) stores filters to find contacts and then **loads activities in the background**.

The loader runs as a background process to avoid overloading the system and to allow large loads to run safely. A load does not start on save; **Actions > Load batch** starts it. The **Load Activities** list is ordered by creation date with the newest activity loads first, is paged, and supports the standard admin bulk-selection controls. The form, the three sources (**Manual**, **Automatic**, **Dialer**) and the load report are described in [Load activities](../user-manual/load-inventory.md); automatic AI loads in [Automated AI SMS and voice](../user-manual/automated-ai.md).

Each created activity resolves its campaign, channel, channel endpoint, and interaction type from the batch selections, falling back to the subject's part settings. The interaction type is derived from the source: the **Automatic** source creates **Automated** activities, while other sources create **Manual** activities. Manual activity loads assign each created activity to a selected user. Dialer activity loads use the phone channel, leave activities unassigned with assignment status `Available`, and apply the selected dialer profile so the created activities inherit the profile's dialing mode before dialers reserve them later. The campaign on a dialer-loaded activity comes from the load, falling back to the subject flow's default campaign; the dialer profile never sets it.

Dialer profile selection is an optional integration supplied through the Omnichannel-owned `IActivityDialerContributor` contract. Omnichannel Management remains independently activatable when Contact Center Outbound Dialer is disabled; in that configuration, dialer profile choices are unavailable and non-dialer activity loading continues to work normally.

The **Automatic** source dispatches work through a channel processor (such as SMS, from the **SMS Omnichannel Automation** feature, see [SMS](sms.md)) and drives each conversation with an AI profile. Its **AI profile** selector lists only **Chat** profiles that have **Start the conversation automatically** enabled, because the opening message is what starts the automated conversation; a load without a profile falls back to the subject's profile, and one of the two is required. The **Address** list offers only addresses with the capability of the chosen channel. The `AutomatedActivitiesProcessorBackgroundTask` picks up due automated activities every five minutes.

## Getting started

Enable the features in **Tools > Features**:

- `Omnichannel` (`CrestApps.OrchardCore.Omnichannel`)
- `Omnichannel Management` (`CrestApps.OrchardCore.Omnichannel.Managements`), which also enables `Omnichannel Activities` and `Omnichannel Channel Endpoints`
- (Optional) `SMS Omnichannel Automation` (`CrestApps.OrchardCore.Omnichannel.Sms`) for AI SMS automation, see [SMS](sms.md)
- (Optional) `Omnichannel CRM` (`CrestApps.OrchardCore.Omnichannel.Crm`) for leads, accounts and opportunities, see [CRM](crm.md)

Then configure the CRM in this order. Each step is a browser task documented in the User Manual:

1. Create the contact content type and contacts: [Contacts](../user-manual/contacts.md).
2. Create subject content types: [Subjects](../user-manual/subjects.md).
3. Create dispositions: [Dispositions](../user-manual/dispositions.md).
4. Create campaign groups and campaigns: [Campaigns](../user-manual/campaigns.md).
5. Configure each subject's settings and its flow (**Manage Flow**): [Subjects](../user-manual/subjects.md#subject-settings) and [Subject flows](../user-manual/subject-flows.md).
6. Add the numbers you send from and call from: [Omnichannel addresses](../user-manual/channel-endpoints.md).
7. Create and load activities: [Load activities](../user-manual/load-inventory.md).
8. Work and complete activities: [Working activities](../user-manual/activities.md).

To provision a tenant from another environment instead of clicking through these screens, use the deployment steps in [Exporting and importing configuration](#exporting-and-importing-configuration).

## Extending activity load sources

Activity loading is extensible. Each activity load has a **source**, and the source controls how it resolves and loads activities. There are two layers of extensibility:

1. **Registering a source** — register sources through `ActivityBatchSourceOptions` in a feature `Startup`. Each `ActivityBatchSourceEntry` provides the display name, description, whether the source requires user assignment, and whether it should appear in the creation picker. Display drivers can add source-specific editor sections.

2. **Controlling the load** — implement `IActivityBatchLoader` (from `CrestApps.OrchardCore.Omnichannel.Core.Services`) to fully own how a source queries leads, applies filters, and creates activities. The loader's `Source` property must match the registered source. Register the loader as a scoped service:

   ```csharp
   services.AddScoped<IActivityBatchLoader, MyCustomActivityBatchLoader>();
   ```

When an activity load is started, the `IActivityBatchLoadCoordinator` transitions it to the loading state, resolves the loader whose `Source` matches the selected source, and delegates to it. Sources **without** a dedicated loader fall back to the built-in `DefaultContactActivityBatchLoader`, which pages over contacts of the activity load's contact content type, applies the standard lead filters (created range, phone number, time zone, last completed activity), and creates activities from the subject flow settings. The default loader is not sealed, so a custom loader can inherit from it to reuse the contact-paging pipeline while overriding individual stages. If a loader throws, the coordinator logs the error and returns the activity load to the `New` state so it can be retried.

## Scheduled activities list

**Interaction Center > Activities** (`Admin/omnichannel/activities`, permission `ListActivities`) lists the current user's own **Not started** manual activities, newest first, with filters for urgency, subject, channel, attempt, time zone and a scheduled date range. Rows show the contact's current local time when a time zone is stored. See [Working activities](../user-manual/activities.md#your-activity-list).

The **Purge** action (permission `PurgeActivity`, implied by `ManageActivities`) is irreversible: it changes the activity status to `Purged`, records the UTC purge time and the current user's identifier and username for auditing, and clears any reservation state while preserving assignment. Every activity in one bulk purge records the same purge time and actor.

## Contacts list

The **Interaction Center > Contacts** menu item opens the standard Orchard Core content list restricted to your Omnichannel contact content types. It links to the content `List` action and passes every content type that has `OmnichannelContactPart` attached as a comma-separated `contentTypeId`, so the resulting screen only shows contact items and only offers contact types when creating new content. With the **Omnichannel CRM** feature, lead types are listed on their own menu item and left out of this one.

The contact content types are read from the cached `OmnichannelContentTypeProvider` that tracks which types attach `OmnichannelContactPart`, so the menu stays in sync as you attach or detach the part without scanning every content definition on each request. The menu item is available to users with the **List content items** permission (`ListContent`).

## Phone number search

Phone filters in **Load Activities**, **Manage Activities**, and Content Admin search the primary **Cell** and **Home** contact methods. How users type searches, and the `phone:`, `phone-exact:`, `phone-starts:` and `phone-ends:` terms, are described in [Find a contact](../user-manual/contacts.md#find-a-contact) and [Load activities](../user-manual/load-inventory.md#phone-number-tips).

- Input that does not begin with `+` is reduced to digits and matched against the national number. Input whose trimmed value begins with `+` is matched against the E.164 value; the plus sign is a literal format indicator, not a wildcard.
- **Contains** is the default match mode (`PhoneNumberMatchType`). **Exact match**, **Begins with**, and **Ends with** are also available in Load Activities and Manage Activities.
- In Load Activities and Content Admin, **Exact match** looks for the number in every shape it may have been stored in. A national entry is also compared with the E.164 value: a ten-digit entry is read as a North American (`+1`) number, and a longer entry as one that already carries its country code. A number imported without a country, whose E.164 value is empty, is found by its stored digits with or without the leading `1`. Both screens share one definition of the phone match.
- Content Admin evaluates the displayed content version. Load Activities uses published or latest contact values according to **Only published records**, while Manage Activities uses the latest saved contact values.
- The shared contact index stores primary Cell and Home numbers as national digits for national searches, while the corresponding normalized values remain in E.164 format.

The named terms are registered by `OmnichannelContactPhoneContentsAdminListFilterProvider`, an `IContentsAdminListFilterProvider`. When the content list is scoped exclusively to contact content types — the **Interaction Center > Contacts** menu item, or any Content Admin URL whose `contentTypeId` lists only types that attach `OmnichannelContactPart` — the provider also overrides the default text term, so a plain entry matches the **Display Text** *or* a contact phone number (a contains match on the primary Cell and Home numbers). On any other content list the search box behaves exactly as the framework default and matches Display Text only. On those contact-scoped lists a **Phone** card also appears in the Content Admin **Filters** popover.

## Bulk Activity Management

The **Manage Activities** page provides a centralized interface for managing active omnichannel inventory across manual, automated, and dialer-oriented activities. It targets editable work states such as **Not started**, `Scheduled`, `Pending`, `AwaitingAgentResponse`, `Failed`, and `Cancelled`. Historical activities without a subject content type remain manageable and are represented by the generic **Activity** type instead of failing the page or completion action. The filters, page size and bulk actions are described in [Managing activities in bulk](../user-manual/bulk-activities.md).

### Accessing the page

**Interaction Center > Management > Manage Activities**, available to users with the **Manage activities** permission (`ManageActivities`).

Route: `Admin/omnichannel/manage-activities`

### Filters

The filter panel groups fields into **Contact filters** and **Activity filters**. Its expanded or collapsed state is stored in the browser's local storage. The assigned-user filter searches across all users instead of only agent-role users. For the attempt filter, values `0` and `1` both mean no attempt, and `2` means the second attempt.

#### Source and channel options

The **Source** and **Channel** lists of Manage Activities, the contact activity list, and the Omnichannel and Contact Center report filters are built from two option registries, so they only offer values an activity can actually carry. A feature that writes a source or channel onto activities registers it with `services.Configure<ActivitySourceOptions>(...)` or `services.Configure<ActivityChannelOptions>(...)`.

| Source | Matches the stored values | Registered by |
| --- | --- | --- |
| **Manual** | `Manual` | Omnichannel Activities |
| **Automatic** | `Automatic` | Omnichannel Activities |
| **Inbound** | `Inbound` | Omnichannel Activities (agent-logged inbound activities; Inbound Voice calls store the same value) |
| **Dialer** | `Dialer`, `PreviewDial`, and with Paced Dialing also `PowerDial`, `ProgressiveDial`, `PredictiveDial` | Contact Center Outbound Dialer, extended by Contact Center Paced Dialing |
| **Callback** | `Callback` | Contact Center Outbound Dialer |

One option can match several stored values: a dialer load writes the dialer profile's mode onto each activity, so choosing **Dialer** filters on all of the modes at once. The `Workflow` and `Api` constants remain readable on older activities but are not offered, because nothing creates them. A selected value that is not registered, for example from a saved report filter, stays in the list with its raw value as the label and matches only itself.

Channels: **Phone** and **SMS** are registered by Omnichannel Activities, because subject flows and activity loads offer both whenever activities are enabled. Contact Center report channel filters always offer **Voice** (interactions are only ever voice calls) and add **SMS** only on reports that also count CRM activities.

The **Change Source** bulk action only offers, and the server only accepts, sources registered with `CanBeSetManually` (**Manual** and **Automatic**). Dialer sources are set through **Change Dialer Profile**.

### Bulk Actions

The bulk actions apply either to the activities selected on the current page or to **all matching activities** returned by the current filter. Their behavior, as seen by a manager, is in [Apply a bulk action](../user-manual/bulk-activities.md#apply-a-bulk-action). Implementation notes:

- **Assign** distributes the activities round-robin among the selected users.
- **Reschedule** takes a date only; activities are scheduled for midnight of that date in the site's time zone.
- **Purge** requires `PurgeActivity` (implied by `ManageActivities`) and sets the status to `Purged`.
- **Change Dialer Profile** is shown when the Contact Center dialer feature is available. It sets the activity's dialer source to match the selected profile; the activity keeps its own campaign, is switched to the **Manual** interaction type, and has any AI session cleared. It can also clear assignment and reservation state so the dialer can pick the activity up again.

## Reports

When the **Reports** feature (`CrestApps.OrchardCore.Reports`) is enabled, Omnichannel Management contributes 25 CRM reports to the admin **Reports** area, grouped by category: **Operations**, **Queue & Routing**, **Agent Performance**, **CRM & Campaigns**, **Compliance & Audit**, and **Technical & IT**. They include activity summary, campaign performance, disposition breakdown, handoff containment, backlog and aging, source, channel and channel-endpoint performance, per-user productivity and completion time, and campaign source, channel, disposition, and attempt mixes.

Every Omnichannel report can be narrowed by **Campaign group**, **Campaign**, **Channel**, **Source**, and **Status**. Reports export to CSV, and to Excel (`.xlsx`) when the **Reports (OpenXml)** feature is enabled. Viewing them requires the **View Omnichannel reports** permission, which **Manage activities** implies. See [Reports](../user-manual/reports.md) in the user manual.

## Permissions

The headless **Omnichannel Activities** feature registers these permissions (`PermissionProvider`). The *Administrator* role gets all of them; the *Agent* role gets the four marked *Agent* below. The **Omnichannel CRM** feature adds its own; see [CRM](crm.md).

| Key | Name shown in the role editor | Notes |
| --- | --- | --- |
| `ListActivities` | List activities | Agent. Opens **Interaction Center > Activities**. |
| `ListContactActivities` | List Contact activities | Agent. Opens a contact's **Activities** page. Implied by `ListActivities`. |
| `CompleteActivity` | Complete activity | Completes any activity. |
| `CompleteOwnActivity` | Complete own activity | Agent. Completes activities assigned to the user. |
| `EditActivity` | Create and edit activities | Agent. Gates the contact's **Add Activity** screens (outbound and inbound) and editing an activity. Implied by `ManageActivities`. |
| `ManageActivities` | Manage activities | **Manage Activities** and **Numbers Not In Service**. |
| `PurgeActivity` | Purge activity | Implied by `ManageActivities`. |
| `ManageDispositions` | Manage dispositions | |
| `ManageCampaigns` | Manage campaigns | |
| `ManageCampaignGroups` | Manage campaign groups | |
| `ManageCadences` | Manage cadences | |
| `ManageChannelEndpoints` | Manage omnichannel addresses | |
| `ManageActivityBatches` | Manage activity batches | **Load Activities**. |
| `DeleteLoadedActivityBatches` | Delete loaded activity batches | Deletes a load whose status is *Loaded*; the activities it created are kept. |
| `ManageSubjectFlows` | Manage subject flows | |
| `ViewOmnichannelReports` | View Omnichannel reports | Implied by `ManageActivities`. |

## Exporting and importing configuration

Omnichannel configuration moves between environments through Orchard Core's standard deployment and recipe pipelines, so a tenant can be provisioned from staging to production without re-entering settings by hand.

Each configurable entity has its own deployment step and a matching recipe step:

| Entity | Deployment step (category **Omnichannel**) | Recipe step name |
|--------|--------------------------------------------|------------------|
| Dispositions | Omnichannel Dispositions | `OmnichannelDisposition` |
| Channel endpoints | Omnichannel Channel Endpoints | `OmnichannelChannelEndpoint` |
| Campaign groups | Omnichannel Campaign Groups | `OmnichannelCampaignGroup` |
| Campaigns | Omnichannel Campaigns | `OmnichannelCampaign` |
| Re-engagement cadences | Omnichannel Cadences | `OmnichannelCadence` |
| Subject actions | Omnichannel Subject Actions | `OmnichannelSubjectAction` |

To export, open **Configuration -> Import/Export -> Deployment Plans**, add the Omnichannel steps you need, and execute or download the plan. Each step exports every entry of its type.

On import, entries are matched by their identifier: an entry that already exists is updated in place, and a new entry is created with its original identifier preserved. Because identifiers are preserved, cross-references (for example a campaign that points at a campaign group, or a subject action that points at a disposition) keep working after the import.

When a plan carries several of these steps, order them so that referenced entities import first: dispositions, channel endpoints and cadences, then campaign groups, then campaigns, and finally subject actions.

Subject flow configuration is stored on the `OmnichannelSubjectPart` content-type part settings, so it travels with the content type definition through the standard **Content Definition** deployment step rather than a dedicated omnichannel step.

## Data at rest and privacy

The omnichannel/CRM layer stores customer communication content and contact addresses as **plaintext** in the tenant SQL database. `OmnichannelMessage.Content` (the message body), `OmnichannelMessage.CustomerAddress`, and `OmnichannelMessage.ServiceAddress` are persisted unencrypted in the YesSql document, and the two addresses are additionally projected — still in plaintext — into the `OmnichannelMessageIndex` table so they can be queried. No application-level encryption is applied to this data.

This is a deliberate contrast with telephony **recording media**, which the media-execution layer encrypts at rest through the data protection provider. That asymmetry matters operationally: encrypting the recording bytes does not encrypt the message bodies or the phone numbers/addresses that the CRM stores alongside them. Protecting this content at rest is therefore a **deployment responsibility** — enable database- or disk-level encryption (for example, transparent data encryption) and restrict access to the database and its backups accordingly. Treat message content and contact addresses as personal data.

There is currently **no automated per-contact subject erasure** (right-to-be-forgotten) across the CRM. The activity **Purge** action marks an activity as `Purged` and removes it from the work queue, but it does **not** delete the underlying message content, the customer/service addresses, or the contact record — that data remains in the database and its index. Comprehensive per-contact erasure across omnichannel activities, messages, and contacts is a known limitation and a general-availability blocker; until it ships, satisfy erasure requests through direct, audited database operations against the tenant store.
