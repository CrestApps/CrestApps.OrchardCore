---
sidebar_label: Overview
sidebar_position: 0
title: Samples
description: Sample projects related to CrestApps.OrchardCore and the shared CrestApps.Core framework.
---

# Samples

This section contains sample projects that demonstrate how to integrate with CrestApps Orchard Core modules.

## Available samples

| Sample | Description |
|--------|-------------|
| [MCP Client Sample](mcp-client) | ASP.NET Core Razor Pages app that connects to the MCP server hosted by CrestApps Orchard Core |
| [A2A Client Sample](a2a-client) | ASP.NET Core sample that interacts with A2A capabilities exposed by the Orchard Core solution |

## Shared framework sample host

The standalone framework samples now live with **CrestApps.Core**:

- **[MVC example](https://core.crestapps.com/docs/framework/mvc-example)** — full feature-by-feature reference host
- **[Framework docs](https://core.crestapps.com/docs/framework)** — canonical guidance for the shared `CrestApps.Core.*` packages

Most samples are designed to run with the **Aspire AppHost** (`CrestApps.Core.Aspire.AppHost`). Start the Aspire host first, then access the sample endpoints from the Aspire dashboard.
