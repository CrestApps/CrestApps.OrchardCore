# AG-UI for the AI chat and the admin widget — plan

Status: done (2026-11-12). The protocol, the hub, the HTTP endpoint and the chat UI landed in CrestApps.Core (#317, #318, #320; package `2.0.0-preview.373`, `@crestapps/ai-chat-ui` and `@crestapps/webmcp` `2.0.0-preview.258`). The Orchard Core side landed with the admin widget page context, the destructive AI Agent tools marked for approval, the AG-UI endpoint under the AI Chat API feature and localized AG-UI texts. See "Outcome" at the end for what was built, how it differs from this plan, and what is left.

## 1. Short answers

| Question | Answer |
| --- | --- |
| Is AG-UI implemented? | No. No AG-UI events, no `RunAgentInput`, no SSE agent endpoint, no AG-UI or Microsoft Agent Framework package in either repository. |
| Can the admin widget already act on the page with the user? | Partly. The WebMCP chat bridge lets the model call the page's tools during a turn: read the page, list its links, open a page, read and fill form fields (never submit). It is invisible to the user, all-or-nothing for consequential tools, and has no shared state. |
| What does AG-UI add for "doing things together"? | A visible run (steps, tool calls with arguments and results, reasoning), shared state the agent and the page both edit, context the page sends with each message, and interrupts that ask the user before a consequential action. |
| Where does it belong? | The protocol, the event projection and the hub changes go to **CrestApps.Core** (`CrestApps.Core.AI.AGUI`, `CrestApps.Core.AI.Chat`, `@crestapps/ai-chat-ui`). Orchard Core gets a thin layer: context providers, approval settings, the endpoint mapping and the admin widget configuration. See the "Building Blocks and Reuse" rules in `AGENTS.md`. |
| Do we need Microsoft Agent Framework? | No. The AG-UI .NET SDK (`AGUI.Abstractions`, `AGUI.Server`, `AGUI.Formatting`, released 2026-09) works on a plain `Microsoft.Extensions.AI` `IChatClient`. Agent Framework's `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` is only needed for Agent Framework agents. |
| Does the SignalR widget have to move to SSE? | No. AG-UI is transport-agnostic; the widget keeps SignalR and receives AG-UI events over it. The SSE endpoint is for other AG-UI clients (CopilotKit and the like). |

## 2. What exists today

All Core paths are under `CrestApps.Core/src`.

### 2.1 The admin widget

- `AIChatAdminWidgetFilter` adds the `AIChatAdminWidget` shape to the `Footer` zone of every admin page for signed-in users allowed to query the profile chosen in **Settings → AI Chat Admin Widget**.
- `Views/AIChatAdminWidget.cshtml` builds the widget configuration (hub URL, chat mode, documents, realtime) for `ai-chat-widget.js` from `@crestapps/ai-chat-ui`. It sends **nothing about the page** the user is on.

### 2.2 The chat hub and the stream

- `AIChatHubCore<TClient>.SendMessage(profileId, prompt, sessionId, sessionProfileId)` (`Primitives/CrestApps.Core.AI.Chat/Hubs/AIChatHubCore.cs:776`) returns `ChannelReader<CompletionPartialMessage>`. The Orchard Core hub, `src/Core/CrestApps.OrchardCore.AI.Chat.Core/Hubs/AIChatHub.cs`, derives from it.
- `CompletionPartialMessage` carries `MessageId`, `ResponseId`, `Content`, `SessionId`, `References` and `Appearance`: text and citations only.
- Pipeline: `AIChatResponseHandler` → `IOrchestrator.ExecuteStreamingAsync` → `IAICompletionService` → `NamedAICompletionClient`, which wraps the provider client in Microsoft.Extensions.AI's `FunctionInvokingChatClient`.
- The `ChatResponseUpdate` stream does contain `FunctionCallContent` and `FunctionResultContent`, but `ProcessChatPromptAsync` skips every update without text (`AIChatHubCore.cs:2306`, `if (string.IsNullOrEmpty(chunk.Text)) continue;`). The only trace of a tool call in the UI is a `ResponseId` change, rendered as a blank line. `ChatInteractionHubBase` does the same.
- History is text-only: `AIChatSessionPrompt` stores `Content` and `References`, and `ChatConversationHistoryBuilder` rebuilds plain `ChatMessage(role, text)` messages. Tool calls are never saved.
- Reasoning (`TextReasoningContent`) is never used.

### 2.3 Page tools (WebMCP chat bridge)

The closest thing to AG-UI frontend tools, and it already works mid-turn:

1. Before each message, `ai-chat.js` calls `crestAppsWebMcp.connectChat(connection).sync()`, which sends `document.modelContext.getTools()` to the hub with `SetPageTools` (`AIChatHubCore.cs:791`). The hub keeps them per connection in `IWebMcpPageToolRegistry` and drops them `OnDisconnectedAsync`.
2. `WebMcpPageToolRegistryProvider` offers them to the model as `WebMcpPageToolFunction` when the profile's `WebMcpProfileSettings.AllowPageTools` is on, and leaves out `consequentialHint` tools unless `AllowConsequentialPageTools` is on.
3. A call runs `HubWebMcpPageToolClient<THub>.ExecuteAsync`, which uses SignalR client results (`Clients.Client(id).InvokeAsync<WebMcpPageToolResult>("ExecutePageTool", ...)`) and waits up to `ToolCallTimeout` (one minute). `FunctionInvokingChatClient` blocks inside the turn while it waits.

Built-in page tools (`webmcp-page-tools.js`): `page_get_info`, `page_list_links`, `page_navigate`, `page_list_form_fields`, `page_set_form_field`. Gating in Orchard Core: the WebMCP feature, the **Use WebMCP tools in the browser** permission, and the profile's **Allow page tools**. `ChatInteractionHubBase` has no `SetPageTools`, so Chat Interactions cannot use page tools.

`page_navigate` calls `location.assign()`, which unloads the page and its SignalR connection in the middle of the turn. Multi-page tasks therefore end at the first navigation (to confirm in a live test: the stream's token is cancelled on disconnect and the page tools are removed with the connection).

### 2.4 Server tools

`CrestApps.OrchardCore.AI.Agent` has 52 tools: content, content types, features, recipes, roles, tenants, users, workflows, communications, analytics. Several are destructive (`DeleteContentTool`, `RemoveContentTypeDefinitionsTool`, `DisableFeatureTool`, `RemoveTenantTool`, `DisableTenantTool`, `SetupTenantTool`, `ImportOrchardTool`, `ExecuteStartupRecipesTool`, `SendEmailTool`, `SendSmsTool`) and run without asking.

### 2.5 Pieces to reuse

| Need | Existing piece |
| --- | --- |
| Per-turn data | `ChatResponseHandlerContext.Properties`, `AICompletionContext.AdditionalProperties` (`AICompletionContextKeys`), `AIInvocationScope.Current.Items` |
| Per-session data | `AIChatSession : ExtensibleEntity` (`Put`/`TryGet`), `AIChatSession.ExtractedData` |
| A UI action channel | `ChatNotification` buttons → `HandleNotificationAction` → keyed `IChatNotificationActionHandler` |
| A server-to-client push | `ReceiveNotification`/`UpdateNotification`, the `aichat-session-{id}` session group |
| Tool approval | `Microsoft.Extensions.AI` 10.11: `ApprovalRequiredAIFunction`, `ToolApprovalRequestContent`, `ToolApprovalResponseContent` |
| SSE lifecycle example | The A2A host samples (`Startup/CrestApps.Core.*.Web/Services/A2AHostExtensions.cs`) |
| Endpoint style | `MapWebMcpTools` route group (authentication, antiforgery, access evaluator) |

## 3. Gaps against AG-UI

| AG-UI | Today | Needed |
| --- | --- | --- |
| `RUN_STARTED` / `RUN_FINISHED` / `RUN_ERROR` | Implied by the stream opening and closing; errors go to `ReceiveError` | A run id per turn and the three events |
| `STEP_STARTED` / `STEP_FINISHED` | None | Orchestrator planning, retrieval (RAG) and document steps as steps |
| `TEXT_MESSAGE_START/CONTENT/END` | `CompletionPartialMessage` chunks | A direct mapping |
| `TOOL_CALL_START/ARGS/END/RESULT` | Dropped at `AIChatHubCore.cs:2306` | Projection from `FunctionCallContent`/`FunctionResultContent`; saved in history |
| `REASONING_*` | Not used | Projection from `TextReasoningContent`, behind a profile option |
| `STATE_SNAPSHOT` / `STATE_DELTA` | None | Session state, JSON Patch deltas, a state tool for the agent |
| `MESSAGES_SNAPSHOT` | `LoadSession(object)` payload | The same data in AG-UI messages, tool messages included |
| `RunAgentInput.context` / `forwardedProps` | `SendMessage` takes strings only | A context list from the page, with Orchard Core providers |
| `RunAgentInput.tools` (frontend tools) | `SetPageTools` before each message | Keep for SignalR; accept `tools` on the SSE endpoint |
| Interrupts (`RUN_FINISHED` with `outcome: interrupt`, `resume`) | None; consequential page tools are a profile switch | Approval-required tools that pause the run and resume with the user's answer |
| `ACTIVITY_SNAPSHOT` / `ACTIVITY_DELTA` | `[chart:]`, `[fig:]`, `[doc:]` text markers | Optional: plans and progress cards |
| Standard transport (HTTP `POST` + SSE) | SignalR only | `POST /api/ai/agui/{profileId}` |

## 4. Design decisions

### 4.1 One event model, two transports

AG-UI events become the chat's internal stream contract. The SignalR hub streams them to our own UI; an SSE endpoint streams the same events to any AG-UI client. One projector, so the two cannot drift.

- New hub method `RunAgent(RunAgentInput input)` returning `ChannelReader<BaseEvent>` (AG-UI SDK types). `SendMessage` stays, implemented on top of the projector, until `@crestapps/ai-chat-ui` has moved and one release has passed.
- The thread id is the chat session id. The run id is new, generated per turn and saved on the assistant prompt. The message id stays the prompt `ItemId`.

### 4.2 Frontend tools: keep the round trip on SignalR, use the AG-UI flow on SSE

The AG-UI standard ends the run at a frontend tool call; the client runs the tool and starts a new run carrying the result. Our SignalR round trip (`ExecutePageTool` with client results) already does the same work inside one run, needs no history reconciling and works in a web farm.

- **SignalR (widget):** keep the round trip. Emit `TOOL_CALL_*` events around it so the user sees it.
- **SSE (other clients):** take `RunAgentInput.tools`, expose them as declaration-only functions, end the run when the model calls one (`FunctionInvokingChatClient` terminates on functions it cannot invoke), and accept the tool result from the next run's `messages`.

### 4.3 Approvals pause the run; they never hold it open

A user can take minutes to decide, and the page-tool wait already times out at one minute. Approval-required tools use the AG-UI interrupt flow on both transports:

1. The tool is wrapped in `ApprovalRequiredAIFunction`. `FunctionInvokingChatClient` returns a `ToolApprovalRequestContent` instead of running it.
2. The projector ends the run with `RUN_FINISHED`, `outcome: interrupt`, one interrupt per request (tool name, arguments, a readable summary). The pending requests are saved on the session.
3. The widget shows Approve / Reject (with the arguments) in the message list.
4. The answer starts a new run with `resume`; the hub turns it into `ToolApprovalResponseContent` and the run continues.

This replaces the all-or-nothing **Allow consequential page tools** switch with "ask every time" for those tools.

### 4.4 Context is data, state is shared

- **Context** (`RunAgentInput.context`, read-only for the agent): what the user is looking at. Sent with every run, placed in the system prompt as data, never as instructions.
- **State** (`STATE_*`, editable by both sides): what the user and the agent are working on together. Stored on `AIChatSession`, changed by the agent through a `update_shared_state` tool (JSON Patch) and by the page through the next run's `state`. Size and schema limits like the page-tool limits.

### 4.5 A run survives a page navigation

The admin widget restores its session after a page load, but the run dies with the connection. With run ids saved on the session:

- `page_navigate` answers first, then navigates (it already returns `navigatedTo`); the tool result is saved before the page unloads.
- The run continues server-side to the end of the current model call and saves its output, instead of being cancelled by the disconnect.
- The widget on the new page reconnects to the session, receives `MESSAGES_SNAPSHOT` plus any interrupt still open, and the agent continues with the new page's tools on the next run.

Needs a decision on how long a run may continue without a connection, and a cap on runs per session.

## 5. Phases

One branch and one PR per repository, a commit per slice. Core first, then a Core preview, then the Orchard Core side.

### Phase 1 — Event projection and history (CrestApps.Core)

- Add `CrestApps.Core.AI.AGUI` (references the AG-UI SDK, `AGUI.Abstractions` + `AGUI.Formatting`; check version, license and target frameworks before taking it on).
- `AGUIEventProjector`: `IAsyncEnumerable<ChatResponseUpdate>` → `IAsyncEnumerable<BaseEvent>` for text, tool calls, tool results, reasoning and references (references as a `CUSTOM` event until AG-UI has citations).
- Refactor `AIChatHubCore.ProcessChatPromptAsync` to consume the projector. Add `RunAgent`. Keep `SendMessage` through an adapter.
- Save tool calls and results on `AIChatSessionPrompt` (new optional fields, no migration of old rows) and replay them in `ChatConversationHistoryBuilder`, cut to a size limit.
- The same for `ChatInteractionHubBase`, plus `SetPageTools` there.

### Phase 2 — Show the run (`@crestapps/ai-chat-ui`)

- Consume `RunAgent` and render events: tool-call cards (name, readable arguments, running / done / failed, result on expand), step progress, a collapsible reasoning block.
- Friendly labels for page tools (e.g. "Filling Title") and for server tools through a `displayName` in the tool descriptor.
- Bump `@crestapps/ai-chat-ui` in `CrestApps.OrchardCore.AI.Chat/package.json` and rebuild `wwwroot`.

### Phase 3 — Page context (Core + Orchard Core)

- Core: `IAGUIContextProvider` (server side) and a `context` collector in `ai-chat-ui` (client side), merged into the run input.
- Client context from every page: URL, title, admin or front end, selected text.
- Orchard Core providers: the content item and type being edited (from the route), the current admin menu section, the tenant. Each in the module that owns the concept.
- Admin widget: on by default, with a setting to turn it off.

### Phase 4 — Approvals (Core + Orchard Core)

- Core: the interrupt flow of section 4.3 on the hub and the projector; Approve / Reject UI in `ai-chat-ui`.
- Core: a tool can declare that it needs approval (a flag on `AIToolDefinitionEntry`, set at registration). WebMCP `consequentialHint` tools need approval instead of being hidden.
- Orchard Core: mark the destructive `AI.Agent` tools listed in section 2.4. A per-profile setting for tools that always / never need approval, and a deployment step and recipe step for it.

### Phase 5 — Shared state (Core + Orchard Core)

- Core: session state, `STATE_SNAPSHOT` on run start and `STATE_DELTA` after each change, the `update_shared_state` tool, limits.
- Orchard Core: the first use is the content editor. The state is the form being edited (from `page_list_form_fields`); the agent proposes changes as a delta, the widget shows them as a diff the user applies or rejects field by field.

### Phase 6 — Runs across navigation (Core)

Section 4.5.

### Phase 7 — SSE endpoint (Core + Orchard Core)

- Core: `MapAGUIChat(pattern)` on `AGUI.Server`, with the frontend-tool flow of section 4.2 and the same projector.
- Orchard Core: map `POST /api/ai/agui/{profileId}` in `CrestApps.OrchardCore.AI.Chat` behind the Api authentication scheme and profile authorization, rate limited when Rate Limits is on. Off by default, turned on per profile.
- Docs: Technical Manual and User Manual pages, changelog entries.

### Phase 8 — Optional

Activity events for plans and progress, and moving the `[chart:]` / `[fig:]` / `[doc:]` markers to activity events.

## 6. Open questions

1. Take the AG-UI .NET SDK as a dependency, or write our own event types against the spec (it is small)? The SDK gives wire compatibility and an SSE formatter; check its license and preview status first.
2. How long may a run continue after its connection drops (section 4.5)?
3. Should the widget send context to every profile, or only to profiles that turn it on?
4. Who may approve on behalf of a tool? Approval is by the signed-in user; the tool's own permission check still runs when it executes.
5. Do the realtime voice sessions emit the same events (tool calls during a voice conversation)? Leave out of the first version.

## 7. References

- AG-UI events: https://docs.ag-ui.com/concepts/events
- AG-UI .NET SDK announcement: https://devblogs.microsoft.com/dotnet/ag-ui-dotnet-sdk/
- Agent Framework AG-UI integration: https://learn.microsoft.com/agent-framework/integrations/ag-ui
- WebMCP in this repository: `src/CrestApps.Docs/docs/ai/mcp/webmcp.md`

## 8. Outcome

What was built differs from the plan above in these ways:

- **SDK:** `AGUI.Abstractions` and `AGUI.Formatting` 1.0.1 (MIT) with an own projector (`AGUIRunEventWriter`), not `AGUI.Server`.
- **Transports:** the hub's `RunAgent` streams AG-UI events; `MapAGUIChat<THub, TClient>()` serves the same runs over HTTP and SSE through a headless hub. Orchard Core maps it at `POST /api/ai/v1/agui` under the **AI Chat API** feature (API key, bearer token or cookie).
- **Frontend tools:** the page tools keep the SignalR round trip; `RunAgentInput.tools` from other clients end the run waiting for the client, as planned.
- **Approvals:** `.RequireApproval()` on tool registration; interrupts and `resume` as in 4.3. Orchard Core marks the 11 destructive AI Agent tools. WebMCP `consequentialHint` tools ask instead of being hidden.
- **Context:** the chat UI sends the page title, address and selected text; Orchard Core adds `IAIChatPageContextProvider` (content item being edited or created, site and tenant), with the admin widget setting **Tell the AI which page the user is on**.
- **State:** `update_shared_state` with JSON Patch, `STATE_SNAPSHOT`/`STATE_DELTA`. No Orchard Core content-editor state provider yet.
- **Navigation (4.5):** `page_navigate` ends the turn in a hub run; the chat on the next page continues it with `forwardedProps.continueRun` (at most five pages in a row). A run keeps going for two minutes after its connection drops. `page_read_content` reads the page's text and tables.

Left for later:

1. Per-profile overrides of which tools need approval (always / never), with a deployment and recipe step.
2. A content-editor shared state provider (the form as state, the agent's changes as a reviewable diff).
3. AG-UI for Chat Interactions (`ChatInteractionHubBase`) and the realtime voice conversation.
4. Rate limiting on the AG-UI endpoint when the Rate Limits feature is on.

