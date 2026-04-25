---
title: Consuming AI Services via Code
description: Where to find the shared AI service API documentation for the Orchard modules built on CrestApps.Core.
---

# Consuming AI Services via Code

Programmatic service usage is documented primarily in **CrestApps.Core** because the Orchard modules consume the same shared APIs.

## Use the Core docs for API details

- [AI core services](https://core.crestapps.com/docs/core/ai-core)
- [AI profiles](https://core.crestapps.com/docs/core/ai-profiles)
- [Chat concepts](https://core.crestapps.com/docs/core/chat)
- [Orchestration](https://core.crestapps.com/docs/core/orchestration)
- [Tools](https://core.crestapps.com/docs/core/tools)

## Orchard-specific guidance

In Orchard Core, those shared services are typically consumed from modules that also rely on:

- feature enablement through Orchard manifests
- site and tenant settings
- profile, connection, and deployment records managed in the admin UI
- Orchard recipes, workflows, and display drivers

If you are building an Orchard module, start with the Orchard-facing docs on this site for module setup and feature composition, then use the Core docs for the service-level APIs.
