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

A speaking style is a prompt template whose category is `AISpeakingStyles.Category` ("Speaking style"). The AI module ships eight in its `Templates/Prompts` folder (`AISpeakingStyles.BuiltIn`, such as `speaking-style-echo-verification.md`); more are saved as System Prompt templates (`AIProfileTemplate` with the `SystemPrompt` source) in that category, which `ITemplateService` lists with the module's under their technical name. So styles get the template admin, the **AIProfileTemplate** recipe step and the AI profile template deployment step for free. None are listable, so they stay out of the prompt template picker.

`AIProfileSpeakingStyleSettings.Styles` holds the chosen template identifiers, in the order the editor lists them. `ITemplateService.ListOptionsByCategoryAsync` (in `AITemplateCategoryExtensions`) lists a category's templates by title for an editor.

`AISpeakingStyleCompletionContextBuilderHandler` renders the chosen templates with `ITemplateService` in `BuiltAsync` and appends them to the system message in that order. A template that is gone or fails to render is left out and logged.

## Guardrails

`AIProfileGuardrailSettings` holds `CheckCustomer`, `CheckReplies`, `Topics`, `OtherTopics` and `PlaceholderReply`. A topic is a prompt template whose category is `AIGuardrailTopics.Category` ("Guardrail topic"): its title names the topic and its rendered text is the definition. The AI module ships eight (`AIGuardrailTopics.BuiltIn`, such as `guardrail-topic-violence.md`), and saved System Prompt templates in the category join them, with their recipe and deployment steps.

`AIGuardrailService.CheckAsync(profile, text, direction, sessionId)` reads the chosen topics with `LoadTopicsAsync` (a template that is gone or no longer in the category is skipped), asks the utility deployment, with the usage purpose `Guardrail`, whether the text falls under one of them, and returns an `AIGuardrailVerdict` or `null`. The verdict's `Category` is the topic's template identifier, or `other`, and `Topic` its title. A topic the profile did not choose is allowed unless the profile has `OtherTopics`. A failure to reach the model is logged and allows the text. A verdict is passed to every `IAIGuardrailHandler`; the workflow handler starts the **AI Guardrail Triggered** event with the verdict as `Guardrail` and its fields, plus the conversation input when an `AIConversationScope` is current.

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
