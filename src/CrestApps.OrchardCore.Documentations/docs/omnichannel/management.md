---
sidebar_label: "Management (Mini-CRM)"
sidebar_position: 2
title: CrestApps Omnichannel Management (Mini-CRM)
description: Mini-CRM for managing contacts, campaign actions, and activity-driven processes across communication channels.
---

| | |
| --- | --- |
| **Feature Name** | Omnichannel Management |
| **Feature ID** | `CrestApps.OrchardCore.Omnichannel.Managements` |

Provides way to manage Omnichannel Contacts.

## Overview

The `CrestApps.OrchardCore.Omnichannel.Managements` module is a **mini-CRM** built on Orchard Core.

It provides everything you need to manage **contacts**, define **campaign actions**, and run activity-driven processes (manual or AI-automated) across communication channels such as SMS, Email, and Phone.

## Core concepts

### Contact
A **Contact** is any content item that has `OmnichannelContactPart` attached.

This lets you model customers/leads however you want (name, phone, email, account fields, custom fields, etc.).

### Subject ("the nature of the interaction")
A **Subject** is any content type with the `OmnichannelSubject` stereotype.

Subjects are used to describe the nature of the interaction and to define the data you want agents (human or AI) to capture during the interaction. You can add any fields, parts, or custom data to the subject.

### Disposition
A **Disposition** is the outcome of an activity (e.g. `Completed`, `FollowUp`, `DoNotCall`, `Scheduled`, `Sold`).

Dispositions are a key building block for controlling what happens next via campaign actions.

### Campaign
A **Campaign** ties together:

- The channel to use (SMS / Email / Phone)
- The campaign actions that define what happens for each disposition
- The endpoint identity to send from (e.g. phone number for SMS, "from" address for Email)

### Campaign Action
A **Campaign Action** links a disposition to an action type and defines what happens when an activity is completed with that disposition.

Each campaign can have multiple actions per disposition, and each action has its own parameters.

**Available action types:**

| Type | Description |
|------|-------------|
| **Finish** | Completes the task. No additional actions are taken. |
| **Try Again** | Creates a retry activity with the same details and an incremented attempt count. Configurable parameters: max attempts, urgency level, assigned user, default schedule hours. |
| **New Activity** | Creates a brand new activity, optionally targeting a different campaign or subject type. Configurable parameters: target campaign, subject content type, urgency level, assigned user, default schedule hours. |

**Communication preferences:** Every action type can optionally set Do-Not-Call, Do-Not-SMS, Do-Not-Email, and Do-Not-Chat flags on the contact when executed.

### Activity
An **Activity** is a task to be completed for a contact.

- **Manual activity**: A user completes the activity in the UI, adds notes, and selects a disposition.
- **Automated activity**: An AI agent completes the activity through the configured channel.

When an activity is completed, the user selects a disposition and is shown a preview of the campaign actions that will execute. Actions that create follow-up activities allow the user to adjust the schedule date before submitting.

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
3. Configure a channel endpoint if needed (e.g. phone number for SMS).

### 6) Add Campaign Actions

After creating a campaign, scroll to the **Campaign Actions** section at the bottom of the campaign editor.

1. Click **Add Campaign Action**.
2. Select an action type (**Finish**, **Try Again**, or **New Activity**).
3. Choose a disposition and configure the action parameters.
4. Repeat to add multiple actions per disposition or for different dispositions.

**Example setup:**

| Disposition | Action Type | Notes |
|-------------|-------------|-------|
| Follow up | Try Again | Max 3 attempts, schedule 24 hours later |
| Not interested | Finish | Sets Do-Not-Call flag on contact |
| Sold | New Activity | Creates a new activity in the "Onboarding" campaign |
| Sold | Finish | Completes the current workflow |

### 7) Create and Load an Activity Batch

1. Go to `Omnichannel` → `Activity Batches`.
2. Create a new batch:
   - Select the campaign
   - Select contact type
   - Select subject type
   - Assign agents
   - Optionally set lead created range filters
3. Click `Load`.

The batch will run in the background and will load activities incrementally.

### 8) Complete Activities

1. Open an activity from the activities list.
2. Review the contact and subject details.
3. Select a disposition from the dropdown.
4. A preview appears showing what actions will execute (e.g. "Try Again — schedule date" or "New Activity — target campaign").
5. Adjust the schedule dates if needed.
6. Click **Complete** to save and execute the campaign actions.

