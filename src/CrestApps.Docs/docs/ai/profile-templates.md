---
sidebar_label: Profile Templates
sidebar_position: 11
title: AI Profile Templates
description: Orchard Core guidance for reusable AI profile templates and their relationship to the shared CrestApps.Core profile model.
---

# AI Profile Templates

AI profile templates are the Orchard Core-friendly way to stamp out repeatable AI profile configurations inside the admin UI and recipes.

## Orchard-specific scope

Use profile templates when you want to:

- create reusable Orchard-managed AI profile defaults
- standardize provider, deployment, prompt-template, and capability choices
- generate multiple tenant profiles from a curated template definition

## Where to manage them

After enabling the base AI module, Orchard adds:

- **Artificial Intelligence -> Templates**

This screen is backed by the Orchard profile-template manager and related display drivers contributed by enabled AI features.

## What can be templated in Orchard

Depending on the enabled features, profile templates can carry Orchard-managed defaults for items such as:

- selected orchestrator
- provider connection and deployment choices
- prompt-template selections
- tool and agent selections
- memory-related options
- chat-specific settings exposed by enabled modules

This makes profile templates the easiest way to standardize new AI profiles for content editors and administrators.

## How they fit with recipes

The base AI module registers Orchard recipe support for AI profile templates, so templates can be created and promoted through recipes as part of tenant provisioning or deployment workflows.

## Shared framework documentation

The underlying profile model and shared profile concepts are documented in **CrestApps.Core**:

- [AI profiles](https://core.crestapps.com/docs/core/ai-profiles)
- [AI templates](https://core.crestapps.com/docs/core/ai-templates)

## Related Orchard docs

- [AI Services](overview)
- [AI Prompt Templates](prompt-templates)
- [AI Chat](chat)
- [AI Chat Interactions](chat-interactions)
