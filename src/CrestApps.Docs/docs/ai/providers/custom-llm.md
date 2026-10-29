---
sidebar_label: Custom LLM (WebSocket)
sidebar_position: 6
title: Custom LLM over a WebSocket
description: Answer AI turns from your own server over a WebSocket, for a model or agent that does not speak an OpenAI-compatible API.
---

| | |
| --- | --- |
| **Feature Name** | Custom LLM (WebSocket) |
| **Feature ID** | `CrestApps.OrchardCore.AI.CustomLlm` |

Use this provider when the model or agent that should answer runs on your own server and does not expose an OpenAI-compatible API. Each turn is sent to your server over a WebSocket, and its reply is streamed back to the chat, the text conversation or the phone call. If your server does speak the OpenAI API, use the OpenAI provider with its address instead.

## Set it up

1. Enable **Custom LLM (WebSocket)**.
2. Under **Artificial Intelligence → Provider Connections**, add a **Custom LLM (WebSocket)** connection. Its **Endpoint** is your server's address (`wss://`, or `https://`, which is used as `wss://`), and its **API key**, when set, is sent as `Authorization: Bearer` when the socket opens.
3. Add a deployment on that connection. Its model name is sent with each request, so one server can host several agents.
4. Choose the deployment as the chat deployment of an AI profile.

## The protocol

For each turn, the site opens a socket, sends one `request` message, and reads messages until `done` or `error`. Every message is a JSON text frame.

```json
{"type":"request","model":"collections-agent","conversationId":"4d2dz2gjsyhd22s2xanjna934k","messages":[{"role":"system","content":"You are..."},{"role":"user","content":"Can I pay next week?"}],"temperature":0.3,"maxOutputTokens":400}
```

Your server answers with any number of `delta` messages, then `done`:

```json
{"type":"delta","content":"Yes, "}
{"type":"delta","content":"I can set that up for you."}
{"type":"done","usage":{"inputTokens":812,"outputTokens":11}}
```

`usage` is optional; when given, it is counted in AI usage. Send `{"type":"error","message":"..."}` to fail the turn. If nothing arrives for 30 seconds, the turn fails. `messages` holds the whole conversation so far, including the system message with the profile's instructions; `role` is `system`, `user` or `assistant`. `conversationId` is the AI session, so your server can keep its own state per conversation.

The provider answers chat turns only: tools the profile has are not sent to your server, and embeddings, images and speech come from other providers.
