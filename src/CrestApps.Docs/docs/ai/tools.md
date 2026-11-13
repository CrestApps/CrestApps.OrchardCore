---
title: AI Tools
description: Shared tool registration and orchestration concepts are documented in CrestApps.Core.
user_manual:
  - user-manual/ai/tools-and-agents
---

# AI Tools

The shared tool system is documented primarily in **CrestApps.Core**:

- [Tools](https://core.crestapps.com/docs/core/tools)
- [Agents](https://core.crestapps.com/docs/core/agents)
- [MCP](https://core.crestapps.com/docs/mcp/index)
- [A2A](https://core.crestapps.com/docs/a2a/index)

This page is the Orchard-specific catalog for the AI functions registered or documented in this repository. Shared framework utilities that are not registered by Orchard features in this repo, along with external MCP and A2A tools, continue to be documented in the shared Core docs.

Within Orchard Core, tools become useful through the modules that register or expose them:

- [AI Services](overview)
- [AI Agents](agent)
- [AI Documents](./documents/)
- [AI Data Sources](./data-sources/)
- [MCP](./mcp/)
- [A2A](./a2a/)

## Orchard-specific AI function catalog

Use this catalog to see which Orchard feature makes each AI function available and what it does. When the codebase adds, removes, renames, or re-describes an AI function, update this page together with the related feature docs so the catalog stays synchronized with the current registrations.

Tools marked as hidden in the shared Core registry are not shown in Orchard Core capability pickers such as AI Profile, AI Profile Template, Chat Interaction, workflow-task, or post-session tool selectors. Hidden tools are reserved for internal orchestration paths such as system agents that reference them explicitly by name.

This catalog only lists functions registered in code. Functions created by administrators from a tool instance source are not listed here because they are tenant data rather than code registrations. See [AI Tool Instances](tool-instances) for that surface.

### System tools

**Available when:** `CrestApps.OrchardCore.AI.Agent`

| Function | Description |
| --- | --- |
| `listTimeZones` | Retrieves a list of the available time zones in the system. |
| `listAIProfiles` | Lists AI profiles with optional filters for type, analytics, data extraction, and post-session processing. |
| `viewAIProfile` | Retrieves detailed configuration for a specific AI profile by ID or name. |
| `listAIConversations` | Lists AI conversations on every channel, newest first, by profile, status and time. |
| `viewAIConversation` | Reads a conversation's transcript, variables, extracted fields, analysis, evaluations and flow stages. |
| `getAIContactMemory` | Reads what the AI remembers about a contact. |

### AI quality tools

With the **AI Quality** feature on, the admin AI agent can also look at QA results and run tests, so an admin can ask questions such as "why did calls fail yesterday?" and get proposed fixes to review.

| Function | Description |
| --- | --- |
| `summarizeAIQuality` | Sums up QA evaluations over a period: count, average score, pass rate, resolution, sentiment, the most common issues, the questions asked most and the trend by day. |
| `listAITestCases` | Lists the simulation test cases. |
| `runAITests` | Queues test cases to run in the background and returns the batch. |
| `listAIAlerts` | Lists the alert rules and which are open. |

### Recipe tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Recipes.Core`

| Function | Description |
| --- | --- |
| `applySiteSettings` | Applies predefined system configurations and settings using AI assistance. |
| `getOrchardCoreRecipeJsonSchema` | Returns the Orchard Core recipe root JSON Schema with a `steps` array. It can limit the schema to one step definition while still exposing every valid recipe step name. Call it first before building recipe JSON. |
| `listOrchardCoreRecipeStepsAndSchemas` | Lists all available Orchard Core recipe steps and returns their JSON schema definitions. |
| `importOrchardCoreRecipe` | Imports and runs Orchard Core recipes within your site. Call `getOrchardCoreRecipeJsonSchema` first and match that schema. |
| `listNonStartupRecipes` | Retrieves all available Orchard Core recipes that are not executed during startup. |
| `executeNonStartupRecipe` | Executes Orchard Core recipes that are not configured to run at application startup. |

### Tenant tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Tenants`

| Function | Description |
| --- | --- |
| `listStartupRecipes` | Retrieves a list of Orchard Core recipes configured to run at application startup. |
| `createTenant` | Creates a new tenant in the Orchard Core application. |
| `getTenant` | Retrieves detailed information about a specific tenant. |
| `listTenant` | Returns information about all tenants in the system. |
| `enableTenant` | Enables a tenant that is currently disabled. |
| `disableTenant` | Disables a tenant that is currently active. |
| `removeTenant` | Removes an existing tenant that can be safely deleted. |
| `reloadTenant` | Reloads the configuration and state of an existing tenant. |
| `setupTenant` | Sets up new tenants. |

### Content tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Contents`

| Function | Description |
| --- | --- |
| `searchForContentItems` | Searches for content items. |
| `getSampleContentItemForContentType` | Generates a structured sample content item for a specified content type. |
| `getContentItemSchema` | Returns the current content-item JSON schema for one or more Orchard Core content types. Call it immediately before `createOrUpdateContentItem` whenever that tool is available. |
| `publishContentItem` | Publishes a draft or previously unpublished content item. |
| `unpublishContentItem` | Unpublishes a currently published content item. |
| `getContentItemById` | Retrieves a specific content item by its ID or type. |
| `deleteContentItem` | Deletes a content item from the system. |
| `cloneContentItem` | Creates a duplicate of an existing content item. |
| `createOrUpdateContentItem` | Creates a new content item or updates an existing one. Before calling it, call `getContentItemSchema` first whenever available, then call it once for the top-level item and include nested or contained items in the same payload. |
| `getLinkForContentItem` | Retrieves a link for a content item. |

`getOrchardCoreRecipeJsonSchema` should be called immediately before `importOrchardCoreRecipe` whenever recipe-backed JSON needs to be generated. Its response always describes the root recipe object with a `steps` array; when you request a specific step, the schema still lists every valid step name in `steps[].name` but only expands the selected step's payload contract. If you ask for an unknown step, the tool returns an error that includes the available step names so the request can be retried with a valid identifier.

When `createOrUpdateContentItem` is available alongside recipe-backed content schema support, call `getContentItemSchema` immediately before it and request the parent content type plus any nested content types that will appear in the payload so the model can inspect the current content-item contract.

`createOrUpdateContentItem` must be called for the parent content item only. When the payload contains nested or contained content items such as `BagPart`, `FlowPart`, widgets, or blocks, include those child items inside the parent JSON instead of invoking the tool once per child item. During creation, the tool now initializes each nested content item with Orchard Core's `NewAsync()` flow before merging the authored payload, so nested handlers run for child items too. The tool also rejects payloads when the submitted JSON does not match the expected Orchard Core content-item shape. By default it uses dropped-value detection after `ContentItem` mapping to catch misplaced or unmapped values. If `CrestApps.OrchardCore.Recipes` is enabled, the tool validates the payload against the content type's recipe-backed JSON schema as authored, uses the registered contained-part schema metadata to find nested child payloads recursively, and returns the same schema contract in its corrective guidance so the model can retry with the correct structure.

### Content definition tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.ContentTypes`

| Function | Description |
| --- | --- |
| `getContentTypeDefinition` | Retrieves the definitions of all available content types. |
| `getContentPartDefinition` | Retrieves the definitions of all available content parts. |
| `listContentTypesDefinitions` | Provides a list of available content type definitions. |
| `listContentPartsDefinitions` | Provides a list of available content part definitions. |
| `listContentFieldDefinitions` | Provides a list of available content fields. |

### Content definition recipe tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.ContentTypes` + `OrchardCore.Recipes.Core`

| Function | Description |
| --- | --- |
| `removeContentTypeDefinition` | Removes the content type definition. |
| `removeContentPartDefinition` | Removes the content part definition. |
| `applyContentTypeDefinitionFromRecipe` | Creates a new content type definition or updates an existing one. |

### Feature management tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Features`

| Function | Description |
| --- | --- |
| `disableSiteFeature` | Disables site features. |
| `enableSiteFeature` | Enables site features. |
| `searchSiteFeature` | Searches available features for a match. |
| `listSiteFeature` | Retrieves available site features. |
| `getSiteFeature` | Retrieves info about a feature. |

### Communication tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + one or more communication features

| Function | Required feature | Description |
| --- | --- | --- |
| `sendNotification` | `OrchardCore.Notifications` | Sends a notification message to a user. |
| `sendEmail` | `OrchardCore.Email` | Sends an email message on behalf of the logged-in user. |
| `sendSmsMessage` | `OrchardCore.Sms` | Sends an SMS message to a user. |

### User and role tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + the matching user-management feature

| Function | Required feature | Description |
| --- | --- | --- |
| `getUserInfo` | `OrchardCore.Users` | Gets information about a user. |
| `searchForUsers` | `OrchardCore.Users` | Searches the system for users. |
| `getRoleInfo` | `OrchardCore.Roles` | Gets information about a role. |

### Workflow tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + the matching workflow feature set

| Function | Required feature(s) | Description |
| --- | --- | --- |
| `getWorkflowType` | `OrchardCore.Workflows` | Gets information about a workflow type. |
| `listWorkflowTypes` | `OrchardCore.Workflows` | Lists information about workflow types. |
| `createOrUpdateWorkflow` | `OrchardCore.Workflows` + `OrchardCore.Recipes.Core` | Creates or updates a workflow. |
| `listWorkflowActivities` | `OrchardCore.Workflows` + `OrchardCore.Recipes.Core` | Lists all available tasks and activities for workflows. |

### Analytics tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.AI.Chat.Analytics`

| Function | Description |
| --- | --- |
| `queryChatSessionMetrics` | Queries aggregated chat session analytics metrics with optional date range and profile filters, returning statistics for charts and reports. |

### Memory tools

**Available when:** `CrestApps.OrchardCore.AI.Memory` is enabled and user memory is enabled for the current authenticated user

| Function | Description |
| --- | --- |
| Search User Memories | Semantic search across the current user's saved memories. |
| List User Memories | Enumerates the current user's existing memories. |
| Save User Memory | Creates or updates a named memory entry for the current user. |
| Remove User Memory | Removes a saved memory entry when it should be forgotten. |

### NLWeb tools

**Available when:** `CrestApps.OrchardCore.NLWeb` with `CrestApps.OrchardCore.AI` on

The tool answers as the caller, with the **Ask the site questions with NLWeb** (`QueryNLWeb`) permission, and only returns published content items the caller may view. See [NLWeb](../modules/nlweb.md#the-ask-tool).

| Function | Description |
| --- | --- |
| `ask` | Asks the site a question in natural language (NLWeb) and returns the matching content items as schema.org objects, with a summary or an answer written from them when asked. Selectable on the MCP Server, WebMCP and AI profiles. |

### Theme design tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.ThemeDesigner`

The tools act as the signed-in user and change only that user's theme draft, which only they see until it is published. Front-end templates need **Manage templates**, admin templates need **Manage admin templates** (and the Admin Templates feature), and site stylesheets and scripts need **Manage site stylesheets and scripts**. `publishThemeDesign` and `discardThemeDesign` ask the user to approve each call when they are ticked on the assistant the user chats with. The **Theme Designer Agent** profile template bundles the other fourteen, since tools an agent runs for an assistant are not put to the user for approval. See [Theme Designer](../modules/theme-designer.md#ai-tools).

| Function | Description |
| --- | --- |
| `getThemeDesignOverview` | Returns the active front-end and admin themes, what the user may change, the changes in their draft, the review page address and the theme design guide (the `theme-design-guide` prompt template, which a site can override). |
| `listThemeShapes` | Lists the shape types and alternates the active front-end or admin theme renders, with the file that renders each and whether a template replaces it (`scope`, `search`, `take`). |
| `getThemeShapeSource` | Returns the Liquid or Razor source the theme renders a shape with, so a template can keep its markup. |
| `listShapeTemplates` | Lists the published front-end or admin templates merged with the draft's changes. |
| `getShapeTemplate` | Returns a template's Liquid as published and as the draft has it. |
| `saveShapeTemplate` | Adds or replaces a Liquid template in the draft after checking that it parses. A Layout must keep the resource tags and `render_body`, and must keep loading the stylesheets and scripts the theme's own Layout loads, unless it loads Bootstrap itself (a new theme) or names them in `dropThemeResources`. Warns about heavy inline styling. |
| `removeShapeTemplate` | Removes a published template in the draft, or drops one the draft added. |
| `listSiteResources` | Lists the published site stylesheets and scripts merged with the draft's changes. |
| `getSiteResource` | Returns a site resource with its content as published and as the draft has it. |
| `saveSiteResource` | Adds or changes a stylesheet or script in the draft, matched by name; only the given arguments change an existing one. It is validated and minified. |
| `removeSiteResource` | Removes a published site resource in the draft, or drops one the draft added. |
| `previewThemeDraftPage` | Renders a page of the site as the user, with their draft, and returns its status, any error, the stylesheets it loads (with their size and whether they are the site's own) and its scripts, the templates that rendered each part (with the name a replacement must take) and its body markup. |
| `revertThemeDesignChange` | Drops one change from the draft. |
| `setThemeDesignPreview` | Turns on or off whether the user sees their draft while browsing. |
| `publishThemeDesign` | Publishes every change in the draft for everyone, all or nothing. Requires approval. |
| `discardThemeDesign` | Deletes the draft. Requires approval. |

### Search Engine Optimization tools

**Available when:** `CrestApps.OrchardCore.Seo` with `CrestApps.OrchardCore.AI` on

The tools set up schema.org structured data. Changing a content type needs **Edit content types**, and changing the site settings needs **Manage the advanced SEO settings**. See [Advanced SEO](../modules/advanced-seo.md#ai-tools).

| Function | Description |
| --- | --- |
| `getSchemaOrgOptions` | Lists the content types with their schema.org type and the types the editor knows; for a content type, its settings, the values its items can give, a type's properties and the suggested mappings. |
| `configureContentTypeSchemaOrg` | Adds the Schema.org part to a content type if needed, sets its schema.org type and maps its properties, adding the suggested mappings unless told not to. Returns the required properties still missing. |
| `configureSiteStructuredData` | Reads or changes the site's publisher, logo and profiles, and whether pages describe the web site and its search. |

### Calendar tools

**Available when:** `CrestApps.OrchardCore.Calendars` with `CrestApps.OrchardCore.AI` on

These tools act for the signed-in person using the AI, with that person's access to each calendar, and refuse anonymous callers and callers without **Use calendars** (`AccessCalendars`). See [Calendars](../modules/calendars.md#ai-tools).

| Function | Description |
| --- | --- |
| `listCalendars` | Lists the calendars the user can see. |
| `findCalendarEvents` | Lists the events on the user's calendars between two times (at most 62 days apart). |
| `findFreeTime` | Finds times when the user and others are free, optionally together with calendars such as a room (at most 31 days). |
| `createCalendarEvent` | Adds an event to one of the user's calendars, or to their personal calendar when none is named. |
| `moveCalendarEvent` | Moves an event, or one occurrence of a repeating event, to another time. |
| `cancelCalendarEvent` | Cancels an event, or one occurrence of a repeating event. |

### Scheduling tools

**Available when:** `CrestApps.OrchardCore.Scheduling` with `CrestApps.OrchardCore.AI` on

These tools are meant for customer-facing assistants and also work for anonymous visitors. They offer only appointment types that are enabled and allow self-service, unless the caller has **Manage appointments**, and hold customers to the appointment type's booking policies. See [Scheduling](../modules/scheduling.md#ai-tools).

| Function | Description |
| --- | --- |
| `listAppointmentTypes` | Lists the kinds of appointments that can be booked. |
| `findAppointmentSlots` | Finds open times for an appointment. |
| `bookAppointment` | Books an appointment for the customer at a time `findAppointmentSlots` returned, and says which staff, rooms and equipment it is with. |
| `changeAppointment` | Cancels or moves a customer's appointment. The customer proves it is theirs with the manage code, or the email or phone number on the appointment. |

### Commerce tools

**Available when:** `CrestApps.OrchardCore.AgenticCommerce` with `CrestApps.OrchardCore.AI`, `CrestApps.OrchardCore.Products.Listing` and `CrestApps.OrchardCore.Products.Feeds` on

These tools are meant for a customer-facing shopping assistant and also work for anonymous visitors. They read the same products as the store's product feeds, and only products agents may see (**Show to AI agents**). The assistant never pays: `startStoreCheckout` returns a link where the buyer enters their address and pays in the store's own checkout. A signed-in visitor's checkout is theirs. The cart tools only use the caller's own cart. The tools are in the **Commerce** category and selectable on AI profiles, the MCP Server and WebMCP. They are marked safe for anyone, so WebMCP can offer them to visitors who aren't signed in (see [storefront tools](mcp/webmcp.md#storefront-tools)). See [Agentic Commerce](../modules/agentic-commerce.md#ai-tools).

| Function | Description |
| --- | --- |
| `searchStoreProducts` | Searches the store's products (`query`, `limit` 1 to 20, 10 by default). Returns each product with its variants, the item id to buy each with, its price and whether it is in stock. |
| `getStoreProduct` | Returns one of the store's products with its description, photos and variants, each with its item id, price and availability. Takes a product id or a variant's item id. |
| `startStoreCheckout` | Starts a checkout for the buyer's chosen items (item ids and quantities) and optional e-mail address, and returns its total, its items, any problems and the `checkout_url` where the buyer pays. |
| `addToStoreCart` | Adds an item (item id and quantity) to the caller's own cart and returns the cart. |
| `getStoreCart` | Returns the caller's own cart: its items, prices, subtotal and the cart page link. |

## Tool permissions

Tool permissions decide who may **attach** a tool, and, for chat interactions, who may **use** it. They do not limit who may use the tools of an AI profile: for a profile, the profile itself is the authorization boundary.

| Permission | Description |
| --- | --- |
| `QueryAnyAIProfile` (or `QueryAIProfile_{profileName}`) | Allows the identity to query the AI profile, and with it to use every tool the profile is configured with. |
| `AccessAnyAITool` | Allows the identity to use every registered AI function. This permission is security critical and is granted only to the `Administrator` role by default. |
| `AccessAITool_{toolName}` | Allows the identity to use one specific AI function. |

The tool permissions are checked in two places:

- **When tools are selected.** The tools editors of AI profiles, AI profile templates, chat interactions and the MCP server settings list only the tools the editing user holds `AccessAnyAITool` or `AccessAITool_{toolName}` for.
- **When a chat interaction sends a message.** The tools selected on the interaction are checked again against the person chatting. A tool they are not authorized for is excluded from the request instead of failing it. The model then answers without that capability, which usually looks like an incomplete or degraded answer. Each excluded tool is reported in the logs with a `Warning` entry that lists the tool names and the permissions to grant, so check the application log first when an interaction behaves correctly for an administrator but not for another user.

AI profile chats run the profile exactly as it is configured. This covers the admin chat, the frontend chat widget, and API or OpenID clients that query a profile. Anyone allowed to query the profile can use all of its tools, without holding any tool permission. That includes anonymous visitors when the `Anonymous` role has `QueryAIProfile_{profileName}`.

:::warning[Choose a profile's tools for its least-privileged user]
Do not attach tools that change data or site configuration, such as tools that create or edit content, enable features, manage tenants, run recipes, change settings, or send email or SMS, to a profile that less-privileged or anonymous users can query. Remember that text the model reads, such as uploaded documents, crawled pages and content items, can steer it toward calling those tools.
:::

:::tip
Custom roles and OpenID applications that query a profile need only the profile query permission to use its tools. To use the tools of a chat interaction, a user needs `AccessAnyAITool`, or the matching `AccessAITool_{toolName}` permission for every tool the interaction uses.
:::

### How the caller is resolved

The identity used for the chat interaction check comes from the framework's `IUserAccessor` rather than from `IHttpContextAccessor`. This matters for SignalR: `HttpContext` is frequently unavailable inside hub invocations on long-lived transports, on backplane-delivered messages, and behind hosted SignalR services, so resolving the caller from the HTTP request could not be relied upon there. The AI chat and chat interaction hubs publish the connection's principal for the duration of every invocation, which means tool permissions are evaluated the same way over SignalR as they are over a regular HTTP request.

Two cases are treated differently:

- **No caller at all**, such as a background task, a workflow, or a recipe. Permission checks are skipped and every configured tool stays available, because trusted server-side code is not a security boundary.
- **An unauthenticated caller of a chat interaction.** The permission check still runs, so whatever you grant the `Anonymous` role continues to apply exactly as it does elsewhere in Orchard Core. An anonymous visitor using a public chat widget is querying an AI profile, so the profile's query permission alone decides what they can use.

## Invocation Context (AIInvocationScope)

`AIInvocationScope` is the shared per-request context for references, tool state, and other invocation-scoped data. For the framework-level explanation, see the shared Core documentation:

- [Tools](https://core.crestapps.com/docs/core/tools)
