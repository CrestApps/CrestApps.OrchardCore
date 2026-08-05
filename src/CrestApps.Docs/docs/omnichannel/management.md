---
sidebar_label: "Management (CRM)"
sidebar_position: 2
title: CrestApps Omnichannel Management (CRM)
description: Customer Relationship Management (CRM) tools for contacts, subject flows, campaigns, and activity-driven work across communication channels.
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

It provides the admin tools you need to manage **contacts**, define **subject-level flows**, group work under **campaigns**, and run activity-driven processes (manual or automated) across channels such as SMS, email, and phone.

## Core concepts

### Contact
A **Contact** is any content item that has `OmnichannelContactPart` attached.

This lets you model customers/leads however you want (name, phone, email, account fields, custom fields, etc.).

### Subject ("the nature of the interaction")
A **Subject** is any content type that has `OmnichannelSubjectPart` attached.

Subjects are used to describe the nature of the interaction and to define the data you want agents (human or AI) to capture during the interaction. You can add any fields, parts, or custom data to the subject.

### Disposition
A **Disposition** is the outcome of an activity (e.g. `Completed`, `FollowUp`, `DoNotCall`, `Scheduled`, `Sold`).

Dispositions are a key building block for controlling what happens next via subject actions. Disposition names are unique and become fixed after creation so subject-flow mappings stay stable.

### Campaign
A **Campaign** is now used primarily for **reporting, grouping, and business outcome tracking**.

Campaigns no longer define the interaction type, channel, channel endpoint, or disposition-driven flow logic. Those settings now live on the subject flow so different subjects inside the same campaign can behave differently.

### Subject Flow
A **Subject Flow** defines how a content type with `OmnichannelSubjectPart` behaves. The stable configuration of a subject now lives in the **content-type part settings** of `OmnichannelSubjectPart`, edited from the standard Orchard Core content type editor (the same place you attach the part), following the pattern used by parts such as `TitlePart`. There is no separate configure screen; volatile per-run values (campaign, channel, channel endpoint, and interaction type) are chosen when an activity batch is loaded.

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

- the chat AI profile, filtered to profiles with **Add initial prompt** enabled
- the subject goal
- AI update permissions for the contact and subject
- phone automation defaults for speech-to-text deployment, text-to-speech deployment, and voice
- SMS automation controls such as no-response timeout, response delay, and opt-out keywords

The editor progressively discloses these fields so only the relevant ones are visible:

| Subject configuration | AI settings shown |
|-----------------------|-------------------|
| Outbound | None — the AI configuration card is hidden. Outbound AI configuration is part of the inventory-load process and is controlled by the **Automatic** source rather than the subject |
| Inbound + Manual | None — the whole AI card is hidden because an inbound manual subject is always handled by an agent |
| Inbound + Automated + Phone | AI profile, subject goal, AI permissions, and voice call automation |
| Inbound + Automated + SMS | AI profile, subject goal, AI permissions, and SMS automation |

The visibility is applied when the editor loads and updated live as you change the direction, interaction type, or channel. Hidden fields keep their stored values, so switching direction back and forth never discards configuration.

Activity batches carry only the AI profile per run for outbound automated work loaded through the **Automatic** source; the profile selector appears in the **Inventory load settings** card directly under the campaign. Speech-to-text, text-to-speech, and voice fall back to the subject flow and then the global AI site settings.

### Subject Action
A **Subject Action** links a disposition to an action type and defines what happens when an activity is completed with that disposition for a given subject type.

Each subject can have multiple actions per disposition, and each action has its own parameters.

**Available action types:**

| Type | Description |
|------|-------------|
| **Finish** | Completes the task. No additional actions are taken. |
| **Try Again** | Creates a retry activity with the same details and an incremented attempt count. Configurable parameters include max attempts, urgency level, owner assignment, and default schedule hours. |
| **New Activity** | Creates a brand new activity, optionally targeting a different subject type. The new activity resolves its campaign, interaction type, and channel settings from the target subject flow and supports configurable owner assignment. |

Actions that create follow-up activities expose an **Assignment type**:

- **Same owner** assigns the follow-up activity to the user who completes the current activity.
- **Specific owner** displays a required user selector and assigns the follow-up activity to that selected user.

**Communication preferences:** Every action type can optionally set Do-Not-Call, Do-Not-SMS, Do-Not-Email, and Do-Not-Chat flags on the contact when executed.

### Activity
An **Activity** is a task to be completed for a contact.

- **Manual activity**: A user completes the activity in the UI, adds notes, and selects a disposition.
- **Automated activity**: An AI agent completes the activity through the configured channel.

When an activity is completed, the user selects a disposition and is shown a preview of the subject actions that will execute. Actions that create follow-up activities allow the user to adjust the schedule date and, optionally, enter **Preparation notes** for each result. A preparation note becomes the follow-up activity's instructions, giving the next agent context before they start the work.

Editing an already completed activity does **not** re-run workflow logic. Administrators can correct the saved disposition or notes without creating retry or follow-up activities.

#### Logging activities from a contact

On a contact's **Activities** page, the **Add Activity** button is a dropdown with two options that map to the subject direction:

**Outbound** creates a *scheduled* activity, exactly like the previous **New Activity** behavior:

- The subject selector lists **outbound** subjects only. When exactly one outbound subject is configured it is auto-selected on page load.
- You set the activity owner, scheduled date, urgency, instructions, and any subject fields, then save the scheduled activity.
- If no outbound subject is configured, a warning is shown and scheduling is blocked.

**Inbound** logs a *completed* activity for work the contact initiated. The screen mirrors the complete-activity experience:

- The subject selector lists **inbound** subjects only. When exactly one inbound subject is configured it is auto-selected on page load, and changing the selector reloads the screen so the correct subject fields, dispositions, and workflow appear.
- The subject's editable fields are rendered as an edit view directly under the selector, so there is no separate subject-details card - the same fields the agent would fill while completing a scheduled activity.
- You review the contact information, fill any subject fields, add notes, and select a disposition. The workflow-results preview shows the follow-up actions the disposition will run.
- The activity is stored as **completed by the current user**, and the subject flow runs immediately, so it may create a follow-up activity depending on the inbound subject's actions. The logged activity then appears in the contact's completed activities list.
- If no inbound subject is configured, a warning is shown and inbound logging is blocked.

The screencast below walks through a complete inbound scenario for a call center. It first creates an `Inbound` subject content type and configures its flow with the **Inbound** direction on the `Phone` channel (a basic flow that does not require a disposition so agents can log a call quickly). Then it simulates a customer calling in: the agent searches Content Items with `phone:7025556666`, finds the matching contact, opens the contact's **Activities** page, chooses **Inbound** from the **Add Activity** menu, selects the `Inbound` subject, adds a note about the call, and logs the completed activity.

<video controls preload="metadata" width="100%" aria-label="Screen cast of creating an inbound subject and logging an inbound call for an existing contact">
  <source src="/img/docs/omni-inbound-existing.mp4" type="video/mp4" />
</video>

Sometimes an inbound caller is not in the system yet. The screencast below shows that variation: the agent searches Content Items with `phone:7025559999`, gets no match, creates a new `Contact` content item for the caller (capturing their name, time zone, and phone number), and then logs the inbound call under the new contact.

<video controls preload="metadata" width="100%" aria-label="Screen cast of creating a new contact for an unknown inbound caller and logging the call">
  <source src="/img/docs/omni-inbound-new-contact.mp4" type="video/mp4" />
</video>

### Load Inventory
A **Load Inventory** definition stores filters to find contacts and then **loads activities in the background**.

The loader runs as a background process to avoid overloading the system and to allow loading large inventory sets safely.

The **Load Inventory** list is ordered by creation date with the newest inventory loads first, so a load you just created appears at the top. The list is paged and supports the standard admin bulk-selection controls (the header checkbox selects every row on the page).

Dialer profile selection is an optional integration supplied through the Omnichannel-owned `IActivityDialerContributor` contract. Omnichannel Management remains independently activatable when Contact Center Outbound Dialer is disabled; in that configuration, dialer profile choices are unavailable and non-dialer inventory management continues to work normally.

#### Loading Automated SMS Activities with an AI Profile

When you choose the **Automatic** source for an inventory load, the batch can dispatch work through a channel processor (such as SMS) and drive each conversation with an AI profile. The **AI profile** selector on the inventory-load form lists only **Chat** profiles that have **Add initial prompt** enabled, because the initial prompt is what starts the automated conversation.

To load automated SMS activities:

1. Enable the **SMS Omnichannel Automation** feature so the SMS channel processor is available.
2. Create an **AI profile** (type **Chat**) with **Add initial prompt** enabled and an initial prompt written for your outreach.
3. In **Interaction Center → Channel Endpoints**, add an **SMS** endpoint for the number you send from.
4. In **Load Inventory**, click **Add Inventory Load → Automatic**, then select the subject, the AI profile, the **SMS** channel, the SMS channel endpoint, and the contact type.
5. Save the load, then open its **Actions → Load batch** menu to generate the activities in the background.

The screencast below creates an automatic SMS inventory load for the *New Customer - Welcome* subject powered by the *SMS Outreach Assistant* profile, then loads the batch to generate the automated activities.

<video controls preload="metadata" width="100%" aria-label="Screencast of creating an automatic SMS inventory load driven by an AI profile and loading it to generate automated activities">
  <source src="/img/docs/omni-load-automated-sms.mp4" type="video/mp4" />
</video>

## Getting started (recommended order)

### 1) Enable required features

In Orchard Core Admin:

1. Go to `Tools` → `Features`.
2. Enable:
   - `Omnichannel`
   - `Omnichannel Management`
   - (Optional) `SMS Omnichannel Automation` if you want AI/SMS automation

### 2) Create your Contact content type

1. Go to `Content` → `Content Definition` → `Content Types`.
2. Create a new content type (e.g. `Contact`).
3. Attach `OmnichannelContactPart`.
4. Add any fields/parts you need (phone number, email, lead status, custom fields, etc.).
5. Create/import contact items.

Typically a single contact content type is all you need for the CRM record that represents your customer or contact. You can create more than one when you want to manage different kinds of contacts separately (for example, `Customer` versus `Employee`). The content type can carry any parts and fields your business needs, so model it around the data your agents capture.

The screencast below creates a `Contact` type, attaches `OmnichannelContactPart`, and then logs a new lead with a time zone and a cell phone number:

<video controls preload="metadata" width="100%" aria-label="Screen cast of creating a Contact content type and a contact item">
  <source src="/img/docs/omni-contact-type.mp4" type="video/mp4" />
</video>

If you use the built-in `PhoneNumberInfoPart`, the `Number` field is a `PhoneField` (from `CrestApps.OrchardCore.ContentFields`) that stores the phone number in E.164 format alongside the ISO country code, so the correct country flag is always displayed when the field is edited again.

When a content type includes `OmnichannelContactPart`, the module now enforces two code-controlled omnichannel surfaces:

- `OmnichannelContactPart` stores the contact-level communication compliance flags (`DoNotCall`, `DoNotSms`, `DoNotEmail`, `DoNotChat`) and their UTC timestamps.
- A fixed `ContactMethods` bag part is added automatically and reserved for `ContactMethod` stereotype items so imports, exports, indexing, and activity-batch loading always read phone numbers and email addresses from a known location.

Do not rename or replace the `ContactMethods` bag in custom definitions. Instead, add or extend content types with the `ContactMethod` stereotype (such as `EmailAddress` and `PhoneNumber`) so they can be stored there consistently.

The management feature depends on `OrchardCore.Flows` so the enforced `ContactMethods` bag renders with the standard Orchard bag editor when you edit a contact content item. The bag is injected during Orchard's content-type definition build pipeline, so content types that attach `OmnichannelContactPart` always materialize with the named `ContactMethods` bag even when the stored type definition does not yet include it.

`OmnichannelContactPart` also includes configurable part settings in the content-type editor:

- **Require time zone** is enabled by default and forces editors to choose a lead time zone before the contact can be saved.
- **Use Do not call** is enabled by default and controls whether the contact editor shows the Do not call preference.
- **Use Do not SMS**, **Use Do not chat**, and **Use Do not email** are disabled by default and can be enabled individually when that contact type should track those communication preferences.

#### Import and export contact methods

Omnichannel contact imports and exports integrate with **Content Transfer**.

- exports write the first available contact-method entries to `Email`, `Cell Phone`, and `Phone` workbook columns
- exports also write `DoNotCall`, `DoNotCallUtc`, `DoNotSms`, `DoNotSmsUtc`, `DoNotEmail`, `DoNotEmailUtc`, `DoNotChat`, and `DoNotChatUtc`
- imports can recreate those values as contact-method content items inside the `ContactMethods` bag
- imports and exports include `TimeZoneId`, and imports can infer that IANA time zone from the normalized phone number when the file does not provide one explicitly
- imports can populate the same DNC/compliance columns directly onto `OmnichannelContactPart`
- duplicate filtering can ignore rows that repeat a previously imported phone number, while still allowing updates when the imported row already targets the owning `ContentItemId`
- when a row targets an existing `ContentItemId`, the imported column values overwrite the mapped omnichannel fields on the new latest version of that content item
- do-not-call filtering can skip rows whose phone numbers are registered on one or more configured registries
- imports can normalize national-format phone numbers to E.164 by using the selected lead country before duplicate checks, before DNC registry lookups run, and before contact-method storage runs
- channel endpoints now normalize valid phone numbers to Orchard Core's international `+<country code><number>` format before saving, so SMS and phone campaigns compare the same canonical value
- contact publish and update operations now keep the omnichannel contact indexes in sync automatically
Use **Settings** -> **Content Import** to enforce DNC checks globally for imports, and use **Settings** -> **DNC Registries** to configure provider access for registries such as **USA FTC Registry** and **Canada LNNTE-DNCL Registry**. See [DNC Registry](../modules/dnc-registry) for setup details, credential requirements, and extension guidance.

When the import file is not already using E.164 phone numbers, select the default country represented by that file in the import UI. Files for content types with `OmnichannelContactPart` should contain leads from one country per file unless every phone number is already expressed in E.164. The picker mirrors the Local DNC country list, shows each option as `Country (+calling code)`, and is required before the import can start so phone normalization always has region context.

The screencast below shows both directions. It first exports the existing contacts as a CSV workbook through the **Export** panel (choose the CSV format and the `Contact` type, then **Export Data**), then imports a batch of new leads through **Content** -> **Import** -> **Contact**. During import it selects the lead country, enables **Ignore duplicate by phone number**, and turns on **Ignore numbers on national do-not-call registries** with the **Local Do Not Call Registry** selected, so any lead whose phone number is already on the DNC list is scrubbed automatically before contacts are created.

<video controls preload="metadata" width="100%" aria-label="Screen cast of exporting contacts and importing leads with DNC auto-scrub">
  <source src="/img/docs/omni-contact-import-export.mp4" type="video/mp4" />
</video>

### 3) Create your Subject content type

1. Go to `Content` → `Content Definition` → `Content Types`.
2. Create a new content type or edit an existing one.
3. Attach `OmnichannelSubjectPart` to mark the content type as an Omnichannel subject.
4. Add any fields or parts you want the agent to capture during the interaction.

A subject is just a content type marked with `OmnichannelSubjectPart`. Create one subject per interaction goal, such as `Lead Generation` for outbound prospecting. You can create additional subjects for other stages of the customer journey, for example `Lead Generation - 30 day follow-up` and `New Customer - Welcome`.

The screencast below creates a `Lead Generation` subject and opens its flow settings:

<video controls preload="metadata" width="100%" aria-label="Screen cast of creating a Lead Generation subject content type">
  <source src="/img/docs/omni-subject-leadgen.mp4" type="video/mp4" />
</video>

The former `OmnichannelSubject` stereotype is no longer recognized. Existing subject content types must remove that stereotype and attach `OmnichannelSubjectPart`.

Because subject content items are authored and completed through the omnichannel subject flow rather than the standard content workflow, the default content editor action buttons Orchard Core injects (**Publish**, **Save Draft**, and **Preview**) are automatically hidden on the editor of any content type that has `OmnichannelSubjectPart` attached. This applies as soon as the part is attached and is reverted automatically when the part is detached, without any placement configuration.

### 4) Create Dispositions

1. Go to `Interaction Center` → `Management` → `Dispositions`.
2. Create dispositions that represent outcomes (e.g. `Follow up`, `Not interested`, `Sold`).
3. After a disposition is created, you can still change its description, but its name remains read-only.

For a lead-conversion story, create outcomes such as `No answer`, `Call back`, `Follow up 30 days`, `Lead won`, and `Do not call`. The screencast below adds all five:

<video controls preload="metadata" width="100%" aria-label="Screen cast of creating omnichannel dispositions">
  <source src="/img/docs/omni-dispositions.mp4" type="video/mp4" />
</video>

### 5) Create Campaign Groups and Campaigns

1. Optionally go to `Interaction Center` → `Management` → `Campaign Groups` and create a name and description for a reporting group.
2. Go to `Interaction Center` → `Management` → `Campaigns`.
3. Create the campaign name and description and optionally select its campaign group.
4. Save the campaign.

Campaign groups let reporting users combine multiple related campaigns without changing activity execution. Activities continue to store the campaign identifier, and reports resolve the campaign's current group when they run. Moving a campaign to another group therefore changes the group used for historical aggregation.

### 6) Configure Subject Flows

Subject flow configuration lives on the `OmnichannelSubjectPart` content-type part settings, so you edit it from the content type editor. The `Interaction Center` → `Management` → `Subject Flows` list gives you a read-only overview and shortcuts.

1. Go to `Interaction Center` → `Management` → `Subject Flows` and review the content types that attach `OmnichannelSubjectPart`. Each subject shows a badge for its configured direction (**Outbound** or **Inbound**). Automated subjects additionally show an **Automated** badge and the channel being used.
2. To change the configuration, click **Edit Content Type** (shown when you have permission to edit content type definitions). This opens the Orchard Core content type editor for the subject. Alternatively, click **Edit Settings** to jump straight to the `OmnichannelSubjectPart` settings editor. On that part settings screen the Azure AI Search and Elasticsearch index settings that Orchard Core injects into every part editor are hidden, because indexing for omnichannel subjects is managed automatically.
3. In the `OmnichannelSubjectPart` settings, select the direction. New subjects default to **Outbound**.
4. For **Inbound** subjects, select the interaction type and channel; automated inbound subjects also require a channel endpoint. For **Outbound** subjects these fields are hidden because they are resolved when inventory is loaded.
5. Optionally set the default campaign, which is applied to activities created outside an activity batch and used as the batch fallback. Leave **Require a disposition** enabled unless the subject is a fire-and-forget notification with no outcome to record.
6. If the AI feature is enabled, the AI settings editor exposes the AI profile, subject goal, update permissions, speech-to-text deployment, text-to-speech deployment, voice, no-response timeout, response delay, and opt-out keyword fields. Only the fields that apply to the selected direction, interaction type, and channel are shown. Leaving a speech selection empty uses the global AI site setting when the automated conversation starts.
7. Save the content type.

Any content type with `OmnichannelSubjectPart` is a valid subject. The per-run campaign, channel, and channel endpoint used by each activity are chosen when an activity batch is loaded, so a subject does not need every field set on its part settings before it can be used.

### 7) Manage Flow

From the `Subject Flows` list, click **Manage Flow** next to a subject.

1. Click **Add Action**.
2. Select an action type (**Finish**, **Try Again**, or **New Activity**).
3. Choose a disposition and configure the action parameters. For **Try Again** and **New Activity**, choose **Same owner** or **Specific owner**.
4. Repeat to add multiple actions per disposition or for different dispositions.

**Example setup:**

| Disposition | Action Type | Notes |
|-------------|-------------|-------|
| Follow up | Try Again | Max 3 attempts, schedule 24 hours later |
| Not interested | Finish | Sets Do-Not-Call flag on contact |
| Sold | New Activity | Creates a new activity that targets the `Onboarding` subject |
| Sold | Finish | Completes the current workflow |

Subjects without any actions show a **Missing flow** badge in the Subject Flows list so you can find incomplete setups quickly.

The screencast below walks through a complete lead-generation flow. It assigns the `Spring Lead Drive` campaign to the subject, then maps four dispositions to actions that tell the story of converting a lead into a customer of the fictional *X Company*: **No answer** retries the call up to three times (**Try Again**), **Follow up 30 days** schedules a new activity against the *Lead Generation - 30 day follow-up* subject 720 hours out (**New Activity**), **Lead won** creates a *New Customer - Welcome* activity 72 hours (3 days) later, and **Do not call** finishes the interaction while setting the contact's Do-Not-Call preference (**Finish**).

<video controls preload="metadata" width="100%" aria-label="Screen cast of configuring a subject flow with disposition actions">
  <source src="/img/docs/omni-subject-flow.mp4" type="video/mp4" />
</video>

### 8) Create and Load Inventory

1. Go to `Interaction Center` → `Management` → `Load Inventory`.
2. Click **Add Inventory Load** and choose a source:
   - **Manual** loads activities assigned to the selected users immediately.
   - **Automatic** loads unassigned activities so the background AI automation processes them.
   - **Dialer** loads unassigned activities for outbound dialing and requires a dialer profile when the inventory load is created.
3. Create the inventory load:
   - Select contact type
   - Select subject type
   - Select the campaign to use for the loaded activities. The subject's part settings provide the defaults when a value is not chosen.
   - For **Automatic** loads, optionally select the AI profile just under the campaign. Leaving it empty uses the subject flow profile. The channel endpoint is also shown only for the automatic source.
   - Select the channel to use for the loaded activities. The channel is hidden for the dialer source because dialer loads always use the phone channel.
   - For **Dialer** inventory loads, select the required dialer profile that controls the dialing mode, queue, and campaign assignment.
   - Assign users when the selected source requires assignment.
   - Optionally set contact created range, phone number, time zone, and last activity filters
4. Click `Load`.

The screencast below creates a **Manual** inventory load for the *Lead Generation* subject, targets the `Contact` content type on the phone channel, assigns the generated activities to an agent, and loads the call list for the `Spring Lead Drive` campaign.

<video controls preload="metadata" width="100%" aria-label="Screen cast of creating a manual inventory load">
  <source src="/img/docs/omni-load-inventory-manual.mp4" type="video/mp4" />
</video>

The inventory load runs in the background and loads activities incrementally. Each created activity resolves its campaign, channel, channel endpoint, and interaction type from the batch selections, falling back to the subject's part settings. The interaction type is derived from the source: the **Automatic** source creates **Automated** activities, while other sources create **Manual** activities. Manual inventory loads assign each created activity to a selected user. Dialer inventory loads use the phone channel, leave activities unassigned with assignment status `Available`, and apply the selected dialer profile so the created activities inherit the profile's dialing mode and campaign before dialers reserve them later.

When an automated AI conversation completes, the activity stores the AI session identifier, appends the generated call summary as disposition notes, and applies the AI-selected disposition through the same subject-action lifecycle used by agents. Authorized administrators can open **Review AI conversation** from the activity actions to inspect the full transcript.

### Extending inventory load sources

Inventory loading is extensible. Each inventory load has a **source**, and the source controls how it resolves and loads activities. There are two layers of extensibility:

1. **Registering a source** — register sources through `ActivityBatchSourceOptions` in a feature `Startup`. Each `ActivityBatchSourceEntry` provides the display name, description, whether the source requires user assignment, and whether it should appear in the creation picker. Display drivers can add source-specific editor sections.

2. **Controlling the load** — implement `IActivityBatchLoader` (from `CrestApps.OrchardCore.Omnichannel.Core.Services`) to fully own how a source queries leads, applies filters, and creates activities. The loader's `Source` property must match the registered source. Register the loader as a scoped service:

   ```csharp
   services.AddScoped<IActivityBatchLoader, MyCustomActivityBatchLoader>();
   ```

When an inventory load is started, the `IActivityBatchLoadCoordinator` transitions it to the loading state, resolves the loader whose `Source` matches the selected source, and delegates to it. Sources **without** a dedicated loader fall back to the built-in `DefaultContactActivityBatchLoader`, which pages over contacts of the inventory load's contact content type, applies the standard lead filters (created range, phone number, time zone, last completed activity), and creates activities from the subject flow settings. The default loader is not sealed, so a custom loader can inherit from it to reuse the contact-paging pipeline while overriding individual stages. If a loader throws, the coordinator logs the error and returns the inventory load to the `New` state so it can be retried.

### 9) Complete Activities

1. Open an activity from the activities list.
2. Review the contact and subject details.
3. Select a disposition from the dropdown.
4. A preview appears showing what actions will execute (for example, `Try Again` with a schedule date or `New Activity` targeting another subject).
5. Adjust the schedule dates if needed, and optionally add **Preparation notes** for any result. A note is stored as the instructions of the follow-up activity it generates.
6. Click **Complete** to save and execute the subject actions.

The screencast below shows an agent working their assigned queue. The first call is completed with **No answer**, which triggers the *Try Again* action: an inline **Schedule at** calendar and **Preparation notes** field appear so the agent can override the default next call time and leave a note for the retry. A second lead is completed with **Lead won**, and the *New Activity* action automatically schedules a *New Customer - Welcome* call three days out.

<video controls preload="metadata" width="100%" aria-label="Screen cast of an agent completing activities with dispositions">
  <source src="/img/docs/omni-agent-activities.mp4" type="video/mp4" />
</video>

### Scheduled activities list

Navigate to **Interaction Center** -> **Activities** to review scheduled omnichannel work at `Admin/omnichannel/activities`.

The scheduled activities list now includes a **Time zone** filter alongside the existing urgency, subject, channel, and attempt filters so agents can narrow work to leads in call-safe regions. Activity summary rows also display the contact's current local time when a lead time zone is stored, and the tooltip shows the full local date/time plus the IANA time zone id so agents can confirm whether the lead is ahead of or behind their own day before opening or completing the activity.

Users with the **Purge activity** permission see a **Purge** button on each scheduled activity in a contact profile. Purging is irreversible, changes the activity status to `Purged`, records the UTC purge time and current user's identifier and username for auditing, and clears any reservation state while preserving assignment. The same permission is required for the bulk **Purge** action on the Manage Activities page; every activity in one bulk operation records the same purge time and actor, and **Manage activities** implies **Purge activity**.

### Contacts list

The `Interaction Center` → `Contacts` menu item opens the standard Orchard Core content list restricted to your Omnichannel contact content types. It links to the content `List` action and passes every content type that has `OmnichannelContactPart` attached as a comma-separated `contentTypeId`, so the resulting screen only shows contact items and only offers contact types when creating new content.

The contact content types are read from the cached provider that tracks which types attach `OmnichannelContactPart`, so the menu stays in sync as you attach or detach the part without scanning every content definition on each request. The menu item is available to users with the **List content** permission.

### Phone number search

Phone filters in **Load Inventory**, **Manage Activities**, and Content Admin search the primary **Cell** and **Home** contact methods.

- Input that does not begin with `+` is reduced to digits and matched against the national number, so values such as `702499`, `(702) 499`, or `702-499` are accepted.
- Input whose trimmed value begins with `+` is matched against the E.164 value. The plus sign is a literal format indicator, not a wildcard.
- **Contains** is the default match mode. **Exact match**, **Begins with**, and **Ends with** are also available in Load Inventory and Manage Activities.

Content Admin evaluates the displayed content version. Load Inventory uses published or latest contact values according to **Only published leads**, while Manage Activities uses the latest saved contact values.

The shared contact index stores primary Cell and Home numbers as national digits for national searches, while the corresponding normalized values remain in E.164 format.

Content Admin supports these named search terms:

| Term | Match behavior | Example |
|------|----------------|---------|
| `phone:` | Contains | `phone:702499` |
| `phone-exact:` | Exact match | `phone-exact:7024993350` |
| `phone-starts:` | Begins with | `phone-starts:+1702` |
| `phone-ends:` | Ends with | `phone-ends:3350` |

National-number searches can match contacts from more than one country. Use a leading `+` when the country calling code must be part of the search.

## Bulk Activity Management

The **Manage Activities** page provides a centralized interface for managing active omnichannel inventory across manual, automated, and dialer-oriented activities. It targets editable work states such as `NotStarted`, `Scheduled`, `Pending`, `AwaitingAgentResponse`, `Failed`, and `Cancelled` so managers can clean up, re-route, or reclassify queued work without opening each activity one by one. Historical activities without a subject content type remain manageable and are represented by the generic **Activity** type instead of failing the page or completion action.

### Accessing the page

Navigate to **Interaction Center → Management → Manage Activities** in the admin menu. This page is available to users with the **Manage Activities** permission.

Route: `Admin/omnichannel/manage-activities`

### Filters

The filter panel groups fields into **Contact filters** and **Activity filters** so managers can narrow the result set quickly. Every filter is rendered with its own label so the purpose of each field stays clear. The phone match-type selector keeps a screen-reader-only label because it sits inside the phone number field group.

The filter card is collapsible and does not stick to the top of the page, which leaves the full viewport available for results. Use the **Filters** toggle in the card header to expand or collapse the panel. The chosen state is stored in the browser's local storage, so the panel reopens in the same state on the next visit.

#### Contact Filters

| Filter | Type | Description |
|--------|------|-------------|
| Contact status | Select | Filter by published or unpublished contacts |
| Phone number | Text | Search primary Cell and Home numbers using national-number fragments or a leading `+` for E.164 |
| Phone match type | Select | Contains, exact match, begins with, or ends with |
| Time zones | Multi-select | Filter by one or more contact time zones |
| Do not call from | Date | Only include contacts marked as do-not-call on or after this date |
| Do not call to | Date | Only include contacts marked as do-not-call on or before this date |

#### Activity Filters

| Filter | Type | Description |
|--------|------|-------------|
| Attempts | Select | Filter by the current attempt number. Values `0` and `1` both mean no attempt, and `2` means the second attempt. |
| Subject | Select | Filter by subject content type |
| Channel | Select | Filter by communication channel (Phone, SMS, Email) |
| Source | Select | Filter by activity source such as Manual, Automatic, Dialer, Preview dial, Power dial, or Progressive dial |
| Interaction type | Select | Filter by manual versus automated activities |
| Status | Select | Filter by active editable statuses |
| Assignment status | Select | Filter by unassigned, available, reserved, assigned, in-progress, or released work |
| Campaign | Select | Filter by campaign |
| Assigned to users | User picker | Filter by one or more assigned users |
| Urgency level | Select | Filter by urgency level (Normal, Low, Medium, High, etc.) |
| Scheduled from | Date | Filter activities scheduled on or after this date |
| Scheduled to | Date | Filter activities scheduled on or before this date |
| Created from | Date | Filter activities created on or after this date |
| Created to | Date | Filter activities created on or before this date |
| Limit | Number | Limit the number of records to retrieve |

The assigned-user filter is displayed on its own row to make multi-user searches easier to manage, and it searches across all users instead of only agent-role users.

Activity rows display an urgency icon so managers can identify priority visually at a glance.

### Bulk Actions

Use the **Bulk actions** card to choose an action and its scope:

The screencast below shows a manager redistributing queued work: they select the activities, open the **Select an action** menu (which exposes Assign, Reschedule, Set Urgency Level, Change Subject, and more), reassign the activities to an agent, and execute the bulk action.

<video controls preload="metadata" width="100%" aria-label="Screen cast of a manager reassigning activities in bulk">
  <source src="/img/docs/omni-manager-redistribute.mp4" type="video/mp4" />
</video>

- Apply the action to the activities selected on the current page
- Apply the action to **all matching activities** returned by the current filter

The page also includes a **Page size** selector so managers can review more than the default number of results at once.

| Action | Description |
|--------|-------------|
| **Assign** | Assign activities to one or more users. When multiple users are selected, activities are evenly distributed (round-robin). |
| **Reschedule** | Set a new scheduled date for all selected activities. |
| **Purge** | Change the status of selected activities to `Purged`. This cannot be undone. |
| **Set Instructions** | Set instruction text for all selected activities. Instructions are notes the agent reads before completing the task. |
| **Set Urgency Level** | Update the urgency level for all selected activities. |
| **Change Subject** | Change the subject content type for all selected activities. |
| **Clear Assignment** | Remove the current assignee and clear reservation state so the activity can be re-routed or dialed again. |
| **Change Source** | Change the activity source and optionally clear assignment and reservation state. This is useful when reclassifying inventory between manual, automatic, and dialer-style workflows. |
| **Change Dialer Profile** | When the Contact Center dialer feature is available, update the activity campaign and dialer source to match a selected dialer profile. This can also clear assignment and reservation state so the dialer can pick the activity up again. |

Use **Change Source** and **Clear Assignment** together when you need to convert assigned manual work back into dialer-ready inventory. Use **Change Dialer Profile** when you want to move selected outbound inventory to a different dialer campaign path without recreating the activities.

## Exporting and importing configuration

Omnichannel configuration moves between environments through Orchard Core's standard deployment and recipe pipelines, so a tenant can be provisioned from staging to production without re-entering settings by hand.

Each configurable entity has its own deployment step and a matching recipe step:

| Entity | Deployment step (category **Omnichannel**) | Recipe step name |
|--------|--------------------------------------------|------------------|
| Dispositions | Omnichannel Dispositions | `OmnichannelDisposition` |
| Channel endpoints | Omnichannel Channel Endpoints | `OmnichannelChannelEndpoint` |
| Campaign groups | Omnichannel Campaign Groups | `OmnichannelCampaignGroup` |
| Campaigns | Omnichannel Campaigns | `OmnichannelCampaign` |
| Subject actions | Omnichannel Subject Actions | `OmnichannelSubjectAction` |

To export, open **Configuration -> Import/Export -> Deployment Plans**, add the Omnichannel steps you need, and execute or download the plan. Each step exports every entry of its type.

On import, entries are matched by their identifier: an entry that already exists is updated in place, and a new entry is created with its original identifier preserved. Because identifiers are preserved, cross-references (for example a campaign that points at a campaign group, or a subject action that points at a disposition) keep working after the import.

When a plan carries several of these steps, order them so that referenced entities import first: dispositions and channel endpoints, then campaign groups, then campaigns, and finally subject actions.

Subject flow configuration is stored on the `OmnichannelSubjectPart` content-type part settings, so it travels with the content type definition through the standard **Content Definition** deployment step rather than a dedicated omnichannel step.

## Data at rest and privacy

The omnichannel/CRM layer stores customer communication content and contact addresses as **plaintext** in the tenant SQL database. `OmnichannelMessage.Content` (the message body), `OmnichannelMessage.CustomerAddress`, and `OmnichannelMessage.ServiceAddress` are persisted unencrypted in the YesSql document, and the two addresses are additionally projected — still in plaintext — into the `OmnichannelMessageIndex` table so they can be queried. No application-level encryption is applied to this data.

This is a deliberate contrast with telephony **recording media**, which the media-execution layer encrypts at rest through the data protection provider. That asymmetry matters operationally: encrypting the recording bytes does not encrypt the message bodies or the phone numbers/addresses that the CRM stores alongside them. Protecting this content at rest is therefore a **deployment responsibility** — enable database- or disk-level encryption (for example, transparent data encryption) and restrict access to the database and its backups accordingly. Treat message content and contact addresses as personal data.

There is currently **no automated per-contact subject erasure** (right-to-be-forgotten) across the CRM. The activity **Purge** action marks an activity as `Purged` and removes it from the work queue, but it does **not** delete the underlying message content, the customer/service addresses, or the contact record — that data remains in the database and its index. Comprehensive per-contact erasure across omnichannel activities, messages, and contacts is a known limitation and a general-availability blocker; until it ships, satisfy erasure requests through direct, audited database operations against the tenant store.
