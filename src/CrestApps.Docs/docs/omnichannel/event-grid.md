---
sidebar_label: Azure Event Grid
sidebar_position: 4
title: CrestApps Omnichannel (Azure Event Grid)
description: Receive inbound Omnichannel notifications via Azure Event Grid for decoupling and reliability.
---

| | |
| --- | --- |
| **Feature Name** | Omnichannel (Azure Event Grid) |
| **Feature ID** | `CrestApps.OrchardCore.Omnichannel.EventGrid` |

Provides a secure inbound webhook for Azure Event Grid notifications.

## Overview

The `CrestApps.OrchardCore.Omnichannel.EventGrid` module lets you receive inbound Omnichannel notifications via **Azure Event Grid**.

Use this when your SMS (or other channel) provider can publish events to Event Grid, or when you want to route provider webhooks into Orchard Core through Event Grid for decoupling and reliability.

## Enable the feature

1. In Orchard Core Admin, go to `Tools` → `Features`.
2. Enable `Omnichannel (Azure Event Grid)`.

## Webhook endpoint

This module exposes an endpoint for Azure Event Grid notifications:

- `~/Omnichannel/webhook/AzureEventGrid`

You can configure your Event Grid subscription to deliver events to this endpoint.

## Authentication options

The endpoint accepts requests only when one of these authentication modes succeeds:

1. **Shared access signature header** using the `aeg-sas-key` header.
2. **Microsoft Entra ID bearer token** validated against the configured OpenID Connect metadata endpoint.

If neither check succeeds, the endpoint returns `401 Unauthorized`.

## Configuration

Configure the module through tenant configuration:

```json
{
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
```

### Configuration fields

| Setting | Required | Description |
| --- | --- | --- |
| `EventGridSasKey` | No | Shared key compared against the incoming `aeg-sas-key` header. |
| `AADIssuer` | Only for bearer token auth | Expected issuer for Microsoft Entra ID tokens. |
| `AADAudience` | Only for bearer token auth | Expected audience for Microsoft Entra ID tokens. |
| `AADMetadataAddress` | Only for bearer token auth | OpenID Connect metadata address used to load signing keys for token validation. |

If you want bearer token authentication, configure **all three** AAD values. Partial AAD configuration is rejected.

## Azure Event Grid subscription setup

1. Create or open your Event Grid topic or system topic in Azure.
2. Add a new event subscription.
3. Choose **Webhook** as the endpoint type.
4. Set the webhook URL to your Orchard endpoint, for example:

   `https://your-host.example.com/Omnichannel/webhook/AzureEventGrid`

5. If you use SAS-key authentication, add the matching `aeg-sas-key` value to the subscription delivery settings.
6. If you use Microsoft Entra ID delivery, configure the subscription to send bearer tokens for the same issuer, audience, and metadata endpoint values you configured in Orchard Core.

## Subscription validation

Azure Event Grid sends a `Microsoft.EventGrid.SubscriptionValidationEvent` handshake before it starts normal delivery. The module handles that automatically and returns the validation code in the expected JSON response shape.

## Request size limit

The endpoint rejects payloads larger than **1 MB** with `413 Payload Too Large`.

## How it fits

A typical flow is:

1. Provider emits inbound/outbound event.
2. Event is delivered to Azure Event Grid.
3. Event Grid posts to `~/Omnichannel/EventGrid`.
4. Omnichannel processes the communication event and routes it to the appropriate channel/service.
