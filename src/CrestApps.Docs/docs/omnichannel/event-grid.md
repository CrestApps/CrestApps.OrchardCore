---
sidebar_label: Azure Event Grid
sidebar_position: 4
title: CrestApps Omnichannel - Azure Event Grid
description: Receive inbound Omnichannel notifications via Azure Event Grid for decoupling and reliability.
user_manual:
  - user-manual/messaging
---

| | |
| --- | --- |
| **Feature Name** | Omnichannel - Azure Event Grid |
| **Feature ID** | `CrestApps.OrchardCore.Omnichannel.EventGrid` |

Provides a secure inbound webhook for Azure Event Grid notifications.

## Overview

The `CrestApps.OrchardCore.Omnichannel.EventGrid` module lets you receive inbound Omnichannel notifications via **Azure Event Grid**.

Use this when your SMS (or other channel) provider can publish events to Event Grid, or when you want to route provider webhooks into Orchard Core through Event Grid for decoupling and reliability.

## Enable the feature

1. In Orchard Core Admin, go to `Tools` → `Features`.
2. Enable `Omnichannel - Azure Event Grid`.

## Webhook endpoint

This module exposes an endpoint for Azure Event Grid notifications:

- `POST ~/api/azure/webhook/eventgrid`

You can configure your Event Grid subscription to deliver events to this endpoint. The endpoint accepts the
**Event Grid event schema** only; a subscription that delivers the CloudEvents schema is answered with
`400 Bad Request`, so choose **Event Grid Schema** when you create the subscription.

## Authentication options

The endpoint accepts requests only when one of these authentication modes succeeds:

1. **Shared access signature header** using the `aeg-sas-key` header.
2. **Microsoft Entra ID bearer token** validated against the configured OpenID Connect metadata endpoint.

If neither check succeeds, the endpoint returns `401 Unauthorized`.

## Configuration

The module binds the `CrestApps:Omnichannel:EventGrid` section from the tenant's shell configuration.
In the application's root `appsettings.json` that section sits under the `OrchardCore` key:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Omnichannel": {
        "EventGrid": {
          "EventGridSasKey": "your-event-grid-sas-key",
          "AADIssuer": "https://sts.windows.net/<tenant-id>/",
          "AADAudience": "api://your-app-id",
          "AADMetadataAddress": "https://login.microsoftonline.com/<tenant-id>/.well-known/openid-configuration"
        }
      }
    }
  }
}
```

:::note
A tenant that overrides these values in its own `App_Data/Sites/{tenant}/appsettings.json` writes the same
keys **without** the `OrchardCore` wrapper, starting at `CrestApps`, because that file is already scoped to
the tenant.
:::

### Configuration fields

| Setting | Required | Description |
| --- | --- | --- |
| `EventGridSasKey` | No | Shared key compared against the incoming `aeg-sas-key` header. |
| `AADIssuer` | Only for bearer token auth | Expected issuer for Microsoft Entra ID tokens. |
| `AADAudience` | Only for bearer token auth | Expected audience for Microsoft Entra ID tokens. |
| `AADMetadataAddress` | Only for bearer token auth | OpenID Connect metadata address used to load signing keys for token validation. |

If you want bearer token authentication, configure **all three** AAD values. With only some of them set, the
endpoint logs a warning and skips the bearer-token check, so a request carrying only a bearer token is rejected
with `401 Unauthorized`; SAS-key authentication keeps working.

## Azure Event Grid subscription setup

1. Create or open your Event Grid topic or system topic in Azure.
2. Add a new event subscription.
3. Choose **Webhook** as the endpoint type.
4. Set the webhook URL to your Orchard endpoint, for example:

   `https://your-host.example.com/api/azure/webhook/eventgrid`

5. If you use SAS-key authentication, add a **custom delivery property** (a delivery header) named `aeg-sas-key` whose value matches `EventGridSasKey`. Event Grid does not send this header on its own for webhook subscriptions.
6. If you use Microsoft Entra ID delivery, configure the subscription to send bearer tokens for the same issuer, audience, and metadata endpoint values you configured in Orchard Core.

## Subscription validation

Azure Event Grid sends a `Microsoft.EventGrid.SubscriptionValidationEvent` handshake before it starts normal delivery. The module handles that automatically and returns the validation code in the expected JSON response shape. A validation event without a validation code is answered with `400 Bad Request`.

The handshake goes through the same authentication as every other delivery, so the `aeg-sas-key` delivery header (or the bearer token) must already be configured on the subscription when you create it.

## Request size limit

The endpoint rejects payloads larger than **1 MB** with `413 Payload Too Large`.

## How it fits

A typical flow is:

1. Provider emits inbound/outbound event.
2. Event is delivered to Azure Event Grid.
3. Event Grid posts to `~/api/azure/webhook/eventgrid`.
4. The endpoint maps each event by its type, as the table below shows, and answers Event Grid with `200 OK`.

| Event Grid event type | What happens |
| --- | --- |
| `Microsoft.Communication.SMSReceived` | Routed. Raised as the platform's own `SmsReceived` event on the `SMS` channel, the same event the Twilio and Telnyx webhooks raise, so [SMS Automation](./sms.md) and the SMS channel of the [Messaging Workspace](./messaging-workspace.md) both act on it. |
| `Microsoft.Communication.SMSDeliveryReportReceived` | Stored, not routed. The delivery status of the sent message is not updated. |
| Any other type | Stored, not routed. |

### Inbound texts

For an Azure Communication Services inbound text, the event data maps onto the inbound message like this:

| Event data field | Inbound message field |
| --- | --- |
| `from` | Customer address |
| `to` | Service address, which selects the channel endpoint |
| `message` | Content |
| `messageId` | Provider message id (the Event Grid event id when `messageId` is missing) |
| `receivedTimestamp` | Created time (the server time when the timestamp is missing or ahead of the server clock) |

The endpoint answers Event Grid at once and processes the text in the background, because an automated reply can take longer than the 30 seconds Event Grid waits for a response. Event Grid delivers at least once, so a redelivery of the same `messageId` is ignored rather than stored and answered a second time.

A text without a `from` or `to` number cannot be matched to a channel endpoint. It is stored with its raw event type, like an unmapped event, and a warning is logged.

### Delivery reports and other events

A delivery report and any event type without a mapping keep the original behaviour:

1. The event is saved as an **inbound** Omnichannel message. The endpoint reads the sender, recipient, content,
   channel, and timestamp from common property names in the event data (`from`, `to`,
   `content`/`message`/`body`/`text`, `channel`, `timestamp`). The channel is `Unknown` unless the data carries
   one, and the raw event data is stored as the content when no content field is found.
2. Each registered `IOmnichannelEventHandler` is then called with the raw Event Grid event type, subject, and
   data, and decides whether the event is one it handles.

A custom `IOmnichannelEventHandler` can still act on these events by checking the raw event type.

### Troubleshooting

The endpoint logs one line for each event it receives, with the event type and the event id, and then one line that says what it did with it:

- **Information:** the event was mapped to the `SMS` channel and the `SmsReceived` event, with the provider message id and the message length.
- **Information:** the event is a delivery report, with its status, and it is stored but not routed.
- **Information:** a redelivery of a text that was already processed was ignored.
- **Warning:** storing an inbound text hit a concurrency conflict and is retried (up to three attempts in total). An **Error** is logged only when every attempt fails, or on any other failure.
- **Warning:** a recognized event could not be mapped, and why.
- **Debug:** the event type has no mapping.

These lines never include the message text or the phone numbers.
