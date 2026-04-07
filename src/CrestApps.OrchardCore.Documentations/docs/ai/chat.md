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

AI profiles are now source-agnostic in the admin UI. When you click **Add Profile**, Orchard Core opens the profile editor directly instead of asking you to choose a source first. The selected chat and utility deployments now determine which client and model are used.

### AI Profile and Template Editor Layout

The AI Profile editor keeps the Orchard-style top-level tabs focused on broader areas:

- **Content** — contains the **General**, **Instructions**, and **Parameters** cards
- **Knowledge** — data source selection, filter, retrieval behavior, uploaded documents, session-document uploads, and memory settings

Profile-source AI Templates follow the same layout, and new profiles show a dedicated **Apply Templates** card above the editor so you can pre-fill the form before saving.

Only **Filter** hides until a data source is selected. **Restrict answers to retrieved data only**, **Document Top N**, **Strictness**, and **Retrieved documents** stay visible so knowledge behavior can still be tuned even without selecting a data source first. In the Orchard Core AI Profile knowledge tab, **Enable user memory** and **Allow session document uploads** appear before the data-source selector, while the retrieval settings and **Document Top N** stay grouped together below the data-source filter. The model-parameter editors in both MVC and Orchard Core now also use consistent `(?)` hover help for Temperature, Top P, penalties, token limits, and past-message settings so profile editing matches the chat and template UX.

When **Data Extraction** is enabled for an AI Profile, the live chat session now feeds any already extracted field values back into the model prompt. This helps scripted assistants avoid re-asking for values they already collected in the same session unless the user is correcting them.

**Note**: This feature does not provide completion client implementations (e.g., OpenAI, Azure OpenAI, etc.). To enable chat capabilities, you must enable at least one feature that implements an AI completion client, such as:

- **OpenAI AI Chat** (`CrestApps.OrchardCore.OpenAI`): AI-powered chat using OpenAI service.
- **Azure OpenAI Chat** (`CrestApps.OrchardCore.OpenAI.Azure`): AI services using Azure OpenAI models.
- **Azure AI Inference Chat** (`CrestApps.OrchardCore.AzureAIInference`): AI services using Azure AI Inference (GitHub models) models.
- **Ollama AI Chat** (`CrestApps.OrchardCore.Ollama`): AI-powered chat using Ollama service.

### Welcome Message Behavior

When an AI profile has a **Welcome Message** configured, it is displayed as placeholder text for new sessions. It is not automatically added to the model conversation history.

If **Add initial prompt** is enabled on the profile, the welcome message is ignored for new sessions. Instead, the session is created immediately with an assistant message from the configured **Initial prompt**, and that message appears in chat history when the page loads or when a new session is started. That initial assistant turn also participates in the live chat transcript, so follow-up user replies are grounded on it just like any other assistant message.

### Chat Mode

AI Chat supports three chat modes that control how users interact with the AI. The **Chat Mode** dropdown appears on the AI Profile editor (and AI Profile Template editor for Profile source templates) only for profiles of type **Chat**.

| Mode | Description | UI Element |
| --- | --- | --- |
| **Text Only** (default) | Standard text-based chat. Users type prompts and receive text responses. | — |
| **Audio Input** | Adds a microphone button (🎤) for speech-to-text dictation. Users speak their prompts, review the transcribed text, and click send manually. | Microphone button |
| **Conversation** | Persistent two-way voice interaction like ChatGPT voice mode. A continuous audio stream stays open — the user speaks, the AI responds with both text and voice simultaneously. | Headset button |

#### Prerequisites

- **Audio Input** requires a resolvable speech-to-text deployment. The system first uses **Settings → Artificial Intelligence → Default Deployments** and then falls back to the first available deployment that supports speech-to-text (for example, Azure Speech or OpenAI Whisper).
- **Conversation** requires resolvable speech-to-text and text-to-speech deployments using that same fallback behavior.
- Optionally, set a **Default Text-to-Speech Voice** in **Settings → Artificial Intelligence → Default Deployments**. This voice is used when no profile-specific voice is selected.
- If an AI Profile leaves its chat model set to **Default deployment**, chat sessions use **Default Chat Deployment** from **Settings → Artificial Intelligence → Default Deployments** and otherwise fall back to the first available chat deployment.

#### Configuring Chat Mode

1. Navigate to the AI Profile editor (or AI Profile Template editor for Profile source templates).
2. Select the desired option from the **Chat Mode** dropdown. The dropdown only appears for **Chat** profile types and when the required deployment types can be resolved.
3. When **Conversation** is selected, a **Voice** dropdown appears. Available voices are fetched from the resolved text-to-speech deployment. If no voice is selected, the default voice from site settings (or the provider's default) is used.
4. Save the profile.

When the **Deployments** card leaves **Chat deployment** or **Utility deployment** set to **Default**, the editor now shows a warning if the matching site default has not been configured yet. Use **Settings → Artificial Intelligence → Default Deployments** to set the shared fallback models explicitly.

Once configured, the selected chat mode applies to all chat UIs associated with that profile:
- Admin session chat
- Frontend widget
- Admin widget

#### How Audio Input Works

1. Click the microphone button to start recording.
2. Speak your prompt — the button shows a pulsing red stop icon while recording.
3. Audio is streamed to the server in real-time via SignalR as the user speaks (chunks are sent approximately every second).
4. The server transcribes audio using the configured speech-to-text provider and streams transcript text back to the UI as it becomes available — you see words appear while still speaking.
5. Click the stop button (or the transcription finishes automatically when you stop speaking).
6. The complete transcribed text appears in the input field for review or editing before sending as a prompt.

#### How Conversation Mode Works

1. Click the **headset** button to start conversation mode.
2. The microphone button, send button, and text input are hidden — a persistent audio stream opens to the server.
3. Speak naturally — your speech is continuously streamed to the server and transcribed in real time.
4. When a complete utterance is recognized, it is automatically displayed as a user message in the chat and sent to the AI.
5. The AI response streams to the chat as text **and** is simultaneously synthesized to speech — you see the text appear while hearing it read aloud.
6. If you start speaking while the AI is still responding, the AI's current response (both text and audio) is interrupted, and your new prompt is processed instead.
7. The stream stays open for continuous back-and-forth conversation — no need to click send between turns.
8. Click the headset button again to end the conversation. The microphone, send button, and text input are restored.

:::info
If the speech-to-text service encounters an error (e.g., authentication failure), the error is reported immediately and the recording stops automatically — the microphone button resets so you can try again.
:::

:::info
Text-to-speech synthesis occurs after the full response text has been received — it does not interrupt or delay the text streaming experience.
:::

### Admin Chat User Interface

![Screen cast of the admin chat](/img/docs/admin-ui-sample.gif)

When **Enable session metrics** is turned on for a chat profile, the MVC and Orchard chat UIs both surface the same hover actions on assistant messages: the copy button plus thumbs up/down feedback controls. The action tray stays tucked into the lower-right corner of the message until hover/focus so long responses do not waste vertical space, and recent admin chat sessions are listed newest first. The MVC **Chat Analytics** feedback card now mirrors Orchard Core with separate positive/negative count tiles plus the same stacked satisfaction bar.

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
3. The floating chat widget will appear in the bottom-right corner of the admin dashboard after it has been configured with a profile.

#### Configuring the Admin Widget

Navigate to **Settings** → **Artificial Intelligence** → **Admin Widget** to configure:

- **Profile**: Select the AI chat profile to use for the admin widget. 
- **Max Sessions**: Set the maximum number of previous chat sessions displayed in the history panel (1–50).
- **Primary Color**: Customize the widget's primary color (header, toggle button). Defaults to `#41b670` (Orchard Core green).

If no profile is selected, the admin widget stays disabled and is not rendered.

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

Provides comprehensive analytics and reporting for AI chat sessions, including conversation metrics, performance tracking, user segmentation, feedback analysis, per-user/model AI usage, and a companion extracted-data report for profiles that use structured data extraction.

For complete documentation, see the [AI Chat Analytics](./chat-analytics.md) guide.
