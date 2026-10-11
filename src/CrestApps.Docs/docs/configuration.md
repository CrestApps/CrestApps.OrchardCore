---
sidebar_label: Configuration
sidebar_position: 3
title: Configuration Reference
description: Every configuration section the CrestApps modules read, how values reach a tenant through appsettings.json, environment variables and secret stores, and which source wins.
user_manual:
  - user-manual/getting-started/features-and-settings
---

# Configuration Reference

This page lists every configuration section the CrestApps Orchard Core modules read outside the admin
screens: the exact section path, what it controls, an `appsettings.json` example, and the environment
variable form. Each entry links to the page that documents the setting in full.

Most settings are also, or only, available in the browser under **Settings**. Those are covered in the
User Manual; see [Features and settings](user-manual/getting-started/features-and-settings.md).

## How configuration reaches the modules

The modules read their settings through Orchard Core's tenant configuration (`IShellConfiguration`). Orchard
Core builds it from the host's configuration, takes the `OrchardCore` section, and layers the tenant's own
values on top. A module that reads `CrestApps:ContentTransfer` in code is therefore configured at
`OrchardCore:CrestApps:ContentTransfer` in the host's `appsettings.json`.

For Orchard Core's own description of these sources, see
[Configuration](https://docs.orchardcore.net/en/latest/reference/modules/Configuration/) in the Orchard Core
documentation.

### appsettings.json

In the startup project's `appsettings.json` (and `appsettings.{Environment}.json`), put every CrestApps
section under the `OrchardCore` key:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContentTransfer": {
        "MaxUploadFileSize": 5368709120
      }
    }
  }
}
```

Values set here apply to every tenant on the host.

### Environment variables

Environment variables use a double underscore (`__`) for each level of the path, including the
`OrchardCore` prefix. Array entries are addressed by index, as `__0__`, `__1__` and so on:

```text
OrchardCore__CrestApps__ContentTransfer__MaxUploadFileSize=5368709120
OrchardCore__CrestApps__AI__Connections__0__Name=openai-cloud
OrchardCore__CrestApps__AI__Connections__0__ApiKey=<api-key>
```

A section whose name already contains a single underscore keeps it. `CrestApps_Telephony:Commands:Timeout`
becomes `OrchardCore__CrestApps_Telephony__Commands__Timeout`. Hosting platforms that take "app settings",
such as Azure App Service, pass them to the application as environment variables, so the same names apply.

:::caution[Array indexes are positions]
An environment variable that sets `Connections__0__Name` replaces whatever the entry at index `0` was in
`appsettings.json`; it does not add a new entry. To add an entry beside the ones in a file, use an index
the file does not use. The AI connection list accepts gaps in its indexes.
:::

### Per-tenant configuration

There are two ways to give one tenant its own values.

**A tenant-named section.** Inside the `OrchardCore` section, a section named after the tenant overrides
the shared values for that tenant only. Use `Default` for the default tenant:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContentTransfer": {
        "MaxUploadFileSize": 1073741824
      }
    },
    "Contoso": {
      "CrestApps": {
        "ContentTransfer": {
          "MaxUploadFileSize": 5368709120
        }
      }
    }
  }
}
```

The environment-variable form adds the tenant name after `OrchardCore`:

```text
OrchardCore__Contoso__CrestApps__ContentTransfer__MaxUploadFileSize=5368709120
```

**The tenant's own file.** `App_Data/Sites/{TenantName}/appsettings.json` holds values for that tenant
only. The file is already scoped to the tenant, so write the keys **without** the `OrchardCore` wrapper,
starting at `CrestApps`:

```json
{
  "CrestApps": {
    "ContentTransfer": {
      "MaxUploadFileSize": 5368709120
    }
  }
}
```

Orchard Core writes this file during tenant setup, so keep the keys it already holds when you edit it.

**The global App_Data file.** `App_Data/appsettings.json` applies to every tenant, like the startup
project's file, and uses the same `OrchardCore` wrapper.

:::note
Several pages in this documentation show examples that start at `CrestApps` without the `OrchardCore`
wrapper. That shape is only correct inside a tenant's `App_Data/Sites/{TenantName}/appsettings.json`. In
the startup project's `appsettings.json`, `App_Data/appsettings.json` and environment variables, the
`OrchardCore` prefix is required.
:::

### Secrets

Keep API keys, passwords and connection strings out of files that are committed to source control:

- **Environment variables** are the simplest option for containers and hosted environments.
- **User secrets** are read only when `ASPNETCORE_ENVIRONMENT` is `Development`, and only by a startup
  project that declares a `UserSecretsId`. The `CrestApps.OrchardCore.Cms.Web` host does not declare one,
  so run `dotnet user-secrets init` in that project once before you set a secret. The key uses `:` and
  includes the `OrchardCore` prefix:

  ```powershell
  dotnet user-secrets set OrchardCore:CrestApps:AI:Connections:0:ApiKey "<api-key>"
  ```

- **Other secret stores**, such as Azure Key Vault, are added to the host's configuration in `Program.cs`
  with their standard ASP.NET Core configuration provider. Tenants see their values like any other host
  configuration. Azure Key Vault secret names use `--` in place of `:`, for example
  `OrchardCore--CrestApps--AI--Connections--0--ApiKey`.

Some admin screens also store secrets, such as the Telnyx API key or the Claude API key. Those are
encrypted with ASP.NET Core data protection before they are saved.

### Which source wins

When the same key comes from more than one source, the later source in this list wins:

1. The startup project's `appsettings.json`.
2. `appsettings.{Environment}.json`.
3. User secrets (Development only).
4. Any other provider the host adds in `Program.cs`, such as a secret store, in the order it is added.
5. `App_Data/appsettings.json`.
6. Environment variables, then command-line arguments. Orchard Core applies these again after
   `App_Data/appsettings.json`, so they win over it.

Then, for each tenant:

7. Keys under the tenant-named section (`OrchardCore:{TenantName}:...`), from any of the sources above,
   win over the shared `OrchardCore:...` keys.
8. `App_Data/Sites/{TenantName}/appsettings.json` wins over everything else.

Configuration is read when a tenant starts. Restart the application after you change a configuration file
or an environment variable.

## Admin settings versus configuration

Many modules can be set up both in the browser and in configuration. Which one wins depends on the module:

| Module | Admin screen | Configuration | Which wins |
| --- | --- | --- | --- |
| AI connections and deployments | **Artificial Intelligence** connections and deployments | `CrestApps:AI:Connections`, `CrestApps:AI:Deployments` | Both are used. Entries from configuration appear beside the ones stored in the database; deployments from configuration are read-only in the admin. |
| AI default parameters | **Settings > Artificial Intelligence** | `CrestApps:AI:DefaultParameters` | Configuration sets the defaults. The settings page can override maximum iterations, distributed caching and OpenTelemetry for the tenant, and cannot raise iterations above `AbsoluteMaximumIterationsPerRequest`. |
| Claude | **Settings > Artificial Intelligence > Claude** | `CrestApps:AI:Claude` | The settings page wins for every value it holds. The stored API key is used only while API-key authentication is selected. |
| Copilot | **Settings > Artificial Intelligence > Copilot** | `CrestApps:AI:Copilot` | The settings page wins for every value it holds. |
| MCP server | **Settings > Artificial Intelligence**, **MCP Server** card | `CrestApps:AI:McpServer` | **Configuration wins.** Values in configuration override the stored settings. |
| AI data sources | The data source editor | `CrestApps:PostgreSQL`, `CrestApps:Elasticsearch` and the `CrestApps:AI:DataSources` overrides | A connection stored on a data source wins over configuration. |
| Telnyx SMS | **Settings > Communication > SMS**, **Telnyx** | `OrchardCore_Sms_Telnyx` | The settings page wins while its provider is enabled. A field left blank there falls back to configuration. |
| Telnyx voice | **Settings > Communication > Telephony**, **Telnyx** | None | Admin screen only. |
| Asterisk | **Settings > Communication > Telephony**, **Asterisk** | `CrestApps:Asterisk:Default` | They are two separate providers. Configuration adds a **Default Asterisk** provider; the tenant picks which provider to use as its default telephony provider. |
| Do Not Call registries | **Settings > DNC Registries** | `CrestApps:DncRegistry:AzureBlobStorage` | Registry credentials are admin-only. File storage in Azure is configuration-only. |
| Phone number verification | **Settings > Phone Number Verifications** | None | Admin screen and recipes only. |

Every other section on this page is configuration-only: it has no admin screen.

## AI

### AI connections and deployments

| | |
| --- | --- |
| **Sections** | `CrestApps:AI:Connections`, `CrestApps:AI:Deployments` |
| **Read by** | CrestApps.Core (the AI framework packages), through the tenant configuration |
| **Controls** | Provider connections (endpoint, credentials) and the model deployments that use them, with the capabilities each model supports |

Both are arrays. A deployment names its provider in `ClientName` and its connection in `ConnectionName`:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "Connections": [
          {
            "Name": "openai-cloud",
            "ClientName": "OpenAI",
            "ApiKey": "<api-key>"
          }
        ],
        "Deployments": [
          {
            "Name": "chat-default",
            "ClientName": "OpenAI",
            "ConnectionName": "openai-cloud",
            "ModelName": "gpt-4o",
            "Properties": {
              "AIDeploymentMetadata": {
                "Features": [ "textGeneration", "toolCalling", "streaming" ]
              }
            }
          }
        ]
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__AI__Connections__0__ApiKey=<api-key>
OrchardCore__CrestApps__AI__Deployments__0__ModelName=gpt-4o
```

Which deployment serves chat, utility, embedding and the other slots is chosen on the settings page, not in
configuration. See [AI Providers](ai/providers/index.md) and [Model Capabilities](ai/model-capabilities.md).

### AI provider connection keys

The keys on a connection depend on the provider (`ClientName`):

| Provider | `ClientName` | Keys | Details |
| --- | --- | --- | --- |
| OpenAI and OpenAI-compatible services | `OpenAI` | `ApiKey`, `Endpoint` (for a compatible service) | [OpenAI](ai/providers/openai.md) |
| Azure OpenAI | `Azure` | `Endpoint`, `AuthenticationType` (`Default`, `ManagedIdentity`, `ApiKey`), `ApiKey`, `IdentityId` | [Azure OpenAI](ai/providers/azure-openai.md) |
| Azure AI Inference (GitHub models) | `AzureAIInference` | `Endpoint`, `AuthenticationType`, `ApiKey` | [Azure AI Inference](ai/providers/azure-ai-inference.md) |
| Ollama | `Ollama` | `Endpoint` | [Ollama](ai/providers/ollama.md) |

Azure Speech deployments carry their own connection: a deployment with `ClientName` `AzureSpeech` holds
`Endpoint`, `AuthenticationType` and `ApiKey` itself. See [Azure OpenAI](ai/providers/azure-openai.md).

```text
OrchardCore__CrestApps__AI__Connections__0__ClientName=Azure
OrchardCore__CrestApps__AI__Connections__0__AuthenticationType=ManagedIdentity
```

### Default AI parameters

| | |
| --- | --- |
| **Section** | `CrestApps:AI:DefaultParameters` |
| **Controls** | The completion parameters used when a profile does not set its own, and the request limits |

Keys: `Temperature`, `MaxOutputTokens`, `TopP` (default `1`), `FrequencyPenalty` (`0`), `PresencePenalty`
(`0`), `PastMessagesCount` (`10`), `MaximumIterationsPerRequest` (`10`),
`AbsoluteMaximumIterationsPerRequest` (`100`), `EnableOpenTelemetry` (`false`) and
`EnableDistributedCaching` (`true`). The defaults come from CrestApps.Core.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "DefaultParameters": {
          "Temperature": 0,
          "MaxOutputTokens": 800
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__AI__DefaultParameters__Temperature=0
OrchardCore__CrestApps__AI__DefaultParameters__MaxOutputTokens=800
```

See [AI Services](ai/overview.md).

### Azure OpenAI client

| | |
| --- | --- |
| **Section** | `CrestApps:AI:AzureClient` |
| **Feature** | Azure OpenAI Chat (`CrestApps.OrchardCore.OpenAI.Azure`) |
| **Controls** | Retries and logging of the Azure OpenAI client |

Keys: `EnableDefaultRetryPolicy` (default `true`), `MaxRetryAttempts` (`5`), `RateLimitRetryDelay`
(`00:00:01`), `MaxRetryDelay` (`00:00:32`), `BackoffType` (`Exponential`), `UseJitter` (`true`),
`EnableLogging`, `EnableMessageLogging` and `EnableMessageContentLogging` (all `false`). The defaults come
from CrestApps.Core.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "AzureClient": {
          "MaxRetryAttempts": 3
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__AI__AzureClient__MaxRetryAttempts=3
```

See [Azure OpenAI](ai/providers/azure-openai.md).

### Claude

| | |
| --- | --- |
| **Section** | `CrestApps:AI:Claude` |
| **Controls** | The Anthropic API key, base URL and default model |

Keys: `ApiKey`, `BaseUrl` (default `https://api.anthropic.com`), `DefaultModel`.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "Claude": {
          "ApiKey": "<anthropic-api-key>",
          "DefaultModel": "<model-name>"
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__AI__Claude__ApiKey=<anthropic-api-key>
```

The settings page wins over these values. See [Claude Integration](ai/claude.md).

### Copilot

| | |
| --- | --- |
| **Section** | `CrestApps:AI:Copilot` |
| **Controls** | How the Copilot orchestrator authenticates, and the model it uses |

Keys: `AuthenticationType` (`NotConfigured`, `GitHubOAuth` or `ApiKey`), `ClientId`, `ClientSecret` and
`Scopes` for GitHub OAuth; `ProviderType` (`openai`, `azure` or `anthropic`), `BaseUrl`, `ApiKey`,
`DefaultModel`, `WireApi` (`completions` or `responses`) and `AzureApiVersion` for API-key authentication.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "Copilot": {
          "AuthenticationType": "ApiKey",
          "ProviderType": "openai",
          "BaseUrl": "https://api.openai.com/v1",
          "ApiKey": "<api-key>",
          "DefaultModel": "<model-name>"
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__AI__Copilot__AuthenticationType=ApiKey
OrchardCore__CrestApps__AI__Copilot__ApiKey=<api-key>
```

The settings page wins over these values. See [Copilot](ai/copilot.md).

### Realtime voice transport

| | |
| --- | --- |
| **Sections** | `CrestApps:AI:RealtimeTransport`, `CrestApps:AI:RealtimeTransport:Cloudflare` |
| **Controls** | WebRTC on or off, turn detection, idle and session limits, knowledge grounding, STUN and TURN servers, and Cloudflare-issued TURN credentials |

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "RealtimeTransport": {
          "EnableWebRtc": true,
          "IdleTimeoutSeconds": 30,
          "TurnUrls": [ "turns:turn.example.com:5349" ],
          "TurnSecret": "<shared-secret>",
          "Cloudflare": {
            "TokenId": "<turn-token-id>",
            "ApiToken": "<api-token>"
          }
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__AI__RealtimeTransport__EnableWebRtc=false
OrchardCore__CrestApps__AI__RealtimeTransport__TurnUrls__0=turns:turn.example.com:5349
```

A tenant that declares `StunUrls` or `TurnUrls` replaces the host's list rather than adding to it. See
[Realtime Voice](ai/realtime-voice.md) for every key.

### Chat interaction batch processing

| | |
| --- | --- |
| **Section** | `CrestApps:AI:ChatInteractions:BatchProcessing` |
| **Feature** | AI Chat Interactions |
| **Controls** | How large tabular documents are processed row by row in batches |

Keys: `RowBatchSize` (default `25`), `MaxConcurrentBatches` (`3`), `MaxRowsPerDocument` (`1000`),
`ContinueOnBatchFailure` (`true`), `BatchTimeoutSeconds` (`300`), `DelayBetweenBatchesMs` (`100`),
`EnableResultCaching` (`true`) and `CacheExpirationMinutes` (`30`). The defaults come from CrestApps.Core.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "ChatInteractions": {
          "BatchProcessing": {
            "RowBatchSize": 50
          }
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__AI__ChatInteractions__BatchProcessing__RowBatchSize=50
```

See [AI Chat Interactions](ai/chat-interactions.md).

## AI documents, data sources and file sources

### Shared PostgreSQL and Elasticsearch connections

| | |
| --- | --- |
| **Sections** | `CrestApps:PostgreSQL`, `CrestApps:Elasticsearch` |
| **Controls** | The connection every PostgreSQL or Elasticsearch feature uses when it does not define its own |

`CrestApps:PostgreSQL` takes `ConnectionString`. `CrestApps:Elasticsearch` takes `Url` or `CloudId`,
`AuthenticationType` (`None`, `Basic`, `ApiKey`, `Base64ApiKey` or `KeyIdAndKey`), `Username`, `Password`,
`ApiKey`, `Base64ApiKey`, `ApiKeyId` and `CertificateFingerprint`.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "PostgreSQL": {
        "ConnectionString": "Host=localhost;Port=5432;Username=<user>;Password=<password>;Database=vectordb"
      },
      "Elasticsearch": {
        "Url": "http://localhost:9200",
        "AuthenticationType": "Basic",
        "Username": "<user>",
        "Password": "<password>"
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__PostgreSQL__ConnectionString=<connection-string>
OrchardCore__CrestApps__Elasticsearch__Url=http://localhost:9200
```

These are separate from `OrchardCore_Elasticsearch`, which configures Orchard Core's own Elasticsearch
feature. See [AI Data Sources - PostgreSQL](ai/data-sources/postgresql.md) and
[AI Data Sources - Elasticsearch](ai/data-sources/elasticsearch.md).

### Data source connection overrides

| | |
| --- | --- |
| **Sections** | `CrestApps:AI:DataSources:PostgreSQL`, `CrestApps:AI:DataSources:Elasticsearch` |
| **Controls** | A different server for AI data sources only. Values here override the shared section. |

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "DataSources": {
          "Elasticsearch": {
            "Url": "http://search.example.com:9200"
          }
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__AI__DataSources__PostgreSQL__ConnectionString=<connection-string>
OrchardCore__CrestApps__AI__DataSources__Elasticsearch__Url=http://search.example.com:9200
```

A connection stored on a data source wins over both sections. See
[AI Data Sources](ai/data-sources/index.md).

### AI documents in Azure Blob Storage

| | |
| --- | --- |
| **Section** | `CrestApps:AI:AzureDocuments` |
| **Feature** | AI Documents - Azure Blob Storage (`CrestApps.OrchardCore.AI.Documents.Azure`) |
| **Controls** | Where uploaded AI documents are stored, and what happens to the container when the tenant is removed |

Keys: `ConnectionString` and `ContainerName` (both required), `BasePath`, `CreateContainer`,
`RemoveContainer`, `RemoveFilesFromBasePath`. When either required key is missing, the feature logs an
error and documents stay on the local file system. `ContainerName` and `BasePath` accept Liquid such as
`{{ ShellSettings.Name }}`, for a container per tenant or a shared container with a folder per tenant; see
[Separating tenants](ai/documents/azure-blob-storage.md#separating-tenants).

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "AzureDocuments": {
          "ConnectionString": "<storage-connection-string>",
          "ContainerName": "ai-documents",
          "CreateContainer": true
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__AI__AzureDocuments__ConnectionString=<storage-connection-string>
OrchardCore__CrestApps__AI__AzureDocuments__ContainerName=ai-documents
```

See [Azure Blob Storage for AI documents](ai/documents/azure-blob-storage.md).

### File sources

| | |
| --- | --- |
| **Section** | `CrestApps:AI:FileSources` |
| **Controls** | How much work one file source run may take on, and how often sources re-index |

Keys: `MaxItemsPerRun` (default `200`), `MaxConcurrentFetches` (`2`), `RunCheckIntervalMinutes` (`15`, no
effect under Orchard Core) and `DefaultRunIntervalMinutes` (`1440`). `AllowedLocalRoots` is ignored: a
tenant can only read its own folder.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "FileSources": {
          "MaxItemsPerRun": 500
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__AI__FileSources__MaxItemsPerRun=500
```

See [File Sources](ai/file-sources.md).

## MCP and A2A

### MCP server

| | |
| --- | --- |
| **Section** | `CrestApps:AI:McpServer` |
| **Controls** | How MCP clients authenticate, and which tools the server exposes |

Keys: `AuthenticationType` (`OpenId`, `ApiKey` or `None`), `ApiKey`, `RequireAccessPermission`,
`ExposeAllTools`, `Tools`. Values here override the **MCP Server** settings stored in the admin.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "McpServer": {
          "AuthenticationType": "ApiKey",
          "ApiKey": "<api-key>"
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__AI__McpServer__AuthenticationType=ApiKey
OrchardCore__CrestApps__AI__McpServer__ApiKey=<api-key>
```

Use `None` only for local development. See [MCP Server](ai/mcp/server.md).

### A2A host

| | |
| --- | --- |
| **Section** | `CrestApps:AI:A2AHost` |
| **Controls** | How A2A clients authenticate, and whether agents are exposed as one card with skills |

Keys: `AuthenticationType` (`OpenId` by default, `ApiKey` or `None`), `ApiKey`, `RequireAccessPermission`,
`ExposeAgentsAsSkill`.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "A2AHost": {
          "AuthenticationType": "ApiKey",
          "ApiKey": "<api-key>"
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__AI__A2AHost__AuthenticationType=ApiKey
OrchardCore__CrestApps__AI__A2AHost__ExposeAgentsAsSkill=true
```

See [A2A Host](ai/a2a/host.md).

## Omnichannel and SMS

### Automated activity processing

| | |
| --- | --- |
| **Section** | `CrestApps:Omnichannel:Automation` |
| **Feature** | Omnichannel Management, with AI Services enabled |
| **Controls** | How much work one automated-activity pass takes on, and how failed activities are retried |

Keys: `ProcessorLeaseMilliseconds` (default `600000`), `ProcessorBatchSize` (`100`),
`MaxActivitiesPerInvocation` (`1000`), `MaxProcessingAttempts` (`5`), `RetryDelayMinutes` (`5`). Every value
must be greater than zero, and the batch size cannot exceed `MaxActivitiesPerInvocation`.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Omnichannel": {
        "Automation": {
          "ProcessorBatchSize": 50
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__Omnichannel__Automation__ProcessorBatchSize=50
```

See [Omnichannel Management](omnichannel/management.md) and
[Configuration validation](contact-center/production-support.md#configuration-validation).

### Messaging workspace

| | |
| --- | --- |
| **Section** | `CrestApps:Omnichannel:Messaging` |
| **Controls** | Inbox paging, conversation locks, outbox batches, the per-endpoint send budget, and inbound attachment limits |

Keys: `InboxPageSize` (default `50`), `ConversationLockTimeoutSeconds` (`10`),
`ConversationLockExpirationSeconds` (`30`), `OutboxBatchSize` (`200`), `MaxMessagesPerPassPerEndpoint`
(`20`), `MaxInboundAttachmentBytes` (`10485760`), `MaxInboundAttachments` (`10`),
`AttachmentLinkLifetimeHours` (`72`).

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Omnichannel": {
        "Messaging": {
          "InboxPageSize": 100
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__Omnichannel__Messaging__InboxPageSize=100
```

See [Messaging Workspace](omnichannel/messaging-workspace.md#configuration).

### Routed message distribution

| | |
| --- | --- |
| **Section** | `CrestApps:Omnichannel:Messaging:RoutedDistribution` |
| **Controls** | How routed (push) conversations are offered to agents |

Keys: `PickupGraceMinutes` (default `5`), `MaxReassignmentAttempts` (`2`), `VoiceCapacityWeight` (`3`).

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Omnichannel": {
        "Messaging": {
          "RoutedDistribution": {
            "PickupGraceMinutes": 10
          }
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__Omnichannel__Messaging__RoutedDistribution__PickupGraceMinutes=10
```

See [Messaging Workspace](omnichannel/messaging-workspace.md).

### SMS keyword replies

| | |
| --- | --- |
| **Section** | `CrestApps:Omnichannel:Messaging:Sms:KeywordReplies` |
| **Feature** | SMS Messaging Channel |
| **Controls** | The replies sent when a contact texts STOP, HELP or START |

Keys: `StopMessage`, `HelpMessage`, `StartMessage`. A key you leave out keeps the built-in reply.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Omnichannel": {
        "Messaging": {
          "Sms": {
            "KeywordReplies": {
              "HelpMessage": "Contoso support: reply STOP to opt out."
            }
          }
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__Omnichannel__Messaging__Sms__KeywordReplies__HelpMessage=<reply text>
```

See [Messaging Workspace](omnichannel/messaging-workspace.md).

### Azure Event Grid

| | |
| --- | --- |
| **Section** | `CrestApps:Omnichannel:EventGrid` |
| **Controls** | How the Event Grid webhook authenticates incoming events |

Keys: `EventGridSasKey`, `AADIssuer`, `AADAudience`, `AADMetadataAddress`.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Omnichannel": {
        "EventGrid": {
          "EventGridSasKey": "<sas-key>"
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__Omnichannel__EventGrid__EventGridSasKey=<sas-key>
```

See [Azure Event Grid](omnichannel/event-grid.md).

### Telnyx SMS

| | |
| --- | --- |
| **Section** | `OrchardCore_Sms_Telnyx` |
| **Feature** | Telnyx SMS (`CrestApps.OrchardCore.Telnyx.Sms`) |
| **Controls** | The configuration-backed Telnyx SMS provider |

Keys: `ApiKey`, `MessagingProfileId`, `WebhookPublicKey`, `ApiBaseUrl`. The provider is enabled whenever
`ApiKey` has a value. The section name follows Orchard Core's `OrchardCore_Sms_Twilio` convention, and it
also sits under the `OrchardCore` key in the startup project's `appsettings.json`:

```json
{
  "OrchardCore": {
    "OrchardCore_Sms_Telnyx": {
      "ApiKey": "<telnyx-api-key>",
      "MessagingProfileId": "<messaging-profile-id>",
      "WebhookPublicKey": "<ed25519-public-key>"
    }
  }
}
```

```text
OrchardCore__OrchardCore_Sms_Telnyx__ApiKey=<telnyx-api-key>
OrchardCore__OrchardCore_Sms_Telnyx__MessagingProfileId=<messaging-profile-id>
```

The SMS settings screen wins while its Telnyx provider is enabled. See
[Telnyx SMS](telephony/telnyx.md#telnyx-sms).

## Telephony

Telnyx voice is configured only on the **Telnyx** tab of the telephony settings; it reads no configuration
section. See [Telnyx](telephony/telnyx.md).

### Telephony commands

| | |
| --- | --- |
| **Section** | `CrestApps_Telephony:Commands` |
| **Controls** | The deadline for one PBX command, such as a dial, transfer or hang-up |

Key: `Timeout` (default `00:00:10`, from one second to two minutes).

```json
{
  "OrchardCore": {
    "CrestApps_Telephony": {
      "Commands": {
        "Timeout": "00:00:15"
      }
    }
  }
}
```

```text
OrchardCore__CrestApps_Telephony__Commands__Timeout=00:00:15
```

See [Voice Routing](contact-center/voice-routing.md#pbx-mutation-execution-boundary).

### Telephony coordination

| | |
| --- | --- |
| **Section** | `CrestApps_Telephony:Coordination` |
| **Controls** | Lock waits and leases around calls, and how long client-recorded calls are kept open |

Keys: `InteractionLockTimeout` (default `00:00:05`), `InteractionLockExpiration` (`00:02:00`),
`NewInteractionGracePeriod` (`00:00:15`), `ClientRecordedCallMaxAge` (`04:00:00`),
`ClientRecordedCallSilenceTimeout` (`00:05:00`), `TokenRefreshLockTimeout` (`00:00:10`),
`TokenRefreshLockExpiration` (`00:01:00`). Each lease expiry must be longer than its lock wait.

```json
{
  "OrchardCore": {
    "CrestApps_Telephony": {
      "Coordination": {
        "InteractionLockTimeout": "00:00:10"
      }
    }
  }
}
```

```text
OrchardCore__CrestApps_Telephony__Coordination__InteractionLockTimeout=00:00:10
```

See [Timings are configuration, not constants](contact-center/production-support.md#timings-are-configuration-not-constants).

### Call recordings in Azure Blob Storage

| | |
| --- | --- |
| **Section** | `CrestApps:Telephony:AzureRecordings` |
| **Controls** | Where call recordings are stored. Recordings stay encrypted before they reach Azure. |

Keys: `ConnectionString`, `ContainerName`, `BasePath`, `CreateContainer`. `ContainerName` and `BasePath`
accept Liquid such as `{{ ShellSettings.Name }}`, for a container per tenant or a shared container with a
folder per tenant; see [Separating tenants](telephony/recording-azure-blob-storage.md#separating-tenants).

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Telephony": {
        "AzureRecordings": {
          "ConnectionString": "<storage-connection-string>",
          "ContainerName": "telephony-recordings",
          "BasePath": "{{ ShellSettings.Name }}"
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__Telephony__AzureRecordings__ConnectionString=<storage-connection-string>
OrchardCore__CrestApps__Telephony__AzureRecordings__ContainerName=telephony-recordings
```

See [Recording storage in Azure Blob Storage](telephony/recording-azure-blob-storage.md).

### Default Asterisk provider

| | |
| --- | --- |
| **Section** | `CrestApps:Asterisk:Default` |
| **Controls** | A host-level **Default Asterisk** provider that every tenant with the Asterisk module can select |

Required keys: `BaseUrl`, `UserName`, `Password`, `ApplicationName`. The provider appears only when all four
have values. Other keys include `EndpointTemplate`, `TimeoutSeconds`, `VoicemailContext`,
`VoicemailExtensionTemplate`, `VoicemailPriority` and the browser WebRTC settings.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Asterisk": {
        "Default": {
          "BaseUrl": "http://pbx.example.com:8088/ari/",
          "UserName": "<ari-user>",
          "Password": "<ari-password>",
          "ApplicationName": "crestapps-telephony"
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__Asterisk__Default__BaseUrl=http://pbx.example.com:8088/ari/
OrchardCore__CrestApps__Asterisk__Default__Password=<ari-password>
```

In the `Production` environment a tenant refuses to start when this section holds one of the development
credentials published in this repository. See
[Asterisk](telephony/asterisk.md#configuration-backed-default-asterisk-provider).

### Asterisk coordination

| | |
| --- | --- |
| **Section** | `CrestApps:Asterisk:Coordination` |
| **Controls** | Lock and HTTP timings for Asterisk, and the real-time event buffer |

Keys: `CredentialLockTimeout` (default `00:00:05`), `CredentialLockExpiration` (`00:00:30`),
`ChannelBindingCreateLockTimeout` (`00:00:10`), `PendingReclamationThreshold` (`00:05:00`),
`HttpTotalRequestTimeout` (`00:00:30`), `HttpAttemptTimeout` (`00:00:10`), `RealtimeEventBufferCapacity`
(`1000`, at most `100000`), `RealtimeEventBackpressureTimeout` (`00:00:05`), `MaxRealtimeMessageBytes`
(`1048576`).

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Asterisk": {
        "Coordination": {
          "HttpTotalRequestTimeout": "00:01:00"
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__Asterisk__Coordination__HttpTotalRequestTimeout=00:01:00
```

See [Configuration validation](contact-center/production-support.md#configuration-validation).

### WebSockets

| | |
| --- | --- |
| **Section** | `CrestApps:WebSockets` |
| **Controls** | The ASP.NET Core WebSocket middleware used by features that host raw WebSockets, such as Telnyx voice media |

Keys: `KeepAliveInterval`, `KeepAliveTimeout`, `AllowedOrigins`. ASP.NET Core's defaults apply to any key
you leave out.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "WebSockets": {
        "KeepAliveInterval": "00:00:30",
        "AllowedOrigins": [ "https://app.example.com" ]
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__WebSockets__KeepAliveInterval=00:00:30
OrchardCore__CrestApps__WebSockets__AllowedOrigins__0=https://app.example.com
```

See [WebSockets](modules/websockets.md).

## Contact Center

The Contact Center sections are validated when the tenant starts. An invalid value stops that tenant from
starting instead of failing later. See
[Configuration validation](contact-center/production-support.md#configuration-validation).

### Topology

| | |
| --- | --- |
| **Section** | `CrestApps:ContactCenter:Topology` |
| **Controls** | The deployment topology the tenant declares, and whether its infrastructure requirements are enforced |

Key: `ProfileId`, such as `single-node-distributed` or `single-node-development`. Leaving it out runs the
tenant as a single node with no infrastructure checks.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContactCenter": {
        "Topology": {
          "ProfileId": "single-node-distributed"
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__ContactCenter__Topology__ProfileId=single-node-distributed
```

See [The declared topology is enforced at startup](contact-center/production-support.md#the-declared-topology-is-enforced-at-startup).

### Health checks

| | |
| --- | --- |
| **Section** | `CrestApps:ContactCenter:HealthChecks` |
| **Controls** | Health check thresholds, the optional node serving gate, and the shared health endpoint warning |

Keys: `DeadLetterDegradedThreshold` (default `1`), `DeadLetterUnhealthyThreshold` (`25`),
`OverdueBacklogDegradedThreshold` (`50`), `OverdueBacklogUnhealthyThreshold` (`500`),
`EnableNodeServingGate` (`false`), `ConsecutiveFailuresBeforeUnready` (`3`),
`ConsecutiveSuccessesBeforeReady` (`2`), `AllowUnsafeSharedEndpointRoute`.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContactCenter": {
        "HealthChecks": {
          "DeadLetterUnhealthyThreshold": 50
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__ContactCenter__HealthChecks__DeadLetterUnhealthyThreshold=50
```

See [Health checks](contact-center/production-support.md#health-checks).

### Base-voice verification

| | |
| --- | --- |
| **Section** | `CrestApps:ContactCenter:BaseVoiceVerification` |
| **Controls** | The operator's declaration that the WebRTC audio path of this deployment was verified |

Keys: `AudioVerificationAcknowledged`, `AudioVerificationEvidenceReference`. An acknowledgment needs an
evidence reference. In the `Production` environment, readiness is withheld until the acknowledgment is set.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContactCenter": {
        "BaseVoiceVerification": {
          "AudioVerificationAcknowledged": true,
          "AudioVerificationEvidenceReference": "<ticket-or-document-id>"
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__ContactCenter__BaseVoiceVerification__AudioVerificationAcknowledged=true
```

See [Base-voice deployment acceptance](contact-center/production-support.md#base-voice-deployment-acceptance).

### Feature lifecycle

| | |
| --- | --- |
| **Section** | `CrestApps:ContactCenter:FeatureLifecycle` |
| **Controls** | How long a Contact Center feature waits for running work to finish when it is disabled |

Key: `DrainTimeoutSeconds` (default `30`, from `1` to `300`).

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContactCenter": {
        "FeatureLifecycle": {
          "DrainTimeoutSeconds": 60
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__ContactCenter__FeatureLifecycle__DrainTimeoutSeconds=60
```

See [Feature lifecycle contract](contact-center/production-support.md#feature-lifecycle-contract).

### Coordination

| | |
| --- | --- |
| **Section** | `CrestApps:ContactCenter:Coordination` |
| **Controls** | Lock waits and leases for inbound calls, assignments and reservations, and agent pre-dial timing |

Keys: `InboundLockTimeout` (default `00:00:10`), `InboundLockExpiration` (`00:01:00`),
`AssignmentLockTimeout` (`00:00:10`), `AssignmentLockExpiration` (`00:00:30`), `ReservationLockTimeout`
(`00:00:10`), `ReservationLockExpiration` (`00:00:30`), `AgentPreDialEnabled` (`true`),
`AgentPreDialRingGrace` (`00:00:05`), `AgentPreDialMinimumOfferRemaining` (`00:00:02`), `ReclaimLockWait`
(`00:00:00.050`), `ExpiryPageSize` (`100`), `QueuedWorkSyncLease` (`00:00:02`),
`QueuedWorkSyncFallbackInterval` (`00:00:30`).

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContactCenter": {
        "Coordination": {
          "AssignmentLockTimeout": "00:00:15"
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__ContactCenter__Coordination__AssignmentLockTimeout=00:00:15
```

See [Timings are configuration, not constants](contact-center/production-support.md#timings-are-configuration-not-constants).

### Agent availability

| | |
| --- | --- |
| **Section** | `CrestApps:ContactCenter:Availability` |
| **Feature** | Contact Center Agents |
| **Controls** | How long an agent heartbeat counts as live, and the longest wrap-up before capacity is recovered |

Keys: `HeartbeatTimeout` (default `00:01:30`), `MaximumWrapUpDuration` (`00:15:00`).

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContactCenter": {
        "Availability": {
          "MaximumWrapUpDuration": "00:10:00"
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__ContactCenter__Availability__MaximumWrapUpDuration=00:10:00
```

See [Agent availability tuning](contact-center/production-support.md#agent-availability-tuning).

### Webhook ingress

| | |
| --- | --- |
| **Section** | `CrestApps:ContactCenter:WebhookIngress` |
| **Feature** | Contact Center Voice |
| **Controls** | Per-node rate and concurrency limits for provider webhooks, and accepted event age |

Keys: `ConcurrencyPermitLimit` (default `8`), `RatePermitLimit` (`120`), `RatePeriodSeconds` (`60`),
`MaximumDeliveryAgeSeconds` (`900`), `MaximumFutureSkewSeconds` (`120`).

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContactCenter": {
        "WebhookIngress": {
          "RatePermitLimit": 300
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__ContactCenter__WebhookIngress__RatePermitLimit=300
```

See [Provider webhook ingress tuning](contact-center/production-support.md#provider-webhook-ingress-tuning).

### Dialer compliance

| | |
| --- | --- |
| **Sections** | `CrestApps:ContactCenter:Compliance`, `CrestApps:ContactCenter:Compliance:ManualDialing` |
| **Feature** | Contact Center Outbound Dialer |
| **Controls** | The abandonment measurement window, and how soft-phone dials are screened |

`Compliance` takes `AbandonmentRollingWindowMinutes` (default `30`, from `1` to `1440`). `ManualDialing`
takes `RespectDoNotCall` (`true`), `EnforceCallingWindow` (`false`), `CallingCalendarId` and
`DefaultRegionCode`.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContactCenter": {
        "Compliance": {
          "AbandonmentRollingWindowMinutes": 60,
          "ManualDialing": {
            "EnforceCallingWindow": true,
            "DefaultRegionCode": "US"
          }
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__ContactCenter__Compliance__AbandonmentRollingWindowMinutes=60
OrchardCore__CrestApps__ContactCenter__Compliance__ManualDialing__RespectDoNotCall=true
```

See [Outbound compliance gate](contact-center/agents-queues-dialer.md#outbound-compliance-gate) and
[Manual soft-phone screening](contact-center/agents-queues-dialer.md#manual-soft-phone-screening).

### Predictive dialing

| | |
| --- | --- |
| **Section** | `CrestApps:ContactCenter:PredictiveDialing` |
| **Feature** | Contact Center Paced Dialing |
| **Controls** | The timings and limits of predictive pacing, and how its statistics are measured |

Every value is validated on start, and an invalid one stops the tenant with the key named.

| Key | Default | Rule |
| --- | --- | --- |
| `PacingInterval` | `00:00:02` | Greater than zero. |
| `PacingDebounce` | `00:00:00.250` | Greater than zero and shorter than `PacingInterval`. |
| `PacingLockExpiration` | `00:00:15` | Longer than `PacingInterval`. |
| `MaxDialsPerCycle` | `25` | `1` to `500`. |
| `ConnectLockWait` | `00:00:00.150` | Greater than zero and shorter than 2 seconds. |
| `ComplianceWindowDays` | `30` | `1` to `90`. |
| `DefaultRingHorizon` | `00:00:12` | Greater than zero. |
| `AnsweredUnconnectedSweepAfter` | `00:00:05` | Longer than 2 seconds. |
| `AgentLegAnswerTimeout` | `00:00:03` | `1` to `30` seconds. |
| `PacingLockRetryDelay` | `00:00:01` | Greater than zero and shorter than `PacingLockExpiration`. |
| `DiscountAgentsWithWaitingInbound` | `false` | `true` or `false`. |
| `StatisticsCacheDuration` | `00:00:05` | Greater than zero. |
| `MaxTimingSamples` | `2000` | Greater than zero. |

They govern over-dialing (Predictive profiles on the **Over-dial** pacing model): `PacingDebounce` delays a pacing run
after the event that asked for it, `PacingInterval` is how soon a queue that is placing calls is paced again,
`PacingLockExpiration` bounds the per-queue pacing lock, `MaxDialsPerCycle` caps the calls one cycle places,
`ConnectLockWait` is how long the claim of an agent at answer waits for the agent's lock before trying the next agent,
`ComplianceWindowDays` is the long-run abandonment window that must stay under the cap, and `DefaultRingHorizon` is the
ring time assumed for agents about to free up until it is measured. `StatisticsCacheDuration` and `MaxTimingSamples`
govern the measurement shown on the profile and used for pacing.

`AgentLegAnswerTimeout` is how long, from the moment an agent is claimed for an answered call, the agent's leg has to
answer before it is hung up, the agent is released back to work and the person hears the abandoned-call message. The
agent's leg is an invite to their phone and takes a second or more to set up on its own, so no value can promise an agent
within the two seconds after which a call counts as abandoned (a later connect is counted abandoned anyway); the timeout
bounds how long a person hears silence when a phone does not pick up. `AnsweredUnconnectedSweepAfter` is how long after
the answer the minute sweep gives the message to a call that nothing connected or abandoned, and gives up on a claimed
agent whose leg never answered (after `AgentLegAnswerTimeout` plus this delay), should the node that held the connect or
the deadline stop. `PacingLockRetryDelay` is how soon a campaign is paced again when a cycle found its pacing lock held by
another. `DiscountAgentsWithWaitingInbound` leaves out of the free agents an over-dial is sized for every agent who is
also signed in to an inbound queue with calls waiting.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContactCenter": {
        "PredictiveDialing": {
          "StatisticsCacheDuration": "00:00:10",
          "MaxTimingSamples": 1000
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__ContactCenter__PredictiveDialing__MaxTimingSamples=1000
```

See [Predictive dialing](contact-center/agents-queues-dialer.md#predictive-dialing).

### Reporting

| | |
| --- | --- |
| **Section** | `CrestApps:ContactCenter:Reporting` |
| **Controls** | The widest date range a Contact Center report accepts |

Key: `MaximumReportRange` (default `400.00:00:00`, 400 days).

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContactCenter": {
        "Reporting": {
          "MaximumReportRange": "180.00:00:00"
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__ContactCenter__Reporting__MaximumReportRange=180.00:00:00
```

See [Report Catalog](contact-center/report-catalog.md).

### Retention

| | |
| --- | --- |
| **Section** | `CrestApps:ContactCenter:Retention` |
| **Controls** | How many days each kind of Contact Center record is kept, the floors that hold records longer, and the purge batch size |

Every `...RetentionDays` key defaults to `0`, which keeps records indefinitely. Keys include
`InteractionRetentionDays`, `InteractionEventRetentionDays`, `CallSessionRetentionDays`,
`QueueItemRetentionDays`, `SharedVoicemailRetentionDays`, `CallQualityRecordRetentionDays`,
`LegalHoldMinimumDays`, `ProjectionReplayHorizonDays`, `PurgeBatchSize` and `MaxPurgeBatchesPerCycle`.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContactCenter": {
        "Retention": {
          "InteractionEventRetentionDays": 400,
          "CallSessionRetentionDays": 400
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__ContactCenter__Retention__InteractionEventRetentionDays=400
```

See [Retention, legal holds, and replay horizon](contact-center/production-support.md#retention-legal-holds-and-replay-horizon)
for the full list.

## Content transfer, Do Not Call and background work

### Content transfer

| | |
| --- | --- |
| **Section** | `CrestApps:ContentTransfer` |
| **Controls** | Import and export batch sizes, upload size limits, and which content types can be imported |

Keys: `ImportBatchSize` (default `100`), `ExportBatchSize` (`200`), `ExportQueueThreshold` (`500`),
`MaxUploadFileSize` (`1073741824`), `MaxUploadChunkSize` (`26214400`), `TemporaryFileLifetime`
(`01:00:00`), `AllowAllContentTypes` (`true`), `AllowedContentTypes`.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContentTransfer": {
        "MaxUploadFileSize": 5368709120
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__ContentTransfer__MaxUploadFileSize=5368709120
OrchardCore__Default__CrestApps__ContentTransfer__ImportBatchSize=50
```

See [Content Transfer](modules/content-transfer.md).

### Content transfer files in Azure Blob Storage

| | |
| --- | --- |
| **Section** | `CrestApps:ContentTransfer:AzureBlobStorage` |
| **Feature** | Content Transfer - Azure Blob Storage (`CrestApps.OrchardCore.ContentTransfer.Azure`) |
| **Controls** | Where uploaded import files and queued export files are stored |

Keys: `ConnectionString` and `ContainerName` (both required), `BasePath`, `CreateContainer`,
`RemoveContainer`, `RemoveFilesFromBasePath`. When either required key is missing, files stay on the local
file system and an error is logged. A site that runs on more than one instance needs this; see
[Running on more than one instance](modules/content-transfer.md#running-on-more-than-one-instance).
`ContainerName` and `BasePath` accept Liquid such as `{{ ShellSettings.Name }}`, for a container per tenant
or a shared container with a folder per tenant; see
[Separating tenants](modules/content-transfer.md#separating-tenants).

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContentTransfer": {
        "AzureBlobStorage": {
          "ConnectionString": "<storage-connection-string>",
          "ContainerName": "content-transfer"
        }
      }
    }
  }
}
```

### Background work pacing

| | |
| --- | --- |
| **Section** | `CrestApps:BackgroundWork:Pacing` |
| **Controls** | How much database time bulk imports and Do Not Call list jobs may use |

Keys: `DatabaseShare` (default `0.25`, from `0.05` to `1`), `MaxPause` (`00:00:30`).

```json
{
  "OrchardCore": {
    "CrestApps": {
      "BackgroundWork": {
        "Pacing": {
          "DatabaseShare": 0.5
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__BackgroundWork__Pacing__DatabaseShare=0.5
```

See [Limiting how much of the database an import uses](modules/content-transfer.md#limiting-how-much-of-the-database-an-import-uses).

### Do Not Call files in Azure Blob Storage

| | |
| --- | --- |
| **Section** | `CrestApps:DncRegistry:AzureBlobStorage` |
| **Feature** | DNC Registry - Azure Blob Storage (`CrestApps.OrchardCore.DncRegistry.Azure`) |
| **Controls** | Where uploaded local Do Not Call list files are stored |

Keys: `ConnectionString` and `ContainerName` (both required), `BasePath`, `CreateContainer`,
`RemoveContainer`, `RemoveFilesFromBasePath`. When either required key is missing, files stay on the local
file system and an error is logged. `ContainerName` and `BasePath` accept Liquid such as
`{{ ShellSettings.Name }}`, for a container per tenant or a shared container with a folder per tenant; see
[Separating tenants](modules/dnc-registry.md#separating-tenants).

```json
{
  "OrchardCore": {
    "CrestApps": {
      "DncRegistry": {
        "AzureBlobStorage": {
          "ConnectionString": "<storage-connection-string>",
          "ContainerName": "dnc-registry"
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__DncRegistry__AzureBlobStorage__ConnectionString=<storage-connection-string>
OrchardCore__CrestApps__DncRegistry__AzureBlobStorage__ContainerName=dnc-registry
```

The registry credentials (USA FTC, Canada DNCL) are set only in the admin. See
[DNC Registry](modules/dnc-registry.md#store-local-registry-files-in-azure-blob-storage).

### Phone number verification

The phone number verification providers have no configuration section. Set them up under
**Settings > Phone Number Verifications**, or with the recipe `settings` step. See
[Phone Number Verifications](modules/phone-number-verifications.md).

## Multi-tenancy

### Tenant hierarchy

| | |
| --- | --- |
| **Section** | `TenantHierarchy`, at the root of the application configuration |
| **Controls** | The platform domain, the egress guard, the database pools child tenants are created in, and reserved slugs |

This section is read from the application configuration only, not from `OrchardCore` or a tenant's own
configuration, so no tenant can change it.

Keys: `PlatformDomain`, `Scheme`, `UseHostPrefixedCookies` (on, except in development), `ReservedSlugs`,
`Egress:Enabled` (`true`), `Egress:BlockPrivateNetworks` (`true`), `Egress:AllowedHosts`, and per pool
`DatabasePools:{name}:DatabaseProvider`, `DatabasePools:{name}:ConnectionString` and
`DatabasePools:{name}:RetainRemovedDatabases` (`true`).

```json
{
  "TenantHierarchy": {
    "PlatformDomain": "platform.com",
    "DatabasePools": {
      "firms": {
        "DatabaseProvider": "Postgres",
        "ConnectionString": "Host=db;Username=provisioner;Password=..."
      }
    }
  }
}
```

```text
TenantHierarchy__PlatformDomain=platform.com
TenantHierarchy__Egress__AllowedHosts__0=ollama.internal
TenantHierarchy__DatabasePools__firms__DatabaseProvider=Postgres
```

A parent's policy is set on the platform's **Policy** screen and stored in the parent's shell settings. See
[Tenant Hierarchy](modules/tenant-hierarchy.md#configuration).

## Orchard Core sections the modules depend on

Some features rely on Orchard Core's own configuration. These sections belong to Orchard Core; see its
documentation for their keys.

| Section | Used for |
| --- | --- |
| `OrchardCore_Redis` | The Redis connection behind the distributed lock and the SignalR Redis backplane that a production Contact Center requires. See [Multi-node real-time backplane](contact-center/production-support.md#multi-node-real-time-backplane). |
| `OrchardCore_HealthChecks` | The route of Orchard Core's shared health endpoint. Contact Center warns when it still claims a liveness route. See [Health checks](contact-center/production-support.md#health-checks). |
| `OrchardCore_Elasticsearch` | Orchard Core's Elasticsearch feature, which owns the Orchard-managed search indexes. |
| `OrchardCore_Sms_Twilio` | Orchard Core's configuration-backed Twilio SMS provider. |

```text
OrchardCore__OrchardCore_Redis__Configuration=<redis-connection-string>
OrchardCore__OrchardCore_HealthChecks__Url=/health/aggregate
```

## Local development with Aspire

The Aspire host (`CrestApps.Aspire.AppHost`) passes configuration to the CMS as environment variables, so
the CMS needs no per-feature setup. It sets:

- `OrchardCore__CrestApps__PostgreSQL__ConnectionString` and `OrchardCore__CrestApps__Elasticsearch__*` for
  the shared connections;
- `OrchardCore__OrchardCore_Elasticsearch__*` and `OrchardCore__OrchardCore_Redis__Configuration` for Orchard
  Core's own features;
- an Ollama connection at `OrchardCore__CrestApps__AI__Connections__90__*`, a high index so it is added
  beside your own connections rather than replacing the first one;
- `AuthenticationType` `None` for the MCP server and the A2A host, for local clients only;
- the `OrchardCore__CrestApps__Asterisk__Default__*` keys for the local Asterisk container.

The app host's own credentials are Aspire parameters (`Parameters:PostgresUser`,
`Parameters:PostgresPassword`, `Parameters:ElasticsearchPassword`), which you can set with user secrets in
the app host project. See [Getting Started](getting-started.md#aspire-host).

## Deprecated section names

These older names still work unless noted, but use the new names in new configuration:

| Old section | New section | Still read |
| --- | --- | --- |
| `CrestApps_AI:DefaultParameters` | `CrestApps:AI:DefaultParameters` | Yes, with a warning in the log. The new section wins. |
| `CrestApps_AI:Providers` (connections nested under each provider) | `CrestApps:AI:Connections` and `CrestApps:AI:Deployments` | Yes |
| `CrestApps:McpServer` | `CrestApps:AI:McpServer` | Yes. The new section wins. |
| `CrestApps:A2AHost` | `CrestApps:AI:A2AHost` | Yes. The new section wins. |
| `CrestApps:Sms:Workspace` | `CrestApps:Omnichannel:Messaging` | No |
| `CrestApps:Sms:RoutedDistribution` | `CrestApps:Omnichannel:Messaging:RoutedDistribution` | No |
| `CrestApps:Sms:Portal:KeywordReplies` | `CrestApps:Omnichannel:Messaging:Sms:KeywordReplies` | No |
