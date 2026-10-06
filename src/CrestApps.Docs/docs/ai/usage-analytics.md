---
sidebar_label: AI Usage Analytics
sidebar_position: 4
title: AI Usage Analytics
description: Report every metered AI request per model, category and purpose, chat completion tokens by user, model, deployment, profile and connection, and the talk time, silence and outcomes of automated AI voice calls.
user_manual:
  - user-manual/ai/analytics
---

| | |
| --- | --- |
| **Feature Name** | AI Chat Session Analytics |
| **Feature ID** | `CrestApps.OrchardCore.AI.Chat.Analytics` |
| **Page** | **Artificial Intelligence → Reports → AI Usage Analytics** (`/Admin/AI/UsageAnalytics/Index`) |
| **Permission** | `ViewChatAnalytics` |

The AI Usage Analytics page reports what AI cost a tenant, in three parts:

- **Metered usage** — every request sent to an AI provider, of every kind and from every feature: chat
  completions, embeddings, images, speech-to-text, text-to-speech and realtime voice, with every unit the
  provider bills in.
- **Completions** — the chat completions that belong to a chat session or a chat interaction: tokens, latency and
  the deployment, model and connection each one ran on.
- **Voice** — one summary per automated AI voice call: how long the caller heard the assistant, how long the caller
  spoke, how long neither did, how the call ended, and which model held it.

All three are captured only while **Enable AI usage tracking** is on, under **Settings → Artificial
Intelligence**. The page shows a warning while it is off; nothing recorded before it was turned on can be
reported.

Running the report and reading its figures is described in the User Manual under
[AI Usage Analytics](../user-manual/ai/analytics.md#ai-usage-analytics). This page covers what is recorded
and how each figure is measured.

## Filters

| Filter | Applies to | What it does |
| --- | --- | --- |
| **From** / **To** | All | The date range, in the site's time zone. Completions are matched on when they were recorded; voice calls on when their summary was written, which is when the assistant's part of the call ended. |
| **AI profile** | All | Limits the report to one chat profile. Metered requests that belong to no profile, such as indexing, are left out while a profile is chosen. |
| **Group metered usage by** | Metered usage | **Model**, **Model and category** or **Model and purpose**. |
| **Group completions by** | Completions | **User and model** (the original breakdown: user, client and model together), **Model**, **Deployment**, **AI profile** or **Connection**. |
| **Group voice calls by** | Voice | **Deployment (model)**, **AI profile**, **Campaign**, **Channel**, **Engine** (realtime or turn-based) or **Day** (the local day the call started). |

## Metered usage

Every AI client the site creates meters its own requests, whatever the provider: Azure OpenAI, OpenAI, Azure AI
Inference, Ollama, Anthropic, Azure AI Speech, or a provider added by a module. One row is written for each
request sent to the provider, so a single answer can add several: planning, rewriting the search query, each step
of a tool-calling loop, describing an image, and each batch of embeddings are each counted on their own. A
response served from a cache adds none, because nothing was billed.

Every row keeps the provider, the model and the kind of request apart, because each combination is priced on its
own meters:

| Column | Meaning |
| --- | --- |
| **Kind** | Chat, embeddings, images, speech-to-text, text-to-speech, realtime voice, or realtime transcription. |
| **Requests** | Requests sent to the provider. |
| **Input**, **Output**, **Tokens** | Tokens in, out, and in total. |
| **Cached input** | Input tokens the provider served from its prompt cache, usually billed at a discount. |
| **Reasoning** | Output tokens a reasoning model spent before answering. |
| **Audio in**, **Audio out** | Audio tokens, billed at a different rate from text tokens. |
| **Audio minutes** | Audio transcribed, for speech-to-text models billed by duration. Measured up to where the last recognized speech ended. |
| **Characters** | Characters synthesized, for text-to-speech models billed by character. |
| **Images** | Images generated, for image models billed per image. |

A unit a model is not billed in shows as a dash.

### Categories and purposes

**Model and category** splits each model by the feature that made the request:

| Category | Requests |
| --- | --- |
| **Chat sessions** | Chat with an AI profile. |
| **Chat interactions** | Chat interactions. |
| `Sms` | Automated SMS conversations. |
| `Voice` | Automated voice calls, turn-based and realtime. |
| `Api` | The AI completion API endpoints. |
| `Workflow` | AI workflow tasks. |
| `A2A` | Tasks received from other agents. |

**Model and purpose** splits each model by why the request was made. The framework's purposes are listed in
[CrestApps.Core: AI Usage Metering](https://core.crestapps.com/docs/core/ai-usage-metering#categories-and-purposes);
this site adds `ConversationConclusion` (the review that closes an SMS conversation or a call), `HandoffSummary`,
`ReplyDecision` (whether an SMS needs a reply), `ReEngagement` and `Utility`.

A module records its own category or purpose with `UseUsageLabels` when it creates the client, or with an
`AIUsageScope` around a call to a service that creates the client for it, such as `IAICompletionService`:

```csharp
var client = await clientFactory.CreateChatClientAsync(deployment, builder => builder
    .UseDefaultResilience()
    .UseUsageLabels(contextType: "Leads", purpose: "LeadScoring"));

using var usageScope = AIUsageScope.Begin(contextType: "Leads", purpose: "LeadScoring");
var completion = await completionService.CompleteAsync(deployment, messages, context);
```

### Turning usage into cost

Multiplying a row by the provider's list prices gives an estimate. To match what was actually paid, take the cost
of each billing meter from the provider's invoice for a period and divide it in proportion to the units each row
used on that meter in the same period. Discounts, credits and committed capacity are then included, and the shares
always add up to the bill.

## Completions

The completion table is the one the page always had: calls, sessions, interactions, input, output and total
tokens, and average latency for each group. Only completions that belong to a chat session or a chat interaction
are counted, and only chat completions: the embeddings, speech and realtime requests made for the same sessions
are in the metered usage table.

The review that concludes every automated call — the completion that writes its summary and chooses its
disposition — runs on the call's own chat session, so its tokens are counted with the call.

## Voice

Every automated AI voice call leaves one **AI voice session summary** when the assistant's part of it ends: at the
end of a live session, at the handoff to a person, or at the hangup. The summary records the activity, AI session,
AI profile, campaign, channel and channel endpoint, the provider call, the engine that held the call, the deployment
with the model and connection behind it, when it started and ended, and how it ended.

### Totals

| Figure | Meaning |
| --- | --- |
| **Calls** | Automated calls in the range, one per activity. |
| **AI minutes** | Minutes the caller heard the assistant speak. |
| **Caller minutes** | Minutes the caller was heard speaking, on calls whose engine can hear it. |
| **Silence minutes** | Minutes neither side was speaking, on the same calls. |
| **Average call** | The average length of the assistant's part of a measured call. |
| **Handoff rate** | The share of calls handed to a live agent. |
| **Barge-ins per call** | How often, on average, the caller talked over the assistant and it stopped, on calls that can be talked over. |

The table adds call minutes, the average wait for the assistant's first word, idle prompts, the text tokens
recorded against the calls' chat sessions, and audio tokens once they are reported.

### How each metric is measured

Everything is measured from the call's audio and the provider's voice events, never from transcript timestamps:
a transcript only exists once a turn is over, and says nothing about how long it took to say.

| Metric | Realtime (speech-to-speech) calls | Turn-based calls |
| --- | --- | --- |
| **Session duration** | From the moment the call was answered and handed to the live session until the session ended. | From the provider's answered event to the handoff or the hangup. |
| **Assistant speaking** | The playback schedule of the audio written to the line. The model delivers speech faster than it plays, so each piece is counted for the time it actually plays, back to back after what is already queued. When the caller talks over the assistant and its queued speech is cleared, whatever had not yet played is not counted. Audio still queued when the call ends is not counted. | The provider's *speak started* to *speak ended* events, on the times the provider reports them rather than when its webhooks arrived. A line whose start was never reported leaves the figure unknown rather than zero. |
| **Caller speaking** | The provider's voice-activity events: from *speech started* to the commit of the caller's turn. The commit follows the detector's end-of-turn silence, so this includes that short tail. | Not measurable: the provider only reports what the caller said once it is transcribed. Shown as unknown. |
| **Mutual silence** | The session duration minus the time either side was speaking. Overlap is counted once, so a caller talking over the assistant is neither silence nor double the talk. | Unknown, because caller speech is. |
| **Time to first assistant audio** | From the answer to the moment the assistant's first audio starts playing, including the time the session took to open. | From the answer to the provider's first *speak started*. |
| **Assistant turns / caller turns** | The lines each side said, from the stored transcript. | Same. |
| **Barge-ins** | Times the caller talked over the assistant *and* its speech was stopped: the provider reported speech while the echo guard heard a voice over the assistant. The closing line is left to finish and is not counted. | Not applicable: a turn-based call stops listening while it speaks. |
| **Idle prompts** | Times the session spoke up because the line had been quiet on both sides. | The "are you still there?" lines it said. |
| **Echo held** | Caller audio heard while the assistant was speaking that the echo guard sent to the model as silence. | Not applicable. |

Durations that an engine cannot measure are stored as unknown and left out of that figure's totals and averages,
so a turn-based call counts toward **Calls** and **AI minutes** but does not dilute caller minutes or barge-ins with
zeros it never measured.

### Outcomes

| Outcome | Meaning |
| --- | --- |
| **HandedToAgent** | The assistant transferred the caller to a live agent. |
| **CompletedByAI** | The assistant finished the conversation and ended the call. |
| **Voicemail** | The call reached a voicemail. |
| **NoAnswer** | Nobody answered. |
| **CallerHungUp** | The caller hung up before the assistant ended the conversation. |
| **Failed** | The call failed, or the live session reported an error it could not continue from, or was lost and could not be replaced. An error that only refuses one request, such as trimming a line the caller talked over, does not fail the session. |

### Tokens

- **Text tokens** are the text tokens recorded against the call's chat session: every reply of a turn-based call,
  the text part of every realtime response, and the review that concludes every call. They are joined from the
  metered records when the report is built, so they are always current.
- **Audio tokens** are the audio tokens the realtime provider reported for the call's responses and for the
  transcription of the caller's speech. A call held by the turn-based engine has none.

### Storage and limits

- Summaries are stored in the AI collection beside the completion usage records, indexed by day and by activity.
  Like the completion records, they are not aged out automatically.
- A turn-based call's measurements are held in memory between the provider events that make up the call. A call
  whose events reach more than one server is still summarized, with its audio measurements left unknown.
- A call is summarized once. When the hangup and the end of a live session race, the summary that measured the
  audio is kept.

## Related

- [AI Chat Session Analytics](chat-analytics.md) — per-session conversation metrics for chat.
- [Realtime Voice](realtime-voice.md) — the speech-to-speech sessions the voice figures are measured on.
