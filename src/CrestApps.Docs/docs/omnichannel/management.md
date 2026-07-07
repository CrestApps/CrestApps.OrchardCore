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

Provides way to manage Omnichannel Contacts.

## Overview

The `CrestApps.OrchardCore.Omnichannel.Managements` module is a lightweight **Customer Relationship Management (CRM)** experience built on Orchard Core.

It provides the admin tools you need to manage **contacts**, define **subject-level flows**, group work under **campaigns**, and run activity-driven processes (manual or automated) across channels such as SMS, email, and phone.

## Core concepts

### Contact
A **Contact** is any content item that has `OmnichannelContactPart` attached.

This lets you model customers/leads however you want (name, phone, email, account fields, custom fields, etc.).

### Subject ("the nature of the interaction")
A **Subject** is any content type with the `OmnichannelSubject` stereotype.

Subjects are used to describe the nature of the interaction and to define the data you want agents (human or AI) to capture during the interaction. You can add any fields, parts, or custom data to the subject.

### Disposition
A **Disposition** is the outcome of an activity (e.g. `Completed`, `FollowUp`, `DoNotCall`, `Scheduled`, `Sold`).

Dispositions are a key building block for controlling what happens next via subject actions. Disposition names are unique and become fixed after creation so subject-flow mappings stay stable.

### Campaign
A **Campaign** is now used primarily for **reporting, grouping, and business outcome tracking**.

Campaigns no longer define the interaction type, channel, channel endpoint, or disposition-driven flow logic. Those settings now live on the subject flow so different subjects inside the same campaign can behave differently.

### Subject Flow
A **Subject Flow** defines how a specific `OmnichannelSubject` content type behaves.

Each subject flow stores:

- the campaign association used for reporting and grouping
- the interaction type (`Manual` or `Automated`)
- the communication channel
- the channel endpoint used for automated work
- the subject actions that run for each disposition

When the AI feature is enabled, the subject flow editor also adds AI-specific settings for:

- the chat AI profile, filtered to profiles with **Add initial prompt** enabled
- the subject goal
- AI update permissions for the contact and subject
- SMS automation controls such as no-response timeout, response delay, and opt-out keywords

### Subject Action
A **Subject Action** links a disposition to an action type and defines what happens when an activity is completed with that disposition for a given subject type.

Each subject can have multiple actions per disposition, and each action has its own parameters.

**Available action types:**

| Type | Description |
|------|-------------|
| **Finish** | Completes the task. No additional actions are taken. |
| **Try Again** | Creates a retry activity with the same details and an incremented attempt count. Configurable parameters: max attempts, urgency level, assigned user, default schedule hours. |
| **New Activity** | Creates a brand new activity, optionally targeting a different subject type. The new activity resolves its campaign, interaction type, and channel settings from the target subject flow. |

**Communication preferences:** Every action type can optionally set Do-Not-Call, Do-Not-SMS, Do-Not-Email, and Do-Not-Chat flags on the contact when executed.

### Activity
An **Activity** is a task to be completed for a contact.

- **Manual activity**: A user completes the activity in the UI, adds notes, and selects a disposition.
- **Automated activity**: An AI agent completes the activity through the configured channel.

When an activity is completed, the user selects a disposition and is shown a preview of the subject actions that will execute. Actions that create follow-up activities allow the user to adjust the schedule date before submitting.

Editing an already completed activity does **not** re-run workflow logic. Administrators can correct the saved disposition or notes without creating retry or follow-up activities.

### Activity Batch
An **Activity Batch** defines filters to find contacts and then **loads activities in the background**.

The batch loader runs as a background process to avoid overloading the system and to allow loading large contact lists safely.

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
Use **Settings** -> **Import Content Settings** to enforce DNC checks globally for imports, and use **Settings** -> **DNC Registries** to configure provider access for registries such as **USA FTC Registry** and **Canada LNNTE-DNCL Registry**. See [DNC Registry](../modules/dnc-registry) for setup details, credential requirements, and extension guidance.

When the import file is not already using E.164 phone numbers, select the default country represented by that file in the import UI. Files for content types with `OmnichannelContactPart` should contain leads from one country per file unless every phone number is already expressed in E.164. The picker mirrors the Local DNC country list, shows each option as `Country (+calling code)`, and is required before the import can start so phone normalization always has region context.

### 3) Create your Subject content type

1. Go to `Content` → `Content Definition` → `Content Types`.
2. Create a new content type.
3. Set stereotype to `OmnichannelSubject`.
4. Add any fields/parts you want the agent to capture during the interaction.

### 4) Create Dispositions

1. Go to `Interaction Center` → `Dispositions`.
2. Create dispositions that represent outcomes (e.g. `Follow up`, `Not interested`, `Sold`).
3. After a disposition is created, you can still change its description, but its name remains read-only.

### 5) Create a Campaign

1. Go to `Interaction Center` → `Campaigns`.
2. Create the campaign name and description.
3. Save the campaign.

### 6) Configure Subject Flows

After creating your subject content types and campaigns, go to `Interaction Center` → `Subject Flows`.

1. Review the list of content types with the `OmnichannelSubject` stereotype.
2. Click **Configure** next to a subject.
3. Select the campaign used for reporting and grouping.
4. Select the interaction type and channel.
5. If the subject uses automated interactions, configure the channel endpoint.
6. If the AI feature is enabled, automated subject flows also expose the AI profile, subject goal, update permissions, no-response timeout, response delay, and opt-out keyword fields.
7. Save the subject flow.

Subjects are only considered **configured** after the flow has the required campaign, channel, and interaction settings (plus a channel endpoint and AI profile for automated flows). Activity creation, batch loading, and subject-selection UIs only allow configured subjects because the subject flow now supplies the campaign and runtime channel settings used by each activity.

### 7) Manage Flow

After saving the subject flow, click **Manage Flow** from the `Subject Flows` list.

1. Click **Add Action**.
2. Select an action type (**Finish**, **Try Again**, or **New Activity**).
3. Choose a disposition and configure the action parameters.
4. Repeat to add multiple actions per disposition or for different dispositions.

**Example setup:**

| Disposition | Action Type | Notes |
|-------------|-------------|-------|
| Follow up | Try Again | Max 3 attempts, schedule 24 hours later |
| Not interested | Finish | Sets Do-Not-Call flag on contact |
| Sold | New Activity | Creates a new activity that targets the `Onboarding` subject |
| Sold | Finish | Completes the current workflow |

Subjects without any actions show a **Missing flow** badge in the Subject Flows list so you can find incomplete setups quickly.

### 8) Create and Load an Activity Batch

1. Go to `Interaction Center` → `Activity Batches`.
2. Click **Add Activity Batch** and choose a source:
   - **Manual** loads activities assigned to the selected users immediately.
   - **Automatic** loads unassigned automated activities for AI processing.
   - **Dialer** loads unassigned activities that a dialer reserves and assigns later.
3. Create the new batch:
   - Select contact type
   - Select subject type
   - For **Automatic** batches, optionally select an AI profile. The list only includes chat profiles
     with **Add initial prompt** enabled; leaving it empty uses the subject flow's AI profile.
   - Assign users when the selected source requires assignment
   - Optionally set lead created range, phone number, time zone, and last activity filters
4. Click `Load`.

The batch runs in the background and loads activities incrementally. Loaded activities use the selected subject's flow configuration to resolve the campaign, interaction type, channel, and channel endpoint. For Automatic batches, the loader stores the selected batch AI profile on each activity; if no batch profile is selected, the activity uses the subject flow's AI profile. The automated activity processor then uses that profile's initial prompt to send the first outbound SMS and to continue the AI conversation when the contact replies. Manual batches assign each created activity to a selected user. Automatic batches leave activities unassigned but immediately eligible for the automated activity processor when their schedule is due. Dialer batches leave activities unassigned with assignment status `Available` so dialers can reserve and assign them safely later.

### Extending activity batch sources

Activity batch loading is extensible. Each batch has a **source**, and the source controls how the batch resolves and loads activities. There are two layers of extensibility:

1. **Registering a source** — register sources through `ActivityBatchSourceOptions` in a feature `Startup`. Each `ActivityBatchSourceEntry` provides the display name, description, and whether the source requires user assignment. Registered sources appear as creation cards, and display drivers can add source-specific editor sections.

2. **Controlling the load** — implement `IActivityBatchLoader` (from `CrestApps.OrchardCore.Omnichannel.Core.Services`) to fully own how a source queries leads, applies filters, and creates activities. The loader's `Source` property must match the registered source. Register the loader as a scoped service:

   ```csharp
   services.AddScoped<IActivityBatchLoader, MyCustomActivityBatchLoader>();
   ```

When a batch is loaded, the `IActivityBatchLoadCoordinator` transitions the batch to the loading state, resolves the loader whose `Source` matches the batch source, and delegates to it. Sources **without** a dedicated loader fall back to the built-in `DefaultContactActivityBatchLoader`, which pages over contacts of the batch contact content type, applies the standard lead filters (created range, phone number, time zone, last completed activity), and creates activities from the subject flow settings. The default loader is not sealed, so a custom loader can inherit from it to reuse the contact-paging pipeline while overriding individual stages. If a loader throws, the coordinator logs the error and returns the batch to the `New` state so it can be retried.

### 9) Complete Activities

1. Open an activity from the activities list.
2. Review the contact and subject details.
3. Select a disposition from the dropdown.
4. A preview appears showing what actions will execute (for example, `Try Again` with a schedule date or `New Activity` targeting another subject).
5. Adjust the schedule dates if needed.
6. Click **Complete** to save and execute the subject actions.

### Scheduled activities list

Navigate to **Interaction Center** -> **Activities** to review scheduled omnichannel work at `Admin/omnichannel/activities`.

The scheduled activities list now includes a **Time zone** filter alongside the existing urgency, subject, channel, and attempt filters so agents can narrow work to leads in call-safe regions. Activity summary rows also display the contact's current local time when a lead time zone is stored, and the tooltip shows the full local date/time plus the IANA time zone id so agents can confirm whether the lead is ahead of or behind their own day before opening or completing the activity.

## Bulk Activity Management

The **Manage Activities** page provides a centralized interface for managing active omnichannel inventory across manual, automated, and dialer-oriented activities. It targets editable work states such as `NotStarted`, `Scheduled`, `Pending`, `AwaitingAgentResponse`, `Failed`, and `Cancelled` so managers can clean up, re-route, or reclassify queued work without opening each activity one by one.

### Accessing the page

Navigate to **Interaction Center → Manage Activities** in the admin menu. This page is available to users with the **Manage Activities** permission.

Route: `Admin/omnichannel/manage-activities`

### Filters

The filter panel groups fields into **Contact filters** and **Activity filters** so managers can narrow the result set quickly.

#### Contact Filters

| Filter | Type | Description |
|--------|------|-------------|
| Contact status | Select | Filter by published or unpublished contacts |

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

The assigned-user filter is displayed on its own row to make multi-user searches easier to manage, and it searches across all users instead of only agent-role users.

Activity rows display an urgency icon so managers can identify priority visually at a glance.

### Bulk Actions

Use the **Bulk actions** card to choose an action and its scope:

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
