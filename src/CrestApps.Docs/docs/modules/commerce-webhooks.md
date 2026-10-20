---
sidebar_label: Commerce Webhooks
title: Commerce Webhooks
description: Outgoing webhooks for commerce events, signed with a per-webhook HMAC-SHA256 secret, queued with the change that raised them, sent once it is saved, retried with backoff, and logged per delivery.
user_manual: user-manual/commerce/webhooks
---

| | |
| --- | --- |
| **Feature** | `CrestApps.OrchardCore.Commerce.Webhooks` (Commerce Webhooks) |
| **Contracts** | `CrestApps.OrchardCore.Abstractions`, namespace `CrestApps.OrchardCore.Commerce.Webhooks` |
| **Category** | Commerce |
| **Depends on** | Commerce |

The **Commerce Webhooks** feature posts commerce events (an order paid, a shipment shipped, stock sold out) as signed JSON to the URLs merchants subscribe. Each event is queued in the same unit of work as the change that raised it, sent once that work is saved, and retried with backoff until the receiver answers with a `2xx` status. Every delivery is logged.

The modules that raise events don't depend on this one: they resolve the publisher when an event happens, and skip when the feature is off.

## Model

Both documents live in the `Webhooks` YesSql collection.

- **`WebhookSubscription`** (`WebhookSubscriptionIndex`): `Name`, `Url`, `ProtectedSecret`, `Events` (event names, or `*` for every event, those added later included), `IsEnabled`, `CreatedUtc` and `ModifiedUtc`. `Takes(eventName)` says whether an enabled subscription wants an event.
- **`WebhookDelivery`** (`WebhookDeliveryIndex`): one event for one subscription. `EventId` (the same for every subscription the event went to), `EventName`, `Payload` (the JSON posted, the same on every attempt), `Status` (`Pending`, `Delivered` or `Failed`), `Attempts`, `NextAttemptUtc`, `LastStatusCode`, `LastError` (cut to 500 characters), `CreatedUtc`, `LastAttemptUtc` and `DeliveredUtc`.

The secret is made by `WebhookDispatcher.NewProtectedSecret()`: `whsec_` and 24 random bytes in URL-safe base 64, protected with ASP.NET Core Data Protection under the purpose `CrestApps.OrchardCore.Commerce.Webhooks.Secret`. The tenant's Data Protection keys must survive restarts and be shared by every node, or the secrets can't be read back.

## Publishing an event

```csharp
public interface ICommerceWebhookPublisher
{
    Task PublishAsync(string eventName, object data, CancellationToken cancellationToken = default);
}
```

`CommerceWebhookPublisher` reads the enabled subscriptions once per scope, and for each one that takes the event creates a pending `WebhookDelivery` in the current session, with the payload:

```json
{ "id": "evt_...", "event": "order.paid", "createdUtc": "2026-10-19T14:03:22.512Z", "data": { } }
```

`data` is serialized with `JsonSerializerOptions.Web` (camelCase). The event id is `evt_` and a new id, unless the caller passes one to `PublishAsync(eventName, data, eventId)`: an event that may be raised twice for the same thing passes a stable id, so receivers drop the repeat. A new delivery's `NextAttemptUtc` is a minute away (`Backoff[0]`), so the background task leaves it to the first send. It then adds a `ShellScope` deferred task that, after the request's work is committed, sends those deliveries in a scope of its own, so a slow receiver never holds up the request. An error while queuing is logged and swallowed: a webhook must never fail the order that raised it.

A module offers an event to the admin's event picker with `AddCommerceWebhookEvent(name, displayName, group)`, and publishes it with the publisher, resolved with `GetService` so it runs without the feature:

```csharp
services.AddCommerceWebhookEvent("subscription.renewed", "Subscription renewed", "Subscriptions");

if (serviceProvider.GetService<ICommerceWebhookPublisher>() is { } publisher)
{
    await publisher.PublishAsync("subscription.renewed", new { subscription = ToApi(subscription) }, cancellationToken);
}
```

Send the commerce API's shapes in `data` where there is one, so a receiver reads one format.

## Events

`CommerceWebhookEvents` names the events the commerce modules publish:

| Event | Published by | `data` |
| --- | --- | --- |
| `order.created` | Orders, `OrderWebhookEventHandler.CreatedAsync`: in a deferred task, once the order is saved and isn't canceled (another handler, such as the stock hold, can still refuse it), with the id `evt_order-created_` and the order's id | `order` (`ApiOrder`) |
| `order.paid` | Orders, `OrderWebhookEventHandler.PaidAsync` | `order` |
| `order.canceled` | Orders, `OrderWebhookEventHandler.CanceledAsync`, for every `OrderCancelReason` but `Replaced` | `order`, `reason` (camelCase `OrderCancelReason`) |
| `order.refunded` | Orders, `OrderWebhookEventHandler.RefundedAsync`, once per refund | `order`, `refund` (`ApiOrderRefund`) |
| `customer.created` | Customers, `WebhookCustomerService` (decorating `ICustomerService`: `CreateGuestAsync` and a new `GetOrCreateForUserAsync`) and **Add customer** in the admin, with the id `evt_customer-created_` and the customer's id | `customer` (`ApiCustomer`) |
| `shipment.shipped` | Shipping, `ShipmentWebhookHandler.ShippedAsync` | `shipment` (`ApiShipment`), `orderNumber`, `customerEmail` |
| `shipment.delivered` | Shipping, `ShipmentWebhookHandler.DeliveredAsync` | the same |
| `stock.changed` | Inventory, `StockWebhookObserver` (an `IStockLevelObserver`): after the unit of work commits, one event per stock item whose total `Available` differs from before its first change | `stock` (`ApiStockItem`), `previousAvailable` |
| `stock.low` | Inventory, `StockWebhookHandler` (an `IStockLevelHandler`), when `RanLow` and not `SoldOut` | `stock`, `previousAvailable` |
| `stock.sold-out` | Inventory, `StockWebhookHandler`, when `SoldOut` | `stock`, `previousAvailable` |
| `webhook.ping` | The admin's **Send a test event**, to one subscription only | `webhookId`, `name` |

Customers made by the `Customers` recipe step aren't published.

## Delivery

`WebhookDispatcher.SendAsync` makes one attempt at a pending delivery and saves what it came to.

- It posts `Payload` with `Content-Type: application/json`, a `CrestApps-Commerce-Webhooks/1.0` user agent, and the headers in `WebhookConstants.Headers`: `X-Webhook-Id` (the event id), `X-Webhook-Event`, `X-Webhook-Timestamp` (Unix seconds, at this attempt) and `X-Webhook-Signature`.
- The signature is `WebhookDispatcher.Sign(secret, timestamp, body)`: `v1=` and the lowercase hex HMAC-SHA256 of `{timestamp}.{body}`, keyed with the UTF-8 bytes of the whole secret.
- It uses the named `HttpClient` `CommerceWebhooks`, with a 15-second timeout, and doesn't follow redirects: a `3xx` is an answer that isn't `2xx`. Its handler (`WebhookNetwork`) also refuses to connect when the host resolves only to loopback, private or link-local addresses, so a hostname can't reach the site's own network either; loopback is allowed while the host environment is Development. Of a refusal, only the first 500 characters are read, for at most 5 seconds.
- A `2xx` answer marks it `Delivered`. Any other answer, a timeout or a connection error schedules the next attempt from `WebhookDispatcher.Backoff`: 1 minute, 5 minutes, 30 minutes, 2 hours, 6 hours, 12 hours and 24 hours after the attempts so far. After the eighth attempt fails it's `Failed`.
- A delivery fails at once, without being sent, when its subscription was deleted (*The webhook was deleted.*), is turned off (*The webhook was turned off.*), or has a secret Data Protection can't unprotect (*The webhook's signing secret can't be read. Give it a new secret, then send it again.*).

`WebhookDeliveriesBackgroundTask` runs every minute, under a 10-minute lock. It sends up to 20 pending deliveries whose `NextAttemptUtc` has come (`IWebhookDeliveryStore.ListDueAsync`, oldest first), loading each afresh and saving it as soon as it's sent, so a run that stops half way, or the next run, never sends it again. It then deletes up to 20 delivered or failed deliveries created more than 30 days ago.

**Send again** in the admin (`WebhookDispatcher.ResendAsync`) resets a delivery to pending with no attempts and sends it at once.

## Receiving

A receiver should check the signature against the raw body, refuse an old timestamp, answer `2xx` quickly and do its work afterwards, and drop an `X-Webhook-Id` it has already handled, since a delivery can arrive more than once. The [user manual page](../user-manual/commerce/webhooks.md#check-the-signature) has C# and Node.js examples.

## Admin

- **Commerce > Webhooks** (`commerce/webhooks`, `WebhooksAdminController`) lists the subscriptions through the `AdminList` shape, with **Add webhook**.
- A subscription's editor sets its name, URL, whether it's on, and its events, grouped by each event's group. `IsAllowedUrl` takes an absolute `https` URL without user info, and refuses a loopback, private or link-local IP address; plain `http` only to a loopback host when the host environment is Development. Deleting a subscription deletes its deliveries. Saved, it shows the **Signing secret**, with **New secret** (`RotateSecret`).
- **Deliveries** (`commerce/webhooks/Deliveries/{itemId}`) lists a subscription's deliveries, newest first, with a status filter, **Send again** per delivery, and **Send a test event** (`Ping`), which sends `webhook.ping` to that subscription in the request.

## Permissions

| Permission | Allows |
| --- | --- |
| **Manage commerce webhooks** (`ManageCommerceWebhooks`) | Everything under **Commerce > Webhooks**, including reading the signing secrets. |

Administrators have it by default.

## Related modules

- [Carts and Orders](carts-and-orders): order events.
- [Customers](customers): customer events.
- [Shipping](shipping): shipment events.
- [Inventory](inventory): stock events.
- [Checkout](checkout#the-commerce-api): the commerce API whose shapes the events carry.
