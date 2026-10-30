---
sidebar_label: Speaking Style, Guardrails and Privacy
title: Speaking Style, Guardrails and Privacy
description: How speaking styles are added to the system message, where guardrails check messages on each channel, and how storage modes and retention are applied.
user_manual:
  - user-manual/ai/speaking-style-guardrails-and-privacy
---

# Speaking Style, Guardrails and Privacy

How to use these in the admin is in the User Manual under [Speaking style, guardrails and privacy](../user-manual/ai/speaking-style-guardrails-and-privacy.md).

The runtime of these features lives in CrestApps.Core: see [Speaking styles and guardrails](https://core.crestapps.com/docs/core/ai-speaking-styles-and-guardrails) and [Conversation privacy](https://core.crestapps.com/docs/core/ai-conversation-privacy). This page covers what the Orchard Core modules add.

## Speaking styles

A speaking style is a System Prompt template (`AIProfileTemplate` with the `SystemPrompt` source) whose category is `AISpeakingStyles.Category` ("Speaking style"). The AI module calls `AddCoreAISpeakingStyles()`, which registers Core's `AISpeakingStyleCompletionContextBuilderHandler` and its eight default styles. So styles get the template admin, the **AIProfileTemplate** recipe step and the AI profile template deployment step for free. None are listable, so they stay out of the prompt template picker.

The `AISystemPromptTemplateDefaultsMigrations` migration of the AI module runs `IAISystemPromptTemplateSeeder.SeedAsync()` once per tenant. It saves each default speaking style and guardrail topic that no saved template has the name of yet, so an admin can edit or delete them. They are seeded only there, so a deleted default stays deleted.

The **Speaking Style** card of the profile editor (`AIProfileSpeakingStyleDisplayDriver`) lists the category's templates with `ITemplateService.ListOptionsByCategoryAsync` and saves the chosen identifiers in `AIProfileSpeakingStyleSettings.Styles`. **Artificial Intelligence > Speaking Styles** lists the category's templates.

## Guardrails

The AI module calls `AddCoreAIGuardrails()`, which registers Core's `AIGuardrailService` and its eight default topics. The **Guardrails** card of the profile editor (`AIProfileGuardrailDisplayDriver`, in AI Chat) edits `AIProfileGuardrailSettings`. **Artificial Intelligence > Guardrail Topics** lists the topic templates.

Every verdict is passed to each `IAIGuardrailHandler`. The AI module's `WorkflowAIGuardrailHandler` starts the **AI Guardrail Triggered** event with the verdict as `Guardrail` and its fields, plus the conversation input when an `AIConversationScope` is current.

Where the checks run:

| Channel | Customer | Reply |
| --- | --- | --- |
| Web chat | Core's `AIChatHubCore.ValidatePromptSecurityAsync`, after Prompt Security. The caller is shown the profile's placeholder reply. | Core's `AIChatHubCore.ValidateOutputAsync`, after the output security filter. The caller is shown the placeholder reply. |
| SMS | `SmsOmnichannelEventHandler`, on the customer's pending texts joined, before the completion. | The same handler, before the reply is sent. |
| Turn-based voice | `VoiceAgentConversationLoop.CompleteAsync`, on the caller's last line. | The same method, before the reply is stored and spoken. |
| Realtime voice | `RealtimeVoiceConversationRunner`, on each caller transcript, in a shell scope of its own beside the call. A refusal adds a system note not to act on the request. A reply still in flight is cleared from the line and kept out of the transcript, and the placeholder is asked for in the conversation (a note, then a response with no instructions of its own); a reply already finished is left. | Not checked: the audio is played as it is generated. |

A refused customer message skips the completion, so no tool runs. A refused reply keeps what the model decided (a handoff or the end of the call) and replaces only its words. On a realtime session the model answers before the transcript exists, so a tool it called for a refused request may already have run.

## Privacy

The AI module calls `AddCoreAIConversationPrivacy()`. The **Data Privacy** card of the profile editor (`AIProfileDataPrivacyDisplayDriver`, in AI Chat) edits `AIProfileDataPrivacySettings` and sets `StorageModeSinceUtc` when the mode changes.

`AIConversationPrivacyBackgroundTask` runs every 10 minutes. For each chat profile it calls `AIConversationPrivacyService.ProcessAsync(profile, cursor)`, which applies the storage mode to the conversations that ended since the cursor and deletes those past the retention. The task keeps each profile's cursor in the `AIConversationPrivacyState` document, forgets it when `ProcessAsync` returns none, and commits.

Two `IAIConversationPrivacyHandler`s carry the data Core does not own:

- `AIConversationEvaluationPrivacyHandler` (AI module) gives the evaluations' summaries and issue quotes to the personal data search, redacts or clears each summary, criterion reason, issue detail and quote, and deletes the evaluations with the conversation.
- `AIChatSessionExtractedDataPrivacyHandler` (Chat Analytics) does the same for the conversation's extracted data record.
