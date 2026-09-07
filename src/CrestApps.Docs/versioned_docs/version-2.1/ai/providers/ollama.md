---
sidebar_label: Ollama
sidebar_position: 4
title: Ollama AI Chat Feature
description: Ollama integration for local AI model support in Orchard Core.
---

| | |
| --- | --- |
| **Feature Name** | Ollama AI Chat |
| **Feature ID** | `CrestApps.OrchardCore.Ollama` |

Provides local AI model integration through Ollama.

## Overview

The Ollama provider lets Orchard Core use any model exposed by your Ollama host. You can explore available models at [Ollama Search](https://ollama.com/search).

## Running Ollama locally

To run an Ollama model locally, you need a container runtime such as Docker Desktop, Podman, or Docker Engine on WSL 2. See the official [Docker Desktop installation guide](https://docs.docker.com/desktop/setup/install/windows-install/) if you need a starting point.

Next, do the following in this project:

1. Set `CrestApps.Aspire.AppHost` as your startup project.
2. Run the project to start the Aspire host, which sets up the local Ollama environment.

By default, the project uses the `deepseek-v2:16b` model (8.9GB). Ensure your system has enough storage space before running it. The model downloads automatically on the first run. You can monitor the download and service statuses from the **Resources** tab in the Aspire dashboard.

## Configuration

To configure the Ollama connection manually, add the following settings to `appsettings.json`:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "Providers": {
          "Ollama": {
            "DefaultConnectionName": "Default",
            "Connections": {
              "Default": {
                "Endpoint": "http://localhost:11434",
                "Deployments": [
                  {
                    "Name": "local-chat",
                    "ModelName": "deepseek-v2:16b",
                    "Purpose": "Chat"
                  },
                  {
                    "Name": "local-utility",
                    "ModelName": "deepseek-v2:16b",
                    "Purpose": "Utility"
                  }
                ]
              }
            }
          }
        }
      }
    }
  }
}
```

## Aspire

If you are running this project with Aspire, Ollama is configured automatically and you do not need to add the `appsettings.json` section manually.
