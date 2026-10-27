---
sidebar_label: Speaking Style, Guardrails and Privacy
title: Speaking Style, Guardrails and Privacy
description: How speaking styles are added to the system message, where guardrails check messages on each channel, and how storage modes and retention are applied.
user_manual:
  - user-manual/ai/speaking-style-guardrails-and-privacy
---

# Speaking Style, Guardrails and Privacy

How to use these in the admin is in the User Manual under [Speaking style, guardrails and privacy](../user-manual/ai/speaking-style-guardrails-and-privacy.md).

## Speaking styles

`AIProfileSpeakingStyleSettings.Styles` holds the chosen styles from `AISpeakingStyles`. Each style is a prompt template in the AI module's `Templates/Prompts` folder named `speaking-style-` and the style, such as `speaking-style-echo-verification.md`, so the wording is read and changed without editing code. The templates are not listable, so they don't show in the prompt template picker.

`AISpeakingStyleCompletionContextBuilderHandler` renders the chosen templates with `ITemplateService` in `BuiltAsync` and appends them to the system message in the order of `AISpeakingStyles.All`. A template that fails to render is left out and logged.

## Guardrails

`AIProfileGuardrailSettings` holds `CheckCustomer`, `CheckReplies`, `Categories` (from `AIGuardrailCategories`), `OtherTopics` and `PlaceholderReply`.

`AIGuardrailService.CheckAsync(profile, text, direction, sessionId)` asks the utility deployment, with the usage purpose `Guardrail`, whether the text falls under one of the profile's topics, and returns an `AIGuardrailVerdict` or `null`. A topic the profile did not choose is allowed unless the profile has `OtherTopics`. A failure to reach the model is logged and allows the text. A verdict is passed to every `IAIGuardrailHandler`; the workflow handler starts the **AI Guardrail Triggered** event with the verdict as `Guardrail` and its fields, plus the conversation input when an `AIConversationScope` is current.

Where the checks run:

| Channel | Customer | Reply |
| --- | --- | --- |
| Web chat | `AIChatHub.ValidatePromptSecurityAsync`, after Prompt Security. A refusal is returned as a blocked `PromptSecurityResult`, and `GetPromptBlockedMessage` returns the placeholder reply. | `AIGuardrailOutputSecurityFilter`, which replaces the registered `IOutputSecurityFilter` and runs `DefaultOutputSecurityFilter` first. `GetOutputBlockedMessage` returns the placeholder reply. |
| SMS | `SmsOmnichannelEventHandler`, on the customer's pending texts joined, before the completion. | The same handler, before the reply is sent. |
| Turn-based voice | `VoiceAgentConversationLoop.CompleteAsync`, on the caller's last line. | The same method, before the reply is stored and spoken. |

A refused customer message skips the completion, so no tool runs. A refused reply keeps what the model decided (a handoff or the end of the call) and replaces only its words. Realtime voice sessions are not checked.

## Privacy

`AIProfileDataPrivacySettings` holds `StorageMode` (`Everything`, `RedactPersonalData` or `MetadataOnly`) and `RetentionDays`.

`AIConversationPrivacyBackgroundTask` runs every 10 minutes. For each chat profile:

- With a storage mode other than `Everything`, it walks the profile's sessions by `AIChatSessionIndex.LastActivityUtc` from a cursor kept per profile in the `AIConversationPrivacyState` document, 50 at a time, leaving sessions alone until 15 minutes after their last activity. It stops at the first session still active or with post-session processing pending, and at the first failure, so no session is passed over. The cursor starts at `StorageModeSinceUtc`, which the profile editor sets when the mode changes (or when the task first sees the mode, for a profile changed another way), so sessions whose last activity was before are not changed.
- With `RetentionDays`, it deletes up to 50 sessions whose last activity is older, through `AIConversationPrivacyService.DeleteAsync`.

`AIConversationPrivacyService.ApplyAsync(session, mode)` does nothing to a session that carries `AIConversationPrivacyMetadata`, and puts it there when done. It saves through `IAIChatSessionStore` so the last activity is not moved.

- `RedactPersonalData` collects the title, prompts, post-session results, extracted values, evaluation texts and the Chat Analytics extracted data record, asks the utility deployment (usage purpose `PersonalDataRedaction`) for the names, street addresses, dates of birth and identity numbers in them, and replaces those and the pattern matches of `AIPersonalDataRedactor` (email addresses, Luhn-valid card numbers, US social security numbers, and phone numbers of 10 to 15 digits, or 8 or more after `+`) everywhere.
- `MetadataOnly` deletes the prompts and clears the title, post-session result values, extracted values and evaluation texts.
- Both clear `RemoteAddress`. The address hash is kept for rate limits.

`DeleteAsync(sessionId)` deletes the session's evaluations, its Chat Analytics extracted data record (only when Chat Analytics is on, through `AIConversationPrivacyOptions.IncludeExtractedDataRecords`), its prompts and the session, and runs the `IAIChatSessionHandler` deleting and deleted handlers, which remove its documents. It finds the session through `IAIChatSessionStore` rather than `IAIChatSessionManager`, which deletes only the signed-in user's own sessions.
