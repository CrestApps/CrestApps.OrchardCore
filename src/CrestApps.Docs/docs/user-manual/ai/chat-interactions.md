---
sidebar_label: Chat Interactions
title: Chat Interactions
description: Chat with an AI model directly, choosing the model, instructions, knowledge and tools yourself, without building an AI profile first.
technical_manual:
  - ai/chat-interactions
---

A **chat interaction** is a free-form chat where you set everything up yourself, right beside the conversation: which model to use, the instructions, files to ask about, and the tools the AI may use. Nothing has to be prepared by an administrator first. Use it to try a model, to draft text, to ask questions about a file, or to test instructions before you put them in an [AI profile](profiles.md).

| | |
| --- | --- |
| **Menu** | Artificial Intelligence > Chat Interactions |
| **Permission** | List chat interactions; Edit own chat interactions; Delete own chat interaction |
| **Feature** | AI Chat Interactions; AI Documents for Chat Interactions to attach files |

<AskYourAdmin />

<YouTube video="ai-chat-interactions" title="Video overview of AI chat interactions" />

The video covers why chat interactions exist, how to start one and set it up, the tools of the Capabilities tab, how to manage your interactions and the site settings.

## Start a chat interaction

<YouTube video="ai-chat-interactions" title="Video of starting a chat interaction" start={92} end={145} />

1. Open **Artificial Intelligence > Chat Interactions** and click **New Chat**. A new interaction opens and is saved straight away.
2. In the settings panel on the left, give it a **Title** and pick a **Chat deployment** (or leave **Default**).
3. Type your question in the message box and press **Enter**.

Your settings are saved as you change them; there is no **Save** button. Click the collapse button to hide the settings panel, and **Configurations** to show it again.

The chat area works like the chat page of an assistant: answers appear as they are written, show their sources, and can have copy and read-aloud buttons. See [Chat with an AI assistant](chat.md).

## Find, copy and delete interactions

<YouTube video="ai-chat-interactions" title="Video of reopening, copying, clearing and deleting interactions" start={255} end={306} />

- The list shows your interactions with their titles. Click **Chat** to reopen one and continue. Type in **Search** and click the search button to find one.
- In an open interaction, **New Chat** offers **Empty Chat** (start fresh) or **Chat with Preset Settings** (a new interaction with the same model, instructions, files and tools).
- **Clear History** removes the messages but keeps your files, settings and tools. It cannot be undone.
- To delete an interaction, open its **Actions** menu in the list and choose **Delete**. To delete several, tick them and use **Actions > Delete** above the list. Deleting an interaction also deletes its files.

You see only your own interactions, unless you have the **List chat interactions for others** permission.

## Settings tab

<YouTube video="ai-chat-interactions" title="Video of the Settings tab of a chat interaction" start={145} end={215} />

| Field | What it does |
| --- | --- |
| **Title** | A name to find the interaction again later. |
| **Orchestrator** | The engine that plans the answer and decides which tools to use. Shown only when your site has more than one. |
| **Chat deployment** | The model to chat with. **Default** uses the site's default chat deployment. The list is grouped by connection. |
| **AI persona** | A reusable character for the assistant, such as a name and a tone. Shown when the **AI Personas** feature is on. See [Personas](profiles.md#personas). |
| **Model parameters** | Options the chosen model supports, such as reasoning effort. |
| **Conversation deployment** / **Voice** | For spoken conversations: the voice model and the voice. Leave empty to use the site defaults. See [Talk instead of type](#talk-instead-of-type). |
| **Utility deployment** | A smaller model for side jobs. Leave it on **Default**. |
| **System instructions** | Optional instructions that tell the AI who it is and how to answer, for example "You are a copy editor. Answer in British English." |
| **Prompt templates** | Reusable pieces of instructions to add, when your site has any. See [Templates](prompt-templates.md#reuse-pieces-of-instructions). |
| **Max response tokens**, **Temperature**, **Top P**, **Frequency penalty**, **Presence penalty**, **Past messages** | Fine-tuning. Leave on **Default** unless you know what you want. They mean the same as on a [profile](profiles.md#parameters). |

When your site uses the GitHub Copilot or Claude engine, its model and **Effort level** settings appear here too; for Copilot you may be asked to **Sign in with GitHub**.

## Knowledge tab

Ask questions about your own files or knowledge base.

<YouTube video="ai-walkthroughs" title="Video of uploading a PDF to a chat interaction and receiving a grounded answer that cites the file" start={133} end={201} />

1. Open the **Knowledge** tab.
2. Drag files onto **Drag and drop files here**, or click **Browse files**. **Supported formats** lists the file types you can use.
3. Wait until the file is processed, then ask your question in the chat.

The answer cites your file as a numbered source. Files you attach belong to this interaction only.

| Field | What it does |
| --- | --- |
| **Data source** | A knowledge base to answer from. **No data source** for none. |
| **Filter**, **Restrict answers to retrieved data only**, **Strictness**, **Retrieved documents** | How the data source is searched. See [Knowledge](knowledge.md#use-a-data-source-in-a-profile). |
| **Document retrieval mode** | **Chunk** (only matching passages of your files) or **Hierarchical** (whole files). **Use site default** keeps the site's choice. |
| **Attached files** | Your uploaded files. Use **Download file** or **Remove file** on each. |

If the **Knowledge** tab has no upload area, your administrator has not set up document storage or has turned off uploads for chat interactions.

## Capabilities tab

<YouTube video="ai-chat-interactions" title="Video of letting the AI use tools in a chat interaction" start={215} end={255} />

Let the AI use tools in this interaction. Tick what you need. In **Tools**, type in **Search tools...** to find a tool by name or description, and use **Select All Tools** or **Select All** in a category to tick several at once:

| Section | What it does |
| --- | --- |
| **Connections** | External MCP tool servers. |
| **A2A Connections** | Remote AI agents. |
| **Agents** | Agent profiles on this site. |
| **Tools** | Built-in actions, grouped by category. |
| **Tool Instances** | Tools your team set up, such as a call to your order system. |

You can only use tools your role is allowed to use. See [Tools and agents](tools-and-agents.md).

## Talk instead of type

Voice in chat interactions is set for the whole site by an administrator (see below). Depending on the setting:

- **Audio input** adds a microphone (**Voice input**) to dictate your message.
- **Conversation** adds a **Start Conversation** button for spoken back-and-forth. Click **End Conversation** to go back to typing.
- With read-aloud turned on, each answer has a **Read aloud** button.

How these buttons work is described in [Chat with an AI assistant](chat.md#talk-instead-of-type).

## Site settings for chat interactions (administrators)

<YouTube video="ai-chat-interactions" title="Video of the site settings for chat interactions" start={306} end={342} />

Open **Settings > Artificial Intelligence**:

| Section | Field | What it does |
| --- | --- | --- |
| **Chat Interactions** | **Chat mode** | **Text input**, **Audio input** (needs a default speech-to-text deployment) or **Conversation** (needs speech-to-text and text-to-speech). Applies to every chat interaction. |
| **Chat Interactions** | **Enable text-to-speech playback** | Adds a **Read aloud** button to every answer. Needs a default text-to-speech deployment. |
| **Documents** | **Index profile**, **Document retrieval mode** | Where attached files are stored and how they are searched. See [Knowledge](knowledge.md#before-you-start). |
| **Documents** | **Allow document uploads in chat interactions** / **Allow image uploads in chat interactions** | Which kinds of files people can attach. Pictures also need a default vision deployment. |
| **Documents** | **Largest document that can be indexed** / **Describe figures in uploaded documents** | Limits for attached files. |
| **Memory** | **Enable user memory** | Lets the AI remember signed-in users' preferences across interactions. See [Memory](memory.md). |

Changing these sections needs the **Manage chat interaction settings** or **Edit any chat interactions** permission.
