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

Azure Communication Services delivery and inbound-message events are normally delivered through Azure Event Grid. Enable [Omnichannel - Azure Event Grid](./event-grid) and configure its authenticated webhook when inbound ACS events must enter the Omnichannel event pipeline.

:::caution
Inbound ACS text messages do not reach the Messaging Workspace or SMS Automation. The Event Grid endpoint stores each event as an Omnichannel message and passes its raw event type (`Microsoft.Communication.SMSReceived`) to the event handlers, but the built-in SMS handlers only act on the platform's own `SmsReceived` event, so they ignore it. Azure SMS works for outbound messages; receiving ACS texts requires a custom `IOmnichannelEventHandler`.
:::

## How it fits with the other Omnichannel docs

- Use [Omnichannel Communications](./) for the shared base concepts and the inbound webhook map.
- Use [Management (CRM)](./management) for campaigns, subject flows, activities, contact management, and bulk operations.
- Use [SMS Automation](./sms) when you need AI-driven SMS activity handling.
- Use [Azure Event Grid](./event-grid) when inbound events are delivered through Event Grid instead of direct provider delivery.
