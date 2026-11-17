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

Orchard Core part: 189 planned new tools, 132 built, 57 deferred. Left out on purpose: the four template tools (`listTemplates`, `getTemplate`, `saveTemplate`, `deleteTemplate`), because pull request 1321 adds shape template tools; and the six "existing, improve" rows (`listWorkflowTypes`, `getWorkflowType`, `listWorkflowActivities` and three AI tools), because phase 1 does not change existing tools.

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

## Notes for the maintainer

- `EgressNetwork.IsInternal` does not classify NAT64 (`64:ff9b::/96`) or `198.18.0.0/15`, which the Tenant Hierarchy classifier does. `importMediaFromUrl` relies on it.
- All tool calls of one chat message share one service scope, so Orchard Core's immutable document reads stay stale until the scope commits. The URL rewriting and translation tools handle it (commit 8bf806dfe); any later tool that writes through a document manager and reads it back in the same message needs the same care.
- `AI.Agent` now references the Lucene and Elasticsearch core packages, which pulls Lucene.Net and the Elasticsearch client in for every site that installs it.
