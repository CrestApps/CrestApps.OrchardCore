---
title: "Proposal: Reusable Wizard (Stepper) Module"
sidebar_label: "Wizard Module (Proposal)"
draft: true
---

# Proposal: Reusable Wizard (Stepper) Module

> **Status:** Draft proposal for review. No code has been written yet.
> **Target version:** 3.0.0 (`VersionPrefix` in `Directory.Build.props`).
> **Reviewed independently** by a second model; see [Independent review](#independent-review).
> This revision incorporates that review's findings.

## 1. Summary

The multi-step "stepper/wizard" experience is currently implemented **twice** in this
repository, once for subscription sign-up and once for commerce checkout, as two nearly
identical, feature-specific engines. Neither can be reused by anything else.

This proposal extracts that engine into **one** standalone, reusable module
(`CrestApps.OrchardCore.Wizard`) that any feature can use to build a guided, multi-step
experience, consumable two ways:

- **Via code** — a feature contributes steps and finalization through handlers and per-step
  display drivers (this is how Subscriptions and Checkout already work, generalized).
- **Via Content Items** — a `WizardPart` (modeled on `OrchardCore.Flows`' `BagPart`) lets an
  editor compose a wizard from content items, with part **settings**, a **selectable editor**,
  and **workflow events** per step.

The Subscriptions and Checkout modules then consume the shared engine instead of each owning a
private copy.

## 2. Answers to the feasibility questions

| Question | Answer | Basis |
| --- | --- | --- |
| Can we create a reusable Wizard module? | **Yes.** | The engine already exists and is proven — *twice*. `SubscriptionFlow`/`SubscriptionFlowStep`/`ISubscriptionHandler`/`SubscriptionFlowDisplayDriver`/`ISubscriptionSessionStore` and the parallel `CheckoutFlow`/`CheckoutFlowStep`/`ICheckoutHandler`/`CheckoutSessionStore` are the same content-agnostic state machine. Only names and a few commerce fields differ. |
| Can Subscriptions use it? | **Yes.** | Subscriptions becomes a consumer that registers a `"Subscription"` wizard and moves its handlers/drivers onto the generic contracts. Payment/Stripe/tax stay in Subscriptions. |
| Can we use it with Contents like `BagPart`? | **Yes**, with a deliberate design for the runtime model and security (Sections 5.4, 6.2, 7). A `WizardPart` holds step content items; a built-in handler projects them into steps. |
| Can it be flexible (code **and** Contents)? | **Yes.** | The engine core knows nothing about content items or payments. Content and commerce concerns are optional layers on the same public extension points. |

**Recommendation: proceed.** This is a **consolidation** of existing duplication, not a new
abstraction, which is the strongest possible feasibility signal. It is *not* a pure rename,
however: the **content-driven public consumer** and the **payment/async completion path** need
first-class design (Sections 7–8). With those designed as below, it "won't complicate things" —
it will *reduce* net complexity by deleting one of the two duplicate engines.

### Naming

Recommend module id **`CrestApps.OrchardCore.Wizard`**; concept named **Wizard** (the multi-step
process), with **Stepper** reserved for the UI shape that renders progress.

## 3. Baseline: the existing engines

There are **two** structurally identical engines today. Extracting a third-party-quality generic
engine is low-risk because the shape is already validated by both.

### 3.1 Subscriptions (`CrestApps.OrchardCore.Subscriptions.*`)

- **`SubscriptionFlow`** — in-memory navigation coordinator over a session: `GetSortedSteps`,
  `GetCurrentStep`/`SetCurrentStep`, `GetNextStep`/`GetPreviousStep`, `GetFirstStep`/`GetLastStep`,
  `CurrentStepEquals`. Sorts visible (`!Conceal`) steps by `Order` then session order. Carries a
  required `ContentItem`.
- **`SubscriptionFlowStep`** — `Key`, `Title`, `Description`, `Order`, `CollectData`, `Data`
  (`Dictionary<string,object>`), `Conceal`, plus commerce-only `BillingItem[] BillingItems`.
- **`ISubscriptionFlowSession`/`SubscriptionSession : Entity`** — durable, resumable state:
  `SessionId`, `CurrentStep`, `Status`, `Steps`, `SavedSteps` (`JsonObject`), `OwnerId`, anonymous
  metadata (`IPAddress`, `AgentInfo`), content target (`ContentType`, `ContentItemId`,
  `ContentItemVersionId`). Note it also inherits `Entity.Properties`, used to persist invoices,
  Stripe metadata, payment metadata, and tax snapshots.
- **`ISubscriptionHandler`/`SubscriptionHandlerBase`** — nine lifecycle events
  (`Activating/Activated`, `Initializing/Initialized`, `Loading/Loaded`, `Completing/Completed`,
  `Failed`). `ContentSubscriptionHandler.Activating` adds **one step per configured content type**
  (`Order = (i+1)*10`); `Completing` creates/publishes the collected items.
- **`SubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlow>`** — base that runs a driver
  only for its `StepKey`; step UI = a display driver.
- **`ISubscriptionSessionStore`/`SubscriptionSessionStore`** — YesSql + `SubscriptionSessionIndex`,
  ownership-checked `GetAsync(id, status)`, `NewAsync`, `SaveAsync`.
- **`SubscriptionsController`** — navigation host: invokes lifecycle events, builds/updates the
  editor via `IDisplayManager<SubscriptionFlow>`, advances via `GetNextStep`, gates completion on
  every `CollectData` step having data, and **finalizes under `IDistributedLock`**. Resumable via
  a cookie (`SubscriptionCookieManager`, which stores **raw JSON**, not signed — see Section 9).

### 3.2 Checkout (`CrestApps.OrchardCore.Checkout.*`)

A **near-duplicate** of the above: `CheckoutFlow`, `CheckoutFlowStep` (identical fields, including
`BillingItems`), `ICheckoutHandler`, `CheckoutFlowContextBase` + the same nine contexts,
`ICheckoutSessionStore`/`CheckoutSessionStore`, `CheckoutSessionIndex`, plus a
`CheckoutReconciliationBackgroundTask`. `CheckoutFlowStep` differs from `SubscriptionFlowStep`
only in XML comments.

**Implication for the design:** `BillingItems` is a **commerce** concern shared by *both*
existing engines, not a subscription-only one. The generic wizard step must **not** include it.
Instead, introduce an optional intermediate **commerce/billing layer** (see 5.6) that both
Subscriptions and Checkout consume, so billing/invoice logic is shared once rather than promoted
into the generic engine or duplicated again.

## 4. Reference: `OrchardCore.Flows` `BagPart` (the Contents pattern)

- **`BagPart : ContentPart`** — `List<ContentItem> ContentItems`.
- **`BagPartSettings`** — `ContainedContentTypes`, `ContainedStereotypes`, `DisplayType`,
  `CollapseContainedItems`. **There is no `Editor` property** — the selected editor is stored on
  the **content type part definition**, and `BagPartDisplayDriver.Edit` resolves it via
  `GetEditorShapeType(context)`. (This corrects an earlier draft that put `Editor` in settings.)
- **`BagPartDisplayDriver`** — `UpdateAsync` rebuilds each contained item through
  `IContentItemDisplayManager.UpdateEditorAsync` with a per-item `htmlFieldPrefix`, **merges**
  existing items to preserve nested content-item ids (e.g., media fields), runs content handlers
  (`Creating`/`Updating`), and authorizes each item. This merge protects **authoring-time**
  embedded items; it is *not* a solution for per-session runtime responses (see Section 7).
- **Alternate editors** are extra `IContentTypePartDefinitionDisplayDriver`s selected via the
  part's `Editor` value.

The community "Tenant Registration" cookbook (Surevelox) validates the content-driven model:
steps composed of widgets/content, add/remove customizable, recipe-seedable.

## 5. Proposed architecture

Three-tier split (Abstractions → Core → Module), matching repo conventions.

### 5.1 `CrestApps.OrchardCore.Wizard.Abstractions`

- `WizardStep` — `Key`, `Title`, `Description`, `Order`, `CollectData`, `Data`
  (`Dictionary<string,object>`), `Conceal`, and an explicit **per-step state**
  (`StepState`: `NotStarted`, `InProgress`, `Completed`, `Failed`) rather than inferring
  completion from `SavedSteps.ContainsKey`. **No** billing/payment fields.
- `WizardIdentity` — the mandatory execution identity (see 5.5): `WizardType`, optional
  `DefinitionId`, `DefinitionVersionId`, `PartName`, and per-step `StepId`.
- `IWizardSession : IEntity` / `WizardSession : Entity` — durable, resumable state incl.
  `WizardIdentity`, `Status` (`WizardSessionStatus`: `Pending`, `Completed`, `Failed`,
  `Abandoned`), `Steps`, `SavedSteps` (`JsonObject`), `OwnerId`, anonymous metadata, timestamps,
  and a completion **idempotency key**/attempt marker.
- `IWizardHandler` / `WizardHandlerBase` — the same nine lifecycle events, but dispatched
  **by identity** (see 5.5), not by voluntary early-return.
- Lifecycle context classes mirroring the existing `*FlowContext` set.
- `IWizardSessionRepository` — abstraction the engine writes through, so a consumer (Subscriptions)
  can supply its **own** persistence/facade instead of a single global store (critical for
  migration, Section 8). A `DefaultWizardSessionStore` is provided for new consumers.
- `IWizardDefinition` / `IWizardDefinitionProvider` — **mandatory** registry for any wizard exposed
  through the generic public controller: declares type, route, **permissions**, completion policy,
  and navigation rules. (Not "optional convenience" — the public host must validate against it.)

### 5.2 `CrestApps.OrchardCore.Wizard.Core`

- `WizardFlow` — navigation coordinator (generalized `SubscriptionFlow`/`CheckoutFlow`), with the
  required `ContentItem` relaxed to an optional target/context.
- `WizardFlowDisplayDriver : DisplayDriver<WizardFlow>` — per-step driver base, dispatched by
  `WizardIdentity` + `StepKey` so drivers from different wizards using the same key **cannot**
  collide.
- `IWizardEngine` / `DefaultWizardEngine` — extracts controller orchestration: create/resume,
  invoke lifecycle, build/update via `IDisplayManager<WizardFlow>`, advance, gate on `CollectData`,
  and run a **completion coordinator** (Section 8) that owns **one** distributed lock, permits a
  domain **prepare-under-lock callback**, records external evidence atomically, and runs
  finalization **once** with an idempotency key.
- `DefaultWizardSessionStore` + `WizardSessionIndex` (`SessionId`, `WizardType`, `DefinitionId`,
  `Status`, `OwnerId`).
- `WizardConstants`.

### 5.3 `CrestApps.OrchardCore.Wizard` (Module)

- **`Startup`** — engine, default store, index migration, `IDisplayManager<WizardFlow>`, permissions.
- **`WizardController`** — generic navigation host validated against `IWizardDefinition`:
  `Start/{wizardType}`, `Step/{sessionId}/{step?}`, POST, `Confirmation`. Server-side navigation,
  skip, and backtrack enforcement; resume via a **data-protected** opaque token (Section 9); lock
  and rate-limit hooks as extension points.
- **Views** — `WizardStepper.cshtml` (progress UI generalized from `SubscriptionFlowStepper.cshtml`),
  buttons, confirmation; theme-overridable with placement/shape alternates that include wizard and
  step identity.
- **Workflow integration** (`[RequireFeatures("OrchardCore.Workflows")]`) — `WizardStartedEvent`,
  `WizardStepEnteredEvent`, `WizardStepCompletedEvent`, `WizardCompletedEvent`, `WizardFailedEvent`,
  with defined semantics (Section 10).
- **Permissions** — distinct `StartWizard`/`ContinueWizard` (execution) separate from content
  `EditContent` (authoring).
- **Recipes / deployment** — recipe step + schema (via `CrestApps.OrchardCore.Recipes`) to seed
  content-driven wizard definitions/parts.

### 5.4 Content integration — `[Feature("CrestApps.OrchardCore.Wizard.Contents")]`

The `BagPart`-style layer, a separate feature so code-only consumers don't pay for it.

- `WizardPart : ContentPart` — `List<ContentItem> Steps` (authored **step-definition** items),
  `[BindNever]` like `BagPart.ContentItems`. Implements `ContainedContentItemsAspect` for
  contained-item discovery.
- `WizardPartSettings` — `ContainedContentTypes`, `ContainedStereotypes`, completion policy
  (create/publish response items and/or raise workflows), navigation options (allow back/skip).
  **No `Editor` property** — editor is chosen via the part definition's editor selection
  (`GetEditorShapeType`), like `BagPart`.
- `WizardPartDisplayDriver` — models `BagPartDisplayDriver` for **authoring** (per-item prefixes,
  merge, content handlers, authorization). Uses `GetEditorShapeType(context)` for **editor
  selection parity** with other parts.
- `WizardPartSettingsDisplayDriver` — settings editor (contained types/stereotypes, completion
  policy), mirroring `BagPartSettingsDisplayDriver`.
- `ContentWizardHandler : WizardHandlerBase` — projects the authored **step-definition** items
  into runtime steps; per session, clones each into an independent **step-response** item with a
  new stable id, renders it through a **restricted public editor group** (Section 7), persists it
  into `SavedSteps`, preserves response ids on back-navigation, and applies the completion policy
  idempotently. This is the generalization of `ContentSubscriptionHandler` + `ContentStep`, but see
  Section 7 for the definition-vs-response and security requirements it must satisfy.

### 5.5 Wizard identity and dispatch (replaces a bare `WizardType` string)

A single `WizardType` string is **not** enough to isolate multiple wizards. Every session,
context, driver match, query, route, lock key, workflow correlation, and index uses a
**`WizardIdentity`**: `WizardType` + `DefinitionId` (+ `DefinitionVersionId`, `PartName`, `StepId`
where applicable). Handlers and step drivers are **dispatched by identity** (the base class filters
before invoking the derived member) so a handler cannot accidentally run for a wizard it does not
own, and two wizards sharing a step key like `"Payment"` cannot collide.

### 5.6 Optional commerce/billing layer (consolidation target)

Because `BillingItems` is shared by Subscriptions **and** Checkout, add a thin
**`CrestApps.OrchardCore.Wizard.Commerce`** (or reuse an existing commerce abstraction) that layers
billing/invoice concerns onto a wizard — e.g., a `BillingWizardStep` extension read from
`WizardStep.Data`, and an invoice aggregation handler. Both Subscriptions and Checkout consume this
instead of each carrying `BillingItems` on the core step. This is where the *net complexity
reduction* comes from.

### 5.7 Component map (old → new)

| Subscriptions / Checkout (today) | Wizard (proposed) |
| --- | --- |
| `SubscriptionFlow` / `CheckoutFlow` | `WizardFlow` (Core) |
| `SubscriptionFlowStep` / `CheckoutFlowStep` | `WizardStep` (billing → Commerce layer) |
| `ISubscriptionFlowSession` / `SubscriptionSession` (+ Checkout) | `IWizardSession` / `WizardSession` |
| `SubscriptionSessionStatus` | `WizardSessionStatus` |
| `ISubscriptionHandler` / `ICheckoutHandler` (+ bases) | `IWizardHandler` / `WizardHandlerBase` (identity-dispatched) |
| `Subscription*Context` / `Checkout*Context` | `Wizard*Context` |
| `SubscriptionFlowDisplayDriver` | `WizardFlowDisplayDriver` |
| `ISubscriptionSessionStore` / `ICheckoutSessionStore` | `IWizardSessionRepository` / `DefaultWizardSessionStore` |
| `SubscriptionsController` navigation | `WizardController` + `IWizardEngine` |
| `SubscriptionFlowStepper.cshtml` | `WizardStepper.cshtml` |
| `ContentSubscriptionHandler` + `ContentStep` | `ContentWizardHandler` (+ `WizardPart`) |
| `BillingItems` on step | `Wizard.Commerce` billing layer |

## 6. Two consumption models (both ship documented)

### 6.1 Via code (Subscriptions/Checkout pattern, generalized)

1. Register an `IWizardDefinition` (type, route, permissions, completion policy).
2. Implement `IWizardHandler`(s) via `WizardHandlerBase`, dispatched to your identity:
   `Activating` adds `WizardStep`s; `Completing` finalizes.
3. Implement one `WizardFlowDisplayDriver` per step key for that step's editor UI, reading/writing
   `session.SavedSteps[Key]`.
4. Drive navigation with the generic `WizardController`, or a thin custom controller for bespoke
   routing (Subscriptions keeps its own routes/cookies/payment endpoints).

No content types required.

### 6.2 Via Content Items (`BagPart` pattern)

1. Enable **Wizard Contents**.
2. Attach **`WizardPart`** to a content type; in **settings** choose contained content
   types/stereotypes, the **editor**, and the completion policy.
3. Author step-definition items; reorder/add/remove them like a `BagPart`.
4. Optionally attach workflows to `WizardStepCompletedEvent`/`WizardCompletedEvent`.
5. Publish; end users navigate the rendered stepper. `ContentWizardHandler` clones per-session
   response items, persists them, and completes idempotently.

No C# required.

## 7. Content-driven runtime model and security (must-design)

The current subscription handler adds a step per **content type** and creates a **fresh** content
item per session; it does **not** project authored items. `WizardPart` introduces authored items,
so the plan must define both cleanly:

- **Step definition vs. step response.** A **definition item** is embedded in `WizardPart` and is
  immutable during execution. A **response item** is cloned once per session with a **new stable
  `ContentItemId`** and stored in `SavedSteps`. Support both "type-driven" (new blank item per
  session, like today) and "template-driven" (clone an authored item's defaults).
- **Stable step key** derived from the definition item id (not the content type), so repeated steps
  of the same type and informational-only steps are supported.
- **Back-navigation** preserves the response item id and edits (do not re-clone). Editing an
  earlier step **invalidates** dependent later steps per completion policy.
- **Idempotent final creation** after a partial failure (no duplicate published items); reconcile
  like `CheckoutReconciliationBackgroundTask`.
- **Public execution security** (distinct from authoring):
  - Authoring uses normal `EditContent`/`DeleteContent`/type authorization.
  - Execution uses dedicated `StartWizard`/`ContinueWizard` permission against the **published**
    definition — **not** `EditContent` (which would make public wizards unusable and could expose
    admin-only fields).
  - Restrict runtime step types to an explicit **stereotype/allowlist** (e.g., `WizardStep`).
  - Render response items through a **dedicated public editor group/display mode** so owner,
    publication, security, and other administrative fields are never emitted.
  - Enforce navigation/skip/backtrack **server-side**.
- **Cleanup/retention** for abandoned media uploads and large/abandoned session documents.

## 8. Completion, locking, and idempotency (must-design)

The Subscriptions Stripe hosted-checkout return performs, **under one lock**: reload the pending
session, fetch Stripe state, validate payment/currency/subscription/`ClientReferenceId`, store
metadata, finalize. The generic engine must support this without deadlock or double finalization.

- **One lock, prepare-under-lock callback.** `IWizardEngine` completion acquires a single
  distributed lock and invokes a domain callback **inside** it (reload authoritative state, validate
  provider evidence, atomically record external-completion data, transition step state, run
  finalization once). A caller already holding the lock (the checkout-return path) uses the same
  coordinator rather than re-acquiring.
- **Explicit state machine** (`WizardStep.StepState` + `WizardSessionStatus`); `SavedSteps` presence
  alone is not a completion signal.
- **Externally-completed / async steps** (payment redirect, webhook) must guarantee: server-only
  transition, provider/correlation binding, replay safety, redirect/webhook race safety, idempotent
  repeated callbacks, and **no** completion from a client-supplied session id alone.
- **Idempotency beyond locking.** A lock is not exactly-once. Define a completion attempt/status +
  idempotency key, require handler idempotency, define the ordering/persistence boundary between
  `Completing`, status save, and `Completed`, define crash recovery, and use a durable per-step
  marker or outbox for workflow side effects.

## 9. Corrections adopted from review

- **Editor selection** is stored on the content type part definition, not on settings. Remove any
  `Editor` field from `WizardPartSettings`; register selectable editor shapes and resolve with
  `GetEditorShapeType`, exactly like `BagPart`.
- **Resume token security.** `SubscriptionCookieManager` stores **raw JSON** and is not
  signed/protected. The generic module must use ASP.NET **Data Protection** (or a protected opaque
  token) with `SameSite`, expiry, path, and retention, plus ownership checks. IP/user-agent binding
  is brittle and must not be the default isolation mechanism.

## 10. Workflow event semantics (must-specify)

For each event define: exact trigger point and transaction boundary; inputs (wizard identity,
session, definition item, step definition, **sanitized** response); correlation id (session id);
filters (wizard/definition/step); revisit/resubmit re-trigger behavior; failure/retry behavior;
and a durable per-step marker to prevent duplicate side effects.

## 11. Subscriptions/Checkout migration (phased, backward compatible)

Persisted `SubscriptionSession` and `CheckoutSession` documents and their YesSql indexes already
exist in production, and both carry data in the `Entity.Properties` bag (invoices, Stripe, tax).
Migration must be lossless.

- **Phase 0 — Ship Wizard** (Abstractions/Core/Module + Contents feature + Commerce layer) with full
  tests. No consumer change. Low risk.
- **Phase 1 — Subscriptions consumes the engine.** Keep `SubscriptionSession`, its serialized shape,
  collection, and **all** existing indexes (subscription, tenant, transaction, admin/report queries)
  unchanged. Provide a **lossless facade** implementing `IWizardSessionRepository`/`IWizardSession`
  over the existing `SubscriptionSession`, proxying `Entity.Properties` and translating the legacy
  step JSON. Keep `BillingItems` readable via the Commerce layer. **Do not** move `BillingItems` into
  a raw `Dictionary<string,object>` on the core step (persisted `object` values do not reliably
  round-trip). The engine operates against the caller-supplied repository — not a single global store.
  Add fixture tests that deserialize **real pre-upgrade** pending and completed documents and drive
  them through completion.
- **Phase 2 — Checkout consumes the engine** the same way, via its own facade over `CheckoutSession`.
- **Phase 3 — Route navigation through `IWizardEngine`** while keeping subscription/checkout routes,
  cookie/token names, rate-limit groups, and payment endpoints; delete the duplicated navigation code.
- **Phase 4 — Cleanup.** Remove dead code; keep public contracts as `[Obsolete]` shims if external
  consumers exist.
- **Deferred — Storage unification (optional, later).** Only if a single shared session store is
  truly needed: a dual-read/cutover migration with idempotent copying and full index rebuild, run in
  maintenance mode — **not** described as a routine data migration.

Each phase is independently shippable; the engine is validated by **three** real consumers (Contents,
Subscriptions, Checkout) before any deletion.

## 12. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Over-generalizing / complicating commerce | Keep payment/tax/Stripe in Subscriptions/Checkout; billing shared via the Commerce layer; engine stays payment-agnostic. |
| Persisted session/index migration | Runtime facade over existing sessions; keep indexes/shape unchanged; fixture tests on real documents; defer any storage unification. |
| `object` round-trip loss (`BillingItems`) | Do not relocate to a raw dictionary; keep strongly-typed via the Commerce layer. |
| Multiple wizards colliding | `WizardIdentity` on session/index/context/driver dispatch + mandatory `IWizardDefinition`. |
| Public content editors exposing admin fields | Execution permissions ≠ authoring; stereotype allowlist; restricted public editor group; server-side navigation. |
| Definition vs. response confusion / back-navigation data loss | Explicit clone-per-session response items with stable ids; preserve on back; idempotent creation. |
| Payment completion deadlock/double-charge | Single-lock completion coordinator with prepare-under-lock callback + idempotency key + explicit step states. |
| A third parallel flow abstraction | Wizard is the shared base for Subscriptions **and** Checkout; net engines go from 2→1. |
| Docs/build breakage | Ship both models' docs; validate the Docusaurus build and links. |

## 13. Deliverables / definition of done

- New projects: `Wizard.Abstractions`, `Wizard.Core`, `CrestApps.OrchardCore.Wizard`
  (with `Wizard.Contents` feature) and the `Wizard.Commerce` billing layer; added to the solution
  and `CrestApps.OrchardCore.Cms.Core.Targets`.
- Unit tests: `WizardFlow` navigation; engine gating/locking/idempotency/external completion;
  identity dispatch; content projection (definition→response, back-navigation, idempotent creation);
  and **real-document** migration fixtures.
- `WizardPart` + settings + settings/editor drivers with **editor selection parity**;
  `ContainedContentItemsAspect`; content indexing of step **definitions** (never private responses);
  GraphQL for **definitions only**; deployment/export + recipe step; named `WizardPart` support;
  localization/pinned culture-version; placement/shape alternates carrying wizard+step identity.
- Workflow events with the semantics in Section 10.
- Permissions (`StartWizard`/`ContinueWizard`) and data-protected resume tokens.
- **Documentation**: `docs/modules/wizard.md` covering *both* "via code" and "via content items",
  part settings, editor selection, security, and workflow events; changelog entry in
  `docs/changelog/3.0.0.md`; docs site builds with links resolving.
- Subscriptions **and** Checkout refactored onto the engine (Phases 1–4) with no regression.

## Independent review

The plan was reviewed by a second model (GPT-family) against the actual codebase. Verdict:
**good architectural direction; the extraction is feasible and worthwhile, but it is not a pure
rename** — the content-driven public consumer and the payment/async completion path require the
deliberate design captured above. The review also identified a pre-existing **third** duplicate
(`Checkout`) engine, which this revision now folds into the consolidation.

Blocking items raised — all now addressed in this revision:

1. Content step **definition vs. response** model → Section 7.
2. **Public editor security** (execution ≠ authoring) → Sections 5.3, 7.
3. `WizardType` insufficient → **`WizardIdentity`** + identity dispatch + mandatory
   `IWizardDefinition` → Sections 5.1, 5.5.
4. Migration understated → **lossless facade**, keep indexes, no `object` relocation, real-document
   fixtures → Sections 5.1, 11.
5. External/async completion under-specified → **single-lock prepare-under-lock coordinator** +
   state machine → Section 8.
6. Locking ≠ idempotency → idempotency key/attempt + outbox/durable markers → Sections 8, 10.

Non-blocking items adopted: the existing **Checkout** engine is named and folded in (Sections 1, 3.2,
5.6, 11); **cookie** is raw JSON, replaced by data-protected tokens (Section 9); **editor** lives on
the part definition, not settings (Sections 4, 9); workflow semantics specified (Section 10); content
parity deliverables added — GraphQL/indexing/deployment/localization/placement/cleanup (Section 13).

**Overall verdict: recommended.** Because it consolidates two (now three) existing duplicate engines
into one, the change *reduces* long-term complexity, provided the Section 7–8 designs are treated as
first-class rather than incidental.
