---
sidebar_label: AI in Workflows
title: AI in Workflows
description: Use AI tasks in automated workflows, and start a workflow when an AI chat ends or collects information.
technical_manual:
  - ai/workflows
---

Orchard Core **Workflows** let you automate work without code. The AI features add two tasks that ask an AI model for an answer, and four events that start a workflow when something happens in an AI chat. Use them when you want a summary, a classification or a draft written automatically, or when you want your team to act on what a visitor told the website assistant.

| | |
| --- | --- |
| **Menu** | Design > Workflows |
| **Permission** | Manage workflows |
| **Feature** | Workflows, plus AI Services. **AI Completion using Direct Config** also needs AI Chat Interactions. |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of adding an AI Completion using Profile task to a workflow">
  <source src="/img/docs/ai-workflows.mp4" type="video/mp4" />
</video>

## Add an AI task to a workflow

1. Open **Design > Workflows** and open your workflow, or click **Create Workflow** to start a new one.
2. Click **Add Task**, find **AI Completion using Profile** (or **AI Completion using Direct Config**) under **Artificial Intelligence**, and click **Add**.
3. Fill in the fields described below and click **Save**.
4. On the canvas, connect the task to the step before it, and connect its outcomes to the steps that follow.
5. Click **Save**.

The answer is stored under the name you give in **Result property name**, so later steps can use it. For example, with the name `AI-Summary`, a later task can insert the answer with `{{ Workflow.Output["AI-Summary"].Content }}`.

Every AI task has three outcomes:

| Outcome | When it happens |
| --- | --- |
| **Done** | The AI answered. The answer is stored in the result property. |
| **Drew Blank** | The AI returned an empty answer. |
| **Failed** | The profile was not found, the prompt was empty, or the AI service returned an error. |

:::tip
Connect **Failed** and **Drew Blank** to something useful, such as a notification to an administrator, so a workflow never stops silently.
:::

### AI Completion using Profile

Uses an existing [AI profile](profiles.md), with its model, instructions, knowledge and tools. Use it when you already have a profile that does the job, for example a summarizer.

| Field | What it does |
| --- | --- |
| **Profiles** | The AI profile that answers. Required. |
| **Result property name** | The name the answer is stored under. Start it with `AI-` so it does not clash with other tasks. Required. |
| **Prompt template** | The message sent to the AI. You can mix your own text with values from the workflow, written in Liquid, for example the output of an earlier task. Required. |

### AI Completion using Direct Config

Sets up the model and the instructions right inside the task, without a profile. Its editor has the same settings as a [chat interaction](chat-interactions.md), spread over tabs:

| Tab | What you set |
| --- | --- |
| **Content** | **Result property name**, **Prompt template**, **Orchestrator**, **Chat deployment**, **Utility deployment**, **System instructions**, reusable prompt templates (when AI Prompt Templates is on), and the tuning values **Max response tokens**, **Temperature**, **Top P**, **Frequency penalty** and **Presence penalty**. Leave a value on **Default** to use the site default. |
| **Knowledge** | A data source to answer from, and documents to upload as the task's own knowledge (when those features are on). See [Knowledge](knowledge.md). |
| **Capabilities** | Tools, tool instances, MCP connections and agent connections the AI may use (when those features are on). See [Tools and agents](tools-and-agents.md). |

## Start a workflow from an AI chat

Four events, under **AI Chat**, start a workflow when something happens in a chat. Each has one field, **Profile**: leave it on **Any profile**, or pick one profile to react only to its chats. Each event has one outcome, **Done**.

| Event | When it starts |
| --- | --- |
| **AI Chat Session Closed** | A chat ends, because the AI detected a goodbye or because the visitor stopped replying for the profile's inactivity time. |
| **AI Chat Session Field Extracted** | The AI collected a value for one of the profile's [data extraction](profiles.md#data-processing-and-metrics) fields, such as an email address. |
| **AI Chat Session All Fields Extracted** | Every data extraction field of the profile has a value. It fires once per chat. |
| **AI Chat Session Post-Processed** | The AI finished analyzing a closed chat with the profile's post-session tasks, such as a summary or a disposition. |

The events pass the chat and its profile to the workflow, so later tasks can use what was collected. The exact values each event passes are listed in the [Technical Manual](../../ai/workflows.md#chat-session-workflow-events).

## Examples

- **Send new leads to sales.** A website assistant collects a visitor's name, email and question as data extraction fields. Start a workflow with **AI Chat Session All Fields Extracted** for that profile, and add an email task that sends the values to your sales team.
- **Summarize closed chats.** Give a support profile a post-session task that writes a short summary. Start a workflow with **AI Chat Session Post-Processed** and save or send the summary.
- **Draft a reply to a form.** When a contact form is submitted, add **AI Completion using Profile** with a profile that drafts friendly replies, and send the draft to a staff member for review.
- **Tag incoming requests.** Use **AI Completion using Direct Config** with instructions such as "Answer with one word: Sales, Support or Billing", then route the request on the answer.

For contact center events and tasks, see [Contact Center Workflows](../workflows.md).
