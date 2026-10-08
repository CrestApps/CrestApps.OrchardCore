---
sidebar_label: AI Agents
sidebar_position: 8
title: AI Agents
description: Orchard Core module guidance for agent profiles and agent-enabled AI experiences.
user_manual:
  - user-manual/ai/tools-and-agents
---

# AI Agents

| | |
| --- | --- |
| **Feature Name** | Orchard Core AI Agent |
| **Feature ID** | `CrestApps.OrchardCore.AI.Agent` |

The Orchard agent module surfaces agent profiles inside Orchard Core so they can participate in module-driven AI experiences such as profile-based chat, A2A hosting, and other Orchard-managed orchestration flows.

Creating agent profiles and giving profiles tools in the admin, with a screencast, is described in the User Manual under [Tools and agents](../user-manual/ai/tools-and-agents.md).

## What this module adds in Orchard Core

- Orchard-aware AI tools for system, recipe, tenant, content, role, user, workflow, analytics, and communication scenarios
- agent-related profile editing support through the Orchard AI profile experience
- compatibility with Orchard modules such as A2A host, recipes, tenants, content types, contents, and workflows when those features are enabled

## How to use it in Orchard

Enable **Orchard Core AI Agent** (listed under **Artificial Intelligence** in **Tools → Features**) together with the base AI features. Its tools then appear, grouped by category, in the **Tools** section of the **Capabilities** tab of AI profiles, profile templates and chat interactions. The **Agent** profile type itself (with its **Description** and **Availability** fields) comes from the base AI module. Enable the related Orchard features if you want additional tool categories to appear.

The exact tool set available to agents depends on which Orchard modules are enabled. For example, tenant-management tools only light up when Orchard tenants support is enabled, and recipe tools depend on Orchard recipes support.

For the complete Orchard-specific AI function catalog, including the feature that enables each function and its description, see [AI Tools](tools).

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
