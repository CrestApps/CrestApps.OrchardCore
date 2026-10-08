---
sidebar_label: AI Assistant
title: AI Features at a Glance
description: Learn what each AI feature does for your business, who sets it up, and which page explains it.
technical_manual:
  - ai/index
  - ai/overview
---

The AI features let you build assistants that answer questions, chat with your website visitors, work from your own documents, and do routine work in the background. You set them up in the admin screens under **Artificial Intelligence**, and most people only ever chat with them.

This section explains each feature in plain words. For installation, configuration files and the technical details, see the [Technical Manual AI overview](../../ai/index.md).

<video controls preload="metadata" width="100%" aria-label="Screencast touring the AI provider connections, deployments, and profiles admin screens">
  <source src="/img/docs/ai-overview.mp4" type="video/mp4" />
</video>

## What each feature does

| Feature | What it does for your business | Typical uses |
| --- | --- | --- |
| **Provider connections and deployments** | Connect the site to an AI service, such as OpenAI or Azure OpenAI, and name the models you are allowed to use. | Done once by an administrator before anything else works. |
| **AI profiles** | An assistant with its own name, instructions, model, knowledge and tools. Every chat runs on a profile. | A support assistant, a sales assistant, a summarizer that runs in the background. |
| **AI chat** | A chat page in the admin, one per profile, with a history of your past conversations. | Staff ask an internal assistant about policies, products or how to do something. |
| **Chat widgets** | A floating chat button on every admin page, and a chat box you can place on your public website. | Help for staff wherever they are in the admin; a website assistant for visitors. |
| **Chat interactions** | A free-form chat where you pick the model and settings yourself, without building a profile first. | Trying out a model, drafting text, asking questions about a file you upload. |
| **Templates** | Reusable starting points for new profiles, and reusable pieces of instructions. | Create a new assistant in a minute from a ready-made scenario. |
| **Knowledge** | Give the AI your own content: uploaded documents, search indexes, a folder of files or your public website. | Answers that come from your price list, manuals or help center, with links to the source. |
| **Tools and agents** | Let the AI look things up or take action: call your own web services, search documentation sites, or work with other AI agents and services. | Look up an order's status; search your product documentation. |
| **Memory** | The AI remembers stable preferences of a signed-in user across conversations. | "Always answer in short bullet points." |
| **Analytics** | Reports on how chats go, how many tokens they use, and what they cost. | See how many questions the assistant resolves, and which days are busiest. |
| **Workflows** | Use AI inside automated workflows, and start a workflow when a chat ends or collects information. | Summarize a form submission; send the details a visitor gave to your sales team. |

## Who does what

Most people only chat. Setting things up is done by a few people with extra permissions.

| Role | What they do | Pages for them |
| --- | --- | --- |
| **Administrator** | Turns on the AI features, connects AI providers, chooses the default models, and sets site-wide AI settings. | [Connections and deployments](connections.md) |
| **AI builder** (for example a team lead or content manager) | Creates and edits AI profiles, templates, knowledge, tools and agents. | [AI profiles](profiles.md), [Templates](prompt-templates.md), [Knowledge](knowledge.md), [Tools and agents](tools-and-agents.md) |
| **Staff** | Chat with the assistants they are allowed to use, in the admin or in the admin chat widget. | [Chat with an AI assistant](chat.md), [Chat widgets](chat-widgets.md), [Chat interactions](chat-interactions.md) |
| **Manager or analyst** | Reviews chat reports and usage. | [Analytics](analytics.md) |
| **Website visitors** | Chat with the website assistant. They need no account. | [Chat widgets](chat-widgets.md#what-visitors-see) |

What you can see depends on your role's permissions. If a menu item or a button described in this section is missing for you, your role lacks the permission, or the feature is not turned on.

<AskYourAdmin />

## Pages in this section

| Page | Read it when you want to |
| --- | --- |
| [Connections and deployments](connections.md) | Connect an AI provider and choose which models the site uses. |
| [AI profiles](profiles.md) | Create an assistant and set its instructions, model, voice, knowledge and tools. |
| [Chat with an AI assistant](chat.md) | Start a chat, find old chats, attach files, talk instead of type, and read the sources. |
| [Chat widgets](chat-widgets.md) | Turn on the admin chat widget or add a chat box to your website. |
| [Chat interactions](chat-interactions.md) | Chat with a model directly, without a profile. |
| [Templates](prompt-templates.md) | Start profiles from templates and reuse pieces of instructions. |
| [Knowledge](knowledge.md) | Let the AI answer from your documents, indexes, files and website. |
| [Tools and agents](tools-and-agents.md) | Let the AI call your web services, search documentation, or use other agents. |
| [Memory](memory.md) | Understand what the AI remembers about you, and clear it. |
| [Analytics](analytics.md) | Read the chat and usage reports. |
| [AI in workflows](workflows.md) | Use AI in automated workflows. |

## Use cases

Step-by-step stories that combine these features:

- [Let the AI answer your customers](../use-cases/ai-answers-customers.md)
- [Put an AI assistant on your website](../use-cases/ai-assistant-on-your-website.md)
- [Build an assistant that knows your documents](../use-cases/ai-knowledge-assistant.md)

For AI that texts and calls your contacts on its own, see [Automated AI SMS and Voice](../automated-ai.md).
