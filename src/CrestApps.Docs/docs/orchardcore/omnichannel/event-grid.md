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

Provides was to communicate using Azure Event Grid.

## Overview

The `CrestApps.OrchardCore.Omnichannel.EventGrid` module lets you receive inbound Omnichannel notifications via **Azure Event Grid**.

Use this when your SMS (or other channel) provider can publish events to Event Grid, or when you want to route provider webhooks into Orchard Core through Event Grid for decoupling and reliability.

## Enable the feature

1. In Orchard Core Admin, go to `Tools` → `Features`.
2. Enable `Omnichannel (Azure Event Grid)`.

## Webhook endpoint

This module exposes an endpoint for Azure Event Grid notifications:

- `~/Omnichannel/EventGrid`

You can configure your Event Grid subscription to deliver events to this endpoint.

## How it fits

A typical flow is:

1. Provider emits inbound/outbound event.
2. Event is delivered to Azure Event Grid.
3. Event Grid posts to `~/Omnichannel/EventGrid`.
4. Omnichannel processes the communication event and routes it to the appropriate channel/service.

