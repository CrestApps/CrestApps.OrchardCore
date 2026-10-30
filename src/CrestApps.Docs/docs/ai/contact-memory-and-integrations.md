---
sidebar_label: Contact Memory
title: Contact Memory and Signed Webhooks
description: How contact memory is stored, read into the system message and rewritten, and how the Send Signed Webhook task signs its requests.
user_manual:
  - user-manual/ai/contact-memory-and-integrations
---

# Contact Memory and Signed Webhooks

How to use these in the admin is in the User Manual under [Contact memory and integrations](../user-manual/ai/contact-memory-and-integrations.md).

## Contact memory

`AIContactMemory` keeps one note per contact (`ContactId`, `Text` of at most 2,000 characters, `UpdatedUtc`, `LastSessionId`) in the AI collection, indexed by `AIContactMemoryIndex.ContactId` and read through `IAIContactMemoryStore`. It is a record of its own rather than a part on the contact, so a rewrite after each conversation does not create a content item version.

`AIContactMemoryService`:

- `GetAsync` and `SetAsync` read and replace the note; an empty note deletes it.
- `RememberAsync(contactId, turns, sessionId)` asks the utility deployment to rewrite the note from the current note and the conversation, and saves it.
- `RememberSessionAsync(profile, session, prompts)` does that only when the profile's `AIProfileContactMemorySettings.SaveMemory` is on and the session carries an `AIConversationContactMetadata`.

`OmnichannelConversationContextFactory` puts `AIConversationContactMetadata` (`ContactId`, `ActivityId`) on the session the first time it builds a conversation context for an activity with a contact. The voice loop and the SMS handler save the session with each turn, so the session keeps it.

`AIChatSessionCloseBackgroundTask` calls `RememberSessionAsync` for each session it closes, before raising **AI Chat Session Closed**. A failure is logged and does not stop the close.

`AIContactMemoryCompletionContextBuilderHandler` appends the note to the system message in `BuiltAsync` when the profile's `UseMemory` is on and `AIConversationScope.Resolve()` has a contact.

`OmnichannelContactMemoryDisplayDriver` adds the note to the editor of every content type with `OmnichannelContactPart`, and saves it only when it changed.

## Send Signed Webhook

`SendSignedWebhookTask` posts the rendered body with `X-Webhook-Id`, `X-Webhook-Event`, `X-Webhook-Timestamp` and `X-Webhook-Signature`. The signature is the commerce webhooks' scheme: `v1=` and the lowercase hex HMAC-SHA256 of `{timestamp}.{body}` keyed with the secret.

- The secret is stored protected with the data protector purpose `CrestApps.OrchardCore.AI.SendSignedWebhookTask`, so it is tied to the tenant's keys and can't be moved with a recipe.
- A request is retried on 408, 429, 5xx and network errors, up to `MaxAttempts` (1 to 5), 1, 3, 9 and 27 seconds apart. Other responses fail at once.
- The named HTTP client `CrestApps.SignedWebhook` has a 15 second timeout.
