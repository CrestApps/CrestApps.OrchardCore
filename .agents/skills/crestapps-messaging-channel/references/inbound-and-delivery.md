# Inbound messages, channel rules and delivery receipts

## The inbound path (what you build vs. what you reuse)

```
provider webhook ──► verify signature ──► normalize to OmnichannelMessage ──► durable provider inbox
                                                                                    │
                                   (retried from storage on failure, deduped on provider message id)
                                                                                    ▼
                                                              your IProviderWebhookInboxHandler
                                                                                    │
                                                                                    ▼
                                             IMessagingInboundProcessor.ProcessAsync(message)   ◄── reuse
                                                                                    │
      find/create conversation (locked per channel+endpoint+contact) → IMessagingInboundHandler.ReceivingAsync
      → routing chain (auto-reply, existing thread, endpoint routing, fallback) → first-response SLA
      → IMessagingInboundHandler.ReceivedAsync → roll-up → save message → SignalR notification (badges, list)
```

You build only the part above the processor.

### 1. The webhook endpoint

- A minimal-API endpoint in the channel module (see `TelnyxSmsWebhookEndpoint`). Anonymous, but **verify the
  provider signature** before reading anything (HMAC/Ed25519 per provider). Reject with 401/403 on failure.
- Parse the provider payload into a small typed record in the `*.Core` project (unit-test the parser with recorded
  payloads). Map the provider's delivery statuses to `MessageDeliveryStatus`
  (`Queued`, `Sent`, `Delivered`, `Failed`, `Undelivered`).
- Respond fast (200) once the delivery is committed to the inbox; do the work asynchronously.

### 2. The durable provider inbox

- Commit via `IProviderWebhookInbox.AcceptAsync(new ProviderWebhookInboxDelivery { ProviderName, DeliveryId, HandlerName, Payload })`:
  `ProviderName` is the provider (e.g. `"SendGrid"`), `DeliveryId` the provider's own message id (the idempotency
  key), `HandlerName` your inbox handler's stable `TechnicalName` (e.g. `"email-inbound"`), and `Payload` the
  serialized normalized `OmnichannelMessage`. The inbox needs the `ContactCenterConstants.Feature.ProviderInbox`
  feature — add it to your manifest dependencies.
- Implement `IProviderWebhookInboxHandler` (`TechnicalName`, `ReplaySafety`, `HandleAsync(payload)`): deserialize the
  message, save it in `OmnichannelConstants.CollectionName` if the automated path also needs it, and call
  `IMessagingInboundProcessor.ProcessAsync(message)`. Reference: `SmsInboundInboxHandler` (which raises the shared
  `SmsReceived` Omnichannel event so AI automation also sees the text; a new channel may call the processor directly).
- **Propagate** concurrency and lock failures — the inbox retries only when the handler throws. Swallowing them drops
  the message.
- Register the handler: `services.AddScoped<IProviderWebhookInboxHandler, TInboxHandler>();`.

### 3. The normalized message

```csharp
new OmnichannelMessage
{
    Channel = OmnichannelConstants.Channels.Email,   // your channel Name
    ServiceAddress = "support@contoso.com",          // our endpoint address (the processor normalizes it)
    CustomerAddress = "ann@example.com",             // the customer (normalized by the processor)
    Content = plainTextBody,                         // plain text; strip HTML/quoted replies for email
    IsInbound = true,
    CreatedUtc = providerTimestampUtc,
    ProviderMessageId = providerId,
};
```

The processor returns `null` (and stores nothing) when no enabled channel has that name or no endpoint matches the
service address, and yields to an active automated (AI) activity for that contact on that endpoint.

## Channel-only rules: `IMessagingInboundHandler`

Use an inbound handler for rules that belong to your channel alone; return early for other channels
(`context.Channel.Name`).

- `ReceivingAsync` runs **before routing**. Set `context.SuppressAutomatedReplies = true` when the message already got
  the one reply it is owed (compliance keyword, consent confirmation) so the auto-reply stays silent.
- `ReceivedAsync` runs **after routing, before save**. Change `context.Conversation` (close it, reopen it), update
  the contact (opt-out flags), send compliance confirmations straight through your provider (not the conversation
  service, which refuses opted-out contacts).
- Register with `services.AddScoped<IMessagingInboundHandler, THandler>();`.

Reference: `SmsKeywordInboundHandler` (STOP/START/HELP).

## Delivery receipts

When the provider posts status callbacks:

```csharp
await conversationService.ApplyDeliveryReceiptAsync(new MessageDeliveryReceipt
{
    Channel = OmnichannelConstants.Channels.Email,
    ServiceAddress = from,
    ContactAddress = to,
    ProviderMessageId = providerId,
    Status = MessageDeliveryStatus.Delivered,
    ErrorCode = errorCode,
}, cancellationToken);
```

Resolve `IMessagingConversationService` with `GetService` (optional) inside provider modules that can run without the
workspace, as the Telnyx webhook does. The receipt matches the message by provider id first, then the newest
outbound message without an id.

## Outbound

You do not write an outbound pipeline. The workspace calls `IMessagingChannel.SendAsync` for agent replies, new
conversations, broadcasts and auto-replies, records `MessageDispatchResult.ProviderMessageId`, and retries refused
messages from the outbox on a backoff schedule. Return `Failed` (don't throw) for a refusal.

## AI hand-off

`MessagingAgentHandoffService` accepts hand-offs for **every** enabled messaging channel (it resolves the channel
from the activity's `Channel`). If AI automation runs on your channel, hand-off to a human works with no extra code.
