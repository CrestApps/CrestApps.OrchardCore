---
sidebar_label: AI Agents
sidebar_position: 8
title: AI Agents
description: Orchard Core module guidance for agent profiles and agent-enabled AI experiences.
---

# AI Agents

| | |
| --- | --- |
| **Module** | `CrestApps.OrchardCore.AI.Agent` |

The Orchard agent module surfaces agent profiles inside Orchard Core so they can participate in module-driven AI experiences such as profile-based chat, A2A hosting, and other Orchard-managed orchestration flows.

## What this module adds in Orchard Core

- Orchard-aware AI tools for system, recipe, tenant, content, role, user, workflow, analytics, and communication scenarios
- agent-related profile editing support through the Orchard AI profile experience
- compatibility with Orchard modules such as A2A host, recipes, tenants, content types, contents, and workflows when those features are enabled

## How to use it in Orchard

1. Enable **AI Agents** together with the base AI features.
2. Go to **Artificial Intelligence -> Profiles**.
3. Create or edit the AI profile that should participate in agent scenarios.
4. Enable the related Orchard features if you want additional tool categories to appear.

The exact tool set available to agents depends on which Orchard modules are enabled. For example, tenant-management tools only light up when Orchard tenants support is enabled, and recipe tools depend on Orchard recipes support.

## Orchard-specific role of agent profiles

In Orchard Core, agent profiles are useful when you want:

- specialized AI capabilities that can be attached to other AI experiences
- locally hosted agents that can be exposed through [A2A Host](a2a/host)
- Orchard-aware AI automation over content, tenants, recipes, roles, users, and workflows

## Feature composition

The agent module becomes more useful as you add Orchard features:

- **Recipes** adds recipe-related tools
- **Tenants** adds tenant-management tools
- **Contents** and **Content Types** add content-management tools
- **Workflows** adds workflow-related tools

## Shared framework documentation

The reusable agent model, agent invocation patterns, and delegation concepts are documented in **CrestApps.Core**:

- [Agents](https://core.crestapps.com/docs/core/agents)

## Related Orchard features

- [AI Services](overview)
- [AI Chat](chat)
- [A2A Host](a2a/host)
