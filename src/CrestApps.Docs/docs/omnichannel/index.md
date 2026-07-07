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

The management experience layers a lightweight Customer Relationship Management (CRM) workflow on top of those building blocks, including contacts, subjects, campaigns, subject flows, dispositions, activities, and batches.

## Available Orchard modules

| Module | Docs |
| --- | --- |
| Base orchestration module | This page |
| Azure Communication Services integration | [Azure Communication Services](azure-communication-services) |
| Event Grid integration | [Event Grid](event-grid) |
| Management UI | [Management](management) |
| SMS automation | [SMS](sms) |

## What the base module does

- provides the shared Orchard communication layer
- exposes the generic omnichannel webhook endpoint
- supplies shared concepts used by the management and channel modules
- acts as the dependency root for optional channel integrations such as Azure Communication Services

## Enable the feature

1. Go to **Tools -> Features** in Orchard Core.
2. Enable **Omnichannel**.
3. Add the related management or channel modules you need.

## Webhook endpoint

The base module exposes:

- `~/Omnichannel/CommunicationService`

Provider-specific integrations can forward inbound events into this endpoint.

## Reports

When **Omnichannel Management** and the shared **Reports** feature (`CrestApps.OrchardCore.Reports`) are enabled, CRM
reports are contributed to the reusable [Reports](../modules/reports.md) framework and appear under the
top-level admin **Reports** menu (grouped under **CRM**). Each report shares the standard from/to
date-range filter and a CSV export.

- **Activity summary** - activity volume and completion, broken down by source, channel, and status,
  with a daily created-activity trend.
- **Campaign performance** - per-campaign *completed vs pending* progress across the CRM activity
  inventory.
- **Disposition breakdown** - how completed activities were dispositioned in the period.

Access is gated by the **View Omnichannel reports** (`ViewOmnichannelReports`) permission, which is
implied by **Manage activities** and granted to administrators by default.

## Notes

- The base module does not provide the full management UI by itself.
- Use the related module pages for channel-specific or management-specific setup.
