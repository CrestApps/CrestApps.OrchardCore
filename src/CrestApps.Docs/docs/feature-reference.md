---
sidebar_position: 3
title: Feature ID Reference
description: Complete reference of the manifest-backed feature IDs declared by the modules in this repository.
user_manual:
  - user-manual/getting-started/features-and-settings
---

# Feature ID Reference

Every feature ID declared by a module under `src/Modules`, grouped by the **Category** the manifest gives it.
That category is the heading the feature appears under in **Tools** → **Features**, so this page reads in the same
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
| `CrestApps.OrchardCore.AI.Personas` | AI Personas | [AI Personas](./ai/personas) |
| `CrestApps.OrchardCore.AI.Chat.Core` | AI Chat Services (dependency only) | [AI Services](./ai/overview) |
| `CrestApps.OrchardCore.AI.Chat.Api` | AI Chat WebAPI | [AI Services](./ai/overview) |
| `CrestApps.OrchardCore.AI.Chat` | AI Chat | [AI Chat](./ai/chat) |
| `CrestApps.OrchardCore.AI.Chat.AdminWidget` | AI Chat Admin Widget | [AI Chat](./ai/chat) |
| `CrestApps.OrchardCore.AI.Chat.Analytics` | AI Chat Session Analytics | [AI Chat Session Analytics](./ai/chat-analytics) |
| `CrestApps.OrchardCore.AI.UsageAnalytics` | AI Usage Analytics | [AI Usage Analytics](./ai/usage-analytics) |
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
| `CrestApps.OrchardCore.AI.WebMcp` | WebMCP | [WebMCP](./ai/mcp/webmcp) |

## Artificial Intelligence - A2A

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.AI.A2A` | Agent-to-Agent (A2A) Client | [A2A Client](./ai/a2a/client) |
| `CrestApps.OrchardCore.AI.A2A.Host` | Agent-to-Agent (A2A) Host | [A2A Host](./ai/a2a/host) |

## Contact Center

The Contact Center reports (executive, interaction, queue/SLA, agent, transfer, recording, campaign, and subject) and the Orchard Core Workflows bridge are **not** separate features. Reports activate automatically when `CrestApps.OrchardCore.ContactCenter.Queues` and `CrestApps.OrchardCore.Reports` are both enabled; the workflow activities activate automatically when `OrchardCore.Workflows` is enabled alongside Contact Center. See [Agents, Queues & Dialer](./contact-center/agents-queues-dialer) and [Workflows automation](./contact-center/workflows).

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.Omnichannel` | Omnichannel | [Omnichannel Communications](./omnichannel/) |
| `CrestApps.OrchardCore.Omnichannel.AzureCommunicationServices` | Omnichannel - Azure Communication Services | [Azure Communication Services](./omnichannel/azure-communication-services) |
| `CrestApps.OrchardCore.Omnichannel.EventGrid` | Omnichannel - Azure Event Grid | [Azure Event Grid](./omnichannel/event-grid) |
| `CrestApps.OrchardCore.Omnichannel.Activities` | Omnichannel Activities | [Management (CRM)](./omnichannel/management) |
| `CrestApps.OrchardCore.Omnichannel.Managements` | Omnichannel Management | [Management (CRM)](./omnichannel/management) |
| `CrestApps.OrchardCore.Omnichannel.Sms` | SMS Omnichannel Automation | [SMS Automation](./omnichannel/sms) |
| `CrestApps.OrchardCore.Omnichannel.ChannelEndpoints` | Omnichannel Channel Endpoints (dependency only) | [Management (CRM)](./omnichannel/management#omnichannel-address) |
| `CrestApps.OrchardCore.Omnichannel.Messaging` | Omnichannel Messaging Workspace | [Messaging Workspace](./omnichannel/messaging-workspace) |
| `CrestApps.OrchardCore.Omnichannel.Messaging.Sms` | SMS Messaging Channel | [Messaging Workspace](./omnichannel/messaging-workspace#setting-up-sms) |
| `CrestApps.OrchardCore.ContactCenter` | Contact Center | [Contact Center](./contact-center/) |
| `CrestApps.OrchardCore.ContactCenter.AgentServices` | Contact Center Agent Services (dependency only) | [Agents, Queues & Dialer](./contact-center/agents-queues-dialer) |
| `CrestApps.OrchardCore.ContactCenter.Agents` | Contact Center Agents | [Agents, Queues & Dialer](./contact-center/agents-queues-dialer) |
| `CrestApps.OrchardCore.ContactCenter.AgentEntitlements` | Contact Center Agent Entitlements | [Agents, Queues & Dialer](./contact-center/agents-queues-dialer) |
| `CrestApps.OrchardCore.ContactCenter.Queues` | Contact Center Work Distribution | [Agents, Queues & Dialer](./contact-center/agents-queues-dialer) |
| `CrestApps.OrchardCore.ContactCenter.IntervalStatistics` | Contact Center Interval Statistics | [Report catalog](./contact-center/report-catalog#interval-statistics) |
| `CrestApps.OrchardCore.ContactCenter.Dialer` | Contact Center Outbound Dialer | [Agents, Queues & Dialer](./contact-center/agents-queues-dialer) |
| `CrestApps.OrchardCore.ContactCenter.Dialer.Paced` | Contact Center Paced Dialing | [Agents, Queues & Dialer](./contact-center/agents-queues-dialer) |
| `CrestApps.OrchardCore.ContactCenter.Voice` | Contact Center Voice (dependency only) | [Voice routing](./contact-center/voice-routing) |
| `CrestApps.OrchardCore.ContactCenter.Voice.Media` | Contact Center Voice Media | [Voice routing](./contact-center/voice-routing) |
| `CrestApps.OrchardCore.ContactCenter.EntryPoints` | Contact Center Inbound Entry Points | [Entry points](./user-manual/entry-points-and-ivr) |
| `CrestApps.OrchardCore.ContactCenter.InboundVoice` | Contact Center Inbound Voice | [Voice routing](./contact-center/voice-routing) |
| `CrestApps.OrchardCore.ContactCenter.Recording` | Contact Center Call Recording | [Agents, Queues & Dialer](./contact-center/agents-queues-dialer) |
| `CrestApps.OrchardCore.ContactCenter.SecureCapture` | Contact Center Secure Data Capture | [Agents, Queues & Dialer](./contact-center/agents-queues-dialer) |
| `CrestApps.OrchardCore.ContactCenter.Supervision` | Contact Center Supervision & Live Dashboard | [Agents, Queues & Dialer](./contact-center/agents-queues-dialer) |
| `CrestApps.OrchardCore.ContactCenter.RealTime` | Contact Center Real-Time (dependency only) | [Agents, Queues & Dialer](./contact-center/agents-queues-dialer) |
| `CrestApps.OrchardCore.Telnyx.AiVoice` | Telnyx AI Voice Agent | [Telnyx AI Voice Agent](./telephony/telnyx#telnyx-ai-voice-agent) |
| `CrestApps.OrchardCore.ContactCenter.BusinessHours` | Contact Center Business Hours | [Business hours and overflow](./contact-center/agents-queues-dialer#business-hours-and-overflow) |
| `CrestApps.OrchardCore.ContactCenter.ProviderInbox` | Provider Webhook Inbox (dependency only, in the Webhooks module) | [Webhooks](./modules/webhooks#provider-webhook-inbox) |
| `CrestApps.OrchardCore.ContactCenter.Recording.Core` | Contact Center Recording Governance (dependency only) | [Recording governance](./contact-center/#recording-governance) |
| `CrestApps.OrchardCore.Omnichannel.Messaging.RoutedDistribution` | Omnichannel Messaging Routed Distribution | [Messaging Workspace](./omnichannel/messaging-workspace) |
| `CrestApps.OrchardCore.Omnichannel.Voice` | Automated Voice | [Telnyx AI Voice Agent](./telephony/telnyx#telnyx-ai-voice-agent) |

## Telephony

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.Telephony` | Telephony | [Telephony](./telephony/) |
| `CrestApps.OrchardCore.Telephony.SoftPhone` | Telephony Soft Phone | [Telephony](./telephony/#soft-phone-widget) |
| `CrestApps.OrchardCore.Telephony.SoftPhone.Core` | Telephony Soft Phone Core (dependency only) | [Telephony](./telephony/) |
| `CrestApps.OrchardCore.Telephony.SoftPhone.Extension` | Telephony Soft Phone Extension | [Telephony](./telephony/#standalone-soft-phone-page-and-browser-extension) |
| `CrestApps.OrchardCore.Telephony.Azure` | Telephony - Azure Blob Storage | [Recording — Azure Blob Storage](./telephony/recording-azure-blob-storage) |
| `CrestApps.OrchardCore.Omnichannel.Messaging.Azure` | Omnichannel Messaging - Azure Blob Storage | [Messaging — Azure Blob Storage](./omnichannel/messaging-azure-blob-storage) |
| `CrestApps.OrchardCore.Telnyx` | Telnyx | [Telnyx](./telephony/telnyx) |
| `CrestApps.OrchardCore.Asterisk` | Asterisk | [Asterisk](./telephony/asterisk) |

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
| `CrestApps.OrchardCore.ContentFields.Address` | Address Field | [Content Fields](./modules/content-fields#addressfield) |
| `CrestApps.OrchardCore.ContentFields.ConditionalFields` | Conditional Fields | [Content Fields](./modules/content-fields#conditional-fields) |
| `CrestApps.OrchardCore.ContentAccessControl` | Content Access Control | [Content Access Control](./modules/content-access-control) |
| `CrestApps.OrchardCore.ContentTransfer` | Content Transfer | [Content Transfer](./modules/content-transfer) |
| `CrestApps.OrchardCore.ContentTransfer.OpenXml` | Content Transfer (OpenXml) | [Content Transfer](./modules/content-transfer) |
| `CrestApps.OrchardCore.ContentTransfer.Azure` | Content Transfer - Azure Blob Storage | [Content Transfer](./modules/content-transfer#store-import-and-export-files-in-azure-blob-storage) |
| `CrestApps.OrchardCore.ProtectedFiles.Azure` | Protected Files - Azure Blob Storage | [Protected Files](./modules/protected-files#store-protected-files-in-azure-blob-storage) |
| `CrestApps.OrchardCore.Seo` | Advanced SEO | [Advanced SEO](./modules/advanced-seo) |
| `CrestApps.OrchardCore.NLWeb` | NLWeb | [NLWeb](./modules/nlweb) |
| `CrestApps.OrchardCore.Accounts` | Accounts | [Accounts](./modules/accounts) |
| `CrestApps.OrchardCore.Accounts.Members` | Account Members | [Accounts](./modules/accounts#account-members) |
| `CrestApps.OrchardCore.Accounts.Locations` | Account Locations | [Accounts](./modules/accounts#account-locations) |
| `CrestApps.OrchardCore.Accounts.EmailDomains` | Account E-mail Domains | [Accounts](./modules/accounts#account-e-mail-domains) |
| `CrestApps.OrchardCore.Addresses` | Addresses | [Addresses](./modules/addresses) |
| `CrestApps.OrchardCore.Addresses.Countries` | Address Countries | [Addresses](./modules/addresses#country-lists) |
| `CrestApps.OrchardCore.Addresses.GooglePlaces` | Address Suggestions (Google Places) | [Addresses](./modules/addresses#address-suggestions) |
| `CrestApps.OrchardCore.Products` | Products | [Products](./modules/products) |
| `CrestApps.OrchardCore.Products.Elasticsearch` | Product Search (Elasticsearch) | [Products](./modules/products#search-servers) |
| `CrestApps.OrchardCore.Products.Lucene` | Product Search (Lucene) | [Products](./modules/products#search-servers) |
| `CrestApps.OrchardCore.Products.AzureAI` | Product Search (Azure AI Search) | [Products](./modules/products#search-servers) |
| `CrestApps.OrchardCore.Wizard` | Wizard | [Wizard](./modules/wizard) |

## Reporting

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.Reports` | Reports | [Reports](./modules/reports) |
| `CrestApps.OrchardCore.Reports.OpenXml` | Reports (OpenXml) | [Reports](./modules/reports) |
| `CrestApps.OrchardCore.Reports.Pdf` | Reports (PDF) | [Reports](./modules/reports) |
| `CrestApps.OrchardCore.Reports.Builder` | Report Builder | [Report Builder](./modules/report-builder/) |

## Calendar

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.Calendars` | Calendars | [Calendars](./modules/calendars) |
| `CrestApps.OrchardCore.Calendars.Teams` | Team Calendars | [Calendars](./modules/calendars#calendar-types) |
| `CrestApps.OrchardCore.Calendars.Resources` | Resource Calendars | [Calendars](./modules/calendars#conflicts-and-concurrency) |
| `CrestApps.OrchardCore.Calendars.Office` | Office Calendars | [Calendars](./modules/calendars#calendar-types) |
| `CrestApps.OrchardCore.Calendars.Reminders` | Calendar Reminders | [Calendars](./modules/calendars#reminders) |
| `CrestApps.OrchardCore.Calendars.Invitations` | Calendar Invitations | [Calendars](./modules/calendars#invitations) |
| `CrestApps.OrchardCore.Calendars.Feeds` | Calendar Feeds | [Calendars](./modules/calendars#icalendar-feeds-import-and-export) |
| `CrestApps.OrchardCore.Calendars.External` | External Calendars | [Calendars](./modules/calendars#external-calendars) |
| `CrestApps.OrchardCore.Calendars.Api` | Calendars API | [Calendars](./modules/calendars#http-api) |
| `CrestApps.OrchardCore.Scheduling` | Scheduling | [Scheduling](./modules/scheduling) |
| `CrestApps.OrchardCore.Scheduling.SelfService` | Scheduling Self-Service | [Scheduling](./modules/scheduling#self-service) |
| `CrestApps.OrchardCore.Scheduling.Api` | Scheduling API | [Scheduling](./modules/scheduling#http-api) |

## Workforce

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.Skills` | Skills | [Skills](./modules/skills) |
| `CrestApps.OrchardCore.Skills.IssuedCertificates` | Skills: Issued Certificates | [Skills](./modules/skills#issued-certificates) |
| `CrestApps.OrchardCore.Teams` | Teams | [Teams](./modules/teams) |
| `CrestApps.OrchardCore.Agreements` | Agreements | [Agreements](./modules/agreements) |
| `CrestApps.OrchardCore.BusinessHours` | Business Hours | [Business Hours](./modules/business-hours#the-business-hours-feature) |
| `CrestApps.OrchardCore.ServiceLevels` | Service Levels | [Service Levels](./modules/service-levels) |
| `CrestApps.OrchardCore.Timeline` | Timeline | [Timeline](./modules/timeline) |
| `CrestApps.OrchardCore.Tickets` | Tickets | [Tickets](./modules/tickets) |
| `CrestApps.OrchardCore.Tickets.Api` | Tickets API | [Tickets](./modules/tickets/api) |
| `CrestApps.OrchardCore.Tickets.Portal` | Tickets Portal | [Tickets](./modules/tickets/portal-and-web-form#portal) |
| `CrestApps.OrchardCore.Tickets.WebForm` | Ticket Web Form | [Tickets](./modules/tickets/portal-and-web-form#web-form) |
| `CrestApps.OrchardCore.KnowledgeBase` | Knowledge Base | [Knowledge Base](./modules/knowledge-base) |
| `CrestApps.OrchardCore.Attachments` | Attachments | [Attachments](./modules/attachments) |
| `CrestApps.OrchardCore.Workforce` | Workforce | [Workforce](./modules/workforce) |
| `CrestApps.OrchardCore.Workforce.ShiftPlanning` | Workforce Shift Planning | [Workforce](./modules/workforce#shift-planning) |
| `CrestApps.OrchardCore.Workforce.ContactCenter` | Workforce for Contact Center | [Workforce for Contact Center](./modules/workforce-contact-center) |
| `CrestApps.OrchardCore.Workforce.ContactCenter.SignInPunches` | Contact Center Sign-in Punches | [Workforce for Contact Center](./modules/workforce-contact-center#sign-in-punches) |
| `CrestApps.OrchardCore.Workforce.ContactCenter.TimeTracker` | Contact Center Time Tracker | [Workforce for Contact Center](./modules/workforce-contact-center#agent-states-in-time-tracker) |
| `CrestApps.OrchardCore.Workforce.Forecasting` | Workforce Forecasting | [Workforce for Contact Center](./modules/workforce-contact-center#forecasting) |
| `CrestApps.OrchardCore.Workforce.Intraday` | Workforce Intraday | [Workforce for Contact Center](./modules/workforce-contact-center#intraday) |
| `CrestApps.OrchardCore.Workforce.Adherence` | Workforce Adherence | [Workforce for Contact Center](./modules/workforce-contact-center#adherence) |

## Finance

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.Approvals` | Approvals | [Approvals](./modules/approvals) |
| `CrestApps.OrchardCore.Payroll.PaySetup` | Pay Setup | [Payroll](./modules/payroll) |
| `CrestApps.OrchardCore.Payroll` | Payroll | [Payroll](./modules/payroll) |
| `CrestApps.OrchardCore.Payroll.MultipleEmployers` | Multiple Employers | [Payroll](./modules/payroll#multiple-employers) |
| `CrestApps.OrchardCore.Payroll.UnitedStates` | United States Payroll Filings | [Payroll](./modules/payroll#united-states) |
| `CrestApps.OrchardCore.Payroll.Canada` | Canada Payroll Filings | [Payroll](./modules/payroll#canada) |
| `CrestApps.OrchardCore.Taxation.IncomeTax` | Income Tax | [Taxation](./modules/taxation#income-tax) |
| `CrestApps.OrchardCore.Taxation.IncomeTax.UnitedStates` | United States Income Tax | [Taxation](./modules/taxation#country-packs) |
| `CrestApps.OrchardCore.Taxation.IncomeTax.Canada` | Canada Income Tax | [Taxation](./modules/taxation#country-packs) |
| `CrestApps.OrchardCore.Banking` | Bank Accounts | [Banking](./modules/banking#bank-accounts) |
| `CrestApps.OrchardCore.Banking.Checks` | Checks | [Banking](./modules/banking#checks) |
| `CrestApps.OrchardCore.Banking.Disbursements` | Disbursements | [Banking](./modules/banking#disbursements-and-direct-deposit) |
| `CrestApps.OrchardCore.Banking.DirectDeposit` | Direct Deposit | [Banking](./modules/banking#providers) |
| `CrestApps.OrchardCore.Plaid` | Plaid Bank Account Linking | [Plaid](./modules/plaid) |

## Geo

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.Geo` | Geo, Maps and Territories | [Geo](./modules/geo) |
| `CrestApps.OrchardCore.Geo.AzureMaps` | Azure Maps | [Geo](./modules/geo#providers) |
| `CrestApps.OrchardCore.Geo.GoogleMaps` | Google Maps | [Geo](./modules/geo#providers) |
| `CrestApps.OrchardCore.Geo.Osrm` | OSRM | [Geo](./modules/geo#providers) |

## Field service

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.Assets` | Assets | [Assets](./modules/assets) |
| `CrestApps.OrchardCore.Assets.Fleet` | Fleet | [Assets](./modules/assets#fleet) |
| `CrestApps.OrchardCore.Dispatch` | Dispatch | [Dispatch](./modules/dispatch) |
| `CrestApps.OrchardCore.TimeTracker` | Time Tracker | [Time Tracker](./modules/time-tracker) |
| `CrestApps.OrchardCore.WorkOrders` | Work Orders | [Work Orders](./modules/work-orders) |
| `CrestApps.OrchardCore.WorkOrders.Agreements` | Service Agreements | [Work Orders](./modules/work-orders#service-agreements) |

## Learning

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.Learning` | Learning | [Learning](./modules/learning) |
| `CrestApps.OrchardCore.Learning.LearnerPages` | Learner Pages | [Learning](./modules/learning#learner-pages) |
| `CrestApps.OrchardCore.Learning.SelfEnrollment` | Self Enrollment | [Learning](./modules/learning#learner-pages) |
| `CrestApps.OrchardCore.Learning.CourseCategories` | Course Categories | [Learning](./modules/learning#course-categories) |
| `CrestApps.OrchardCore.Learning.Cohorts` | Enrollment by Cohort | [Learning](./modules/learning#enrollment-by-cohort) |
| `CrestApps.OrchardCore.Learning.Assessments` | Learning Assessments | [Learning Assessments](./modules/learning-assessments) |
| `CrestApps.OrchardCore.Learning.Assessments.Gradebook` | Learning Gradebook | [Learning Assessments](./modules/learning-assessments) |
| `CrestApps.OrchardCore.Learning.Assessments.QtiImport` | Learning QTI Import | [Learning Assessments](./modules/learning-assessments#qti-import) |
| `CrestApps.OrchardCore.Learning.Assessments.MoodleImport` | Learning Moodle Import | [Learning Assessments](./modules/learning-assessments#moodle-import) |
| `CrestApps.OrchardCore.Learning.AI` | Learning Assistant | [Learning AI](./modules/learning-ai) |
| `CrestApps.OrchardCore.Learning.AI.Authoring` | Learning Authoring Assistant | [Learning AI](./modules/learning-ai#author-prompts) |
| `CrestApps.OrchardCore.Learning.Certifications` | Learning Certifications | [Learning Certifications](./modules/learning-certifications) |
| `CrestApps.OrchardCore.Learning.Classes` | Learning Classes | [Learning Classes](./modules/learning-classes) |
| `CrestApps.OrchardCore.Learning.Commerce` | Learning Commerce | [Learning Commerce](./modules/learning-commerce) |
| `CrestApps.OrchardCore.Learning.Coursework` | Learning Coursework | [Learning Coursework](./modules/learning-coursework) |
| `CrestApps.OrchardCore.Learning.Packages.Scorm` | Learning SCORM Packages | [Learning Packages](./modules/learning-packages) |
| `CrestApps.OrchardCore.Learning.Packages.Cmi5` | Learning cmi5 Packages | [Learning Packages](./modules/learning-packages#cmi5) |
| `CrestApps.OrchardCore.Learning.Xapi` | Learning Record Store | [Learning Record Store](./modules/learning-xapi) |
| `CrestApps.OrchardCore.Learning.Lti` | External Tools | [Learning External Tools](./modules/learning-lti) |
| `CrestApps.OrchardCore.Learning.Credentials` | Digital Credentials | [Learning Digital Credentials](./modules/learning-credentials) |
| `CrestApps.OrchardCore.Learning.Programs` | Learning Paths | [Learning Programs](./modules/learning-programs) |
| `CrestApps.OrchardCore.Learning.Programs.RequiredLearning` | Required Learning | [Learning Programs](./modules/learning-programs#required-learning) |
| `CrestApps.OrchardCore.Learning.Programs.TeamLearning` | Team Learning | [Learning Programs](./modules/learning-programs#team-learning) |

## Data connectors

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.DataConnectors` | Data Connectors | [Data Connectors](./modules/data-connectors) |
| `CrestApps.OrchardCore.DataConnectors.Http` | HTTP API Data Connector | [Data Connectors](./modules/data-connectors#http-api) |
| `CrestApps.OrchardCore.DataConnectors.Elasticsearch` | Elasticsearch Data Connector | [Data Connectors](./modules/data-connectors#elasticsearch) |
| `CrestApps.OrchardCore.DataConnectors.Files` | Media Files Data Connector | [Data Connectors](./modules/data-connectors#media-files) |
| `CrestApps.OrchardCore.DataConnectors.SqlServer` | SQL Server Data Connector | [Data Connectors](./modules/data-connectors#sql-server) |

## Commerce

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.Customers` | Customers | [Customers](./modules/customers) |
| `CrestApps.OrchardCore.Commerce.Foundation` | Commerce Foundation (dependency only) | [Commerce](./modules/commerce#the-commerce-foundation-and-commerce-features) |
| `CrestApps.OrchardCore.Commerce` | Commerce (dependency only) | [Commerce](./modules/commerce#the-commerce-foundation-and-commerce-features) |
| `CrestApps.OrchardCore.Checkout` | Checkout | [Checkout](./modules/checkout) |
| `CrestApps.OrchardCore.Markets` | Markets | [Markets](./modules/markets) |
| `CrestApps.OrchardCore.Accounts.B2B` | Accounts B2B | [Accounts B2B](./modules/accounts-b2b) |
| `CrestApps.OrchardCore.Marketing` | Marketing | [Marketing](./modules/marketing) |
| `CrestApps.OrchardCore.Carts.Wishlists` | Wishlists | [Carts and Orders](./modules/carts-and-orders#wishlists) |
| `CrestApps.OrchardCore.Reports.Subscriptions` | Report Subscriptions | [Reports](./modules/reports#report-subscriptions) |
| `CrestApps.OrchardCore.Products.Api` | Catalog API | [Products](./modules/products#api) |
| `CrestApps.OrchardCore.Carts.Api` | Cart API | [Carts and Orders](./modules/carts-and-orders#api) |
| `CrestApps.OrchardCore.Checkout.Api` | Checkout API | [Checkout](./modules/checkout#the-commerce-api) |
| `CrestApps.OrchardCore.Orders.Api` | Orders API | [Carts and Orders](./modules/carts-and-orders#api) |
| `CrestApps.OrchardCore.Customers.Api` | Customers API | [Customers](./modules/customers#api) |
| `CrestApps.OrchardCore.Shipping.Api` | Shipping API | [Shipping](./modules/shipping#api) |
| `CrestApps.OrchardCore.Fulfillment` | Fulfillment Partners | [Fulfillment Partners](./modules/fulfillment-partners) |
| `CrestApps.OrchardCore.Inventory.Api` | Inventory API | [Inventory](./modules/inventory#api) |
| `CrestApps.OrchardCore.Commerce.Webhooks` | Commerce Webhooks | [Commerce Webhooks](./modules/commerce-webhooks) |
| `CrestApps.OrchardCore.AgenticCommerce` | Agentic Commerce | [Agentic Commerce](./modules/agentic-commerce) |
| `CrestApps.OrchardCore.AgenticCommerce.Ucp` | Universal Commerce Protocol | [Agentic Commerce](./modules/agentic-commerce#ucp-profile-and-negotiation) |
| `CrestApps.OrchardCore.AgenticCommerce.Ucp.IdentityLinking` | UCP Account Linking | [Agentic Commerce](./modules/agentic-commerce#account-linking-ucp) |
| `CrestApps.OrchardCore.AgenticCommerce.Ucp.Catalog` | UCP Catalog and Cart | [Agentic Commerce](./modules/agentic-commerce#catalog-and-cart-ucp) |
| `CrestApps.OrchardCore.AgenticCommerce.Ucp.Mcp` | UCP over MCP | [Agentic Commerce](./modules/agentic-commerce#ucp-over-mcp) |
| `CrestApps.OrchardCore.AgenticCommerce.Ucp.A2a` | UCP over A2A | [Agentic Commerce](./modules/agentic-commerce#ucp-over-a2a) |
| `CrestApps.OrchardCore.AgenticCommerce.Acp` | Agentic Commerce Protocol | [Agentic Commerce](./modules/agentic-commerce#acp-discovery-and-versions) |
| `CrestApps.OrchardCore.AgenticCommerce.Acp.Mcp` | ACP over MCP | [Agentic Commerce](./modules/agentic-commerce#acp-over-mcp) |
| `CrestApps.OrchardCore.AgenticCommerce.Feeds` | Agent Product Feeds | [Agentic Commerce](./modules/agentic-commerce#agent-product-feeds) |
| `CrestApps.OrchardCore.AgenticCommerce.Ap2` | Agent Payments Protocol (Preview) | [Agentic Commerce](./modules/agentic-commerce#ap2-mandates-preview) |
| `CrestApps.OrchardCore.Webhooks` | Webhooks | [Webhooks](./modules/webhooks#outbound-webhooks) |
| `CrestApps.OrchardCore.PointOfSale` | Point of Sale | [Point of Sale](./modules/point-of-sale) |
| `CrestApps.OrchardCore.PointOfSale.WebRegister` | Point of Sale Web Register | [Point of Sale](./modules/point-of-sale#web-register) |
| `CrestApps.OrchardCore.PayLater` | Pay Later | [Pay Later](./modules/pay-later) |
| `CrestApps.OrchardCore.Receipts` | Receipts | [Receipts](./modules/receipts) |
| `CrestApps.OrchardCore.BusinessDocuments` | Business Documents | [Business Documents](./modules/business-documents) |
| `CrestApps.OrchardCore.BusinessDocuments.Pdf` | Business Documents PDF | [Business Documents](./modules/business-documents#the-pdf) |
| `CrestApps.OrchardCore.Invoicing` | Invoicing | [Invoicing](./modules/invoicing) |
| `CrestApps.OrchardCore.Invoicing.Estimates` | Invoicing Estimates | [Invoicing](./modules/invoicing#estimates) |
| `CrestApps.OrchardCore.Invoicing.Api` | Invoicing API | [Invoicing](./modules/invoicing#api) |
| `CrestApps.OrchardCore.Invoicing.Billing` | Invoicing Billing | [Invoicing](./modules/invoicing#billing-work-and-costs), [usage](./modules/invoicing#usage-meters-and-tiers) |
| `CrestApps.OrchardCore.Invoicing.Collections` | Invoicing Collections | [Invoicing](./modules/invoicing#late-fees-and-debit-notes) |
| `CrestApps.OrchardCore.Invoicing.Recurring` | Invoicing Recurring | [Invoicing](./modules/invoicing#recurring-plans) |
| `CrestApps.OrchardCore.ExchangeRates` | Exchange Rates | [Exchange Rates](./modules/exchange-rates) |
| `CrestApps.OrchardCore.Taxation` | Sales Tax | [Taxation](./modules/taxation) |
| `CrestApps.OrchardCore.Taxation.Tables` | Tax Tables | [Taxation](./modules/taxation#features) |
| `CrestApps.OrchardCore.Taxation.TaxJar` | TaxJar | [Taxation](./modules/taxation#taxjar) |
| `CrestApps.OrchardCore.Taxation.Returns` | Tax Returns | [Taxation](./modules/taxation#tax-returns) |
| `CrestApps.OrchardCore.Taxation.EFiling` | Tax E-Filing | [Taxation](./modules/taxation#e-filing) |
| `CrestApps.OrchardCore.Transactions` | Transactions | [Transactions](./modules/transactions) |
| `CrestApps.OrchardCore.Transactions.Notification` | Transaction Reminders | [Transactions](./modules/transactions) |
| `CrestApps.OrchardCore.Transactions.Receipts` | Send Payment Receipts | [Transactions](./modules/transactions#payment-receipts) |
| `CrestApps.OrchardCore.Transactions.PaymentTerms` | Payment Terms | [Transactions](./modules/transactions#payment-terms) |

## Payment Providers

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.Stripe` | Stripe | [Payments](./modules/payments) |
| `CrestApps.OrchardCore.Checkout.TestProvider` | Test payments | [Checkout](./modules/checkout#test-payments) |

## Subscriptions

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.Subscriptions` | Subscriptions | [Subscriptions](./modules/subscriptions) |
| `CrestApps.OrchardCore.Subscriptions.Tenants` | Subscriptions - Sites | [Subscriptions](./modules/subscriptions) |
| `CrestApps.OrchardCore.Subscriptions.Installments` | Subscriptions - Installment Plans | [Installment Plans](./modules/installment-plans) |

## Communication

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.SignalR` | SignalR (Deprecated) | [SignalR](./modules/signalr) |
| `CrestApps.OrchardCore.SignalR.Redis` | SignalR Redis Backplane (Deprecated) | [SignalR](./modules/signalr) |
| `CrestApps.OrchardCore.SignalR.Azure` | SignalR Azure Backplane (Deprecated) | [SignalR](./modules/signalr) |
| `CrestApps.OrchardCore.Telnyx.Sms` | Telnyx SMS | [Telnyx SMS](./telephony/telnyx#telnyx-sms) |
| `CrestApps.OrchardCore.Outreach` | Outreach (dependency only) | [Outreach](./modules/outreach) |

## Users

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.Users` | Users Core Components (dependency only) | [Users](./modules/users) |
| `CrestApps.OrchardCore.Users.DisplayName` | User Display Name | [Users](./modules/users) |
| `CrestApps.OrchardCore.Users.Avatars` | User Avatar | [Users](./modules/users) |
| `CrestApps.OrchardCore.Users.Audiences` | User Audiences | [Users](./modules/users#user-audiences) |
| `CrestApps.OrchardCore.Users.Cohorts` | Cohorts | [Users](./modules/users#cohorts) |

## Multi-Tenancy

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.TenantHierarchy.Platform` | Tenant Hierarchy Platform (Default tenant only) | [Tenant Hierarchy](./modules/tenant-hierarchy) |
| `CrestApps.OrchardCore.TenantHierarchy.Parent` | Parent Tenant (turned on by the platform) | [Tenant Hierarchy](./modules/tenant-hierarchy) |
| `CrestApps.OrchardCore.TenantHierarchy.Child` | Child Tenant (turned on by the platform) | [Tenant Hierarchy](./modules/tenant-hierarchy) |

## Infrastructure, Resources, and Roles

| Feature ID | Name | Docs |
| --- | --- | --- |
| `CrestApps.OrchardCore.Recipes` | CrestApps Recipes | [Recipes](./modules/recipes) |
| `CrestApps.OrchardCore.TimeZones` | Time Zones | [Time Zones](./modules/time-zones) |
| `CrestApps.OrchardCore.Resources` | CrestApps Resources | [Resources](./modules/resources) |
| `CrestApps.OrchardCore.Roles` | Enhanced Roles | [Roles](./modules/roles) |
| `CrestApps.OrchardCore.WebSockets` | WebSockets (dependency only) | [WebSockets](./modules/websockets) |

## Settings groups and the permissions that open them

Orchard Core's settings screen (`/Admin/Settings/{groupId}`) authorizes `SettingsPermissions.ManageGroupSettings` with the group id as the resource, and `SiteSettingsAuthorizationHandler` grants it only through the permissions registered for that group with `services.AddSiteSettingsPermission(groupId, permission)` (in `OrchardCore.Settings.Core`), besides `ManageSettings` itself. Every CrestApps settings group whose display driver checks a module permission registers that permission for its group, in the same startup (and therefore the same feature) that registers the driver, so a role that holds only the module permission and `AccessAdminPanel` can open the page.

| Group id | Permission keys | Registered by |
| --- | --- | --- |
| `stripe` | `ManageStripeSettings` | Stripe |
| `transactions` | `ManageTransactionSettings` | Transaction Reminders (`CrestApps.OrchardCore.Transactions.Notification`) |
| `paylater` | `ManageTransactionSettings` | Pay Later |
| `receipts` | `ManageReceiptSettings` | Receipts |
| `number-series` | `ManageNumberSeries` | Business Documents |
| `invoicing` | `ManageInvoicingSettings` | Invoicing |
| `exchangeRates` | `ManageExchangeRates` | Exchange Rates |
| `tax-origin`, `tax-calculation` | `ManageTaxation` | Taxation |
| `inventory` | `ManageInventory` | Inventory |
| `shipping` | `ManageShipping` | Shipping |
| `taxjar` | `ManageTaxation` | TaxJar (`CrestApps.OrchardCore.Taxation.TaxJar`) |
| `address-suggestions` | `ManageAddressSuggestions` | Address Suggestions (Google Places) (`CrestApps.OrchardCore.Addresses.GooglePlaces`) |
| `subscriptions` | `ManageSubscriptionSettings` | Subscriptions, and its roles, Stripe currency and installment plan features |
| `telephony` | `ManageTelephonySettings` | Telephony, the soft phone widget, Asterisk and Telnyx |
| `sms` | `ManageSmsSettings` | Telnyx SMS (`CrestApps.OrchardCore.Telnyx.Sms`), whose settings section now also requires this permission, as Orchard Core's own SMS and Twilio sections do |
| `contactcenter` | `ManageContactCenter` | Contact Center, its agent numbers, recording and secure capture features |
| `ai-settings` | `ManageAIProfiles`, `ManageChatInteractionSettings`, `EditChatInteractions`, `ManageCopilotSettings`, `ManageClaudeSettings`, `ManageMcpServerSettings` | Each AI feature registers the permission its own section checks (AI, AI Chat, AI Chat admin widget, AI Data Sources, AI Documents, AI Memory, Chat Interactions, Copilot, Claude, MCP Server) |
| DNC registry groups (`ImportContentSettings`, `UsaFtcRegistry`, `CanadaDnclRegistry` in `DncRegistryConstants.SettingsGroupIds`) | `ManageDncRegistrySettings` | DNC Registry and its USA FTC and Canada DNCL features |
| `PhoneNumberVerificationsConstants.SettingsGroupIds.Verifications` | `ManagePhoneNumberVerificationSettings` | Phone Number Verifications and each provider feature |
| `userDisplayName` | `ManageDisplaySettings` | User Display Name |
| `avatarOptions` | `ManageAvatarSettings` | User Avatars |

The `orders` and `customers` groups are registered by their own change. Registering one permission for a shared group (such as `ai-settings`) opens the page; each section on it still checks its own permission, so the holder sees only the sections that permission covers.

A module that adds a settings driver with its own permission must call `AddSiteSettingsPermission` next to the driver registration, and reference `OrchardCore.Settings.Core`. The `SiteSettingsGroupPermissionRegistrationTests` architecture test scans every `SiteDisplayDriver` under `src/Modules` and fails when the startup that registers a driver does not register the driver's permission for its group.
