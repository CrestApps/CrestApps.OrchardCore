# CrestApps Omnichannel Management (Mini-CRM)

The `CrestApps.OrchardCore.Omnichannel.Managements` module is a **mini-CRM** built on Orchard Core.

It provides everything you need to manage **contacts**, define your **workflows**, and run activity-driven processes (manual or AI-automated) across communication channels such as SMS, Email, and Phone.

This module is typically used together with:

- `CrestApps.OrchardCore.Omnichannel` (core/orchestrator)
- A channel automation module (e.g. `CrestApps.OrchardCore.Omnichannel.Sms`)

## Core concepts

### Contact
A **Contact** is any content item that has `OmnichannelContactPart` attached.

This lets you model customers/leads however you want (name, phone, email, account fields, custom fields, etc.).

### Subject ("the nature of the interaction")
A **Subject** is any content type with the `OmnichannelSubject` stereotype.

Subjects are used to describe the nature of the interaction and to define the data you want agents (human or AI) to capture during the interaction. You can add any fields, parts, or custom data to the subject.

### Disposition
A **Disposition** is the outcome of an activity (e.g. `Completed`, `FollowUp`, `DoNotCall`, `Scheduled`, `Sold`).

Dispositions are a key building block for controlling what happens next in your workflow.

### Campaign
A **Campaign** ties together:

- The channel to use (SMS / Email / Phone)
- The allowed dispositions for activities in that campaign
- The endpoint identity to send from (e.g. phone number for SMS, “from” address for Email)

### Activity
An **Activity** is a task to be completed for a contact.

- **Manual activity**: A user completes the activity in the UI, adds notes, and selects a disposition.
- **Automated activity**: An AI agent completes the activity through the configured channel.

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
   - `OrchardCore.Workflows`
   - (Optional) `SMS Omnichannel Automation` if you want AI/SMS automation

### 2) Create your Contact content type

1. Go to `Content` → `Content Definition` → `Content Types`.
2. Create a new content type (e.g. `Contact`).
3. Attach `OmnichannelContactPart`.
4. Add any fields/parts you need (phone number, email, lead status, custom fields, etc.).
5. Create/import contact items.

### 3) Create your Subject content type

1. Go to `Content` → `Content Definition` → `Content Types`.
2. Create a new content type.
3. Set stereotype to `OmnichannelSubject`.
4. Add any fields/parts you want the agent to capture during the interaction.

### 4) Create Dispositions

1. Go to `Omnichannel` → `Dispositions`.
2. Create dispositions that represent outcomes (e.g. `Follow up`, `Not interested`, `Sold`).

### 5) Create a Campaign

1. Go to `Omnichannel` → `Campaigns`.
2. Select a channel (SMS/Email/Phone).
3. Choose the dispositions that can be used for activities in this campaign.
4. Configure a channel endpoint if needed (e.g. phone number for SMS).

### 6) Create and Load an Activity Batch

1. Go to `Omnichannel` → `Activity Batches`.
2. Create a new batch:
   - Select the campaign
   - Select contact type
   - Select subject type
   - Assign agents
   - Optionally set lead created range filters
3. Click `Load`.

The batch will run in the background and will load activities incrementally.

### 7) Build your Workflow (Tool → Design)

1. Go to `Workflows` → `Workflows`.
2. Create a new workflow.
3. Use `Tool` → `Design`.
4. Use Omnichannel events/tasks to control what happens when an activity is completed.

A common approach:

- Handle `Completed Activity` event.
- Branch based on the selected **Disposition**.
- Create/schedule the next activity (or end the process).

## Related modules

- Omnichannel core/orchestrator: `../CrestApps.OrchardCore.Omnichannel/README.md`
- SMS Omnichannel Automation (AI): `../CrestApps.OrchardCore.Omnichannel.Sms/README.md`
- Omnichannel (Azure Event Grid): `../CrestApps.OrchardCore.Omnichannel.EventGrid/README.md`
