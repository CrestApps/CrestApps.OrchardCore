---
sidebar_label: Overview
sidebar_position: 1
title: Agent-to-Agent Protocol (A2A)
description: Orchard Core client and host modules for the Agent-to-Agent protocol.
---

# Agent-to-Agent Protocol (A2A)

The A2A modules bring the **Agent-to-Agent protocol** into Orchard Core so tenants can connect to remote agents or expose local agent profiles to other clients.

## Orchard modules

| Feature | Feature ID | Purpose |
| --- | --- | --- |
| [A2A Client](client) | `CrestApps.OrchardCore.AI.A2A` | Connect to remote A2A hosts and surface their agents to Orchard AI features |
| [A2A Host](host) | `CrestApps.OrchardCore.AI.A2A.Host` | Expose Orchard-managed agent profiles through A2A |

## Scope split

Use the Orchard docs here for:

- feature IDs
- Orchard admin setup
- how the A2A modules fit into Orchard AI profiles and agent features
- the sample client that ships in this repository

Use the Core docs for shared protocol concepts:

- [A2A overview](https://core.crestapps.com/docs/a2a/index)
- [A2A client concepts](https://core.crestapps.com/docs/a2a/client)
- [A2A host concepts](https://core.crestapps.com/docs/a2a/host)
