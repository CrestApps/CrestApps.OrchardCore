---
sidebar_label: Azure Communication Services
sidebar_position: 2
title: CrestApps Omnichannel (Azure Communication Services)
description: Azure Communication Services channel support for the Orchard Core Omnichannel stack.
---

| | |
| --- | --- |
| **Feature Name** | Omnichannel - Azure Communication Services |
| **Feature ID** | `CrestApps.OrchardCore.Omnichannel.AzureCommunicationServices` |

Enables Orchard Core's Azure Communication Services email and SMS providers alongside the base [Omnichannel](./) feature.

## When to enable this feature

Enable **Omnichannel - Azure Communication Services** when the tenant uses Azure Communication Services to send email or SMS through Orchard Core's standard communication services. Omnichannel components consume those standard services, so no separate ACS client or credential store is required in this feature.

This feature depends on:

- `CrestApps.OrchardCore.Omnichannel`
- `OrchardCore.Email.Azure`
- `OrchardCore.Sms.Azure`

## What it adds

- Azure Communication Services implementations of Orchard Core's email and SMS provider contracts
- the provider settings pages supplied by `OrchardCore.Email.Azure` and `OrchardCore.Sms.Azure`
- a single feature that activates the base Omnichannel model and both ACS outbound providers

The feature intentionally does not define another connection-string model or settings driver. Orchard Core owns those provider settings and protects their secrets through its standard site-settings infrastructure.

## Configuration

After enabling the feature:

1. Open **Settings > Communication > Email** and select/configure the Azure email provider.
2. Open **Settings > Communication > SMS** and select/configure the Azure SMS provider.
3. Enable the Omnichannel channel feature that consumes the service when required by the application: **SMS Omnichannel Automation** for AI-driven SMS, or the **SMS Messaging Channel** of the [Messaging Workspace](./messaging-workspace), which can pin the Azure SMS provider on an SMS channel endpoint for outbound messages.

Provider selection remains tenant-specific. Enabling this feature registers the ACS providers but does not silently change an existing tenant's selected email or SMS provider.

## Inbound events

Azure Communication Services delivers inbound-message and delivery events through Azure Event Grid. To receive ACS texts, enable [Omnichannel - Azure Event Grid](./event-grid), configure its authenticated webhook, and subscribe it to the ACS resource's SMS events.

| ACS event | What happens |
| --- | --- |
| `Microsoft.Communication.SMSReceived` | Routed as an inbound text on the `SMS` channel. It reaches [SMS Automation](./sms) and the SMS channel of the [Messaging Workspace](./messaging-workspace) in the same shape as a Twilio or Telnyx inbound text. |
| `Microsoft.Communication.SMSDeliveryReportReceived` | Stored, not routed. The delivery status of the sent message is not updated in the Messaging Workspace. |

The ACS number that receives the text is matched to a channel endpoint by its address, so the number must be set up as an SMS channel endpoint, as it is for any other provider. See [Azure Event Grid](./event-grid#inbound-texts) for how the event fields map onto the inbound message.

## How it fits with the other Omnichannel docs

- Use [Omnichannel Communications](./) for the shared base concepts and the inbound webhook map.
- Use [Management (CRM)](./management) for campaigns, subject flows, activities, contact management, and bulk operations.
- Use [SMS Automation](./sms) when you need AI-driven SMS activity handling.
- Use [Azure Event Grid](./event-grid) when inbound events are delivered through Event Grid instead of direct provider delivery.
