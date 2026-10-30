---
sidebar_label: AI Profile Builder
sidebar_position: 4
title: AI Profile Builder
description: Build an AI profile by role-playing a conversation and giving feedback on replies; the AI rewrites its own instructions until it behaves as intended.
user_manual:
  - user-manual/ai/profile-builder
---

| | |
| --- | --- |
| **Feature Name** | AI Profile Builder |
| **Feature ID** | `CrestApps.OrchardCore.AI.Chat.Interactions.ProfileBuilder` |
| **Depends on** | AI Chat Interactions |
| **Permissions** | Manage AI profiles, and edit chat interactions |

How to use the builder in the admin is described in the User Manual under [Profile Builder](../user-manual/ai/profile-builder.md).

This video shows why the Profile Builder exists, how it works, and how to build, test and save a profile with it.

<YouTube video="ai-profile-builder" title="Video overview of the AI Profile Builder" />

## Overview

A conversation-driven AI profile, such as one that runs automated SMS conversations or phone calls, behaves only as well as its system message. Writing that message by hand means guessing at every situation, testing, and editing again. The AI Profile Builder turns this into a loop:

1. Role-play the person the assistant will talk to.
2. Under a reply you do not like, write what was wrong.
3. A coach model rewrites the instructions from your comment, keeping every earlier fix, and can regenerate the reply with them.
4. Replay every message you commented on to check that nothing regressed.
5. Save the result as an AI profile.

## How it is built

A builder session is a **chat interaction** marked with `ProfileBuilderInteractionMetadata`, plus a `ProfileBuilderSession` document kept beside it in the `AI` collection (`ProfileBuilderSessionIndex`).

- **The conversation** is the chat interaction. Role-play therefore uses the chat interaction hub unchanged: streaming, tools, data sources, documents, the speech-to-text and text-to-speech conversation mode, and realtime voice. Realtime turns are stored as chat interaction prompts, so they can receive feedback like typed turns.
- **The ledger** is the session document. It holds every piece of feedback (the user message, the reply, the conversation before it, the comment, and whether the reply was regenerated), every revision of the instructions, and the last replay. It is kept out of the chat interaction because the settings panel saves the whole interaction through the hub, and a ledger stored there would be lost whenever an autosave raced a feedback request.
- **The page** reuses the chat interaction editor. The controller sets a `ChatInteractionChatClientOverrides` request feature that gives the chat client a message template with **Feedback** and **Looks good** under each reply. The panel script (`profile-builder.js`) drives the chat client's own Vue instance: it flushes pending settings saves before feedback, writes the rewritten values back into the settings panel, and sends a user message again to regenerate a reply.

Builder sessions are left out of the **Chat Interactions** list.

## The coach

Feedback other than an approval is sent to the coach: the deployment chosen on the session, or the interaction's chat deployment when none is chosen. The coach's system prompt is the `profile-builder-coach` template in `Templates/Prompts`. Its request carries:

- the current system message, opening message and parameters,
- the conversation as it stands,
- every earlier piece of feedback, oldest first, with each rejected or approved reply quoted,
- the revision history, and
- the new feedback.

The coach returns JSON with the complete system message and, when the feedback calls for it, new values for the opening message, temperature, top-p, frequency and presence penalties, maximum tokens and past message count. Values outside the ranges the settings panel accepts are clamped. Each answer becomes a revision with the coach's summary.

Because the whole history is in every request, a fix found on the fourth try at a message is written so that the first three mistakes stay fixed too.

## Regenerating a reply

**Feedback & regenerate** is offered for the latest reply only. After the rewrite, the reply and the message it answered are deleted from the conversation, and the client sends that message again, so the new reply is generated without the rejected one in its history. The rejected reply stays in the ledger.

In a voice conversation the reply has already been spoken, so regenerate is not offered. A realtime session keeps the instructions it started with; end and restart it to use new ones.

## Replay

Replay groups feedback by situation (the same user message at the same point in a conversation), answers each situation again with the current settings, and asks the `profile-builder-replay-judge` template to mark each answer **Pass**, **Fail** or **Unknown** against all the feedback given on it. At most 20 situations are replayed, the most recent ones. Tools are not offered during a replay, so replaying a conversation never repeats an action such as sending a message.

## Simulation

`ProfileBuilderSimulator.RunRoundAsync` runs one round of the session's `SimulationSettings`: each scenario through `AIConversationSimulator` (the customer on `CustomerDeploymentName`, the assistant on a profile built from the linked profile, or a new one, with `ProfileBuilderProfileMapper.CopyToProfile`, so the linked profile's knowledge and tools take part), judged by `IAIConversationEvaluator` on `JudgeDeploymentName`. Results are kept in `ProfileBuilderSession.Simulations` (the most recent 60).

When a conversation fails and `Improve` is on, the lowest-scoring one becomes a `ProfileBuilderFeedback` whose comment lists every failed conversation's score, summary, problems and unmet criteria, anchored on the assistant line a problem quotes (or the last reply). The coach rewrites the settings from it as a revision with the source `Simulation`, so the feedback ledger, **Replay** and later rewrites all see it.

Each round is one request (`POST Simulate?runId=`), so no request runs for minutes; the page posts again with the run until the response says `finished`: every conversation passed, the rounds ran out, or the coach could not rewrite. After each round the page writes the new settings into the settings panel, so its autosave keeps them.

## Saving the result

- **Create AI profile** creates a chat profile with the session's system message, opening message (`AIProfileMetadata.InitialPrompt`), parameters, chat and utility deployments, orchestrator, tools, agents, MCP and agent-to-agent connections, tool instances, data source and retrieval settings, model parameters and prompt templates. A conversation deployment sets the profile's chat mode to conversation with the same deployment and voice.
- **Update the AI profile** writes the same values onto the profile the session was started from, or last saved to.
- **Copy system message** copies the current system message.

Documents attached to the chat interaction are not copied to the profile.

## Usage labels

Coach calls are recorded under the usage purpose `ProfileBuilderCoaching`, and replay and judge calls under `ProfileBuilderReplay`, so they can be told apart from conversation traffic in [AI Usage Analytics](usage-analytics.md).
