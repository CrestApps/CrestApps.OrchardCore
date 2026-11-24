# Agents

Part of the [AI tool coverage plan](README.md). Nothing in this file is built yet. The roster and the tool lists below are generated from the tool tables in the catalog files, with the decisions of section 2 applied; where an agent note in a catalog file differs, this file wins.

## 1. How agents work here, and what that means for the design

- An agent is an AI profile of type Agent with its own instructions and tools. The main assistant sees it as one tool and hands it a job in words; the agent does the work with its tools and reports back (`AgentProxyTool` in CrestApps.Core).
- Delegation is one level deep: an agent cannot call another agent (`AgentToolRegistryProvider` returns no agents when `AgentInvocationDepth` is above zero). So each agent has every tool its jobs need, and a few read tools are shared between agents on purpose.
- An agent that changes the site is `OnDemand`: it is offered only to profiles that select it. `AlwaysAvailable` agents are offered to every profile on the site, public chat widgets included, so no agent in this plan uses it.
- The orchestrator trims a profile's tool list above 30 entries and adds a planning step above 100 (`DefaultOrchestratorOptions`). An agent stays at 25 tools or fewer, and so does the main assistant's list of agents plus direct tools.
- Each delegation costs at least one more model call, and detail can be lost in the hand-off. An area gets an agent when it has about six or more tools or needs its own instructions; a few broadly useful tools stay direct on the assistant. A profile never gets both an agent and that agent's tools.
- Approval does not reach tools inside an agent today. F2 in [01-foundation.md](01-foundation.md#f2-approval-reaches-tools-inside-agents-m-core) fixes that, and it comes before any agent below gets a tool that asks for approval.
- Agent instructions follow the three existing templates (`content-agent.md`, `workflow-agent.md`, `calendar-agent.md`): look before you change, change only what was asked, never delete without an exact target, report what was done item by item with ids and links, never invent results.

## 2. Roster decisions that differ from the catalog files

- `SiteOperationsAgent` is not created. Redirects and URL rewrites go to `SeoAgent`; background tasks and health go to `SiteConfigurationAgent`, with test email and test SMS.
- The admin dashboard tools go to `SiteStructureAgent`, and the content access tools to `UserAccessAgent`, so `ContentAgent` stays at 25. `ContentAgent` does not carry the media and shortcode read tools; the assistant asks `MediaAgent` or `DesignAgent` for a path or a shortcode first.
- The webhooks agent is named `WebhooksAgent` (the data group called it `IntegrationsAgent`, which reads like `AIIntegrationsAgent`).
- Tools the commerce extensions and platform groups handed to another group's agent are listed under that agent (`OrdersAgent`, `UserAccessAgent`, `SeoAgent`, `DeploymentAgent`).
- `getSiteSettings` and `updateSiteSettings` are shared: `SeoAgent` and `SecurityAgent` carry them next to `SiteConfigurationAgent`, limited by their instructions to their own settings groups.

## 3. Roster

### Site (Orchard Core)

| Agent | Status | Purpose | Tools | Template lives in | Catalog |
| --- | --- | --- | --- | --- | --- |
| `ContentAgent` | existing, extended | Finds, reads, creates, edits, translates, schedules, versions and deletes content items, including the widgets and items nested inside them. | 25 | `AI.Agent` | [10](10-orchard-content.md), [13](13-orchard-site-administration.md), [22](22-crestapps-platform-and-parties.md) |
| `ContentTypeAgent` | new | Designs content types: creates and changes types, parts and fields with their settings, order, editors and display modes, and removes them. | 14 | `AI.Agent` | [10](10-orchard-content.md) |
| `SiteStructureAgent` | new | Manages what the site is built from around the content: menus, taxonomies, site-wide widgets and layers, zones, the home page and the admin dashboard. | 20 | `AI.Agent` | [10](10-orchard-content.md), [13](13-orchard-site-administration.md) |
| `SeoAgent` | new | Manages what search engines see: sitemaps, robots.txt, redirects and URL rewrites, and schema.org structured data. | 14 | `AI.Agent (structured data tools stay in Seo)` | [10](10-orchard-content.md), [22](22-crestapps-platform-and-parties.md), [13](13-orchard-site-administration.md) |
| `MediaAgent` | new | Manages the media library: finds, imports, moves, copies and deletes files and folders, and manages media profiles and the media cache. | 14 | `AI.Agent` | [11](11-orchard-media-design-search.md) |
| `DesignAgent` | new | Changes how the site is rendered: Liquid templates for the site and the admin, placement rules, shortcode templates and the current themes. | 12 | `AI.Agent` | [11](11-orchard-media-design-search.md) |
| `SearchAgent` | new | Manages search indexes and saved queries and runs searches and queries (index, SQL, GraphQL) to answer data questions. | 14 | `AI.Agent` | [11](11-orchard-media-design-search.md) |
| `UserAccessAgent` | new | Manages who can sign in and what they may do: user accounts, their status and sign-in recovery, roles and permissions, and which roles may see a content item. | 24 | `AI.Agent` | [12](12-orchard-users-security-messaging.md), [22](22-crestapps-platform-and-parties.md) |
| `SecurityAgent` | new | Manages the site's protection and identity plumbing: OpenID applications and scopes, secrets (never their values), rate limits, and the security, sign-in provider, HTTPS, CORS and reverse proxy settings. | 23 | `AI.Agent` | [12](12-orchard-users-security-messaging.md) |
| `SiteConfigurationAgent` | new | Reads and changes how the site is configured and runs: settings pages, features, custom admin menus, dynamic translations, background tasks, health, and test email and SMS. | 20 | `AI.Agent` | [13](13-orchard-site-administration.md), [12](12-orchard-users-security-messaging.md) |
| `TenantAgent` | new (Default tenant only) | Manages tenants from the Default tenant: create, set up, change, enable, disable, reload, remove, and feature profiles. | 15 | `AI.Agent` | [13](13-orchard-site-administration.md) |
| `DeploymentAgent` | new | Moves configuration and content in and out of the site with recipes, deployment plans and remote instances. | 17 | `AI.Agent` | [10](10-orchard-content.md), [13](13-orchard-site-administration.md) |
| `WorkflowAgent` | existing, extended | Reads, explains, builds, validates, publishes, enables, runs and troubleshoots the site's workflows and their instances. | 21 | `AI.Agent` | [14](14-orchard-automation.md) |
| `DataPipelineAgent` | new | Designs, previews, publishes, runs and monitors data pipelines, finds the data sets they can read, and manages shared download links. | 21 | `AI.Agent` | [14](14-orchard-automation.md) |

### AI

| Agent | Status | Purpose | Tools | Template lives in | Catalog |
| --- | --- | --- | --- | --- | --- |
| `AIConfigAgent` | new | Builds and edits assistants: profiles, agents, templates, speaking styles, guardrail topics and personas. | 19 | `AI` | [20](20-crestapps-ai-core.md) |
| `AIModelsAgent` | new | Manages where AI runs and what it costs: provider connections, deployments, prices, voices and API keys. | 13 | `AI` | [20](20-crestapps-ai-core.md) |
| `AIIntegrationsAgent` | new | Connects assistants to the outside: tool instances, MCP hosts, the site's own MCP server prompts and resources, and A2A hosts. | 15 | `AI` | [20](20-crestapps-ai-core.md) |
| `AIOperationsAgent` | existing, extended | Answers how the assistants are doing and runs the quality loop: conversations, quality scores, tests, cohorts, alerts, usage and clean-up. | 24 | `AI.Agent` | [20](20-crestapps-ai-core.md) |
| `AIKnowledgeAgent` | new | Manages what the AI assistants know: data sources, file sources, web crawlers and profile documents, including synchronizing and test-searching them. | 16 | `AI.DataSources` | [21](21-crestapps-ai-knowledge-reports-data.md) |

### Data and integration

| Agent | Status | Purpose | Tools | Template lives in | Catalog |
| --- | --- | --- | --- | --- | --- |
| `ReportsAgent` | new | Answers questions from the site's reports and data, exports reports to files, and manages scheduled report emails. | 11 | `Reports` | [21](21-crestapps-ai-knowledge-reports-data.md) |
| `ReportBuilderAgent` | new | Builds and maintains designed reports, report views and report groups, and controls who they are shared with. | 19 | `Reports` | [21](21-crestapps-ai-knowledge-reports-data.md) |
| `DataConnectorsAgent` | new | Connects external systems and exposes their tables, APIs and files as data sets for reports and data pipelines. | 11 | `DataConnectors` | [21](21-crestapps-ai-knowledge-reports-data.md) |
| `ContentTransferAgent` | new | Bulk imports and exports content items from and to CSV or Excel files and follows their progress. | 7 | `ContentTransfer` | [21](21-crestapps-ai-knowledge-reports-data.md) |
| `WebhooksAgent` | new | Manages outbound webhooks: subscriptions, signing secrets, test pings and the delivery log. | 8 | `Webhooks` | [21](21-crestapps-ai-knowledge-reports-data.md) |

### People and platform

| Agent | Status | Purpose | Tools | Template lives in | Catalog |
| --- | --- | --- | --- | --- | --- |
| `TenantHierarchyAgent` | new | Creates and manages child tenants, their features and who may enter them on a parent tenant, and manages parents and policies on the Default tenant. | 19 | `TenantHierarchy` | [22](22-crestapps-platform-and-parties.md) |
| `StaffAgent` | new | Manages the people who work for the business, their reporting line, pay, badges and capabilities, and the team hierarchy. | 13 | `Staff` | [22](22-crestapps-platform-and-parties.md) |
| `SkillsAgent` | new | Maintains the skill and certification catalog, who holds which skill, and people's certificates. | 14 | `Skills` | [22](22-crestapps-platform-and-parties.md) |
| `CustomerAgent` | new | Looks up and maintains store customers, their addresses, store credit and loyalty points, and handles data access and erasure requests. | 11 | `Customers` | [22](22-crestapps-platform-and-parties.md) |
| `AccountAgent` | new | Manages accounts (businesses and households): members, locations, account team, B2B terms, punchout and price lists. | 12 | `Accounts` | [22](22-crestapps-platform-and-parties.md) |
| `PlacesAgent` | new | Handles places and contact data: service territories, geocoding, friendly time zone names and the phone verification queue. | 14 | `Geo` | [22](22-crestapps-platform-and-parties.md) |

### Commerce

| Agent | Status | Purpose | Tools | Template lives in | Catalog |
| --- | --- | --- | --- | --- | --- |
| `CatalogAgent` | new | Manages what the store sells and at what price: products with their variants and sales, currencies, markets, and the shopping feed. | 17 | `Products` | [23](23-crestapps-commerce-core.md) |
| `InventoryAgent` | new | Answers stock questions and changes stock: counts, receipts, transfers, locations and purchase orders from suppliers. | 17 | `Inventory` | [23](23-crestapps-commerce-core.md) |
| `OrdersAgent` | new | Looks up and acts on orders: notes, refunds, cancellations, draft orders and quotes, returns, and shipping them with or without a carrier label. | 23 | `Orders` | [23](23-crestapps-commerce-core.md), [24](24-crestapps-commerce-extensions.md) |
| `PaymentsAgent` | new | Follows the money: what customers owe, payments, refunds, manual settlements, payment issues and the Stripe connection. | 15 | `Transactions` | [23](23-crestapps-commerce-core.md) |
| `PromotionsAgent` | new | Creates and tracks discounts and stored value: coupons, automatic discounts, generated codes and gift cards. | 11 | `Checkout` | [23](23-crestapps-commerce-core.md) |
| `StoreSetupAgent` | new | Configures how the store runs: the setup guide, shipping zones and methods, tax categories, jurisdictions, tables and rules, and fulfillment partners. | 20 | `Commerce` | [23](23-crestapps-commerce-core.md) |
| `PointOfSaleAgent` | new | Sets up registers and departments and reports on shifts and their cash. | 6 | `PointOfSale` | [23](23-crestapps-commerce-core.md) |
| `SubscriptionsAgent` | new | Manages recurring revenue and paid access: subscription agreements, sold-site provisioning jobs, installment plans and memberships. | 13 | `Subscriptions` | [24](24-crestapps-commerce-extensions.md) |
| `MarketplaceAgent` | new | Runs third-party selling and customer-generated content: vendors with their earnings and payouts, moderation of posted listings and of product reviews. | 11 | `Marketplace` | [24](24-crestapps-commerce-extensions.md) |
| `MarketingAgent` | new | Builds customer segments and writes, tests, sends and cancels email campaigns. | 11 | `Marketing` | [24](24-crestapps-commerce-extensions.md) |
| `BookingsAgent` | new | Manages bookings and the dates and capacity of bookable products, including cancellations with refunds and balance charges. | 8 | `Bookings` | [24](24-crestapps-commerce-extensions.md) |
| `AgenticCommerceAgent` | new | Controls which AI agent platforms may buy from the store, their credentials, and how products reach them through feeds and visibility. | 8 | `AgenticCommerce` | [24](24-crestapps-commerce-extensions.md) |

### CRM, contact center and telephony

| Agent | Status | Purpose | Tools | Template lives in | Catalog |
| --- | --- | --- | --- | --- | --- |
| `CrmAgent` | new | Works the CRM day to day: finds, schedules, completes and bulk-manages activities, prepares and loads activity batches, converts leads, manages not-in-service numbers and starts AI calls or texts. | 16 | `Omnichannel.Managements` | [25](25-crestapps-crm-contact-center-telephony.md) |
| `OmnichannelSetupAgent` | new | Configures the CRM for administrators: campaigns and groups, dispositions, cadences, subject flows, the business's own numbers and addresses, lead statuses and opportunity stages. | 17 | `Omnichannel.Managements` | [25](25-crestapps-crm-contact-center-telephony.md) |
| `MessagingAgent` | new | Reads and answers customer conversations in the messaging workspace and manages templates and broadcasts. | 11 | `Omnichannel.Messaging` | [25](25-crestapps-crm-contact-center-telephony.md) |
| `ContactCenterSetupAgent` | new | Configures the Contact Center for administrators: queues, queue groups, entry points and phone menus, business hours, agents and their entitlements, agent states, dialer profiles, voice media and interval statistics. | 24 | `ContactCenter` | [25](25-crestapps-crm-contact-center-telephony.md) |
| `ContactCenterSupervisorAgent` | new | Runs live operations for supervisors: live queue and agent state, dialer diagnostics, interventions on live calls and AI calls, agent state changes, shared voicemail, call recordings and health. | 22 | `ContactCenter` | [25](25-crestapps-crm-contact-center-telephony.md) |
| `TelephonyAgent` | new | Manages phone infrastructure and calling compliance: extensions, provider connection and phone numbers, local do-not-call lists, and dialing from the user's soft phone. | 16 | `Telephony` | [25](25-crestapps-crm-contact-center-telephony.md) |

### Operations and workforce

| Agent | Status | Purpose | Tools | Template lives in | Catalog |
| --- | --- | --- | --- | --- | --- |
| `CalendarAgent` | existing, extended | Day-to-day calendar and appointment work: find, add, change and answer events, find free time, book and manage appointments and the waitlist. | 22 | `Calendars` | [26](26-crestapps-operations-workforce.md) |
| `CalendarAdminAgent` | new | Sets up calendars and scheduling: calendars, sharing, categories, feeds, external subscriptions, appointment types and bookable resources with their working hours. | 21 | `Calendars` | [26](26-crestapps-operations-workforce.md) |
| `FieldServiceAgent` | new | Runs field service day to day: work orders, visits, technicians and the dispatch day. | 24 | `Dispatch` | [26](26-crestapps-operations-workforce.md) |
| `FieldServiceSetupAgent` | new | Sets up field service: work order types, checklists, service levels, sub-statuses, service agreements and assets. | 18 | `WorkOrders` | [26](26-crestapps-operations-workforce.md) |
| `WorkforceSchedulingAgent` | new | Plans and runs team schedules: drafts, publishing, published-shift changes, open shifts, trades, labor rule overrides and predictability pay. | 23 | `Workforce` | [26](26-crestapps-operations-workforce.md) |
| `TimeOffAgent` | new | Handles time off: requests and approvals, balances, adjustments and policy assignment. | 8 | `Workforce` | [26](26-crestapps-operations-workforce.md) |
| `TimeAndAttendanceAgent` | new | Reviews worked time: timesheets, punches, attendance, payroll export and Time Tracker entries and setup. | 23 | `Workforce` | [26](26-crestapps-operations-workforce.md) |
| `WorkforceSetupAgent` | new | Sets up Workforce: every setup catalog through one tool family, team and worker settings, kiosks, PINs and pay rates. | 13 | `Workforce` | [26](26-crestapps-operations-workforce.md) |
| `WorkforcePlanningAgent` | new | Contact center workforce planning: forecasts, capacity, the intraday view and offers, and adherence. | 18 | `Workforce.ContactCenter` | [26](26-crestapps-operations-workforce.md) |

Total: 57 agents, 4 of them existing. "Template lives in" is the module (without the `CrestApps.OrchardCore.` prefix) whose `Templates/Profiles` folder holds the agent, following the layering rule that the owner of a concept owns its AI integration.

### Direct tools on the assistant

| Tool | Why direct | Catalog |
| --- | --- | --- |
| `listSiteSettingsGroups` | Read only; answers "where is that setting" without a delegation. Also on `SiteConfigurationAgent`. | [13](13-orchard-site-administration.md) |
| `getSiteSettings` | Read only; answers "what is it set to" without a delegation. Also on `SiteConfigurationAgent`. | [13](13-orchard-site-administration.md) |
| `listAuditTrailEvents` | "Who changed this" is asked in every area. | [12](12-orchard-users-security-messaging.md) |
| `getAuditTrailEvent` | Follows `listAuditTrailEvents`. | [12](12-orchard-users-security-messaging.md) |
| `listNotifications` | The signed-in user's own notifications. | [12](12-orchard-users-security-messaging.md) |
| `markNotifications` | The signed-in user's own notifications. | [12](12-orchard-users-security-messaging.md) |
| `deleteNotifications` | The signed-in user's own notifications. | [12](12-orchard-users-security-messaging.md) |
| `findAdminPage` | Every assistant should be able to point the user to the right admin page. | [13](13-orchard-site-administration.md) |
| `validateAddress` | Used by several areas (customers, accounts, places); only on assistants for those areas. | [22](22-crestapps-platform-and-parties.md) |
| `normalizePhoneNumber` | Used by several areas (customers, accounts, places); only on assistants for those areas. | [22](22-crestapps-platform-and-parties.md) |
| `getMyTimeStatus` | Self-service for the signed-in worker. | [26](26-crestapps-operations-workforce.md) |
| `clockMyTime` | Self-service for the signed-in worker. | [26](26-crestapps-operations-workforce.md) |

The existing direct tools stay as they are: `listTimeZones`, `sendEmail`, `sendNotification`, `sendSmsMessage`, the customer-facing scheduling and shopping tools, the user memory tools and NLWeb `ask`.

## 4. Assistants: which agents go on which profile

A site with every feature on has all 57 agents, more than one profile should carry. The admin widget points at one profile today (`AIChatAdminWidgetSettings.ProfileId`). The plan ships these presets, created by "set up the admin assistant" (F11) for the features that are on:

| Assistant | Agents | Count |
| --- | --- | --- |
| Site assistant | `ContentAgent`, `ContentTypeAgent`, `SiteStructureAgent`, `SeoAgent`, `MediaAgent`, `DesignAgent`, `SearchAgent`, `UserAccessAgent`, `SecurityAgent`, `SiteConfigurationAgent`, `TenantAgent`, `DeploymentAgent`, `ContentTransferAgent`, `WorkflowAgent`, `DataPipelineAgent` (`TenantAgent` on the Default tenant, `TenantHierarchyAgent` on a parent tenant) | 15 |
| AI and data assistant | `AIConfigAgent`, `AIModelsAgent`, `AIIntegrationsAgent`, `AIOperationsAgent`, `AIKnowledgeAgent`, `ReportsAgent`, `ReportBuilderAgent`, `DataConnectorsAgent`, `WebhooksAgent` | 9 |
| Store assistant | `CatalogAgent`, `InventoryAgent`, `OrdersAgent`, `PaymentsAgent`, `PromotionsAgent`, `StoreSetupAgent`, `PointOfSaleAgent`, `SubscriptionsAgent`, `MarketplaceAgent`, `MarketingAgent`, `BookingsAgent`, `AgenticCommerceAgent`, `CustomerAgent`, `AccountAgent`, `ReportsAgent` | 15 |
| Contact center assistant | `CrmAgent`, `OmnichannelSetupAgent`, `MessagingAgent`, `ContactCenterSetupAgent`, `ContactCenterSupervisorAgent`, `TelephonyAgent`, `WorkforcePlanningAgent`, `StaffAgent`, `SkillsAgent`, `ReportsAgent` | 10 |
| Operations assistant | `CalendarAgent`, `CalendarAdminAgent`, `FieldServiceAgent`, `FieldServiceSetupAgent`, `WorkforceSchedulingAgent`, `TimeOffAgent`, `TimeAndAttendanceAgent`, `WorkforceSetupAgent`, `StaffAgent`, `SkillsAgent`, `PlacesAgent`, `ReportsAgent` | 12 |

Each assistant also gets the direct tools above, which keeps every preset under 25 entries. How the widget reaches more than one assistant is decision D2 in the README; the three options are one assistant with every agent (relying on the orchestrator's scoping, to be measured by test R3 in [03-testing.md](03-testing.md)), the widget choosing the assistant from the admin section the user is in (the page context is already sent with each message), or a switch in the widget.

Options that would shrink the roster further, not applied here because each agent was sized against the 25-tool limit: `TimeOffAgent` into `WorkforceSchedulingAgent`, `PointOfSaleAgent` into `StoreSetupAgent`, `ContentTransferAgent` into `DeploymentAgent`, `WebhooksAgent` into `AIIntegrationsAgent`, `SkillsAgent` into `StaffAgent`. Each needs two or three tools folded or dropped first.

## 5. Tools per agent

Existing tools are in italics. A tool followed by [A] asks for approval.

### Site (Orchard Core)

**`ContentAgent`** (25): *`getContentItemById`*, *`getSampleContentItemForContentType`*, *`createOrUpdateContentItem`*, *`getContentItemSchema`*, *`cloneContentItem`*, *`publishContentItem`*, *`unpublishContentItem`*, *`deleteContentItem`* [A], *`getLinkForContentItem`*, *`listContentTypesDefinitions`*, *`getContentTypeDefinition`*, `listContentItems`, `listContentItemVersions`, `restoreContentItemVersion`, `discardContentItemDraft` [A], `changeContentItemOwner` [A], `listContentItemLocalizations`, `localizeContentItem`, `moveListItem`, `listNestedContentItems`, `saveNestedContentItem`, `removeNestedContentItem` [A], `scheduleContentItem`, `findContentItemByHandle`, `getTaxonomyTerms`

**`ContentTypeAgent`** (14): *`listContentTypesDefinitions`*, *`getContentTypeDefinition`*, *`listContentPartsDefinitions`*, *`getContentPartDefinition`*, *`listContentFieldDefinitions`*, *`applyContentTypeDefinitionFromRecipe`*, *`removeContentTypeDefinition`* [A], *`removeContentPartDefinition`* [A], `getContentDefinitionOptions`, `saveContentTypePart`, `removeContentTypePart` [A], `saveContentPartField`, `removeContentPartField` [A], `reorderContentTypeElements`

**`SiteStructureAgent`** (20): `getMenuTree`, `saveMenuItem`, `deleteMenuItem` [A], `getTaxonomyTerms`, `saveTaxonomyTerm`, `deleteTaxonomyTerm` [A], `listLayers`, `saveLayer`, `deleteLayer` [A], `setLayerWidgetPlacement`, `setHomepage`, `findContentItemByHandle`, `listContentItems`, *`getContentItemById`*, *`createOrUpdateContentItem`*, *`getContentItemSchema`*, *`publishContentItem`*, *`deleteContentItem`* [A], `listAdminDashboardWidgets`, `arrangeAdminDashboardWidgets`

**`SeoAgent`** (14): `listSitemaps`, `saveSitemap`, `deleteSitemap` [A], `purgeSitemapCache`, `getRobotsTxt`, *`getSchemaOrgOptions`*, *`configureContentTypeSchemaOrg`*, *`configureSiteStructuredData`*, `getSiteSettings`, `updateSiteSettings` [A], `getContentItemStructuredData`, `listUrlRewriteRules`, `saveUrlRewriteRule`, `deleteUrlRewriteRule` [A]

**`MediaAgent`** (14): `listMediaFiles`, `getMediaFile`, `getMediaOptions`, `createMediaFolder`, `importMediaFromUrl` [A], `saveMediaTextFile` [A], `moveMediaFile`, `copyMediaFile`, `deleteMediaFile` [A], `deleteMediaFolder` [A], `listMediaProfiles`, `saveMediaProfile`, `deleteMediaProfile` [A], `purgeMediaCache`

**`DesignAgent`** (12): `listTemplates`, `getTemplate`, `saveTemplate` [A], `deleteTemplate` [A], `listPlacements`, `savePlacement`, `deletePlacement` [A], `listShortcodes`, `saveShortcodeTemplate` [A], `deleteShortcodeTemplate` [A], `listThemes`, `setTheme`

**`SearchAgent`** (14): `listIndexProfiles`, `getIndexProfile`, `saveIndexProfile`, `synchronizeIndexProfile`, `rebuildIndexProfile` [A], `deleteIndexProfile` [A], `searchIndex`, `runSearchQuery`, `listQueries`, `saveQuery` [A], `deleteQuery` [A], `runQuery`, `runSqlQuery` [A], `runGraphQLQuery`

**`UserAccessAgent`** (24): `listUsers`, `getUser`, `saveUser` [A], `setUserRoles` [A], `enableUser` [A], `disableUser` [A], `unlockUser` [A], `confirmUserEmail` [A], `deleteUser` [A], `sendUserVerificationEmail` [A], `sendUserPasswordResetEmail` [A], `disableUserTwoFactor` [A], `removeUserPasskeys` [A], `saveUserCustomSettings`, `listRoles`, `getRole`, `listPermissions`, `saveRole` [A], `deleteRole` [A], *`listTimeZones`*, `setUserDisplayName`, `setUserAvatar`, `getContentItemAccessRoles`, `setContentItemAccessRoles` [A]

**`SecurityAgent`** (23): `listOpenIdApplications`, `getOpenIdApplication`, `saveOpenIdApplication` [A], `deleteOpenIdApplication` [A], `listOpenIdScopes`, `saveOpenIdScope` [A], `deleteOpenIdScope` [A], `listSecrets`, `saveSecret` [A], `deleteSecret` [A], `moveSecrets` [A], `listRateLimitPolicies`, `saveRateLimitPolicy` [A], `enableRateLimitPolicy` [A], `disableRateLimitPolicy` [A], `deleteRateLimitPolicy` [A], `listSiteSettingsGroups`, `getSiteSettings`, `updateSiteSettings` [A], `getOpenIdSettings`, `updateOpenIdSettings` [A], `getCorsSettings`, `updateCorsSettings` [A]

**`SiteConfigurationAgent`** (20): `listSiteSettingsGroups`, `getSiteSettings`, `updateSiteSettings` [A], *`listTimeZones`*, *`listSiteFeature`*, *`searchSiteFeature`*, *`getSiteFeature`*, *`enableSiteFeature`*, *`disableSiteFeature`* [A], `listAdminMenus`, `saveAdminMenu`, `deleteAdminMenu` [A], `listDynamicTranslations`, `saveDynamicTranslations`, `sendTestEmail` [A], `sendTestSms` [A], `listBackgroundTasks`, `saveBackgroundTaskSettings`, `runBackgroundTask`, `getSiteHealth`

**`TenantAgent`** (15): *`listTenant`*, *`getTenant`*, *`createTenant`*, `updateTenant`, *`setupTenant`* [A], `getTenantSetupLink` [A], *`enableTenant`*, *`disableTenant`* [A], *`reloadTenant`*, *`removeTenant`* [A], *`listStartupRecipes`*, `listTenantFeatureProfiles`, `saveTenantFeatureProfile`, `deleteTenantFeatureProfile` [A], *`tenant`*

**`DeploymentAgent`** (17): `addContentItemsToDeploymentPlan`, `exportContentItemsToDeploymentTarget` [A], *`listNonStartupRecipes`*, *`executeNonStartupRecipe`* [A], *`getOrchardCoreRecipeJsonSchema`*, *`listOrchardCoreRecipeStepsAndSchemas`*, *`importOrchardCoreRecipe`* [A], `listDeploymentPlans`, `saveDeploymentPlan`, `deleteDeploymentPlan` [A], `exportDeploymentPlan` [A], `listRemoteInstancesAndClients`, `saveRemoteInstance` [A], `deleteRemoteInstance` [A], `saveRemoteClient` [A], `deleteRemoteClient` [A], `exportDeploymentPlanToRemote` [A]

**`WorkflowAgent`** (21): `listWorkflowTypes`, `getWorkflowType`, `listWorkflowActivities`, `getWorkflowActivity`, `saveWorkflowType`, `updateWorkflowDraft`, `publishWorkflowType` [A], `discardWorkflowDraft` [A], `listWorkflowTypeVersions`, `cloneWorkflowType`, `enableWorkflowType`, `disableWorkflowType`, `deleteWorkflowType` [A], `listWorkflowInstances`, `getWorkflowInstance`, `runWorkflowType` [A], `rerunWorkflowInstances` [A], `cancelWorkflowInstances` [A], `deleteWorkflowInstances` [A], `triggerWorkflowSignal` [A], `completeWorkflowUserTask` [A]

**`DataPipelineAgent`** (21): `listDataPipelines`, `getDataPipeline`, `listDataPipelineStepTypes`, `saveDataPipeline`, `updateDataPipelineDraft`, `previewDataPipelineStep`, `publishDataPipeline` [A], `discardDataPipelineDraft` [A], `enableDataPipeline`, `disableDataPipeline`, `resetDataPipelineState` [A], `deleteDataPipeline` [A], `runDataPipeline` [A], `listDataPipelineRuns`, `getDataPipelineRun`, `cancelDataPipelineRun`, `listDataSets`, `getDataSetSchema`, `listSharedLinks`, `revokeSharedLink` [A], `deleteSharedLink` [A]

### AI

**`AIConfigAgent`** (19): `listAIProfiles`, `viewAIProfile`, `listAITools`, `saveAIProfile`, `setAIProfileCapabilities` [A], `createAIProfileFromTemplate`, `deleteAIProfile` [A], `listAIProfileVersions`, `restoreAIProfileVersion` [A], `testAIProfile` [A], `listAIProfileTemplates`, `getAIProfileTemplate`, `saveAIProfileTemplate`, `deleteAIProfileTemplate` [A], `listAIPersonas`, `saveAIPersona`, `deleteAIPersona` [A], `listAIDeployments`, `listAIVoices`

**`AIModelsAgent`** (13): `listAIProviderConnections`, `saveAIProviderConnection` [A], `deleteAIProviderConnection` [A], `listAIDeployments`, `saveAIDeployment` [A], `setAIDeploymentPricing`, `deleteAIDeployment` [A], `listAIVoices`, `cloneAIVoice` [A], `deleteAIVoice` [A], `listAIApiKeys`, `saveAIApiKey` [A], `deleteAIApiKey` [A]

**`AIIntegrationsAgent`** (15): `listAIToolInstances`, `saveAIToolInstance` [A], `deleteAIToolInstance` [A], `listMcpConnections`, `saveMcpConnection` [A], `deleteMcpConnection` [A], `listMcpPrompts`, `saveMcpPrompt`, `deleteMcpPrompt` [A], `listMcpResources`, `saveMcpResource` [A], `deleteMcpResource` [A], `listA2AConnections`, `saveA2AConnection` [A], `deleteA2AConnection` [A]

**`AIOperationsAgent`** (24): `listAIProfiles`, `viewAIProfile`, *`listAIConversations`*, *`viewAIConversation`*, *`getAIContactMemory`*, *`summarizeAIQuality`*, *`listAITestCases`*, `runAITests` [A], *`listAIAlerts`*, *`queryChatSessionMetrics`*, `listAITestRuns`, `saveAITestCase`, `deleteAITestCases` [A], `saveAIAlertRule`, `deleteAIAlertRule` [A], `listAIQACohorts`, `saveAIQACohort` [A], `deleteAIQACohort` [A], `deleteAIConversation` [A], `deleteAllAIConversations` [A], `queryAIUsage`, `listChatInteractions`, `deleteChatInteractions` [A], `clearAIUserMemory` [A]

**`AIKnowledgeAgent`** (16): `listAIDataSources`, `saveAIDataSource` [A], `deleteAIDataSource` [A], `syncAIDataSource` [A], `searchAIDataSource`, `listAIProfileDocuments`, `addAIProfileDocument`, `removeAIProfileDocument` [A], `listAIFileSources`, `saveAIFileSource` [A], `deleteAIFileSource` [A], `runAIFileSource` [A], `listAIWebCrawlers`, `saveAIWebCrawler`, `deleteAIWebCrawler` [A], `runAIWebCrawler` [A]

### Data and integration

**`ReportsAgent`** (11): `listReports`, `getReport`, `runReport`, `exportReport`, `listReportDataSets`, `getReportDataSetSchema`, `queryReportData`, `listReportSubscriptions`, `saveReportSubscription` [A], `sendReportSubscription` [A], `deleteReportSubscription` [A]

**`ReportBuilderAgent`** (19): `listReports`, `getReport`, `runReport`, `listReportDataSets`, `getReportDataSetSchema`, `queryReportData`, `saveReportDesign`, `shareReportDesign` [A], `restoreReportDesignVersion`, `deleteReportDesign` [A], `createReportShareLink` [A], `revokeReportShareLink` [A], `listReportViews`, `saveReportView`, `refreshReportView`, `deleteReportView` [A], `listReportGroups`, `saveReportGroup`, `deleteReportGroup` [A]

**`DataConnectorsAgent`** (11): `listDataConnections`, `saveDataConnection` [A], `testDataConnection`, `deleteDataConnection` [A], `listDataConnectionResources`, `listConnectedDataSets`, `getConnectedDataSet`, `saveConnectedDataSet`, `refreshConnectedDataSet`, `previewConnectedDataSet`, `deleteConnectedDataSet` [A]

**`ContentTransferAgent`** (7): `listContentTransferTypes`, `listContentTransferEntries`, `getContentTransferEntry`, `exportContentItems`, `importContentItems` [A], `setContentImportState`, `deleteContentTransferEntry` [A]

**`WebhooksAgent`** (8): `listWebhooks`, `listWebhookEvents`, `saveWebhook` [A], `rotateWebhookSecret` [A], `deleteWebhook` [A], `sendWebhookPing` [A], `listWebhookDeliveries`, `resendWebhookDelivery` [A]

### People and platform

**`TenantHierarchyAgent`** (19): `listChildTenants`, `saveChildTenant` [A], `changeChildTenantState` [A], `removeChildTenant` [A], `restoreChildTenant` [A], `retryChildTenantSetup` [A], `discardChildTenant` [A], `listChildTenantFeatures`, `setChildTenantFeatures` [A], `listChildTenantAccessGrants`, `grantChildTenantAccess` [A], `revokeChildTenantAccess` [A], `getTenantHierarchyOverview`, `saveParentTenant` [A], `changeParentTenantState` [A], `unmakeParentTenant` [A], `removeParentTenant` [A], `moveChildTenant` [A], `detachChildTenant` [A]

**`StaffAgent`** (13): *`staffFind`*, *`staffReportingLine`*, `getStaffMember`, `saveStaffMember`, `deleteStaffMember` [A], `setStaffPay`, `setStaffBadgePin` [A], `listStaffCapabilities`, `saveStaffCapability`, `deleteStaffCapability` [A], `listTeams`, `saveTeam`, `deleteTeam` [A]

**`SkillsAgent`** (14): `listSkills`, `saveSkill`, `deleteSkill` [A], `listCertificationTypes`, `saveCertificationType`, `deleteCertificationType` [A], `getSubjectSkills`, `saveSkillAssignment`, `removeSkillAssignment`, `findSkillHolders`, `listCertificates`, `saveCertificate`, `setCertificateStatus`, `deleteCertificate` [A]

**`CustomerAgent`** (11): `listCustomers`, `getCustomer`, `saveCustomer`, `saveCustomerAddress`, `deleteCustomerAddress` [A], `exportCustomerData` [A], `eraseCustomerData` [A], `adjustCustomerStoreCredit` [A], `adjustCustomerLoyaltyPoints` [A], `validateAddress`, `normalizePhoneNumber`

**`AccountAgent`** (12): `listAccounts`, `setAccountMembers` [A], `saveAccountLocation`, `deleteAccountLocation` [A], `setAccountTeam`, `getB2BAccountSummary`, `saveB2BAccountSettings`, `configureB2BPunchout` [A], `listPriceLists`, `savePriceList`, `deletePriceList` [A], `validateAddress`

**`PlacesAgent`** (14): `listServiceTerritories`, `saveServiceTerritory`, `deleteServiceTerritory` [A], `findServiceTerritory`, `geocodeAddress` [A], `getGeocodingStatus`, `listTimeZoneMaps`, `saveTimeZoneMap`, `deleteTimeZoneMap` [A], `listPhoneNumberVerifications`, `retryPhoneNumberVerifications` [A], `validateAddress`, `normalizePhoneNumber`, *`listTimeZones`*

### Commerce

**`CatalogAgent`** (17): `listProducts`, `saveProductVariants`, `updateProductPricing`, `listCurrencies`, `saveCurrency`, `deleteCurrency` [A], `listMarkets`, `saveMarket`, `deleteMarket` [A], `getProductFeedStatus`, `regenerateProductFeedKey` [A], *`getContentItemById`*, *`getContentItemSchema`*, *`createOrUpdateContentItem`*, *`publishContentItem`*, *`unpublishContentItem`*, *`cloneContentItem`*

**`InventoryAgent`** (17): `listStockItems`, `getStockItem`, `adjustStock`, `transferStock`, `importStockCounts`, `rebuildStockItem`, `syncInventory`, `listStockLocations`, `saveStockLocation`, `deleteStockLocation` [A], `listPurchaseOrders`, `getPurchaseOrder`, `savePurchaseOrder`, `markPurchaseOrderOrdered`, `receivePurchaseOrder`, `cancelPurchaseOrder` [A], `deletePurchaseOrder` [A]

**`OrdersAgent`** (23): `listOrders`, `getOrder`, `addTimelineNote`, `refundOrder` [A], `cancelOrder` [A], `resendOrderConfirmation` [A], `saveDraftOrder`, `sendOrderInvoice` [A], `listOrderReturns`, `getOrderReturn`, `updateOrderReturnStatus` [A], `receiveOrderReturn`, `refundOrderReturn` [A], `getOrderFulfillment`, `createShipment`, `shipShipment` [A], `updateShipmentStatus`, `deleteShipment` [A], `getShipmentLabelRates` [A], `buyShipmentLabel` [A], `listOrderDownloads`, `setOrderDownloadAccess` [A], `getFundraisingCampaignProgress`

**`PaymentsAgent`** (15): `listTransactions`, `getTransaction`, `recordTransactionPayment` [A], `adjustTransaction` [A], `cancelTransaction` [A], `sendTransactionNotice` [A], `addTransactionNote`, `listPayments`, `refundPayment` [A], `listRefunds`, `resolveRefund` [A], `listPaymentIssues`, `resolvePaymentIssue`, `getStripeConnectionStatus`, `disconnectStripe` [A]

**`PromotionsAgent`** (11): `listCoupons`, `getCoupon`, `saveCoupon`, `deleteCoupon` [A], `generateCouponCodes`, `listGiftCards`, `getGiftCard`, `issueGiftCard` [A], `updateGiftCard`, `adjustGiftCardBalance` [A], `emailGiftCard` [A]

**`StoreSetupAgent`** (20): `getStoreSetupGuide`, `runStoreSetupStep` [A], `skipStoreSetupStep`, `listShippingZones`, `saveShippingZone`, `deleteShippingZone` [A], `saveShippingMethod`, `deleteShippingMethod` [A], `testShippingRates`, `listTaxRecords`, `saveTaxCategory`, `saveTaxJurisdiction`, `saveTaxTable`, `saveTaxRule`, `deleteTaxRecord` [A], `listFulfillmentPartners`, `testFulfillmentPartnerConnection` [A], `listSiteSettingsGroups`, `getSiteSettings`, `updateSiteSettings` [A]

**`PointOfSaleAgent`** (6): `listRegisters`, `saveRegister` [A], `listPosDepartments`, `savePosDepartment`, `deletePosDepartment` [A], `listRegisterShifts`

**`SubscriptionsAgent`** (13): `listSubscriptions`, `getSubscription`, `cancelSubscription` [A], `setSubscriptionPaused` [A], `listSiteProvisioningJobs`, `resolveSiteProvisioningJob`, `listInstallmentPlans`, `getInstallmentPlan`, `createInstallmentPlan`, `chargeInstallmentPayment` [A], `cancelInstallmentPlan` [A], `listMemberships`, `endMembership` [A]

**`MarketplaceAgent`** (11): `listVendors`, `getVendor`, `saveVendor` [A], `releaseVendorEarnings` [A], `payOutVendor` [A], `listClassifieds`, `approveClassified`, `rejectClassified` [A], `listProductReviews`, `moderateProductReview`, `deleteProductReview` [A]

**`MarketingAgent`** (11): `listMarketingSegments`, `getMarketingSegment`, `saveMarketingSegment`, `deleteMarketingSegment` [A], `listMarketingCampaigns`, `getMarketingCampaign`, `saveMarketingCampaign`, `sendMarketingCampaignTest` [A], `sendMarketingCampaign` [A], `cancelMarketingCampaign` [A], `deleteMarketingCampaign` [A]

**`BookingsAgent`** (8): `listBookings`, `getBooking`, `completeBooking`, `cancelBooking` [A], `chargeBookingBalance` [A], `listBookingDates`, `saveBookingDate`, `syncBookingsCalendar`

**`AgenticCommerceAgent`** (8): `listAgentPlatforms`, `getAgentPlatform`, `saveAgentPlatform` [A], `deleteAgentPlatform` [A], `regenerateAgentPlatformSecret` [A], `getAgentFeedStatus`, `pushAgentFeeds` [A], `setProductAgentVisibility`

### CRM, contact center and telephony

**`CrmAgent`** (16): `listOmnichannelActivities`, `getOmnichannelActivity`, `saveOmnichannelActivity`, `completeOmnichannelActivity`, `bulkUpdateOmnichannelActivities`, `purgeOmnichannelActivities` [A], `listActivityBatches`, `saveActivityBatch`, `previewActivityBatch`, `loadActivityBatch` [A], `deleteActivityBatch` [A], `listNotInServiceNumbers`, `setNumberNotInService`, `convertLead`, `startAIActivity` [A], `cancelAIActivity`

**`OmnichannelSetupAgent`** (17): `listOmnichannelCampaigns`, `saveOmnichannelCampaign`, `listOmnichannelCampaignGroups`, `saveOmnichannelCampaignGroup`, `listOmnichannelDispositions`, `saveOmnichannelDisposition`, `listOmnichannelCadences`, `saveOmnichannelCadence`, `listSubjectFlows`, `saveSubjectAction`, `listOmnichannelAddresses`, `saveOmnichannelAddress`, `listLeadStatuses`, `saveLeadStatus`, `listOpportunityStages`, `saveOpportunityStage`, `deleteOmnichannelRecord` [A]

**`MessagingAgent`** (11): `listMessagingConversations`, `getMessagingConversation`, `sendMessagingMessage` [A], `updateMessagingConversation`, `transferMessagingConversation`, `setMessagingAvailability`, `listMessageTemplates`, `saveMessageTemplate`, `deleteMessageTemplate` [A], `listMessagingBroadcasts`, `sendMessagingBroadcast` [A]

**`ContactCenterSetupAgent`** (24): `listContactCenterQueues`, `getContactCenterQueue`, `saveContactCenterQueue`, `listContactCenterQueueGroups`, `saveContactCenterQueueGroup`, `listContactCenterEntryPoints`, `getContactCenterEntryPoint`, `saveContactCenterEntryPoint`, `listBusinessHoursCalendars`, `saveBusinessHoursCalendar`, `checkBusinessHours`, `listContactCenterAgents`, `saveContactCenterAgent` [A], `listAgentStateReasonCodes`, `saveAgentStateReasonCode`, `listDialerProfiles`, `getDialerProfile`, `saveDialerProfile` [A], `listVoiceMedia`, `saveVoiceMedia`, `resyncVoiceMedia` [A], `deleteContactCenterRecord` [A], `getIntervalStatisticsStatus`, `rebuildIntervalStatistics`

**`ContactCenterSupervisorAgent`** (22): `getContactCenterLiveState`, `getDialerDispatchDiagnostics`, `listLiveAIVoiceCalls`, `getLiveAIVoiceCallTranscript`, `sendNoteToLiveAIVoiceCall` [A], `monitorLiveCall` [A], `interveneInLiveCall` [A], `transferLiveCall` [A], `setContactCenterAgentState`, `freeContactCenterAgent`, `sendMessageToContactCenterAgent`, `listSharedVoicemails`, `updateSharedVoicemail`, `callBackSharedVoicemail` [A], `deleteSharedVoicemail` [A], `listCallRecordings`, `getCallRecording`, `shareCallRecording` [A], `eraseCallRecording` [A], `getContactCenterHealth`, `getMyAgentWorkState`, `setMyAgentPresence`

**`TelephonyAgent`** (16): `listTelephonyExtensions`, `saveTelephonyExtension`, `deleteTelephonyExtension` [A], `dialFromSoftPhone` [A], `getTelnyxConnectionStatus`, `setTelnyxConnection` [A], `listTelnyxNumbers`, `searchTelnyxAvailableNumbers`, `buyTelnyxNumber` [A], `setTelnyxNumberCallerName` [A], `releaseTelnyxNumber` [A], `listLocalDncLists`, `importLocalDncList`, `setLocalDncListImportState`, `deleteLocalDncList` [A], `checkDoNotCallNumbers`

### Operations and workforce

**`CalendarAgent`** (22): *`listCalendars`*, *`findCalendarEvents`*, *`findFreeTime`*, *`createCalendarEvent`*, *`moveCalendarEvent`*, *`cancelCalendarEvent`*, *`listAppointmentTypes`*, *`findAppointmentSlots`*, *`bookAppointment`*, *`changeAppointment`*, `getCalendarEvent`, `updateCalendarEvent`, `deleteCalendarEvent` [A], `respondToCalendarEvent`, `listCalendarCategories`, `getTeamAvailability`, `listAppointments`, `getAppointment`, `setAppointmentStatus`, `listAppointmentWaitlist`, `addToAppointmentWaitlist`, `removeFromAppointmentWaitlist`

**`CalendarAdminAgent`** (21): `getCalendar`, `listCalendarTypes`, `saveCalendar`, `shareCalendar` [A], `deleteCalendar` [A], `saveCalendarCategory`, `deleteCalendarCategory` [A], `getCalendarFeedAddress` [A], `resetCalendarFeedAddress` [A], `importCalendarEvents`, `saveCalendarSubscription` [A], `syncCalendar` [A], `getAppointmentType`, `saveAppointmentType`, `deleteAppointmentType` [A], `listSchedulingResources`, `saveSchedulingResource`, `deleteSchedulingResource` [A], *`listCalendars`*, `listCalendarCategories`, *`listAppointmentTypes`*

**`FieldServiceAgent`** (24): *`workOrderCreate`*, *`workOrderStatus`*, *`workOrderSummarize`*, *`dispatchFindTechnician`*, *`dispatchOptimizeAndExplain`*, *`dispatchRescheduleVisit`*, `listWorkOrders`, `updateWorkOrder`, `setWorkOrderStatus`, `closeWorkOrder` [A], `addWorkOrderNote`, `recordWorkOrderPartUsed`, `answerWorkOrderChecklist`, `completeWorkOrder` [A], `createFollowUpWorkOrder`, `listVisits`, `getVisit`, `createVisits`, `unassignVisit`, `setVisitStatus` [A], `closeVisit` [A], `pinVisit`, `getDispatchBoard`, `getFieldServiceKpis`

**`FieldServiceSetupAgent`** (18): `listWorkOrderSettings`, `getWorkOrderSetting`, `saveWorkOrderSetting`, `deleteWorkOrderSetting` [A], `listRecords` (kind `agreement`), `getServiceAgreement`, `saveServiceAgreement`, `runServiceAgreement`, `deleteServiceAgreement` [A], `listAssets`, `getAsset`, `saveAsset`, `deleteAsset` [A], `recordAssetMeterReading`, `listAssetTypes`, `saveAssetType`, `deleteAssetType` [A], `getFieldServiceSetupStatus`

**`WorkforceSchedulingAgent`** (23): *`workforceWhoIsWorking`*, *`workforceMySchedule`*, *`workforceFindCover`*, *`workforceExplainViolations`*, `listShiftSchedules`, `getShiftSchedule`, `createShiftSchedule`, `saveDraftShift`, `deleteDraftShift` [A], `fillShiftSchedule`, `publishShiftSchedule` [A], `changePublishedShift` [A], `deleteShiftSchedule` [A], `listOpenShifts`, `postOpenShift` [A], `decideOpenShiftClaim` [A], `cancelOpenShift` [A], `listShiftTrades`, `createShiftTrade` [A], `decideShiftTrade` [A], `listLaborRuleOverrides`, `listPredictabilityPay`, `decidePredictabilityPay` [A]

**`TimeOffAgent`** (8): *`workforceTimeOffBalance`*, *`workforceRequestTimeOff`*, `listTimeOffRequests`, `submitTimeOffRequest` [A], `decideTimeOffRequest` [A], `getWorkerTimeOff`, `adjustTimeOffBalance` [A], `assignTimeOffPolicy`

**`TimeAndAttendanceAgent`** (23): `listTimesheets`, `getTimesheet`, `prepareTimesheets`, `decideTimesheet` [A], `lockPayPeriod` [A], `saveTimesheetPunch` [A], `removeTimesheetPunch` [A], `addTimesheetLine` [A], `removeTimesheetLine` [A], `decidePunchCorrection`, `listAttendanceExceptions`, `decideAttendanceException`, `listPayrollExports`, `runPayrollExport` [A], `listTimeEntries`, `getTimeTotals`, `saveTimeEntry`, `deleteTimeEntry` [A], `listTimeTrackerSetup`, `saveTimeTrackerSetup`, `deleteTimeTrackerSetup` [A], `getContactCenterTimeStages`, `saveContactCenterTimeStages`

**`WorkforceSetupAgent`** (13): `listWorkforceSetup`, `getWorkforceSetup`, `saveWorkforceSetup`, `deleteWorkforceSetup` [A], `saveWorkforceTeamSettings`, `saveWorkerWorkLimits`, `getWorkforceSetupStatus`, `getWorkerTimeClock`, `setWorkerTimeClockPin` [A], `saveWorkerPayRate` [A], `removeWorkerPayRate` [A], `renewTimeClockKioskAddress` [A], `createWorkersForAgents`

**`WorkforcePlanningAgent`** (18): *`workforceForecastSummary`*, *`workforceOutOfAdherence`*, `listForecasts`, `getForecast`, `createForecast`, `adjustForecast`, `activateForecast`, `deleteForecast` [A], `getCapacityPlanProjection`, `getIntradayStatus`, `reforecastIntraday`, `postIntradayOffer` [A], `closeIntradayOffer`, `getAdherenceDay`, `listAdherenceExceptions`, `addAdherenceException`, `deleteAdherenceException` [A], `listWorkforceSetup`
