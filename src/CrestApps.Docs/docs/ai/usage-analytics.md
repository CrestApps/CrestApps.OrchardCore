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
| **Feature Name** | AI Usage Analytics |
| **Feature ID** | `CrestApps.OrchardCore.AI.UsageAnalytics` |
| **Page** | **Reports → Billing & Usage → AI Usage Analytics** (`/Admin/AI/UsageAnalytics/Index`) |
| **Permission** | `ViewChatAnalytics` |

The AI Usage Analytics page reports what AI cost a tenant, in three parts:

- **Metered usage** — every request sent to an AI provider, of every kind and from every feature: chat
  completions, embeddings, images, speech-to-text, text-to-speech and realtime voice, with every unit the
  provider bills in.
- **Completions** — the chat completions that belong to a chat session or a chat interaction: tokens, latency and
  the deployment, model and connection each one ran on.
- **Voice** — one summary per automated AI voice call: how long the caller heard the assistant, how long the caller
  spoke, how long neither did, how the call ended, and which model held it.

All three are captured only while the **AI Usage Analytics** feature is enabled; the report and its menu entry
come with it. Nothing used before the feature was enabled can be reported.

Running the report and reading its figures is described in the User Manual under
[AI Usage Analytics](../user-manual/ai/analytics.md#ai-usage-analytics). This page covers what is recorded
and how each figure is measured.

## Filters

| Filter | Applies to | What it does |
| --- | --- | --- |
| **Date range** | All | The dates to report on, picked from presets such as Today or Last 30 Days, or set as a custom range, in the site's time zone. Completions are matched on when they were recorded; voice calls on when their summary was written, which is when the assistant's part of the call ended. |
| **AI profile** | All | Limits the report to one chat profile. Metered requests that belong to no profile, such as indexing, are left out while a profile is chosen. |
| **Group metered usage by** | Metered usage | **Model**, **Model and category** or **Model and purpose**. |
| **Group audio usage by** | Audio usage | **Model**, **Model and voice** or **Model and language**. |
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

## Audio usage

The **Audio usage** table reports speech-to-text and text-to-speech on their own, with the units audio models are
billed in. It is built from the same metered requests, so it needs no extra setup.

| Column | Meaning |
|---|---|
| **Requests** | Speech-to-text or text-to-speech requests |
| **Hours** | Total length of the audio. Speech-to-text runs to where the last recognized speech ended. Text-to-speech length is known when the provider reports it or the audio is WAV |
| **Speech hours** | Speech-to-text length without the silence before the first word |
| **Characters** | Characters synthesized by text-to-speech |
| **Audio in**, **Audio out** | Size of the audio sent for transcription and of the audio generated |
| **Segments** | Pieces of text a transcription returned |
| **Tokens** | Tokens, for models that bill audio by token |

**Group audio usage by** is **Model**, **Model and voice** or **Model and language**. An asterisk next to the
hours means some requests did not report an audio length, so the figure is a minimum. Each request also keeps the
sample rate, audio format, speed, pitch and target language, for reporting outside this page.

Requires the CrestApps.Core release that records audio usage details.

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
| **Idle prompts** | Times the session asked whether the caller was still there because the line had been quiet on both sides: at most two in a row, after which the next silence ends the call. | The "are you still there?" lines it said. |
| **Echo held** | Caller audio heard while the assistant was speaking that the echo guard sent to the model as silence. | Not applicable. |
| **Answer latency** | For each answer, the time from the caller's voice stopping on the line to the assistant's first audio: the turn detector's wait plus the model's time to first audio. The call keeps the median, 90th and 95th percentiles and the longest. | For each answer to a caller's line, the time from the line being taken to the provider's *speak started*: the model's completion plus the start of speech. A voicemail message is not counted. |
| **Model latency** | From the provider taking the caller's turn as finished to the model's first audio. Median per call. | The completion's time. Median per call. |
| **Turn detection wait** | From the caller's voice stopping on the line to the provider taking the turn as finished, where the voice was heard. Median per call. | Not measurable. |

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

## Report Builder data source

<YouTube video="ai-usage-analytics" title="Video of the AI chat data sets in the Report Builder" start={299} end={345} />

The usage records are also two data sets of the **AI chat** data source (technical name `AIChat`), so the [Report Builder](../modules/report-builder/index.md) can build your own usage and cost reports from them, such as the cost per model per month or the tokens each profile used per day. The same source has the **Chat sessions** (`ChatSessions`) and **Chat session metrics** (`ChatSessionMetrics`) data sets.

| | |
| --- | --- |
| **Data source** | `AIChat` |
| **Registered when** | **Data Pipelines Core** (`OrchardCore.DataPipelines.Core`) and **AI Chat Session Analytics** (`CrestApps.OrchardCore.AI.Chat.Analytics`) are enabled. That feature indexes the usage records, so the two data sets are listed only while it is on. |
| **Permission** | **View AI Chat Analytics** (`ViewChatAnalytics`), the permission of the AI Usage Analytics page. |

| Data set | Technical name | Rows | Main date |
| --- | --- | --- | --- |
| **AI completion usage** | `CompletionUsage` | One row for each request sent to an AI provider, of every kind and from every feature: the rows of the metered usage table, not only the completions the completion table counts. | `CreatedUtc` |
| **AI voice usage** | `VoiceUsage` | One row for each AI voice session summary. | `CreatedUtc` |

Both data sets are filled only while **Enable AI usage tracking** is on. A usage record has no success flag: a request is recorded when the provider answered it. A voice summary has no user, because the caller is not a user of the site.

### AI completion usage fields

| Group | Fields |
| --- | --- |
| **Request** | `CreatedUtc` (when the request was recorded), `OperationType` (the kind: `Chat`, `Embedding`, `Image`, `SpeechToText`, `TextToSpeech`, `Realtime` or `RealtimeTranscription`; a request recorded before kinds were recorded is `Chat`), `ContextType` (the category), `Purpose`, `IsStreaming`, `ResponseLatencyMs` |
| **Model** | `ClientName` (the provider), `ConnectionName`, `DeploymentName`, `ModelName` (the model the provider reported, empty when it reported none), `Model` (the model the provider reported, or the deployment when it reported none) |
| **Context** | `ProfileId` (identifier), `ProfileName` (the current name of the AI profile), `SessionId` (identifier, references `AIChat.ChatSessions.SessionId`), `InteractionId` (identifier), `UserId` (identifier, references `Users.UserId`), `UserName`, `IsAuthenticated` |
| **Billing units** | `InputTokenCount`, `OutputTokenCount`, `TotalTokenCount`, `CachedInputTokenCount`, `ReasoningTokenCount`, `InputAudioTokenCount`, `OutputAudioTokenCount`, `AudioDurationMs`, `AudioMinutes`, `CharacterCount`, `ImageCount` |

The client identifier, the visitor identifier and the provider's response identifier are not exposed.

### AI voice usage fields

| Group | Fields |
| --- | --- |
| **Call** | `ItemId` (identifier), `ActivityId` (identifier, references `Omnichannel.Activities.ItemId`), `AISessionId` (identifier, references `AIChat.ChatSessions.SessionId`), `Channel`, `ChannelEndpointId` (identifier), `ProviderName` (the telephony provider), `CampaignId` (identifier, references `Omnichannel.Campaigns.ItemId`), `CampaignName`, `Engine` (`Realtime` or `TurnBased`), `Outcome`, `StartedUtc`, `EndedUtc`, `CreatedUtc` (when the summary was written) |
| **Model** | `AIProfileId` (identifier), `AIProfileName` (the name of the profile when the call ended), `DeploymentName`, `ModelName`, `ConnectionName` |
| **Talk time** | `SessionDurationMs`, `SessionSeconds`, `SessionMinutes`, `AssistantSpeakingMs`, `AIMinutes`, `CallerSpeakingMs`, `CallerMinutes`, `MutualSilenceMs`, `SilenceMinutes`, `TimeToFirstAssistantAudioMs`, `EchoHeldMs` |
| **Turns** | `AssistantTurns`, `CallerTurns`, `BargeIns`, `IdlePrompts` |
| **Tokens** | `InputAudioTokens`, `OutputAudioTokens`, `InputTextTokens`, `OutputTextTokens`, `CachedInputTokens` |
| **Latency** | `ResponseLatencyP50Ms`, `ResponseLatencyP90Ms`, `ResponseLatencyP95Ms`, `ResponseLatencyMaxMs`, `ModelLatencyP50Ms`, `DetectorWaitP50Ms`, `LatencySamples` (the answers measured), in the **Talk time** group |
| **Cost** | `EstimatedCost`, `CostCurrency`, in the **Call** group |

A measurement that the engine of the call cannot make is empty, as described in [How each metric is measured](#how-each-metric-is-measured). The provider's call identifier is not exposed. The names keep at most 255 characters, as the index does. The AI Usage Analytics page counts one summary per activity; in the rare case that a call has two summaries, count the distinct `ActivityId` values to count calls.

### Cost per call

Each AI call's summary carries an estimated cost when prices are set under **Settings > Artificial Intelligence**, in **Costs**:

- the **Currency**;
- the carrier's price of a **Phone call, per minute** and of a **Text message**;
- the price of each **AI deployment**, one per line: `name | input | output | audio input | audio output | per minute`, with token prices per million tokens. Use **per minute** for a model billed by the minute.

The cost is the call's minutes at the carrier's price, plus its minutes or tokens at its deployment's prices. It is written when the call ends, with the prices of that moment, so a price change applies to later calls only. It is an estimate: to match an invoice, see [Turning usage into cost](#turning-usage-into-cost).

### Cost per model

To estimate the cost of each model, group **AI completion usage** by `Model` (and `OperationType` when a model is billed for more than one kind of request), and add a [calculated field](../modules/report-builder/formulas.md) that multiplies each billing unit by the provider's price for it. For example, with prices per million tokens, and `Usage` as the alias of the data set in the report:

```text
IF([Usage.Model] = 'gpt-4o', ([Usage.InputTokenCount] - [Usage.CachedInputTokenCount]) * 2.50 / 1000000 + [Usage.CachedInputTokenCount] * 1.25 / 1000000 + [Usage.OutputTokenCount] * 10.00 / 1000000, 0)
```

Add the field as a measure with the **Sum** aggregate, and add one such term for each model you use.

Use `AudioMinutes` for speech-to-text models billed by the minute, `CharacterCount` for text-to-speech models billed by the character, and `ImageCount` for image models billed by the image. To match the invoice instead of the list prices, see [Turning usage into cost](#turning-usage-into-cost).

Each data set reads its newest rows first, a page at a time, and narrows the read to the date range of the report's filters on `CreatedUtc`, to the join keys of its identifier fields, and to the values the filters allow for the category, provider, connection, deployment and model (and for the voice usage, the channel, engine and outcome). A report that only groups and adds up the fields the index holds is calculated in the database.

## Related

- [AI Chat Session Analytics](chat-analytics.md) — per-session conversation metrics for chat.
- [Realtime Voice](realtime-voice.md) — the speech-to-speech sessions the voice figures are measured on.
