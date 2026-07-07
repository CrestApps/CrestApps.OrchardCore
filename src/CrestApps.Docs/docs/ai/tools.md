---
title: AI Tools
description: Shared tool registration and orchestration concepts are documented in CrestApps.Core.
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

### System tools

**Available when:** `CrestApps.OrchardCore.AI.Agent`

| Function | Description |
| --- | --- |
| `listTimeZones` | Retrieves a list of the available time zones in the system. |
| `listAIProfiles` | Lists AI profiles with optional filters for type, analytics, data extraction, and post-session processing. |
| `viewAIProfile` | Retrieves detailed configuration for a specific AI profile by ID or name. |

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

## Invocation Context (AIInvocationScope)

`AIInvocationScope` is the shared per-request context for references, tool state, and other invocation-scoped data. For the framework-level explanation, see the shared Core documentation:

- [Tools](https://core.crestapps.com/docs/core/tools)
