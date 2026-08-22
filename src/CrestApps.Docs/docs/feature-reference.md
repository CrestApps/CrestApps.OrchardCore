---
sidebar_position: 3
title: Feature ID Reference
description: Complete reference of manifest-backed module and feature IDs declared under src/Modules.
---

# Feature ID Reference

This page tracks every manifest-backed **module ID** or **feature ID** declared under `src\Modules` and points to the docs page that covers it. IDs marked **Feature** are enabled from the *Features* admin screen (or a `Feature` recipe step); IDs marked **Module** expose no separate toggleable feature beyond the module's default feature, which shares the module ID.

## Artificial Intelligence

| Manifest ID | Name | Kind | Docs |
| --- | --- | --- | --- |
| `CrestApps.OrchardCore.AI` | AI Services | Feature | [AI Services](./ai/overview) |
| `CrestApps.OrchardCore.AI.ConnectionManagement` | AI Connection Management | Feature | [AI Services](./ai/overview) |
| `CrestApps.OrchardCore.AI.Chat.Core` | AI Chat Services | Feature | [AI Services](./ai/overview) |
| `CrestApps.OrchardCore.AI.Chat.Api` | AI Chat WebAPI | Feature | [AI Services](./ai/overview) |
| `CrestApps.OrchardCore.AI.Chat` | AI Chat | Feature | [AI Chat](./ai/chat) |
| `CrestApps.OrchardCore.AI.Chat.AdminWidget` | AI Chat Admin Widget | Feature | [AI Chat](./ai/chat) |
| `CrestApps.OrchardCore.AI.Chat.Analytics` | AI Chat Session Analytics | Feature | [AI Chat Session Analytics](./ai/chat-analytics) |
| `CrestApps.OrchardCore.AI.Chat.Interactions` | AI Chat Interactions | Feature | [AI Chat Interactions](./ai/chat-interactions) |
| `CrestApps.OrchardCore.AI.Chat.Copilot` | AI Copilot Orchestrator | Feature | [Copilot Integration](./ai/copilot) |
| `CrestApps.OrchardCore.AI.Chat.Claude` | AI Claude Orchestrator | Feature | [Claude Integration](./ai/claude) |
| `CrestApps.OrchardCore.AI.Agent` | Orchard Core AI Agent | Module | [AI Agents](./ai/agent) |
| `CrestApps.OrchardCore.AI.Prompting` | AI Prompt Templates | Feature | [AI Prompt Templates](./ai/prompt-templates) |
| `CrestApps.OrchardCore.AI.ToolInstances` | AI Tool Instances | Feature | [AI Tools](./ai/tools) |
| `CrestApps.OrchardCore.AI.A2A` | Agent-to-Agent (A2A) Client | Feature | [A2A Client Integration](./ai/a2a/client) |
| `CrestApps.OrchardCore.AI.A2A.Host` | Agent-to-Agent (A2A) Host | Feature | [A2A Host](./ai/a2a/host) |
| `CrestApps.OrchardCore.AI.Mcp` | Model Context Protocol (MCP) Client | Feature | [MCP Client Integration](./ai/mcp/client) |
| `CrestApps.OrchardCore.AI.Mcp.Stdio` | Model Context Protocol (MCP) Local Client | Feature | [MCP Client Integration](./ai/mcp/client) |
| `CrestApps.OrchardCore.AI.Mcp.Server` | Model Context Protocol (MCP) Server | Feature | [MCP Server](./ai/mcp/server) |
| `CrestApps.OrchardCore.AI.Mcp.Resources.Ftp` | Model Context Protocol (MCP) FTP Resource | Feature | [MCP FTP Resource](./ai/mcp/ftp) |
| `CrestApps.OrchardCore.AI.Mcp.Resources.Sftp` | Model Context Protocol (MCP) SFTP Resource | Feature | [MCP SFTP Resource](./ai/mcp/sftp) |
| `CrestApps.OrchardCore.AI.Documents` | AI Documents | Feature | [AI Documents](./ai/documents/) |
| `CrestApps.OrchardCore.AI.Documents.ChatInteractions` | AI Documents for Chat Interactions | Feature | [AI Documents](./ai/documents/) |
| `CrestApps.OrchardCore.AI.Documents.ChatSessions` | AI Documents for Chat Sessions | Feature | [AI Documents](./ai/documents/) |
| `CrestApps.OrchardCore.AI.Documents.Profiles` | AI Documents for Profiles | Feature | [AI Documents](./ai/documents/) |
| `CrestApps.OrchardCore.AI.Documents.Azure` | AI Documents - Azure Blob Storage | Module | [Azure Blob Storage](./ai/documents/azure-blob-storage) |
| `CrestApps.OrchardCore.AI.Documents.AzureAI` | AI Documents indexing using Azure AI Search | Module | [Azure AI Search](./ai/documents/azure-ai) |
| `CrestApps.OrchardCore.AI.Documents.Elasticsearch` | AI Documents indexing using Elasticsearch | Module | [Elasticsearch](./ai/documents/elasticsearch) |
| `CrestApps.OrchardCore.AI.Documents.OpenXml` | AI Documents (OpenXml) | Module | [OpenXML](./ai/documents/openxml) |
| `CrestApps.OrchardCore.AI.Documents.Pdf` | AI Documents (PDF) | Module | [PDF](./ai/documents/pdf) |
| `CrestApps.OrchardCore.AI.DataSources` | AI Data Sources | Module | [Data Sources](./ai/data-sources/) |
| `CrestApps.OrchardCore.AI.DataSources.AzureAI` | AI Data Sources - Azure AI Search | Module | [Azure AI Search](./ai/data-sources/azure-ai) |
| `CrestApps.OrchardCore.AI.DataSources.Elasticsearch` | AI Data Sources - Elasticsearch | Module | [Elasticsearch](./ai/data-sources/elasticsearch) |
| `CrestApps.OrchardCore.AI.DataSources.PostgreSQL` | AI Data Sources - PostgreSQL | Module | [PostgreSQL](./ai/data-sources/postgresql) |
| `CrestApps.OrchardCore.AI.Memory` | AI Memory | Feature | [AI Memory](./ai/memory) |
| `CrestApps.OrchardCore.AI.Memory.AzureAI` | AI Memory indexing using Azure AI Search | Module | [Azure AI Memory](./ai/memory-azure-ai) |
| `CrestApps.OrchardCore.AI.Memory.Elasticsearch` | AI Memory indexing using Elasticsearch | Module | [Elasticsearch Memory](./ai/memory-elasticsearch) |
| `CrestApps.OrchardCore.AzureAIInference` | Azure AI Inference Chat | Feature | [Azure AI Inference](./ai/providers/azure-ai-inference) |
| `CrestApps.OrchardCore.OpenAI` | OpenAI Chat | Feature | [OpenAI Provider](./ai/providers/openai) |
| `CrestApps.OrchardCore.OpenAI.Azure` | Azure OpenAI Chat | Feature | [Azure OpenAI Provider](./ai/providers/azure-openai) |
| `CrestApps.OrchardCore.Ollama` | Ollama AI Chat | Module | [Ollama Provider](./ai/providers/ollama) |

## Omnichannel

| Manifest ID | Name | Kind | Docs |
| --- | --- | --- | --- |
| `CrestApps.OrchardCore.Omnichannel` | Omnichannel | Feature | [Omnichannel Communications](./omnichannel/) |
| `CrestApps.OrchardCore.Omnichannel.Managements` | Omnichannel Management | Feature | [Management (CRM)](./omnichannel/management) |
| `CrestApps.OrchardCore.Omnichannel.Activities` | Omnichannel Activities | Feature | [Management (CRM)](./omnichannel/management) |
| `CrestApps.OrchardCore.Omnichannel.AzureCommunicationServices` | Omnichannel - Azure Communication Services | Feature | [Azure Communication Services](./omnichannel/azure-communication-services) |
| `CrestApps.OrchardCore.Omnichannel.EventGrid` | Omnichannel - Azure Event Grid | Feature | [Azure Event Grid](./omnichannel/event-grid) |
| `CrestApps.OrchardCore.Omnichannel.Sms` | SMS Omnichannel Automation | Feature | [SMS Automation](./omnichannel/sms) |

## Contact Center

The Contact Center reports (executive, interaction, queue/SLA, agent, transfer, recording, campaign, and subject) and the Orchard Core Workflows bridge are **not** separate features. Reports activate automatically when `CrestApps.OrchardCore.ContactCenter.Queues` and `CrestApps.OrchardCore.Reports` are both enabled; the workflow activities activate automatically when `OrchardCore.Workflows` is enabled alongside Contact Center. See [Agents, Queues & Dialer](./contact-center/agents-queues-dialer) and [Workflows automation](./contact-center/workflows).

| Manifest ID | Name | Kind | Docs |
| --- | --- | --- | --- |
| `CrestApps.OrchardCore.ContactCenter` | Contact Center | Feature | [Contact Center](./contact-center/) |
| `CrestApps.OrchardCore.ContactCenter.Agents` | Contact Center Agents | Feature | [Agents, Queues & Dialer](./contact-center/agents-queues-dialer) |
| `CrestApps.OrchardCore.ContactCenter.Queues` | Contact Center Work Distribution | Feature | [Agents, Queues & Dialer](./contact-center/agents-queues-dialer) |
| `CrestApps.OrchardCore.ContactCenter.Dialer` | Contact Center Outbound Dialer | Feature | [Agents, Queues & Dialer](./contact-center/agents-queues-dialer) |
| `CrestApps.OrchardCore.ContactCenter.Dialer.Paced` | Contact Center Paced Dialing | Feature | [Agents, Queues & Dialer](./contact-center/agents-queues-dialer) |
| `CrestApps.OrchardCore.ContactCenter.Voice` | Contact Center Voice | Feature | [Voice routing](./contact-center/voice-routing) |
| `CrestApps.OrchardCore.ContactCenter.Voice.Media` | Contact Center Voice Media | Feature | [Voice routing](./contact-center/voice-routing) |
| `CrestApps.OrchardCore.ContactCenter.InboundVoice` | Contact Center Inbound Voice | Feature | [Voice routing](./contact-center/voice-routing) |
| `CrestApps.OrchardCore.ContactCenter.Recording` | Contact Center Call Recording | Feature | [Agents, Queues & Dialer](./contact-center/agents-queues-dialer) |
| `CrestApps.OrchardCore.ContactCenter.SecureCapture` | Contact Center Secure Data Capture | Feature | [Agents, Queues & Dialer](./contact-center/agents-queues-dialer) |
| `CrestApps.OrchardCore.ContactCenter.Supervision` | Contact Center Supervision & Live Dashboard | Feature | [Agents, Queues & Dialer](./contact-center/agents-queues-dialer) |
| `CrestApps.OrchardCore.ContactCenter.RealTime` | Contact Center Real-Time | Feature | [Agents, Queues & Dialer](./contact-center/agents-queues-dialer) |

## Telephony

| Manifest ID | Name | Kind | Docs |
| --- | --- | --- | --- |
| `CrestApps.OrchardCore.Telephony` | Telephony | Feature | [Telephony](./telephony/) |
| `CrestApps.OrchardCore.Telephony.SoftPhone` | Telephony Soft Phone | Feature | [Telephony](./telephony/) |
| `CrestApps.OrchardCore.Telnyx` | Telnyx | Feature | [Telnyx](./telephony/telnyx) |
| `CrestApps.OrchardCore.Asterisk` | Asterisk | Feature | [Asterisk](./telephony/asterisk) |
| `CrestApps.OrchardCore.Dialpad` | Dialpad | Feature | [Dialpad](./telephony/dialpad) |

## Phone Numbers

| Manifest ID | Name | Kind | Docs |
| --- | --- | --- | --- |
| `CrestApps.OrchardCore.PhoneNumbers` | Phone Numbers Services | Feature | [Phone Number Verifications](./modules/phone-number-verifications) |
| `CrestApps.OrchardCore.PhoneNumbers.Verifications` | Phone Number Verifications | Feature | [Phone Number Verifications](./modules/phone-number-verifications) |
| `CrestApps.OrchardCore.PhoneNumbers.Verifications.AbstractApi` | AbstractAPI Phone Number Verification | Feature | [AbstractAPI](./modules/phone-number-verifications-abstractapi) |
| `CrestApps.OrchardCore.PhoneNumbers.Verifications.Twilio` | Twilio Phone Number Verification | Feature | [Twilio](./modules/phone-number-verifications-twilio) |
| `CrestApps.OrchardCore.PhoneNumbers.Verifications.Veriphone` | Veriphone Phone Number Verification | Feature | [Veriphone](./modules/phone-number-verifications-veriphone) |

## Reports

| Manifest ID | Name | Kind | Docs |
| --- | --- | --- | --- |
| `CrestApps.OrchardCore.Reports` | Reports | Feature | [Reports](./modules/reports) |
| `CrestApps.OrchardCore.Reports.OpenXml` | Reports (OpenXml) | Feature | [Reports](./modules/reports) |

## Standard modules

| Manifest ID | Name | Kind | Docs |
| --- | --- | --- | --- |
| `CrestApps.OrchardCore.ContentTransfer` | Content Transfer | Module | [Content Transfer](./modules/content-transfer) |
| `CrestApps.OrchardCore.ContentTransfer.OpenXml` | Content Transfer (OpenXml) | Module | [Content Transfer](./modules/content-transfer) |
| `CrestApps.OrchardCore.ContentFields` | CrestApps Content Fields | Feature | [Content Fields](./modules/content-fields) |
| `CrestApps.OrchardCore.ContentAccessControl` | Content Access Control | Module | [Content Access Control](./modules/content-access-control) |
| `CrestApps.OrchardCore.DncRegistry` | DNC Registry | Feature | [DNC Registry](./modules/dnc-registry) |
| `CrestApps.OrchardCore.DncRegistry.UsaFtc` | USA FTC Do Not Call Registry | Feature | [DNC Registry](./modules/dnc-registry) |
| `CrestApps.OrchardCore.DncRegistry.CanadaDncl` | Canada LNNTE-DNCL Registry | Feature | [DNC Registry](./modules/dnc-registry) |
| `CrestApps.OrchardCore.DncRegistry.Local` | Local Do Not Call Registry | Feature | [DNC Registry](./modules/dnc-registry) |
| `CrestApps.OrchardCore.DncRegistry.Azure` | DNC Registry - Azure Blob Storage | Module | [DNC Registry](./modules/dnc-registry) |
| `CrestApps.OrchardCore.Recipes` | CrestApps Recipes | Module | [Recipes](./modules/recipes) |
| `CrestApps.OrchardCore.Resources` | CrestApps Resources | Module | [Resources](./modules/resources) |
| `CrestApps.OrchardCore.Roles` | Enhanced Roles | Module | [Roles](./modules/roles) |
| `CrestApps.OrchardCore.TimeZones` | Time Zones | Feature | [Time Zones](./modules/time-zones) |
| `CrestApps.OrchardCore.SignalR` | SignalR (Deprecated) | Feature | [SignalR](./modules/signalr) |
| `CrestApps.OrchardCore.SignalR.Redis` | SignalR Redis Backplane (Deprecated) | Feature | [SignalR](./modules/signalr) |
| `CrestApps.OrchardCore.SignalR.Azure` | SignalR Azure Backplane (Deprecated) | Feature | [SignalR](./modules/signalr) |
| `CrestApps.OrchardCore.Users` | Users Core Components | Feature | [Users](./modules/users) |
| `CrestApps.OrchardCore.Users.DisplayName` | User Display Name | Feature | [Users](./modules/users) |
| `CrestApps.OrchardCore.Users.Avatars` | User Avatar | Feature | [Users](./modules/users) |
