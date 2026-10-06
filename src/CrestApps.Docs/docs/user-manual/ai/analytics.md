---
sidebar_label: Analytics
title: AI Chat and Usage Analytics
description: See how your AI assistants are used, whether they solve people's problems, what users think of them, what they collect, and how many tokens and minutes they use.
technical_manual:
  - ai/chat-analytics
  - ai/usage-analytics
---

The AI reports answer the questions a manager asks about an assistant: How many people use it? Does it solve their problem? Do they like the answers? What details did it collect? And what does it cost? Use them to decide where an assistant needs better instructions or knowledge, and to plan your AI budget.

| | |
| --- | --- |
| **Menu** | Artificial Intelligence > Reports |
| **Permission** | View AI Chat Analytics (Export AI Chat Analytics to download CSV files) |
| **Feature** | AI Chat Session Analytics |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of enabling session metrics and running the AI chat analytics report">
  <source src="/img/docs/ai-chat-analytics.mp4" type="video/mp4" />
</video>

| Report | What it shows |
| --- | --- |
| **AI Chat Session Analytics** | How chats with your assistants go: volume, resolution, timing, ratings and token use. |
| **AI Chat Extracted Data** | The details an assistant collected in each chat, one row per chat. |
| **AI Chat Conversion Goals** | How each chat scored against the assistant's goals. |
| **AI Usage Analytics** | Tokens used by every AI request, and the minutes of automated AI phone calls. |

## Collect the data

The reports only contain what was collected, and only from the moment collection was turned on.

- **For the chat reports**, open each chat profile under **Artificial Intelligence > Profiles**, go to the **Data Processing & Metrics** tab and tick **Enable session metrics**. Tick **Enable AI resolution detection** to have the AI judge whether each chat solved the problem, and **Enable conversion metrics** to score chats against goals. Data extraction and conversion goals are set on the same tab; see [AI profiles](profiles.md#data-processing-and-metrics).
- **For the usage report**, an administrator ticks **Enable AI usage tracking** in the **General** section of **Settings > Artificial Intelligence**. While it is off, the report shows a warning.

When session metrics are on, every answer in the chat gets **Thumbs up** and **Thumbs down** buttons, and people's ratings feed the **User Feedback** section.

A chat counts as finished when it ends naturally or when nobody writes for the profile's **Session inactivity timeout**. Resolution, conversion and extracted data appear only after a chat has finished and been analyzed, which can take a few minutes.

## AI Chat Session Analytics

1. Open **Artificial Intelligence > Reports > AI Chat Session Analytics**.
2. Choose the dates in **From** and **To**, and optionally one profile (or **Any profile**).
3. Click **Show**.

To download the chats behind the report, click **Export as CSV**.

### Conversation & Usage

| Figure | What it means |
| --- | --- |
| **Total Sessions** | Chats started in the period. |
| **Unique Visitors** | Different people who chatted, by user account or visitor. |
| **Containment Rate** | Share of chats the assistant handled without a person stepping in. Higher is better. |
| **Abandonment Rate** | Share of chats where the person left without a resolution. A high rate can mean unhelpful answers. |
| **Avg Session Duration** | Average time from the first to the last message. |
| **Avg Messages/Session** | Average number of messages, from both sides, per chat. |
| **Returning User Rate** | Share of people who came back for another chat. |
| **Avg Steps to Resolve** | Average number of messages needed in resolved chats. Fewer is faster. |
| **Resolved Sessions** / **Abandoned Sessions** / **Active Sessions** | Chats that ended resolved, ended by inactivity without a resolution, or are still going. |

### Usage by Time of Day and Usage by Day of Week

When people chat, by hour and by weekday. Use them to see peak times, for example to have staff ready when a live hand-off is likely.

### User Segmentation

How many chats came from signed-in users (**Authenticated Sessions**, **Unique Logged-in Users**) and from anonymous visitors (**Anonymous Sessions**, **Unique Anonymous Visitors**).

### Model & System Performance

| Figure | What it means |
| --- | --- |
| **Avg Response Latency** | How long the AI takes to write an answer, in milliseconds. |
| **Total Tokens Used** | All tokens used: **Input Tokens** (what was sent to the AI, including instructions and history) plus **Output Tokens** (what the AI wrote). Tokens are what AI providers charge for. |
| **Avg Tokens/Session**, **Avg Input Tokens/Session**, **Avg Output Tokens/Session** | Tokens per chat. Useful to estimate the cost of one conversation. High input tokens can mean long instructions or long histories. |

Some AI services do not report tokens for every answer, so these figures can be lower than your bill.

### Resolution & Conversion

| Figure | What it means |
| --- | --- |
| **Resolution Rate** | Share of finished chats that the AI judged as solved, from the conversation itself. Needs **Enable AI resolution detection**. |
| **Resolved** / **Unresolved** | The number of chats judged solved or not. Read some unresolved chats to find gaps in the assistant's knowledge. |
| **Avg Conversion Score** | The average score against your conversion goals, as a share of the best possible score. Needs **Enable conversion metrics**. |
| **Evaluated Sessions** | Chats that were scored. |
| **High Performing** / **Low Performing** | Share of scored chats at 70% or more, and below 30%. |

### User Feedback

| Figure | What it means |
| --- | --- |
| **Positive Ratings** / **Negative Ratings** | Chats rated thumbs up or thumbs down. A person rates the whole chat; their latest rating counts. |
| **Satisfaction Rate** | Share of rated chats with a thumbs up. |
| **Feedback Rate** | Share of chats that were rated at all. |

## AI Chat Extracted Data

Lists the details an assistant collected with data extraction, such as names, emails and products of interest, one row per chat.

1. Open **Artificial Intelligence > Reports > AI Chat Extracted Data**.
2. Pick the **AI Profile** (required), and optionally **From** and **To**.
3. Click **Show**. The table has **Session date**, **Session ID** and one column per extracted detail.
4. Click **Export as CSV** to download it, for example to import leads into another system.

## AI Chat Conversion Goals

Shows how each finished chat scored against the profile's conversion goals.

1. Open **Artificial Intelligence > Reports > AI Chat Conversion Goals**.
2. Pick the **AI Profile** (required), and optionally **From** and **To**.
3. Click **Show**. The table has **Session date**, **Session ID**, **Total points** and one column per goal.
4. Click **Export as CSV** to download it.

## AI Usage Analytics

Shows what AI used across the whole site: every completion from chats and chat interactions, and the automated AI phone calls.

1. Open **Artificial Intelligence > Reports > AI Usage Analytics**.
2. Choose **From**, **To**, and optionally an **AI profile**.
3. Choose how to group the figures:
   - **Group completions by**: **User and model**, **Model**, **Deployment**, **AI profile** or **Connection**.
   - **Group voice calls by**: **Deployment (model)**, **AI profile**, **Campaign**, **Channel**, **Engine** or **Day**.
4. Click **Show**.

The top tiles show **Provider calls**, **Chat sessions**, **Chat interactions** and **Total tokens**. The table shows, per group, the **Calls**, **Sessions**, **Interactions**, **Input**, **Output** and total **Tokens**, and the **Avg latency**.

The **Voice** section covers automated AI phone calls:

| Figure | What it means |
| --- | --- |
| **Calls** | Automated calls in the period. |
| **AI minutes** | Minutes the caller heard the assistant speak. |
| **Caller minutes** | Minutes the caller spoke, on calls where this can be measured. |
| **Silence minutes** | Minutes nobody spoke, on the same calls. |
| **Average call** | The average length of the assistant's part of a call. |
| **Handoff rate** | Share of calls handed to a live agent. |
| **Barge-ins per call** | How often callers talked over the assistant and it stopped to listen. |

Some figures cannot be measured on every kind of call and are left out rather than counted as zero. How each figure is measured is explained in the [Technical Manual](../../ai/usage-analytics.md). For the automated calls themselves, see [Automated AI SMS and Voice](../automated-ai.md).
