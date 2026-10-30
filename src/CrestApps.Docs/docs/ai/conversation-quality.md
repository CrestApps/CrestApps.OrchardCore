---
sidebar_label: Conversation Quality
title: AI Conversation Quality
description: Profile versions, conversation evaluations, simulated test conversations, traffic splits and metric alerts, and the services and records behind them.
user_manual:
  - user-manual/ai/conversation-quality
---

# AI Conversation Quality

This page describes the services and records behind testing and monitoring AI conversations. How to use them in the admin is in the User Manual under [Test and monitor AI conversations](../user-manual/ai/conversation-quality.md).

## Profile versions

`AIProfileVersionHandler`, an `ICatalogEntryHandler<AIProfile>` in the AI module, numbers each save of a profile:

- `CreatingAsync` and `UpdatingAsync` set `AIProfileVersionMetadata.Version` on the profile to one more than the highest version kept.
- `CreatedAsync` and `UpdatedAsync` save an `AIProfileVersion` document: the profile's `Clone()`, the number, the time, the signed-in user and an optional note.

A caller that wants a note on the next version sets `AIProfileVersionMetadata.PendingNote` before saving. The handler moves it to the version and clears it from the profile before the profile is stored. A restore uses it to write *Restored from version N*.

`DefaultAIChatSessionManager.NewAsync` copies the profile's `AIProfileVersionMetadata` to the new session, so each conversation records the version that answered it. The **Chat sessions** report data set reads it as `ProfileVersion`.

Versions are kept in the AI collection, indexed by `AIProfileVersionIndex` (`ProfileId`, `Version`, `CreatedUtc`) and read through `IAIProfileVersionStore`. `AIProfileVersionsController` lists, compares and restores them. A restore copies the version's settings, `Properties` and `Settings` onto the profile and keeps its `ItemId`, `Name`, `Type`, `OwnerId` and `CreatedUtc`.

## Evaluations

`IAIConversationEvaluator` scores a list of `AIConversationTurn`s against criteria with the utility deployment (falling back to the chat deployment), using structured output. `AIConversationEvaluator.ToEvaluation` normalizes the judge's answer:

- the score is clamped to 0..100;
- a criterion the judge did not answer counts as not met;
- an issue kind outside `AIConversationIssueKinds` becomes `Other`;
- `Passed` is true when the score reaches `PassingScore` and every criterion is met.

`AIConversationEvaluationService` loads a session's turns and the profile's rendered instructions, evaluates, and saves the `AIConversationEvaluation` through `IAIConversationEvaluationStore`. The evaluation records the profile version: the session's for a stored conversation, the profile's for a simulated one. Evaluations are indexed by `AIConversationEvaluationIndex` (`SessionId`, `ProfileId`, `Label`, `Score`, `Passed`, `Resolved`, `IssueCount`, `IssueKinds`, `EvaluatedUtc`), which the **Conversation evaluations** report data set and the alert metrics read.

The **Evaluate AI Conversation** and **Simulate AI Conversation** tasks raise `AIConversationEvaluatedEvent` with `Evaluation`, `SessionId`, `ProfileId`, `Score`, `Passed` and `Label` in the input.

## Simulated conversations

`AIConversationSimulator` plays a profile against a customer played by the utility deployment:

1. It opens an `AIConversationScope` with `IsSimulation = true` and the given variables, and builds the profile's completion context, so Liquid in the system message renders with the variables.
2. The customer model gets the scenario as its system message and the conversation with the roles turned around. It says `[END]` when it is done.
3. The profile answers through `IAICompletionService` with the profile's context, so its tools and knowledge are used.

`AIConversationWorkflowInput` passes `Simulation` to every conversation workflow, so a tool workflow can return test data instead of acting.

## Metrics and alerts

`AIConversationMetrics.MeasureAsync` measures an `AIConversationMetric` over a window:

| Metrics | Read from |
| --- | --- |
| `Conversations`, `ResolutionRate`, `AverageHandleTimeSeconds`, `AverageResponseLatencyMs`, `NegativeFeedbackRate` | `IAIChatSessionEventStore` (AI Chat Session Analytics) |
| `AverageQAScore`, `QAFailureRate`, `ToolFailureRate`, `HallucinationRate` | `IAIConversationEvaluationStore` |
| `AICalls`, `AICallHandoffRate`, `AICallFailureRate`, `AverageTimeToFirstAudioMs` | `IAIVoiceSessionSummaryStore` |

A metric with nothing to measure returns a `null` value and a sample size of zero.

The **Check AI Conversation Metric** task keeps the open alerts in the `AIConversationAlertState` document, keyed by workflow type and activity, so **Alert started** and **Resolved** are each taken once per change. A window measured over fewer samples than the minimum neither opens nor resolves an alert.

## Call outcomes

`AIVoiceSessionOutcome.Transferred` marks an AI call the AI transferred to a phone number. The loop puts an `AICallTransfer` on the activity when it transfers, and `AIVoiceSessionOutcomes.ForTurnBased` reads it. The handoff rate metric counts both `HandedToAgent` and `Transferred`.
