---
sidebar_label: Connections and Deployments
title: Connect an AI Provider
description: Connect the site to an AI service, add the models you use as deployments, say what each model can do, and choose the site's default models.
technical_manual:
  - ai/providers/index
  - ai/model-capabilities
  - ai/overview
  - ai/realtime-voice
---

Before anyone can chat with an assistant, the site needs to reach an AI service, such as OpenAI or Azure OpenAI. An administrator does this once in three steps: add a **connection** with the service's address and key, add a **deployment** for each model you want to use, and choose the site's **default** models.

| | |
| --- | --- |
| **Menu** | Artificial Intelligence > Provider Connections; Artificial Intelligence > Deployments; Settings > Artificial Intelligence |
| **Permission** | Manage AI Provider Connections; Manage AI deployments; Manage AI profiles (for the settings) |
| **Feature** | AI Services and AI Connection Management, plus a provider feature: OpenAI Chat, Azure OpenAI Chat, Azure AI Inference Chat or Ollama AI Chat |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of the Azure OpenAI provider connection and its chat, utility, and embedding deployments">
  <source src="/img/docs/ai-provider-azure-openai.mp4" type="video/mp4" />
</video>

Your technical team can also set up connections and deployments in the site's configuration files. Those show in the lists under **Configuration-based connections** or **Configuration-based deployments** with a **Read-only** badge, and can only be changed there. See the [Technical Manual provider pages](../../ai/providers/index.md).

## Add a connection

A connection holds the address and the key the site uses to reach one AI service account.

1. Open **Artificial Intelligence > Provider Connections** and click **Add Connection**.
2. In **Available Providers**, click **Add** on your provider.
3. Fill in the fields below and click **Save**.

| Field | What it does |
| --- | --- |
| **Title** | The name shown for the connection. Required. |
| **Technical name** | A unique name, filled in from the title. It cannot be changed later. |
| **Endpoint** | The service's web address, from your provider's portal. **OpenAI**, **Azure OpenAI** and **Azure AI Inference** only. |
| **Authentication type** | **Azure OpenAI** and **Azure AI Inference** only. **Default authentication**, **Managed identity** or **API key**. Your technical team tells you which to use. |
| **API key** | The secret key from your provider. It is stored securely and never shown again. To keep a stored key, leave the field blank when you edit the connection. |
| **Identity client ID** | **Managed identity** only, and only when your team gives you one. |

:::tip[Other AI services]
Many AI services, such as DeepSeek, Google Gemini or Together AI, work like OpenAI. Choose **OpenAI** and enter that service's endpoint and key.
:::

The site checks the fields when you click **Save**; it does not contact the provider. If the key or the address is wrong, you find out the first time someone chats. A change to a connection can make the site reload its settings for a moment.

**Ollama** connections only have a **Title** and a **Technical name** in the admin; the address of the Ollama server is set in the site's configuration by your technical team.

## Add a deployment

A deployment is one model you use through a connection, such as `gpt-4.1-mini` for chat or `text-embedding-3-small` for search. Add one for every model the site needs.

1. Open **Artificial Intelligence > Deployments** and click **Add Deployment**.
2. In **Available Providers**, click **Add** on the provider of the model.
3. Fill in the fields below and click **Save**.

| Field | What it does |
| --- | --- |
| **Model name** | The model or deployment name exactly as your provider knows it, for example `gpt-4.1-mini`. Required. |
| **Technical name** | A unique name used by profiles and settings, filled in from the model name. It cannot be changed later. |
| **Connection name** | The connection the model is used through. Picked for you when the provider has only one. |
| **Model capabilities** | What the model can do. See below. At least one is required. |

### Model capabilities

Tick, under **Trained features**, only what the model really supports. The site uses these ticks to decide where the model is offered: an embedding model shows up only where an embedding model is needed, and a feature you leave unticked is never asked of the model.

| Capability | Tick it for |
| --- | --- |
| **Text conversation** | Models that answer chat messages. Ticked by default. |
| **Tool calling** | Chat models that can use tools. Ticked by default. |
| **Streaming** | Chat models that write the answer as it comes. Ticked by default. |
| **Structured outputs** | Chat models that can answer in a fixed format. |
| **Reasoning** | Models that think before answering. Ticking it shows **Reasoning effort**. |
| **Text embedding** | Embedding models used to search documents and knowledge bases. Never for a chat model. |
| **Image input (vision)** | Models that can read pictures. |
| **Image output** | Models that create pictures. |
| **Speech to text (transcription)** | Models that turn speech into text, such as Whisper. |
| **Text to speech (synthesis)** | Models that read text aloud. |
| **Realtime (speech-to-speech)** | Live voice models that hold spoken conversations. |
| **Audio input**, **Audio output**, **Video input**, **Video output** | Models that accept or produce audio or video directly. |

**Reasoning effort** lets profiles choose how hard a reasoning model thinks: **Minimal**, **Low**, **Medium**, **High** or **Extra high**. Tick it, then optionally narrow the **Supported values** to those your model accepts, and pick a **Default value** (it starts on **Medium**). Higher effort gives more careful answers but is slower and costs more.

### Special deployments

- **Azure AI Services** deployments carry their own **Endpoint**, **Authentication type** and **API key** instead of a connection. Use one for Azure speech-to-text.
- A **Cascaded Realtime** deployment gives you spoken conversations when your provider has no live voice model. It chains three deployments you already have: a **Speech-to-text deployment** that hears the user, a **Chat deployment** that writes the reply, and a **Text-to-speech deployment** that speaks it. The speech-to-text deployment must also have **Realtime (speech-to-speech)** ticked, because it has to transcribe continuously. The three can come from different providers. Expect a little more delay before the assistant starts speaking than with a live voice model.

## Choose the default models

Open **Settings > Artificial Intelligence** and fill in the **Default Deployments** section. A profile or chat left on **Default** uses these. Each list only shows deployments with the right capability.

| Field | What it is used for |
| --- | --- |
| **Default chat deployment** | Chats and chat interactions that do not pick a model. Set this one first. |
| **Default utility deployment** | Small side jobs, such as planning and working out what the user wants. |
| **Default embedding deployment** | Searching documents and knowledge bases. |
| **Default image deployment** | Creating pictures. |
| **Default vision deployment** | Reading pictures that users attach. |
| **Default speech-to-text deployment** | Dictation and spoken conversations. |
| **Default text-to-speech deployment** | Reading answers aloud and spoken conversations. |
| **Default text-to-speech voice** | The voice used when a profile does not pick one. The list loads after you pick a text-to-speech deployment. |

Click **Save**.

### Other site-wide AI settings

The same page has these sections, each explained on its own page:

| Section | Where it is explained |
| --- | --- |
| **General** | **Enable AI usage tracking** turns on the data for the [usage report](analytics.md#ai-usage-analytics). **Enable preemptive memory retrieval** lets the assistant read a user's [memory](memory.md) before it answers. The other options are technical; see the [Technical Manual](../../ai/overview.md#site-settings). |
| **Default Orchestrator** | **Enable preemptive retrieval-augmented generation (RAG)**: search your knowledge before every answer. See [Knowledge](knowledge.md). |
| **Prompt Security** and **Visitor Identity** | Protections for public chat. See [Chat widgets](chat-widgets.md#protect-a-public-assistant). |
| **Data Sources** and **Documents** | Defaults for [Knowledge](knowledge.md). |
| **Chat Interactions** | Voice and read-aloud for [chat interactions](chat-interactions.md). |
| **Memory** | [Memory](memory.md). |
| **Admin Widget** | The [admin chat widget](chat-widgets.md). |
| **Copilot** and **Claude** | Sign-in for the GitHub Copilot and Claude engines, set up by your technical team. See the Technical Manual pages for [Copilot](../../ai/copilot.md) and [Claude](../../ai/claude.md). |
| **MCP Server** | Which tools outside AI apps may use. See [Tools and agents](tools-and-agents.md#share-your-tools-with-other-ai-apps). |

## Where each model is used

| You want | You need a deployment with |
| --- | --- |
| Chat, chat widgets, AI workflow tasks | **Text conversation** |
| Answers from documents, data sources, file sources or web crawlers | **Text embedding** (for the search index) plus a chat model |
| Attach pictures in a chat, or describe figures in documents | **Image input (vision)** |
| Create pictures | **Image output** |
| Dictate messages | **Speech to text (transcription)** |
| Read answers aloud | **Text to speech (synthesis)** |
| Spoken conversations | **Realtime (speech-to-speech)**, or a **Cascaded Realtime** deployment, or both a speech-to-text and a text-to-speech default |
