---
sidebar_label: Webhooks
title: Webhooks
description: The webhooks building block. The provider webhook inbox durably accepts authenticated callbacks from providers, de-duplicates redeliveries and processes them from storage with retries.
---

| | |
| --- | --- |
| **Features** | Provider Webhook Inbox (`CrestApps.OrchardCore.ContactCenter.ProviderInbox`, dependency only) |
| **Core** | `CrestApps.OrchardCore.Webhooks.Core` |
| **Abstractions** | `CrestApps.OrchardCore.Webhooks.Abstractions` |
| **Category** | Webhooks |
| **Depends on** | Nothing |

The Webhooks module owns webhooks in both directions. This page covers the inbound side: callbacks that providers
(Twilio, Telnyx, Azure Event Grid, Stripe) send to the site.

## Provider webhook inbox

Providers deliver webhooks at least once, and they deliver them again when the site is slow to answer or restarting.
An endpoint that processes a callback inline loses it when the process stops, and processes it twice when the
provider sends it again. The provider webhook inbox fixes both:

1. The endpoint authenticates the request (see [Signature validators](#signature-validators)) and parses it.
2. It calls `IProviderWebhookInbox.AcceptAsync` with the provider name, the provider's own delivery id, the name of
   the handler that processes it and the normalized payload. The delivery is committed before the endpoint answers.
   A delivery whose provider and delivery id are already stored is a `Duplicate`, on every node.
3. The endpoint answers the provider, then starts processing with `AfterResponseWork` (from
   `CrestApps.OrchardCore.Core`), so a slow handler never makes the provider send the callback again.
4. The **Provider Webhook Inbox Dispatch** background task retries every delivery whose processing did not complete,
   with exponential backoff, and dead-letters one after 10 failed attempts.

Completed deliveries are kept for at least seven days as idempotency tombstones, so a redelivery within that window is
still recognized. When Contact Center is enabled, its retention policy also purges completed and dead-lettered
deliveries, and its `WebhookInboxMessageRetentionDays` setting sets how long completed ones are kept.

The feature ID is still `CrestApps.OrchardCore.ContactCenter.ProviderInbox`, the ID it was released with inside
Contact Center, so tenants that already had it enabled keep it. The inbox no longer depends on Contact Center: an
SMS-only or payments-only tenant uses it on its own.

### Using the inbox from a module

Depend on the `WebhooksConstants.Feature.ProviderInbox` feature and register a handler:

```csharp
public sealed class MyProviderInboxHandler : IProviderWebhookInboxHandler
{
    public const string Name = "MyProvider.Event";

    public string TechnicalName => Name;

    // Delivery is at-least-once, so say how a repeat is made harmless. Unspecified is refused at startup.
    public WebhookHandlerReplaySafety ReplaySafety => WebhookHandlerReplaySafety.DeduplicatedByEventId;

    public Task HandleAsync(string payload, CancellationToken cancellationToken = default)
    {
        // Process the normalized payload the endpoint stored.
        return Task.CompletedTask;
    }
}

[RequireFeatures(WebhooksConstants.Feature.ProviderInbox)]
public sealed class InboxStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddScoped<IProviderWebhookInboxHandler, MyProviderInboxHandler>();
}
```

The endpoint then accepts each authenticated delivery:

```csharp
var acceptance = await inbox.AcceptAsync(new ProviderWebhookInboxDelivery
{
    ProviderName = "MyProvider",
    DeliveryId = providerEventId,
    HandlerName = MyProviderInboxHandler.Name,
    Payload = json,
}, context.RequestAborted);
```

Answer `2xx` for `Accepted` and `Duplicate`. For `Busy`, or when `AcceptAsync` throws, nothing was stored, so answer a
status that makes the provider send the delivery again (`409` or `503`).

### Contracts

| Contract | Where | What it is for |
| --- | --- | --- |
| `IProviderWebhookInbox` | Abstractions | Accept a delivery, dispatch one, dispatch the due ones. |
| `IProviderWebhookInboxHandler` | Abstractions | Process one stored payload, with a `WebhookHandlerReplaySafety` contract. |
| `IProviderWebhookIngressLimiter` | Abstractions | Concurrency and per-provider rate limits before a body is buffered, and the freshness check of a signed timestamp. |
| `IWebhookProviderNameNormalizer` | Abstractions | Map provider aliases to one canonical name before a delivery is keyed. Telephony's `IProviderIdentityResolver` is the normalizer when Telephony or Contact Center is on; otherwise names are used as given. |
| `IWebhookSignatureValidator` | Abstractions | Check a sender's signature in constant time. |
| `AddProviderWebhookInbox()` | Core | Registers the inbox, its store and the built-in signature validators. The feature calls it; a module never needs to. |

Inbox documents keep the YesSql type name and collection (`ContactCenter`) they were first stored under, so deliveries
a tenant stored before the inbox moved out of Contact Center stay readable without a data migration.

### Signature validators

`CrestApps.OrchardCore.Webhooks.Core.Signatures` holds the shared checks:

- `HmacWebhookSignatureValidator`: an HMAC over the signed content with a configurable algorithm, digest encoding and
  prefix. `Sha256Hex` and `Sha1Base64` (Twilio's request validation) are built in.
- `SharedKeyWebhookSignatureValidator`: the shared key itself in a header, such as Event Grid's `aeg-sas-key`.
- `WebhookSignatures.FixedTimeEquals`: the constant-time comparison every validator uses.

### Who uses it

| Module | Provider name | Handler |
| --- | --- | --- |
| [SMS](../omnichannel/sms.md) and the [Messaging Workspace](../omnichannel/messaging-workspace.md) | `Twilio`, `AzureEventGrid`, `Telnyx` | `sms-inbound` |
| [Contact Center voice](../contact-center/voice-routing.md) and [Telnyx](../telephony/telnyx.md) | the telephony provider | the provider's voice event handler |
| [Stripe](payments.md) | `Stripe` | `Stripe.Event`; a tenant without the inbox processes Stripe events inline |
