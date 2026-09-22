---
sidebar_label: Realtime Voice
sidebar_position: 19
title: Realtime Voice (Speech-to-Speech)
description: Run live, spoken AI conversations over a provider realtime session, with a WebRTC transport, automatic WebSocket fallback, and audio-only chat UI.
---

# Realtime Voice (Speech-to-Speech)

A realtime-capable model can hold a live, spoken conversation — audio in, audio out — while still honoring the profile's system message, tools, and data sources. The whole turn runs through a provider **realtime session** driven by the realtime orchestrator, so replies begin while the user is still finishing their sentence and the user can interrupt (barge-in).

One chat thread carries both kinds of turn. A surface in **Conversation** chat mode shows a voice toggle
*beside* an ordinary message box: start a session and speak, end it and type, and both land in the same
thread. Choosing a speech-to-speech model no longer turns the surface voice-only.

## Prerequisites

1. **A realtime deployment.** Either of:
   - **A provider's own speech-to-speech model.** Create an **AI → Deployment** whose model supports speech-to-speech and, on its **Model capabilities** card, enable the **Realtime (speech-to-speech)** feature. See [Model Capabilities](model-capabilities.md).
   - **A cascaded realtime deployment**, when no provider you use ships a speech-to-speech model. See [Cascaded realtime](#cascaded-realtime-when-no-provider-speaks) below.

   Optionally set the site's default realtime deployment under **Settings → Artificial Intelligence → Default
   Deployments**.
2. **A surface in conversation mode.**
   - **AI Chat session** — set the profile's **Chat mode** to **Conversation**. A **Conversation deployment**
     picker appears beside it; leave it empty to inherit the site default.
   - **Chat interactions** — an interaction has no chat mode of its own, so set chat interactions to
     **Conversation** site-wide and name the **Conversation deployment** on the interaction. Naming one is
     itself how that interaction asks to speak.

## Conversation mode and the realtime slot

A surface holds a spoken conversation when its chat mode is **Conversation**. How that conversation is carried
is resolved, not stored:

1. the **conversation deployment** named on the profile or the chat interaction, if any;
2. otherwise the site's default realtime deployment;
3. otherwise the first deployment whose model declares the `realtime` capability.

If none of those answers, the conversation falls back to the client-driven **speech-to-text plus
text-to-speech cascade**, which needs both a speech-to-text and a text-to-speech deployment configured for the
site. Failing that it degrades to microphone dictation, and failing that to plain typing.

Because step 3 accepts *any* realtime-capable deployment, an installation that has one will use it for
conversation mode even when nothing is named and no site default is set. There is deliberately no per-profile
switch back to the cascade: the transport is not a stored property of a profile or an interaction. To keep the
cascade, have no deployment declare `realtime`; to speak without a speech-to-speech model, use a
[cascaded realtime deployment](#cascaded-realtime-when-no-provider-speaks).

A deployment named as the conversation deployment that cannot serve realtime is reported rather than quietly
replaced, so a typo does not silently move the conversation to a different model.

The **Chat deployment** means only "the text model this profile talks to". It answers typed messages,
including those typed during a voice conversation, so its picker lists text-capable deployments only.

:::note
A profile written before the conversation deployment existed named its speech-to-speech model as its chat
deployment. Nothing is rewritten on disk: when such a profile is read, that model is shown as its
**Conversation deployment** and the chat mode as **Conversation**, and saving the profile persists that shape.
No migration is required.
:::

## What changes in the UI

In conversation mode the chat surface keeps everything it had and adds a voice toggle:

- Between sessions the message box has the row, with **Send** beside it and the soundwave toggle on the right.
- While a session runs, the message box and **Send** give way to the voice settings and the **End
  Conversation** button takes the width they leave. Sending a typed message ends the session first, so the two
  never overlap.
- The dictation microphone is hidden while a realtime session carries the conversation: the session already
  owns the microphone.
- A **Voice** picker appears beside the conversation deployment, populated from the resolved realtime model's
  own voices — or from the text-to-speech deployment's when the conversation runs as the cascade.
- A short settings popover (from the shared realtime audio controller) exposes only per-device preferences — microphone, speaker, assistant volume, language, **Allow interruptions** (barge-in), and **Push-to-talk** — saved per browser. Everything acoustic (echo margins, the microphone gate, turn-detection timing) is measured automatically; there are no acoustic knobs to tune.

The realtime experience is delivered by the `@crestapps/ai-chat-ui` package (the vendored `realtime-audio.js` controller plus the `ai-chat.js` / `chat-interaction.js` apps); no additional page script is required.

## Cascaded realtime: when no provider speaks

Most providers do not ship a speech-to-speech model. A **cascaded realtime deployment** produces the same
experience by chaining three deployments you already have:

```
mic ──▶ speech-to-text ──▶ chat (tools, data sources) ──▶ text-to-speech ──▶ speaker
```

Nothing above it knows the difference: the profile, the chat UI, transcripts, and turn persistence behave
exactly as they do with a provider's own realtime model. The three deployments may come from different
providers — transcribe with one, reason with another, speak with a third.

### Creating one

1. Create the three deployments it will chain, if you do not have them already:
   - a **speech-to-text** deployment whose model declares the **Realtime** feature (it has to transcribe
     continuously, not file-at-a-time),
   - a **chat** deployment,
   - a **text-to-speech** deployment.
2. Go to **AI → Deployments → Create** and choose the **Cascaded Realtime** provider.
3. Name the deployment, then pick the three deployments on the **Cascaded realtime** card.

The deployment declares the **Realtime** feature for you — a cascade is realtime by construction — so it
appears wherever a realtime deployment is offered. It owns no connection of its own; the three deployments
it names carry the credentials.

Name it as a profile's **Conversation deployment** exactly as you would a native realtime model. The **Voice**
picker lists the voices of the text-to-speech deployment, since that is the one that speaks.

### What to expect

- **More delay before the assistant starts speaking.** A turn passes through three services rather than one.
  The reply is spoken a sentence at a time so audio begins before the model has finished writing, but the
  first word still arrives later than it would from a native speech-to-speech model.
- **Interruption is driven by transcription.** When the user talks over the assistant, the first partial
  transcript cancels the reply and drops the audio already buffered — so barge-in responds as quickly as the
  transcribing model reports speech.
- **Everything else is unchanged.** Tools, data sources, and the profile's instructions are applied to the
  chat deployment, so a cascaded voice profile keeps every capability a text profile has.

### In a recipe

```json
{
  "name": "aideployment",
  "deployments": [
    {
      "Name": "cascaded-voice",
      "ClientName": "CascadedRealtime",
      "Properties": {
        "AIDeploymentMetadata": {
          "Features": [ "realtime" ]
        },
        "CascadedRealtimeMetadata": {
          "SpeechToTextDeploymentName": "scribe",
          "ChatDeploymentName": "gpt-4o",
          "TextToSpeechDeploymentName": "eleven-tts"
        }
      }
    }
  ]
}
```

## Transports: WebRTC with WebSocket fallback

Realtime audio is carried over **WebRTC** when available and over **WebSocket** (PCM over SignalR) otherwise. The application selects between them automatically at connect time — there is no user-facing transport switch — and a post-connect drop simply ends the session rather than migrating.

WebRTC is the preferred transport because it couples playback with the browser's acoustic echo canceller, which matters in open rooms (external speakers + an open mic). The browser peers with the application's own hub (not the model provider), so it works with any realtime provider.

### Enabling WebRTC

The WebRTC transport ships in the `CrestApps.Core.AI.Realtime.WebRtc` package and is registered by the AI Chat and AI Chat Interactions features. When registered, the realtime hubs advertise WebRTC to the browser; when it cannot connect (blocked UDP, no TURN, unsupported browser), realtime still works over the WebSocket transport. This is a deployment decision, not a user setting — turn it off (or configure TURN) through the options below.

## Configuration: turn detection, idle timeout, STUN/TURN

Realtime transport options bind to the `OrchardCore:CrestApps:AI:RealtimeTransport` configuration section:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "RealtimeTransport": {
          "EnableWebRtc": true,
          "TurnDetectionType": "semantic_vad",
          "TurnDetectionEagerness": "auto",
          "IdleTimeoutSeconds": 30,
          "MaxSessionDurationSeconds": 300,
          "StunUrls": [ "stun:stun.l.google.com:19302" ]
        }
      }
    }
  }
}
```

| Property | Purpose |
| --- | --- |
| `EnableWebRtc` | Whether WebRTC is offered to browsers (default `true`). Turn it off on hosts with no inbound UDP and no reachable TURN relay, otherwise every session waits out the connect timeout before falling back. |
| `TurnDetectionType` | `semantic_vad` (default) lets the model decide when the user has finished; `server_vad` ends the turn after a fixed silence. A deployment that rejects semantic detection is switched to server VAD automatically. |
| `TurnDetectionEagerness` | For `semantic_vad`: `low`, `medium`, `high`, or `auto` (default). Lower waits longer for the user to continue. |
| `IdleTimeoutSeconds` | How long a session may go without user speech before it ends (default `30`). A realtime session holds an open, billed provider connection whether or not anyone is talking. |
| `MaxSessionDurationSeconds` | Hard ceiling on a single session's length, regardless of activity (default `300`). |
| `EnableKnowledgeGrounding` | Whether the server retrieves knowledge for a turn before asking the model to reply, instead of letting the model answer immediately (default `true`). |
| `GroundingAcknowledgementDelayMs` | How long retrieval may run before the caller hears one short spoken filler sentence ("let me look that up") so the wait is not silent (default `700`). |
| `GroundingResponseWatchdogSeconds` | How long to wait for the grounded reply before answering anyway rather than leaving the turn unanswered (default `15`). |
| `StunUrls` | STUN server URLs. Defaults to a public server when empty. |
| `TurnUrls` | TURN server URLs (`turn:`/`turns:`). Required for users behind strict/symmetric NATs or blocked UDP. |
| `TurnSecret` / `TurnCredentialTtlSeconds` | coturn `use-auth-secret` shared secret and TTL (default `3600`); enables short-lived ephemeral TURN credentials (recommended for production). |
| `TurnUsername` / `TurnCredential` | Static TURN credentials, used only when `TurnSecret` is unset. |
| `IceTransportPolicy` | `All` (default) lets the browser try direct connectivity first; `Relay` forces every candidate through TURN. |

STUN enables direct connectivity through most home/office NATs. A **TURN** server is required where traffic must be relayed; without it, those users fall back to WebSocket. ICE servers are fetched per session (over the hub), so ephemeral TURN credentials are always fresh.

The section is read through `IShellConfiguration`, so it is a **per-tenant** setting and each tenant can point realtime at its own STUN and TURN servers. A tenant that declares nothing inherits the host's values, and a tenant that declares `StunUrls` or `TurnUrls` replaces the host's list rather than appending to it.

:::note
The `OrchardCore` wrapper above is how the host `appsettings.json` is shaped — Orchard Core reads its own configuration from that section. A tenant that overrides the values in its own `App_Data/Sites/{tenant}/appsettings.json` writes the same keys **without** the wrapper, starting at `CrestApps`, because that file is already scoped to the tenant.
:::

### Cloudflare Realtime TURN

Running a TURN server is not the only option. Cloudflare Realtime mints short-lived TURN credentials through an API, so nothing long-lived is handed to the browser and nothing has to be rotated by hand. Cloudflare exposes no shared secret, so the credentials cannot be signed locally the way coturn's `use-auth-secret` allows — they are issued by an API call authenticated with a TURN Token ID and API token:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "RealtimeTransport": {
          "Cloudflare": {
            "TokenId": "<turn-token-id>",
            "ApiToken": "<api-token>",
            "TtlSeconds": 86400
          }
        }
      }
    }
  }
}
```

| Property | Purpose |
| --- | --- |
| `TokenId` | The **TURN Token ID** shown in the Cloudflare dashboard. |
| `ApiToken` | The API token issued alongside it. Both are long-lived and belong in a secret store rather than a configuration file checked into source control. |
| `TtlSeconds` | Lifetime requested for each set of credentials (default `86400`). Credentials are replaced halfway through, so the value only needs to comfortably exceed the longest call — it is not a session limit; that is `MaxSessionDurationSeconds`. |

This is read through `IShellConfiguration` like the rest of the section, so each tenant can mint credentials from its own Cloudflare account. While a tenant supplies no `TokenId` and `ApiToken`, realtime falls back to the `StunUrls` and `TurnUrls` configured above, so a tenant running its own coturn — or none at all — is unaffected until it opts in. The same fallback covers a Cloudflare outage that leaves no credentials to serve.

## How it runs (for the curious)

Realtime turns are dispatched through `IRealtimeOrchestrator` rather than the standard orchestrator, so tool calling, system-prompt injection, data sources, and turn persistence all apply to the spoken conversation. The server owns the session lifecycle and reports it to the browser over a single hub event (`session_ready`, `speech_started`, `playback_flush`, `user_turn_pending`, `session_ended`, …). Transcripts and errors stay on SignalR; the WebRTC path carries audio only.

## Related

- [Model Capabilities](model-capabilities.md) — declaring the `realtime` feature on a deployment.
- [AI Chat](chat.md) and [AI Chat Interactions](chat-interactions.md) — the two chat surfaces that support realtime.
