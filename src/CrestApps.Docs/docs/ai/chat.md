---
sidebar_label: AI Chat
sidebar_position: 2
title: AI Chat
description: AI chat capabilities for Orchard Core with admin and frontend chat widgets.
user_manual:
  - user-manual/ai/chat
  - user-manual/ai/chat-widgets
  - user-manual/ai/profiles
---

| | |
| --- | --- |
| **Feature Name** | AI Chat |
| **Feature ID** | `CrestApps.OrchardCore.AI.Chat` |

Provides UI to interact with AI models using the profiles.

This page covers the features, prerequisites, runtime behavior and security settings of AI Chat. For day-to-day use, see the User Manual:

- [Chat with an AI assistant](../user-manual/ai/chat.md): starting chats, chat history, attachments, dictation, conversation mode, read-aloud, citations and **Invoke Profile**.
- [AI profiles](../user-manual/ai/profiles.md): the **New AI Profile** picker and every field of the profile editor.
- [Chat widgets](../user-manual/ai/chat-widgets.md): turning on the admin widget and placing the website widget.

## AI Chat Feature

The **AI Chat** feature adds profile-driven chat capabilities to **AI Services**. Once enabled, any chat-type AI profile with the **Show On Admin Menu** option appears under **Artificial Intelligence** in the admin menu. The menu entry requires `QueryAnyAIProfile` (or the per-profile `QueryAIProfile_{profileName}` permission), and the menu is cached and invalidated whenever a profile is saved.

AI profiles are source-agnostic in the admin UI. **Add Profile** opens the **New AI Profile** picker: **Blank profile** opens the profile editor directly, and every other card creates the profile from a profile template (see [Creating a profile from a starting point](profile-templates#creating-a-profile-from-a-starting-point)). Either way, the selected chat and utility deployments determine which client and model are used.

### AI Profile and Template Editor Layout

The profile editor and the editor of profile-source AI templates share one layout. The first tab holds the **General**, **Deployments & Interactions**, **Instructions**, **Parameters** and **Prompt Security** cards; the **Knowledge**, **Capabilities** and **Data Processing & Metrics** tabs are contributed by the enabled features through display drivers. Every field is described in the User Manual under [AI profiles](../user-manual/ai/profiles.md#the-profile-editor).

For chat profiles, the required **Past messages included** parameter loads the default value automatically when a profile is opened without an explicit saved value.

When the AI Documents features are enabled, the **Knowledge** tab for **AI Profiles**, **profile-source AI Templates**, and **Chat Interactions** also exposes a **Document retrieval mode** selector. Leaving it blank uses the site default from **Settings → Artificial Intelligence → Documents**. **Chunk** keeps chunk-level context, while **Hierarchical** retrieves matching chunks and then injects the full text of the matched documents.

**Note**: This feature does not provide completion client implementations (e.g., OpenAI, Azure OpenAI, etc.). To enable chat capabilities, you must enable at least one feature that implements an AI completion client, such as:

- **OpenAI AI Chat** (`CrestApps.OrchardCore.OpenAI`): AI-powered chat using OpenAI service.
- **Azure OpenAI Chat** (`CrestApps.OrchardCore.OpenAI.Azure`): AI services using Azure OpenAI models.
- **Azure AI Inference Chat** (`CrestApps.OrchardCore.AzureAIInference`): AI services using Azure AI Inference (GitHub models) models.
- **Ollama AI Chat** (`CrestApps.OrchardCore.Ollama`): AI-powered chat using Ollama service.

### Welcome Message Behavior

When an AI profile has a **Welcome Message** configured, it is displayed as placeholder text for new sessions. It is not automatically added to the model conversation history.

If **Start the conversation automatically** is enabled on the profile, the welcome message is ignored for new sessions. The opening message is now saved lazily: Orchard creates and persists the chat session only after the visitor sends the first real message, then the configured assistant **Opening message** is inserted ahead of that first user prompt in the stored conversation history.

This avoids creating empty anonymous chat sessions just because a page or widget loaded.

### Chat Mode

AI Chat supports three chat modes that control how users interact with the AI. The **Chat mode** dropdown appears on the AI Profile editor (and AI Profile Template editor for Profile source templates) only for profiles saved with the type **Chat**.

| Mode | Description | UI Element |
| --- | --- | --- |
| **Text only** (default) | Standard text-based chat. Users type prompts and receive text responses. | — |
| **Audio input** | Adds a microphone button for speech-to-text dictation. Users speak their prompts, review the transcribed text, and click send manually. | Microphone button |
| **Conversation** | Two-way voice interaction the user switches on and off beside an ordinary message box. Starting a session hands the turn to speech; ending it gives the message box back, and both kinds of turn land in the same thread. | Soundwave button |

How each mode looks and works for the person chatting is described in [Talk instead of type](../user-manual/ai/chat.md#talk-instead-of-type).

#### Prerequisites

- **Audio input** requires a **Default Speech-to-Text Deployment** configured in **Settings → Artificial Intelligence → Default Deployments** (any deployment supporting the `ISpeechToTextClient` interface, such as Azure Speech or OpenAI Whisper).
- **Conversation** is carried by a realtime (speech-to-speech) deployment when one resolves — the profile's own **Conversation deployment**, then the site's default realtime deployment, then the first realtime-capable deployment. See [Realtime Voice](realtime-voice.md). When none resolves it falls back to the client-driven speech-to-text plus text-to-speech cascade, which requires both a **Default Speech-to-Text Deployment** and a **Default Text-to-Speech Deployment**.
- Optionally, set a **Default Text-to-Speech Voice** in **Settings → Artificial Intelligence → Default Deployments**. This voice is used when no profile-specific voice is selected.
- If an AI Profile leaves its chat model set to **Default deployment**, chat sessions use **Default Chat Deployment** from **Settings → Artificial Intelligence → Default Deployments** after checking the connection-level default.

#### Configuring Chat Mode

Chat mode is set per profile, in the **Deployments & Interactions** card (see [AI profiles](../user-manual/ai/profiles.md#deployments--interactions)). When **Conversation** is selected, the **Conversation deployment** and **Voice** fields appear. The voices are fetched from the model that will speak — the resolved realtime deployment's own voices, or the text-to-speech provider's when the conversation runs as the cascade. If no voice is selected, the default voice from site settings (or the provider's default) is used.

The **Chat deployment** is a separate question: it names the text model the profile talks to, and it answers typed messages including those typed during a voice conversation. Its picker lists text-capable deployments only.

Once configured, the selected chat mode applies to all chat UIs associated with that profile:

- Admin session chat
- Frontend widget
- Admin widget

#### Runtime behavior

- **Audio input** streams the recorded audio to the server over SignalR in chunks of approximately one second. The server transcribes it with the configured speech-to-text provider and streams the transcript back as it becomes available, so words appear while the user is still speaking. The final transcript is placed in the input field for review; it is not sent automatically.
- **Conversation** keeps the audio stream open for continuous back-and-forth. Each recognized utterance is added to the chat as a user message and sent automatically. The response streams as text and is synthesized to speech at the same time. Speech from the user while the AI is responding interrupts the current response (text and audio), and the new prompt is processed instead. Sending a typed message ends any live voice session first, so the two kinds of turn take turns rather than overlapping.

:::info
If the speech-to-text service encounters an error (e.g., authentication failure), the error is reported immediately and the recording stops automatically — the microphone button resets so you can try again.
:::

:::info
Text-to-speech synthesis occurs after the full response text has been received — it does not interrupt or delay the text streaming experience.
:::

### Text-to-Speech Playback

Even when a profile is not using the full **Conversation** chat mode, on-demand text-to-speech playback can add a **Read aloud** button to each AI-generated message.

#### Enabling TTS Playback

TTS playback can be enabled at two levels:

- **Site level (Chat Interactions)**: **Enable text-to-speech playback** in the **Chat Interactions** section of **Settings → Artificial Intelligence** enables playback across all Chat Interaction sessions.
- **Profile level**: **Enable text-to-speech playback** on the AI Profile editor enables playback for all chat UIs associated with that specific profile.

#### Prerequisites

- A **Default Text-to-Speech Deployment** must be configured in **Settings → Artificial Intelligence → Default Deployments**.
- The playback feature works with any chat mode (Text only, Audio input, or Conversation). In Conversation mode, TTS is already built-in and always active, so the per-message playback button is hidden.

#### Behavior

When a user clicks the playback button on a message, the text is sent to the configured TTS provider and audio is streamed back to the browser. Starting another message's playback stops the current one. See [Listen to an answer](../user-manual/ai/chat.md#listen-to-an-answer) for what the user sees.

### Session document uploads

Session uploads require the **AI Documents for Chat Sessions** feature (`CrestApps.OrchardCore.AI.Documents.ChatSessions`) and are opted in per profile with **Allow session document uploads** and **Allow session image uploads**. Image uploads also need a vision deployment. How users attach files is described in [Attach files to a chat](../user-manual/ai/chat.md#attach-files-to-a-chat).

If a profile allows session document uploads or session image uploads, the chat UI keeps restored widget sessions aligned with the current profile before uploading files. This avoids sending attachment requests through a different profile.

The admin and frontend chat widgets restore their saved toggle and panel positions before the chat app finishes initializing.

The admin session chat and both widgets show a compact **Supported formats** note above the input attachment bar. Document extensions follow the profile's **Allow session document uploads** setting, and image extensions only appear when **Allow session image uploads** is enabled and a vision deployment is available.

The module packages the upstream CSS source maps for the main chat UI and widget styles alongside the compiled assets, so browser developer tools can resolve `ai-chat.css.map` and `chat-widget.css.map` without 404 warnings. The chat UI scripts and styles come from the `@crestapps/ai-chat-ui` npm package and are copied into the module's `wwwroot`.

By default, session-document uploads are stored on the local file system through the shared AI Documents storage pipeline. If you want widget uploads stored in Azure Blob Storage instead, enable `CrestApps.OrchardCore.AI.Documents.Azure` and configure it as described in [AI Documents - Azure Blob Storage](./documents/azure-blob-storage.md).

### Citations and references

When a chat response includes document markers such as `[doc:1]`, the AI Chat UIs convert them into superscript citations and render a linked reference list below the assistant message when a resolver can provide a URL for the reference.

This linked citation rendering applies consistently across the admin chat UI, the admin widget, and the frontend widget.

### Admin Chat User Interface

The admin chat page is served at `ai/chat/session/{profileId}/{sessionId?}`, and the chat history at the profile's **View chat history** action. Deleting one session requires `DeleteChatSession`; deleting all of a profile's sessions requires `DeleteAllChatSessions`. Both are granted to the Administrator role by default.

<video controls preload="metadata" width="100%" aria-label="Screen cast of the admin chat">
  <source src="/img/docs/admin-ui-sample.mp4" type="video/mp4" />
</video>

### Invoking Utility and Agent Profiles

For **Utility** and **Agent** profile types, the AI profile list includes an **Invoke Profile** action (route `ai/chat/test/{profileId}`, requires `QueryAnyAIProfile`). It opens a single-response screen that keeps no chat session, history, metrics or ratings. See [Try out a utility or agent profile](../user-manual/ai/chat.md#try-out-a-utility-or-agent-profile).

---

### Admin Chat Widget

| | |
| --- | --- |
| **Feature Name** | AI Chat Admin Widget |
| **Feature ID** | `CrestApps.OrchardCore.AI.Chat.AdminWidget` |

Provides a floating AI chat widget on every admin page, allowing users to interact with a predefined AI profile.

The widget is configured in the **Admin Widget** section of **Settings → Artificial Intelligence** (stored as site settings, editable with the **Manage AI profiles** permission): the chat profile, the maximum number of history sessions (1–50, default 10), and the primary color (default `#41b670`). It renders on every admin page for signed-in users who are authorized to query the selected profile, and it stores its position, size, open state and current session in the browser's local storage.

Setting it up and using it are described in [Chat widgets](../user-manual/ai/chat-widgets.md#turn-on-the-admin-chat-widget).

:::tip[Pro Tip]
It's best to enable **Orchard Core AI Agent** (i.e., `CrestApps.OrchardCore.AI.Agent`). Then when creating a profile, select the tool capabilities the profile needs to perform tasks on your website.
:::

---

### Frontend Chat Widget

A **frontend chat widget** is available to add to your site's public-facing pages using the Orchard Core Widgets system. The module registers the `AIChat` widget content type (displayed as **Artificial Intelligence Chat**) with the `AIProfilePart`, when the **Widgets** feature (`OrchardCore.Widgets`) is enabled. The part stores the chat profile and the number of history sessions to show; the history list is only populated for authenticated visitors.

The widget is only rendered for visitors who are authorized to query its profile, so grant the `Anonymous` role the per-profile `QueryAIProfile_{profileName}` permission for a public assistant.

Placing the widget in a layer through the admin UI is described in [Add a chat box to your website](../user-manual/ai/chat-widgets.md#add-a-chat-box-to-your-website).

The frontend widget also normalizes theme paragraph spacing inside rendered chat messages so theme-level `p` margins and padding do not add extra blank space above or below each response.

Frontend widgets also work with the shared anonymous-visitor protection flow:

- Anonymous visitors receive a stable first-party visitor cookie for more accurate unique-visitor analytics.
- Chat message throttling and anonymous session-start throttling use that visitor identity together with the configured remote-address mode.
- Widgets no longer auto-create sessions on page load just because a profile has an opening message.
- **Settings → Artificial Intelligence** includes **Prompt security** and **Anonymous visitor identity** sections so operators can tune rate limits, prompt filtering, and remote-address handling.

---

### Security and Visitor Identity Settings

Both the admin widget and the frontend widget share a common protection layer that guards against prompt injection, prompt leakage, abusive traffic, and anonymous session churn. These options are configured per tenant under **Settings** > **Artificial Intelligence**, and require the **Manage AI Profiles** permission.

Saving either section may restart the tenant so the updated options take effect.

#### Prompt Security

The **Prompt Security** section tunes the shared AI chat protections.

| Setting | Default | Description |
| --- | --- | --- |
| Enable injection detection | Enabled | Blocks known prompt-injection and instruction-override patterns before they reach the model. |
| Enable output filtering | Enabled | Filters generated responses for leaked system prompts, tool details, and unsafe disclosures. |
| Enable security preamble | Enabled | Prepends a hardened security instruction to chat system prompts. |
| Enable input delimiters | Enabled | Wraps user input with clear boundaries so the model can distinguish user text from system instructions. |
| Enable audit logging | Enabled | Records prompt security decisions for diagnostics and abuse investigations. |
| Maximum prompt length | `8000` | Prompts longer than this character count are rejected. Must be between `1` and `100000`. |
| Blocking threshold | `High` | Prompts classified at or above this risk level (`None`, `Low`, `Medium`, `High`, `Critical`) are blocked. |
| Maximum messages per window | `20` | How many chat messages one rate-limit partition can send within the message window. Governs authenticated callers, and anonymous callers only when no anonymous message tiers are configured. Set to `0` to disable message throttling. |
| Message rate-limit window (seconds) | `60` | Duration of the shared chat message rate-limit window. Must be between `1` and `86400`. |
| Anonymous message rate-limit tiers | `5, 00:00:30`<br />`30, 00:05:00`<br />`150, 01:00:00`<br />`500, 1.00:00:00` | Multi-tier sliding-window message limits applied to anonymous callers only. Leave blank to fall back to the single message window above. |
| Maximum anonymous sessions per window | `20` | Limits how many new anonymous chat sessions can be started within the anonymous session window. Used only when no anonymous session-start tiers are configured. Set to `0` to disable anonymous session-start throttling. |
| Anonymous session window (seconds) | `600` | Duration of the anonymous chat session-start rate-limit window. Must be between `1` and `86400`. |
| Anonymous session-start rate-limit tiers | `5, 00:00:30`<br />`10, 00:05:00`<br />`150, 01:00:00`<br />`500, 1.00:00:00` | Multi-tier sliding-window session-start limits applied to anonymous callers only. Leave blank to fall back to the single session window above. |

Message and session-start throttling are keyed by the resolved visitor identity (authenticated user id or anonymous visitor id) together with the configured remote-address mode described below. Authenticated callers are additionally keyed by their network address, so signing out does not shed the per-address allowance.

When a visitor is throttled while starting a new chat, the message shown to them is deliberately generic and discloses neither the configured limit, the current count, nor the retry delay, since those values would let an abuser tune their traffic to sit just under the throttle.

##### Anonymous rate-limit tiers

The two tier fields accept one tier per line in the form `limit, window`, where the window is a .NET `TimeSpan` such as `00:00:30`, `01:00:00`, or `1.00:00:00` for a day. A request is throttled when it would exceed **any** configured tier, which lets a short burst tier sit alongside longer sustained tiers. Blank lines are ignored, and clearing the field falls back to the single-window limits above. Tiers apply to anonymous callers only — authenticated callers are always governed by **Maximum messages per window** and **Message rate-limit window**.

:::caution[Tiers take precedence over the single-window values]
The single-window settings are **fallbacks used only when the matching tier field is empty**. Because the shipped defaults populate both tier fields, lowering **Maximum anonymous sessions per window** on its own has **no effect**.

For example, setting **Maximum anonymous sessions per window** to `5` while the default `10, 00:05:00` tier is still present throttles anonymous visitors at **10**, not `5`. To make the single-window value authoritative, clear the tier field; to tighten the limit while keeping tiers, lower the relevant tier instead. The same applies to **Maximum messages per window** for anonymous callers.
:::

##### Per-profile throttle overrides

The six anti-spam **throttle** limits above are site-wide defaults. Individual AI profiles can raise or lower them for their own use case from a **Prompt Security** tab on the AI Profile editor, and AI profile templates (source `Profile`) can seed the same overrides so a template acts as a reusable throttle preset.

| Override | Behavior |
| --- | --- |
| Maximum messages per window | When left blank, inherits the site default. Set to `0` to disable message throttling for this profile only. |
| Message rate-limit window (seconds) | When left blank, inherits the site default. Must be between `1` and `86400`. |
| Anonymous message rate-limit tiers | When left blank, inherits the site tiers. Enter one tier per line as `limit, window` to replace them for this profile. |
| Maximum anonymous sessions per window | When left blank, inherits the site default. Set to `0` to disable anonymous session-start throttling for this profile only. |
| Anonymous session window (seconds) | When left blank, inherits the site default. Must be between `1` and `86400`. |
| Anonymous session-start rate-limit tiers | When left blank, inherits the site tiers. Enter one tier per line as `limit, window` to replace them for this profile. |

Each tier field also has a **Do not use tiered ... limits for this profile** checkbox. The three states are:

| Tier field | Checkbox | Result |
| --- | --- | --- |
| Blank | Unchecked | Inherits the site tiers. |
| One tier per line | Unchecked | Uses the profile's tiers instead of the site tiers. |
| Blank | Checked | Opts this profile out of tiered limits entirely, falling back to the profile's single-window values. |

Filling in the tiers *and* checking the box is rejected, because one of the two would have to be silently discarded.

The same precedence trap applies per profile: while a profile inherits or defines tiers, its **Maximum anonymous sessions per window** override is ignored. Check the box to make the single-window overrides authoritative for that profile.

Each override is optional and independent: an unset field always falls back to the corresponding site-wide default, so a profile can override only the message limit while still inheriting the anonymous session limits. Overrides are stored on the profile settings and applied by the chat message and session-start rate limiters. When a profile is created from a profile-source template, the template's throttle overrides are copied onto the new profile. The higher-level prompt-injection, output-filtering, and prompt-length guards remain site-wide only and cannot be overridden per profile.

#### Anonymous Visitor Identity

The **Visitor Identity** section controls how anonymous widget visitors are tracked for unique-visitor analytics, abuse controls, and optional remote-address storage. Anonymous visitors receive a stable first-party cookie during page load so repeat visits are recognized as the same visitor instead of a new one for each chat session.

That identity is also what a visitor's chat history is listed by: an anonymous visitor sees the sessions their own cookie owns, exactly as a signed-in user sees theirs. A visitor whose cookie has not been issued yet, or whose browser refuses it, has no history to list.

| Setting | Default | Description |
| --- | --- | --- |
| Visitor cookie name | `crestapps-ai-visitor` | Stable first-party cookie used to identify anonymous visitors across chat sessions. |
| Cookie lifetime (days) | `180` | How long the anonymous visitor cookie remains valid before a new visitor identity is issued. Must be between `1` and `3650`. |
| Allow cross-site embedding | Off | Writes the cookie `SameSite=None; Secure` so it survives inside a frame on another site. See below. |
| Partition the cookie per embedding site | On | Adds the `Partitioned` attribute (CHIPS) while cross-site embedding is on. |
| Remote address storage mode | `Hashed` | How the remote address is captured. See the modes below. |
| Remote address hash salt | `CrestApps.Core.AI.VisitorIdentity` | Application-specific salt used when hashing remote addresses for abuse controls. Required for the `Hashed` and `Encrypted` modes. |

Turn **Allow cross-site embedding** on only when the chat is embedded in a frame on another site, such as the external chat widget. The cookie is otherwise written `SameSite=Lax`, which a browser refuses in a third-party context and reports as "Cookie 'crestapps-ai-visitor' has been rejected because it is in a cross-site context". Every request from inside the frame then looks like a brand new visitor: the conversation does not survive a page load, and the visitor rate-limit partition never accumulates, so throttling falls back to the coarser network-address, session, and connection keys. The cookie stays `HttpOnly` either way, and it identifies a visitor rather than authenticating one, so it grants no privilege of its own.

`SameSite=None` is only legal together with `Secure`, and a `Secure` cookie never reaches a plain HTTP page, so a request that did not arrive over HTTPS keeps the `SameSite=Lax` cookie rather than losing it altogether.

Leave **Partition the cookie per embedding site** on unless a deployment needs one identifier across sites. `SameSite` is not part of a cookie's identity key, so without partitioning the cookie written inside a frame replaces the first-party cookie of the same name and downgrades that one to `SameSite=None` everywhere. Partitioning also keeps the cookie working in browsers that are phasing out unrestricted third-party cookies, and gives a visitor a separate identifier per embedding site, which is what per-site abuse control wants.

The **Remote address storage mode** supports the following values:

- **Disabled** &mdash; do not capture or persist any remote-address data.
- **Hashed** (recommended) &mdash; store only a salted hash of the remote address for privacy-first abuse controls.
- **Plain text** &mdash; store the remote address in plain text for operational controls such as blocklists.
- **Encrypted** &mdash; store the remote address encrypted at rest with Data Protection while still using a salted hash for throttling keys.

:::info
The remote-address value is read from the `X-Forwarded-For` header when present, otherwise from the connection remote IP address. When running behind a reverse proxy, make sure forwarded headers are configured so the correct visitor address is captured.
:::

---

### Chat Analytics

| | |
| --- | --- |
| **Feature Name** | AI Chat Session Analytics |
| **Feature ID** | `CrestApps.OrchardCore.AI.Chat.Analytics` |

Provides comprehensive analytics and reporting for AI chat sessions, including conversation metrics, performance tracking, user segmentation, and feedback analysis.

For complete documentation, see the [AI Chat Session Analytics](./chat-analytics.md) guide.
