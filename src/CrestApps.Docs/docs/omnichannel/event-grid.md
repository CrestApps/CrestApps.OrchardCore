---
sidebar_label: Azure Event Grid
sidebar_position: 4
title: CrestApps Omnichannel - Azure Event Grid
description: Receive inbound Omnichannel notifications via Azure Event Grid for decoupling and reliability.
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

Azure Event Grid sends a `Microsoft.EventGrid.SubscriptionValidationEvent` handshake before it starts normal delivery. The module handles that automatically and returns the validation code in the expected JSON response shape.

## Request size limit

The endpoint rejects payloads larger than **1 MB** with `413 Payload Too Large`.

## How it fits

A typical flow is:

1. Provider emits inbound/outbound event.
2. Event is delivered to Azure Event Grid.
3. Event Grid posts to `~/api/azure/webhook/eventgrid`.
4. Every event (other than the subscription-validation handshake) is saved as an **inbound** Omnichannel message.
   The endpoint reads the sender, recipient, content, channel, and timestamp from common property names in the
   event data (`from`, `to`, `content`/`message`/`body`/`text`, `channel`, `timestamp`); the channel is
   `Unknown` unless the data carries one, and the raw event data is stored as the content when no content field
   is found.
5. Each registered `IOmnichannelEventHandler` is then called with the raw Event Grid event type, subject, and
   data, and decides whether the event is one it handles.

:::caution
The built-in SMS handlers do not consume Azure Communication Services `Microsoft.Communication.SMSReceived`
events delivered this way, so an ACS inbound text is stored as a message but does not start or continue an
SMS conversation. Handling it requires a custom `IOmnichannelEventHandler`.
:::
