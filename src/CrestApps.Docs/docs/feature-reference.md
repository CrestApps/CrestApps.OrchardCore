---
sidebar_position: 3
title: Feature ID Reference
description: Complete reference of the manifest-backed feature IDs declared by the modules in this repository.
---

# Feature ID Reference

Every feature ID declared by a module under `src/Modules`, grouped by the **Category** the manifest gives it.
That category is the heading the feature appears under in **Tools → Features**, so this page reads in the same
order as that screen.

A module whose manifest declares no explicit `[Feature]` still contributes one feature whose ID is the module
(assembly) name, so every row below is an ID you can enable, list in a recipe, or use in a `[Feature]`
attribute.

Features marked **dependency only** cannot be turned on directly. Enabling a feature that depends on them
turns them on, and they disappear again when nothing needs them.

## Artificial Intelligence

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.AI` | AI Services (dependency only) | [AI Services](./ai/overview) |
| `CrestApps.OrchardCore.AI.ConnectionManagement` | AI Connection Management | [AI Providers](./ai/providers/) |
| `CrestApps.OrchardCore.AI.ToolInstances` | AI Tool Instances | [AI Tool Instances](./ai/tool-instances) |
| `CrestApps.OrchardCore.AI.Chat.Core` | AI Chat Services (dependency only) | [AI Services](./ai/overview) |
| `CrestApps.OrchardCore.AI.Chat.Api` | AI Chat WebAPI | [AI Services](./ai/overview) |
| `CrestApps.OrchardCore.AI.Chat` | AI Chat | [AI Chat](./ai/chat) |
| `CrestApps.OrchardCore.AI.Chat.AdminWidget` | AI Chat Admin Widget | [AI Chat](./ai/chat) |
| `CrestApps.OrchardCore.AI.Chat.Analytics` | AI Chat Session Analytics | [AI Chat Session Analytics](./ai/chat-analytics) |
| `CrestApps.OrchardCore.AI.Chat.Interactions` | AI Chat Interactions | [AI Chat Interactions](./ai/chat-interactions) |
| `CrestApps.OrchardCore.AI.Agent` | Orchard Core AI Agent | [AI Agents](./ai/agent) |
| `CrestApps.OrchardCore.AI.Prompting` | AI Prompt Templates | [AI Prompt Templates](./ai/prompt-templates) |
| `CrestApps.OrchardCore.AzureAIInference` | Azure AI Inference Chat | [Azure AI Inference](./ai/providers/azure-ai-inference) |
| `CrestApps.OrchardCore.Ollama` | Ollama AI Chat | [Ollama](./ai/providers/ollama) |
| `CrestApps.OrchardCore.OpenAI` | OpenAI Chat | [OpenAI Provider](./ai/providers/openai) |
| `CrestApps.OrchardCore.OpenAI.Azure` | Azure OpenAI Chat | [Azure OpenAI Provider](./ai/providers/azure-openai) |

## Artificial Intelligence - Orchestrators

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.AI.Chat.Claude` | AI Claude Orchestrator | [Claude Integration](./ai/claude) |
| `CrestApps.OrchardCore.AI.Chat.Copilot` | AI Copilot Orchestrator | [Copilot Integration](./ai/copilot) |

## Artificial Intelligence - Knowledgebase

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.AI.Documents` | AI Documents (dependency only) | [AI Documents](./ai/documents/) |
| `CrestApps.OrchardCore.AI.Documents.ChatInteractions` | AI Documents for Chat Interactions | [AI Documents](./ai/documents/) |
| `CrestApps.OrchardCore.AI.Documents.ChatSessions` | AI Documents for Chat Sessions | [AI Documents](./ai/documents/) |
| `CrestApps.OrchardCore.AI.Documents.Profiles` | AI Documents for Profiles | [AI Documents](./ai/documents/) |
| `CrestApps.OrchardCore.AI.Documents.Azure` | AI Documents - Azure Blob Storage | [Azure Blob Storage](./ai/documents/azure-blob-storage) |
| `CrestApps.OrchardCore.AI.Documents.AzureAI` | AI Documents indexing using Azure AI Search | [Azure AI Search](./ai/documents/azure-ai) |
| `CrestApps.OrchardCore.AI.Documents.Elasticsearch` | AI Documents indexing using Elasticsearch | [Elasticsearch](./ai/documents/elasticsearch) |
| `CrestApps.OrchardCore.AI.Documents.OpenXml` | AI Documents (OpenXml) | [Open XML](./ai/documents/openxml) |
| `CrestApps.OrchardCore.AI.Documents.Pdf` | AI Documents (PDF) | [PDF](./ai/documents/pdf) |
| `CrestApps.OrchardCore.AI.DataSources` | AI Data Sources (dependency only) | [AI Data Sources](./ai/data-sources/) |
| `CrestApps.OrchardCore.AI.DataSources.AzureAI` | AI Data Sources - Azure AI Search | [Azure AI Search](./ai/data-sources/azure-ai) |
| `CrestApps.OrchardCore.AI.DataSources.Elasticsearch` | AI Data Sources - Elasticsearch | [Elasticsearch](./ai/data-sources/elasticsearch) |
| `CrestApps.OrchardCore.AI.DataSources.PostgreSQL` | AI Data Sources - PostgreSQL | [PostgreSQL](./ai/data-sources/postgresql) |
| `CrestApps.OrchardCore.AI.WebCrawlers` | AI Web Crawlers | [Web Crawlers](./ai/data-sources/web-crawlers) |
| `CrestApps.OrchardCore.AI.FileSources` | AI File Sources | [AI File Sources](./ai/file-sources) |
| `CrestApps.OrchardCore.AI.FileSources.Ftp` | AI File Sources - FTP | [AI File Sources](./ai/file-sources#ftp-and-sftp-connectors) |
| `CrestApps.OrchardCore.AI.FileSources.Sftp` | AI File Sources - SFTP | [AI File Sources](./ai/file-sources#ftp-and-sftp-connectors) |
| `CrestApps.OrchardCore.AI.Memory` | AI Memory (dependency only) | [AI Memory](./ai/memory) |
| `CrestApps.OrchardCore.AI.Memory.AzureAI` | AI Memory indexing using Azure AI Search | [AI Memory - Azure AI Search](./ai/memory-azure-ai) |
| `CrestApps.OrchardCore.AI.Memory.Elasticsearch` | AI Memory indexing using Elasticsearch | [AI Memory - Elasticsearch](./ai/memory-elasticsearch) |

## Artificial Intelligence - MCP

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.AI.Mcp` | Model Context Protocol (MCP) Client | [MCP Client](./ai/mcp/client) |
| `CrestApps.OrchardCore.AI.Mcp.Stdio` | Model Context Protocol (MCP) Local Client | [MCP Client](./ai/mcp/client) |
| `CrestApps.OrchardCore.AI.Mcp.Server` | Model Context Protocol (MCP) Server | [MCP Server](./ai/mcp/server) |
| `CrestApps.OrchardCore.AI.Mcp.Resources.Ftp` | Model Context Protocol (MCP) FTP Resource | [MCP FTP Resource](./ai/mcp/ftp) |
| `CrestApps.OrchardCore.AI.Mcp.Resources.Sftp` | Model Context Protocol (MCP) SFTP Resource | [MCP SFTP Resource](./ai/mcp/sftp) |

## Artificial Intelligence - A2A

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.AI.A2A` | Agent-to-Agent (A2A) Client | [A2A Client](./ai/a2a/client) |
| `CrestApps.OrchardCore.AI.A2A.Host` | Agent-to-Agent (A2A) Host | [A2A Host](./ai/a2a/host) |

## Contact Center

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.Omnichannel` | Omnichannel | [Omnichannel Communications](./omnichannel/) |
| `CrestApps.OrchardCore.Omnichannel.AzureCommunicationServices` | Omnichannel - Azure Communication Services | [Azure Communication Services](./omnichannel/azure-communication-services) |
| `CrestApps.OrchardCore.Omnichannel.EventGrid` | Omnichannel - Azure Event Grid | [Azure Event Grid](./omnichannel/event-grid) |
| `CrestApps.OrchardCore.Omnichannel.Activities` | Omnichannel Activities | [Management (CRM)](./omnichannel/management) |
| `CrestApps.OrchardCore.Omnichannel.Managements` | Omnichannel Management | [Management (CRM)](./omnichannel/management) |
| `CrestApps.OrchardCore.Omnichannel.Sms` | SMS Omnichannel Automation | [SMS Automation](./omnichannel/sms) |

## Telephony

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.Telephony` | Telephony | [Telephony](./telephony/) |
| `CrestApps.OrchardCore.Telephony.Admin` | Telephony Administration | [Telephony](./telephony/#site-settings) |
| `CrestApps.OrchardCore.Telephony.SoftPhone` | Telephony Soft Phone | [Telephony](./telephony/#soft-phone-widget) |
| `CrestApps.OrchardCore.Dialpad` | Dialpad | [Dialpad](./telephony/dialpad) |

## Compliance

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.DncRegistry` | DNC Registry | [DNC Registry](./modules/dnc-registry) |
| `CrestApps.OrchardCore.DncRegistry.UsaFtc` | USA FTC Do Not Call Registry | [DNC Registry](./modules/dnc-registry) |
| `CrestApps.OrchardCore.DncRegistry.CanadaDncl` | Canada LNNTE-DNCL Registry | [DNC Registry](./modules/dnc-registry) |
| `CrestApps.OrchardCore.DncRegistry.Local` | Local Do Not Call Registry | [DNC Registry](./modules/dnc-registry) |
| `CrestApps.OrchardCore.DncRegistry.Azure` | DNC Registry - Azure Blob Storage | [DNC Registry](./modules/dnc-registry#store-local-registry-files-in-azure-blob-storage) |

## Phone Verification

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.PhoneNumbers` | Phone Numbers Services (dependency only) | [Phone Number Verifications](./modules/phone-number-verifications) |
| `CrestApps.OrchardCore.PhoneNumbers.Verifications` | Phone Number Verifications (dependency only) | [Phone Number Verifications](./modules/phone-number-verifications) |
| `CrestApps.OrchardCore.PhoneNumbers.Verifications.AbstractApi` | AbstractAPI Phone Number Verification | [AbstractAPI](./modules/phone-number-verifications-abstractapi) |
| `CrestApps.OrchardCore.PhoneNumbers.Verifications.Twilio` | Twilio Phone Number Verification | [Twilio](./modules/phone-number-verifications-twilio) |
| `CrestApps.OrchardCore.PhoneNumbers.Verifications.Veriphone` | Veriphone Phone Number Verification | [Veriphone](./modules/phone-number-verifications-veriphone) |

## Content and Content Management

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.ContentFields` | CrestApps Content Fields | [Content Fields](./modules/content-fields) |
| `CrestApps.OrchardCore.ContentAccessControl` | Content Access Control | [Content Access Control](./modules/content-access-control) |
| `CrestApps.OrchardCore.ContentTransfer` | Content Transfer | [Content Transfer](./modules/content-transfer) |
| `CrestApps.OrchardCore.ContentTransfer.OpenXml` | Content Transfer (OpenXml) | [Content Transfer](./modules/content-transfer) |

## Reporting

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.Reports` | Reports | [Reports](./modules/reports) |
| `CrestApps.OrchardCore.Reports.OpenXml` | Reports (OpenXml) | [Reports](./modules/reports) |

## Communication

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.SignalR` | SignalR (Deprecated) | [SignalR](./modules/signalr) |
| `CrestApps.OrchardCore.SignalR.Redis` | SignalR Redis Backplane (Deprecated) | [SignalR](./modules/signalr) |
| `CrestApps.OrchardCore.SignalR.Azure` | SignalR Azure Backplane (Deprecated) | [SignalR](./modules/signalr) |

## Users

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.Users` | Users Core Components (dependency only) | [Users](./modules/users) |
| `CrestApps.OrchardCore.Users.DisplayName` | User Display Name | [Users](./modules/users) |
| `CrestApps.OrchardCore.Users.Avatars` | User Avatar | [Users](./modules/users) |

## Infrastructure, Resources, and Roles

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.Recipes` | CrestApps Recipes | [Recipes](./modules/recipes) |
| `CrestApps.OrchardCore.TimeZones` | Time Zones | [Time Zones](./modules/time-zones) |
| `CrestApps.OrchardCore.Resources` | CrestApps Resources | [Resources](./modules/resources) |
| `CrestApps.OrchardCore.Roles` | Enhanced Roles | [Roles](./modules/roles) |
