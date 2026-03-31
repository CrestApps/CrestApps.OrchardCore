---
sidebar_label: AI Workflows
sidebar_position: 11
title: AI Chat with Workflows
description: How to use AI completion tasks in Orchard Core Workflows.
---

# AI Chat with Workflows

When combined with the **Workflows** feature, the **AI Services** module introduces new activities that allow workflows to interact directly with AI chat services.

## AI Completion using Profile Task

This activity lets you request AI completions using an existing **AI Profile**, and store the response in a workflow property.
To use it, search for the **AI Completion using Profile** task in your workflow and specify a unique **Result Property Name**.
The generated response will be saved in this property.

For example, if the **Result Property Name** is `AI-CrestApps-Step1`, you can access the response later using:

```liquid
{{ Workflow.Output["AI-CrestApps-Step1"].Content }}
```

To prevent naming conflicts with other workflow tasks, it's recommended to prefix your **Result Property Name** with `AI-`.

## AI Completion using Direct Config Task

This activity allows you to request AI completions by defining the configuration directly within the workflow, without relying on a predefined AI Profile.
To use it, search for the **AI Completion using Direct Config** task in your workflow, choose the chat **Deployment** to execute, and specify a unique **Result Property Name**.
The generated response will be saved in this property.

For example, if the **Result Property Name** is `AI-CrestApps-Step1`, you can access the response later using:

```liquid
{{ Workflow.Output["AI-CrestApps-Step1"].Content }}
```

As with other AI tasks, it's recommended to prefix your **Result Property Name** with `AI-` to avoid conflicts.

## Chat Session Workflow Events

The AI Services module triggers workflow events at key points in the chat session lifecycle. These events include full `Session` and `Profile` objects in their input dictionaries, enabling rich workflow automation.

### AI Chat Session Closed

Triggered when a chat session is closed (either naturally or by inactivity timeout).

| Input Property | Type | Description |
| --- | --- | --- |
| `SessionId` | string | The unique session identifier. |
| `ProfileId` | string | The AI profile identifier. |
| `Session` | AIChatSession | The full session object with extracted data and metadata. |
| `Profile` | AIProfile | The full AI profile configuration. |
| `ClosedBecauseFarewellDetected` | bool | Whether the session closed due to farewell intent detection. |

### AI Chat Session Post-Processed

Triggered after post-session processing completes on a closed session.

| Input Property | Type | Description |
| --- | --- | --- |
| `SessionId` | string | The unique session identifier. |
| `ProfileId` | string | The AI profile identifier. |
| `Session` | AIChatSession | The full session object. |
| `Profile` | AIProfile | The full AI profile configuration. |
| `Results` | Dictionary | Post-session processing results keyed by task name. |

### AI Chat Session Field Extracted

Triggered each time a data extraction field value is collected from the conversation.

| Input Property | Type | Description |
| --- | --- | --- |
| `SessionId` | string | The unique session identifier. |
| `ProfileId` | string | The AI profile identifier. |
| `Session` | AIChatSession | The full session object. |
| `Profile` | AIProfile | The full AI profile configuration. |
| `Changes` | List&lt;ExtractedFieldChange&gt; | The field changes extracted in this turn. |

Each `ExtractedFieldChange` has:
- `Key`: The field name.
- `Value`: The extracted value.
- `PreviousValue`: The previous value (if updated).

### AI Chat Session All Fields Extracted

Triggered once when **all** configured data extraction fields have been collected for a session. Unlike the per-field event, this fires only when every entry in the profile's data extraction configuration has at least one value.

| Input Property | Type | Description |
| --- | --- | --- |
| `SessionId` | string | The unique session identifier. |
| `ProfileId` | string | The AI profile identifier. |
| `Session` | AIChatSession | The full session object. |
| `Profile` | AIProfile | The full AI profile configuration. |

