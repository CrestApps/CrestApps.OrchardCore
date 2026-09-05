---
sidebar_label: Realtime Voice
sidebar_position: 19
title: Realtime Voice (Speech-to-Speech)
description: Run live, spoken AI conversations over a provider realtime session, with a WebRTC transport, automatic WebSocket fallback, and audio-only chat UI.
---

# Realtime Voice (Speech-to-Speech)

A realtime-capable model can hold a live, spoken conversation — audio in, audio out — while still honoring the profile's system message, tools, and data sources. Unlike the `Conversation` chat mode (which chains speech-to-text → chat → text-to-speech), realtime runs the whole turn through a provider **realtime session** driven by the realtime orchestrator, so replies begin while the user is still finishing their sentence and the user can interrupt (barge-in).

## Prerequisites

1. **A realtime deployment.** Create an **AI → Deployment** whose model supports speech-to-speech and, on its **Model capabilities** card, enable the **Realtime (speech-to-speech)** feature. See [Model Capabilities](model-capabilities.md). Optionally set the site's default realtime deployment.
2. **Chat mode set to Realtime.** A realtime model only runs a realtime session when the chat mode is **Realtime**:
   - **AI Chat session** — set the profile's chat mode to *Realtime (speech-to-speech)* on the profile editor. The option appears once a realtime deployment exists.
   - **Chat interactions** — set the site chat mode to *Realtime* under the AI settings, or select a realtime deployment on the interaction (a realtime-capable deployment forces realtime for that interaction).

If a profile or interaction is set to Realtime but no realtime deployment is available, it falls back to the `Conversation` pipeline when speech-to-text and text-to-speech deployments exist, otherwise to plain text.

## What changes in the UI

When realtime is active, the chat surface becomes audio-only:

- The text input is hidden and a **Start speaking** button is shown. Press it and talk; press again to end the session.
- On the chat interaction editor a **Voice** picker appears, populated from the selected realtime model's voices.
- A short settings popover (from the shared realtime audio controller) exposes only per-device preferences — microphone, speaker, assistant volume, language, **Allow interruptions** (barge-in), and **Push-to-talk** — saved per browser. Everything acoustic (echo margins, the microphone gate, turn-detection timing) is measured automatically; there are no acoustic knobs to tune.

The realtime experience is delivered by the `@crestapps/ai-chat-ui` package (the vendored `realtime-audio.js` controller plus the `ai-chat.js` / `chat-interaction.js` apps); no additional page script is required.

## Transports: WebRTC with WebSocket fallback

Realtime audio is carried over **WebRTC** when available and over **WebSocket** (PCM over SignalR) otherwise. The application selects between them automatically at connect time — there is no user-facing transport switch — and a post-connect drop simply ends the session rather than migrating.

WebRTC is the preferred transport because it couples playback with the browser's acoustic echo canceller, which matters in open rooms (external speakers + an open mic). The browser peers with the application's own hub (not the model provider), so it works with any realtime provider.

### Enabling WebRTC

The WebRTC transport ships in the `CrestApps.Core.AI.Realtime.WebRtc` package and is registered by the AI Chat and AI Chat Interactions features. When registered, the realtime hubs advertise WebRTC to the browser; when it cannot connect (blocked UDP, no TURN, unsupported browser), realtime still works over the WebSocket transport. This is a deployment decision, not a user setting — turn it off (or configure TURN) through the options below.

## Configuration: turn detection, idle timeout, STUN/TURN

Realtime transport options bind to the `CrestApps:AI:RealtimeTransport` configuration section:

```json
{
  "CrestApps": {
    "AI": {
      "RealtimeTransport": {
        "EnableWebRtc": true,
        "TurnDetectionType": "semantic_vad",
        "TurnDetectionEagerness": "auto",
        "IdleTimeoutMinutes": 10,
        "StunUrls": [ "stun:stun.l.google.com:19302" ]
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
| `IdleTimeoutMinutes` | How long a session may go without user speech before it ends (default `10`; `0` disables). A realtime session holds an open, billed provider connection whether or not anyone is talking. |
| `StunUrls` | STUN server URLs. Defaults to a public server when empty. |
| `TurnUrls` | TURN server URLs (`turn:`/`turns:`). Required for users behind strict/symmetric NATs or blocked UDP. |
| `TurnSecret` / `TurnCredentialTtlSeconds` | coturn `use-auth-secret` shared secret and TTL; enables short-lived ephemeral TURN credentials (recommended for production). |
| `TurnUsername` / `TurnCredential` | Static TURN credentials, used only when `TurnSecret` is unset. |

STUN enables direct connectivity through most home/office NATs. A **TURN** server is required where traffic must be relayed; without it, those users fall back to WebSocket. ICE servers are fetched per session (over the hub), so ephemeral TURN credentials are always fresh.

## How it runs (for the curious)

Realtime turns are dispatched through `IRealtimeOrchestrator` rather than the standard orchestrator, so tool calling, system-prompt injection, data sources, and turn persistence all apply to the spoken conversation. The server owns the session lifecycle and reports it to the browser over a single hub event (`session_ready`, `speech_started`, `playback_flush`, `user_turn_pending`, `session_ended`, …). Transcripts and errors stay on SignalR; the WebRTC path carries audio only.

## Related

- [Model Capabilities](model-capabilities.md) — declaring the `realtime` feature on a deployment.
- [AI Chat](chat.md) and [AI Chat Interactions](chat-interactions.md) — the two chat surfaces that support realtime.
