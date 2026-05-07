---
sidebar_label: Azure Communication Services
sidebar_position: 2
title: CrestApps Omnichannel (Azure Communication Services)
description: Azure Communication Services channel support for the Orchard Core Omnichannel stack.
---

| | |
| --- | --- |
| **Feature Name** | Omnichannel (Azure Communication Services) |
| **Feature ID** | `CrestApps.OrchardCore.Omnichannel.AzureCommunicationServices` |

Provides a communication-channel integration for Azure Communication Services on top of the base [Omnichannel](./) feature.

## When to enable this feature

Enable **Omnichannel (Azure Communication Services)** when your Orchard tenant needs to send or receive omnichannel traffic through Azure Communication Services instead of relying only on the generic base module or provider-specific webhook relays.

This feature depends on:

- `CrestApps.OrchardCore.Omnichannel`

## What it adds

- Azure Communication Services-specific channel wiring for the Omnichannel stack
- a provider integration point that can be composed with Omnichannel campaigns and activity processing
- the Azure Communication Services layer that other Omnichannel features can build on when ACS is part of the deployment

## How it fits with the other Omnichannel docs

- Use [Omnichannel Communications](./) for the shared base concepts and webhook entry point.
- Use [Management (Mini-CRM)](./management) for campaigns, activities, contact management, and bulk operations.
- Use [SMS Automation](./sms) when you need AI-driven SMS activity handling.
- Use [Azure Event Grid](./event-grid) when inbound events are delivered through Event Grid instead of direct provider delivery.
