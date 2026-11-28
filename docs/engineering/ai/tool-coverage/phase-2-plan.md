# Phase 2 plan: agents, routing and what phase 1 left

Part of the [AI tool coverage plan](README.md). Phase 1 built the AI tools only; this file plans everything else. It takes the maintainer's review of pull request 1334 (2026-11-17) as its basis. What phase 1 actually built is in [status.md](status.md).

## Where phase 1 ended (2026-11-20)

- 706 tools (the `TheName` and `ToolName` constants under `src`; about 100 before), plus the generic record tools (`listRecords`, `getRecord`, `saveRecord`, `deleteRecord`) with 66 record kinds (the `AddAIRecordKind<` registrations). Plain setup records with a display-driver editor are kinds; records whose editor is a controller form keep dedicated tools (status.md says which and why).
- Added after `main` was merged (local merge commit `0b743ddd6`; see "Modules new from main" in status.md):
  - Tools for the modules new from main: Approvals (3), Timeline (2), Agreements (1), Knowledge Base (2), Fulfillment (2), Invoicing (14: 8 for the invoice life cycle, 6 for credit notes, estimates and recurring plans) and Service Levels (2).
  - Tools for the admin actions main added: the Transactions actions (5: preview, take back, apply credit, cancel a scheduled charge, collection settings) and shift tips (1).
  - 15 record kinds: `approvalPolicy`, `agreement`, `lateFeePolicy`, `estimate`, `recurringInvoicePlan`, `usageMeter`, `billableItem`, `ticketDesk`, `ticketStatus`, `ticketPriority`, `ticketView`, `serviceLevelPolicy`, `businessDocumentTemplate`, `paymentTerm` and `partyCollectionSettings`.
  - Main's new fields on existing tools: Transactions, Contact Center business hours, Teams, Messaging, Staff, Point of sale, Orders and Accounts.
  - Removed: `addOrderNote` (now `addTimelineNote`) and `listServiceAgreements` (now the `agreement` kind).
  - The 706 include 12 tools main shipped itself: six ticket tools, four invoicing tools (`findInvoices`, `summarizeReceivables`, `draftInvoice`, `sendInvoiceReminder`) and two payroll tools.
- 50 built tools removed and 29 never built, per the review's drop list; look-alike tools merged as the review asked (see "Merged after the maintainer's review" in status.md). One merge deliberately not done: `setAppointmentStatus` stays apart from `changeAppointment`, because approval is set per tool.
- Build: 0 errors, 0 warnings (full `--no-incremental` build). Tests: 22,110 of 22,111 pass and 1 is skipped (an Asterisk end-to-end test that needs a live Asterisk server). That full run came before the last change to the bank-wording check; the suites that use the check (the check's own tests, Invoicing and Transactions: 868 tests) were rerun after it and pass.
- Nothing was run against a live site or a live model.
- CrestApps.Core draft pull request 327: kinds, aliases, approval inside agents and fail-closed approval, merged with Core's main. Core's main already has #333 (fail-closed by leaving the tool out); this branch replaces it with a refusing stand-in: the maintainer picks one.

## Step 0: review, commit and publish phase 1 (owner)

1. The owner reviews phase 1. The OrchardCore branch has about 140 local, unpushed commits (made before the no-commit rule) plus uncommitted edits; `git diff cf0149f2f --stat` covers both. The Core branch has local commits 644f59c1 and a5eb39fe (unpushed) plus 9 uncommitted files; `git diff d0056a86 --stat` covers them.
2. `main` is merged into the branch (local merge commit `0b743ddd6`: Payroll #1332, Invoicing #1331, Fulfillment Partners #1338 and others); `getOrder` now reads the order's timeline. Our work is still uncommitted: commit it on top of the merge. The AI tools for the modules that came from main (Approvals, Timeline, Agreements, Knowledge Base, Fulfillment, Invoicing, Service Levels, ticket setup, Business Documents) and for the admin actions main added (the Transactions actions, payment terms and shift tips) were built after the merge; see "Modules new from main" in status.md.
3. Decide where the "Recipe import gate" changes go (Import permission on the four recipe tools, approval on `applySiteSettings` and `createOrUpdateWorkflow`): this pull request or the separate security work.
4. Bump CrestApps.OrchardCore to a CrestApps.Core package that contains #324 (sets `HttpContext` in hub calls) and, once merged, #327. Then use `.AsRead()`, `.AsWrite()`, `.AsDestructive()`, `.AsExternal()`, `.AsSecurity()` on every new registration (phase 1 could not: the package in use predates them), and replace `RequireApproval()` where the kind implies it. Until this bump, approval flags are honored only in the AG-UI chat: SignalR chat and MCP run approval tools without asking (fixed in Core by #333 and #327).
5. With the owner's approval: push both branches, update both draft pull requests' titles and text, open the CrestApps.OrchardCore issue and link it to pull request 1334 (Core already has issue 326), and post a comment on both pull requests describing how the tools group into agents (the roster in Step 2).

## Step 1: framework work in CrestApps.Core (before any agent)

From the review, in this order:

| Item | What |
| --- | --- |
| F15 | Tool retrieval that works: embeddings plus lexical scoring; score against the message, recent turns, page context and tools used earlier; sticky scope; structured planner output; implement or delete the dead expansion options |
| F16 | A tool-search meta-tool (`findTools`, `loadTools`) so the model can recover when scoping missed |
| F17 | Page-context routing: the widget sends admin area, route and entity id; each feature maps to its agent, pinned for that page |
| F18 | Agent hand-off and return: pass the user's words, page context and ids in; return a structured result (changed records, links, refusals); stream the agent's tool calls as AG-UI sub-run events |
| F20 | Permission UI at scale: a tool that declares an Orchard permission does not also create `AccessAITool_{name}`; group the rest |
| F21 | Model-facing descriptions separate from localized UI text, versioned |
| F3 follow-up | Approval as its own flag, not derived from the kind; a "Financial" kind for money tools |

## Step 2: agents (about 30, not 57)

Use the review's criteria: an agent only for multi-step work with its own rules whose tools are used together; direct tools for reads used everywhere and for self-service; generic record tools inside the owning agent for setup; no tool for operations measured in seconds. Cap each agent at about 20 entries. Agents that change the site are `OnDemand`, attached to the admin assistant only, with `LockSystemMessage` on.

Starting roster, merged from 02-agents.md as the review proposes:

- Site: Content, ContentType, SiteStructure, Seo, Media, Design, Search, AccessAndSecurity (UserAccess plus Security), SiteConfiguration, Tenant, Deployment (plus content transfer), Workflow, DataPipeline.
- AI and data: AIConfig (plus AIModels), AIIntegrations, AIOperations, AIKnowledge, Reports, ReportBuilder, DataConnectors, Webhooks.
- Parties: Staff (plus Skills), Customer, Account, TenantHierarchy.
- Commerce: Catalog, Inventory, Orders, Billing (every money tool: refunds, store credit, gift card balances, transactions, payments), StoreSetup (plus point of sale), Subscriptions, Marketplace, Marketing, Bookings, AgenticCommerce.
- CRM and contact center: Crm, OmnichannelSetup (plus telephony), Messaging, ContactCenterSetup, ContactCenterSupervisor.
- Operations: Calendar (one agent), FieldService (one agent), Workforce (manager-facing), WorkforcePlanning, OperationsSetup (generic record tools).
- Direct on the assistant: `findParty`, `findAdminPage`, `getSiteSettings`, `listSiteSettingsGroups`, audit trail reads, and self-service (my schedule, my time, clock in and out, my open shifts, my presence, my notifications).

Each agent ships with its template, tests through the real orchestrator, and passes the selection and routing thresholds in 03-testing.md before it is offered. Billing ships last, after F2 and the money guardrails are verified live.

## Step 3: what phase 1 left open

- Deferred tools (status.md "Why tools were deferred"): most need a service inside an Orchard Core module assembly. Options: move contracts to Orchard Core abstractions upstream, or a bridge module per area (decision D5).
- Record kinds that are read-only because their admin editor is a controller form, not a display driver (workforce setup, markets, registers, vendors, marketing segments and campaigns, tax tables, shipping zones): add editor drivers or a write hook per kind so `saveRecord` can write them.
- Read-only kinds whose editor posts a list of objects: a `saveServiceLevelPolicy` tool for `serviceLevelPolicy` (targets as `[{ metric, priority, minutes }]`, through the driver's `ServiceLevelTargetGrid`), and a write for `approvalPolicy` (stages with lists of approver rules; a new policy's type comes from the page's address).
- Agreements by customer: an agreement whose Work Orders terms name a customer who is not a member of its account is not found by that customer's ID. It needs a column on `AgreementIndex`, filled by WorkOrders (an index provider on the Agreements collection, plus a migration).
- The Priorities page's delete check misses desk priority matrices, ticket category priorities and service level policy targets, which the `ticketPriority` kind checks (see "Bugs found in main's code" in status.md). Give the page the kind's check.
- Main's services that take the history actor (the name written in a record's history) from `HttpContext`: credit notes, estimates, tickets, and the invoicing tools main shipped. A hub call has no `HttpContext` until the CrestApps.Core bump with #324 (Step 0, item 4) sets it, which fixes them.
- Pausing the reminders of a single transaction: not a tool today (one click on its page; `setPartyCollectionSettings` pauses a whole account or customer). Add one if users ask for it from the chat.
- A tool to read or adjust a business account's store credit, and store credit accounts as a record kind: not built (see "Why tools were deferred" in status.md). No admin page shows or changes an account's credit apart from the transaction page's Apply credit card.
- The Contact Center keeps its own Luhn check (`MaskingSecureCaptureTokenSink.IsValidLuhn`, `ContactCenter.Core`). It could use AI.Core's `AIMoneyArguments.IsValidLuhn` later; `ContactCenter.Core` does not reference `AI.Core` today.
- Invoice approvals (upstream design question): Invoicing has its own approval flow (**Submit for approval**, **Approve and issue**, `decideInvoiceApproval`), beside the Approvals module that pay runs use (`decideApprovalRequest`). Ask the maintainer whether invoices should go through the Approvals module instead, so one set of rules and one tool decide every request.
- `deleteRecord` always asks for approval; add a per-kind flag for easy-to-undo deletes.
- Changes to tools that existed before phase 1 (phase 1 did not change them): renames with aliases (F10), `getContentItemById` into `getContentItem`, `listWorkflowActivities(schemaFor)`, `workforceRequestTimeOff(workerId)`, `moveCalendarEvent` into `updateCalendarEvent`, `workOrderCreate(followUpOf)`, the existing tools' short descriptions, caller lookup in the older base classes (done for Scheduling; Workforce, Calendars, Staff and AgenticCommerce remain), and the existing bugs the review lists (`DispatchOptimizeTool` applying unapproved moves, `workforceFindCover` scope, `createTenant`/`setupTenant` taking a connection string and a password, `calendar-agent.md` feature requirements; `reloadTenant`'s message and `listAIProfiles`' "Embedding" type are fixed). Also: merge the two waitlist tools, and `workforceTimeOffBalance` with `getWorkerTimeOff`; align `listAccounts`' permission with `getAccount`.
- Recipe path guard (F4): the deferred recipe import runs after the AI invocation scope ends, so the configuration guard cannot see it; run recipes synchronously or carry an "AI origin" marker; `importOrchardCoreRecipe` must check `Import`.
- Prompt injection (F24): mark untrusted text in results as data; agent instructions say so.
- Money (F23) completion: per-user daily caps and a `previewRefund` token step. Give `adjustCustomerStoreCredit` (Customers) the preview-token flow too: today it gives or takes store credit with approval only.
- Files (F9): a permission-checked download store for exports and coupon codes.
- Live testing: run the selection, routing and end-to-end tests of 03-testing.md against a seeded site and real models; record approval rates.
- Generic against dedicated A/B (review section 3): measure on three agents, then decide whether to retire dedicated setup tools built in phase 1.
- Plan docs: update README, 01, 02 and the catalog files to match what was built and to the review's corrections. The Fulfillment section is done: pull request 1338 came in with the merge of main, and `listFulfillmentRequests` and `sendOrderToFulfillmentPartners` are built (batch `new-fulfillment` in status.md).
