---
sidebar_label: AI Chat Session Analytics
sidebar_position: 3
title: AI Chat Session Analytics
description: Comprehensive analytics and reporting for AI chat sessions in Orchard Core, including conversation metrics, performance tracking, user segmentation, and feedback analysis.
---

| | |
| --- | --- |
| **Feature Name** | AI Chat Session Analytics |
| **Feature ID** | `CrestApps.OrchardCore.AI.Chat.Analytics` |

Provides comprehensive analytics and reporting for AI chat sessions. Track conversation metrics, user engagement, model performance, and user satisfaction through an admin dashboard.

## Overview

The **AI Chat Session Analytics** feature captures detailed metrics about every chat session — including session duration, message counts, resolution outcomes, response latency, token usage, and user feedback. All data is displayed through an interactive admin dashboard with filterable reports.

### Enabling Analytics

1. Go to **Tools** > **Features** in the admin menu.
2. Search for **AI Chat Session Analytics** and enable it.
3. Open each **AI Profile** where you want to collect metrics.
4. In the **Analytics** section of the profile editor, check **Enable Session Metrics**.
5. Navigate to **Artificial Intelligence** > **Chat Session Analytics** in the admin menu.

> **Note:** Session metrics collection is disabled by default. You must enable it per-profile in the profile editor.

### Prerequisites

- **AI Chat Services** (`CrestApps.OrchardCore.AI.Chat.Core`) must be enabled.
- At least one AI completion provider must be configured (e.g., OpenAI, Azure OpenAI, Ollama).

---

## Analytics Dashboard

The analytics dashboard provides a comprehensive view of your AI chat performance through multiple report sections. Use the **Filters** panel to narrow results by date range and AI profile.

### Filters

| Filter | Description |
| --- | --- |
| **Start Date** | Filter sessions starting from this date. Uses a flatpickr date picker for easy selection. |
| **End Date** | Filter sessions up to this date. |
| **AI Profile** | Optionally filter analytics to a specific AI chat profile. |

Click **Generate Report** to run the query and display results.

---

## Metrics Reference

### 🤖 Conversation & Usage Metrics

These metrics provide an overview of how your chatbot is being used.

| Metric | Description |
| --- | --- |
| **Total Sessions** | The total number of chat conversations started within the selected period. |
| **Unique Visitors** | The number of distinct visitors who interacted with the chat, identified by visitor ID or user account. |
| **Containment Rate** | The percentage of sessions that were fully resolved by the bot without requiring human escalation. Higher is better. Also known as **Bot-Only Resolution Rate**. |
| **Abandonment Rate** | The percentage of sessions where the user left without reaching a resolution. May indicate user frustration or irrelevant responses. |
| **Avg Session Duration** | The average time from the first message to the last message in a session. Previously called "Avg Handle Time." |
| **Avg Messages/Session** | The average number of messages (user + assistant) exchanged per session. Indicates conversation depth and engagement. Also known as **Messages per Bot Session**. |
| **Returning User Rate** | The percentage of unique visitors who engaged with the bot in more than one session. A high rate indicates users find the bot useful enough to come back. |
| **Avg Steps to Resolve** | The average number of messages needed to reach a resolution in resolved sessions. Fewer steps means faster problem-solving. |
| **Resolved Sessions** | The number of sessions that ended with a natural resolution (the bot successfully handled the request). |
| **Abandoned Sessions** | The number of sessions that ended due to inactivity timeout without resolution. |
| **Active Sessions** | The number of sessions currently in progress that have not yet ended. |

### ⏰ Usage Distribution

#### Time of Day

Shows how chat sessions are distributed across the 24 hours of the day. Each hour displays a bar chart showing relative session volume. Use this to:
- Identify **peak usage hours** for capacity planning
- Schedule maintenance during low-traffic periods
- Optimize staffing if human escalation is available

#### Day of Week

Shows how chat sessions are distributed across the seven days of the week. Helps identify:
- **Busiest days** that may need additional resources
- **Weekend vs. weekday** usage patterns
- Seasonal or recurring trends

> **Localization:** Day-of-week names (Sunday, Monday, etc.) and time labels (AM/PM) are fully localizable through the standard Orchard Core localization system (PO files).

### 👥 User Segmentation

Breakdown of chat sessions by user authentication status.

| Metric | Description |
| --- | --- |
| **Authenticated Sessions** | Sessions initiated by logged-in users with a known user account. |
| **Anonymous Sessions** | Sessions from visitors who were not logged in, tracked by a browser-generated visitor ID. |
| **Unique Logged-in Users** | The number of distinct authenticated user accounts that initiated sessions. |
| **Unique Anonymous Visitors** | The number of distinct anonymous visitors identified by their browser-generated visitor ID. |

A stacked progress bar shows the **authenticated vs. anonymous split** as a percentage.

### ⚙️ Model & System Performance

Metrics about AI model response speed and resource consumption. These are captured automatically when the AI provider returns token usage and latency data.

| Metric | Description |
| --- | --- |
| **Avg Response Latency** | The average time (in milliseconds) the AI model takes to generate a complete response. Lower latency means faster responses. |
| **Total Tokens Used** | The total number of tokens (input + output) consumed across all sessions. Token usage directly affects API costs. |
| **Input Tokens** | The total prompt tokens sent to the AI model, including conversation history, system instructions, and user messages. |
| **Output Tokens** | The total completion tokens generated by the AI model in assistant responses. |
| **Avg Tokens/Session** | The average total tokens consumed per session. Useful for estimating per-conversation costs. |
| **Avg Input Tokens/Session** | Average input tokens per session. High values may indicate lengthy conversation histories or verbose system prompts. |
| **Avg Output Tokens/Session** | Average output tokens per session. Indicates how verbose AI responses are on average. |

:::note
Performance metrics are only available when the AI provider reports token usage. Not all providers return this data for streaming responses.
:::

### 😊 User Feedback

Summary of user satisfaction ratings collected during chat sessions.

| Metric | Description |
| --- | --- |
| **Positive Ratings** | The number of sessions where users gave a thumbs up (👍), indicating a satisfactory experience. |
| **Negative Ratings** | The number of sessions where users gave a thumbs down (👎), indicating a poor experience. Review these sessions to identify improvement areas. |
| **Satisfaction Rate** | The percentage of positive ratings out of all sessions that received feedback. Higher is better. |
| **Feedback Rate** | The percentage of total sessions where users provided a rating. A low rate may indicate users aren't being prompted to rate. |

A stacked progress bar visualizes the **positive vs. negative feedback ratio**.

:::tip
If the feedback rate is low, consider adding more prominent rating prompts to your chat widget configuration.
:::

:::tip
When session metrics are enabled, thumbs up (👍) and thumbs down (👎) buttons appear on all assistant messages in the chat widget, the admin chat widget, and the AI Chat UI. Users can rate the session to provide feedback.
:::

---

## CSV Export

The analytics dashboard supports exporting filtered data as a CSV file for external analysis. Click **Export as CSV** after generating a report.

### Exported Fields

| Column | Description |
| --- | --- |
| `SessionId` | Unique identifier for the chat session |
| `ProfileId` | The AI profile used in the session |
| `VisitorId` | Persistent anonymous visitor identifier |
| `UserId` | Authenticated user ID (if available) |
| `IsAuthenticated` | Whether the user was logged in |
| `SessionStartedUtc` | Session start timestamp (ISO 8601) |
| `SessionEndedUtc` | Session end timestamp (ISO 8601) |
| `MessageCount` | Total messages in the session |
| `HandleTimeSeconds` | Session duration in seconds |
| `IsResolved` | Whether the session was resolved |

### Permissions

| Permission | Description |
| --- | --- |
| **View Chat Analytics** | Access the analytics dashboard and generate reports. |
| **Export Chat Analytics** | Download analytics data as CSV files. |

---

## How Metrics Are Collected

### Session Lifecycle

1. **Session Start**: When a user sends their first message, a session event record is created with the session ID, profile ID, visitor/user ID, and start timestamp.
2. **Message Completion**: After each AI response, token usage and response latency are accumulated on the session event record.
3. **Session End (Resolved)**: When the bot naturally concludes the conversation (e.g., farewell intent), the session is marked as resolved.
4. **Session End (Abandoned)**: A background task periodically checks for inactive sessions and closes them as "abandoned" after the configured inactivity timeout.

### Data Storage

Analytics events are stored as documents in the YesSql database using the AI collection. A map index (`AIChatSessionMetricsIndex`) provides optimized querying across multiple dimensions including:
- Session ID, Profile ID
- Date ranges (`SessionStartedUtc`, `SessionEndedUtc`)
- Time-of-day (`HourOfDay`, `DayOfWeek`)
- Visitor and user identifiers
- Resolution status

---

## Configuration

### Per-Profile Session Metrics

Session metrics collection is **disabled by default**. To enable it for a specific AI profile:

1. Edit the AI profile in **Artificial Intelligence** > **Profiles**.
2. Open the **Analytics** section.
3. Check **Enable Session Metrics**.
4. Save the profile.

When enabled, the following data is captured for each chat session:
- Session start/end timestamps and duration
- Message counts and resolution outcomes
- Token usage (input/output) per completion
- Response latency per completion
- User feedback ratings (thumbs up/down)

### User Feedback (Thumbs Up / Down)

When session metrics are enabled on a profile, assistant messages in the chat UI display thumbs up and thumbs down buttons alongside the copy button. This applies to:
- The **frontend chat widget** (Widget-AIChat)
- The **admin chat widget** (AIChatAdminWidget)
- The **AI Chat session UI** (AIChatSessionChat)

Ratings are per-session (not per-message). The most recent rating is stored and displayed in the analytics feedback section.

### Inactivity Timeout

Sessions are considered "abandoned" when they exceed the inactivity timeout configured on the AI profile's **Data Extraction** settings. The background task runs every 10 minutes to close inactive sessions.

### Token Usage

Token usage data is captured from AI completion responses when available. The amount of data depends on the AI provider:

- **Azure OpenAI**: Reports `InputTokenCount`, `OutputTokenCount`, and `TotalTokenCount`
- **OpenAI**: Reports token usage for non-streaming completions
- **Ollama**: Token reporting may vary by model

---

## Extending Analytics

The analytics system uses Orchard Core's display driver pattern, making it easy to add custom metrics:

1. Create a new **view model** in the `ViewModels/` folder
2. Create a **display driver** that extends `DisplayDriver<AIChatAnalyticsReport>`
3. Create a **Razor view** matching your shape name
4. Register the driver in `Startup.cs`

Example:

```csharp
public sealed class MyCustomMetricDisplayDriver : DisplayDriver<AIChatAnalyticsReport>
{
    public override IDisplayResult Display(AIChatAnalyticsReport context, BuildDisplayContext buildContext)
    {
        return Initialize<MyCustomViewModel>("MyCustomMetric", model =>
        {
            // Compute metrics from context.Events
            model.MyValue = context.Events.Count(e => /* your condition */);
        }).Location("Content:10"); // Position in the report
    }
}
```

Then register in your module's `Startup.cs`:

```csharp
services.AddDisplayDriver<AIChatAnalyticsReport, MyCustomMetricDisplayDriver>();
```

---

## AI Tools for Analytics

When the **AI Chat Session Analytics** feature is enabled alongside the **Orchard Core AI Agent** feature, three AI tools become available for use in chat profiles:

### Query Chat Session Metrics

| | |
| --- | --- |
| **Tool Name** | `queryChatSessionMetrics` |
| **Category** | AI Analytics |

Queries aggregated chat session metrics from the analytics index. Returns statistics like total sessions, average messages per session, resolution rate, average handle time, token usage, rating distribution, and breakdowns by hour-of-day and day-of-week.

**Parameters:**

| Parameter | Type | Description |
| --- | --- | --- |
| `profileId` | string | Optional. Filter metrics to a specific AI profile by its ID. |
| `startDateUtc` | string | Optional. Start date in ISO 8601 format. |
| `endDateUtc` | string | Optional. End date in ISO 8601 format. |

:::tip
Combine this tool with the **Generate Chart** tool to create visual dashboards. The AI model can query metrics and then render them as Chart.js visualizations.
:::

### List AI Profiles

| | |
| --- | --- |
| **Tool Name** | `listAIProfiles` |
| **Category** | AI Profiles |

Lists AI profiles with optional filters for type and enabled features.

**Parameters:**

| Parameter | Type | Description |
| --- | --- | --- |
| `type` | string | Optional. Filter by profile type: `Chat`, `Utility`, or `Embedding`. |
| `onlyWithMetricsEnabled` | boolean | Optional. Only return profiles with session analytics enabled. |
| `onlyWithDataExtraction` | boolean | Optional. Only return profiles with data extraction enabled. |
| `onlyWithPostSessionProcessing` | boolean | Optional. Only return profiles with post-session processing enabled. |

### View AI Profile

| | |
| --- | --- |
| **Tool Name** | `viewAIProfile` |
| **Category** | AI Profiles |

Retrieves detailed configuration for a specific AI profile including analytics settings, data extraction entries, and post-session processing tasks.

**Parameters:**

| Parameter | Type | Description |
| --- | --- | --- |
| `profileId` | string | The unique ID of the AI profile. |
| `profileName` | string | The technical name (used if `profileId` is not provided). |

---

## Post-Session Processing

Post-session processing runs after a chat session is closed and performs AI-powered analysis on the full conversation transcript. Configure tasks in the **Data Processing** tab of each AI profile.

### Task Types

| Type | Description |
| --- | --- |
| **Predefined Options** | The AI selects one or more values from a predefined list. Used for dispositions, classifications, or any scenario where outcomes are known upfront. Each option has a **Value** (the identifier stored as the result) and an optional **Description** (helps the AI understand when to select this option). An **Allow Multiple Values** checkbox controls whether the AI can select more than one option. |
| **Semantic** | The AI generates freeform text based on the provided instructions. Used for summaries, sentiment analysis, or any open-ended analysis task. |

### Configuring Tasks

1. Edit the AI profile in **Artificial Intelligence** > **Profiles**.
2. Open the **Data Processing** tab.
3. Check **Enable Post-Session Processing**.
4. Click **Add Task** to create a new task.
5. Configure the task:
   - **Name**: A unique identifier (alphanumeric + underscores only).
   - **Type**: Choose **Predefined Options** or **Semantic**.
   - **Instructions**: Guidance for the AI model on how to process this task.
   - For **Predefined Options**: Add options with values and optional descriptions.
6. Save the profile.

### Example: Disposition Task

A typical disposition task classifies the outcome of a conversation:

- **Name**: `disposition`
- **Type**: Predefined Options
- **Instructions**: "Classify the outcome of this conversation based on the user's final state."
- **Options**:
  - Value: `Resolved`, Description: "The user's question or issue was fully addressed."
  - Value: `Escalated`, Description: "The user requested to speak with a human agent."
  - Value: `Abandoned`, Description: "The user left without completing their request."

### Example: Semantic Summary Task

- **Name**: `summary`
- **Type**: Semantic
- **Instructions**: "Write a concise 2-3 sentence summary of the conversation."
