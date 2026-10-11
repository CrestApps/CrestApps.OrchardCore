---
sidebar_label: Features & Settings
sidebar_position: 4
title: Features and Settings (for Administrators)
description: Turn a feature on in Tools > Features, find the site-wide settings screens, and look up which User Manual page covers each feature.
technical_manual:
  - feature-reference
---

This page is for **administrators**. The product is made of **features**: parts you turn on only when your company uses them, such as the outbound dialer or the AI assistant. A feature's menus, screens and permissions appear only while it is on. If you are not an administrator, you cannot change features; see [Roles and permissions](roles-and-permissions.md) to ask for access.

## Turn a feature on

1. Open **Tools > Features**.
2. Type part of the feature's name in the search box, for example *Dialer*.
3. Click **Enable** next to the feature.

Features it depends on are turned on with it. For example, turning on **SMS Messaging Channel** also turns on **Omnichannel Messaging Workspace**. Some features never appear in the list, because they are switched on automatically when another feature needs them.

Turning a feature off also turns off the features that depend on it, and hides their screens. The records they created are kept, so turning it on again brings them back.

:::tip[Turn on only what you use]
Every feature adds menus, permissions and screens. Turn on the features for the work your company does now, and add more later. The [use cases](../use-cases/index.md) list the features each goal needs.
:::

After a feature is on, give the right roles its permissions. The roles named Agent and Supervisor receive their default permissions automatically; see [Roles and permissions](roles-and-permissions.md).

Some features are also set up in one step by a ready-made **recipe** under **Tools > Recipes**, such as **Omnichannel CRM starter**, which turns on the CRM and adds lead, contact and opportunity types. See [Leads, accounts and opportunities](../leads-accounts-opportunities.md).

## Where the settings are

Site-wide settings live under **Settings** and **Tools**. Each screen appears when its feature is on.

| Menu | What you set there | Learn more |
| --- | --- | --- |
| **Settings > General** | The site name and the site's default time zone. | |
| **Settings > Contact Center** | Call recording rules, default phone and SMS numbers, approved outside transfer numbers, and secure data capture. | [Contact Center settings](../contact-center-settings.md) |
| **Settings > Communication > Telephony** | The phone provider (for example Telnyx), the default provider and the soft phone options. | [Phone and SMS setup](../telephony-settings.md) |
| **Settings > Communication > SMS** | The SMS provider (Twilio or Telnyx). | [Phone and SMS setup](../telephony-settings.md) |
| **Settings > Artificial Intelligence** | AI defaults, the admin chat widget, prompt security and the knowledge indexes. | [AI Assistant](../ai/index.md) |
| **Settings > DNC Registries** | The national do-not-call registries, such as the USA FTC and Canada LNNTE-DNCL registries. | [Do-not-call lists](../administration/do-not-call-lists.md) |
| **Settings > Content Import** | Which do-not-call registries every contact import checks. | [Do-not-call lists](../administration/do-not-call-lists.md) |
| **Settings > Phone Number Verifications** | The phone number lookup provider. | [Phone number verification](../administration/phone-number-verification.md) |
| **Settings > User Display Name**, **Settings > User Avatars** | How people's names and pictures are shown. | [Users](../administration/users.md) |
| **Tools > Features** | Turn features on and off. | This page |
| **Tools > Recipes** | Run ready-made setups. | |
| **Tools > Time Zones** | The friendly time zone names people pick from. | [Time zones](../administration/time-zones.md) |
| **Tools > Phone Verifications Queue** | The numbers waiting to be checked. | [Phone number verification](../administration/phone-number-verification.md) |

Things that need a server, a configuration file or the hosting environment, such as connection strings, are set up by IT. See the [Technical Manual](../../getting-started.md).

## Which page covers each feature

Find the feature by the name shown in **Tools > Features**. The [feature reference](../../feature-reference.md) in the Technical Manual lists the technical IDs.

### CRM and outreach

| Feature | What it adds | User Manual page |
| --- | --- | --- |
| **Omnichannel Management** | Contacts, subjects, dispositions, subject flows, campaigns, activity loads, cadences and omnichannel addresses. | [Contacts](../contacts.md), [Subjects](../subjects.md), [Dispositions](../dispositions.md), [Subject flows](../subject-flows.md), [Campaigns](../campaigns.md), [Omnichannel addresses](../channel-endpoints.md), [Load activities](../load-inventory.md), [Activities](../activities.md), [Managing activities in bulk](../bulk-activities.md), [Cadences](../cadences.md) |
| **Omnichannel Activities** | The activity services behind the CRM. Turned on with Omnichannel Management. | [Numbers not in service](../numbers-not-in-service.md) |
| **Omnichannel CRM** | Leads, accounts and opportunities. | [Leads, accounts and opportunities](../leads-accounts-opportunities.md) |
| **Omnichannel Messaging Workspace** | The shared text inbox, broadcasts and templates. | [Messaging workspace](../messaging.md) |
| **SMS Messaging Channel** | Texting in the messaging workspace and text entry points. | [Messaging workspace](../messaging.md), [Entry points](../entry-points-and-ivr.md) |
| **Omnichannel Messaging Routed Distribution** | Hands each new text conversation to one available agent. | [Entry points](../entry-points-and-ivr.md) |
| **SMS Omnichannel Automation** | AI text conversations, cadence follow-ups, and AI answering texts. | [Automated AI SMS and voice](../automated-ai.md), [Cadences](../cadences.md) |
| **Automated Voice** | AI phone conversations over any phone provider. | [Automated AI SMS and voice](../automated-ai.md) |

### Contact center

| Feature | What it adds | User Manual page |
| --- | --- | --- |
| **Contact Center** | The base of the contact center, its settings and the voice media library. | [Contact Center settings](../contact-center-settings.md), [Voice media](../voice-media.md) |
| **Contact Center Agents** | Agent sign-in, presence and reason codes. | [Agent workspace](../agent-workspace.md), [Agent states](../agent-states.md) |
| **Contact Center Agent Entitlements** | Which queues and campaigns each agent may sign in to, and their skills. | [Skills and entitlements](../skills-and-entitlements.md) |
| **Contact Center Work Distribution** | Queues, queue groups, skills and routing. | [Queues](../queues.md), [Skills and entitlements](../skills-and-entitlements.md) |
| **Contact Center Business Hours** | Opening-hours calendars. Also turned on by the features that use it. | [Business hours](../business-hours.md) |
| **Contact Center Inbound Entry Points** | Entry points for your numbers. | [Entry points and IVR](../entry-points-and-ivr.md) |
| **Contact Center Inbound Voice** | Call entry points: welcome messages, phone menus, after-hours handling and voicemail. | [Entry points and IVR](../entry-points-and-ivr.md), [Voicemail](../voicemail.md) |
| **Contact Center Outbound Dialer** | Dialer profiles, preview dialing and queue callbacks. | [Dialer profiles](../dialer-profiles.md) |
| **Contact Center Paced Dialing** | Power and progressive dialing. | [Dialer profiles](../dialer-profiles.md) |
| **Contact Center Call Recording** | Call recording and its rules. | [Contact Center settings](../contact-center-settings.md) |
| **Contact Center Secure Data Capture** | A secure link for customers to type card or bank details. | [Contact Center settings](../contact-center-settings.md), [Agent workspace](../agent-workspace.md) |
| **Contact Center Supervision & Live Dashboard** | The live dashboard and listen, whisper and barge. | [Live dashboard](../live-dashboard.md) |
| **Contact Center Voice Media** | Two-way audio for AI phone calls, which a live (realtime) AI conversation needs. | [Automated AI SMS and voice](../automated-ai.md) |

### Phone and SMS providers

| Feature | What it adds | User Manual page |
| --- | --- | --- |
| **Telephony** | The phone settings screen and extensions. | [Phone and SMS setup](../telephony-settings.md), [Extensions](../extensions.md) |
| **Telephony Soft Phone Extension** | The `/softphone` page the browser extension and Windows app open. | [Soft phone](../soft-phone.md), [Phone apps](../phone-apps.md) |
| **Telephony Soft Phone** | A phone button on every admin page, and its options. | [Soft phone](../soft-phone.md) |
| **Telnyx** | Telnyx as the phone provider. | [Phone and SMS setup](../telephony-settings.md) |
| **Telnyx SMS** | Telnyx as an SMS provider. | [Phone and SMS setup](../telephony-settings.md) |
| **Telnyx AI Voice Agent** | AI phone calls over Telnyx, and the **AI voice agent** choice on call entry points. | [Automated AI SMS and voice](../automated-ai.md), [Entry points and IVR](../entry-points-and-ivr.md) |
| **Asterisk** | Asterisk as the phone provider. | [Phone and SMS setup](../telephony-settings.md) |

### Reports, compliance and administration

| Feature | What it adds | User Manual page |
| --- | --- | --- |
| **Reports** | The Reports menu and CSV export. | [Reports](../reports.md) |
| **Reports (OpenXml)** | Excel export of reports. | [Reports](../reports.md) |
| **Report Builder** | Build your own reports with drag and drop, and share them. | [Report Builder](../report-builder/index.md) |
| **DNC Registry**, **Local Do Not Call Registry**, **USA FTC Do Not Call Registry**, **Canada LNNTE-DNCL Registry** | Do-not-call checks on imports and calls. | [Do-not-call lists](../administration/do-not-call-lists.md) |
| **AbstractAPI Phone Number Verification**, **Veriphone Phone Number Verification**, **Twilio Phone Number Verification** | Phone number lookups that catch dead numbers before you dial. | [Phone number verification](../administration/phone-number-verification.md) |
| **Content Transfer**, **Content Transfer (OpenXml)** | Importing and exporting contacts and other records as CSV or Excel. | [Import and export](../administration/import-and-export.md) |
| **Content Access Control** | Limits who may see a content item by role. | [Content access control](../administration/content-access-control.md) |
| **CrestApps Content Fields** | Extra field types for content types. | [Content fields](../administration/content-fields.md) |
| **User Display Name**, **User Avatar** | Full names and pictures for users. | [Users](../administration/users.md) |
| **Enhanced Roles** | A role picker for content items. | [Roles](../administration/roles.md) |
| **Time Zones** | Friendly time zone names. | [Time zones](../administration/time-zones.md) |
| **Workflows** (from Orchard Core) | Automations that react to events. | [Contact Center workflows](../workflows.md), [AI workflows](../ai/workflows.md) |

### Artificial intelligence

| Feature | What it adds | User Manual page |
| --- | --- | --- |
| **OpenAI Chat**, **Azure OpenAI Chat**, **Azure AI Inference Chat**, **Ollama AI Chat** | An AI provider to connect to. | [AI connections](../ai/connections.md) |
| **AI Connection Management** | The screens to manage AI provider connections. | [AI connections](../ai/connections.md) |
| **AI Chat** | AI profiles you can chat with, and the website chat widget. | [AI profiles](../ai/profiles.md), [AI chat](../ai/chat.md), [Chat widgets](../ai/chat-widgets.md) |
| **AI Chat Admin Widget** | A floating AI assistant on every admin page. | [Chat widgets](../ai/chat-widgets.md) |
| **AI Chat Interactions** | Ad-hoc AI chats without a profile. | [Chat interactions](../ai/chat-interactions.md) |
| **AI Chat Session Analytics** | AI chat reports. | [AI analytics](../ai/analytics.md) |
| **AI Prompt Templates** | Reusable instructions for AI profiles. | [Prompt templates](../ai/prompt-templates.md) |
| **AI Documents for Profiles**, **AI Documents for Chat Interactions**, **AI Documents for Chat Sessions**, **AI Documents (PDF)**, **AI Documents (OpenXml)** | Documents the AI answers from. | [Knowledge](../ai/knowledge.md) |
| **AI Data Sources - Elasticsearch**, **AI Data Sources - Azure AI Search**, **AI Data Sources - PostgreSQL**, **AI Web Crawlers**, **AI File Sources** | Company data and websites the AI answers from. | [Knowledge](../ai/knowledge.md) |
| **AI Memory indexing using Elasticsearch**, **AI Memory indexing using Azure AI Search** | The AI remembers preferences of signed-in users. | [Memory](../ai/memory.md) |
| **AI Tool Instances**, **Orchard Core AI Agent**, **Model Context Protocol (MCP) Client**, **Model Context Protocol (MCP) Server**, **Agent-to-Agent (A2A) Client**, **Agent-to-Agent (A2A) Host** | Tools and outside agents the AI can use, and sharing your AI with other systems. | [Tools and agents](../ai/tools-and-agents.md) |

Features not listed here are technical building blocks, or are set up by IT. The [feature reference](../../feature-reference.md) lists every feature.
