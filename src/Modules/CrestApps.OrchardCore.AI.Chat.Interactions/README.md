# AI Chat Interactions Module

This module provides ad-hoc AI chat interactions with configurable parameters, enabling users to chat with AI models without requiring predefined AI Profiles.

## Features

- **AI Chat Interactions**: Create and manage chat sessions with any configured AI provider
- **Session Persistence**: All chat messages are saved and can be resumed later
- **Configurable Parameters**: Customize temperature, TopP, max tokens, frequency/presence penalties, and past messages count
- **Tool Integration**: Select from available AI tools and MCP connections
- **Connection/Deployment Selection**: Choose specific connections and deployments for each interaction

## Getting Started

1. Enable the `AI Chat Interactions` feature in Orchard Core admin
2. Navigate to **Artificial Intelligence > Chat Interactions**
3. Click **+ New Chat** and select an AI provider
4. Configure your chat settings and start chatting

## Dependencies

This module requires:
- `OrchardCore.Liquid`
- `CrestApps.OrchardCore.Resources`
- `CrestApps.OrchardCore.AI.Chat.Core`
- `CrestApps.OrchardCore.SignalR`
- `CrestApps.OrchardCore.AI`

## Related Features

### AI Chat Interactions - Documents

For document upload and RAG (Retrieval Augmented Generation) support, see the [Documents feature documentation](./README-Documents.md).

## Configuration

### Settings Tab Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| Title | User-defined name for the interaction | "Untitled" |
| Connection | AI provider connection to use | Default connection |
| Deployment | AI model deployment to use | Default deployment |
| System Instructions | Persona and context for the AI | Empty |
| Max Response Tokens | Token limit for responses | Provider default |
| Temperature | Controls randomness (0-2) | Provider default |
| Top P | Nucleus sampling (0-1) | Provider default |
| Frequency Penalty | Reduces token repetition (-2 to 2) | Provider default |
| Presence Penalty | Encourages new topics (-2 to 2) | Provider default |
| Past Messages | Context window size | Provider default |

## Permissions

| Permission | Description |
|------------|-------------|
| `EditChatInteractions` | Allows users to create and manage their own chat interactions |
| `ManageChatInteractionSettings` | Allows users to configure site-wide chat interaction settings |

## Technical Details

The module uses SignalR for real-time streaming chat responses. Messages are persisted in the database using YesSql.

### API Endpoints

Chat interactions are managed through the SignalR hub at `/hubs/chat-interaction`.
