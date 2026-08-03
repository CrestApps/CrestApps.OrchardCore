# Orchard Core Production Readiness Plan

**Branch:** `ma/add-contact-center`
**Scope:** ContactCenter, Telephony, Asterisk, DialPad and supporting Abstractions/Core projects
**Last reviewed:** 2026-08-02
**Review method:** Six independent expert reviewers (Orchard Core alignment, ASP.NET Core/.NET, extensibility & public API, UI & resource management, security & production readiness, module architecture & code quality), each running a different model against source only. Every finding below was independently re-verified against the code before being admitted to this plan.

---

## Verdict

| Axis | Score |
| --- | --- |
| **Production ready** | **No** — 1 merge blocker, 13 high-severity items |
| Overall | 7.5 / 10 |
| Orchard Core alignment | 8 / 10 |
| ASP.NET Core practice | 7 / 10 |
| Extensibility | 7 / 10 |
| Maintainability | 6 / 10 |
| Performance & scalability | 5 / 10 |
| Security | 6.5 / 10 |

This is genuinely idiomatic Orchard Core work and belongs in the ecosystem. It is blocked by a small number of specific, fixable defects — not by systemic architectural failure. `OC-001` alone blocks merge.

### Review caveat

A clean full build could **not** be verified in this environment: `dotnet build` fails with `CS0246` on `IModifiedUtcAwareModel` and `AIDataSourceSourceOptions`. This reproduces identically on `origin/main` in a clean worktree, so it is a **pre-existing package/feed issue, not a defect of this branch**. Compile-verification of the branch remains outstanding and must be performed on a machine with CloudSmith feed access before merge.

---

## How to use this plan

Work items are independent unless a `Dependencies` field says otherwise, so they can be picked up one at a time. Update `Status` in place. When an item is completed, mark it `Completed` and re-run the affected verification command in its acceptance criteria.

**Status values:** `Not Started` · `In Progress` · `Blocked` · `Completed`
**Effort:** S (<½ day) · M (1–3 days) · L (1–2 weeks) · XL (>2 weeks)

---

# Phase 1 — Critical Architecture Issues

### OC-001 — Contact Center bricks any tenant running OrchardCore.HealthChecks at its default route

- **Priority:** Critical (merge blocker)
 - **Status:** Completed
- **Category:** Startup correctness / availability
- **Effort:** S
- **Risk:** Low
- **Dependencies:** None

**Problem.** `ContactCenterSharedHealthEndpointStartup` (`src/Modules/CrestApps.OrchardCore.ContactCenter/Startup.cs:288-312`) carries `[RequireFeatures("OrchardCore.HealthChecks")]` and no `[Feature]` attribute, so it belongs to the module's **default** feature. Its `ConfigureServices` calls `SharedHealthCheckEndpointGuard.Validate(...)`, which throws `InvalidOperationException` when the health route's last segment is `live`/`liveness` and the operator has not opted out.

**Root cause.** The guard treats an unconfigured route as unsafe. `SharedHealthCheckEndpointGuard.DefaultSharedEndpointRoute` is `/health/live`, and I confirmed by extracting the string table from `OrchardCore.HealthChecks.Abstractions/3.0.0` that `/health/live` is genuinely the shipped Orchard default. So `Validate(null, false)` throws. The behaviour is deliberate — `SharedHealthCheckEndpointGuardTests.cs:36-41` asserts the unconfigured case throws.

**Why it matters.** Enabling Contact Center on a tenant that already has `OrchardCore.HealthChecks` enabled — both modules at their **shipped defaults** — throws while the shell container is being built. The shell never activates, so every request to that tenant fails, including `/admin`, which is the only place to disable the feature. Recovery requires editing shell configuration and restarting the process. On the `Default` tenant this takes the whole site down.

**Orchard Core pattern violated.** `StartupBase.ConfigureServices` is part of shell-container construction; it is not a validation phase and has no failure surface. The module's own `ContactCenterTopologyValidator` documents the correct rule — *"Throwing during activation bricks the tenant with no diagnostic surface"* — and this code does exactly that one file away. A module must also not veto another module's configuration.

**Recommended solution.** Convert the guard to a non-fatal check:
1. Move the validation out of `ConfigureServices` into `IModularTenantEvents.ActivatedAsync`.
2. Log at `Critical` and register a degraded/unhealthy Contact Center health-check entry, mirroring `BaseVoiceVerificationStartupCheck` and `ContactCenterTopologyValidator`.
3. Surface an admin notification via `INotifier` so an operator actually sees it.
4. Alternatively invert the default: treat the shipped `/health/live` as acknowledged and object only to an explicitly configured liveness route.

**Files affected.** `ContactCenter/Startup.cs:288-312` · `ContactCenter.Core/HealthChecks/SharedHealthCheckEndpointGuard.cs` · `tests/.../ContactCenter/SharedHealthCheckEndpointGuardTests.cs`

**Acceptance criteria.**
- Enabling Contact Center + `OrchardCore.HealthChecks` with no health configuration leaves the tenant fully bootable and `/admin` reachable.
- The hazard is reported via log + health check + admin notification.
- `SharedHealthCheckEndpointGuardTests` asserts the logged/health outcome instead of a throw.

---

### OC-002 — Soft-phone dialing bypasses the outbound compliance gate

- **Priority:** Critical (legal/regulatory exposure)
- **Status:** Completed
- **Category:** Compliance / security
- **Effort:** M
- **Risk:** Medium
- **Dependencies:** Permission model decision (see Notes)

**Problem.** `TelephonyHub.Dial()` (`Telephony/Hubs/TelephonyHub.cs:90-91`) dispatches straight to `DefaultTelephonyService.DialAsync` → `provider.DialAsync` after only a `UseSoftPhone` authorization check. DNC, suppression, retry limits, calling-window and abandonment checks live **only** in `DialerAttemptService` (`ContactCenter.Core/Services/DialerAttemptService.cs:94`, `_eligibilityService.EvaluateAsync`).

**Root cause.** Two independent dial paths exist — the campaign dialer path (gated) and the generic soft-phone path (ungated) — and the compliance gate was attached to the service rather than to the provider boundary that both paths share. A grep for DNC/compliance/eligibility vocabulary across the entire Telephony module returns 2 incidental hits.

**Why it matters.** With the Contact Center Dialer and Compliance features enabled, any agent holding `UseSoftPhone` can place a call to a DNC-suppressed number, or outside permitted calling hours, simply by invoking the hub — bypassing the auditable dialer path entirely. This is a TCPA/DNC regulatory exposure, not merely a design inconsistency.

**Recommended solution.** Pick one and document it:
1. **Preferred** — enforce eligibility at the shared provider boundary so *every* origination passes the gate, with a distinct audited "manual call" policy for agent-initiated dialing.
2. Deny generic PSTN dialing to Contact Center agents unless an explicitly audited, policy-screened manual-call capability is granted.

**Files affected.** `Telephony/Hubs/TelephonyHub.cs` · `Telephony/Services/DefaultTelephonyService.cs` · `ContactCenter.Core/Services/DialerAttemptService.cs` · compliance/eligibility services

**Acceptance criteria.**
- A regression test proves a DNC-suppressed number cannot be dialed through `TelephonyHub.Dial` when Compliance is enabled.
- A regression test proves calling-window enforcement applies to the soft-phone path.
- Every origination path is covered by an audit record.

**Notes.** Manual agent-initiated calls are treated differently from automated campaign dialing under TCPA, so "gate everything identically" may be the wrong answer — but the current *silent* bypass is not defensible. This needs an explicit, documented policy decision.

**Resolution (Solution 1).** Added a provider-agnostic screening extension point in Telephony (`IOutboundCallScreener` / `IOutboundCallScreeningService`, with `OutboundCallScreeningContext`/`OutboundCallScreeningResult`/`OutboundCallOrigin`). `DefaultTelephonyService.DialAsync` now runs the aggregated screeners before dispatching any origination and fails closed on the first denial (and on a null verdict from a registered screener); standalone Telephony with no screener registered still dials, preserving backward compatibility. The layer boundary is respected — Telephony does not depend on ContactCenter. The **Contact Center Outbound Compliance** feature registers `ContactCenterManualCallScreener`, which applies contact opt-out, national do-not-call registries, and (opt-in) calling-window enforcement to soft-phone dials, resolving the destination to E.164 and failing closed on an unparseable number while do-not-call is enforced. Every suppression publishes a `ManualDialSuppressed` audit event. Configured under `CrestApps_ContactCenter:Compliance:ManualDialing` (`ManualDialingComplianceOptions`, bound + validated on start — calling-window enforcement requires a calling calendar id). Tests: `OutboundCallScreeningTests`, `ManualCallScreenerTests` (including an end-to-end composition test that drives the real screener through the real `DefaultTelephonyService` and asserts the provider is untouched and the audit is recorded). Docs: `contact-center/agents-queues-dialer.md` (Manual soft-phone screening) and changelog `v2.0.0.md`. Independently reviewed by gpt-5.6. Follow-up recorded as **OC-048** (audit actor attribution).

---

### OC-003 — A never-released module ships live-upgrade migration chains, including a destructive rebuild

- **Priority:** High
- **Status:** Won't Fix — premise disproven (see Resolution)
- **Category:** Data migrations
- **Effort:** M
- **Risk:** Low
- **Dependencies:** None

**Problem (as originally stated).** The ContactCenter module does not exist on `origin/main`. Yet **14 of 25** migration classes carry `UpdateFromN` chains. `CallSessionIndexMigrations.UpdateFrom3Async` performs a destructive rebuild: schema-qualified raw `drop index if exists`, a tolerant `SchemaBuilder(throwOnError: false)` pass, two `IndexStringColumnRebuild.WidenAsync` calls, index recreation, a `GROUP BY ... HAVING COUNT(*) > 1` duplicate scan that throws on collision, and a raw `CREATE UNIQUE INDEX`. The item asserted that *every brand-new tenant* executes this rebuild against an empty table for zero benefit, and recommended collapsing every ContactCenter migration into a single `CreateAsync`.

**Resolution — Won't Fix (premise disproven; validated by independent gpt-5.6 review).**

Deeper investigation showed the central premise is factually wrong and the recommended collapse would be net-harmful:

1. **Fresh installs run no destructive rebuild.** Orchard Core's migration manager invokes `CreateAsync`, records its returned version `V`, then runs `UpdateFrom{V}Async`, `UpdateFrom{next}Async`, … until no method matches. `CallSessionIndexMigrations.CreateAsync` already builds the final schema (ProviderCallId length 256, claim key 385, all indexes and the unique constraint) and **returns 4** (`CallSessionIndexMigrations.cs:60,110`). The manager then looks for `UpdateFrom4Async` — none exists — so it runs nothing. The destructive `UpdateFrom3Async` (`:176`) runs **only** for a tenant already recorded at version 3 (a real earlier-preview adopter), as a genuine upgrade — never on a fresh tenant.
2. **The chains are an intentional, documented, tested rolling-upgrade capability, not pre-release debt.** They are exercised by `ContactCenterRollingUpgradeTests` (synthesizes the previous-version schema, applies the real upgrade steps to a live DB, then asserts both previous- and current-version writers succeed against the upgraded database; hard-floors `MinimumUpgradeStepCount = 8`), governed by `MigrationAdditiveOnlyGuardTests` (a formal contract register that detects every destructive step, requires explicit authorization, and verifies in-place rebuilds restore each object they drop), enforced by `ContactCenterRetentionCoverageTests` (`covered >= 8` upgrade steps that add a settlement column must backfill it), and validated on real PostgreSQL by the `*PostgresMigrationTests` distributed suite.
3. **Collapsing would remove a real capability and invalidate ~5,000+ lines of deliberate safety tests** to save fresh installs only a handful of harmless additive `ALTER` statements on empty tables.

**Scope carve-outs recorded during review (not part of OC-003; see OC-049).**
- A narrower change — having the additive migrations whose `CreateAsync` returns an old version (e.g. `InteractionIndexMigrations` returns 1) build the final schema and return the final version *while retaining* their `UpdateFromN` for existing tenants — is contract-legal and rolling-test compatible, but is a **measured fresh-activation optimization**, not a correctness fix. Tracked separately as OC-049.
- The rolling-upgrade guarantee should be described as **preserved upgrade + post-upgrade write compatibility**, not unconditional zero-downtime for the in-place rebuild step itself: `UpdateFrom3Async` drops indexes, widens columns and rewrites the table, and acknowledges a MySQL uniqueness window (PostgreSQL can also take disruptive DDL locks). Deployments applying that specific step should drain/maintenance-window it unless concurrent-load testing proves otherwise. Documentation follow-up tracked under OC-049.

**Verification.** Independent gpt-5.6 review returned verdict **WITHDRAW**, confirmed the migration-manager interpretation against the code, and ran the migration-safety suite (**90 tests, 0 failures**). No code changed.

---

# Phase 2 — Orchard Core Integration

### OC-004 — Provider registry is case-sensitive and swallows name collisions

- **Priority:** High · **Status:** Completed · **Category:** Extensibility/DI · **Effort:** S · **Risk:** Low · **Dependencies:** None

**Problem.** `TelephonyProviderOptions` (`Telephony.Abstractions/TelephonyProviderOptions.cs:10,34-38`) backs its registry with `private readonly Dictionary<string, TelephonyProviderTypeOptions> _providers = [];` — a default **case-sensitive** ordinal dictionary. `TryAddProvider` returns `this` silently when the key already exists.

**Why it matters.** `"Asterisk"` and `"asterisk"` become two distinct providers, producing tenant-resolution failures that are near-impossible to debug. A third-party module that collides with an existing technical name is silently discarded with no exception, no log, and no `false` return. `IProviderIdentityResolver.Canonicalize` mitigates alias drift at event ingress but does **not** protect the options registry.

**Recommended solution.** Initialize with `StringComparer.OrdinalIgnoreCase`; make collision observable (return `bool`, or throw at startup since registration happens during container build). Normalize/trim names and validate non-whitespace.

**Acceptance criteria.** Tests cover case-insensitive resolution and an observable collision outcome.

**Resolution (commit `b4aa374f`).** Backed `_providers` with `StringComparer.OrdinalIgnoreCase` and rebuilt the exposed `Providers` frozen dictionary with the same comparer (it was ordinal — a second latent bug affecting `DefaultTelephonyProviderResolver` lookups). Names are trimmed and validated via `ArgumentException.ThrowIfNullOrWhiteSpace`. Collisions are now observable: re-registering the identical provider `Type` is an idempotent no-op, while registering a **different** `Type` under an existing (case-insensitive) name throws `InvalidOperationException` at container/options build. `TryAddProvider` keeps its fluent `TelephonyProviderOptions` return type to avoid a breaking public-API change (Telephony.Abstractions is released on `main`); `ReplaceProvider` remains the intentional override path. Added tests for case-insensitive resolution, idempotent/collision semantics, whitespace trimming, and null-vs-whitespace argument validation (17/17 pass, 0 warnings). Documented in `telephony/custom-providers.md` and the `v2.0.0` changelog. Independently reviewed by gpt-5.6 (code-review agent): one Medium finding (null name should throw `ArgumentNullException` per repo convention) was applied and the change was then APPROVED.

---

### OC-005 — `ReconcileAsync` is a published contract that nothing ever invokes

- **Priority:** Medium · **Status:** Completed · **Category:** Lifecycle · **Effort:** S · **Risk:** Medium · **Dependencies:** None

**Problem.** `IContactCenterFeatureLifecycleParticipant.ReconcileAsync` is documented as reconciling feature state on shell activation, and `ContactCenterFeatureLifecycleCoordinator.ReconcileAsync` implements the fan-out — but `ContactCenterFeatureLifecycleHandler` overrides only `DisablingAsync`. No caller of the coordinator's `ReconcileAsync` exists in the repository. Four types implement it and none is reached. Relatedly `ContactCenterVoiceTenantEvents` is named for tenant events but is never registered as `IModularTenantEvents`.

**Why it matters.** This ships in the **Abstractions package**. A third-party voice provider will implement `ReconcileAsync` per the XML doc and silently receive no post-restart reconciliation. Internally the gap is masked by `ProviderCallStateReconciliationBackgroundTask`, so it will not surface in testing — it will surface as a provider that never recovers after a shell reload.

**Recommended solution.** Either wire it (`IModularTenantEvents.ActivatedAsync` or `FeatureEventHandler.EnabledAsync` resolving the coordinator), or delete `ReconcileAsync` from the interface and all four implementations. Do not ship a contract that lies.

**Resolution (chose deletion).** Deleted `ReconcileAsync` from the interface and all five implementations. Investigation showed every implementation only flipped an in-memory admission flag (`_workManager.Activate` / `_connectionRegistry.Activate`), which is redundant on a fresh shell — a re-enabled feature rebuilds the tenant shell, so the per-instance `ConcurrentDictionary` defaults to not-quiescing and the hub connection registry defaults active. The contract could not be safely wired at activation either: both a synchronous fan-out and a `ShellScope.AddDeferredTask` execute inside the nested activation scope whose `finally` awaits `BeforeDisposeAsync` **before** `IsActivated` is set (verified against Orchard `ShellScope.ActivateShellInternalAsync`), so a hung participant would block tenant startup. The genuine post-restart provider reconciliation is owned by `ProviderCallStateReconciliationBackgroundTask` → `ContactCenterVoiceLifecycleParticipant.ReconcileProviderStateAsync` (a real `IBackgroundTask`, gated on the work-admission gate, fully decoupled from activation) — which is retained. The interface is new on this unreleased branch, so removal is zero-breakage. Also deleted `ContactCenterFeatureLifecycleActivationHandler` and its `IModularTenantEvents` registration, and dropped both `ReconcileAsync` overloads plus the orphaned `ExecuteBestEffortAsync` from the coordinator (now quiesce + drain only). Updated the PublicApi baseline, tests, changelog, `production-support.md`, and the `feature-lifecycle-contracts.v1.json` ledger. Independently reviewed (gpt-5.6-sol): **APPROVE**. Commit `ea3225a1`.

---

### OC-006 — Provider modules register admin settings in their base feature

- **Priority:** Medium · **Status:** Completed · **Category:** Feature design · **Effort:** S · **Risk:** Low · **Dependencies:** None

**Problem.** `Asterisk/Startup.cs:101` and `DialPad/Startup.cs:39` call `AddSiteDisplayDriver<...SettingsDisplayDriver>()` in the un-attributed base `Startup`. Both drivers declare `SettingsGroupId => TelephonyConstants.SettingsGroupId`, whose menu entry is contributed only by the `Telephony Administration` feature — which neither provider depends on.

**Why it matters.** Two symmetric defects: (1) headless deployments enabling Asterisk get admin drivers they explicitly do not want — the exact scenario the `.Admin` split exists to prevent; (2) enabling Asterisk *without* Telephony Administration registers a settings editor with no navigation entry, reachable only by guessing the `groupId` URL.

**Recommended solution.** Move both calls into `[RequireFeatures(TelephonyConstants.Feature.Admin)]`-gated `AsteriskAdminStartup` / `DialPadAdminStartup` classes.

**Resolution.** Extracted `AddSiteDisplayDriver<AsteriskSettingsDisplayDriver>()` out of the Asterisk base `Startup` fluent chain and `AddSiteDisplayDriver<DialPadSettingsDisplayDriver>()` out of the DialPad base `Startup` into new `sealed` `AsteriskAdminStartup` / `DialPadAdminStartup` classes, each decorated with `[RequireFeatures(TelephonyConstants.Feature.Admin)]`. The provider settings drivers are now registered only when the Telephony Administration feature is enabled, so the settings tab and its `telephony` group navigation entry always appear together. Both modules build with 0 warnings.

---

### OC-007 — Logout hooked via tenant-wide middleware matched on hardcoded URLs

- **Priority:** Medium · **Status:** Not Started · **Category:** Authentication integration · **Effort:** M · **Risk:** Medium · **Dependencies:** None

**Problem.** `AvailabilityStartup.Configure` (`ContactCenter/Startup.cs:500-553`) installs `app.Use(...)` into the tenant pipeline; `IsLogoutRequest` matches only `POST /Users/Account/LogOff` and `POST /Users/Account/Logout`.

**Why it matters.** The delegate runs for **every** request on the tenant to evaluate two string comparisons, and silently misses every other way a session ends: OIDC/external sign-out, front-channel logout, cookie expiry, security-stamp invalidation, admin-initiated disable. An agent whose cookie expires stays `Available` and keeps receiving offers until the cleanup sweep, and their browser SIP credentials are never revoked. It also breaks if Orchard renames the account routes.

**Recommended solution.** Replace with `PostConfigure<CookieAuthenticationOptions>` chaining `OnSigningOut` (and `OnValidatePrincipal` for stamp rejection), executing presence sign-out and credential revocation inside a `ShellScope` child scope. Keep `AgentSessionCleanupBackgroundTask` as the backstop.

**Also fix here (trivial).** Line 509 resolves `ILogger<AgentsStartup>` inside `AvailabilityStartup` (wrong log category), and uses a fully-qualified `Microsoft.Extensions.Logging.ILogger<...>` despite the `using` at line 41.

---

### OC-008 — Six `.Admin` sub-features are feature-explosion

- **Priority:** Medium · **Status:** Not Started · **Category:** Feature design · **Effort:** M · **Risk:** Medium · **Dependencies:** Update `support-matrix.v1.json` + `feature-dependency-violations.v1.json` in the same change

**Problem.** The manifest declares 24 features. `ContactCenterRecordingAdminStartup` (`Startup.cs:407-419`) is an entire manifest feature whose body is a single `AddSiteDisplayDriver` call and which contributes no navigation. `DialerAdmin` and `EntryPointsAdmin` are two lines each. All six `.Admin` features depend on both `Admin` and their capability feature, so none can be enabled in isolation.

**Why it matters.** Each feature multiplies the state space that the activation tests, support matrix, dependency ledger and docs must cover, and the on/off combinations an operator must reason about. The headless justification is already satisfied by the single `Contact Center Administration` feature.

**Recommended solution.** Fold the five capability `.Admin` features into `Feature.Admin`, gating each registration with `[RequireFeatures(...)]` sibling startups. Manifest drops 24 → 19. **Retain** the genuine capability features (Agents, Availability, Queues, Routing, Voice, Dialer, Compliance, Recording, EntryPoints, RealTime, Analytics, Workflows) — those map to separately licensable capabilities and are correctly designed.

---

### OC-009 — No `placement.json` in any module

- **Priority:** Low · **Status:** Not Started · **Category:** Display management · **Effort:** S · **Risk:** Low · **Dependencies:** None

**Problem.** None of the four modules ships a `placement.json`; drivers hardcode `.Location("Content:1")`, `"Actions:5"`, `"Meta:5"`.

**Why it matters.** Integrators can still override from a theme, but a module-shipped `placement.json` documents the available slots and lets shape output be reordered or hidden without code — the standard Orchard extension point core modules provide.

---

### OC-010 — No setup/bootstrap recipe for a 24-feature module set

- **Priority:** Low (Enhancement) · **Status:** Not Started · **Category:** Recipes · **Effort:** M · **Risk:** Low · **Dependencies:** Follows OC-008

**Problem.** The only `.recipe.json` in scope is `Migrations/agent-state-reason-codes.recipe.json`. There is no module-level recipe enabling a coherent feature set with seed configuration.

**Why it matters.** First-run experience is a ~20-step manual checklist across 4 modules before a single call can route. All seven recipe steps needed already exist and are tested — they are simply not composed.

**Recommended solution.** Ship "Contact Center — Inbound Voice" and "Contact Center — Outbound Dialer" recipes with a `feature` step plus the existing config steps and starter data.

---

# Phase 3 — Extensibility Improvements

### OC-011 — `IContactCenterVoice*Provider` contracts sit in the orchestration layer, inverting the dependency

- **Priority:** High · **Status:** Won't Fix (premise misclassified) · **Category:** Module boundaries · **Effort:** L · **Risk:** Medium · **Dependencies:** None

**Problem.** Thirteen `IContactCenterVoice*` interfaces are defined in `ContactCenter.Abstractions`. `Asterisk.csproj` and `DialPad.csproj` therefore both take a `ProjectReference` on `ContactCenter.Abstractions` to implement them (`AsteriskContactCenterVoiceProvider.cs:18-25`).

**Why it matters.** The stated contract is *"Telephony contains provider-agnostic abstractions; provider modules implement those abstractions."* Today every telephony provider that wants Contact Center participation must compile against the orchestration layer, so orchestration changes can break provider builds and a third-party provider inherits an unwanted dependency.

**Balanced view.** This is defensible if "Contact Center voice provider" is considered a *distinct role* from "telephony provider" — the provider module genuinely plays two roles. The problem is that the roles are not physically separable today.

**Recommended solution (choose one).**
1. Move the neutral subset into `Telephony.Abstractions` (which `ContactCenter.Abstractions` already references, so no cycle).
2. Ship the Contact Center integration as separate optional modules (`CrestApps.OrchardCore.Asterisk.ContactCenter`), keeping the base provider pure.
3. Provide a generic adapter in `ContactCenter.Core` that elevates any `ITelephonyProvider` into an `IContactCenterVoiceProvider`, so CC participation requires no provider-side code at all.

**Acceptance criteria.** A pure telephony provider can be authored with a reference to `Telephony.Abstractions` only, and the public-API baseline is regenerated.

**Resolution (Won't Fix — architectural inversion not substantiated; independently reviewed, gpt-5.6-sol).** The finding is *misclassified* rather than a genuine defect. Evidence:
- The interfaces live in `ContactCenter.Abstractions` — the shared **contract/abstractions package**, which itself depends *downward* on `Telephony.Abstractions` (not vice-versa). A provider module implementing a host-defined extension port by referencing that contract package is **textbook dependency inversion** (the higher-level Contact Center policy owns the port; provider adapters implement it), identical to a module referencing `OrchardCore.*.Abstractions` to implement `IDisplayDriver`/`IPermissionProvider`. It is *not* an inversion of the intended layering. The repository already enforces this with `ContactCenterFeatureDependencyArchitectureTests` (providers may reference `ContactCenter.Abstractions` but never `ContactCenter.Core`/runtime).
- A **pure** telephony provider can already be authored against `Telephony.Abstractions` only — the base `Asterisk` (Area) feature's manifest depends solely on `TelephonyConstants.Feature.Area`, with zero Contact Center dependency, and `ITelephonyProvider` + ~30 sibling capability contracts live in `Telephony.Abstractions` with no project references. The acceptance criterion (new telephony-only provider needs only `Telephony.Abstractions`) is therefore already met.
- Contact Center integration is already isolated by feature: `AsteriskContactCenterVoiceStartup`/`AsteriskContactCenterMediaStartup` and `DialPadContactCenterStartup`/`DialerStartup` are `[Feature(...ContactCenterVoice/Media)]`-gated, and the manifest CC features declare the `ContactCenterConstants.Feature.Voice`/`VoiceMedia` dependency. Enabling only the base telephony feature activates no CC voice provider.
- Option 1 is infeasible: an exhaustive map shows every operational interface references `ContactCenter.Abstractions` domain types (`ContactCenterDialRequest`, `ContactCenterVoiceProviderResult`, `ContactCenterVoiceTransferRequest`, `ContactCenterVoiceMediaFrame`, …) and the implementers additionally use `IContactCenterFeatureWorkManager`/`IContactCenterFeatureWorkLease` and `ContactCenterConstants` metadata keys. Moving the interfaces down would drag CC orchestration concepts into the provider-agnostic layer — making the layering *worse*. Option 3 cannot synthesize the advanced CC semantics (recording/monitoring/attended-transfer/conference/media-session). Option 2 (separate assemblies) is a pure packaging change whose substantial refactoring cost is not justified for an unreleased module.
- **Residual (conceded):** the combined `Asterisk.dll`/`DialPad.dll` assemblies do retain a *compile-time* `ProjectReference` to `ContactCenter.Abstractions` (a lightweight contract package) because the CC-integration features share the assembly, and the manifests reference `ContactCenterConstants`. `[Feature]` gating removes runtime activation, not the assembly-level dependency. This co-installation of a contract package is intentional and acceptable; physical package separation may be revisited later as a packaging enhancement, not a High-severity layering defect. Fixed two `.csproj` comments (`Asterisk.csproj`, `DialPad.csproj`) that had falsely claimed the provider code depends *only* on Telephony abstractions.

---

### OC-012 — No abstract base classes for the provider contracts (versioning hazard)

- **Priority:** High · **Status:** Won't Fix (premise disproven) · **Category:** Public API · **Effort:** S · **Risk:** Low · **Dependencies:** OC-011

**Problem.** `ITelephonyProvider` and `IContactCenterVoiceProvider` are exposed only as raw interfaces with no `TelephonyProviderBase` / `ContactCenterVoiceProviderBase` to inherit from.

**Why it matters.** Adding a single member in a minor release is a hard compile break for every third-party provider. Orchard's own pattern uses abstract base classes (e.g. `ContentPartDisplayDriver`) as expansion joints.

**Recommended solution.** Introduce abstract bases implementing the interfaces with virtual members; document them as the supported extension point.

**Resolution (Won't Fix — premise disproven; corroborated by the independent OC-011 review, gpt-5.6-sol).** The design already provides a *better* expansion joint than base classes: capabilities are intentionally **interface-segregated (ISP)**. `ITelephonyProvider`/`IContactCenterVoiceProvider` are minimal *identity* contracts (`Name`, `Capabilities`, and for voice `TechnicalName`/`DeliveryModel`) whose XML docs explicitly state *"Executable operations live on the separate capability contracts a provider chooses to implement, so a provider is never obliged to answer for an operation it cannot perform."* New capabilities are therefore added as **new** small interfaces (`IContactCenterVoice*Provider`, `ITelephony*Provider`) — inherently non-breaking to existing implementers — rather than as new members on an existing interface. Consequently:
- The identity interfaces have no optional/defaultable members a base class could usefully virtualize (`Name`/`TechnicalName`/`Capabilities`/`DeliveryModel` are all provider-specific with no sensible default).
- A monolithic base implementing all thirteen capability interfaces would **break capability detection**: the runtime resolves optional capabilities via `provider is IContactCenterVoiceCallControlProvider` (e.g. `AnswerProviderCommandTypeExecutor.cs:171`, `VoiceContactCenterCallRouter.cs:44`) and `provider.Capabilities.HasFlag(...)`. A base that makes every provider satisfy every `is`-check would make providers advertise operations they cannot perform — the exact failure the documented contract prevents.
- An identity-only base (`IContactCenterVoiceProvider`/`ITelephonyProvider` alone) is possible but adds no value today, since those interfaces are already minimal and stable, and evolving through new ISP capability interfaces remains a valid, non-breaking versioning strategy. Not warranted for an unreleased module.

---

### OC-013 — Internal implementation details published via `ContactCenterConstants`

- **Priority:** Medium · **Status:** Completed · **Category:** Public API · **Effort:** M · **Risk:** Low · **Dependencies:** Regenerate public-API baseline

**Problem.** An 819-line `ContactCenterConstants` in the public Abstractions package exposes YesSql `CollectionName`, `CurrentEventSchemaVersion`, projection checkpoint IDs and similar internals.

**Why it matters.** Incrementing an internal projection version forces a public-package version bump and churns downstream consumers who only needed the webhook interfaces.

**Recommended solution.** Keep feature names, permissions and claim types public; move storage/schema/projection constants to `internal static` in `ContactCenter.Core`. Split the file by domain.

**Resolution.** Moved five storage/schema/projection scalars (`CollectionName`, `CurrentEventSchemaVersion`, `MetricsProjectionHandlerId`, `MetricsProjectionVersion`, `ProviderNameLength`) out of the public `ContactCenterConstants` in the Abstractions package into a new `internal static ContactCenterStorage` class in `ContactCenter.Core` (same `CrestApps.OrchardCore.ContactCenter` namespace, so references resolve without new usings). The manual-call aggregate-type discriminator stayed public — it is emitted as `InteractionEvent.AggregateType` on the published `ManualDialSuppressed` event and forms part of the event contract webhook/workflow consumers may inspect — and was relocated into a new public `ContactCenterConstants.AggregateTypes` group (`ManualCall`). Exposed the Core internals to the module and distributed-test assemblies via `InternalsVisibleTo`, then mechanically repointed all ~430 references across Core/Module/Tests/DistributedTests. `SystemActor` and the diagnostic `Components` taxonomy also stay public. Regenerated the `CrestApps.OrchardCore.ContactCenter.Abstractions` and `.Core` public-API baselines to reflect the reduced surface and the two new `InternalsVisibleTo` grants. Incrementing a projection version now no longer forces a public-package bump. Domain file-splitting of the remaining public constants is tracked separately by OC-047. Also corrected a pre-existing `ContactCenterFeatureDependencyArchitectureTests` `.Single()` ambiguity introduced by OC-006's `AsteriskAdminStartup` (disambiguated the base-feature startup by `RequiredFeatureIds.Count == 0`).

---

### OC-014 — Workflows integration is read-only

- **Priority:** Low (Enhancement) · **Status:** Not Started · **Category:** Workflows · **Effort:** L · **Risk:** Low · **Dependencies:** Benefits from a canonical event-type registry

**Problem.** The Workflows feature registers exactly one `EventActivity` (`ContactCenterEvent`) and no `TaskActivity` implementations. `ContactCenterEvent.EventType` is free text with no picker.

**Why it matters.** Workflows can observe the contact center but cannot act on it. "On abandoned call create a callback", "on SLA breach notify a supervisor", "after wrap-up set presence" all require C#. This is the single biggest missed opportunity to make the module extensible without code — Orchard's core value proposition.

**Recommended solution.** Add `TaskActivity` implementations for enqueue activity, assign to agent, set presence, create callback, transfer call, start/stop recording. Back `EventType` with explicit `S["..."]` `SelectListItem` entries per the localization-extraction rule.

---

### OC-015 — Optional cross-feature dependencies hidden behind `IEnumerable<T>` + `FirstOrDefault()`

- **Priority:** Low · **Status:** Not Started · **Category:** DI hygiene · **Effort:** M · **Risk:** Medium · **Dependencies:** None

**Problem.** Feature-conditional services are injected as `IEnumerable<TService>` and reduced with `.FirstOrDefault()`, then null-checked per use site (`ContactCenterCallCommandService.cs:54-55,68-69`, `AgentPresenceManagerService.cs:46-47`, and others).

**Why it matters.** The dependency is invisible in the constructor signature and easy to omit in new code paths. **Verified mitigating fact:** the highest-risk instance (Compliance disabled while Dialer enabled) degrades safely — `ContactCenterCallCommandService.cs:98-111` falls through to the accept-only path rather than dialing ungated. So this is maintainability, not a live correctness defect.

**Recommended solution.** Split feature-gated consumers so the dependency becomes required, or register a null-object default via `TryAddScoped`.

---

# Phase 4 — UI & Display Management

### OC-016 — Agent workspace and supervisor dashboard are inaccessible

- **Priority:** High · **Status:** Completed · **Category:** Accessibility · **Effort:** M · **Risk:** Low · **Dependencies:** None

**Problem.** Verified counts: `Views/AgentWorkspace/Index.cshtml` and `Views/SupervisorDashboard/Index.cshtml` contain **0** `aria-*`/`role` attributes, and there are **0** `aria-live`/`role="status"`/`role="alert"` occurrences across all four ContactCenter scripts. Offers, presence, queue depth and the active-call panel are injected via `innerHTML` into containers with no live-region semantics. The presence menu is a `div`+`button` with no `role="menu"`, `aria-expanded`, Escape handling, or focus management. Error feedback uses `window.alert()`.

**Why it matters.** This is a softphone/agent desktop operated all day. A blind or low-vision agent receives no announcement that a call is ringing or that a response is required within N seconds. By contrast the admin CRUD views do carry baseline Bootstrap ARIA, so the gap is specific to the real-time surfaces.

**Documentation discrepancy — must be corrected.** `.github/contact-center/PRODUCTION-READINESS.md` lists *"Agent desktop accessibility (W6): ARIA/live-region/keyboard/degraded-state work on the agent workspace"* under **completed** work. The code does not support that claim. Correct the record as part of this item.

**Recommended solution.** `role="alert"`/`aria-live="assertive"` on the offer container; `aria-live="polite"` on presence/queue/active regions; convert the presence menu to a proper ARIA menu (or a Bootstrap dropdown that ships the semantics); move focus to Accept when an offer renders; surface disconnected/reconnecting state visibly and via live region; replace `window.alert()` with status semantics.

**Acceptance criteria.** Keyboard-only operation of accept/decline/presence; automated axe scan clean on both views.

**Resolution.** Both real-time surfaces are now accessible. **Agent workspace** (`AgentWorkspace/Index.cshtml` + `agent-workspace.js`): the incoming-offer container is `role="alert" aria-live="assertive" aria-atomic="true"` and moves keyboard focus to the **Accept** button when a new offer renders; the active-interaction, queue-chip regions are `aria-live="polite"` with `aria-label`/`aria-labelledby`; the per-second countdown and talk-time nodes are `aria-hidden="true"` so live regions announce state changes once rather than ticking every second; the presence control is a real ARIA menu — the trigger carries `aria-haspopup="menu"`/`aria-expanded`/`aria-controls`, the menu is `role="menu"` with `role="menuitem"` children, and JS wires open-on-click with focus to the first item, Arrow/Home/End roving focus, Escape-to-close with focus return, and click-away close. **Supervisor dashboard** (`SupervisorDashboard/Index.cshtml` + `supervisor-dashboard.js`): summary/tiles/board regions are `aria-live="polite"` with labels. **Both surfaces** replace `window.alert()` with a non-blocking inline `role="alert"` error region and add a `role="status" aria-live="polite"` connection indicator driven by new lifecycle callbacks (`onConnected`/`onReconnecting`/`onReconnected`/`onDisconnected`) surfaced from the shared `contact-center-realtime.js` helper (which now hooks `connection.onreconnecting` and reports connect/close transitions), so agents are told when live updates pause. New localized `strings` (connected/reconnecting/disconnected, presence-menu label) flow through the existing config dictionaries. SCSS adds `.cc-connection`/`.cc-error`/`.cc-dashboard__topbar` styling; `npm run rebuild` regenerated the minified assets. The false "completed" claim in `.github/contact-center/PRODUCTION-READINESS.md` was corrected to describe the actually-delivered agent-and-supervisor accessibility work. Module builds with 0 warnings.

### OC-017 — Seven near-identical catalog list views

- **Priority:** Medium · **Status:** Not Started · **Category:** UI duplication · **Effort:** M · **Risk:** Medium · **Dependencies:** None

**Problem.** `Views/{Queues,Skills,EntryPoints,DialerProfiles,QueueGroups,AgentStateReasonCodes,BusinessHoursCalendars}/Index.cshtml` are ~57 lines each and structurally identical, differing only by title and add-label.

**Recommended solution.** Extract a shared `_CatalogList` partial/shape taking title, add-label and `ListCatalogEntryViewModel<T>`; keep per-type differences in the already-used item shape.

---

### OC-018 — Real-time views are not shape-composable

- **Priority:** Low · **Status:** Not Started · **Category:** Display management · **Effort:** L · **Risk:** Medium · **Dependencies:** OC-009

**Problem.** `AgentWorkspace/Index.cshtml`, `SupervisorDashboard/Index.cshtml` and `Items/ContactCenterSoftPhoneWork.View.cshtml` are monolithic controller-rendered pages, so a theme cannot override sub-regions (topbar, offer, panels) via alternates or placement. Defensible for a bespoke SPA-like page, but it is the module's least extensible UI.

---

# Phase 5 — Resource Management

### OC-019 — ContactCenter assets bypass the Gulp pipeline and the min/debug resource convention

- **Priority:** High · **Status:** Completed · **Category:** Resource management · **Effort:** S · **Risk:** Low · **Dependencies:** None

**Problem.** Verified: ContactCenter has **no** `Assets/`, **no** `Assets.json`, **no** `package.json`. Five hand-authored files (~2,000 lines) live directly in `wwwroot/` — `scripts/{contact-center-realtime,agent-workspace,supervisor-dashboard,contact-center-soft-phone}.js` and `styles/contact-center-workspace.css` — with no minified variants. Both resource configurations use the single-argument `SetUrl(...)` pointing at unminified files. The sibling Telephony module does it correctly (`Assets.json` + `SetUrl(min, debug)` + `.min` outputs).

**Why it matters.** Production tenants download unminified, source-map-free JS/CSS for the two highest-traffic authenticated pages in the product. `npm run rebuild` never touches these files, so the documented pre-commit asset check silently does not cover them.

**Recommended solution.** Move sources to `Assets/js` and `Assets/scss`, add `Assets.json` mirroring Telephony, run `npm run rebuild`, switch to the two-argument `SetUrl` overload, and use `SetVersion` values that change with content.

**Acceptance criteria.** `npm run rebuild` regenerates all ContactCenter assets; `git status` is clean afterwards; production serves `.min` variants.

**Resolution.** Moved the four scripts to `Assets/js` and the stylesheet to `Assets/scss` (`.css` → `.scss`), added `Assets.json` mirroring the Telephony module, and ran `npm run rebuild` to regenerate `wwwroot/scripts/*.js` + `*.min.js` and `wwwroot/styles/*.css` + `*.min.css`. Both resource configurations now use the two-argument `SetUrl(min, debug)` overload so production serves the minified variant. Module builds with 0 warnings.

---

### OC-020 — Inline script in a settings view hardcodes English strings

- **Priority:** Medium · **Status:** Completed · **Category:** Resource management / localization · **Effort:** S · **Risk:** Low · **Dependencies:** OC-019

**Problem.** `ContactCenterExternalTransferSettings.Edit.cshtml:65-122` contains a ~57-line inline `<script>` with no `at="Foot"`. Server-rendered rows use `@T["Display name"]`/`@T["Remove destination"]`, but the JS row template hardcodes `placeholder="Display name"` and `title="Remove destination"`, so dynamically added rows are always English and the strings are invisible to extraction.

**Recommended solution.** Move to a registered `DefineScript` resource and pass a `strings` config object, as `AgentWorkspace`/`SupervisorDashboard` already do correctly.

**Scope note.** Only **3** of 101 views contain true inline `<script>` blocks; the other two (`AsteriskSettings.Edit.cshtml`, `DialPadSettings.Edit.cshtml`) use `at="Foot"` for ~20 lines of view glue and are acceptable. All 5 `<style>` usages are correct `<style asp-name>` forms, and the single inline `style=` is a justified dynamic CSS variable.

**Resolution.** Extracted the inline logic into a new Gulp-built asset `Assets/js/contact-center-external-transfer-settings.js` (output to `wwwroot/scripts`, minified), registered as the named resource `contact-center-external-transfer-settings` through a new `ContactCenterExternalTransferResourceConfiguration : IConfigureOptions<ResourceManagementOptions>` added to the **Contact Center Administration** feature's startup (matching the feature that registers the settings driver). The view now requires the script with `<script asp-name="contact-center-external-transfer-settings" at="Foot"></script>` and serializes a `strings` config object (`displayName`, `removeDestination`) into a `data-config` attribute on the wrapper — mirroring the `AgentWorkspace`/`SupervisorDashboard` pattern — and the JS reads those localized strings so dynamically added rows honor the active culture and the strings are visible to extraction. The dynamic-row template also HTML-attribute-encodes the injected strings. Module builds 0 warnings; assets rebuilt with `gulp rebuild`.

---

### OC-021 — Duplicated JS helpers and a duplicated call-state enum

- **Priority:** Medium · **Status:** Not Started · **Category:** DRY · **Effort:** M · **Risk:** Low · **Dependencies:** OC-019

**Problem.** `escapeHtml` is copy-pasted across four files; the array `['Idle','Connecting','Ringing','Connected','OnHold','Disconnected','Failed']` — which must stay in sync with the C# `CallState` enum — is duplicated between `contact-center-soft-phone.js` and `Telephony/soft-phone.js`.

**Why it matters.** State-enum drift between JS and C# is a live bug risk.

**Recommended solution.** Export `escapeHtml`, `formatDuration` and `STATE_NAMES` once from the shared telephony/realtime module; ideally emit the state names from the server so the C# enum stays authoritative.

---

# Phase 6 — Performance & Scalability

### OC-022 — Routing repeatedly materializes entire queue backlogs

- **Priority:** High · **Status:** Completed · **Category:** Performance · **Effort:** L · **Risk:** High · **Dependencies:** YesSql index/query changes

**Problem.** Verified: `QueueItemStore.ListWaitingAsync` (`ContactCenter.Core/Services/QueueItemStore.cs:31-44`) queries all waiting items for a queue with `.ListAsync()` and no pagination or limit, then `.ToArray()`. Assignment and offer loops call it repeatedly after individual state changes (`ActivityAssignmentService.cs:127,167`, `ReservationExpiryBackgroundTask.cs:131,165`).

**Why it matters.** A queue spike produces roughly quadratic query traffic and allocations precisely when assignment latency matters most.

**Recommended solution.** Query only the next eligible indexed item or a bounded ordered page; reuse one batch per cycle; maintain queue depth as a separate aggregate.

**Resolution.** The three single-item hot paths that materialized the entire waiting backlog only to pick one item now use a bounded top-one query. Added `IQueueItemStore.FindNextWaitingAsync` (a `FirstOrDefaultAsync` with the exact `Priority` desc → `EnqueuedUtc` asc ordering of `ListWaitingAsync`) and `IQueueItemManager.FindNextWaitingAsync(ActivityQueue queue, DateTime utcNow, …)`. The manager selects a fast path when the queue does not apply SLA aging — where an item's effective priority equals its base priority, so the store's first row is provably identical to `QueueItemPrioritizer.SelectNext` without materializing the backlog — and falls back to the full in-memory scan only for queues that opt into SLA aging (aging can reorder items by wait time, so all candidates must be scored). The assignment loop (`ActivityAssignmentService`) and both voice/generic offer paths (`ReservationExpiryBackgroundTask`) now call `FindNextWaitingAsync`; `ListWaitingAsync` is retained for the aging fallback and for `OverflowDueAsync`, which legitimately iterates the whole backlog. Added `QueueItemManagerTests` proving the fast path calls only the bounded store query and the aging path scores the backlog (an aged low-priority item beats a newer highest-priority item). Independently reviewed (gpt-5.6-sol) and approved; all 1483 ContactCenter tests pass.

---

### OC-023 — Supervisor dashboard polling generates N+1 queries

- **Priority:** High · **Status:** Completed · **Category:** Performance · **Effort:** L · **Risk:** Medium · **Dependencies:** Reporting read-model indexes

**Problem.** Every 10 seconds (`supervisor-dashboard.js:12,258`) the endpoint performs several sequential queries per queue and per agent, including repeated authorization and user-display-name resolution (`SupervisorDashboardEndpoints.cs:74-97,119-144,249`).

**Why it matters.** A few hundred agents can generate thousands of queries per minute per supervisor, saturating the database and increasing routing latency.

**Recommended solution.** Aggregated/batched read models, batched authorization and user resolution, a fixed page size, and coalesced/cached identical polls.

**Resolution.** The per-agent N+1 — the dominant cost, since agents greatly outnumber queues — was eliminated. Waiting depth per authorized queue is now read with the existing batched `IQueueItemManager.CountWaitingByQueueIdsAsync` (one query for all queues). Agent load is resolved with three whole-set batches instead of three queries per agent: active interactions via a new index-backed `IInteractionManager.ListActiveByAgentIdsAsync` (chunked `.IsIn` YesSql query, keeping the most recent by `CreatedUtc` per agent), active counts via the existing `CountActiveByAgentIdsAsync`, and display names by bulk-loading users with `session.Query<User, UserIndex>(x => x.UserId.IsIn(chunk))` and feeding the already-materialized user to `IDisplayNameProvider.GetAsync` (which performs no further database access). Supervisor queue authorization — which reloads the same supervisor profile on every call — is now memoized per queue for the request, so the per-agent monitoring gate no longer reissues the supervisor lookup for each busy agent. Monitoring-mode resolution takes a new `IContactCenterMonitoringService.GetAvailableModesAsync(Interaction)` overload that reuses the already-batched interaction instead of reloading it through `FindByIdAsync`. No new raw SQL was introduced, so the query-plan budget gate is untouched. Remaining per-queue longest-wait/SLA reads are bounded residuals (queues ≪ agents) and are documented as such. Covered by `AvailabilityStoreSharedDatabaseTests.InteractionStore_ListActiveByAgentIds_ReturnsOnlyActiveInteractionsAcrossBatches`, `ContactCenterRecordingAndMonitoringTests.GetAvailableModesAsync_WithMaterializedInteraction_ResolvesModesWithoutReloading`, and the updated public-API baseline.

---

### OC-024 — Recording ingestion buffers whole files multiple times

- **Priority:** High · **Status:** Completed · **Category:** Memory · **Effort:** L · **Risk:** High · **Dependencies:** Recording storage format + migration

**Problem.** Verified: `LocalEncryptedRecordingMediaStore.StoreAsync` calls `_protector.Protect(request.Content)` on a full `byte[]` then wraps it in a `MemoryStream`; `OpenReadAsync` copies the file into a `MemoryStream`, calls `.ToArray()`, `Unprotect`s the whole array, and returns another `MemoryStream`. `AsteriskAriClient.cs:491` downloads as a byte array.

**Why it matters.** A single long recording consumes several times its size in managed memory, causing LOH pressure or OOM under concurrent ingestion.

**Recommended solution.** Streaming download plus chunked authenticated encryption, or storage-native encryption; expose streaming read/write APIs instead of `byte[]`.

**Resolution.** Introduced a streaming chunked-AEAD container (`RecordingMediaCryptoFormat` + `ChunkedAeadEncryptingReadStream`/`ChunkedAeadDecryptingReadStream`) using envelope encryption: a per-recording random AES-256-GCM data key encrypts the media as a sequence of independently authenticated 64 KiB frames, and that data key is wrapped by the data-protection provider (so key management — tenant isolation, rotation — stays with data protection while bulk media streams a fixed chunk at a time). Every frame binds its ordinal counter, length, and an end-of-stream marker into the AES-GCM associated data, so tampering, reordering, and truncation are rejected on read (surfaced as `CryptographicException`). `RecordingMediaWriteRequest.Content` changed from `byte[]` to `Stream`; `LocalEncryptedRecordingMediaStore` now streams straight through `IFileStore.CreateFileFromStreamAsync`/`GetFileStreamAsync`, so a recording is never buffered whole in memory in either direction. On the Asterisk side, `DownloadStoredRecordingAsync` now uses `HttpCompletionOption.ResponseHeadersRead` and returns an owning `AsteriskAriStoredRecordingContent` (holds the open response) whose stream is `await using`-scoped across the store call in `AsteriskRecordingIngestService`. Because recording is off by default and the branch is unmerged there is no on-disk migration burden. Added multi-chunk round-trip, empty-recording, tamper, and truncation tests; updated the public API baseline. Independently reviewed (gpt-5.6) and approved.

---

### OC-025 — Unbounded reporting and reservation-cleanup materialization

- **Priority:** Medium · **Status:** Completed · **Category:** Performance · **Effort:** L · **Risk:** Medium · **Dependencies:** Reporting indexes

**Problem.** Reporting materializes complete date-range result sets before in-memory grouping with no maximum range or pagination (`ContactCenterReportingService.cs:713-750`, `EnterpriseInteractionReportProvider.cs:105-108,1222-1225`). Expired-reservation cleanup loads every expired pending reservation before processing (`ActivityReservationStore.cs:28-35`).

**Recommended solution.** Enforce an interactive range limit and aggregate indexes; page/stream exports. For cleanup, read bounded pages ordered by expiry and process to a deadline.

**Resolution.** Both unbounded materializations are now bounded.

*Reservation cleanup* — `IActivityReservationStore.ListExpiredAsync` / `IActivityReservationManager.ListExpiredAsync` now take a keyset cursor (`afterExpiresUtc`, `afterDocumentId`) plus a `maxResults` bound and return an `ExpiredReservationPage` (the page of reservations plus the cursor for the next page). The store queries the `ActivityReservationIndex` ordered by `ExpiresUtc` then `DocumentId` with the keyset predicate `ExpiresUtc > cursor || (ExpiresUtc == cursor && DocumentId > afterDocumentId)`, then loads the page documents by id — so a page is a fixed, oldest-first slice rather than the entire expiry backlog. `ActivityReservationService.ExpireDueAsync` drains in bounded `ExpiryPageSize` (100) pages using **keyset (seek) paging**: each page advances the cursor past the last row it observed, whether that row was expired here or is currently locked by another node. Because the cursor is an absolute position in the `(ExpiresUtc, DocumentId)` key space rather than a numeric offset, concurrent expirations or insertions elsewhere in the backlog never shift the window, so a live reservation is never skipped and a locked oldest page never starves the drainable reservations behind it. Candidates that could not be processed this run (locked, or already changed) are retried on the next scheduled sweep, which restarts from the oldest expired reservation. Draining stops on a short/empty page or cancellation. New unit tests cover multi-page draining and the anti-starvation case where the oldest page is fully locked, plus a real-store integration test asserting the keyset query pages in stable order against SQLite. Keyset was chosen over offset paging because offset paging over a concurrently-mutating set can still skip live rows when locked candidates are expired by their owner between pages.

*Reporting* — silently trimming rows would corrupt the aggregate totals, so instead of a row cap the reporting paths now **fail fast** on an over-wide window. A new `ContactCenterReportingOptions.MaximumReportRange` (Options pattern, bound from `CrestApps_ContactCenter:Reporting`, default 400 days, `ValidateOnStart`) is enforced by the shared `ContactCenterReportingService.EnsureRangeWithinLimit(...)` guard at the top of every query helper (`QueryInteractionsAsync`, `QueryActivityIndexesAsync`) and in `EnterpriseInteractionReportProvider` before it queries, so every report path enforces the same bound before any rows are read. The 400-day default comfortably covers the built-in day/week/month/quarter/year presets. Tests cover the guard boundary.

*Deferred (documented follow-up, not a regression of this item):* pre-aggregated rollup indexes and streaming/paged CSV export remain an enhancement — the fixes here bound worst-case materialization but still build the in-memory aggregates per request. `AgentWorkforceReportProvider` reads the event store up to `ToUtc` only (it ignores `FromUtc` as a lower bound); tightening that is a separate event-store concern tracked outside OC-025.

---

### OC-026 — Routing-hot configuration is re-queried on every decision; no caching or `ISignal` invalidation

- **Priority:** Medium · **Status:** Not Started · **Category:** Performance · **Effort:** M · **Risk:** Medium · **Dependencies:** None

**Problem.** Queues, skills, business-hours calendars and queue groups are small, slowly-changing configuration read on the routing path, but every read is a fresh `Session.Query` (`ActivityQueueStore.ListEnabledAsync`). A grep across all eight in-scope projects returns **0** hits for `ISignal`, `IMemoryCache`, `IDistributedCache` and `IDocumentManager`. `ISiteService` *is* used correctly for site settings, so the cached-settings path is understood — it just was not extended to catalog entities.

**Orchard Core pattern violated.** `IDocumentManager<TDocument>` with `ISignal.SignalToken` invalidation is Orchard's purpose-built mechanism for exactly this data shape, and is already tenant- and distributed-cache aware.

**Recommended solution.** Cached read models for the four entities, invalidated from the existing `ICatalogEntryHandler<T>` implementations, which already fire on every write (including recipe imports).

---

### OC-027 — Latency-critical work scheduled on one-minute cron background tasks

- **Priority:** Medium · **Status:** Not Started · **Category:** Scheduling · **Effort:** L · **Risk:** High · **Dependencies:** Distributed-lock story (OC-028)

**Problem.** `IActivityReservationService.ExpireDueAsync` has exactly one caller — a `Schedule = "* * * * *"` task. Queues configure `ReservationTimeoutSeconds` (seconds-scale), but expiry acts at minute granularity. Eleven ContactCenter tasks plus three Asterisk/Telephony tasks all run every minute, each taking a distributed lock.

**Why it matters.** An offer configured to expire after 15 seconds is reclaimed up to ~75 seconds later, holding agent capacity while the caller waits. Eleven per-tenant tasks per minute is an 11× scheduler load that scales linearly with tenant count, since Orchard's `ModularBackgroundService` walks tenants sequentially.

**Recommended solution.** Keep the cron tasks as the safety net — they are well written (bounded run budgets, linked CTS, lock-expiration reasoning). Add event-driven triggers for latency-sensitive paths (deadline-scheduled expiry, outbox dispatch driven from the appending commit) and consolidate the eleven tasks into fewer dispatchers.

---

### OC-028 — Scheduler leases can expire while work continues

- **Priority:** High · **Status:** Completed · **Category:** Distributed correctness · **Effort:** M · **Risk:** Medium · **Dependencies:** None

**Problem.** `DialerPacingBackgroundTask` holds a 60-second lock while profiles and outbound attempts execute sequentially with no matching run deadline or lease renewal (`DialerPacingBackgroundTask.cs:10-15,24-43`, `DialerStrategyBase.cs:43-64`). `AgentAvailabilityRecoveryBackgroundTask` has the same shape with a one-minute lock.

**Why it matters.** A second node can begin an overlapping pacing cycle while the first still runs, so per-invocation pacing no longer constrains aggregate call rate — producing over-reservation or a call burst, and racing agent state transitions.

**Recommended solution.** Renewable per-profile leases or a strict execution deadline shorter than lock expiry; stop immediately when the budget expires.

**Resolution.** Both tasks now adopt the same bounded-run pattern already proven in `ReservationExpiryBackgroundTask`: the distributed-lock expiration was raised from 60s to `LockExpiration = 120_000` (twice the one-minute schedule) and each run is bounded to a `MaxRunDurationMilliseconds = 90_000` wall-clock budget that is strictly below the lock expiration, so a run always finishes before its lock can expire and a second node can therefore never start an overlapping run. `DialerPacingBackgroundTask` enforces the budget both with a hard `CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)` + `CancelAfter` that cancels in-flight `RunCycleAsync` work and with a between-profile `IClock.UtcNow` deadline check that defers the remaining profiles to the next tick; `AgentAvailabilityRecoveryBackgroundTask` (a single recovery pass) enforces it with the linked `CancelAfter` token. Both distinguish shutdown cancellation (rethrown so the lease is released promptly) from budget cancellation (logged at Debug and deferred). Covered by new/updated unit tests: reflection-based metadata ordering guards (`schedule < 2× ≤ lock-expiration`, `run-budget < lock-expiration`, `lock-expiration > lock-timeout`), a clock-advance budget-defer test for the pacing task, quiescence and shutdown-propagation tests, and an assertion that the recovery pass runs under the budgeted (linked, cancelable) token rather than the raw shutdown token. Independent gpt-5.6-sol review: APPROVE.

---

# Phase 7 — Security

### OC-029 — Unsafe HTTP retries on non-idempotent operations

- **Priority:** High · **Status:** Completed · **Category:** Resilience/correctness · **Effort:** M · **Risk:** Medium · **Dependencies:** Provider idempotency contracts

**Problem.** Both providers install standard resilience retry pipelines without excluding unsafe methods (`Asterisk/Startup.cs:51-56`, `DialPad/Startup.cs:22-27`), and those clients carry call-origination POSTs and OAuth authorization-code/refresh-token POSTs.

**Why it matters.** A lost response can place a **second outbound call**. Retrying a one-time authorization code or a rotating refresh token yields `invalid_grant` after the first request actually succeeded.

**Recommended solution.** Separate clients/pipelines by operation type; disable retries for unsafe methods unless the provider guarantees idempotency via a deterministic key; never auto-replay ambiguous OAuth grant requests.

**Resolution.** Both provider resilience pipelines now call the framework-provided `options.Retry.DisableForUnsafeHttpMethods()` (from `Microsoft.Extensions.Http.Resilience`) inside their `AddStandardResilienceHandler` retry configuration. This excludes POST/PATCH/PUT/DELETE/CONNECT from automatic replay while preserving retries for idempotent safe methods (status GETs). Neither provider exposes a deterministic idempotency key, so retrying unsafe methods (ARI call-origination, PJSIP credential mutations, DialPad call-origination, and OAuth authorization-code/refresh-token POSTs) is unsound; disabling replay is the correct fail-closed behavior. A source-scanning architecture guard test (`ProviderHttpRetryArchitectureTests`) dynamically discovers **every** source file under `src` that installs the standard resilience handler and asserts each also disables unsafe-method retries — the guard is not limited to a hardcoded provider list, so a future client cannot regress the rule. That dynamic guard surfaced three additional clients (the AbstractAPI, Veriphone, and Twilio Lookup phone-number-verification providers); although those are GET-only lookups today, the same disable call was applied so a future POST cannot silently gain unsafe-retry behavior. Independent gpt-5.6-sol review: initial REQUEST-CHANGES (guard was hardcoded to two files) applied by making the guard dynamic; re-review APPROVE.

---

### OC-030 — OAuth token persistence failures reported as success

- **Priority:** High · **Status:** Completed · **Category:** Correctness/security · **Effort:** S · **Risk:** Low · **Dependencies:** None

**Problem.** `UserManager.UpdateAsync()` returns an `IdentityResult` that is discarded (`DefaultTelephonyUserAccessor.cs:43-46`, `DefaultTelephonyUserTokenStore.cs:72,94`).

**Why it matters.** Validation/concurrency/storage failures do not necessarily throw, so OAuth connect or refresh can report success while replacement tokens were never persisted.

**Recommended solution.** Inspect the result, propagate a typed failure with redacted errors, and report success only after persistence succeeds.

**Resolution.** `DefaultTelephonyUserAccessor.UpdateUserAsync` now inspects the `IdentityResult`, logs only the identity error **codes** (descriptions can carry usernames/emails, so they are never logged), and throws an internal `TelephonyUserPersistenceException` whose message carries codes only. `DefaultTelephonyUserTokenStore.StoreAsync` now throws the same exception when there is no persistable current user rather than silently no-op-ing. `CompleteAuthorizationAsync` converts the exception into `TelephonyResult.Failed(...)` so connect reports success only after persistence succeeds; `GetStatusAsync` degrades to `IsConnected = false` on a refresh-persist failure so the status probe cannot fault; disconnect/refresh surface the failure loudly. Covered by `DefaultTelephonyUserAccessorTests`. Independent gpt-5.6-sol review: two follow-up findings (token-store silent skip, PII in logs) applied and re-confirmed.

---

### OC-031 — Concurrent OAuth refreshes are not serialized

- **Priority:** High · **Status:** Completed · **Category:** Correctness/security · **Effort:** M · **Risk:** Medium · **Dependencies:** Distributed lock

**Problem.** `DefaultTelephonyAuthenticationService.cs:183-223` lets multiple requests read the same expiring token, refresh concurrently, and overwrite stored credentials with no lock or compare-and-swap.

**Why it matters.** Providers using refresh-token rotation invalidate the old token on first use, so concurrent refreshes fail intermittently or lose the only valid replacement token.

**Recommended solution.** Distributed tenant/user/provider lock; re-read inside the lock; refresh once; persist with concurrency checking.

**Resolution.** Added `TokenRefreshLockTimeout` (10s) / `TokenRefreshLockExpiration` (60s) to `TelephonyCoordinationOptions`, validated at startup so the lease must exceed the wait window, and a per-user+provider distributed lock (`Telephony:TokenRefresh:{provider}:{user}`) using the same `IDistributedLock` idiom as `TelephonyInteractionSynchronizationService` (the tenant is already an implicit lock scope). Inside the lock the service now reloads the current user through `ITelephonyUserAccessor.ReloadCurrentUserAsync` — which evicts the user from the YesSQL identity map (`ISession.Detach`) so the re-read observes a peer's committed refresh rather than this request's stale copy — and, after refreshing, commits durably via `SaveChangesAsync` before releasing the lock so a waiting peer actually sees the new tokens instead of rotating a second time. A caller that cannot acquire the lock within the wait window reloads and reuses valid stored tokens rather than starting a competing refresh. Covered by a hardened concurrency test (a gate parks the first refresh inside the critical section while the second provably contends) proving two racing calls trigger exactly one provider refresh. Independent gpt-5.6-sol review: APPROVE.

---

### OC-032 — Provider revocation failures hidden before local tokens are deleted

- **Priority:** Medium · **Status:** Not Started · **Category:** Security · **Effort:** M · **Risk:** Medium · **Dependencies:** None

**Problem.** DialPad revocation catches and logs every exception (`DialPadTelephonyProvider.cs:680-711`); the authentication service then deletes local tokens as though remote revocation succeeded (`DefaultTelephonyAuthenticationService.cs:175-179`).

**Why it matters.** The external grant can remain active with no retained state or durable intent for retry — a disconnected account that is still authorized at the provider.

**Recommended solution.** Return a typed revocation result; clear interactive credentials immediately but persist encrypted retry work or explicitly report incomplete remote revocation.

---

### OC-033 — Cross-tenant ARI ownership guard is process-local static state

- **Priority:** Medium · **Status:** Not Started · **Category:** Multi-tenancy/scale · **Effort:** M · **Risk:** Medium · **Dependencies:** Needs a cross-tenant coordination primitive

**Problem.** `AsteriskAriApplicationOwnershipRegistry.cs:18` uses a `static readonly ConcurrentDictionary` to stop two tenants on one node attaching to the same ARI application. The implementation is careful and its comment candidly explains that `IDistributedLock`/`IDistributedCache` are tenant-scoped and therefore unusable here.

**Why it matters.** On two web nodes, tenant A on node 1 and tenant B on node 2 can both claim the same ARI application — the exact collision the registry exists to prevent — and the failure is silent. It is also the one place in scope violating the repo's "no static mutable state" rule.

**Recommended solution.** Back the claim with a lease in a shared store keyed by ARI identity, keeping the static dictionary as a local fast cache. Short of that, document the single-node constraint in `docs/telephony/asterisk.md` and surface it in the existing topology health check.

---

### OC-034 — Webhook rate limiting is process-local

- **Priority:** Low (Enhancement) · **Status:** Not Started · **Category:** DoS · **Effort:** M · **Risk:** Low · **Dependencies:** Ingress gateway or Redis

**Problem.** Rate and concurrency limiters are in-memory singletons (`ProviderWebhookIngressLimiter.cs:11-16,40-64`), so each node accepts its own configured limit.

**Recommended solution.** Enforce an edge/WAF rate limit and consider a Redis-backed global quota. Document the per-node semantics.

---

### OC-035 — Confirm antiforgery coverage on admin catalog POSTs

- **Priority:** Medium · **Status:** Completed · **Category:** Security verification · **Effort:** S · **Risk:** Low · **Dependencies:** None

**Problem.** Catalog and entitlement POST actions have no explicit antiforgery attribute; they rely on Orchard's global admin antiforgery filter.

**Recommended solution.** Add an integration test proving admin catalog POSTs reject absent/invalid antiforgery tokens, so the reliance is asserted rather than assumed.

**Resolution.** The four modules have no web-host integration harness, so a full HTTP round-trip test was not feasible without introducing one (out of scope for an S item). Instead the reliance was asserted at the layer under our control with a reflection-based architecture test, `AdminPostAntiforgeryArchitectureTests`. It enumerates every concrete MVC controller in the ContactCenter, Telephony, Asterisk, and DialPad module assemblies, finds each action that responds to an unsafe HTTP verb (POST/PUT/PATCH/DELETE via any `IActionHttpMethodProvider`), and asserts none opts out of antiforgery through `[IgnoreAntiforgeryToken]` on the action or its controller. Because Orchard Core registers `AutoValidateAntiforgeryTokenAttribute` globally, the *only* way one of these POSTs could bypass antiforgery is an explicit opt-out — so proving no opt-out exists proves the coverage holds. A second test pins the known catalog controllers (`Skills`, `Queues`, `AgentEntitlements`) as discovered so the scan can never silently degrade to zero actions. Confirmed no controller in any of the four modules declares `[IgnoreAntiforgeryToken]`; provider webhook receivers are minimal-API endpoints and never surface as controllers. Both tests pass.

---

### OC-036 — Cancellation handled as an ordinary fault; unbounded WebSocket frames

- **Priority:** Medium · **Status:** Completed · **Category:** Async correctness / DoS · **Effort:** S · **Risk:** Low · **Dependencies:** None

**Problem (cluster).**
- Broad handlers catch `OperationCanceledException` alongside real failures, so shutdown produces error noise and loops continue with a canceled token (`ContactCenterOutbox.cs:411`, `DialerPacingBackgroundTask.cs:42`, `TelephonyInteractionReconciliationBackgroundTask.cs:28`, `DialPadTelephonyProvider.cs:239-329`).
- Canceled agent readiness is persisted as **no-answer**, corrupting interaction outcomes and agent metrics (`AsteriskAgentChannelReadySignal.cs:89-91`, `AsteriskContactCenterVoiceProvider.cs:275-288`).
- `AsteriskRealtimeVoiceListener.cs:217-241` appends WebSocket frames until `EndOfMessage` with **no accumulated size limit**, then copies again via `ToArray()`.
- `IAsteriskChannelTenantBindingStore` has no `CancellationToken` parameters and uses uncancelable `SemaphoreSlim.WaitAsync()`.
- Durable hub work uses `MustComplete` with no host-stopping token or bounded deadline (`ContactCenterHub.cs:304-350,372-429`).

**Recommended solution.** Rethrow `OperationCanceledException` when the supplied token is canceled before generic handling; return a distinct `Ready/TimedOut/Canceled` result and re-check cancellation before no-answer compensation; enforce a configured max WebSocket message size and close with `MessageTooBig`; thread cancellation through the binding store; link durable hub work to application shutdown with a bounded timeout.

**Resolution.** The cluster was triaged into fixes and two documented Won't-Fix sub-parts (the panel review deliberately treats each sub-part on its own merits rather than mechanically applying the reviewer's blanket recommendation):

- **Cancellation-as-fault (FIXED).** Added `catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }` guards ahead of the swallowing generic handlers in `ContactCenterOutbox` (handler loop), `TelephonyInteractionReconciliationBackgroundTask`, and the six generic catches in `DialPadTelephonyProvider` (call-state, directory list, token revoke, token request, call-action, place-call). `DialerPacingBackgroundTask` was re-examined and already rethrows on shell-token cancellation while distinguishing its own run-budget token — no change required.
- **Canceled readiness recorded as no-answer (FIXED).** Introduced a tri-state `AsteriskAgentChannelReadyOutcome { Ready, NotReady, Canceled }` and changed `IAsteriskAgentChannelReadyRegistration.WaitAsync` to return it (both `internal`, so no PublicApi baseline impact). `AsteriskAgentChannelReadySignal` now distinguishes a genuine answer-timeout from host cancellation by re-checking the caller's token when the delay wins the race, and gives caller cancellation **precedence** over a superseded-registration `false` result so a simultaneous supersede + cancel is never reported as `NotReady`. All four readiness call sites (connect, conference, transfer, supervisor monitor/barge) now propagate `OperationCanceledException` on `Canceled` instead of compensating into a false `*_no_answer` disposition, so shutdown never corrupts interaction outcomes or agent metrics. The connect path additionally gained a dedicated `catch (OperationCanceledException) when (token.IsCancellationRequested)` guard ahead of its broad fault catch, because the readiness-thrown cancellation was otherwise swallowed and returned as `agent_connect_failed` (found by the independent review). Added `AsteriskAgentChannelReadySignalTests` coverage for the `Ready`/`NotReady`/`Canceled` outcomes.
- **Unbounded WebSocket frames (FIXED).** Added a validated `AsteriskCoordinationOptions.MaxRealtimeMessageBytes` (default 1 MiB, ceiling 64 MiB via `AsteriskConstants.MaxRealtimeMessageBytesCeiling`, `.Validate(...)` in `Startup`). `AsteriskRealtimeVoiceListener` now tracks accumulated frame size in its receive loop and, when a message would exceed the cap, logs a warning and closes the socket with `WebSocketCloseStatus.MessageTooBig` instead of buffering unbounded attacker-controlled data. The close is bounded (`CloseOutputAsync` under a `RealtimeCloseHandshakeTimeoutSeconds` token) so a hostile peer cannot stall the listener by refusing to complete the close handshake (found by the independent review).
- **Binding-store cancellation threading (SPLIT → OC-050).** The independent review correctly observed that the OC-036 "durable mutations must run to completion" rationale does not justify the store's *uncancellable, unbounded create-lock acquisition* or its uncancellable *read* queries: a wedged holder can block unrelated colliding channels indefinitely. A safe fix changes `CreateAsync`'s failure contract (an acquisition timeout must surface distinctly, not masquerade as a lost create race) and is larger than OC-036's scope, so it is tracked as **OC-050**. The durable write mutation itself still legitimately runs to completion.
- **`ContactCenterHub` `MustComplete` (SPLIT → OC-051).** `HubConnectionWork.MustComplete` uses `CancellationToken.None` by deliberate, documented design because durable state plus SignalR group membership must not be abandoned half-applied (no repair mechanism exists), and the convention is enforced by `HubCancellationConventionTests`. The independent review agreed the client-disconnect token must stay ignored but noted the missing *bounded application-shutdown* seam. Because a shutdown token is only safe once each durable step is idempotent and group membership is reconstructed on reconnect — an L-sized redesign — it is tracked as **OC-051** rather than changed here, where introducing the token alone would reintroduce the half-applied inconsistency the design prevents.

---

# Phase 8 — Documentation

### OC-037 — No `README.md` in any of the four modules

- **Priority:** Medium · **Status:** Completed · **Category:** Documentation · **Effort:** S · **Risk:** Low · **Dependencies:** None

**Problem.** Verified: none of ContactCenter, Telephony, Asterisk or DialPad has a `README.md`, which the repository conventions require for every module.

**Recommended solution.** Add a `README.md` per module covering purpose, features, installation, configuration, usage and dependencies.

**Resolution.** Added a `README.md` to each of the four modules — `CrestApps.OrchardCore.Telephony`, `CrestApps.OrchardCore.Asterisk`, `CrestApps.OrchardCore.DialPad`, and `CrestApps.OrchardCore.ContactCenter`. Each README documents the module purpose, a feature table with verified feature IDs (cross-checked against the module manifests and `*Constants` classes), a recipe-based installation snippet, configuration, usage, dependencies, and a link to the corresponding page on the documentation site. The Telephony README also covers provider authoring; the Asterisk README surfaces the single-active-process deployment constraint.

---

### OC-038 — Correct the accessibility claim in the readiness record

- **Priority:** Medium · **Status:** Completed · **Category:** Documentation accuracy · **Effort:** S · **Risk:** Low · **Dependencies:** OC-016

**Problem.** `.github/contact-center/PRODUCTION-READINESS.md` lists W6 agent-desktop accessibility as completed. Verified code state contradicts this (0 ARIA attributes on both real-time views, 0 live regions in scripts).

**Recommended solution.** Either complete OC-016 and keep the claim, or restate the claim honestly. Per the project's own "honest capability advertising" principle, do not advertise unverified capability.

**Resolution.** Resolved together with OC-016 by choosing the "complete and keep the claim" path: the ARIA/live-region/keyboard/degraded-state work was actually implemented on both the agent workspace and the supervisor dashboard, and the readiness record's W6 bullet was rewritten to describe the delivered scope precisely (offer/active/queue/summary/tiles/board live regions, keyboard-operable presence menu, focus management, inline error regions, connection-state announcements) across **both** surfaces rather than the agent workspace alone. The claim is now backed by shipped code.

---

### OC-039 — Document the single-active-process telephony constraint at the point of use

- **Priority:** Low · **Status:** Not Started · **Category:** Documentation · **Effort:** S · **Risk:** Low · **Dependencies:** OC-033

**Problem.** The single-active-process constraint for ARI ownership is a deployment-critical limitation that is not surfaced in `docs/telephony/asterisk.md` or in the module README.

**Recommended solution.** Document it in the operator docs and reference it from the topology health check message.

---

# Phase 9 — Technical Debt

### OC-040 — `ContactCenter/Startup.cs`: 36 public types in 1,368 lines

- **Priority:** Medium · **Status:** Not Started · **Category:** Code organization · **Effort:** M · **Risk:** Low · **Dependencies:** OC-008 (do after feature consolidation)

**Problem.** Verified: 36 public types in one file — the only serious violation of the repo's one-public-type-per-file rule in the entire scope (the next worst files have 2–3, an accepted Startup idiom). It also contains 73 registration calls.

**Recommended solution.** Split into per-feature startup files (`VoiceStartup.cs`, `ReportingStartup.cs`, `AgentPresenceStartup.cs`, …), each named for its type.

**Note.** An earlier reviewer reported "363 service registrations"; the verified count is **73**. The file-size and type-count problems are real; the registration count is not extreme.

---

### OC-041 — `ContactCenter.Core/Services` is 226 flat files

- **Priority:** Medium · **Status:** Not Started · **Category:** Code organization · **Effort:** L · **Risk:** Medium · **Dependencies:** None

**Problem.** Verified: 226 `.cs` files in a single flat `Services/` directory within a ~33k-line library whose only other folders are `HealthChecks`, `Indexes`, `Models`, `Telemetry`.

**Recommended solution.** Partition by bounded context — `AgentManagement`, `ActivityQueuing`, `Reporting`, `ProviderExecution`, `CallTopology`. Sub-folders first (low risk); consider extracting `ContactCenter.Reporting.Core` if the reporting surface keeps growing.

---

### OC-042 — God classes in providers and reporting

- **Priority:** Medium · **Status:** Not Started · **Category:** SRP · **Effort:** L · **Risk:** Medium · **Dependencies:** None

**Problem.** Verified files >600 lines (16 in scope). Worst offenders: `AsteriskTelephonyProviderBase.cs` (1,361 — dial, call state, hangup, transfer, merge, credential issuance, directory, with `GetCallStateAsync` ~180 lines and `MergeAsync` ~120), `EnterpriseInteractionReportProvider.cs` (1,298), `DialPadTelephonyProvider.cs` (1,112), `ContactCenterReportingService.cs` (973), `ProviderVoiceEventService.cs` (904).

**Recommended solution.** Extract `AsteriskCallControlService`/`CredentialService`/`DirectoryService`; apply a per-metric-family provider strategy for reporting behind a thin aggregator.

---

### OC-043 — Duplication between the two provider implementations

- **Priority:** Medium · **Status:** Not Started · **Category:** DRY · **Effort:** L · **Risk:** Medium · **Dependencies:** OC-011, OC-012

**Problem.** Asterisk and DialPad each re-implement quiescing guards, HTTP status mapping, retry semantics and the call-lifecycle methods. A bug fix in the dial flow needs two patches.

**Recommended solution.** Extract a shared `TelephonyProviderBase` into `Telephony.Core` covering the quiescing guard, HTTP result mapping and lifecycle scaffolding; both providers extend it. Combines naturally with OC-012.

---

### OC-044 — 54 report providers registered as hand-written lines

- **Priority:** Low · **Status:** Not Started · **Category:** DI/organization · **Effort:** M · **Risk:** Low · **Dependencies:** `IReport` shape in the Reports module

**Problem.** `AnalyticsStartup.ConfigureServices` (`Startup.cs:1224-1350`) contains 37 `AddEnterpriseReport(...)`, 12 `AddWorkforceReport(...)` and 5 direct `AddScoped<IReport,...>` calls, many exceeding 400 characters on one line. Resolving `IEnumerable<IReport>` constructs 54 instances per scope, and the metadata is trapped in imperative code where another module cannot extend it.

**Recommended solution.** Move definitions into `ContactCenterReportOptions` via `services.Configure<...>` — the pattern this same file already uses correctly for `ActivityBatchSourceOptions` — and project them through one provider, keeping `S["..."]` in the options callback so extraction still works.

---

### OC-045 — Assorted hygiene

- **Priority:** Low · **Status:** Not Started · **Category:** Hygiene · **Effort:** S · **Risk:** Low · **Dependencies:** None

- `Manifest.cs:246,273`: feature dependencies declared as magic strings (`"CrestApps.OrchardCore.Omnichannel.Managements"`, `"CrestApps.OrchardCore.SignalR"`) though `OmnichannelConstants.Features.Managements` is already imported and used at lines 24/35.
- Admin menus live in `Services/` in ContactCenter but `Navigation/` in Telephony — pick one.
- Three illustrative provider names in neutral XML docs (`TelephonyConstants.cs:54`, `IProviderIdentityResolver.cs:6-7`). These reference this product's own providers, not competitors, so this is a purity nit rather than a rule violation — but neutral contracts read better without them.
- `TelephonyOAuthController.cs:63,120,147` ignores `HttpContext.RequestAborted`.
- `AsteriskContactCenterVoiceMediaProvider.cs:477-495` does not dispose the `HttpRequestMessage`.
- `AsteriskContactCenterVoiceMediaSession.cs:118-151`: concurrent `StopAsync`/`DisposeAsync` can race semaphore disposal and leak the feature work lease.

---

# Phase 10 — Nice-to-have Improvements

### OC-046 — Split runtime presence out of `AgentProfile` and make it exportable

- **Priority:** Low · **Status:** Not Started · **Category:** Modelling / deployment · **Effort:** L · **Risk:** High · **Dependencies:** Should follow OC-003

**Problem.** `AgentProfile` mixes portable configuration (`MaxConcurrentInteractions`, `AllowedQueueIds`, `AllowedCampaignIds`, `Skills`) with hot runtime state (`PresenceStatus`, `RequestedPresenceStatus`, `PresenceChangedUtc`, `LastAssignedUtc`, `ActiveReservationId`). `AgentPresenceManagerService` rewrites the document on sign-in, sign-out, break, wrap-up start/end and heartbeat recovery.

**Why it matters.** Two consequences. (1) `AgentProfile` is the only major Contact Center configuration entity with **no recipe step and no deployment source** — skills, queues, queue groups, business hours, entry points, dialer profiles and reason codes all have both. Agent entitlements and skills therefore cannot be promoted between environments and must be re-entered by hand. (2) Every presence change rewrites the whole document and re-runs the full handler/index pipeline — write amplification on the hottest path.

**Recommended solution.** Split runtime presence into its own document keyed by `UserId`, leaving `AgentProfile` as stable configuration; then add `AgentProfileStep` + deployment source/step/driver following the existing `ContactCenterSkill` shape, exporting configuration members only (`ContactCenterDeploymentSerializer.EnvironmentOwnedMembers` already provides the exclusion mechanism).

---

### OC-047 — Reduce startup/type-count churn in the abstractions

- **Priority:** Low · **Status:** Not Started · **Category:** API surface · **Effort:** M · **Risk:** Low · **Dependencies:** OC-013

Split `ContactCenterConstants.cs` (819 lines) into domain-scoped files to keep each navigable, alongside the public/internal split from OC-013.

---

### OC-048 — Attribute manual-dial suppression audits to the initiating agent

- **Priority:** Low · **Status:** Not Started · **Category:** Auditability · **Effort:** M · **Risk:** Low · **Dependencies:** None

**Problem.** `ManualDialSuppressed` events published by `ContactCenterManualCallScreener` (OC-002) carry no `ActorId`, so `DefaultContactCenterEventPublisher` stamps them with `ContactCenterConstants.SystemActor`. The suppression is therefore attributed to the system rather than to the agent who attempted the call, which weakens the compliance trail for repeat-offender analysis.

**Root cause.** The screener runs inside the fresh `ShellScope.UsingChildScopeAsync` child scope created by `TelephonyHub.ExecuteAsync`, which does not carry the SignalR `Context.UserIdentifier` into the scope, and the manual soft-phone path has no domain object (reservation/interaction) to source an agent id from — unlike every other agent-attributed event in the codebase.

**Recommended solution.** Add an optional, provider-agnostic `InitiatorUserId` to `OutboundCallScreeningContext` (the generic "who initiated this origination" concept), populate it from `TelephonyHub` (which knows `Context.UserIdentifier`) via a scoped call-context accessor set inside the child scope, and have `ContactCenterManualCallScreener` set it as the suppression event's `ActorId`. Keep the fallback to `SystemActor` when the initiator is genuinely unknown (e.g. non-hub callers).

**Files affected.** `Telephony.Abstractions/Models/OutboundCallScreeningContext.cs` · `Telephony/Hubs/TelephonyHub.cs` · `Telephony/Services/DefaultTelephonyService.cs` · `ContactCenter/Services/ContactCenterManualCallScreener.cs`

**Acceptance criteria.**
- A manual soft-phone suppression records the initiating agent's user id as the audit `ActorId`.
- A non-hub origination with no known initiator still audits cleanly (falls back to `SystemActor`).

**Notes.** Discovered during the independent review of OC-002. Deferred out of OC-002 because actor fidelity is not among that item's acceptance criteria and the fix touches the public Telephony abstraction.

---

### OC-049 — Optional fresh-activation fold + strengthen migration equivalence docs/tests

- **Priority:** Low (Enhancement) · **Status:** Not Started · **Category:** Data migrations / performance · **Effort:** M · **Risk:** Low · **Dependencies:** None

**Problem.** Two low-value refinements surfaced while disproving OC-003 (see OC-003 Resolution): (1) migrations whose `CreateAsync` returns an old version (e.g. `InteractionIndexMigrations` returns 1) run a few additive `ALTER`/`CreateIndex` statements against an empty table on every fresh activation; (2) the deployment documentation's rolling-upgrade wording and the equivalence tests could be sharpened.

**Recommended solution.**
- *(Optional, evidence-gated)* Have such migrations build the final schema in `CreateAsync` and return the final version, **retaining** their `UpdateFromN` methods for already-versioned tenants (contract-legal; rolling-upgrade harness already supports this shape). Only pursue if fresh-activation startup profiling shows measurable benefit — the change duplicates final-schema declarations between `CreateAsync` and the update methods, which several migrations deliberately avoid.
- Reword the deployment docs so the guarantee reads as **preserved upgrade + post-upgrade write compatibility**, and require draining/maintenance when the `CallSessionIndexMigrations` in-place rebuild step (`UpdateFrom3Async`) is deployed, unless concurrent-load testing proves the DDL is non-blocking on the target engine.
- Optionally extend `ContactCenterRollingUpgradeTests` to assert full fresh-vs-upgrade equivalence for *all* ordinary indexes (not only columns and unique constraints) and to follow each update method's returned version exactly as Orchard's manager does.

**Acceptance criteria.**
- Any `CreateAsync` change keeps every `MigrationAdditiveOnlyGuardTests`, `ContactCenterRollingUpgradeTests`, and `ContactCenterRetentionCoverageTests` assertion green.
- Deployment docs no longer imply unconditional zero-downtime for the in-place rebuild step.

**Notes.** Recorded from the independent gpt-5.6 review of the OC-003 disposition. Not a correctness blocker.

---

### OC-050 — Uncancellable channel-binding create lock can block colliding channels indefinitely

- **Priority:** Medium · **Status:** Not Started · **Category:** Async correctness / liveness · **Effort:** M · **Risk:** Medium · **Dependencies:** None

**Problem.** `AsteriskChannelTenantBindingStore.CreateAsync` acquires a process-wide static striped `SemaphoreSlim` with an uncancellable, unbounded `WaitAsync()` and then performs YesSql query + save operations while holding it. If one holder wedges on a database operation, every other channel that hashes to the same stripe — including unrelated channels and other tenants sharing the process — blocks on the lock indefinitely. The store's read-only lookups (`FindByChannelIdAsync`, `FindAllByPeerChannelIdAsync`) also ignore cancellation even though they perform no durable mutation.

**Root cause.** The single-node exactly-once inbound-claim design (see the store's `CreateLockStripeCount` comment) intentionally serializes same-channel creates in-process, but the serialization primitive was written without a liveness bound because the original threat model only considered the two-contender connect/teardown race, not a wedged database operation holding the stripe.

**Recommended solution.**
- Give the create-lock acquisition a bounded wait. On acquisition timeout, surface a distinct failure (throw a dedicated exception the caller maps to its ambiguous/reconcile path) rather than returning the `false` "lost the create race" flag, which would incorrectly signal that another attempt owns the channel and could strand it.
- Thread `CancellationToken` into the read-only lookup methods (no durable-mutation risk) so shutdown does not wait on stalled queries. Keep durable **write** mutations running to completion (the OC-036 rationale still holds for the mutation itself, only the *acquisition* and *reads* gain a bound).
- Consider replacing the striped in-process lock with a YesSql unique index on `ChannelId` so the database enforces exactly-once creation without a process-wide gate; evaluate migration cost against the existing optimistic-concurrency model first.

**Acceptance criteria.** A wedged create holder can no longer block an unrelated colliding channel beyond the bounded window; read-only lookups honor cancellation; every existing binding-store and reconciliation test stays green; no live call is stranded by a create that times out (verified by a unit test that forces acquisition contention).

**Notes.** Split out of OC-036 from the independent gpt-5.6 review, which correctly observed that the OC-036 "durable mutations must complete" rationale does not justify an *uncancellable, unbounded lock acquisition* or uncancellable *read* queries. Kept as its own item because a safe fix changes `CreateAsync`'s failure contract and is larger than OC-036's scope.

---

### OC-051 — Durable hub work uses `CancellationToken.None` with no bounded shutdown deadline

- **Priority:** Medium · **Status:** Not Started · **Category:** Async correctness / graceful shutdown · **Effort:** L · **Risk:** Medium · **Dependencies:** None

**Problem.** `HubConnectionWork.MustComplete` runs durable Contact Center hub work under `CancellationToken.None`. This prevents a client disconnect from abandoning half-applied durable state plus SignalR group membership (correct), but it also means a wedged database or backplane call can hang indefinitely and block graceful shutdown, and it cannot actually *guarantee* completion — the host can still be force-terminated after the shutdown deadline, leaving the same mid-operation state the `None` token was meant to prevent.

**Root cause.** Group membership and durable state are mutated in separate steps with no idempotent replay or reconnect-time reconstruction, so the only tool available to avoid a half-applied disconnect was to make the work uninterruptible. The `HubCancellationConventionTests` convention then froze that decision.

**Recommended solution.** Keep ignoring the client-disconnect token, but (1) make each durable step idempotent and reconstruct group membership on reconnect, then (2) run the work under a token linked to application shutdown with a bounded deadline so shutdown stays graceful. The two parts must land together: introducing the shutdown token before idempotency/reconstruction exists would reintroduce the half-applied membership inconsistency. Update `HubCancellationConventionTests` to encode the new "client-disconnect-ignored, shutdown-bounded, idempotent" contract.

**Acceptance criteria.** Client disconnects still never abandon durable work mid-flight; application shutdown no longer blocks indefinitely on hub work; a killed-mid-operation process recovers consistent group membership on reconnect; the convention test encodes the new contract.

**Notes.** Split out of OC-036 from the independent gpt-5.6 review. The reviewer agreed the client-disconnect token must stay ignored; the disagreement was only about the missing *bounded shutdown* seam. Kept as its own item because it is an idempotency/reconstruction redesign (L) enforced by a convention test, not an OC-036-sized change.

---

## Production Readiness Checklist

Pre-merge blockers:

- [x] **OC-001** — tenant no longer bricked by default-configured `OrchardCore.HealthChecks`
- [x] **OC-002** — soft-phone compliance bypass closed or formally policy-gated and documented
- [x] **OC-003** — Won't Fix (premise disproven): fresh installs run no destructive rebuild; `UpdateFromN` chains are an intentional, tested rolling-upgrade capability. See OC-003 Resolution + OC-049.
- [x] **OC-005** — the lying `ReconcileAsync` lifecycle contract removed; genuine post-restart provider reconcile owned by the background task. See OC-005 Resolution.
- [ ] Full `dotnet build -c Release -warnaserror` verified clean on a machine with feed access (see review caveat)
- [ ] `dotnet test` green, including the feature-activation and distributed test projects
- [x] `npm run rebuild` run and `git status` clean (ContactCenter now covered — see **OC-019**)

Strongly recommended before first release:

- [ ] OC-011, OC-012 — provider extensibility and versioning seams (OC-004 done — provider registry now case-insensitive with observable collisions)
- [x] OC-016 — agent desktop accessibility (or OC-038, restate the claim honestly)
- [x] OC-019 — ContactCenter asset pipeline
- [x] OC-029, OC-030, OC-031 — OAuth and retry-safety cluster
- [x] OC-022 (done), OC-023 (done), OC-024 (done), OC-025 (done) — load-bearing performance items
- [x] OC-028 — scheduler lease correctness
- [ ] OC-037 — module READMEs

Post-merge follow-ups:

- [ ] OC-033, OC-034 — multi-node coordination and edge rate limiting
- [x] OC-035 — antiforgery coverage (delivered as a reflection-based architecture test proving no module controller opts out of the global `AutoValidateAntiforgeryToken` filter)
- [ ] OC-040 → OC-045 — technical debt
- [ ] OC-046 — agent profile split
- [ ] OC-048 — attribute manual-dial suppression audits to the initiating agent
- [ ] OC-049 — optional fresh-activation fold + sharpen rolling-upgrade docs/tests (non-blocking)
- [ ] OC-050 — bound the channel-binding create-lock acquisition; thread cancellation into read-only lookups (split from OC-036)
- [ ] OC-051 — bounded application-shutdown seam for durable hub work, gated on idempotent steps + group reconstruction (split from OC-036)

---

## Positive Findings — do not change these

These decisions are correct and should survive remediation.

1. **Idiomatic Orchard Core throughout.** Settings via `ISite` + `SiteDisplayDriver<T>` with `SettingsGroupId` and `RenderWhen` permission gating; 24 `DataMigration` classes; 7 `NamedRecipeStepHandler` steps; 9 `AdminNavigationProvider` menus; `IPermissionProvider` with stereotypes; `[BackgroundTask]` with lock timeouts; `EventActivity` + `ActivityDisplayDriver`; correct `ShellScope.UsingChildScopeAsync` / `AddDeferredTask` usage.
2. **Every exportable artifact has a matched triple** — `NamedRecipeStepHandler` + `DeploymentSourceBase<TStep>` + `DisplayDriver<DeploymentStep, T>` plus `.Summary`/`.Thumbnail` shapes — 7 of 7, no gaps. Import/export is never hand-rolled.
3. **Not modelling interactions/queues/agents as content items is the right call** for a high-write, non-editorial, non-versioned domain — while still extending Orchard where appropriate (a `DisplayDriver` is added onto Telephony's `SoftPhoneWidget` rather than forking it).
4. **Webhook ingress is exemplary.** The DialPad endpoint enforces a 1 MiB limit, JWT/HMAC signature validation against a data-protected secret, timestamp freshness (replay protection), rate + concurrency admission control, and durable idempotent acceptance with unique provider/delivery indexes.
5. **SignalR hub authorization is exemplary.** `[Authorize]` at class level, per-method permission checks, and `AuthorizeReferencedCallsAsync` enforcing per-user call ownership (IDOR protection) that fails closed on a missing store, unidentified caller, blank id, empty set or unmatched call. Call-control methods are one-line delegations, not fat-hub logic.
6. **Exceptional async hygiene.** Zero `async void`, zero blocking `.Wait()`, zero unsafe `GetAwaiter().GetResult()`, no request-path `Task.Run`, no ad-hoc `new HttpClient()`. The apparent `.Result` at `AsteriskAgentChannelReadySignal.cs:84` is provably safe — `Task.WhenAny` has already established completion.
7. **Repository conventions honoured at scale:** 0 `DateTime.UtcNow` (universal `IClock` injection), 0 `NotImplementedException` stubs, 0 `TODO`/`HACK`/`FIXME`, 0 `global using`, 0 non-sealed public classes in the abstractions, ~96% XML doc coverage on public members.
8. **Localization discipline is genuinely clean** — `IStringLocalizer` named `S` everywhere, and **zero** `S[variable]`/`S[$"..."]` extraction-breaking misuses across all 101 views and the C# drivers.
9. **Immutability and defensive design in the abstractions** — `ProviderVoiceEvent` is an immutable `sealed record` that defensively copies metadata while preserving the provider's comparer; capability contracts are properly segregated (`ITelephonyHoldProvider`, `ITelephonyMuteProvider`, …) so providers implement only what they support.
10. **Capability dispatch degrades gracefully** — an unsupported capability returns a typed, localized `TelephonyResult.Failed` rather than throwing or silently no-oping.
11. **Provider settings are correctly decoupled** — a third party contributes admin settings, workflow tasks and recipe steps through standard Orchard DI without core modules knowing it exists.
12. **Layer separation holds** — Telephony contains no `OmnichannelActivity`/disposition/workflow references; ContactCenter.Core makes no direct ARI/PJSIP/DialPad calls; `Interaction` carries no disposition or workflow state, with transitions guarded by `InteractionLifecycle.CanTransition`.
13. **Provider internals are properly encapsulated** — `IAsteriskAriClient`, `IAsteriskPjsipRealtimeCredentialStore` and peers are `internal sealed`.
14. **Resources are properly registered** via `IConfigureOptions<ResourceManagementOptions>` with explicit versions and correct dependency chains; the Telephony asset pipeline is a correct reference implementation.
15. **Extensive options validation** with `IValidateOptions<T>` + `ValidateOnStart`; no singleton capture of tenant-scoped options found.
16. **All four modules are correctly registered** in both `CrestApps.OrchardCore.slnx` and the Cms.Core.Targets `csproj`.
17. **Documentation is unusually complete** for a branch this size — 16 pages across `docs/contact-center/` and `docs/telephony/`, including `custom-providers.md`.
