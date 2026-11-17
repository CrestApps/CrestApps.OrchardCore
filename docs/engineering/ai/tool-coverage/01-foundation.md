# Foundation: rules and framework work that come before the tools

Part of the [AI tool coverage plan](README.md). Nothing in this file is built yet.

Adding about 800 tools on top of today's framework would make three existing problems much larger: tools run with the profile's authority and check no permission, approvals do not reach tools inside an agent, and the recipe tools can do anything the dedicated tools refuse. This file lists the rules every new tool follows (section 1), the framework work that must land first (section 2, items F1 to F14), and the existing tools to fix or replace (section 3).

Each fact about today's code below was read in the code at the commits named in the README. Nothing was run. Items that rest on one reading say so.

## 1. Rules for every tool

### 1.1 Names and shape

- camelCase, verb first, unique across the site, with the domain noun: `listX`, `getX`, `saveX`, `deleteX`, `enableX` / `disableX`, and action verbs (`runX`, `sendX`, `rebuildX`, `importX`, `exportX`, `assignX`).
- Few, coarse tools. Search, filters and paging are arguments of `listX`. One `saveX` creates when no id is given and otherwise merges the given fields into the stored record. Closely related actions share a tool with an `action` argument only when they take the same arguments and have the same risk. A destructive action is never folded into a read or write tool.
- `listX` returns summaries with ids, a total and a page; `getX` returns one record. Results are built from an allow-list of fields, never by serializing the stored entity.
- Every tool takes ids, not display names, for anything it changes. The model finds the id with a `listX` call first; a `saveX` or `deleteX` that receives a name it cannot resolve to exactly one record refuses and returns the candidates.
- The JSON schema has `additionalProperties: false`, a description on every property, enums where the UI has a fixed list, and the same limits as the UI (page size, lengths, ranges).
- The description is one or two sentences written for the model: what the tool does, when to use it, and the key limit or precondition. The title and description are localized (`S["..."]`) in the registration, as today.
- Dates and times in results are in the user's or the site's time zone (`ILocalClock`), with the zone named. Tools accept local times and convert with `ILocalClock.ToUtcLenientAsync`. See "Dates and Times" in `AGENTS.md`.
- A result that names a record includes its admin link when one exists, so the assistant can offer it and the admin widget can open it with `page_navigate`.

### 1.2 Kinds and approval

| Kind | Meaning | Approval |
| --- | --- | --- |
| Read | Returns data, changes nothing | no |
| Write | Creates or changes data that can be changed back | no |
| Destructive | Deletes, or cannot be undone | yes |
| External | Sends something outside the site (email, SMS, a call, a webhook, a push to a provider), calls a paid service, or moves money | yes |
| Security | Changes who can access what, sign-in, secrets, or what code or templates the site runs | yes |

The kind is stored on the registration (F3), so the profile editor, the MCP server, WebMCP and the configuration guard can reason from data. Approval is a confirmation for the person in the chat. It is not an access check: the access check is the permission (1.3).

### 1.3 Permissions

Every tool checks, for the calling user, the same Orchard Core permission the matching admin action checks, with the same resource (the content item, the user, the role, the queue). The permission is named in each tool's row in the catalog files.

- The caller comes from the framework's `IUserAccessor` (see "How the caller is resolved" in `src/CrestApps.Docs/docs/ai/tools.md`), because `HttpContext` is often missing inside a SignalR hub call.
- A tool that changes the site refuses when there is no signed-in caller. A profile that runs without one (a workflow, a background task, an SMS or voice conversation, post-session processing) can use only tools that opt in explicitly, as the customer-facing scheduling and shopping tools do today.
- A refusal is a tool result the model can relay: what was refused, the permission that is missing, and the admin page where a person can do it.

This is step 2 of finding SEC-A3 in `docs/engineering/review-2026-11/01-security-findings.md`. The catalog files are the list of permissions that step needs.

### 1.4 Secrets and personal data

- A tool never returns a secret: a password or its hash, a security stamp, a reset or confirmation token, an API key, a client secret, a connection string, a private key, an access token, a signing secret, a PIN. It returns whether one is stored (`hasApiKey: true`) and, for Orchard Core secrets, the secret's name.
- A tool does not accept a secret from the model by default. Anything the model sends has already passed through the AI provider and is stored in the chat history. Where a credential is needed, the tool saves everything else and returns the admin link where a person types it, or takes the name of a secret that already exists on the Secrets page. Whether write-only secret arguments are allowed at all is decision D4 in the README.
- Passwords are never set through a tool. Password help is "send a reset link".
- Tools that return customer personal data (transcripts, recordings, order and contact details) send it to the model provider. Each such tool says so in its notes, and D9 in the README asks whether they need an extra site setting.

### 1.5 Where a tool lives

- A tool for an Orchard Core module lives in `CrestApps.OrchardCore.AI.Agent`, in a startup with `[RequireFeatures("<Orchard feature id>")]`, as today.
- A tool for a CrestApps module lives in that module, in an `AIToolsStartup` with `[RequireFeatures("CrestApps.OrchardCore.AI")]`, as Calendars, Scheduling, Dispatch and Workforce do today. No hard reference to `AI.Core` only to register tools (`AGENTS.md`, "Layering and project references").
- An agent is a profile template in the owning module's `Templates/Profiles/<name>-agent.md` (`ProfileType: Agent`, `AgentAvailability: OnDemand`, `AllowToolInvocation: true`, `ToolNames`, `RequiresFeatures`). The agent's instructions are in the template body, never in C#.
- A tool calls the same service the admin action calls. When the logic exists only inside a controller, a display driver or a Razor view, it is extracted into the owner's `.Core` (or the missing extension point is added to the owner) first, and the controller is changed to call it. The catalog files name each such extraction under "Implementation notes". A tool never copies controller logic.

### 1.6 When the generic content tools are enough

Anything that is only a content item (a product, a menu, a taxonomy, a widget, a subscription plan) is already reachable through `createOrUpdateContentItem` and the other content tools. A dedicated tool is proposed only where the generic tool is a poor fit:

- rules that live in display drivers, which `createOrUpdateContentItem` does not run (product prices and variants, account members, owner, Publish Later and Archive Later times, layer rules);
- nested lists, which the content tool replaces whole (menu items, taxonomy terms, flow and bag items, widget zones);
- an action that is not a field edit (set the home page, restore a version, reorder a list);
- a read that needs domain logic (stock on hand, availability, order totals).

Each feature section in the catalog says what the content tools already cover.

## 2. Framework work (F1 to F14)

Sizes: S is up to a day, M up to a week, L more than a week. "Core" means the change is in the CrestApps.Core repository and reaches this one through a package bump.

### F1. Permission checks inside tools (L)

Add a required permission to a tool registration (`.RequirePermission(permission)` or a resource-based callback) and one base class in `CrestApps.OrchardCore.AI.Core` that resolves the caller and authorizes before `InvokeCoreAsync` runs. Retrofit the 59 tools in `CrestApps.OrchardCore.AI.Agent`, none of which calls `IAuthorizationService` today (`OrchardCorePermissions.cs` mirrors six Orchard permissions and nothing uses them), and check the tools of the other modules against their catalog rows.

Today an AI profile runs its tools for anyone who may query the profile ("the profile is the authorization boundary", `docs/ai/agent.md`). Keep that as an explicit per-profile option, "run tools with the profile's authority", that needs `AccessAnyAITool` to set and is refused for the tools the guard in F4 protects, as SEC-A3 proposes. The admin assistant never uses it: it acts as the signed-in administrator.

Tests: each tool refuses a caller without its permission; a profile with the option off never runs a tool the caller could not run in the admin.

### F2. Approval reaches tools inside agents (M, Core)

Grouping tools into agents is the point of this plan, and 309 of the proposed tools ask for approval. Today approval stops at the agent boundary. `AgentProxyTool.RunWithToolsAsync` (`CrestApps.Core.AI/Orchestration/AgentProxyTool.cs`) builds a new orchestration context for the agent and reads only text from its stream. `AICompletionContextKeys.ToolApprovalSupported` is set in one place, `AIChatResponseHandler`, for the top-level turn, and `DefaultToolMaterializer` wraps a tool for approval only when it is set. A tool that needs approval therefore runs without asking when it sits inside an agent. This applies today to `deleteContentItem`, `removeContentTypeDefinition` and `removeContentPartDefinition` in the Content Agent. Read in the code, not run; confirm with a test before starting.

Fix: the agent run inherits the caller's approval support; when a tool inside the agent asks, the agent run pauses, the request is surfaced as an interrupt of the top-level run (with the agent's name, the tool and its arguments), and the answer resumes the agent where it stopped. Until F2 lands, a tool that needs approval is ticked directly on the assistant and is left out of agent templates.

Tests: an approval-required tool inside an agent pauses the top-level run; approve continues it; reject returns a refusal to the agent; a chat that cannot ask (SMS, voice, workflow) does not run it at all (see D3).

### F3. Kind on the registration (S, Core)

Add the kind of 1.2 to `AIToolDefinitionEntry` (`.AsRead()`, `.AsWrite()`, `.AsDestructive()`, `.AsExternal()`, `.AsSecurity()`); approval follows from it and `.RequireApproval()` stays for exceptions. Use it to: leave Destructive and Security tools out of "expose all tools" on the MCP server and WebMCP unless named; show the kind in the tool picker; let F4 and the tests of [03-testing.md](03-testing.md) work from data.

### F4. AI configuration guard (M)

An assistant that can edit AI profiles could change its own instructions and tools, or attach a powerful tool to a profile that visitors can reach. The recipe tools make this possible already. The guard is one service enforced at the catalog layer (`ICatalogEntryHandler<T>` for profiles, templates, personas, tool instances, MCP and A2A connections, and the settings that name the admin widget profile), acting only when the change comes from inside an AI turn (`AIInvocationScope.Current`), so it covers the new tools and the recipe path alike and leaves the admin UI alone.

- Rule A, no self-edit: the running profile, the session's top-level profile, the admin widget profile, their agents, and what their prompt and tools are built from cannot be changed or deleted from inside an AI turn. Refused, not approval-gated.
- Rule B, no powerful tools where untrusted callers can reach: a profile reachable by anonymous visitors (role permission, browser API key, a published chat widget, an always-available agent, a phone number or channel) can carry only tools marked safe for anonymous callers. The check runs in both directions: adding a tool to such a profile, and making a profile reachable.

The full design, including what counts as reachable, is in [20-crestapps-ai-core.md](20-crestapps-ai-core.md#cross-cutting-self-modification-anonymous-exposure-and-secrets--applies-to-every-section-below).

### F5. Recipe tools (M)

`importOrchardCoreRecipe`, `applySiteSettings`, `executeNonStartupRecipe` and `createOrUpdateWorkflow` all run recipes, and a recipe can do nearly everything the dedicated tools restrict: users and roles, settings including the super user, secrets, OpenID applications, templates, SQL queries, AI profiles and MCP connections, mass messaging. They are also deferred: the tool answers "successfully imported" before the recipe has run, so errors never reach the model.

- Class all recipe tools Security, check `ManageRecipes` (F1), and keep them on the Deployment Agent only.
- Give `importOrchardCoreRecipe` a step allow-list per call site and a deny-list by default for the steps that have a dedicated, guarded tool (`Users`, `Roles`, `Secrets`, `settings`, the OpenID steps, the AI and MCP steps, messaging broadcasts, provider credentials). An administrator can lift it in settings.
- Retire `applySiteSettings` when F6 lands, and replace `createOrUpdateWorkflow` with `saveWorkflowType`, which takes workflow data, not a recipe.
- Run the recipe before answering, or return a job id with a `getRecipeRunResult` tool, so the model reports what happened.

### F6. Generic site settings tools (M)

Three tools replace one tool per settings page: `listSiteSettingsGroups`, `getSiteSettings(group)` and `updateSiteSettings(group, values)`. Orchard Core has 39 static settings groups in this checkout and this repository adds about 28 more; the catalog files record each group's settings class, permission and secret fields. The design, the comparison of the three ways to read and write settings, and the list of groups are in [13-orchard-site-administration.md](13-orchard-site-administration.md#settings--orchardcoresettings-always-enabled--design-input-for-the-generic-settings-tools).

Verdict there: read with `ISiteService`, update by replaying the settings display drivers (the same path as the admin page: permission, validation, secret protection, side effects, tenant reload), never through the recipe step. It needs a spike first, because drivers bind view models whose shape differs from the stored settings. Settings pages with their own controller (OpenID server and validation, CORS) get their own tools only if the spike shows they cannot be hosted.

### F7. Module-assembly gap (decision D5, then S to L)

`CrestApps.OrchardCore.AI.Agent` references Orchard Core's abstractions and core packages only, by design (see the comment in `OrchardCorePermissions.cs`). Many tools need a service that exists only inside an Orchard Core module assembly: `TemplatesManager`, `PlacementsManager`, `ShortcodeTemplatesManager`, `MediaProfilesManager`, `ISiteThemeService`, `IAdminMenuService`, `BackgroundTaskManager`, `FeatureProfilesManager`, `RemoteInstanceService`, `ITranslationsManager`, the workflow type and instance stores, the data pipeline managers. The catalog files say, per feature, whether an abstractions-only path exists. Options, in order of preference:

1. Use the abstractions path where one exists (many do).
2. Move the contract to an Orchard Core abstractions package upstream, as the permissions comment already expects, and use it after the next Orchard Core preview.
3. Reference the Orchard Core module package from a small bridge module per area (`CrestApps.OrchardCore.AI.Agent.Workflows`, …), so `AI.Agent` itself stays free of module references.

No reflection, and no copy of the module's logic.

### F8. Results, paging and size (S)

One result convention for every tool: `{ items, total, page, pageSize }` for lists; a cap on result size (default 50 rows and about 20 KB, tool-specific where needed) with a `truncated` flag and how to get the rest; errors as `{ error, reason, fix }`; a `changed` summary for writes so the model can report exactly what it did; no "success" before the work has run (see F5).

### F9. Files in and out of the chat (M)

Several tools end with a file (report and content exports, deployment packages, payroll export) or start from one (content import, media upload, document upload). There is no shared, permission-checked store that turns bytes into a short-lived download link, or a chat attachment into a tool input. Build one in `AI.Core` (signed, expiring links through `SignedTokenService`; owner-only; size and type limits), and have every export tool return a link and every import tool accept an attachment id or a media path.

### F10. Tool aliases (S, Core)

Tool names are stored on profiles, templates, MCP server and WebMCP settings and in role permissions (`AccessAITool_{name}`). The plan replaces or renames some existing tools (section 3). Add aliases to a registration (`.WithAlias("searchForUsers")`) so stored names keep resolving and the old permission keeps granting, and a migration that rewrites stored names. Decide once whether to also rename the seventeen noun-first names (D7).

### F11. Agent templates become real agents (S)

Agent templates are starting points an administrator creates profiles from. Add "set up the admin assistant": one action (and a recipe step) that creates the assistant profile and the agents for the features that are on, attaches the agents and the direct tools, locks their system messages and marks them not removable, and points the admin widget at the assistant. Re-running it after enabling a feature adds the new agents and leaves edited ones alone.

### F12. How many agents one assistant can carry (S, measure first)

The orchestrator trims the tool list above 30 entries (`ScopingThreshold`) and plans above 100 (`PlanningThreshold`), and an agent counts as one entry. A site with every feature on would give the assistant about 58 agents. [02-agents.md](02-agents.md) proposes presets by site type that stay under 25. Before building more than that, measure with the routing test of [03-testing.md](03-testing.md) how well the scoper picks the right agent out of 40 and 58.

### F13. Tool catalog page generated from code (S)

`src/CrestApps.Docs/docs/ai/tools.md` is kept by hand and must change with every tool (`AGENTS.md`, "AI function catalog docs"). At 900 tools that will drift. Generate the catalog tables from the registrations in a test or a build step, and fail CI when the page is stale.

### F14. Test harness (M)

The contract tests, the routing and selection evaluations and the end-to-end runs of [03-testing.md](03-testing.md) need: a way to build every feature's tool registrations in a test (as `AgentToolApprovalTests` does for one module), a recording tool wrapper for selection runs, a seeded site for end-to-end runs, and the per-tool tracking sheet.

## 3. Existing tools to fix, replace or retire

About 105 tools exist today (listed in the README). The catalog files found these problems while comparing them with the admin UI. They are fixed in the phase that covers their area, and their names keep working through F10.

| Tool | Problem | Action |
| --- | --- | --- |
| All 59 tools in `AI.Agent` | No Orchard permission check | F1 |
| `searchForContentItems` | The search term is written to `SearchText` with an empty filter; `DefaultContentsAdminListQueryService.QueryAsync` executes only the parsed filter, so the term is not applied | Replace with `listContentItems`, which parses the text with the admin list's filter parser |
| `getContentItemById`, `cloneContentItem`, `deleteContentItem`, `unpublishContentItem`, `getLinkForContentItem` | Load the published version only, so a draft-only item is "not found" | Load the latest version, as the admin does |
| `createOrUpdateContentItem` | Replaces arrays whole and runs no display driver | Keep; add the dedicated tools of 1.6 and say so in its description |
| `removeContentTypeDefinition`, `removeContentPartDefinition` | Do not go through `IContentDefinitionService` (system-defined guard, removal events) | Fix in place |
| `searchForUsers` | The search term is not applied, and the result does not use the allow-list projection `getUserInfo` uses | Replace with `listUsers` |
| `getUserInfo` | Reports lockout from `IsLockoutEnabled`, which is not the lockout state | Replace with `getUser` |
| `getRoleInfo` | Serializes the role with its claims and gives no effective permissions (those inherited from Anonymous and Authenticated) | Replace with `getRole` |
| `createOrUpdateWorkflow` | Takes a recipe; no approval; answers before the import ran; the model must invent activity ids | Replace with `saveWorkflowType` |
| `listWorkflowTypes`, `getWorkflowType`, `listWorkflowActivities` | `listWorkflowTypes` returns whole workflow documents and counts before it filters; `getWorkflowType` reads the published version only, so a draft is invisible; `listWorkflowActivities` returns no property schemas although `IWorkflowActivitySchemaService` has them, and is registered only when the Recipes feature is on | Improve in place |
| `applySiteSettings` | A second recipe import under another name | Retire after F6 |
| `createTenant`, `removeTenant`, `reloadTenant` | Do not follow the admin's create and remove rules (`ITenantValidator`, feature profiles, `TenantRemovalAllowed`); wrong message for the Default tenant | Fix in place; `saveTenant` |
| `listSiteFeature`, `enableSiteFeature`, `disableSiteFeature` | Leave themes out; cannot act on another tenant | Keep; `setTheme` covers themes |
| `listAIProfiles` | Filters on a profile type that does not exist ("Embedding") and cannot list agents | Fix in place |
| `runAITests` | No tool reads the results; costs money without asking | Add `listAITestRuns`; class External |
| `staffFind` | Returns no staff id, so no follow-up tool can address the person | Fix in place |
| Seventeen noun-first names (`staffFind`, `staffReportingLine`, `workOrderCreate`, `dispatchFindTechnician`, `workforceMySchedule`, …) | Break the verb-first rule | D7: rename with aliases, or leave |
| Work order tools | Registered by Dispatch, so they disappear when Dispatch is off | Move to Work Orders |
| Tools outside `AI.Agent` | None asks for approval (only eleven `AI.Agent` tools do), although several email or text customers (visit assignment, event invitations, cancellations) | Class External with F3 |
