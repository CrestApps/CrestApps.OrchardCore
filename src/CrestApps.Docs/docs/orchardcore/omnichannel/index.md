---
sidebar_label: Overview
sidebar_position: 1
title: Omnichannel Communications
description: Orchard Core modules for unified communication orchestration and management.
---

# Omnichannel Communications

| | |
| --- | --- |
| **Feature Name** | Omnichannel |
| **Feature ID** | `CrestApps.OrchardCore.Omnichannel` |

The Omnichannel modules provide Orchard Core building blocks for coordinating communication across channels such as SMS and event-driven integrations.

## Available Orchard modules

| Module | Docs |
| --- | --- |
| Base orchestration module | This page |
| Event Grid integration | [Event Grid](event-grid) |
| Management UI | [Management](management) |
| SMS automation | [SMS](sms) |

## What the base module does

- provides the shared Orchard communication layer
- exposes the generic omnichannel webhook endpoint
- supplies shared concepts used by the management and channel modules

## Enable the feature

1. Go to **Tools -> Features** in Orchard Core.
2. Enable **Omnichannel**.
3. Add the related management or channel modules you need.

## Webhook endpoint

The base module exposes:

- `~/Omnichannel/CommunicationService`

Provider-specific integrations can forward inbound events into this endpoint.

## Notes

- The base module does not provide the full management UI by itself.
- Use the related module pages for channel-specific or management-specific setup.
