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
| Base orchestration module (includes the **Omnichannel - Azure Communication Services** feature) | This page, [Azure Communication Services](azure-communication-services) |
| Event Grid integration | [Event Grid](event-grid) |
| Management UI (CRM), including re-engagement cadences | [Management](management), [Cadences](cadences) |
| Leads, accounts and opportunities (**Omnichannel CRM**) | [CRM](crm) |
| SMS automation (AI) | [SMS](sms) |
| Automated Voice (AI voice conversations over any telephony provider) | [Telnyx AI Voice Agent](../telephony/telnyx.md#telnyx-ai-voice-agent) |
| Messaging workspace (human two-way, every non-voice channel) and its **SMS Messaging Channel** | [Messaging Workspace](messaging-workspace) |
| DNC Registry (national and local do-not-call screening) | [DNC Registry](../modules/dnc-registry) |
| Contact Center Business Hours (calendars that gate automated sends) | [Business hours](../user-manual/business-hours.md) |

## What the base module does

- provides the shared Orchard communication layer
- supplies the shared message, endpoint, preference, and processing contracts used by the management
  and channel modules
- ships the **Omnichannel - Azure Communication Services** feature
  (`CrestApps.OrchardCore.Omnichannel.AzureCommunicationServices`), which only enables Orchard Core's
  `OrchardCore.Email.Azure` and `OrchardCore.Sms.Azure` providers alongside Omnichannel

## Enable the feature

1. Go to **Tools -> Features** in Orchard Core.
2. Enable **Omnichannel**.
3. Add the related management or channel modules you need.

## Inbound webhooks

The base feature defines no HTTP endpoint of its own. Inbound events arrive through the channel
feature that owns the provider:

| Feature | Endpoint |
| --- | --- |
| [Omnichannel - Azure Event Grid](event-grid) | `POST ~/api/azure/webhook/eventgrid` |
| Omnichannel Twilio SMS, enabled by [SMS Omnichannel Automation](sms) or the [SMS Messaging Channel](messaging-workspace#setting-up-sms) (Twilio inbound SMS) | `POST ~/api/twilio/webhook/sms` |
| [Telnyx SMS](../telephony/telnyx.md#telnyx-sms) (Telnyx inbound SMS) | `POST ~/api/telnyx/webhook/sms` |

Enable the channel feature that matches how your provider delivers events, and configure its
authentication before pointing a subscription at it.

## Reports

When **Omnichannel Management** and the shared **Reports** feature (`CrestApps.OrchardCore.Reports`) are enabled, CRM
reports are contributed to the reusable [Reports](../modules/reports.md) framework and appear under the
top-level admin **Reports** menu. Omnichannel Management contributes 25 reports, grouped across the
**Operations**, **Queue & Routing**, **Agent Performance**, **CRM & Campaigns**, **Compliance & Audit**, and
**Technical & IT** categories. Each report shares the standard from/to date-range filter and adds
**Campaign group**, **Campaign**, **Channel**, **Source**, and **Status** filters. Reports export to CSV, and
to Excel (`.xlsx`) when the **Reports (OpenXml)** feature is enabled.

Examples include:

- **Activity summary** - activity volume and completion, broken down by source, channel, and status,
  with a daily created-activity trend.
- **Campaign performance** - per-campaign *completed vs pending* progress across the CRM activity
  inventory.
- **Disposition breakdown** - how completed activities were dispositioned in the period.
- **Activity backlog** and **Activity aging** - open workload, assignment, reservation, and overdue work.
- **Campaign source, channel, disposition, and attempt mixes**, **Channel endpoint usage**, and per-user
  productivity and completion-time reports.

See [Management: Reports](management#reports) and [Reports](../user-manual/reports.md) in the user manual.

Access is gated by the **View Omnichannel reports** (`ViewOmnichannelReports`) permission, which is
implied by **Manage activities** and granted to administrators by default.

## Notes

- The base module does not provide the full management UI by itself.
- Use the related module pages for channel-specific or management-specific setup.
