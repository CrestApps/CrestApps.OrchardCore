---
sidebar_label: Webhooks
title: Webhooks
description: The webhooks building block, both ways. Outbound, modules declare and publish events that are signed, queued in a delivery outbox and retried through the egress guard. Inbound, the provider webhook inbox durably accepts authenticated callbacks, de-duplicates redeliveries and processes them from storage with retries.
---

| | |
| --- | --- |
| **Features** | Webhooks (`CrestApps.OrchardCore.Webhooks`), Provider Webhook Inbox (`CrestApps.OrchardCore.ContactCenter.ProviderInbox`, dependency only) |
| **Core** | `CrestApps.OrchardCore.Webhooks.Core` |
| **Abstractions** | `CrestApps.OrchardCore.Webhooks.Abstractions` |
| **Category** | Webhooks |
| **Admin** | Configuration > Webhooks (permission **Manage webhooks**, `ManageCommerceWebhooks`) |
| **Depends on** | Nothing |

The Webhooks module owns webhooks in both directions: [outbound events](#outbound-webhooks) the site posts to other
systems, and [inbound callbacks](#provider-webhook-inbox) that providers (Twilio, Telnyx, Azure Event Grid, Stripe) send
to the site.

## Outbound webhooks

The **Webhooks** feature is the one outbound webhooks system of the solution. Commerce (orders, customers, stock,
shipments) and AI (conversations, alerts) both publish through it; neither keeps subscriptions, signing or retries of
its own.

| Piece | Contract | Implementation |
| --- | --- | --- |
| Declare events | `IWebhookEventProvider`, or `services.AddWebhookEvent(name, displayName, group, samplePayload)` | Gathered by `IWebhookEventCatalog` |
| Publish | `IWebhookPublisher.PublishAsync(eventName, payload[, eventId])` | `WebhookPublisher` queues one `WebhookDelivery` per subscription that takes the event, and sends them once the request's work is saved |
| Sign | `IWebhookSigner` | `HmacSha256WebhookSigner`: `v1=` and the hex HMAC-SHA256 of `{timestamp}.{body}` |
| Send once | `IWebhookSender` | `WebhookSender`: one signed POST through the egress-guarded `CrestApps.Webhooks` client |
| Retry | the delivery outbox | `WebhookDispatcher` and the **Webhook deliveries** background task: 1 min, 5 min, 30 min, 2 h, 6 h, 12 h, 24 h, then failed; a host the egress guard refuses fails at once |

The publisher sends only events an enabled feature declares, so a subscription to every event (`*`) never receives
events of a feature that is off. An event is published from a startup gated on the feature that raises it:

```csharp
[RequireFeatures(WebhooksConstants.Feature.Webhooks)]
public sealed class WebhooksStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWebhookEvent("invoice.sent", "Invoice sent", "Invoices", new { invoiceId = "inv_1" });
        services.AddScoped<IInvoiceHandler, InvoiceWebhookHandler>();
    }
}

// InvoiceWebhookHandler takes IWebhookPublisher in its constructor.
await _publisher.PublishAsync("invoice.sent", new { invoice = ToApi(invoice) }, cancellationToken);
```

A feature that posts to a URL of its own instead of to subscriptions, such as the AI **Send Signed Webhook** workflow
task, calls `services.AddWebhookSender()` and sends through `IWebhookSender`, so it signs and guards egress the same way.

The body, the headers and how a receiver checks the signature are described on the
[Commerce Webhooks](commerce-webhooks.md#receiving) page; they are the same for every event.

### Recipes and deployment

Subscriptions travel with the **Webhook subscriptions** deployment step and the `WebhookSubscriptions` recipe step. A
subscription is matched by its `ItemId`, so running a recipe twice changes nothing. The protected secret is never
exported, because it is protected with the site's own keys: a recipe may carry a plain `Secret`, and a new subscription
without one gets a new secret.

```json
{
  "name": "WebhookSubscriptions",
  "Subscriptions": [
    { "ItemId": "4k5y9m2x7q1c8w3n0p6r5t2v9z", "Name": "ERP", "Url": "https://erp.example.com/hooks", "Events": [ "order.paid", "order.refunded" ], "IsEnabled": true }
  ]
}
```

### Compatibility

The Webhooks feature took over the subscriptions, deliveries and admin of Commerce Webhooks, and the subscriptions of
AI Webhooks:

- `WebhookSubscription` and `WebhookDelivery` keep the YesSql type names and the `Webhooks` collection Commerce Webhooks
  stored them under, the secrets keep their data protection purpose, and the schema migration keeps its type name, so
  nothing is rewritten on upgrade.
- The `CrestApps.OrchardCore.Commerce.Webhooks` and `CrestApps.OrchardCore.AI.Webhooks` feature IDs are unchanged; both
  now depend on `CrestApps.OrchardCore.Webhooks`.
- The admin moved from **Commerce > Webhooks** (`/Admin/commerce/webhooks`) to **Configuration > Webhooks**
  (`/Admin/webhooks`). The permission keeps its name, `ManageCommerceWebhooks`, so the roles that had it keep it.
- The AI webhook subscriptions and their pending deliveries are moved into the Webhooks feature once (the
  `AIWebhookIndexMigrations` step 2), with their ids and secrets; the old rows are settled, not deleted, and purged after
  30 days. The old **Artificial Intelligence > Webhooks** page and its **Manage AI webhooks** permission are gone.

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
