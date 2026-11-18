# Build status: AI tools (phase 1, tools only)

Part of the [AI tool coverage plan](README.md). This file records what was actually built, batch by batch, against the catalog. Where it and a catalog file disagree about a built tool, this file describes the code.

Phase 1 builds tools only; no agent templates are added or changed. Existing tools are not renamed, replaced or changed. Nothing in this file was run against a live site: every tool is covered by unit tests with service doubles or in-memory stores.

## How tools find the caller

Every new tool checks its Orchard Core permission for the signed-in caller through `AgentToolAuthorization` in `CrestApps.OrchardCore.AI.Agent`. It reads the caller from `IUserAccessor`, which the chat hubs set for the whole invocation on every transport, and falls back to `HttpContext.User`. A call with no signed-in caller is refused. `AIToolCallerAuthorization` in `CrestApps.OrchardCore.AI.Core` is the same file as in pull request 1319 and still reads `HttpContext` only; it is kept unchanged so the two branches merge cleanly, and should move to `IUserAccessor` too.

Tools that run Orchard Core's settings display drivers (`getSiteSettings`, `updateSiteSettings`, `listSiteSettingsGroups`) and tools that build links need `HttpContext` itself. CrestApps.Core sets it from the connection's request during every hub invocation from commit 8b74c121 (#324) on; the package in use today (`2.0.0-preview.375`) does not, so on a long-polling connection those tools see no request. WebSockets, the default, is not affected.

## Batches

| Batch | Area | Catalog | Planned | Built | Deferred | Commits |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Contents, content types, localization, flows, scheduling | [10](10-orchard-content.md) | 18 | 16 | 2 | ee2fa0950..702dab0d9 |
| 2 | Autoroute and home page, menus, taxonomies, robots.txt, sitemaps | [10](10-orchard-content.md) | 17 | 13 | 4 | 62418988b..fc6bf391c |
| 3 | Media, placements, shortcodes | [11](11-orchard-media-design-search.md) | 20 | 10 | 10 | 94904d2d0..bd3bbec2a |
| 4 | Themes, indexing, search, queries, GraphQL | [11](11-orchard-media-design-search.md) | 16 | 13 | 3 | 05c9f41a4..c6ca1e83f |
| 5 | Users and roles | [12](12-orchard-users-security-messaging.md) | 19 | 16 | 3 | 70f337cde..d9a1132d0 |
| 6 | OpenID, rate limits, secrets, audit trail, notifications, test email and SMS | [12](12-orchard-users-security-messaging.md) | 27 | 23 | 4 | 4c27b20f9..96781153f |
| 7 | Settings, admin, dashboard, admin menu, tenant feature profiles, deployment | [13](13-orchard-site-administration.md) | 18 | 12 | 6 | 3ed72d293..3f12ee5cd |
| 8 | Remote deployment, translations, background tasks, URL rewriting, health | [13](13-orchard-site-administration.md) | 15 | 8 | 7 | f651064cb..8bf806dfe |
| 9 | Workflows | [14](14-orchard-automation.md) | 18 | 15 | 3 | ecc5a9144..0f31da528 |
| 10 | Data pipelines | [14](14-orchard-automation.md) | 21 | 6 | 15 | 89f4affd8..2a331bf76 |
| 11 | AI profiles and agents, profile templates, personas, tool instances | [20](20-crestapps-ai-core.md) | 18 | 18 | 0 | 40fdee087..a97e4d4f1 |
| 12 | AI provider connections, deployments and pricing, voice library, AI quality | [20](20-crestapps-ai-core.md) | 18 | 18 | 0 | e2d901cca..41cba0083 |
| 13 | AI API keys, conversations, usage, chat interactions, MCP connections, MCP server, A2A connections, AI memory | [20](20-crestapps-ai-core.md) | 21 | 21 | 0 | aa0aade44..caedc8999 |

Orchard Core part (batches 1 to 10): 189 planned new tools, 132 built, 57 deferred. Left out on purpose: the four template tools (`listTemplates`, `getTemplate`, `saveTemplate`, `deleteTemplate`), because pull request 1321 adds shape template tools; and the six "existing, improve" rows (`listWorkflowTypes`, `getWorkflowType`, `listWorkflowActivities` and three AI tools), because phase 1 does not change existing tools.

## Why tools were deferred

Almost every deferral has one cause: the service the admin page uses exists only inside an Orchard Core module assembly, and `CrestApps.OrchardCore.AI.Agent` references Orchard Core's abstractions and core packages only. Each such tool can be built once the contract moves to an Orchard Core abstractions package, or once a bridge module references the module package (decision D5 in the README).

| Tool | Batch | Reason |
| --- | --- | --- |
| `getContentDefinitionOptions` | 1 | The editor and display-mode lists exist only in Orchard Core Razor views |
| `moveListItem` | 1 | `IContainerService` and `ListPartSettings` are in the `OrchardCore.Lists` module |
| `listLayers`, `saveLayer`, `deleteLayer`, `setLayerWidgetPlacement` | 2 | `ILayerService`, `LayersDocument`, `LayerMetadata` are in the `OrchardCore.Layers` module |
| `createMediaFolder`, `deleteMediaFolder` | 3 | Need `MediaDirectoryTreeCache.Invalidate()` (Media module), or the library's folder tree goes stale |
| `listMediaProfiles`, `saveMediaProfile`, `deleteMediaProfile` | 3 | `MediaProfilesManager` (Media module) |
| `listPlacements`, `savePlacement`, `deletePlacement` | 3 | `PlacementsManager` (Placements module) |
| `saveShortcodeTemplate`, `deleteShortcodeTemplate` | 3 | `ShortcodeTemplatesManager` (Shortcodes module) |
| `listThemes`, `setTheme` | 4 | `ISiteThemeService` (Themes module) |
| `runGraphQLQuery` | 4 | Execution is private to `GraphQLMiddleware` |
| `sendUserVerificationEmail`, `sendUserPasswordResetEmail` | 5 | `UserEmailService` (Users module) |
| `removeUserPasskeys` | 5 | The Orchard Core version in use has no passkeys |
| `getOpenIdSettings`, `updateOpenIdSettings`, `getCorsSettings`, `updateCorsSettings` | 6, 7 | OpenID server and validation settings and CORS have their own documents and pages, outside the generic settings tools. The OpenID client settings are reachable through `getSiteSettings` and `updateSiteSettings` |
| `arrangeAdminDashboardWidgets` | 7 | The logic is in `DashboardController.Update`; `DashboardPart` is a module type |
| `deleteAdminMenu` | 7 | `IAdminMenuService` (AdminMenu module) |
| `updateTenant` | 7 | `ITenantValidator` (Tenants module) |
| `getTenantSetupLink` | 7 | The token logic is in the Tenants module and its controller |
| `deleteTenantFeatureProfile` | 7 | `FeatureProfilesManager` (Tenants module) |
| `exportDeploymentPlan` | 7 | A plan's recipe can carry the site salt and plain secrets, and cannot be cleaned generically |
| `listRemoteInstancesAndClients`, `saveRemoteInstance`, `deleteRemoteInstance`, `saveRemoteClient`, `deleteRemoteClient`, `exportDeploymentPlanToRemote` | 8 | The Remote Deployment module has no abstractions package and no recipe step; an export can carry secrets |
| `saveBackgroundTaskSettings` | 8 | `BackgroundTaskManager` (BackgroundTasks module) |
| `updateWorkflowDraft`, `publishWorkflowType`, `discardWorkflowDraft` | 9 | `IWorkflowTypeDraftManager` (Workflows module) |
| `listDataPipelines`, `getDataPipeline`, `saveDataPipeline`, `updateDataPipelineDraft`, `previewDataPipelineStep`, `publishDataPipeline`, `discardDataPipelineDraft`, `enableDataPipeline`, `disableDataPipeline`, `resetDataPipelineState`, `deleteDataPipeline`, `runDataPipeline`, `listDataPipelineRuns`, `getDataPipelineRun`, `cancelDataPipelineRun` | 10 | The pipeline, version and run documents and every manager are in the DataPipelines module |

## Built, but narrower than the catalog

| Tool | Batch | What it does not do |
| --- | --- | --- |
| `listContentItems` | 1 | No parent-list, taxonomy or scheduled filter |
| `listContentItemVersions`, `restoreContentItemVersion` | 1 | No audit trail events; version id only |
| `saveContentTypePart`, `saveContentPartField` | 1 | No position (use `reorderContentTypeElements`); settings merged without the drivers' validation |
| Sitemap index tools | 2 | Indexes can be listed, renamed, enabled, disabled and deleted, not created; their sitemaps are not listed or changed |
| `deleteTaxonomyTerm` | 2 | Does not count the content items that reference the term |
| `saveMenuItem` | 2 | Link, content and HTML menu items only |
| `listShortcodes` | 3 | The catalog only, not the Liquid content |
| `importMediaFromUrl`, `saveMediaTextFile`, `moveMediaFile`, `copyMediaFile` | 3 | The target folder must exist; `saveMediaTextFile` writes text extensions only |
| `saveQuery` | 4 | Lucene and Elasticsearch queries only; SQL is refused (its validator is in the Queries module) |
| `runSqlQuery` | 4 | Cannot relay SQL parse errors; results capped at 50 rows and about 40,000 characters; values under credential-like column names are blanked by name only |
| `saveIndexProfile`, `getIndexProfile` | 4 | Update changes name and content settings only; provider settings for Lucene and Elasticsearch only |
| `saveUser` | 5 | Updates only (creating a user needs the set-password email); no time zone or culture |
| `saveSecret` | 6 | Text and RSA secrets are not created; the tool returns the Secrets page link |
| `saveOpenIdApplication` | 6 | Registered only with OpenID Server on; cannot rename a client id; no confidential clients |
| `enableRateLimitPolicy` | 6 | No guard against a global policy strict enough to lock everyone out |
| `updateSiteSettings` | 7 | `reloadRequested` is unknown for the sign-in provider and Media API pages; custom settings groups are refused |
| `listAdminDashboardWidgets` | 7 | Covered by refusal tests only |
| `saveUrlRewriteRule` | 8 | Cannot set a rule's position; new rules go last |
| `saveWorkflowType` | 9 | Creates only; saves the published version directly, so it asks for approval; HTTP Request URLs are generated in the designer |
| `listWorkflowTypeVersions`, `listWorkflowInstances` | 9 | No compare or draft state; no status or date filters |
| `getDataSetSchema`, `listDataPipelineStepTypes` | 10 | No parameters; no settings schema per step type |

## Batch 11: the AI configuration guard inside the tools

The configuration guard of F4 does not exist yet, so the tools of batch 11 apply its rules themselves through one helper, `Profiles/AIConfigurationGuard.cs` in `CrestApps.OrchardCore.AI.Agent`. The recipe path (`importOrchardCoreRecipe`) is not covered: an assistant with that tool can still change itself. The guard still belongs in an `ICatalogEntryHandler<T>` in AI.Core.

| Rule | State | What is missing |
| --- | --- | --- |
| A, no self-edit from inside an AI turn | Implemented | MCP and A2A connections are protected from batch 13 (`IsProtectedMcpConnection`, `IsProtectedA2AConnection`). The running profile is read from `ToolExecutionContext.Resource`, which is set once per turn, so inside an agent it names the top-level profile; the guard covers that by protecting every agent the top-level profile and the session profile can reach, plus every always-available agent. `LockSystemMessage` and `IsRemovable` cannot be turned off: no tool accepts either field, every write to a protected profile (restore included) is refused, and `saveAIProfile` refuses new instructions on a locked profile. |
| B, no powerful tools where visitors can reach | Implemented for the Anonymous role, enabled unexpired browser API keys and always-available agents; partly for published content items with an `AIProfilePart` | The Anonymous-role check asks `IAuthorizationService` with an anonymous principal, so the real role and `QueryAIProfile_{name}` handlers decide; the tests answer it with a double, and it was not run on a site. The published-widget lookup (`PublishedChatWidgetFinder`, a YesSql query) is exercised through a double only. Phone numbers, SMS and other channel bindings, and the Authenticated role, are not checked. The reverse direction is guarded for browser API keys from batch 13 (`saveAIApiKey`, through `AIConfigurationGuard.GetUnsafeCapabilitiesAsync`); a role permission edit or a published widget made through the content tools is still not guarded. Tool instances are never allowed on a reachable profile. |
| C, `AccessAITool_{name}` for every added tool and tool instance | Implemented | Also applied to `restoreAIProfileVersion` and `createAIProfileFromTemplate`, which the admin pages do not do. |
| Secrets | Implemented | Tool instance settings are read and written through an explicit list per source; the Workflow source's settings are admin-page only. Addresses are checked for scheme and internal hosts without resolving the name. |

## Built, but narrower than the catalog (batch 11)

| Tool | What it does not do |
| --- | --- |
| `setAIProfileCapabilities` | No MCP or A2A connections (their metadata types are in packages this module does not reference); they are set on the profile's page |
| `saveAIProfile` | Updates by ID only; no feature settings (analytics, data extraction, post-session, guardrails, speaking styles) |
| `saveAIProfileTemplate` | No tool, agent or connection names on a Profile template |
| `listAIProfileTemplates` | Templates the template manager knows; shipped speaking styles and guardrail topics that exist only as template-service files are not listed |
| `listAITools` | No risk tier (F3 does not exist); no MCP or A2A connections |
| `listAIToolInstances`, `saveAIToolInstance` | Settings through an explicit list per source; Workflow and unknown sources only on the admin page |

`createAIProfileFromTemplate` needed one extraction: `AIProfileTemplateProfileFactory` and `AIProfileTemplateApplicator` moved unchanged from the AI module to `AI.Core` (now public), and the scenario feature check moved to `ProfileScenarioFeatureLookup` in `AI.Core`, which `ProfileScenarioCatalog` calls (commit 40fdee087).

## Batch 12: AI models and AI quality

No tool of batch 12 was deferred. The existing `runAITests`, `listAITestCases`, `listAIAlerts` and `summarizeAIQuality` are unchanged, although the catalog asks for `runAITests` to ask for approval; it still does not.

Keys are never accepted or returned, which is stricter than the catalog rows of `saveAIProviderConnection` and `saveAIDeployment` ("write-only key"); their descriptions say so. A tool refuses any argument whose name looks like a secret and links to the admin page. Rule A covers models through `AIConfigurationGuard.GetProtectedModelsAsync` and `Profiles/AIProtectedModels.cs`: from inside an AI turn, the chat and utility deployments of every protected profile and of a running chat interaction, the site's default chat and utility deployments they fall back to, the voice deployments of the protected personas, and the connections of all of them, cannot be changed or deleted. Pricing is not guarded on purpose: it only changes cost estimates, so "set the price of gpt-4o" works even when gpt-4o is the assistant's own model.

| Tool | What it does not do |
| --- | --- |
| `saveAIProviderConnection` | Never creates a connection that needs a key (OpenAI, or Azure with key authentication): it refuses and links to the provider's Create page. An update cannot be validated on a copy, because the provider handlers only run inside the manager's update; the tool checks the endpoint and that key authentication has a stored key first, and reports any later validation problem in `problems` |
| `saveAIDeployment` | No model parameter limits (allowed values, minimum, maximum), no Cascaded Realtime chain. A speech deployment is saved without its key (`hasApiKey: false`, with the link); its endpoint is checked as an absolute address only, so the Azure Speech editor's "endpoint required" check is not repeated |
| `setAIDeploymentPricing` | Free prices (amount 0) are dropped by `SetPricing`, as in the editor; the result counts them in `ignoredFreePrices` |
| `deleteAIDeployment` | Also skips the site's default deployment of any slot, which the admin page does not; the person picks another default first |
| Rule A for models | Realtime, speech-to-text and text-to-speech deployments a profile uses through its speech settings or a cascaded chain are not protected, only chat, utility and persona voice deployments |
| `listAIVoices` | Cursor paging from the provider, so no `total` or `page` |
| `cloneAIVoice` | Reads the recording from the media library instead of an upload, and needs the media folder permission too |
| `listAITestRuns` | For a test case (no batch), the pass rate and pending count are over the page shown, as on the Runs page |
| `saveAIAlertRule` | Email addresses are checked as addresses, which the editor does not do |

The quality tools needed one extraction (commit e2d901cca): the range checks of `AITestCaseDisplayDriver`, `AIAlertRuleDisplayDriver` and `AIQACohortDisplayDriver` moved unchanged, with their sentences, into `AITestCaseValidator`, `AIAlertRuleValidator` and `AIQACohortValidator` in `AI.Core/Conversations`, which the drivers now call after applying the form. The alert rule's "checked again at once when what it watches changes" moved there too. One difference: a selection of only blank profile IDs, which the driver used to accept, is now refused; the editor's multi-select has no blank option, so the form cannot post one.

## Batch 13: API keys, conversations, usage, chat interactions, MCP, A2A and memory

No tool of batch 13 was deferred. `AI.Agent` now references the `CrestApps.Core.AI.Mcp` and `CrestApps.Core.AI.A2A` packages, which brings the `ModelContextProtocol` package to every site that installs it, as batch 4 did with Lucene. The A2A permission and `ViewChatAnalytics` live in module assemblies, so `AIModulePermissions.cs` mirrors them by name; `AIConstants.Feature` gained `A2A` and `ChatInteractions`. A test checks each mirrored name and each key read by name against the module that owns it.

Extractions (behavior-preserving; the admin code calls them now):

- `AIApiKeyValidator` (AI.Core `ApiKeys`): the name, origin, profile, scope and role checks and the per-kind clean-up of `AIApiKeyDisplayDriver.UpdateAsync`.
- `AIUserMemoryCleaner` (AI.Core `Services`): the count, list and delete loop of `UserMemoryController.Clear`.
- `McpResourceUris` (AI.Core): `McpResourceHandler.BuildUri` and `ExtractPath`, which the handler now forwards to.

Credentials: no MCP or A2A credential (API key, password, OAuth secret, private key, certificate, header value, environment variable value) is returned or accepted, and stdio arguments are returned as a count only. A new connection is anonymous. `saveMcpConnection` and `saveA2AConnection` may switch a connection to anonymous access (which clears its credentials, as the editor does) and change the non-secret fields of its current authentication; choosing an authentication that needs a credential is refused with the admin link. A connection that stores a credential cannot be moved to another host (endpoint or OAuth token endpoint), since the credential would be sent there. Rule A protects the MCP and A2A connections of the running assistant; deleting a stdio connection is allowed.

Catalog handlers: `saveMcpConnection` updates with no data, as the MCP Hosts editor does, because CrestApps.Core's `SseMcpConnectionSettingsHandler` rebuilds a connection's authentication from any data object (it would clear the non-secret fields and protect the stored secrets a second time). The Orchard `McpConnectionHandler` then logs a null-data error that `HandlerExtensions.InvokeAsync` swallows, as it does for the editor. The A2A, prompt and resource tools pass an empty object, which their handlers ignore. The tests use doubles of the managers, so none of this was run against the real handlers.

The batch 11 narrowings of `setAIProfileCapabilities` and `listAITools` ("their metadata types are in packages this module does not reference") are out of date: `AI.Agent` now references both packages. The tools were not changed, as phase 1 does not change built tools.

| Tool | What it does not do |
| --- | --- |
| `saveAIApiKey` | Never creates a key (a new key's value is shown once): it returns the API keys page link. Roles must exist and cannot be Anonymous, which the editor's list also leaves out |
| `deleteAIConversation`, `deleteAllAIConversations` | Only the caller's own conversations, because `IAIChatSessionManager.DeleteAsync` and `DeleteAllAsync`, which the History page calls, filter on the signed-in user. The catalog's "any conversation" would need `AIConversationPrivacyService.DeleteAsync` (the REST API's path), which also runs the `IAIConversationPrivacyHandler`s; the manager's delete does not. The manager also reads the user from `HttpContext`, so on a long-polling connection with the package in use it deletes nothing (the tool then says so). One session per call |
| `queryAIUsage` | No voice call minutes: the voice grouping (`AIVoiceUsageReport`) and the completion grouping helpers are `internal` to the AI Chat module and drag view models and filter drivers with them. The tool groups the same `IAICompletionUsageService` records itself and prices them with the public `AIUsageCostEstimator`; rows are the report's Metered usage, not its Chat completions table |
| `saveMcpConnection` | Endpoints must be `http` or `https`; the editor takes any absolute address. Custom headers are not set |
| `saveMcpResource` | FTP and SFTP resources are not created, and their connection is not changed |
| `clearAIUserMemory` | Checks **Clear AI memory for others** on its own for another user. `UserMemoryController.Clear` checks only `ClearAIMemory` with the user ID as resource; Orchard's role handler grants that permission without looking at the resource, so a role with only **Clear AI memory** may clear anyone's memory on that page. Not fixed here (it is admin code) |

## Notes for the maintainer

- `EgressNetwork.IsInternal` does not classify NAT64 (`64:ff9b::/96`) or `198.18.0.0/15`, which the Tenant Hierarchy classifier does. `importMediaFromUrl` relies on it.
- All tool calls of one chat message share one service scope, so Orchard Core's immutable document reads stay stale until the scope commits. The URL rewriting and translation tools handle it (commit 8bf806dfe); any later tool that writes through a document manager and reads it back in the same message needs the same care.
- `AI.Agent` now references the Lucene and Elasticsearch core packages, which pulls Lucene.Net and the Elasticsearch client in for every site that installs it.
