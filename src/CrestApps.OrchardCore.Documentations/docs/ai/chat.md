---
sidebar_label: AI Chat
sidebar_position: 2
title: AI Chat
description: AI chat capabilities for Orchard Core with admin and frontend chat widgets.
---

| | |
| --- | --- |
| **Feature Name** | AI Chat |
| **Feature ID** | `CrestApps.OrchardCore.AI.Chat` |

Provides UI to interact with AI models using the profiles.

## AI Chat Feature

The **AI Chat** feature builds upon the **AI Services** feature by adding AI chat capabilities. Once enabled, any chat-type AI profile with the "Show On Admin Menu" option will appear under the **Artificial Intelligence** section in the admin menu, allowing you to interact with your chat profiles.

**Note**: This feature does not provide completion client implementations (e.g., OpenAI, Azure OpenAI, etc.). To enable chat capabilities, you must enable at least one feature that implements an AI completion client, such as:

- **OpenAI AI Chat** (`CrestApps.OrchardCore.OpenAI`): AI-powered chat using OpenAI service.
- **Azure OpenAI Chat** (`CrestApps.OrchardCore.OpenAI.Azure`): AI services using Azure OpenAI models.
- **Azure AI Inference Chat** (`CrestApps.OrchardCore.AzureAIInference`): AI services using Azure AI Inference (GitHub models) models.
- **Ollama AI Chat** (`CrestApps.OrchardCore.Ollama`): AI-powered chat using Ollama service.

### Welcome Message Behavior

When an AI profile has a **Welcome Message** configured, it is displayed as placeholder text for new sessions. It is not automatically added to the model conversation history.

If **Add initial prompt** is enabled on the profile, the welcome message is ignored for new sessions. Instead, the session is created immediately with an assistant message from the configured **Initial prompt**, and that message appears in chat history when the page loads or when a new session is started.

### Speech-to-Text (Voice Input)

AI Chat supports speech-to-text input, allowing users to speak their prompts using a microphone button instead of typing.

#### Prerequisites

- A **Default Speech-to-Text Deployment** must be configured in **Settings → Artificial Intelligence → Default Deployments**. This can be any deployment that supports the `ISpeechToTextClient` interface, such as an Azure Speech contained-connection deployment or an OpenAI Whisper deployment.
- The AI provider must support the `ISpeechToTextClient` interface.

#### Enabling Speech-to-Text

1. Navigate to the AI Profile editor (or AI Profile Template editor for Profile source templates).
2. Check the **Enable speech-to-text** checkbox. This checkbox only appears when a default speech-to-text deployment is configured.
3. Save the profile.

Once enabled, a microphone button (🎤) appears in all chat UIs associated with that profile:
- Admin session chat
- Frontend widget
- Admin widget

#### How It Works

1. Click the microphone button to start recording.
2. Speak your prompt — the button shows a pulsing red stop icon while recording.
3. Audio is streamed to the server in real-time via SignalR as the user speaks (chunks are sent approximately every second).
4. The server transcribes audio using the configured speech-to-text provider and streams transcript text back to the UI as it becomes available — you see words appear while still speaking.
5. Click the stop button (or the transcription finishes automatically when you stop speaking).
6. The complete transcribed text appears in the input field for review or editing before sending as a prompt.

:::info
If the speech-to-text service encounters an error (e.g., authentication failure), the error is reported immediately and the recording stops automatically — the microphone button resets so you can try again.
:::

### Admin Chat User Interface

![Screen cast of the admin chat](/img/docs/admin-ui-sample.gif)

---

### Admin Chat Widget

| | |
| --- | --- |
| **Feature Name** | AI Chat Admin Widget |
| **Feature ID** | `CrestApps.OrchardCore.AI.Chat.AdminWidget` |

Provides a floating AI chat widget on every admin page, allowing users to interact with a predefined AI profile.

The **AI Chat Admin Widget** adds a floating chat widget to the Orchard Core admin dashboard. This allows administrators to interact with AI directly from any admin page without navigating away.

#### Enabling the Admin Widget

1. Go to **Tools** > **Features** in the admin menu.
2. Search for **AI Chat Admin Widget** and enable it.
3. The floating chat widget will appear in the bottom-right corner of the admin dashboard.

#### Configuring the Admin Widget

Navigate to **Settings** → **Artificial Intelligence** → **Admin Widget** to configure:

- **Profile**: Select the AI chat profile to use for the admin widget. 
- **Max Sessions**: Set the maximum number of previous chat sessions displayed in the history panel (1–50).
- **Primary Color**: Customize the widget's primary color (header, toggle button). Defaults to `#41b670` (Orchard Core green).

- :::tip Pro Tip
It's best to enable **Orchard Core AI Agent** (i.e., `CrestApps.OrchardCore.AI.Agent`). Then when creating a profile, select all available capabilities to allow the profile to perform tasks on your website.
:::

---

### Frontend Chat Widget

A **frontend chat widget** is available to add to your site's public-facing pages using the Orchard Core Widgets system. This allows site visitors to interact with AI chat directly on the frontend.

#### Adding the Frontend Widget

1. Ensure the **Widgets** feature (`OrchardCore.Widgets`) is enabled.
2. Go to **Design** > **Widgets** in the admin menu.
3. Add a new **AI Chat Widget** to the desired zone (e.g., Footer, Content).
4. Configure the widget by selecting the AI chat profile and optionally choosing a prompt template.

#### Frontend Widget Screen Cast

![Screen cast of the frontend widget](/img/docs/widget-ui-sample.gif)

---

### Chat Analytics

| | |
| --- | --- |
| **Feature Name** | AI Chat Analytics |
| **Feature ID** | `CrestApps.OrchardCore.AI.Chat.Analytics` |

Provides comprehensive analytics and reporting for AI chat sessions, including conversation metrics, performance tracking, user segmentation, and feedback analysis.

For complete documentation, see the [AI Chat Analytics](./chat-analytics.md) guide.
