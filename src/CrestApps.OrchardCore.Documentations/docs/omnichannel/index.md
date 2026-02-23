---
sidebar_label: Overview
sidebar_position: 1
title: Omnichannel Communications
description: Core orchestration services for inbound and outbound communication across channels such as SMS, Email, and Phone.
---

| | |
| --- | --- |
| **Feature Name** | Omnichannel |
| **Feature ID** | `CrestApps.OrchardCore.Omnichannel` |

Provides a unified communication layer that works across any channel (SMS, email, chat, phone, and more).

## Overview

The `CrestApps.OrchardCore.Omnichannel` module is the foundation of CrestApps' Omnichannel suite. It provides the core concepts and services that allow Orchard Core to orchestrate inbound and outbound communication across channels such as **SMS**, **Email**, and **Phone** (and more).

This module is intentionally "headless": it focuses on the orchestration layer and shared primitives and is meant to be paired with UI/CRM modules (like Omnichannel Management) and channel providers (like SMS automation).

## Key concept overview

- **Channel**: The medium of communication (SMS, Email, Phone, etc.).
- **Generic webhook endpoint**: A channel-agnostic endpoint used by external services to notify Orchard Core about inbound communication events.
- **Contact communication preferences**: Supports storing and enforcing the contact's communication preferences (Do Not Call / Do Not SMS / Do Not Email, etc.).

## Enable the feature

1. In Orchard Core Admin, go to `Tools` → `Features`.
2. Enable `Omnichannel`.

## Webhooks

This module exposes a generic communication webhook endpoint:

- `~/Omnichannel/CommunicationService`

Channel provider modules can forward their own provider-specific inbound events into this endpoint.

## Notes

- This module does not provide an end-user UI by itself.
- Use Omnichannel Management to manage contacts, subjects, campaigns, dispositions, activities, and batching.

---

## Azure Communication Services

| | |
| --- | --- |
| **Feature Name** | Omnichannel (Azure Communication Services) |
| **Feature ID** | `CrestApps.OrchardCore.Omnichannel.AzureCommunicationServices` |

Provides way to communicate using Azure Communication Services.
