# AI tool coverage plan: everything the admin can do, through the assistant

Status: plan, 2026-11-16. Nothing in it is built yet.

## Goal

The admin chat widget already speaks AG-UI and WebMCP and runs AI tools, but the tools cover only part of the site: about 105 tools, mostly content, tenants, features, recipes and a few domain helpers. The goal is that an administrator can do through the assistant whatever they can do in the admin UI, in every Orchard Core module and every module of this repository.

This plan lists, feature by feature, the tools to add (name, description, kind, approval, permission), groups them into specialist agents where an area has enough tools to need one, names the framework work that has to come first, and says how each tool will be tested to prove that the model calls it and that it is useful.

## The plan in numbers

| | |
| --- | --- |
| Module folders read | 234 (103 in Orchard Core, 131 in this repository) |
| Feature sections with tools | about 190, plus 188 modules or features that need none (each with the reason) |
| Tool entries | 807: 801 new tools (4 of them only if the generic settings tools cannot host two pages) and 6 existing tools to improve |
| By kind | Read 292, Write 206, Destructive 140, External 86, Security 83 |
| Ask for approval | 309 |
| Existing tools retired or replaced | 6 (`searchForContentItems`, `searchForUsers`, `getUserInfo`, `getRoleInfo`, `createOrUpdateWorkflow`, `applySiteSettings`) |
| Agents | 57: 53 new, 4 existing and extended. None has more than 25 tools |
| Test prompts written | 591 |
| Framework items before the tools | 14 (F1 to F14) |

## Files

| File | Contents |
| --- | --- |
| [01-foundation.md](01-foundation.md) | The rules every tool follows (names, kinds, approval, permissions, secrets, where a tool lives), the framework work F1 to F14, and the existing tools to fix, replace or retire |
| [02-agents.md](02-agents.md) | How agents work and what that means for the design, the roster of 57 agents with their tools, the direct tools, and which agents go on which assistant |
| [03-testing.md](03-testing.md) | How every tool is proven to be used and useful: unit, contract, selection, routing, end to end, review, and usage after release, with thresholds |
| [90-tool-index.md](90-tool-index.md) | Every tool in one list: name, kind, approval, agent, description, and a link to its feature section |

The catalog. Each file has one section per feature: what an administrator can do in the UI, what is already covered, the settings groups, the tool table, implementation notes and test prompts. Each ends with the modules that need no tools, the agents of its area and its open questions.

| File | Area | Tools | Ask for approval |
| --- | --- | --- | --- |
| [10-orchard-content.md](10-orchard-content.md) | Orchard Core: contents, content types, localization, lists, flows, widgets, layers, taxonomies, menus, scheduling, SEO, sitemaps | 37 | 10 |
| [11-orchard-media-design-search.md](11-orchard-media-design-search.md) | Orchard Core: media, templates, placements, shortcodes, themes, indexing, search, queries, GraphQL | 40 | 15 |
| [12-orchard-users-security-messaging.md](12-orchard-users-security-messaging.md) | Orchard Core: users, roles, OpenID, secrets, rate limits, security settings, sign-in providers, audit trail, notifications, email, SMS | 46 | 29 |
| [13-orchard-site-administration.md](13-orchard-site-administration.md) | Orchard Core: settings (the generic settings tools), admin menu, dashboard, features, tenants, recipes, deployment, localization, background tasks, URL rewriting, health | 33 | 12 |
| [14-orchard-automation.md](14-orchard-automation.md) | Orchard Core: workflows and data pipelines | 42 | 16 |
| [20-crestapps-ai-core.md](20-crestapps-ai-core.md) | AI profiles, templates, personas, deployments, connections, tool instances, MCP, A2A, quality, conversations, memory, and the rules against self-modification | 60 | 32 |
| [21-crestapps-ai-knowledge-reports-data.md](21-crestapps-ai-knowledge-reports-data.md) | AI data sources, documents, file sources, crawlers, reports and the report builder, data connectors, content transfer, webhooks | 66 | 28 |
| [22-crestapps-platform-and-parties.md](22-crestapps-platform-and-parties.md) | Tenant hierarchy, staff, teams, skills, customers, accounts and B2B, addresses, phone numbers, geo, SEO structured data | 82 | 36 |
| [23-crestapps-commerce-core.md](23-crestapps-commerce-core.md) | Products, inventory, carts, checkout and promotions, orders, payments, markets, tax, shipping, fulfillment, point of sale | 97 | 34 |
| [24-crestapps-commerce-extensions.md](24-crestapps-commerce-extensions.md) | Subscriptions, memberships, marketplace, classifieds, fundraising, downloads, reviews, marketing, bookings, agentic commerce | 54 | 22 |
| [25-crestapps-crm-contact-center-telephony.md](25-crestapps-crm-contact-center-telephony.md) | CRM, messaging, contact center setup and live operations, telephony, do-not-call, outreach | 106 | 27 |
| [26-crestapps-operations-workforce.md](26-crestapps-operations-workforce.md) | Calendars, scheduling, work orders, dispatch, assets, time tracking, workforce scheduling, time off, time clock, planning | 144 | 48 |

## What exists today

Read from the registrations at commit `cf0149f2f`. These stay unless [01-foundation.md](01-foundation.md#3-existing-tools-to-fix-replace-or-retire) says otherwise.

| Module | Tools |
| --- | --- |
| `AI.Agent` (59) | `listTimeZones`, `listAIProfiles`, `viewAIProfile`, `listAIConversations`, `viewAIConversation`, `getAIContactMemory`, `summarizeAIQuality`, `listAITestCases`, `runAITests`, `listAIAlerts`, `queryChatSessionMetrics`, `applySiteSettings`, `getOrchardCoreRecipeJsonSchema`, `listOrchardCoreRecipeStepsAndSchemas`, `importOrchardCoreRecipe`, `listNonStartupRecipes`, `executeNonStartupRecipe`, `listStartupRecipes`, `createTenant`, `getTenant`, `listTenant`, `enableTenant`, `disableTenant`, `removeTenant`, `reloadTenant`, `setupTenant`, `searchForContentItems`, `getContentItemById`, `getSampleContentItemForContentType`, `getContentItemSchema`, `createOrUpdateContentItem`, `cloneContentItem`, `publishContentItem`, `unpublishContentItem`, `deleteContentItem`, `getLinkForContentItem`, `listContentTypesDefinitions`, `getContentTypeDefinition`, `listContentPartsDefinitions`, `getContentPartDefinition`, `listContentFieldDefinitions`, `applyContentTypeDefinitionFromRecipe`, `removeContentTypeDefinition`, `removeContentPartDefinition`, `listSiteFeature`, `searchSiteFeature`, `getSiteFeature`, `enableSiteFeature`, `disableSiteFeature`, `sendNotification`, `sendEmail`, `sendSmsMessage`, `getUserInfo`, `searchForUsers`, `getRoleInfo`, `listWorkflowTypes`, `getWorkflowType`, `createOrUpdateWorkflow`, `listWorkflowActivities` |
| `Calendars` (6) | `listCalendars`, `findCalendarEvents`, `findFreeTime`, `createCalendarEvent`, `moveCalendarEvent`, `cancelCalendarEvent` |
| `Scheduling` (4) | `listAppointmentTypes`, `findAppointmentSlots`, `bookAppointment`, `changeAppointment` |
| `Dispatch` (7) | `dispatchFindTechnician`, `dispatchOptimizeAndExplain`, `dispatchRescheduleVisit`, `dispatchWhereIsMyTechnician`, `workOrderCreate`, `workOrderStatus`, `workOrderSummarize` |
| `Workforce` (6) | `workforceWhoIsWorking`, `workforceMySchedule`, `workforceFindCover`, `workforceExplainViolations`, `workforceRequestTimeOff`, `workforceTimeOffBalance` |
| `Workforce.ContactCenter` (2) | `workforceForecastSummary`, `workforceOutOfAdherence` |
| `Staff` (2) | `staffFind`, `staffReportingLine` |
| `AgenticCommerce` (5) | `searchStoreProducts`, `getStoreProduct`, `startStoreCheckout`, `addToStoreCart`, `getStoreCart` |
| `Seo` (3) | `getSchemaOrgOptions`, `configureContentTypeSchemaOrg`, `configureSiteStructuredData` |
| `NLWeb` (1) | `ask` |
| In-conversation tools, not admin tools | `transferToLiveAgent`, `endCall`, `collectSecureDigits`, `pressKeys`, `moveToStage`, the four user memory tools and the chat document tools |

Existing agents: `ContentAgent`, `WorkflowAgent`, `AIOperationsAgent` (in `AI.Agent`), `CalendarAgent` (in `Calendars`), and the generic planner, executor, research, writer, reviewer, summarizer, data analyst and code assistant agents.

## The approach in short

1. **One tool does what one admin action does, with the same permission.** Every tool checks the Orchard Core permission its admin action checks, for the user in the chat. Today tools run with the profile's authority and check none (finding SEC-A3 of the November 2026 review).
2. **Few, coarse tools.** `listX` carries search, filters and paging; `saveX` creates or updates; destructive actions are separate. Settings pages are not tools: three generic tools read and update any settings group. Reports are not tools: four generic tools list, describe, run and export any report.
3. **The generic content tools stay the way to edit content.** A dedicated tool exists only where they are a poor fit: rules that live in display drivers, nested lists, actions that are not field edits, reads that need domain logic.
4. **Agents own areas.** An area with about six or more tools gets a specialist agent of at most 25 tools; the assistant delegates to it. A few read tools stay direct.
5. **Approval for what cannot be taken back.** Destructive, External and Security tools ask first. This needs a framework fix, because approval does not reach tools inside an agent today.
6. **No secrets through the model.** Tools never return a secret and by default do not accept one; they return the admin link where a person types it.
7. **The assistant cannot rewire itself.** A guard refuses changes to the running assistant's own profile, agents and tools from inside a chat, and refuses powerful tools on a profile visitors can reach.
8. **A tool that is not called, or not useful, does not ship.** Each tool has prompts, a measured selection rate and an end to end scenario.

## Order of work

Each phase is a set of pull requests, one per agent (its tools, its template, its tests, the docs), after the phase's shared pieces.

| Phase | What | Depends on |
| --- | --- | --- |
| 0 | Foundation: F1 permission checks (with the retrofit of the 59 `AI.Agent` tools), F2 approval inside agents, F3 kinds, F4 configuration guard, F5 recipe tools, F8 result convention, F10 aliases, F14 test harness; the baseline test run on the existing tools; the fixes of [01-foundation.md](01-foundation.md#3-existing-tools-to-fix-replace-or-retire) | D1, D3, D4, D5 |
| 1 | Site assistant, first half: F6 generic settings tools, F11 assistant setup; `ContentAgent`, `ContentTypeAgent`, `SiteStructureAgent`, `MediaAgent`, `UserAccessAgent`, `SiteConfigurationAgent`, and the direct tools | Phase 0 |
| 2 | Site assistant, second half: `SeoAgent`, `DesignAgent`, `SearchAgent`, `SecurityAgent`, `TenantAgent`, `DeploymentAgent`, `WorkflowAgent`, `DataPipelineAgent` | Phase 1, F7, F9 |
| 3 | AI and data: `AIConfigAgent`, `AIModelsAgent`, `AIIntegrationsAgent`, `AIOperationsAgent`, `AIKnowledgeAgent`, `ReportsAgent`, `ReportBuilderAgent`, `DataConnectorsAgent`, `ContentTransferAgent`, `WebhooksAgent`; F13 generated tool catalog | Phase 0, F4, F9 |
| 4 | Commerce: the twelve commerce agents, `CustomerAgent`, `AccountAgent` | Phase 0, F9; the payment items of the November review |
| 5 | CRM, contact center and telephony: six agents | Phase 0; D9 |
| 6 | Operations, workforce and people: nine agents, `StaffAgent`, `SkillsAgent`, `PlacesAgent`, `TenantHierarchyAgent` | Phase 0 |

F12 (how many agents one assistant can carry) is measured at the end of phase 1 and again at the end of phase 3, when enough agents exist.

Within a phase the order follows use: read tools first (they are safe, and they are what the selection tests need), then write tools, then the tools that ask for approval.

## Decisions for the owner

| # | Decision | Recommendation |
| --- | --- | --- |
| D1 | Do tools act as the user in the chat (permission checked in every tool), or keep "the profile is the authorization boundary"? | Act as the user; keep the old behavior as an explicit per-profile option. This is SEC-A3 step 2 |
| D2 | How does the widget reach more than 25 agents: one assistant relying on scoping, the widget choosing an assistant from the admin section, or a switch? | Measure first (test R3), then choose |
| D3 | Approval inside agents: fix `AgentProxyTool` in CrestApps.Core, or keep every approval tool direct on the assistant? And what happens in a chat that cannot ask (SMS, voice, workflow)? | Fix it in Core. A chat that cannot ask does not run approval tools |
| D4 | May a secret be typed into the chat and stored by a tool? | No; the tool returns the admin link, or takes the name of an existing secret |
| D5 | Tools that need a service that lives in an Orchard Core module assembly: move the contract upstream, or add bridge modules that reference the module package? | Upstream where the change is small; a bridge module where it is not |
| D6 | The recipe tools: allow every step, or deny by default the steps that have a guarded tool? | Deny by default, with a setting to lift it |
| D7 | Rename the seventeen noun-first tool names and `viewX` to the verb-first rule, with aliases? | Yes, once, with F10 |
| D8 | Setup catalogs with many kinds (Workforce has 18): one `kind`-based tool family, or one tool set per catalog? | The `kind`-based family; it avoids about 70 tools and is checked by the selection tests |
| D9 | Tools that return customer personal data to the model (transcripts, recordings, order and contact details): always on, or behind a site setting? | Behind a setting per area, off by default |
| D10 | Tools that only cost money (`testAIProfile`, `runAITests`, a full re-index): approval every time, or a lighter notice? | Approval, until usage shows it is too much |
| D11 | The thirteen commerce `saveX` tools that duplicate recipe steps: keep them? | Keep; staff who are not administrators cannot use recipes |
| D12 | Publish the test results (selection and routing rates per tool) in the docs site? | Yes, as a generated page, once phase 1 is in |

Each catalog file ends with the open questions of its area; they are smaller than these.

## How the plan was produced, and its limits

- Twelve read-only passes in parallel, one per group of modules, each reading manifests, admin menus, controllers, settings drivers, permissions, recipe and deployment steps, endpoints and the main services. Nothing was built or run.
- Read at CrestApps.OrchardCore `30f45a0c3`, Orchard Core `47c0c2c1d` (main, 2026-11-16) and CrestApps.Core `8b74c121`. File and type names are for those commits.
- The findings about the framework that shape the design (no permission check in the `AI.Agent` tools, approval not reaching agents, the search term of `searchForContentItems` not applied, the scoping thresholds) were read a second time in the code. They were not run; F2 in particular should be confirmed with a test before work starts.
- Everything else rests on one reading. Each tool's row is a starting point: confirm the permission, the service and the extraction it names before building it. Rows marked "unverified" say so.
- The counts come from the tool tables; [90-tool-index.md](90-tool-index.md) and the tool lists in [02-agents.md](02-agents.md) are generated from them.
- The plan covers what the admin UI can do. Things only possible through code, `appsettings.json` or a server are out of scope, and listed as "No tools needed" with the reason.
- Tool names and descriptions are proposals. The selection tests decide the final wording.
