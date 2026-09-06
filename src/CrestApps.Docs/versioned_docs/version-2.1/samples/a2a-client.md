---
sidebar_label: A2A Client Sample
sidebar_position: 2
title: A2A Client Sample
description: Sample ASP.NET Core Razor Pages app that connects to the A2A host hosted by CrestApps Orchard Core.
---

This sample project is a small ASP.NET Core Razor Pages app that connects to the A2A host served by `CrestApps.OrchardCore.Cms.Web` when running with Aspire. It provides a simple UI to explore A2A capabilities:

- **Home** – Prerequisites and setup instructions.
- **Agents** – List all available agents from the A2A host and send messages to them.

## Run with Aspire

The easiest way to run this sample is using the **Aspire AppHost** project:

1. Make sure [Docker Desktop](https://www.docker.com/products/docker-desktop) is installed and running locally.
2. Set `CrestApps.Aspire.AppHost` as your startup project.
3. Run the project. The Aspire dashboard will open in your browser.
4. From the Aspire dashboard, click on the endpoint for the resource you want to access:
   - **orchardcore** — Opens the Orchard Core CMS application (on port 5001).
   - **a2aclientsample** — Opens the A2A Client sample UI (on port 5003).

:::note

The Aspire AppHost orchestrates several services including **Ollama** (AI model host), **Elasticsearch**, **Redis**, and both the Orchard Core app and the sample clients. All service dependencies and environment variables are configured automatically.

:::

## A2A Host Feature Requirement

If the A2A Host feature is not enabled, requests to `/.well-known/agent-card.json` will return `404`. Enable the A2A Host feature in the default tenant:

- Feature ID: `CrestApps.OrchardCore.AI.A2A.Host`
- Admin UI: **Tools → Features**

After enabling the feature, refresh the sample UI to see available agents.

## Configuration

The A2A endpoint is configured via `A2A:Endpoint`. Aspire sets it automatically, but you can override it in `appsettings.json`:

```json
{
  "A2A": {
    "Endpoint": "https://localhost:5001"
  }
}
```

The agent card is automatically discovered at `/.well-known/agent-card.json` relative to the endpoint.
