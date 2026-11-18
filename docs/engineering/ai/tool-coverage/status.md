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
| 14 | AI data sources, AI documents, AI file sources, AI web crawlers | [21](21-crestapps-ai-knowledge-reports-data.md) | 16 | 14 | 2 | c914acfdc..d289a346e |
| 15 | Data connectors, content transfer | [21](21-crestapps-ai-knowledge-reports-data.md) | 18 | 16 | 2 | 1497ca6f7..c9ae99e26 |
| 16 | Reports, Report Builder | [21](21-crestapps-ai-knowledge-reports-data.md) | 20 | 20 | 0 | 315b52b64..282d0b85c |
| 17 | Report subscriptions, webhooks | [21](21-crestapps-ai-knowledge-reports-data.md) | 12 | 12 | 0 | ce6012f49..e67d74f04 |
| 18 | User display name and avatar, content access control, structured data check, tenant hierarchy (parent and platform) | [22](22-crestapps-platform-and-parties.md) | 24 | 24 | 0 | c0e525064..691457a10 |
| 19 | Time zone maps, address check, phone number check, phone verification queue, territories and geocoding, accounts | [22](22-crestapps-platform-and-parties.md) | 18 | 18 | 0 | a5ab3a301..31309e94e |
| 20 | Accounts B2B (business buying, punchout, price lists), customers (store credit, loyalty points, data requests), teams | [22](22-crestapps-platform-and-parties.md) | 18 | 18 | 0 | e704df3ab..759d49928 |
| 21 | Skills and certificates, staff (records, badges, capabilities) | [22](22-crestapps-platform-and-parties.md) | 22 | 21 | 1 | 5fbe402d4..2d5e10ea6 |

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
| `addAIProfileDocument`, `removeAIProfileDocument` | 14 | The add and remove logic is inline twice, with different messages, in `AIProfileDocumentsDisplayDriver.UpdateAsync` and `AIProfileDocumentsApiEndpoints`, and its pieces (`ProfileDocumentChunkIndexer`, the deferred chunk removal) are internal to the Documents module, while the guard that keeps the running assistant's documents unchanged is internal to AI.Agent. A tool needs a new service contract in AI.Core with its implementation in the Documents module, both callers moved onto it, and a media-file wrapper for `ProcessFileAsync(IFormFile)`; not a small move |
| `exportContentItems` | 15 | Creating an export entry (the count, the queue decision, the filters and the `IDisplayDriver<ExportRequest>` options other modules contribute, which bind from the form) exists only in `AdminController.DownloadExport`, and the Content Transfer controllers are off limits while another pull request changes them. The download itself is safe: `DownloadExportFile` checks the permission and the owner, and `getContentTransferEntry` links to it |
| `setStaffPay` | 21 | Pull request 1332 (Payroll), merged upstream but not on this branch, replaces the pay model (`StaffPayRates` gains a pay basis, a currency, dated salaries and a commission, and changes are written to the history) and the Pay card, which it calls "the only place pay is edited" and puts behind a password confirmation (`StaffPayPrivacy`). The pay rules live only in `StaffPayDisplayDriver` (`StaffFeatureDrivers.cs`), which 1332 rewrote, so extracting them here would conflict; a tool written against this branch's model would erase the new fields after the merge, and would change pay without the confirmation. Build it after the merge, on the new model, if a chat may change pay at all |
| `importContentItems` | 15 | Creating an import entry exists only inside the chunked upload of `AdminController.ImportPOST` (store the file, build the entry, copy the `ImportContent` options bound by `ImportContentDisplayDriver`, trigger processing), which is off limits for the same reason. A tool needs that moved into a service that takes a stream, plus a media library file source |

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
| `saveDataConnection` | 15 | Never takes a credential or a header (saves the rest, returns `missingCredentials` and the page link); creates HTTP API, Elasticsearch and media files connections only; a SQL Server connection is never created and only its name and description change (SEC-A8); a connector from another module gets names only. Will not move a stored credential or header to another host, sign-in type, grant, token endpoint or issuer |
| `saveConnectedDataSet` | 15 | No connector settings (HTTP method, body, records path, paging, Elasticsearch or SQL query, file delimiter); fields are replaced as a whole and lose their description, as in the editor; the connection and technical name cannot change |
| `refreshConnectedDataSet` | 15 | Records the request and lets the every-minute refresh task run it (the admin page also starts it after its request, which a chat has no end of); refuses a disabled data set, which the task skips |
| `listContentTransferEntries` | 15 | No content type filter: the list's filter parser has only `status` and `sort` terms |
| `setContentImportState` | 15 | Does not resume an import still marked processing (running or stalled): telling them apart takes the import's lock, done by the Import list; a resumed import waits for the Imported Files Processor instead of being started at once |
| `deleteContentTransferEntry` | 15 | Stricter than the list's single Delete action: an export must be the caller's own. Queues the deletion with `DeferredWork`; the queued path is not covered by a test |
| `getReport`, `runReport`, `exportReport` | 16 | A built-in report takes only a period (`from`, `to`, `allTime`); its other filters (AI profile, usage grouping, contact center, omnichannel, hours by category, QA cohort) bind only from the report page's form. A filter-description contract would touch the filter drivers of five modules, so it was not added; `getReport` lists the names a report declares through `IReportFilterMetadata` and says they are set on the page |
| `runReport` | 16 | For a designed report, only what its visuals show, as on its page and in its export, not the raw rows of its query, which it reads with its owner's access (the catalog's `ReportWorkflowService.ToOutput` would return every column); 200 rows per table by default, at most 1,000, and about 60,000 characters |
| `exportReport` | 16 | Returns the report page's own export address (which checks the permissions again and builds the file when opened), not a file. A designed report whose first exposed date filter is a relative date ("last N days") cannot take a period in the address |
| `createReportShareLink` | 16 | Always emails the link and never returns its address, which holds the link's secret token (the catalog row says "optionally an email" and "Returns the link"); the registration's description says so. Recipients the email did not reach are listed, with the advice to revoke and create the link again |
| `saveReportDesign` | 16 | Publishes changes to a published report only (a report that was never published is published in the builder); refused while the report has unpublished changes in the builder; the group must exist (no group is created); named queries and containers are kept as stored |
| `shareReportDesign`, `restoreReportDesignVersion` | 16 | Refused while the report has unpublished changes in the builder. Restoring copies the version into the draft and publishes it at once, with the report's current sharing, not the version's |
| Report Builder tools | 16 | A report, view or group created, changed or deleted earlier in the same chat message is not read or changed again until the next reply, because the report, view and group catalogs read the cached document until the scope commits (`ReportToolScopeChanges`); lists say so. Report groups keep their own scoped record, so group tools see each other's changes |
| `sendReportSubscription` | 17 | Stricter than the catalog row (only **Manage report subscriptions**): it first runs the subscription editor's checks on what is stored, for the caller, as **Save and send** does, so the caller must be able to view the report, and export it when a file is attached; a subscription the caller could not save is not sent |
| `saveReportSubscription` | 17 | Built-in reports only, as the editor; the caller becomes the person who last saved it (`ModifiedBy`), whose export permission an attachment needs at every run, so an update by someone who may not export the report is refused while a file is attached |
| `saveWebhook` | 17 | Kind External with approval, not Security (batch brief). Events no enabled module declares are dropped, as the editor drops them, and listed in `ignoredEvents` |
| Webhook tools | 17 | A signing secret is never returned or accepted (`secret`, `signingSecret`, `protectedSecret` and `webhookSecret` arguments are refused); a new or rotated secret is copied from the webhook's admin page, whose address the results give. `listWebhookEvents` returns names and display names only: the event catalog has no descriptions. `resendWebhookDelivery` needs the webhook id as well as the delivery id and refuses a delivery of another webhook, as the Deliveries page does, so a direct (formatter) delivery is never resent from a chat. `sendWebhookPing` and `resendWebhookDelivery` post during the call, as the admin page does |
| `setUserAvatar` | 18 | One image per user (the avatar field allows several); no media text or anchor. Stricter than the avatar card: the file must exist in the media library, and `..` segments are refused |
| `setContentItemAccessRoles` | 18 | Stricter than the Role Picker editor: an unknown or excluded role is refused instead of dropped, a type with several restricting parts needs `partName`, publishing needs **Publish content**, and an item with a pending draft is never published (only `saveAsDraft`). When content validation fails after the draft version was created, that draft version is still saved by the session, as with `createOrUpdateContentItem` |
| `getContentItemStructuredData` | 18 | Content item id only, no path. Nodes that other shapes add while the page renders (`IPageStructuredData.Add`, such as a list's items) are not in the result |
| `saveChildTenant`, `retryChildTenantSetup` | 18 | The setup is queued with `DeferredWork` on the chat call's shell scope instead of `ChildTenantManager.ScheduleSetup` (`HttpBackgroundJob`), and nothing is created when there is no shell scope. A test runs the queued setup inside a real `ShellScope`; it was not run in a live chat hub |
| `listChildTenants` | 18 | The open link is a path (`delegated-access/open/{entryId}`) under the request's path base; favorites, recents and "sign out everywhere" have no tool |
| `setChildTenantFeatures` | 18 | Stricter than the Features screen post: only ids the screen lists (`GetChildFeaturesAsync`) are sent, so a feature enabled only as a dependency cannot be named directly |
| `grantChildTenantAccess` | 18 | Stricter than `AccessGrantManager.AddAsync`: child roles must be among those the Access screen offers (`GetChildRolesAsync`), and the child tenant must not be one the platform changed |
| `saveParentTenant` | 18 | Refuses to make a parent when `TenantHierarchy:PlatformDomain` is not configured and there is no request host (the service would build the host on `localhost`). Recipe names must be setup recipes of the site, which the editor's list enforces and the service does not |
| `detachChildTenant` | 18 | Orphans only, as the page offers it; the service would detach any child |
| `saveTimeZoneMap` | 19 | Finds a map by its name and cannot rename one, as the Time Zones editor cannot. Stricter than the editor: it runs the time zone map handler's checks (valid IANA id, one map per time zone), which the admin controller skips. A time zone freed by a delete earlier in the same chat message is still seen as taken by the handler, which reads the cached document |
| `validateAddress` | 19 | Lists the countries only when the country is missing or unknown, and a country's regions only when the region is unknown |
| `listPhoneNumberVerifications`, `retryPhoneNumberVerifications` | 19 | Retrying re-queues the records for the Phone Number Revalidation background task (every five minutes) instead of verifying them at the end of the call as the queue page does; at most 100 ids per call. The queued verification itself was not run by a test |
| `listServiceTerritories` | 19 | A shape longer than 20,000 characters is not returned (`shapeTruncated`); 25 postal codes per territory in summaries (all with an id); 50 members offered per kind |
| `saveServiceTerritory` | 19 | Stricter than the editor: the parent must exist, the time zone must be one the time zone pickers offer, a member kind must be one a module lists and each id one its kind lists |
| `geocodeAddress` | 19 | At most five candidates. A provider's error is relayed with key-like values hidden; any other failure gets a fixed sentence |
| `setAccountTeam` | 19 | Stricter than the team editor: every id must be a staff member |
| Account write tools | 19 | As `setContentItemAccessRoles`: a pending draft is changed only with `saveAsDraft`, publishing needs **Publish content**, and when content validation fails after the draft version was created, that version is still saved by the session. `saveAccountLocation` needs an address for a new location; an update without one keeps the stored address |
| `getB2BAccountSummary`, `saveB2BAccountSettings`, `configureB2BPunchout` | 20 | Stricter than the catalog row (only **Manage B2B accounts**): they also need **View content** or **Edit content** on the account, and save it as the batch 19 account tools do (pending draft only with `saveAsDraft`, publishing needs **Publish content**). The summary reads the published account (the draft only for an account never published) and lists at most 20 open term invoices |
| `saveB2BAccountSettings` | 20 | Stricter than the Business buying card: every buyer given an order limit must be a member (the card only offers members), and the price list must exist (the card only offers existing lists). Order limits given replace the stored ones. Never changes punchout |
| `configureB2BPunchout` | 20 | Never issues, returns or accepts a punchout key (the catalog row issues a new key and returns it once): turning punchout on for an account with no key is refused with the account editor's address, where the card issues the key and shows it once; a key rotation is done there too. Turning punchout off clears the buyer, as the card does |
| `savePriceList` | 20 | No currency argument (the catalog row has one): the prices are saved in the store's currency, as the Price lists editor does, and the result says when a list's previous currency changed. No name uniqueness check, which the catalog's implementation notes mention but the code does not have |
| `listPriceLists` | 20 | With an id, at most 500 prices per page (100 by default), with an optional SKU filter and `truncated` |
| `listCustomers` | 20 | No phone search (the catalog row has one): the customer query matches only the start of the name, the company or the email, as the list page does |
| `saveCustomer` | 20 | Stricter than the form: an email given for an account holder is refused (the form ignores it), and an anonymized customer is refused with a sentence (the form redirects) |
| `saveCustomerAddress` | 20 | A new address starts with the customer's name, company and phone, and a customer's first address becomes their default billing and shipping address, as the editor pre-fills them; when an address is changed, a recipient, company or phone not given keeps its stored value. The address phone is never set from the chat |
| `exportCustomerData` | 20 | Returns the address of the customer page's Export action instead of the data (the content transfer precedent): opening it checks **Manage customers** again, builds the file and writes the privacy log entry under the name of whoever opens it, not the person who asked the assistant. Needs a web request to build the address; without one it says to use the page |
| `eraseCustomerData` | 20 | Writes the customer at once (`SaveChangesAsync`) so a concurrent change is reported instead of failing after the answer; the personal-data contributors of other modules were not run by a test (the privacy service is a double) |
| `adjustCustomerStoreCredit`, `adjustCustomerLoyaltyPoints` | 20 | Call `StoreCreditService.ChangeAsync` and `LoyaltyService.ChangeAsync` as the cards do; a balance stops at zero, and the result reports the change made and the amount asked. The currency defaults to the store's, as the card's field does. Pull request 1331 adds `TryChangeAsync` (references, insufficient balance refusal); the tools do not use it |
| `saveTeam` | 20 | Core fields only: the cards other modules add to the team editor (Workforce, Contact Center, and the team address of pull request 1332) are not run and keep their values; the linked role is not checked against the roles, as the editor's field is free text |
| `listSkills`, `listCertificationTypes` | 21 | Need **View skills and certifications**, as the catalog row says; the Skills and Certification Types pages need **Manage skills and certifications** |
| `saveSkillAssignment` | 21 | Not refused when the skill requires a certification the person does not hold (the catalog row says it is): `DefaultSkillAssignmentService` stores the assignment and only stops it counting. The tool reports `countsToday` and the reason, and its registered description says so |
| Skill tools on a person | 21 | A person is `subjectKind` (User or Staff) and `subjectId` (user id, user name or email; staff member id from `getStaffMember`), not a free name. Changing a staff member's skills or certificates needs the caller's staff scope, through the new `ISkillSubjectProvider` that Staff.Core implements; this is stricter than the Certificates page, which checks only **Assign skills** for a certificate held by a staff member. `findSkillHolders` asks for users and staff members separately and does not merge a staff member with their linked user |
| `saveCertificate` | 21 | No status argument: a new certificate is pending until `setCertificateStatus` verifies it. The holder never changes (the certificate editor can change the user of a user's certificate). The certification type must be enabled, as the editor offers only those. The document path must name a file in the media library; addresses and `..` segments are refused |
| `getStaffMember` | 21 | The home base address and the badge are never returned (`hasHomeBase` only); the history shows the 20 latest employment changes (the fields Staff itself records), not other modules' entries. Pay (rates, annual salary, hourly cost) is returned with **Manage staff pay rates**, as this branch's Pay card shows it. After pull request 1332 merges, the profile shows pay only after the viewer confirms their password, which a chat cannot ask for: the owner must decide then whether the tool keeps returning pay |
| `saveStaffMember` | 21 | The cards other modules add to the staff editor (pay, badge, skills, Workforce, Telephony and others) are not run and keep their values. Team memberships given replace the stored list. For a person with a user account, a name or e-mail given is ignored and listed in `ignoredFields`, as the editor ignores them. The date of birth is refused (the editor drops it silently) without **View sensitive staff data** |
| `setStaffBadgePin` | 21 | Clears a PIN or reports whether one is set; it never takes or returns a PIN (the catalog row also sets one). A new PIN is set on the staff editor, whose address the result gives |
| `listStaffCapabilities` | 21 | Needs **View staff**, as the catalog row says; the Capabilities page needs **Manage staff** |
| Capability tools | 21 | Capabilities are one document read from the cache until the message's scope commits, so a capability added, changed or deleted in a chat message is seen (and can be given to staff) from the next reply; the same capability is not changed twice in one message (`StaffCapabilityToolScope`), and lists say so |

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

## Batch 14: AI data sources, documents, file sources and web crawlers

The first CrestApps knowledge-module batch. The four modules already reference `AI.Core`, so their startups use `[RequireFeatures(AIConstants.Feature.Area)]` and no new feature-id constant was needed in `CrestApps.OrchardCore.Abstractions`.

Placement:

- `listAIDataSources`, `syncAIDataSource` and `searchAIDataSource` are in the Data Sources module; `listAIProfileDocuments` in the Documents module (feature `...Documents.Profiles`); the file source and web crawler tools in their own modules.
- `saveAIDataSource` and `deleteAIDataSource` are in `AI.Agent` (`AIDataSourcesToolsStartup`, gated on `AIConstants.Feature.DataSources`): they must refuse to change the running assistant's data source, and `AIConfigurationGuard` is internal to `AI.Agent`; the Data Sources module cannot use a type from that module.
- Shared plumbing is one new file in `AI.Core`, `AIModuleTools` (caller from `IUserAccessor` then `HttpContext`, permission check, JSON, paging, local times, admin links, secret-argument refusal), instead of one base class per module. `AIDataSourceToolDescriptions` in `AI.Core` builds the data source result for both assemblies.

Rule A now covers data sources: `AIProtectedConfiguration.DataSourceIds` holds the `DataSourceMetadata.DataSourceId` of every protected profile and of a running chat interaction, and the data source of every protected data source search tool instance. File source and web crawler tools live in their own modules, so they reach the guard through `IAIRunningAssistantGuard` (contract in AI.Core, implemented in AI.Agent by `AIRunningAssistantGuard` and registered by `AIDataSourcesToolsStartup`): saving one whose current or new data source is protected, or deleting one that feeds a protected data source, is refused. Without AI.Agent nothing is protected. Running a file source or a crawler and synchronizing a data source change no configuration and are not guarded.

Cost and approval: the Core catalog handlers queue a full synchronization of the data source when a data source, a file source or a web crawler is created, updated or deleted, and a Web data source's synchronization re-crawls every enabled crawler. So `saveAIWebCrawler` asks for approval, which the catalog row does not, and every save and delete result says a synchronization was queued, not run. `runAIFileSource` queues the run with `DeferredWork` after the turn (the admin Run button runs it inline); without a shell scope it says it could not queue. `runAIWebCrawler` reads the sitemap during the call, as the Sync button does, and queues the indexing.

Credentials: no Azure AI Search API key, Elasticsearch password, API key or Base64 API key, PostgreSQL connection string, FTP password or SFTP password, private key, passphrase or proxy password is returned (`hasX` only) or accepted; any argument whose name marks a secret, at any depth and in any case, is refused with the admin link (`IngestionSourceSecrets.IsSecret`). Canary tests cover each field. A file source's FTP or SFTP host or port, or SFTP proxy host or port, cannot change while a credential for it is stored. Addresses are checked as the editors check them and no more: crawler base and sitemap URLs must be absolute (and the crawl strategy wants one of them `http` or `https`), FTP and SFTP need a host and a port from 1 to 65535. Nothing blocks internal hosts.

The tests use doubles of the catalog managers, so the Core catalog handlers, the source handlers, the ingestion run service and the crawl planner did not run.

| Tool | What it does not do |
| --- | --- |
| `saveAIDataSource` | Creates Azure AI Search and Elasticsearch data sources only on a data connection (their own endpoint, keys and the Elasticsearch default connection are set on the page); PostgreSQL only on the site's connection string. An update changes the name, and the mapping only while the configuration is not locked; connection settings are never changed. The suggested field mapping (`GetDataSourceFieldsEndpoint`) was not extracted, so the tool does not default it or list the index's fields |
| `searchAIDataSource` | Returns the retrieval text an assistant would receive, through `DataSourceSearchToolFunction`; `DataSourceRetrieval` is internal to CrestApps.Core, so there are no separate scores. Covered by refusal and failure tests only |
| `saveAIFileSource` | Never creates an SFTP file source (its editor needs a password or a private key); FTP and SFTP settings are written by name (`FtpConnectionMetadata`, `SftpConnectionMetadata`) because the module does not reference those packages; no SFTP host key fingerprint, no maximum file sizes. A connector added by another module gets only the common fields |
| `runAIFileSource` | Queues the run instead of running it; the summary is read later from `listAIFileSources`. The queued path is not covered by a test |
| `saveAIWebCrawler` | Asks for approval, unlike the catalog row |
| `deleteAIDataSource`, `deleteAIFileSource`, `deleteAIWebCrawler` | Whether a delete removes the indexed documents is up to the catalog handlers, which were not run |

## Batch 15: data connectors and content transfer

The first batch in modules that do not reference `AI.Core`. `AIFeatureIds.Area` was added to `CrestApps.OrchardCore.Abstractions` for `[RequireFeatures]`; a test checks it equals `AIConstants.Feature.Area`. Each module has its own base class (`DataConnectorAIToolBase`, `ContentTransferAIToolBase`): caller from `IUserAccessor` then `HttpContext`, permission check, JSON, paging, local times, admin links. Both modules now reference the `CrestApps.Core.AI` package.

Extractions (behavior-preserving; the admin code calls them now):

- `DataConnectionEditor` (Data Connectors module): the name, HTTP settings and media folder checks and mapping of `DataConnectionDisplayDriver`, `HttpConnectionDisplayDriver` and `MediaFilesConnectionDisplayDriver`. The required-secret checks stay in the HTTP driver.
- `ConnectedDataSetEditor`: the checks and mapping of `ConnectedDataSetDisplayDriver.UpdateAsync`.
- `DataConnectorCommands`: deleting a connection unless a data set reads it, deleting a data set with its rows and refreshes, and recording a refresh request; both controllers call it. Moving the strings changed their localization context.

Credentials: no API key, password, bearer token, client secret or SQL Server password is returned (`hasX` only), and header values are never returned. Any argument named `apiKey`, `password`, `bearerToken`, `clientSecret`, `token`, `accessToken`, `secret`, `secrets`, `connectionString` or `headers`, at any depth and in any case, is refused; names are matched whole, so `apiKeyName`, `apiKeyPrefix` and `tokenUrl` are accepted. Canary tests cover each secret and the header value. SQL Server settings are read by name (`SqlServerConnectionSettings`), with `usesHostIdentity`, and a test pins the names to the SQL Server module.

Queueing from a chat: the admin pages queue refreshes, resumed imports and deletions with `HttpBackgroundJob.ExecuteAfterEndOfRequestAsync`, which waits for the request's `HttpContext` to be released and gives up after 60 seconds; from a hub connection that request lasts as long as the connection, so the job would be skipped (read from the package's source, not run). The refresh tool records the request for the refresh task, the resume tool leaves the import to the Imported Files Processor, and the delete tool queues `IContentTransferEntryManager.DeleteAsync` with `DeferredWork` before marking the entry (nothing else sweeps entries marked as deleting). The Content Transfer controller keeps its private triggers; it was not touched.

The data connector tests use in-memory stores, the real secret manager, reader and editors, and a test connector; the content transfer tests use a real SQLite database with the module's migration, the real entry manager, filter parser and list query. No HTTP, Elasticsearch or SQL Server system was reached.

## Batch 16: reports and the Report Builder

No tool of batch 16 was deferred. The Reports module did not reference `AI.Core`, so it has its own base class, `ReportsAIToolBase`, and now references the `CrestApps.Core.AI` package. Two startups register the tools: `AIToolsStartup` (feature `CrestApps.OrchardCore.Reports`) for `listReports`, `getReport`, `runReport` and `exportReport`, which reach designed reports only when the Report Builder's services are registered, and `BuilderAIToolsStartup` (feature `CrestApps.OrchardCore.Reports.Builder`) for the other sixteen. `listReportDataSets` and `getReportDataSetSchema` overlap batch 10's `listDataSets` and `getDataSetSchema`, which need **Manage data pipelines**; these need the report designer's permission and add the formula functions.

Extractions (behavior-preserving; the admin code calls them now, commit 315b52b64):

- `ReportDateRangeResolver` (Reports module): the period resolution of `ReportDateRangeFilterDisplayDriver` (`NormalizeAsync`, the default ranges), plus `ResolveDaysAsync` for whole days in the user's time zone. The driver keeps an internal `NormalizeAsync` that forwards to it, for its tests.
- `ReportShareLinkService` (Reports module): the list, create and revoke actions of `ReportDesignerApiController`, with their log lines; `ReportShareLinkRequest` moved with it.
- `DesignedReportPresenter.GetFileName`: the designed export's file name, so the tool names the file the link will download.

Permissions are checked as the pages check them, with the report or view as the resource, so `ReportDesignAuthorizationHandler` grants a report to its owner and the people and roles it is shared with. Reading a report's share links also needs **Share custom reports publicly**, as the designer's link list does. A designed report runs with its owner's access, as on its page; `queryReportData` runs with the caller's access, as the designer's preview does.

Sharing: publishing a report saves the sharing lists of what is published, so `saveReportDesign` starts from the stored report and `restoreReportDesignVersion` replaces the version's lists with the current ones; only `shareReportDesign`, which asks for approval, changes them. It refuses a user or role that does not exist (the service would store any name) and the Anonymous role without **Share custom reports publicly**.

Display values: `runReport` resolves user display names in a report through `ReportDisplayValueResolver`, as the export and the workflow tasks do. It renders the `UserDisplayName` Razor shape, which builds its view context from the request's `HttpContext`; on a long-polling connection with the CrestApps.Core package in use there is none (see "How tools find the caller"), so a report whose values carry user names would fail there. The tests mock the display helper, so this path was not run.

The tests run the tools on the real report services: the catalogs over an in-memory document manager, drafts, versions and snapshots in a SQLite database the module's migrations create, the sales test data source, the report authorization handler and an in-memory share link manager. The coded report filter drivers of other modules, the email of share links and a view's snapshot refresh (only the live-view answer) were not run.

## Batch 17: report subscriptions and webhooks

No tool of batch 17 was deferred. The report subscription tools reuse `ReportsAIToolBase` and are registered by a third startup of the Reports module, `SubscriptionsAIToolsStartup` (feature `CrestApps.OrchardCore.Reports.Subscriptions`), because the subscription services exist only with that feature. The Webhooks module had no tools: it now references the `CrestApps.Core.AI` package and has its own base class, `WebhooksAIToolBase`, which refuses a caller who is not signed in or lacks `ManageCommerceWebhooks` (the code name of **Manage webhooks**, `WebhookPermissions.ManageWebhooks`) before any argument is read, and `AIToolsStartup` (feature `CrestApps.OrchardCore.Webhooks`).

Extractions (behavior-preserving; the admin code calls them now, commit ce6012f49):

- `ReportSubscriptionEditor` (Reports module, `Subscriptions/Services`): the private `ApplyAsync` of `ReportSubscriptionsController` (report the user may view, recipients split on commas, semicolons and new lines with at most 50 valid addresses, hour and day ranges, attachment format and the report's export permission). It returns the errors with the same keys, which the controller puts in its model state.
- `WebhookSubscriptionCommands` (Webhooks module, `Services`): `Validate` and `Apply` of `WebhooksAdminController`, adding a webhook with a new secret, rotating the secret, the ping delivery and deleting a webhook then up to 1,000 of its deliveries. The controller's `IsAllowedUrl` forwards to it.

Both moves changed the localization context of the moved strings (now the new types). The subscriptions controller no longer takes an `IStringLocalizer`; `ReportExportPermissionTests` builds it with the editor.

Approval: saving, sending and deleting a subscription, and saving, rotating, deleting, pinging and resending a webhook. Saving or enabling either one sends site data to outside addresses, so it is External with approval; the three lists are not.

The tests run the tools on the real `ReportSubscriptionService`, `ReportSubscriptionEditor`, report and export managers with a test report and a mocked email service, and on the real `WebhookDispatcher` (with a throw-away data protection key and a recording sender) and `WebhookSubscriptionCommands`, over in-memory doubles of the YesSql stores. Secret canaries check that neither a webhook's secret nor its protected form appears in any list, save, rotate or ping result. The YesSql stores, the real email provider, the HTTP sender and the egress guard at send time were not run. `sendReportSubscription` renders user display names through `ReportDisplayValueResolver`, which needs the request's `HttpContext` (see batch 16), so on a long-polling connection with the package in use a report whose values carry user names would fail there.

## Batch 18: user names and avatars, content access, structured data, tenant hierarchy

No tool of batch 18 was deferred. None of the five modules referenced `AI.Core`; Users, Content Access Control and Tenant Hierarchy now reference the `CrestApps.Core.AI` package (Seo already did) and register their tools in startups gated on `AIFeatureIds.Area`: `DisplayNameAIToolsStartup` and `AvatarAIToolsStartup` (Users), `AIToolsStartup` (Content Access Control, its only feature), a new `AIToolsStartup` in Seo beside the unchanged `AIStartup`, and `ParentAIToolsStartup` and `PlatformAIToolsStartup` (Tenant Hierarchy). Each module has its own internal base class (`UsersAIToolBase`, `ContentAccessControlAIToolBase`, `TenantHierarchyAIToolBase`); the single Seo tool reads the caller itself. Every base refuses an argument its schema does not declare, so a host, URL prefix, database, connection string or feature profile can never be passed in. Content Access Control gained `InternalsVisibleTo` for the test project; Seo now references `OrchardCore.Contents.Core` for `CommonPermissions`.

Extractions (behavior-preserving; the admin code calls them now):

- `UserFullNameRules` (Users module): the required-name checks of `UserFullNamePartDisplayDriver`. It returns the missing fields; the driver keeps its own localized messages.
- `UserAvatarRules` (Users module): the image extensions and the extension check of `UserAvatarPartDisplayDriver`.
- `RolePickerRoleSelection` (Roles.Core, public): the excluded-role, Required and Allow select multiple rules of `RolePickerPartDisplayDriver`, returning a `RolePickerSelectionProblem`; the driver keeps its messages.
- `ContentAccessRestrictions` (Content Access Control module): the restricting-part lookup of `RoleBasedContentItemAuthorizationHandler`, which now calls it.
- `ChildTenantListFilter` (Tenant Hierarchy module): the private `Filter` of `ChildTenantsController`.

Tenant hierarchy and the review (SEC-C2, SEC-C18): the child tenant tools create tenants only through `ChildTenantManager.ValidateCreateAsync` and `CreateAsync`, so the address is a slug checked for format, the host's reserved slugs, the parent's address pattern and addresses in use, and the recipe must be one the parent's policy allows. A child tenant is bounded by its parent's `BlockedFeatures` and `DeniedLocalPermissions`, applied by the hierarchy's feature validation provider; there is no Orchard feature profile for child tenants, and the tools do not invent one. The platform tools merge a policy change into the stored policy through the editor's own `ParentPolicyEditViewModel`, so naming one field never clears the blocked features or the recipes (tested). No shell settings are serialized: results are built field by field and a canary test checks that a tenant's connection string and secret do not appear. Setup from a chat: each chat hub call runs in its own shell scope (`ShellScope.UsingChildScopeAsync` in the Orchard chat hubs), so the setup is queued with `DeferredWork` and runs when the reply ends; `ChildTenantManager.ScheduleSetup`, whose `HttpBackgroundJob` would wait for the connection's request to end, is not used. The tools check that a shell scope exists before creating or retrying anything, because a tenant with no setup to follow stays in Setting up and can only be removed.

Tests: refusal tests (not signed in, without the permission, wrong tenant, undeclared argument) for all 24 tools; the parent tools run on the real `ChildTenantManager` and `AccessGrantManager` over a SQLite registry created by the module's migration, with a broker double; the queued setup runs inside a real `ShellScope`; the platform tools run on the real `TenantHierarchyPlatformService` over a shell host double; the user, content access and structured data tools run with doubles of the user manager, content manager, media store and structured data manager. The real broker (other tenants' scopes, setup, provisioning) and a live chat hub were not run.

Open points for the owner:

- Attribution: `ChildTenantManager.RecordAsync` and `AccessGrantManager.AddAsync` (`CreatedByName`) read the actor from `HttpContext`. Only creating a child tenant passes the caller explicitly (`ActorUserId`, `ActorUserName`). On a long-polling connection with the CrestApps.Core package in use (see "How tools find the caller"), the audit events and new grants of the other tools would carry no user.
- The queued setup runs while the chat call's shell scope is disposed, which the hub invocation awaits, so the call returns only when the child tenant's setup has finished; with SignalR's one invocation per connection, the person's next message may wait for it. Whether the streamed reply reaches the person before the setup starts was not checked, as for the deferred `importOrchardCoreRecipe` path.

## Batch 19: time zones, addresses, phone numbers, geo and accounts

No tool of batch 19 was deferred. None of the six modules referenced `AI.Core`; each now references the `CrestApps.Core.AI` package and registers its tools in an `AIToolsStartup` gated on `AIFeatureIds.Area`. Accounts has four startups, one per feature (`AIToolsStartup`, `MembersAIToolsStartup`, `LocationsAIToolsStartup`, `TeamAIToolsStartup`), so its public API baseline changed (commit 31309e94e). TimeZones, Phone Number Verifications, Geo and Accounts have their own internal base class; the single Addresses and PhoneNumbers tools read the caller themselves. Every base refuses an argument its schema does not declare, so no map or verification provider key can be passed in. TimeZones and PhoneNumbers gained `InternalsVisibleTo` for the test project.

Shared value types: an address argument is read by `AddressArguments` (Addresses.Abstractions, public), which `validateAddress`, `geocodeAddress` and `saveAccountLocation` share; it reads the postal fields of `Address` only, never coordinates. Points are `GeoPoint`; phone numbers go through `IPhoneNumberService.TryParse` and `PhoneNumberNormalization.ToE164OrTrimmed`.

Extractions (behavior-preserving; the admin code calls them now):

- `PhoneNumberVerificationRecordsService` (Phone Number Verifications module, public because the controller takes it): the query, status filter, sort, counts, failed-id lookup, re-queueing and deferred processing of `RecordsController`.
- `TerritoryCommands` (Geo module): the territory editor's member merge (now given the kinds to replace; the controller passes every listed kind, the tool only the kinds given), the re-parenting of children on delete, and the geocoding queue counts of `GeoSettingsDisplayDriver`.
- `AccountMemberRules` (Accounts module): the user resolution, de-duplication, one-account rule and contact link of `AccountMembersPartDisplayDriver`, returning the problems; the driver keeps its messages.
- `AccountLocationRules` (Accounts module): the address editor options, the address addressed to the account and the location id and default name of `AccountLocationsPartDisplayDriver`.

Scope: the time zone map catalog reads a cached document until the scope commits, so `TimeZoneMapScopeChanges` keeps the maps saved or deleted in the chat message, as batch 8 did for URL rewrites. Territories are YesSql documents and need no such record.

Approval: deleting a time zone map, a territory or an account location, retrying verifications, geocoding (a billable lookup) and setting account members. Phone verification lookups do not message customers; retrying asks for approval because each lookup is billable.

Checked claim: removing an account location leaves orders and work orders unchanged, because they store a copy of the `Address`, not the location id (`AccountLocationsShippingAddressSource` offers the address; `WorkOrder.ServiceAddress` is an `Address`).

Tests: refusal tests (not signed in, without the permission, with the account as the resource for the account writes) for all 18 tools; the territory and geocoding-status tools run on the real territory manager, resolver and geocode request store over SQLite; the verification tools on a SQLite store with the content item and verification indexes and the real records service; the address and phone tools on the real address services and `DefaultPhoneNumberService`; the time zone and account tools with doubles of the managers, content manager, user manager and staff directory. No map or verification provider was called, and no tool was run in a live chat hub.

## Batch 20: business buying, price lists, customers and teams

No tool of batch 20 was deferred. None of the three modules referenced `AI.Core`; each now references the `CrestApps.Core.AI` package and has its own internal base class (`B2BAccountsAIToolBase`, `CustomersAIToolBase`, `TeamsAIToolBase`), which refuses a caller who is not signed in or lacks the module's permission before any argument is read, and any argument its schema does not declare. Startups: `AIToolsStartup` in Accounts.B2B and Teams; in Customers `AIToolsStartup` (seven tools), `StoreCreditAIToolsStartup` (feature `CrestApps.OrchardCore.Customers.StoreCredit`) and `LoyaltyAIToolsStartup` (feature `CrestApps.OrchardCore.Customers.Loyalty`). None of the three modules has a public API baseline. B2B detects a B2B account through `IAccountContentTypeProvider` (Accounts.Abstractions) and the type's `B2BAccountPart`; it uses no type from the Accounts module assembly.

Extractions (behavior-preserving; the admin code calls them now, and keeps its own messages, so no localized string moved):

- `B2BAccountSettingsRules` (Accounts.B2B): the term ranges, order limit clean-up, punchout buyer check, applying the settings and the punchout, and issuing a key, from the private `ApplyAsync` of `B2BAccountPartDisplayDriver`. Issuing a key is a separate step the driver calls and the tools never do.
- `PriceListRules` (Accounts.B2B): adding a price by SKU, the name, percentage and size checks, applying them, and deleting a list with its accounts moved to the store's prices, from `PriceListsAdminController`.
- `CustomerEditorRules` (Customers): the list filters, the form's checks, applying and reading the form, whether an address book can change, and the page's history (recent orders, merges), from `CustomersAdminController`.
- `CustomerBalanceRules` (Customers): the checks of the Store credit and Rewards cards, from `StoreCreditAdminController` and `LoyaltyAdminController`.

Open pull requests: 1331 (Invoicing) changes `B2BAccountCardsDisplayDriver` (the Invoices tab moves to the Invoicing module), `StoreCreditService` (adds `TryChangeAsync` and archived activity) and both modules' `Startup.cs`; none of those files was edited here. `getB2BAccountSummary` lists open term invoices through `AccountTermsService.ListInvoicesAsync`, which 1331 keeps. Pull request 1332 (Payroll), listed as open in the brief, is merged upstream; it adds the team address card, which `saveTeam` leaves alone. Neither pull request adds a tool of this batch.

Money and personal data: store credit and points are changed only through the services the cards call, with the caller's user name as the actor and the reason kept with the activity, and both ask for approval. `exportCustomerData` returns the Export action's address, never the data. `eraseCustomerData` and the customer writes save at once and report a concurrent change (`ConcurrencyException`) instead of answering first. No punchout key or key hash is returned (a canary test checks the hash), and none is accepted.

Tests: refusal tests (not signed in, without the module permission, and for the account tools without the content permission on the account) for all 18 tools; the B2B tools run with doubles of the content manager, price list store, account store and user manager and the real `AccountTermsService`; the customer tools on an in-memory customer store with the real address book, address editor, `StoreCreditService` and `LoyaltyService` over store doubles, and a privacy service double; the team tools with a team manager double whose validation copies the parent and cycle rules. The real catalog handlers, YesSql stores, personal-data contributors and a live chat hub were not run.

## Batch 21: skills, certificates and staff

21 of 22 tools built; `setStaffPay` is deferred (see "Why tools were deferred"). The existing `staffFind` and `staffReportingLine` are unchanged; `getStaffMember` finds a person by id, employee number or name and returns their id, which `staffFind` does not.

Placement: the 14 Skills tools are in the Skills module (`AIToolsStartup`, gated on `AIFeatureIds.Area`), which now references the `CrestApps.Core.AI` and `OrchardCore.Media.Abstractions` packages and has its own base class, `SkillsAIToolBase`. The Staff tools are in the Staff module, in two new startups beside the unchanged `AIToolsStartup`: `StaffManagementAIToolsStartup` (six tools) and `BadgesAIToolsStartup` (feature `CrestApps.OrchardCore.Staff.Badges`, `setStaffBadgePin`). They use a new base, `StaffManagementAIToolBase`, whose gate is each tool's own (a permission, or a non-empty staff scope), because the older `StaffAIToolBase` reads only `HttpContext` and refuses everyone without **View staff**. Neither module has a public API baseline.

New extension point: `ISkillSubjectProvider` (Skills.Abstractions, public) describes the people of one subject kind to Skills (names, and whether the caller may change their skills). Skills registers the one for users (`UserSkillSubjectProvider`, in its `Startup`); Staff.Core adds `StaffSkillSubjectProvider` in `AddStaffSkillLinks`, which answers with the caller's staff scope. The Skills module never uses a Staff type.

Extractions (behavior-preserving; the editors call them now):

- `StaffTeamMemberships` (Staff.Abstractions, public): "a manager can only add people to the teams they manage" and "the first membership is the main team", from the Teams module's `StaffTeamsDisplayDriver`, which keeps its message.
- `StaffMemberEditorRules` (Staff module, internal): the user linked once, the name and e-mail only without a user, the home base that drops its coordinates when moved, and the offered capabilities, from `StaffMemberDisplayDriver`.

Pull request 1332: none of the files it changed in Staff (`StaffFeatureDrivers.cs`, `Startup.cs`, `StaffProfileDisplayDriver.cs`, `StaffViewModels.cs`, `StaffRecipeStep.cs`, `StaffReports.cs`, `StaffMemberCatalogHandler.cs`, `StaffPayRates.cs`, the pay views) was edited. It adds `myPayStatement` and `payRunChanges` in Payroll; no tool here duplicates them.

Approval: the three Skills deletes, `deleteStaffMember`, `deleteStaffCapability` and `setStaffBadgePin`. `saveStaffMember` does not ask for approval, although setting status Terminated or a termination date starts other modules' offboarding (the catalog's open question).

Tests: refusal tests (not signed in, without the permission or scope of the page) for all 21 tools; the Skills tools run on the real Skills services over SQLite (`SkillsTestHarness`) and the real `UserSkillSubjectProvider`, with doubles of the user manager, the user search, the media store and the staff provider; the Staff tools run on the real staff manager, scope, hierarchy, history, badge and capability services over SQLite (`WorkforceTests.CreateAsync`) with a user manager double; a canary checks that `getStaffMember` returns no PIN hash and no home base address. The real user search, the media library, other modules' staff observers and a live chat hub were not run.

## Notes for the maintainer

- `EgressNetwork.IsInternal` does not classify NAT64 (`64:ff9b::/96`) or `198.18.0.0/15`, which the Tenant Hierarchy classifier does. `importMediaFromUrl` relies on it.
- All tool calls of one chat message share one service scope, so Orchard Core's immutable document reads stay stale until the scope commits. The URL rewriting and translation tools handle it (commit 8bf806dfe); any later tool that writes through a document manager and reads it back in the same message needs the same care.
- `AI.Agent` now references the Lucene and Elasticsearch core packages, which pulls Lucene.Net and the Elasticsearch client in for every site that installs it.
- Web crawlers fetch any address the server can reach: neither the crawler editor, the sitemap strategy nor the crawler's `HttpClient` blocks internal or loopback hosts. The crawler tools check what the editor checks and say so; an egress check belongs in the crawler's HTTP client.
- On this machine the shared compiler server produced bogus Razor errors (tag helpers not found) in batch 14; building with `-p:UseSharedCompilation=false -p:UseRazorBuildServer=false` worked.
