---
sidebar_label: Conversation Workflows
title: AI Conversations and Workflows
description: The conversation scope, conversation variables, the AI Conversation Starting and AI Tool Called events, the Workflow tool source, and the inputs AI conversation workflows receive.
user_manual:
  - user-manual/ai/conversation-workflows
---

# AI Conversations and Workflows

Website chats, automated text threads and AI phone calls are all AI conversations. This page describes how code and workflows take part in them. How to build the workflows in the admin is in the User Manual under [Automate AI conversations](../user-manual/ai/conversation-workflows.md).

## The conversation scope

`AIConversationScope` (in `CrestApps.OrchardCore.AI.Core.Conversations`) holds the `AIConversationContext` of the conversation the current code runs for. It flows with the async call, so a tool the model calls, the system message rendered while the request is built, and a workflow a tool starts all read the same conversation.

| Property | Meaning |
| --- | --- |
| `Channel` | `Chat`, `Sms`, `Phone` or `Email` (`AIConversationChannels`) |
| `Direction` | `Inbound` or `Outbound` (`AIConversationDirections`) |
| `Session` | The `AIChatSession`, when it exists |
| `Profile` | The `AIProfile` answering |
| `ActivityId` | The omnichannel activity, for calls and texts |
| `Contact` | The contact content item, when known |
| `CustomerAddress`, `BusinessAddress` | The customer's number and ours |
| `Values` | Extra template values: `Activity`, `Subject`, `Campaign`, `Endpoint` |

Text threads and phone calls (both the turn-based engine and the realtime engine) begin a scope for each turn or call with `IOmnichannelConversationContextFactory`. A website or admin chat has no channel scope; `AIConversationScope.Resolve()` then describes it from the chat session in `AIInvocationScope`.

## Conversation variables

Variables are text values kept on the session as `AIConversationVariablesMetadata`. Before a session exists (an outbound call not yet dialed), they are kept on the activity and carried into the conversation from there.

```csharp
session.SetConversationVariable("tier", "gold");
var tier = session.GetConversationVariables()["tier"];
```

They are set by the **Set Conversation Variables** task, by a starting handler, by the **Create AI Activity** task and by data extraction (each extracted field is also stored as a variable).

## Liquid in the system message

`AIConversationLiquidCompletionContextBuilderHandler` renders the composed system message as Liquid when it contains Liquid markup. The template values are `Variables`, `Conversation` (an `AIConversationTemplateModel`), `Session`, `Profile`, `Contact` and each entry of `Values`. A template that fails to render is logged and used as written.

## Tools on automated conversations

Automated text threads and turn-based calls call the completion service directly. `AIConversationTools.AttachAsync` gives them the profile's tools from `IToolRegistry` (tools, tool instances, MCP connections and agents) plus the channel's system tools (end call, transfer to a person), the same set the orchestrator offers a chat.

`AIConversationToolParameterContextResolver` fills tool-instance parameters bound to these context keys:

| Key | Value |
| --- | --- |
| `conversation.channel`, `conversation.direction`, `conversation.session_id` | The conversation |
| `conversation.customer_address`, `conversation.business_address` | The customer's number and ours |
| `activity.id`, `contact.id`, `contact.name` | The records the conversation is about |
| `variables.<name>` | Any conversation variable |

## Before the conversation starts

`IAIConversationStartingService` runs every `IAIConversationStartingHandler` before a conversation's first message, within a budget of 3 seconds for inbound conversations and 30 seconds for outbound ones. Each handler adds to an `AIConversationStartingResult`: `Variables`, a `ProfileId`, or `Declined` with a `DeclineReason`. Failures and timeouts are logged and the conversation starts anyway.

It runs for inbound AI calls before the call is answered, inbound texts to an AI number before the conversation is created, automated activities before they are placed (by the background pass and by **Place Call or Send Message**), and website chats before their first answer (variables only).

The Workflows feature registers a handler that triggers the **AI Conversation Starting** event (`AIConversationStartingEvent`). Its tasks write to the result in `Workflow.Input.StartingResult`.

## Workflow tools

The **Workflow** tool-instance source (`WorkflowToolConstants.SourceName`) runs a workflow type that starts with the **AI Tool Called** event (`AIToolCalledEvent`). The function:

1. resolves the instance's declared parameters with `AIToolParameterBinder` (an instance without parameters exposes one `request` string);
2. starts the workflow in the current request with `IWorkflowManager.StartWorkflowAsync`, correlated to the session;
3. returns the workflow's `Output` as JSON, `{"status":"accepted"}` when the workflow halted on a blocking activity, or `{"status":"error","message":...}` when it faulted.

## Workflow input

Every AI conversation workflow receives `AIConversationWorkflowInput.Build(conversation)`:

| Key | Value |
| --- | --- |
| `Channel`, `Direction`, `SessionId`, `ProfileId`, `ActivityId` | The conversation |
| `ContactId`, `ContactName`, `Contact` | The contact |
| `CustomerAddress`, `BusinessAddress` | The numbers |
| `Variables` | The conversation variables |
| `Arguments`, `ToolName` | **AI Tool Called** only |
| `StartingResult` | **AI Conversation Starting** only |

The AI session events (**Field Extracted**, **Closed**, **Post-Processed**) also carry `Variables`, and **Closed** and **Post-Processed** carry `Transcript` (one `Customer:` or `Assistant:` line per turn) and `Turns` (`AIConversationTurn` records), built by `AIConversationTranscript`.
