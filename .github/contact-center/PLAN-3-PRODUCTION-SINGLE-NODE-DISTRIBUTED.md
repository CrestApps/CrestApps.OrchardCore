# PLAN-3 — Production readiness for the `single-node-distributed` topology

<!-- ledger-authority: release-authoritative -->

**Status:** Active. Supersedes the open items of `PLAN.md` (R8, R9) and `PLAN-2-SINGLE-NODE-COMPLETION.md` (Waves 5–6) for release scoping. Those documents remain the reference for architecture and for work already completed; this document is the authoritative execution plan and progress ledger from here to first production release.

**Authored:** 2026-07-26, after the independent Engineering Review Board verdict of **NOT APPROVED** (two model families, eight specialist deep-dives, all findings evidence-cited).

---

## 1. Mission

Ship a **production-supported deployment on exactly one application node that runs the full distributed contract**.

That means: one app node, but PostgreSQL, Redis distributed locking, and the Redis SignalR backplane are **mandatory, validated at startup, and probed by health checks**. Every authority in the system is cluster-correct. Growing to 2–4 nodes later becomes a configuration change and a capacity certification — not a rewrite.

This is deliberately narrower than "enterprise multi-node GA" and deliberately wider than "single-node development". It is the smallest honest production envelope this codebase can earn.

### Why this target is the right one

Single-node-with-Redis is not a weaker version of multi-node — it forces the same correctness work, because a single node **already experiences the distributed failure modes**:

- Rolling restarts and container replacement mean two instances of the app exist simultaneously for seconds. Leader handoff, fencing, and drain all matter.
- Orchard shell reloads (any tenant feature toggle, settings save, or recipe run) tear down and rebuild the shell **in-process**. Process-local ownership registries and in-memory `TaskCompletionSource` waits break here, today, on one node.
- Restart recovery exercises outbox replay, provider-command reconciliation, and ARI reconciliation.
- Redis becoming unavailable must degrade predictably rather than silently changing semantics.

Every one of the board's distributed findings is therefore in scope, even though multi-node capacity certification is not.

---

## 2. The release contract

The current `support-matrix.v1.json` has only two topologies and explicitly lists `"production with a single application node"` as a **prohibited combination**. This release creates and earns a third profile.

### 2.1 New topology profile (target state of `support-matrix.v1.json`)

```json
{
  "id": "single-node-distributed",
  "production": true,
  "minimumApplicationNodes": 1,
  "maximumApplicationNodes": 1,
  "redisBackplaneRequired": true,
  "redisDistributedLockRequired": true,
  "sharedRelationalDatabaseRequired": true
}
```

`prohibitedCombinations` changes:

- **Remove:** `"production with a single application node"`
- **Add:** `"production with a single application node without Redis distributed locking and a Redis SignalR backplane"`
- **Add:** `"production with more than one application node"` (until multi-node capacity certification is earned in a later release)
- **Keep:** `"production with SQLite"`, `"multi-region active-active"`, `"Elasticsearch in routing, assignment, provider ingest, or another correctness path"`

`releaseStatus` moves `blocked-until-r0-r8-pass` → `single-node-distributed-ga` only when §9 passes in full.

### 2.2 Capability certification rule (new, binding)

> **A capability moves from `prohibitedCapabilities` to `allowedCapabilities` only when a named CI job proves it. No capability is certified by code existing.**

Target capability set for this release, each bound to its proving gate:

| Capability | Gate that earns it | In this release? |
|---|---|---|
| `inbound-voice` | `gate-voice-e2e` | Yes |
| `manual-dial`, `preview-dial` | `gate-voice-e2e` | Yes |
| `call-transfer` (blind + attended) | `gate-transfer-e2e` | Yes |
| `conference` | `gate-conference-e2e` | Yes |
| `recording` | `gate-recording-audio-proof` **and** `gate-recording-dual-channel` **and** `gate-erasure-proof` | Yes — conditional on all three passing unskipped (W12.1, W17). Recorded audio that can never be deleted (§14 finding 7) cannot ship. |
| `monitor`, `whisper`, `barge` | `gate-supervision-audio-proof` | Yes — conditional on the audio proof passing unskipped |
| `take-over` | — | **No** — no implementation exists (telephony 7.3); remains prohibited until W12.2 lands |
| `automated-dial` (power/progressive) | `gate-dialer-compliance` | **No** — remains prohibited |
| `predictive-dial` | — | **No** — remains prohibited |
| `bidirectional-media` | — | **No** — remains prohibited |

Capabilities whose gate does not pass by the release date ship as **prohibited**, with the feature disabled by default and the code retained. That is the honest outcome, and it is acceptable.

### 2.3 Capacity envelope

Tier-1 caps stay as published (100 concurrent signed-in agents/tenant, 50 concurrent voice interactions/tenant, 10 new interactions/sec/tenant, 5 tenants/deployment) **but must be re-certified against the single-node-distributed profile on PostgreSQL** (W5.7). If certification measures lower, the published numbers come down. Published numbers are never aspirational.

---

## 3. Non-goals (explicitly deferred, do not start)

Multi-node production certification · multi-region · predictive dialing · workforce management · quality management / scorecards / screen recording · chat, email, and SMS ingress and providers · per-channel agent capacity · skill proficiency and bullseye relaxation · virtual hold · local presence caller-ID pools · list recycling and campaign quotas · Elasticsearch in any correctness path.

These are real gaps (see the board's domain gap table) but none of them block a credible voice-ACD production release, and starting them now delays the items that do.

---

## 4. Locked architectural decisions

These were contested during review. They are now settled; do not relitigate without new evidence.

### AD-1 — Keep the Telephony module. Delete the duplicate *authority*.

**Rejected:** deleting `CrestApps.OrchardCore.Telephony` and collapsing onto the Contact Center voice contracts.

**Reason:** `Telephony/Manifest.cs:14-35` depends only on Users and SignalR; `Asterisk/Manifest.cs:16-49` and `DialPad/Manifest.cs:16-37` each ship a base Telephony feature separate from their Contact Center adapter feature. Telephony is a legitimate standalone click-to-dial / soft-phone product that must not require CRM, queues, or agents. Deleting it destroys a viable smaller SKU.

**Decision:** exactly one component owns raw provider ingress. Both the Telephony call-history projection and the Contact Center orchestration consume the **same normalized, deduplicated, ordered** event stream through an explicit adapter. Telephony keeps generic user-owned call control, authentication, and history. Contact Center owns ACD topology and remains system of record for routed work.

### AD-2 — Ingress authority lives in a new `CrestApps.OrchardCore.Telephony.Core` layer.

The hardened ingress primitives currently in `ContactCenter.Core` (`ProviderVoiceEventService`: per-`(provider,callId)` distributed lock, idempotency-key dedupe, `HighWaterSequence` ordering, lifecycle-rank regression guard, `ConcurrencyException` retry in a fresh scope) move **down** into a layer that Telephony can use without Contact Center. Contact Center then consumes normalized output. This preserves AD-1 while eliminating the unhardened second path.

### AD-3 — Live call topology becomes first-class; history stays where it is.

`Interaction` already carries `CallLegs`, `Participants`, `TransferHistory`, `QueueHistory` (`Interaction.cs:137-147,195-197`). That is **history** and it is fine. The defect is that the **live** aggregate `CallSession` is flat — scalar `BridgeId`, `ConferenceId`, `SupervisorLegId`, `bool IsConference`, `int ParticipantCount`. You cannot drive a consult, conference, or supervised call from live state. `PLAN-2:161` permits preview reset, so this breaking change is cheapest now.

### AD-4 — Fix the routing pass before re-architecting the routing lock.

The per-queue `IDistributedLock` in `ActivityAssignmentService` is a correctness-first design and lock domains scale with queue count. It has **not** been proven to be the binding constraint. The measured cost is the routing pass itself: `ListAvailableForQueueAsync` materializes every available agent document, and `CountActiveByAgentAsync` issues a non-sargable `COUNT`. Fix pass cost and measure; only then consider partitioned single-writer routing.

### AD-5 — Instrument the seams *after* they stop moving.

Minimal safety telemetry lands immediately (W0.8). Full OpenTelemetry lands **after** W1 and W2, so spans and metric dimensions are defined once against the final authority boundaries rather than twice across two duplicate stacks.

### AD-6 — Every `[x]` binds to a named passing CI job **with a versioned test specification**.

A checkbox in any plan document means "a named CI job proves this", not "an increment shipped". Enforced by test, not by convention (W0.3).

**Strengthened 2026-07-26 (§14 finding 16).** A job *name* is not evidence — that is RC-3 recurring one level up. Every gate additionally requires a versioned specification stating: topology, inputs and injected faults, the invariant asserted, the observation source, numerical pass thresholds, and prohibited mocks/skips. `gate-ledger-evidence` verifies both that the specification exists and that the thresholds it declares are the ones the job actually asserts. A ledger entry with a blank `gate:` value cannot be ticked.

---

## 5. Workstreams

Each task lists the files to touch, the change, and its exit criterion. Exit criteria are testable statements, not activities.

---

### W0 — Truth, containment, and the release contract

Nothing else can be trusted until this lands. No other workstream may be marked complete before W0.

#### W0.1 — Add the `single-node-distributed` topology
- **Files:** `.github/contact-center/support-matrix.v1.json`; `tests/.../SupportMatrixTests.cs`
- **Change:** add the profile from §2.1; update `prohibitedCombinations` as specified.
- **Exit:** support-matrix test asserts the new profile exists, is `production: true`, and requires Redis backplane + Redis lock + shared relational DB.

#### W0.2 — Startup topology validation (fail closed)
- **Files:** new `src/Modules/CrestApps.OrchardCore.ContactCenter/Services/ContactCenterTopologyValidator.cs`; new `ContactCenterTopologyOptions.cs`; register in `Startup.cs`
- **Change:** operator declares the active profile in configuration (`ContactCenter:Topology:ProfileId`). On shell activation, when the profile is `production: true`, assert: relational provider is PostgreSQL, `OrchardCore.Redis` enabled, `OrchardCore.Redis.Lock` enabled, `CrestApps.OrchardCore.SignalR.Redis` enabled, `IDistributedLock` is not `ILocalLock`. On failure, log `Critical`, mark the readiness health check `Unhealthy`, and refuse to admit feature work via `IContactCenterFeatureWorkManager`. Do **not** throw during activation (that bricks the tenant with no diagnostic).
- **Exit:** a test enabling a production profile without Redis produces `Unhealthy` readiness and a `Critical` log naming the missing component; the same configuration with Redis is `Healthy`.

#### W0.3 — Evidence-bound ledger
- **Files:** all `.github/contact-center/*.md` progress sections; new `tests/.../Governance/LedgerEvidenceTests.cs`
- **Change:** every completed checkbox gains a trailing `` `gate:<workflow-job-id>` `` annotation. The test parses each progress table, extracts the annotation, and asserts the job id exists in a workflow under `.github/workflows/`. Unannotated `[x]` fails the test.
- **Also:** correct the `PLAN.md:1873` "41 gates" statement to match the enforced count, and re-open `R7` (`PLAN.md:1938`), which is marked complete against a declared-but-unused `ActivitySource`.
- **Exit:** `LedgerEvidenceTests` passes; no `[x]` exists without a resolvable job id.

#### W0.4 — Repair the CI gates
- **Files:** `.github/workflows/contact_center_operations_gates.yml`, `release_ci.yml`, `pr_ci.yml`, `main_ci.yml`, `validate_docs.yml`; new `contact_center_browser_gates.yml`
- **Change:**
  - Ops-gate path filter `src/Modules/...` list → `src/**`. Today it omits `ContactCenter.Core` (25,825 lines), `ContactCenter.Abstractions`, `Asterisk`, `DialPad`, and `Omnichannel.Core`, so a PR touching only Core silently skips the only distributed gate.
  - `release_ci.yml` runs **all** test projects, not only `tests/CrestApps.OrchardCore.Tests`.
  - Add a Playwright workflow; the project exists and is referenced by no workflow.
  - `validate_docs.yml` must not swallow failures through `tee`; propagate exit status.
- **Exit:** a PR modifying only `ContactCenter.Core` triggers the distributed gate; the release pipeline job list includes every test project.

#### W0.5 — Supply chain
- **Files:** `Directory.Build.props`, `.github/dependabot.yml`, new `.github/workflows/supply_chain.yml`
- **Change:** set `NuGetAudit=true` and stop passing `NuGetAudit=false` in CI; add `dotnet list package --vulnerable --include-transitive` as a failing gate; add SBOM generation (`microsoft/sbom-tool`) published as a release artifact; add secret scanning (`gitleaks`); add a third-party license inventory. Re-enable Dependabot version PRs for non-OrchardCore packages (`open-pull-requests-limit: 0` currently disables everything).
- **Also:** the build performs an unpinned runtime download of `copilot-darwin-arm64-*.tgz` from `registry.npmjs.org` via `GitHub.Copilot.SDK.targets`, so the solution **cannot build air-gapped**. Pin, vendor, or make it opt-out.
- **Exit:** the build succeeds with no network access to npm; a knowingly vulnerable package version fails CI.

> **Execution correction (evidence, 2026-xx).** The npm download is performed by target `_DownloadCopilotCli` in
> `~/.nuget/packages/github.copilot.sdk/1.0.5/build/GitHub.Copilot.SDK.targets:77`, which runs `BeforeTargets="BeforeBuild"`
> for **every** project in the graph, not only the Copilot module. The package already ships a supported opt-out
> (`CopilotSkipCliDownload`) and a vendoring hook (`CopilotCliBinaryPath`), documented at `targets:60-76`.
> Applied fix: default `CopilotSkipCliDownload=true` in `Directory.Build.props` so the default build is hermetic, and
> document the two opt-in switches inline. This converts the download from mandatory to opt-in without forking the SDK.
> **Verified:** `dotnet build tests/CrestApps.OrchardCore.Tests` previously failed with `MSB3923` on a socket error;
> it now succeeds offline. Remaining W0.5 scope (NuGetAudit, SBOM, gitleaks, Trivy, licenses, Dependabot) is unchanged.

#### W0.6 — Delete the tests that do not execute code
- **Files:** `tests/.../ContactCenterHubSecurityTests.cs`, `ContactCenterMigrationSqlTests.cs`, `ActivityQueueServiceConcurrencyTests.cs`, `ContactCenterOperationalLogPrivacyTests.cs:126-200`, the six governance JSON-shape test classes
- **Change:** `ContactCenterHubSecurityTests` reads `ContactCenterHub.cs` **as a string** and asserts `IndexOf` ordering — it invokes nothing yet satisfies the R1 SignalR authorization gate. `ContactCenterMigrationSqlTests` asserts a string against a `Mock<ISqlDialect>`. `ActivityQueueServiceConcurrencyTests` contains no concurrency: one mock, one call, asserts the stubbed value. Replace each with a test that invokes the real type. Keep governance shape tests but stop counting them as behavioural coverage.
- **Exit:** deleting the body of `ContactCenterHub.OnConnectedAsync` fails at least one test. Today it does not.

#### W0.7 — Immediate correctness containment
Small, independent, high-value; land these in week 1 regardless of other sequencing.
- `src/Modules/CrestApps.OrchardCore.ContactCenter/Endpoints/VoiceIngressEndpoint.cs:38` — stop passing `httpContext.RequestAborted` into `RouteInboundAsync`. Validate with the request token; mutate with `CancellationToken.None`, matching `ProviderVoiceWebhookEndpoint.cs:84`.
- `src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/AgentWorkStateHealingService.cs:199` — remove the `IServiceProvider.GetRequiredService<IProviderCallStateSynchronizationService>()` service-locator call. It is registered under `[Feature(Queues)]` (`Startup.cs:349`→`368`) but resolves a type only registered under `[Feature(Voice)]` (`Startup.cs:568`→`611`), so a Queues-only tenant throws at runtime. Inject `IEnumerable<IProviderCallStateSynchronizationService>` and no-op with a single warning when empty.
- Health endpoints — `/health/live` is currently bound to the `ready`-tagged aggregate, so a slow outbox restarts the pod. Bind liveness to process liveness only.
- `src/Modules/CrestApps.OrchardCore.Omnichannel.Sms/Endpoints/TwilioEventGridEndpoint.cs:130-132` — replace `==` signature comparison with `CryptographicOperations.FixedTimeEquals`, matching `AzureEventGridEndpoint.cs:205-211`; include the query string in the signed payload.
- **Exit:** one regression test per item.

> **Execution correction 1 — the prescribed `IEnumerable<T>` fix is wrong and must not be applied.**
> Constructor-injecting `IEnumerable<IProviderCallStateSynchronizationService>` creates a container cycle. Verified chain:
> `AgentPresenceManagerService` (`IAgentPresenceManager`, `Startup.cs:225`) → `IEnumerable<IAgentWorkStateHealingService>` →
> `AgentWorkStateHealingService` (`Startup.cs:368`) → `IProviderCallStateSynchronizationService` (`Startup.cs:611`) →
> `ProviderVoiceEventService` → `IAgentPresenceManager` (`:65`) ⟲. `IEnumerable<T>` does not break the cycle because the
> container must still construct every registered element to satisfy it. The service locator is therefore **legitimate**
> here under the repository rule "only fall back to lazy resolution when it is absolutely necessary to break a real
> container circular dependency". Applied fix: keep deferred resolution but switch `GetRequiredService` → `GetService`,
> log one warning, and return `0` so a Queues-only tenant degrades instead of throwing. The diagnosis (a Queues-only
> tenant crashes) was correct; only the prescription was wrong.
>
> **Execution correction 2 (superseded — see 2a) — the health-endpoint item is a build task, not a fix.** A repository-wide
> search finds **no health check registration anywhere in Contact Center source** (matches occur only in `obj/` package
> artifacts). There is no `/health/live` to rebind.
>
> **Execution correction 2a — correction 2 was wrong; the board's original finding was right.** The search behind
> correction 2 was defective. Three checks *are* registered — `contactcenter-storage`, `contactcenter-outbox`,
> `contactcenter-provider-ingress` — in `Startup.cs:76-85`, all tagged `["contactcenter","ready"]`, and two of them are
> already unit-tested. What was missing was any *endpoint*: no `MapHealthChecks` or `UseHealthChecks` existed in the
> repository, so the checks were unreachable. The board's stated pathology is nevertheless real and worse than described:
> `OrchardCore.HealthChecks` maps `MapHealthChecks(healthChecksOptions.Url)` with **no `Predicate`**
> (`OrchardCore.Modules/OrchardCore.HealthChecks/Startup.cs`), and `HealthChecksOptions.Url` defaults to `/health/live`
> (`OrchardCore.HealthChecks.Abstractions/HealthChecksOptions.cs`). Enabling that feature therefore aggregates **every**
> module's checks — including all three `ready`-tagged Contact Center dependency checks — onto the liveness route, so a
> degraded outbox restarts the node and a restart cannot drain an outbox. Applied fix (W0.8): map dedicated
> `api/contact-center/health/live` (predicate selects nothing) and `api/contact-center/health/ready` (predicate selects
> the `ready` tag), extract registration into a testable extension so a drifted tag cannot silently empty readiness, and
> document that the Orchard endpoint is a readiness signal despite its route name.
>
> **Execution correction 2b — correction 2a's own fix was still wrong: readiness must not consult a dependency.**
> Review of the 2a implementation established that aggregating `contactcenter-storage`, `contactcenter-outbox`, and
> `contactcenter-provider-ingress` into *readiness* reproduces the same class of failure it was meant to prevent, one
> level up. Those checks observe state shared by every node — the tenant database, the outbox backlog, the provider
> inbox — so every node evaluates them identically. Crossing an unhealthy threshold therefore returns 503 on **every**
> node at the same instant, the load balancer is left with no healthy target, and a degraded dependency becomes a total
> outage that cannot self-heal, because no node can pass a probe that depends on something still broken. This is the
> documented deep-health-check cascading failure (Amazon Builders' Library, *Implementing health checks*; Kubernetes
> probe guidance). **Governing rule adopted: an orchestrator probe may only consult state that differs between
> instances. If two healthy instances would always answer identically, the check is an alerting signal, not a routing
> signal.** Final W0.8 shape: readiness selects only a new node-local `contactcenter-node` check (host started, not
> shutting down, zero I/O); the three dependency checks move to a `contactcenter-dependency` tag surfaced on a new
> authorized `api/contact-center/health/dependencies` route gated on `MonitorContactCenter`; and reporting unready
> during `ApplicationStopping` supplies the graceful-drain signal that was missing entirely, without which every
> deployment drops in-flight calls. All three routes are mapped inside the tenant shell and therefore inherit the
> tenant request URL prefix; an unprefixed probe reaches a shell that does not map them and returns 404, which an
> orchestrator reads as failure — covered by a real-HTTP test rather than documentation alone.
>
> **Execution correction 3 — the Twilio item must not be implemented as written.** A faithful port of the official
> Twilio validator already exists at `src/Modules/CrestApps.OrchardCore.Omnichannel.Sms/Twillio/TwillioRequestValidator.cs`,
> with `CryptographicOperations.FixedTimeEquals` (`:186`), query-string inclusion, and with/without-port retry
> (`:69-75`, `:88-100`). Writing new HMAC code here would have shipped an inferior duplicate. Applied fix: delete the
> hand-rolled comparison and delegate to the existing validator; the only new code is public-URL reconstruction
> (`GetExternalRequestUrl` + segment-boundary `TrimPrefix`), modelled on `CopilotCallbackUrlProvider.BuildSiteAbsoluteUrl:91-115`.
>
> **Execution correction 4 — the ingress cancellation item is an admission-control problem, not a token-swap.**
> `CancellationToken.None` alone converts torn state into an unbounded wait. The cited precedent
> `ProviderVoiceWebhookEndpoint` is safe because it *also* holds a feature-work lease and a zero-queue concurrency
> lease. A deadline is not an acceptable substitute: `VoiceContactCenterCallRouter` performs non-atomic sequential
> writes (`:323-383`, `:451-456`), so any mid-routing cancellation strands the call. Governing rule adopted:
> **bound admission with leases; never bound the mutation with a deadline.**
>
> **Carried-forward follow-ups (reviewer-accepted, non-blocking):**
> - The 503 quiescing path sets no `Retry-After`. `IContactCenterFeatureWorkManager` exposes no retry hint, and
>   `ProviderVoiceWebhookEndpoint:42-45` behaves identically; inventing a value was rejected as speculative. If W0.8
>   introduces a drain deadline, surface it as `Retry-After` on **both** endpoints together.
> - `CopilotRuntimeLocator` proves file existence, not the Unix execute bit. Add an availability-provider integration
>   test (currently only the locator is covered).
> - `InboundVoiceEventSink:22-28` still forwards a caller token into the router, including the Asterisk
>   (`AsteriskInboundCallOfferBridge.cs:155-164`) and DialPad (`DialPadWebhookService.cs:84-92`) paths. Track a
>   producer-specific cancellation/repair characterization in W1; do **not** reintroduce a generic router deadline.

#### W0.8 — Minimal safety telemetry (not full OTel)
- **Files:** `src/Core/CrestApps.OrchardCore.ContactCenter.Core/Telemetry/ContactCenterDiagnostics.cs`
- **Change:** add only what is needed to operate blind today: outbox backlog age, provider-ingress failures, ARI reconnect count, reservation failures. Full instrumentation is W4, after the seams stabilize (AD-5).
- **Exit:** an operator can answer "is the outbox draining and is the PBX connected" from metrics alone.

---

### W1 — One provider-event authority

Root cause RC-1. Two parallel stacks consume the same Asterisk ARI stream: `ITelephonyProvider` → `TelephonyInteraction` (**no** `CheckConcurrency`) → `TelephonyHub`, 7-state `CallState`, `AsteriskRealtimeVoiceEventDispatcher` with **no** idempotency; and `IContactCenterVoice*Provider` → `Interaction`+`CallSession` (CAS) → `ContactCenterHub`, 12-state `ContactCenterCallState`, `ProviderVoiceEventService` with full hardening. `AsteriskTelephonyProviderBase.cs:18` implements the first; `AsteriskContactCenterVoiceProvider.cs` implements the second. Only one half is hardened.

#### W1.1 — Create `CrestApps.OrchardCore.Telephony.Core`
- **New project**, referenced by `Telephony`, `ContactCenter.Core`, `Asterisk`, `DialPad`.
- **⚠ Corrected 2026-07-26 (challenge finding 1).** This task previously said to "move down `ProviderVoiceEventService` internals". **That is architecturally impossible as stated.** `ProviderVoiceEventService` holds 14 dependencies, 9 of them Contact Center-specific: `IInteractionManager`, `ICallSessionManager`, `IContactCenterVoiceProviderResolver`, `IInteractionEventStore`, `IContactCenterEventPublisher`, `IAgentPresenceManager`, `IProviderCommandStateService`, `IContactCenterScopeExecutor`, plus `ITelephonyProviderResolver` (`ProviderVoiceEventService.cs:26-72`). Moving it as a unit would make `Telephony.Core` depend on Contact Center — inverting the very layering this workstream exists to establish, and it could not satisfy the stated exit criterion ("Telephony can ingest while Contact Center is disabled").
- **Move down only the provider-neutral ingress mechanics**, which have no Contact Center types: the distributed lock keyed on `(canonicalProvider, providerCallId)`, idempotency-key dedupe, `HighWaterSequence`, `OccurredUtc` monotonicity, the lifecycle-rank regression guard, `ConcurrencyException` retry-in-fresh-scope, `IProviderIdentityResolver`, the webhook inbox, and the provider command state machine. The output is a **durable, ordered, deduplicated normalized-event inbox** that knows nothing about interactions, sessions, presence or queues.
- **Leave in `ContactCenter.Core`** everything that touches `Interaction`, `CallSession`, presence, or queues. It becomes a *consumer* of the normalized stream (W1.2's `ContactCenterVoiceProjection`), not the owner of ingress.
- **Cut over by shadow replay, not by switch-flip:** run the new inbox alongside the existing path against recorded ARI and DialPad traffic, compare emitted normalized events and resulting projections, and only remove the old path when they agree over the full cassette corpus.
- **Exit:** `ContactCenter.Core` contains no raw-provider-event ingestion; `Telephony.Core` references no Contact Center assembly (enforced by an architecture test); `Telephony` ingests provider events with full hardening while Contact Center features are disabled; shadow replay shows zero divergence across the cassette corpus on real PostgreSQL and Redis.

#### W1.2 — Single ingest, two projections
- **Delete:** `AsteriskRealtimeVoiceEventDispatcher`'s direct writes to `DefaultTelephonyInteractionStore`.
- **Add:** `INormalizedVoiceEventHandler` in Telephony.Core. Two implementations: `TelephonyCallHistoryProjection` (writes `TelephonyInteraction`) and `ContactCenterVoiceProjection` (drives `Interaction`/`CallSession`/work state). Both run from one normalized, ordered, deduplicated stream.
- **Exit:** an integration test feeds one ARI `StasisStart`/`ChannelStateChange`/`StasisEnd` sequence and asserts exactly one Contact Center transition **and** one Telephony projection, with no second lock acquisition and no second dedupe record.

#### W1.3 — One call-state vocabulary with real hangup causes
- **Files:** `Telephony.Abstractions/CallState.cs`, `ContactCenter.Abstractions/ContactCenterCallState.cs`
- **Change:** `CallState` (7 states) becomes a strict, generated projection of `ContactCenterCallState` (12), or is deleted. Add `HangupCause` (`NormalClearing`, `Busy`, `NoAnswer`, `Rejected`, `Congestion`, `Failed`, `Canceled`, `AnsweringMachine`). Today every hangup collapses to `Disconnected`, destroying outbound-compliance reporting and abandon analytics at the source.
- **Exit:** a test asserts each ARI hangup cause maps to a distinct `HangupCause`; no code path can produce a call ending without one.

#### W1.4 — Split `ITelephonyProvider`
- **Files:** `src/Abstractions/CrestApps.OrchardCore.Telephony.Abstractions/ITelephonyProvider.cs` (15 methods), `ITelephonyService.cs`
- **Change:** split into capability interfaces mirroring the Contact Center pattern that the board judged correct (`IContactCenterVoiceCallControlProvider`, `…Recording…`, `…Monitoring…`, `…Transfer…`, `…Conference…`, `…AttendedTransfer…`, `…Media…`). Add `[Flags] TelephonyProviderCapabilities`. Providers implement only what they support instead of returning "not supported".
- **Exit:** DialPad compiles while implementing only call control; no provider contains a `NotSupportedException` stub.

#### W1.5 — Harden the surviving Telephony store
- **Files:** `src/Modules/CrestApps.OrchardCore.Telephony/Services/DefaultTelephonyInteractionStore.cs`
- **Change:** `CheckConcurrency => true` plus retry-in-fresh-scope, matching the 12 Contact Center stores.
- **Exit:** a concurrent-writer test over one real database proves no lost update.

#### W1.6 — Remove Asterisk-specific leakage from neutral contracts
- **Change:** `AgentChannelId`, `BridgeId`, `HoldingBridgeId`, `SnoopChannelId`, `RecordingName`, `ExternalMediaChannelId` currently travel through `Dictionary<string,string>` metadata into provider-neutral consumers. Replace with typed fields on the topology model from W2.2, or confine them to a provider-private state document. Also remove the legacy `PrimaryCallId`/`SecondaryCallId` from `MergeRequest`, which duplicates the canonical `Calls[]`.
- **Exit:** grep for those keys returns hits only inside the Asterisk module.

---

### W2 — State authority and data lifecycle

#### W2.1 — Extract `ContactCenterWorkState`
- **Promised at `PLAN.md:713`. Zero occurrences in code.** Consequently the CRM's 48-property `OmnichannelActivity` is hot-written 3–5 times per reservation cycle by `ActivityReservationService.cs:187-195,265-271,443-461,633-651` and `DialerAttemptService.cs:149-150,197-199`, under `CheckConcurrency = true`, and `UpdateActivityAsync` (`:745-762`) does **not** retry — it propagates. A legitimate concurrent CRM edit fails a reservation.
- **Change:** new aggregate keyed by activity id with its own `Version`. Move off `OmnichannelActivity`: `AssignmentStatus`, `ReservationId`, `ReservedById`, `ReservedByUsername`, `ReservedUtc`, `ReservationExpiresUtc`, `AssignedToId`, `AssignedToUtc`, `Attempts`, and the intermediate `Reserved`/`Dialing` statuses. `OmnichannelActivity` keeps only what the CRM owns.
- **Migration:** expand → backfill (batched, **outside** the tenant-activation transaction) → migrate readers → contract.
- **Exit:** routing writes zero fields on `OmnichannelActivity`; a CRM edit concurrent with a reservation cycle no longer produces `ConcurrencyException` on either side.

#### W2.2 — Live call topology
- **Files:** `src/Core/CrestApps.OrchardCore.ContactCenter.Core/Models/CallSession.cs`
- **Change:** replace scalar `BridgeId`, `ConferenceId`, `SupervisorAgentId`, `SupervisorLegId`, `bool IsConference`, `int ParticipantCount` with `CallLeg[]`, `Bridge`, `BridgeParticipant[]` (with join/leave timestamps and role), `ConsultCall`, and `CallRelationship` for transfer chains. Give `InteractionCallLeg.Status` a typed enum instead of `string`. Implement `MonitorSession`, declared at `PLAN.md:741` and never built.
- **Exit:** conference membership at any past instant is reconstructible; a consult transfer is representable without provider metadata strings; supervisor monitor/whisper/barge each have a first-class live representation.

#### W2.3 — Retention across all high-volume tables
- **Files:** `ContactCenterRetentionService.cs`, `ContactCenterRetentionBackgroundTask.cs`, new `ContactCenterRetentionOptions`
- **Change:** today only `InteractionEvent` is purged. Add batched, resumable purge with per-entity windows for: `Interaction`, `CallSession`, `QueueItem` (Completed/Removed), `ActivityReservation` (terminal), `ContactCenterOutboxMessage` (Completed/DeadLettered), `ContactCenterProcessedEvent` (bound to `MaxAttempts × MaxBackoff`), `ProviderCommand` (terminal), `AgentSession` (by `LastHeartbeatUtc` regardless of `IsOnline`, to catch crashed nodes). Coordinate windows with recording and audit-trail retention and with the erasure catalog.
- **Exit:** a seeded 5-million-row database returns to steady state within one retention cycle; no table lacks a policy.

#### W2.4 — Query plans
- **Change:** add **predicate-led** indexes for measured hot queries — beginning with `IDX_InteractionIndex_ActiveByAgent (AgentId, Status, DocumentId)`. Do **not** bulk-rewrite the existing `DocumentId`-leading composites: `DocumentId` is the legitimate YesSql join key and those indexes serve join-back and delete-by-document. Convert `Status != … && Status != …` to an inclusive `Status IN (…)` so it is sargable. Replace the in-memory `GroupBy().Count()` in `CountActiveByAgentIdsAsync` with a SQL `GROUP BY`, or precompute `ActiveInteractionCount` on `AgentProfile` transactionally with reservation transitions.
- **Exit:** an `EXPLAIN` budget test on PostgreSQL asserts no sequential scan on `Interaction` for the reservation path, enforced in CI.

#### W2.5 — Migration safety
- **Change:** two migrations perform row-by-row backfills **inside the tenant-startup transaction** — move to batched background backfill. `OmnichannelActivityIndexMigrations.UpdateFrom3Async` drops columns, which is not expand-contract safe. `OmnichannelActivityIndex.AssignmentStatus` has a different column type on fresh versus upgraded tenants — reconcile.
- **Exit:** a tenant with 1M activities activates within the startup budget; a fresh tenant and an upgraded tenant produce byte-identical schemas, asserted by test.

---

### W3 — Distribution correctness on one node

#### W3.1 — ARI ownership becomes a distributed lease
- **Files:** `AsteriskAriApplicationOwnershipRegistry.cs:17` (`private static readonly ConcurrentDictionary` — the XML comment itself says "on this node"), `AsteriskRealtimeVoiceTenantEvents.cs:40-52,143`
- **Change:** replace with an `IDistributedLock`-backed lease keyed on the **normalized `(baseUrl, applicationName)` pair only**, renewed on a heartbeat. Non-leaders open no WebSocket. On lease loss, stop the listener; on acquisition, start and reconcile.
- **⚠ Corrected 2026-07-26 (challenge finding 6).** This task previously specified a lease keyed `(tenantName, baseUrl, applicationName)`. **That would have been a security regression.** The existing in-memory registry deliberately keys on `NormalizeKey(baseUrl, applicationName)` *without* tenant and stores tenant as ownership **metadata**, rejecting any second tenant that claims the same PBX application (`AsteriskAriApplicationOwnershipRegistry.cs:29,35-38` — `if (!string.Equals(existing.TenantName, tenantName, ...)) return false;`). Adding tenant to the key would give two tenants two *different* lease keys for the same PBX application, so both would connect and both would receive the same event stream — converting a cross-tenant misconfiguration from a hard rejection into silent cross-tenant event leakage.
- **Therefore:** tenant remains ownership *metadata* compared under the lease, never part of the key. Preserve the existing reject-on-tenant-mismatch behaviour exactly, now distributed.
- **Also fix while here:** `TryClaim` currently `return true` (claim succeeds) when `baseUrl`, `applicationName` or `ownershipToken` is blank (`:22-27`) — a fail-open path. Make it fail closed.
- **Exit:** a test with two shells over one Redis proves exactly one listener; lease expiry transfers ownership within one renewal interval; **a two-tenant test proves the second tenant is rejected, not granted a parallel lease**; blank-input claims are refused.

#### W3.2 — Durable channel-ready wait
- **Files:** `AsteriskAgentChannelReadySignal.cs`
- **Change:** the in-memory `ConcurrentDictionary<callId, TaskCompletionSource<bool>>` means the originating instance must be the one that receives `StasisStart`. Replace with a poll over the durable `AsteriskChannelBindingIndex` with backoff, capped by the existing 30 s timeout.
- **Exit:** a test where originate and event are handled by different service providers still completes the bridge.

#### W3.3 — Redis as a hard dependency of the production profile
- **Files:** `ContactCenter/Manifest.cs:206-217`, `SignalR/RedisBackplaneStartup.cs`
- **Change:** `Contact Center Real-Time` currently depends on the base `CrestApps.OrchardCore.SignalR` feature, not the Redis backplane, so a production deployment can silently run without it. Enforce via W0.2 validation and a new `contactcenter-backplane` health check. `RedisBackplaneStartup` currently returns silently when `IRedisService` is absent — log `Critical` and report unhealthy instead.
- **Exit:** enabling the production profile without the backplane fails readiness with an actionable message.

#### W3.4 — Graceful drain
- **Files:** `AsteriskRealtimeVoiceListener.cs:52-82,213-239`
- **Change:** shutdown currently **cancels** the buffered channel instead of draining it, so in-flight `StasisEnd`, `ChannelHangupRequest`, and `RecordingFinished` are lost on every restart — producing zombie call sessions, orphan provider commands, and stuck reservations. Drain within a bounded grace window wired to `IHostApplicationLifetime`. Emit a `system.draining` notice to SignalR clients before aborting connections. Raise the bounded channel capacity to an option and, on overflow, trigger targeted reconciliation instead of dropping the connection.
- **Exit:** a restart during 20 active calls leaves zero orphaned records; the drain test asserts every buffered event was applied.

#### W3.5 — Background task hygiene
- **Change:** every-minute tasks with `LockExpiration = 60_000` have zero safety margin — a run exceeding 60 s loses its lease while still mutating. Set `LockExpiration` to ≥ 3× measured p99. Split `ReservationExpiryBackgroundTask` into idempotent expiry and leader-only offer emission (it currently also drives `OfferNextAsync` up to 100×/queue/minute). Add the `IContactCenterFeatureWorkManager` lease that its sibling tasks use and it lacks, so feature drain actually stops it.
- **Exit:** each task declares a measured p99 and a lease ≥ 3× it, asserted by test; disabling the Routing feature stops new offers immediately.

#### W3.6 — Health checks that match reality
- **Change:** only three exist (`contactcenter-storage`, `-outbox`, `-provider-ingress`). Add: ARI/PBX connectivity, Redis backplane, distributed lock provider, background-task liveness (last-success age per task), outbox backlog age. Bind liveness and readiness to the correct tags.
- **Exit:** every dependency named in the topology profile has a probe; killing Asterisk turns readiness `Degraded` within one interval.

#### W3.7 — Document per-node semantics
- **Change:** `ProviderWebhookIngressLimiter`, `ContactCenterHubConnectionRegistry`, and `ContactCenterFeatureWorkManager` are per-node singletons. On a single node this is correct; the limits must be **documented as per-node** so the meaning does not silently change at 2 nodes.
- **Exit:** each is documented, and a test asserts the documented semantics.

---

### W4 — Observability (after W1 and W2 land — AD-5)

#### W4.1 — Adopt OpenTelemetry
- Add packages (none exist in `Directory.Packages.props` today); configure tracing, metrics, and logging with an OTLP exporter; resource attributes include tenant, node, version, topology profile.

#### W4.2 — Spans at the stabilized seams
Provider ingress → normalization → work-state transition → reservation → routing pass → provider command dispatch → outbox dispatch → hub invocation. Propagate W3C `traceparent` inbound on webhooks and outbound into ARI channel variables (today only an app-level interaction id is carried). **Zero `StartActivity` calls exist in the codebase today.**

#### W4.3 — Metrics that make the SLOs measurable
Queue depth; oldest-waiting age; offered / answered / abandoned; **service level %**; RONA count; reservation latency histogram; routing-pass duration; outbox backlog age, redelivered, dead-lettered; provider up/down gauge; agent-state gauge; ARI reconnect count; active calls. Today there are exactly **two** counters, so every published SLO is unmeasurable in-band.

#### W4.4 — Correlation in logs
There are 258 `_logger.` call sites and **zero** `BeginScope`. Add scopes carrying tenant, interaction, activity, agent, and provider call id. Downgrade per-routing-pass and per-hub-action `Information` logging to `Debug`.

#### W4.5 — Log privacy, verified at runtime
Extend `OperationalLogRedactor` coverage and replace the source-substring privacy assertions (`ContactCenterOperationalLogPrivacyTests.cs:126-200`) with runtime redaction tests like the good ones already in that file at `:24-124`.

**Exit for W4:** an operator can answer, from telemetry alone, all seven questions in the board's observability checklist — what happened, when, which customer, which component, why, how long, how many affected.

---

### W5 — Verification that would actually fail if the code broke

#### W5.1 — Real databases
Zero PostgreSQL or SQL Server tests exist; every harness calls `configuration.UseSqLite(...)`, while the support matrix declares SQLite `production: false`. Add a Testcontainers harness (PostgreSQL 16 + Redis 7) and run the full Contact Center suite against it in CI. **This is the single highest-value testing change in the plan.**

#### W5.2 — Restart and rolling-deploy suite (single node)
Kill the process mid-call, mid-reservation, mid-outbox-dispatch, mid-recording. Assert no orphan `CallSession`, `ProviderCommand`, `ActivityReservation`, or `QueueItem`. Assert drain (W3.4) applied every buffered event.

#### W5.3 — Dependency-failure suite
Redis unavailable at startup and mid-run; PostgreSQL failover; Asterisk ARI down and flapping; provider webhook storm. Assert declared degradation, not silent semantic change.

#### W5.4 — Provider contract tests
All Asterisk tests currently assert against `TestAriClient` stubs defined in the same file — there is no ARI protocol contract test. Add recorded-cassette tests against real captured ARI payloads and REST responses, so an Asterisk version bump can break the build.

#### W5.5 — Unskip the audio proofs
`AsteriskBrowserAudioE2ETests.cs:5` and `WebRtcAudioProofTests.cs:11,32` are unconditionally `Skip`ped — these are the only proofs that audio works at all. Run them in CI with Asterisk + coturn containers. **These gates certify `recording`, `monitor`, `whisper`, and `barge` per §2.2.**

#### W5.6 — Call state machine property tests
Randomized transition sequences (including duplicates, reordering, and replay) must never produce an invalid state or a lost terminal transition.

#### W5.7 — Capacity certification
Run tier-1 load on the single-node-distributed profile against PostgreSQL with the `EXPLAIN` budgets from W2.4. Publish measured p95 reservation latency, offer-to-answer latency, and sustained interactions/sec. **If measured below the published caps, lower the published caps.**

---

### W6 — Agent desktop: accessibility and resilience

Missed by all eight specialist reviews and surfaced only by the adversarial challenge. For a tool agents use eight hours a day this is a procurement blocker under WCAG 2.2 AA, Section 508, and EN 301 549.

- **W6.1** — `agent-workspace.js` writes dynamic offer, queue, and call state through `innerHTML` in 9 places with **zero** `aria-*` attributes anywhere in the file. Replace with templates and `textContent`.
- **W6.2** — Add an `aria-live` region announcing incoming offers; focus management on offer arrival and dismissal; keyboard-only accept, decline, and presence change; visible connection state.
- **W6.3** — SignalR and state-refresh failures are swallowed in empty catch blocks (`contact-center-realtime.js:47,56,85,94`), so an agent whose connection dies sees a normal-looking but dead screen. Surface an explicit degraded/disconnected work state and stop accepting actions.
- **W6.4** — Localization: persisted enum state is sent to the browser via `.ToString()` and timestamps rendered with browser-default `toLocaleString()`. Send localized display values from the server; format in tenant or user timezone.
- **W6.5** — Add `axe` accessibility assertions to the Playwright suite and run it in CI (W0.4).

**Exit:** a screen-reader-only operator can receive, accept, handle, and wrap up a call; pulling the network cable produces a visible degraded state within 5 seconds.

---

### W7 — Security and compliance close-out

The security review returned **one LOW finding across seven modules**, which is an unusually strong result. Remaining items are compliance and policy, not vulnerabilities.

- **W7.1** — Timing-safe comparison in `TwilioEventGridEndpoint` (moved into W0.7 for speed).
- **W7.2** — Destination policy is **duplicated** between `TransferDestinationResolver.cs:144-157` and `ContactCenterExternalTransferSettingsDisplayDriver.cs:169-186` — a business rule living in a display driver, guaranteed to diverge. Consolidate into the resolver. Replace suffix matching (`digits.EndsWith("911")` rejects ~0.3% of valid E.164 numbers, e.g. `+14155550911`) with country-aware normalized classification. Make the premium/deny list tenant-configurable and data-driven rather than three hardcoded prefixes — and note that the hardcoded `"4470"` is UK personal numbering, not premium rate.
- **W7.3** — **Emergency calling is out of scope and must be stated as such.** There is no emergency-call path, no dispatchable location, and no notification anywhere in the product. Publish an explicit scope statement, show an operator warning in Telephony settings, and document that the customer's MLTS or carrier must provide emergency service. Do not ship a product that is ambiguous about this.
- **W7.4** — SBOM, secret scanning, dependency audit, license inventory (executed in W0.5).
- **W7.5** — Publish a data-residency contract covering database, Redis, recordings, Asterisk media, provider subprocessors, backups, telemetry export, and support access. The matrix says only "single-region" today.

---

### W8 — Operations and documentation

- **W8.1** — Module `README.md` files (currently **zero**) and C4 architecture diagrams (currently none), including the post-W1 ingress ownership diagram.
- **W8.2** — Operator install guide: Asterisk, PJSIP, TURN/coturn, RTP port ranges and firewall rules, certificates. Note that the Aspire host today boots dev secrets and exposes **no RTP UDP ports at all**.
- **W8.3** — Reference deployment for `single-node-distributed` (docker compose plus Bicep), with a zero-downtime restart procedure that depends on W3.4.
- **W8.4** — Runbooks bound to the W4.3 alerts; SLO / RPO / RTO; a rehearsed backup-and-restore drill.
- **W8.5** — Preview-reset tool: `PLAN-2:161` permits resetting preview tenants, but no operator-visible export, quiesce, reset, and verify procedure exists. W2.1 and W2.2 make one necessary.
- **W8.6** — Capacity and cost envelope from W5.7: sizing for app, PostgreSQL, Redis, and Asterisk; recording storage growth; egress.
- **W8.7** — Correct `telephony/index.md:68`, which contradicts current code regarding browser audio.

---

### W9 — Minimum credible ACD domain set

Not architecture — product credibility. Each item below is release-blocking because its absence is either a data-correctness defect or an operational dead end. Everything else from the board's 30-row gap table is explicitly deferred (§3).

- **W9.1 — Report timezone.** All reports are UTC-only. Any customer outside UTC gets wrong daily boundaries, which silently corrupts every daily metric. Add tenant and per-report timezone.
- **W9.2 — Interval-bucketed metrics (15/30 min).** Daily-only aggregation cannot produce an intraday operations view. Add an interval projection.
- **W9.3 — Service level % (X in Y seconds).** Only `SlaBreachCount` exists. Add `ServiceLevelTargetPercent` and `ServiceLevelWindowSeconds` to `ActivityQueue`; compute real-time and historical SL%.
- **W9.4 — Hold count and hold time.** Not stored at all, so AHT and occupancy are wrong wherever hold is used. Persist hold segments on the interaction.
- **W9.5 — Occupancy.** Derivable once W9.2 and W9.4 land; expose on the supervisor dashboard.
- **W9.6 — RONA auto-Not-Ready.** On offer expiry the agent returns straight to `Available` (`ActivityReservationService.cs:~626`), so the same unattended agent is immediately re-offered, producing offer loops and unbounded customer wait. Transition to a not-ready state with a RONA reason requiring explicit return.
- **W9.7 — Supervisor force actions.** None exist — no force sign-out, force presence, requeue, priority change, or broadcast. A supervisor cannot free an agent stuck in wrap-up or move a stranded interaction. This is an operational dead end in production, not a missing luxury.
- **W9.8 — ASA measured from queue entry**, not `CreatedUtc`, which currently overstates it.

---

## 6. Sequencing

Waves are ordered by dependency, not by team. Within a wave, tasks are parallelizable.

| Wave | Contents | Gate to exit |
|---|---|---|
| **Wave 1 — Truth & containment** | W0 (all) | CI proves what it claims; contained bugs have regression tests |
| **Wave 2 — Authority** | W1, W2.1, W2.2 | One ingress owner; work state extracted; live topology modeled |
| **Wave 3 — Data lifecycle** | W2.3, W2.4, W2.5 | Retention everywhere; `EXPLAIN` budgets green on PostgreSQL |
| **Wave 4 — Distribution** | W3 (all) | Lease-based ownership, durable waits, drain, health |
| **Wave 5 — Observability** | W4 (all) | The seven observability questions answerable from telemetry |
| **Wave 6 — Verification** | W5 (all) | PostgreSQL suite, restart/chaos, audio proofs unskipped |
| **Wave 7 — Product surface** | W6, W9 | Accessible desktop; minimum credible ACD metrics and controls |
| **Wave 8 — Release** | W7, W8, §9 | Support matrix updated; capacity certified; docs true |

**Amendment (§11) sequencing.** W10–W16 are not a ninth wave appended to the end; several must interleave or they cause rework:

| Wave | Added contents | Rationale |
|---|---|---|
| **Wave 1** | W11.4 (feature-dependency audit), W16.8 (Elasticsearch absence), **W15.2 (additive-migration gate, with contract-step exception), W15.3 (N-1 harness), W8.5 (preview reset/export tooling)**, W18 (configuration validation) | These are CI gates and recovery tooling. **A breaking data change may not merge before the tooling that makes it recoverable.** The original plan scheduled W8.5 and W15.3 *after* the W2 migrations they exist to protect — a direct contradiction of its own risk table. |
| **Wave 2** | **W5.4 (ARI/DialPad cassettes), W5.6 (property tests)** as W1 *entry* criteria; then W10.1, W10.2, W10.5, W11.1, W11.2 | W1's stated mitigation was cassettes and property tests, which were scheduled four waves later. They are now prerequisites, not follow-ups. W2 rewrites the same aggregates W10.1/W10.2 restructure — doing them sequentially is a deliberate double-rewrite, so they merge into one aggregate/schema redesign. |
| **Wave 3** | W15.1 (upcasters), W16.4–W16.7 | Upcasters must exist *before* the last breaking schema change, not after. |
| **Wave 4** | W12.4–W12.7, W12.11, W12.12 (signed markers), W12.13 (event journal), W16.1–W16.3 | Telephony state machine, reconciliation, orphan sweep, PBX failover, ownership authenticity and lifecycle-loss prevention are all distribution work and belong with W3. |
| **Wave 5** | W12.8 (media quality telemetry) | Instrument media at the same time as everything else. |
| **Wave 6** | W12.1, W12.2, W12.3, **W17 (subject erasure and media lifecycle)**, W11.8 (API baseline) | Dual-channel recording and erasure must be proven by the same audio/storage infrastructure W5 builds. **W17 blocks the `recording` capability.** |
| **Wave 7** | W10.3, W10.4, W10.7–W10.9, W11.3, W11.5–W11.7, W11.9, W14 | Decomposition, recipes, and product-wide a11y/i18n. |
| **Wave 8** | W12.9, W12.10, W15.4, W15.5, W13 sign-off | Table stakes and release safety. |
| **Wave 0 (parallel, starts immediately)** | **W13 (CRM/Omnichannel review)** | It is a *review*, not a build. It must run early because its findings change the scope of every later wave. Starting it in Wave 8 would be discovering blockers after the plan is frozen. |

**Deferred to Track B:** W10.6 (open channel model) — see §14 finding 6.

W0.7 and W0.8 land in week 1 regardless of wave.

**Do not** begin Wave 5 before Wave 2 completes (AD-5). **Do not** publish any capability as allowed before its gate passes (§2.2).

---

## 7. CI gates

Every gate is a named job. Every plan checkbox references one (AD-6).

| Job id | Runs | Proves |
|---|---|---|
| `gate-build-strict` | every PR | 0 warnings, air-gapped build |
| `gate-unit` | every PR | all five test projects |
| `gate-postgres` | every PR touching `src/**` | full suite on PostgreSQL 16 + Redis 7 |
| `gate-explain-budget` | every PR touching `src/**` | no sequential scan on hot paths |
| `gate-feature-activation` | every PR touching `src/**` | fresh-tenant activation matrix |
| `gate-distributed` | every PR touching `src/**` | lease ownership, backplane, durable waits |
| `gate-restart-drain` | every PR touching `src/**` | no orphans after kill; drain applied |
| `gate-dependency-failure` | nightly | Redis/PostgreSQL/ARI outage behavior |
| `gate-voice-e2e` | every PR touching voice | inbound, manual dial, preview dial |
| `gate-transfer-e2e` | every PR touching transfer | blind and attended |
| `gate-conference-e2e` | every PR touching conference | conference lifecycle |
| `gate-recording-audio-proof` | nightly + release | real audio recorded and retrievable |
| `gate-supervision-audio-proof` | nightly + release | monitor, whisper, barge audio |
| `gate-accessibility` | every PR touching `wwwroot/**` or Views | axe, keyboard-only, live regions |
| `gate-ledger-evidence` | every PR | no unproven `[x]` |
| `gate-supply-chain` | every PR | SBOM, secrets, vulnerable packages, licenses |
| `gate-capacity` | release | tier-1 sustained on one node |

The release pipeline runs **all** gates. Today it runs one test project.

---

## 8. Risks

| Risk | Mitigation |
|---|---|
| W1 is a large refactor of the most correctness-critical code | Land behind the existing normalized-event contract; W5.4 cassettes and W5.6 property tests before the cutover, not after |
| W2.1 and W2.2 are breaking data changes | `PLAN-2:161` permits preview reset; ship the W8.5 reset tool *first* |
| Audio proof gates may not pass in time | §2.2 makes that outcome safe: the capability ships prohibited, disabled by default, code retained |
| Capacity certification may measure below published caps | §2.3 requires lowering the published caps, not renegotiating the test |
| Scope creep from the deferred domain gap table | §3 is a closed list; changes require a plan amendment |

---

## 9. Release exit checklist

All must be true, each proven by its named gate.

1. `gate-ledger-evidence` green; no unproven checkbox in any plan document.
2. `gate-build-strict` green with no network access to npm.
3. `gate-postgres` green; the full suite runs on PostgreSQL 16, not SQLite.
4. `gate-distributed`, `gate-restart-drain`, `gate-dependency-failure` green.
5. Exactly one component ingests raw provider events; proven by W1.2's test.
6. `ContactCenterWorkState` exists; routing writes zero fields on `OmnichannelActivity`.
7. Live call topology supports consult, conference, and monitor as first-class state.
8. Every high-volume table has an enforced retention policy.
9. `gate-explain-budget` green; no sequential scan on the reservation path.
10. The seven observability questions are answerable from telemetry alone.
11. `gate-accessibility` green; screen-reader-only agent workflow verified.
12. W9.1–W9.8 shipped: timezone, interval buckets, SL%, hold, occupancy, RONA, supervisor force actions, ASA.
13. `support-matrix.v1.json` contains `single-node-distributed` as `production: true`; every allowed capability names a passing gate; `releaseStatus` = `single-node-distributed-ga`.
14. Capacity certified at or above published caps, or caps lowered to measured values.
15. Emergency-calling scope statement published; operator warning shown in settings.
16. Data-residency contract published.
17. Backup and restore drill rehearsed and documented.
18. Module READMEs and architecture diagrams exist and match the code.
19. `gate-recording-dual-channel` green, or `recording` ships prohibited (W12.1).
20. `gate-headless-closure` green: the service-only feature set activates with zero UI features (W11.1).
21. `gate-public-api-approved` and `gate-domain-invariants` green (W10.1, W11.8).
22. `gate-upgrade-n-1` green: N-1 and N run simultaneously against one database (W15.3).
23. `gate-a11y-all-views` green across admin and supervisor surfaces, not only the agent desktop (W14).
24. W13 complete: the Omnichannel/CRM layer has been reviewed to the same depth and its findings closed or formally accepted.
25. Recipes and deployment steps exist for all Contact Center configuration (W11.5).
26. Every §11 finding is either closed or carries a written, dated acceptance from a named independent reviewer.
27. `gate-erasure-proof` green: recorded media, transcripts and pointers are provably deleted on subject erasure and on retention expiry, with legal hold honoured (W17).
28. `gate-config-validation` green: no development secret can start a production deployment (W18).
29. Every gate named anywhere in this document has a versioned test specification with numerical pass thresholds; no ledger entry has a blank `gate:` value (§14 findings 16–18).
30. A minimum commercial capacity floor is defined and met, not merely "whatever certification measured" (§14 finding 20).

---

## 10. Progress status

Update this section after each meaningful change. A checkbox may be marked `[x]` **only** with a `gate:` annotation naming a passing CI job (AD-6).

### Verified baseline

First locally verified full run of the primary suite, established after the W0.5 hermetic-build fix made the solution
buildable offline. Prior coverage claims in earlier plans were never executed locally and must not be cited.

| Date | Command | Result |
| --- | --- | --- |
| Wave 1 | `dotnet test tests/CrestApps.OrchardCore.Tests -c Debug` | **2,435 passed · 0 failed · 1 skipped** (2m51s) |
| Wave 1 (after W15.2, W0.1, W0.2) | `dotnet test tests/CrestApps.OrchardCore.Tests -c Debug` | **2,697 passed · 0 failed · 1 skipped** (2m40s) |

The single skip is `AsteriskBrowserAudioE2ETests.BrowserToAsteriskWebRtcAudio_WithDirectIceAndForcedTurn_VerifiesReceivedToneFrequencies`,
which requires live Asterisk and TURN infrastructure. Counting it as coverage is prohibited until R-series certification runs it.

### Wave 1 — Truth & containment
- [x] W0.1 Topology profile added to support matrix `gate:pr_ci.yml#build_test` — ContactCenterSupportMatrixTests — 11 executed cases, 0 failed. Falsified: reverting the single-region-multi-node demotion fails 2 tests.
- [x] W0.2 Startup topology validation `gate:pr_ci.yml#build_test` — ContactCenterTopologyEvaluatorTests (37), ContactCenterTopologyHealthCheckTests (11), ContactCenterFeatureLifecycleTests topology admission (3) — 51 executed cases, 0 failed; full suite 2,697 passed / 0 failed / 1 skipped. Falsified: disabling the admission check, promoting single-region-multi-node in code, and removing the production-host branch fail 8 tests.
- [x] W0.3 Evidence-bound ledger `gate:pr_ci.yml#build_test` — `LedgerEvidenceTests` (21 executed cases, 0 failed); full suite 2,718 passed / 0 failed / 1 skipped (2,698 before, +21). Corrected all 42 control-matrix gate references, which previously resolved to zero real CI jobs. Falsified with 9 probes: claiming a planned gate is implemented, naming a non-existent job, citing a test class the gate's job never runs, marking a plan item complete with no annotation, a second release-authoritative ledger, an unresolvable annotation, a parser that returns nothing, a parser that treats every indented key as a job, and landing an anticipated job while the gate stays planned — each fails only the intended assertions.
- [x] W0.4 CI gates repaired `gate:pr_ci.yml#build_test, contact_center_browser_gates.yml#soft-phone-browser, release_ci.yml#distributed_test` — `LedgerEvidenceTests` (30 executed cases, 0 failed) now enforces the repairs instead of merely recording them: `EveryTestProject_IsExecutedByAtLeastOneWorkflow`, `EveryTestProject_RunsInTheReleasePipeline`, `ContactCenterGateWorkflows_WatchTheWholeSourceAndTestTrees`, and `WorkflowStepsThatPipeIntoTee_PropagateTheFailureOfThePipedCommand`. Ops-gate and activation-matrix path filters widened to `src/**` + `tests/**`; new `contact_center_browser_gates.yml` adopts the orphaned Playwright suite; `release_ci.yml` gained a Linux-only `distributed_test` job with a Redis service that publishing now depends on, plus feature-activation and browser steps, so every test project runs before packages ship; `validate_docs.yml` sets `pipefail` (its build failures were being swallowed by `tee`) and now triggers on code PRs. Repairing the orphan exposed a real regression: `SoftPhoneWidgetTests.BrowserAudio_DialInitializesAdapterAndMicrophone` had been failing since `97f6fe31` made the media-adapter registry per-instance; fixed by adding a `registerMediaAdapter` seam and covered by a new fail-closed test. Wiring the unrun suites into CI immediately paid for itself a second time: `ContactCenterHealthProbeActivationTests.Readiness_StaysHealthy_WhenADependencyCheckIsUnhealthy` had been broken by the W0.1/W0.2 topology check and nothing was running it. It was also weaker than its name promised - it asserted only which checks ran, never that readiness stayed healthy - so it now pins the readiness set, asserts independently that no dependency-tagged check participates in readiness, and asserts the healthy status. Falsified with 7 probes: re-narrowing a gate's path filter, deleting `pipefail`, dropping a test project from the release pipeline, re-orphaning the browser suite, removing the adapter seam, making browser audio fail open, and adding a shared dependency check to readiness — each fails only the intended assertions. A seventh probe caught a hole in this work itself: the `pipefail` check first matched the word inside its own explanatory comment, so it now strips comment lines before scanning.
- [~] W0.5 Supply chain `gate:pr_ci.yml#build_test` — hermetic build only — CopilotSkipCliDownload default in Directory.Build.props; solution builds with no network. NuGetAudit / SBOM / gitleaks / Trivy / licenses / Dependabot still open.
- [ ] W0.6 Non-executing tests replaced `gate:`
- [x] W0.7 Containment fixes `gate:pr_ci.yml#build_test` — VoiceIngressEndpointTests (5), TwilioEventGridEndpointSignatureTests (14), AgentWorkStateHealingServiceTests, CopilotRuntimeLocatorTests (4) — 33 tests; full suite 2451 passed / 0 failed. Reviewed by gpt-5.6-terra over 3 rounds (NO-GO, NO-GO, GO).
- [x] W0.8 Minimal safety telemetry `gate:pr_ci.yml#build_test, contact_center_feature_activation_matrix.yml#fresh-tenant-activation` — ContactCenterProcessHealthMiddlewareTests, `ContactCenterProcessLivenessPathValidatorTests`, `ContactCenterHealthEndpointsTests`, `ContactCenterNodeHealthCheckTests`, `ContactCenterNodeServingHealthCheckTests`, `NodeServingStateTrackerTests`, `SharedHealthCheckEndpointGuardTests`, `ContactCenterHealthProbeActivationTests` — 87 unit + 19 activation, reviewed GO round 7 (gpt-5.6-terra)

### Wave 2 — Authority
- [ ] W1.1 `Telephony.Core` ingress layer `gate:`
- [ ] W1.2 Single ingest, two projections `gate:`
- [ ] W1.3 One call-state vocabulary + hangup causes `gate:`
- [ ] W1.4 `ITelephonyProvider` split `gate:`
- [ ] W1.5 Telephony store hardened `gate:`
- [ ] W1.6 Provider leakage removed `gate:`
- [ ] W2.1 `ContactCenterWorkState` extracted `gate:`
- [ ] W2.2 Live call topology `gate:`

### Wave 3 — Data lifecycle
- [ ] W2.3 Retention `gate:`
- [ ] W2.4 Query plans `gate:`
- [ ] W2.5 Migration safety `gate:`

### Wave 4 — Distribution
- [ ] W3.1 ARI distributed lease `gate:`
- [ ] W3.2 Durable channel-ready wait `gate:`
- [ ] W3.3 Redis hard dependency `gate:`
- [ ] W3.4 Graceful drain `gate:`
- [ ] W3.5 Background task hygiene `gate:`
- [ ] W3.6 Health checks `gate:`
- [ ] W3.7 Per-node semantics documented `gate:`

### Wave 5 — Observability
- [ ] W4.1 OpenTelemetry adopted `gate:`
- [ ] W4.2 Spans at seams `gate:`
- [ ] W4.3 Metrics `gate:`
- [ ] W4.4 Log correlation `gate:`
- [ ] W4.5 Runtime privacy tests `gate:`

### Wave 6 — Verification
- [ ] W5.1 PostgreSQL + Redis harness `gate:`
- [ ] W5.2 Restart/drain suite `gate:`
- [ ] W5.3 Dependency-failure suite `gate:`
- [ ] W5.4 Provider contract cassettes `gate:`
- [ ] W5.5 Audio proofs unskipped `gate:`
- [ ] W5.6 State machine property tests `gate:`
- [ ] W5.7 Capacity certification `gate:`

### Wave 7 — Product surface
- [ ] W6.1–W6.5 Agent desktop accessibility & resilience `gate:`
- [ ] W9.1 Report timezone `gate:`
- [ ] W9.2 Interval buckets `gate:`
- [ ] W9.3 Service level % `gate:`
- [ ] W9.4 Hold tracking `gate:`
- [ ] W9.5 Occupancy `gate:`
- [ ] W9.6 RONA auto-Not-Ready `gate:`
- [ ] W9.7 Supervisor force actions `gate:`
- [ ] W9.8 ASA from queue entry `gate:`

### Wave 8 — Release
- [ ] W7.2 Destination policy consolidated `gate:`
- [ ] W7.3 Emergency-calling scope published `gate:`
- [ ] W7.5 Data-residency contract `gate:`
- [ ] W8.1–W8.7 Docs, diagrams, runbooks, reference deployment `gate:`
- [ ] §9 Release exit checklist fully green `gate:`

### Recorded deviations from the task text

Where an implementation differs from the wording of a W-task, the deviation is recorded here rather than being
absorbed silently, so a later reader can tell an intentional correction from a drift.

| Task | Plan text | Implemented as | Reason |
| --- | --- | --- | --- |
| W0.2 | Configuration key `ContactCenter:Topology:ProfileId` | `CrestApps_ContactCenter:Topology:ProfileId` | Every other Contact Center option already binds under `CrestApps_ContactCenter:*` (`:HealthChecks`, `:FeatureLifecycle`, `:Retention`). A second root would give the module two configuration namespaces and make the topology key the one an operator forgets. |
| W0.2 | "mark the readiness health check `Unhealthy`" | New dedicated `contactcenter-topology` readiness check | The existing readiness checks are node-local by contract and their tests enforce that. Overloading one of them would have silently broken that invariant; a separate check keeps the exception explicit, named, and individually testable. |

---

## 11. Gap-closure amendment (2026-07-26)

W0–W9 above were scoped to *ship a credible voice-ACD release*. They were **not** scoped to close every board finding. An audit of all 8 specialist reports against W0–W9 found **47 findings with no owning task, 3 partially covered, and 2 defects in this plan itself** (both now fixed in §2.2: `take-over` was certified by a gate for an unimplemented feature; `recording` was certified by a gate that mono-only recording would pass).

W10–W16 below close the unowned findings. §12 states honestly which review dimensions can and cannot reach a perfect score inside this release.

### W10 — Domain model and code-quality debt
Closes: data F-09, orchard F-05, F-06, F-07, F-12, F-13, telephony 3.1, 1.5, 2.3.

- **W10.1 De-anemize the aggregates.** Every aggregate (`Interaction`, `CallSession`, `ActivityReservation`, `QueueItem`, `AgentSession`, `ContactCenterWorkState`) currently exposes public setters and delegates all rules to services. Convert to private setters plus intention-revealing methods (`Answer`, `Hold`, `Resume`, `Transfer`, `Abandon`, `Complete`). Illegal transitions throw rather than being prevented only by a rank comparison.
  - **Exit:** a test asserts no public setter remains on any aggregate; a state-machine transition table is exhaustively tested including every illegal edge.
- **W10.2 Value objects over primitives.** `PhoneNumber` (E.164-validated, one canonical parser — retires the duplicate normalizers behind data F-06/dist F18), `Duration`, `SkillTag`, and strongly-typed `InteractionId`/`QueueId`/`AgentId`. Kills primitive obsession and the ambiguous-match class of bug at the type level.
- **W10.3 Decompose `VoiceContactCenterCallRouter`** (23 constructor dependencies, 683 LOC) into an ordered, individually testable routing pipeline. 23 dependencies is not a style complaint: it means the class has 23 reasons to change and cannot be unit-tested without 23 fakes.
- **W10.4 Collapse controller duplication.** ~1,700 duplicated LOC across six catalog CRUD controllers → one generic base. Every future catalog entity then inherits authorization, paging, validation, and audit for free.
- **W10.5 `ProviderVoiceEvent` → immutable record.** It is simultaneously a public provider contract and an internally mutated buffer. Make it immutable; mutation via `with`.
- **W10.6 Open the channel model.** `enum InteractionChannel` is closed; the product promises channel extensibility. Replace with a registry-backed channel descriptor. This is a prerequisite for chat/email/SMS in Track B, so doing it now avoids a second breaking change.
- **W10.7 Decompose the two remaining mega-files:** `AsteriskContactCenterVoiceProvider.cs` (2,401 LOC) and `EnterpriseInteractionReportProvider.cs` (1,501 LOC).
- **W10.8 Fix the scoped report factory** that closes over mutable state (orchard F-13) — a latent cross-request data-leak shape.
- **W10.9 Provider-neutral transfer targets and outcomes.** Transfer target types and transfer/merge/snoop outcome metadata still leak Asterisk key names into caller code.

### W11 — Feature graph, public API, and Orchard idiom
Closes: orchard F-01, F-02, F-04, F-08, F-09, F-10, F-14, F-15, F-16.

- **W11.1 Headless/admin split.** `Queues` (and everything above it) transitively drags in `Omnichannel.Managements` UI. Split every capability into `X` (services) and `X.Admin` (UI). A headless/API-only deployment must be provable.
  - **Exit:** a test enables the full headless closure and asserts no admin/UI feature is activated.
- **W11.2 `ContactCenter.Admin` is a manifest-only marker** — enabling it has zero code effect. Either give it the admin registrations (with W11.1) or delete it. A feature that does nothing is a support liability.
- **W11.3 `Analytics` must declare the `Voice`/`Routing` closure its reports actually read**, or degrade explicitly. Today it silently produces wrong reports when enabled alone.
- **W11.4 Feature-dependency audit.** **DONE** (`ContactCenterFeatureDependencyAuditTests`). Add a test that every used service's feature is declared. This is the general fix for the class of bug W0.7 patched pointwise (orchard F-03).
  - **Delivered:** each feature is booted alone in an isolated tenant and every interface in the Contact Center and Telephony assemblies is resolved, individually and as `IEnumerable<T>`. The oracle is *registered but unconstructable*: not registered → `null` → correct (the owning feature is off); registered but throwing → always a manifest defect. No false positives by construction. Plus `EveryFeature_DeclaresDependenciesThatExist` and `NoFeature_DeclaresACircularDependency`.
  - **Found and fixed:** `ICallControlAuthorizationService`, `ISupervisorQueueAuthorizationService` and `ITransferDestinationResolver` were registered in the base feature but depend on Agents/Queues services. Every consumer injected them *optionally* and fail-**open**ed when absent — including keeping a caller-supplied `ProviderCallId` (IDOR on a live call) and skipping outbound-dial ownership (toll fraud). Registrations moved to Voice and Queues; all optional parameters made mandatory; every fail-open branch deleted. Closed permanently by `ContactCenterOptionalDependencyTests`, which rejects *any* optional injected collaborator in either Contact Center assembly.
  - **Also fixed (pre-existing, surfaced by this work):** system-initiated `Reject`/`SendToVoicemail` for closed entry points and unroutable queues carry no agent user and were therefore denied by the agent-ownership boundary, so those calls were never hung up at the provider. Call control now models the initiator (`CallControlInitiator.System`), resolving the provider call id from the server-owned interaction — no call session exists that early — restricted to the `Decline`/`Voicemail` verbs and never granting supervisor privilege.
  - **Rejected as unsound — do not retry:** `ITypeFeatureProvider` is *not* a valid oracle for "what did this feature register". `ExtensionManager` harvests every public non-abstract class in the module assembly into the feature named by a type-level `[Feature]` attribute, falling back to the module-named feature, *before* `ShellContainerFactory.PopulateTypeFeatureProvider` adds DI descriptors with a non-overwriting `TryAdd`. The map is the union of both. Measured: 147 types attributed to a base-only tenant, including types whose startup never ran.
  - **Disclosed limitation — the "every declared dependency is used" half is not enforceable.** `EnableFeaturesAsync` always enables a feature together with its full declared closure, so no tenant can be booted with one dependency removed. Nor is a static oracle sound: `Voice.SoftPhone → RealTime` looks unused by DI (its startup registers no RealTime service) but is genuinely required — `ContactCenterSoftPhoneWidgetDisplayDriver` emits `GetPathByHub<ContactCenterHub>()`, a hub mapped only by RealTime. A gate would have failed on its first run as a false positive and been suppressed. Unused declared dependencies remain a code-review concern.
- **W11.5 Recipes and deployment steps for all Contact Center configuration** (queues, skills, routing profiles, dispositions, business hours, provider connections). Without these, a customer cannot script a tenant, promote dev→prod, or restore config from source control — table stakes for a distributed commercial Orchard module.
- **W11.6 Stream the voice-webhook body.** Reading the full request body into a `string` under a per-request lease is a memory-pressure attack vector on an unauthenticated-ish edge.
- **W11.7 Business rules out of display drivers.** Rules that must hold on *every* write path currently live in a display driver, so API and recipe writes bypass them. One validation path, invoked by driver, controller, and recipe alike.
- **W11.8 Public API surface audit + approval test.** Every public type: should it be public, sealed, immutable, or exist at all? Lock the result with a `PublicApiGenerator` baseline so surface changes become deliberate review events.
- **W11.9 Fix the agent-workspace N+1.**

### W12 — Telephony and media completeness
Closes: telephony 3.1, 3.2, 3.3, 4.5, 4.6, 5.2, 5.3, 5.4, 6.1, 7.2, 7.3, 8.1, 8.2 and the table-stakes gap.

- **W12.1 Dual-channel (stereo) recording — blocking for the `recording` capability.** `AsteriskContactCenterVoiceProvider.cs:459` calls `_ariClient.StartBridgeRecordingAsync(bridgeId, recordingName, AsteriskAriConstants.RecordingFormat, ...)`. Asterisk bridge recording (`POST /bridges/{id}/record`) produces a **single mixed mono file** — there is no per-leg capture anywhere in the tree (a repo-wide search for stereo/dual-channel recording returns zero hits). Every downstream product expectation — agent-vs-customer diarized transcription, talk-over and silence analytics, QM scoring, AI summarization — requires separated legs, and none of them can be retrofitted onto mixed audio. Implement per-leg capture (snoop-per-channel, or `MixMonitor` with the `b` option) and merge to a two-channel artifact. Shipping mono while certifying the `recording` capability would be a false claim of the exact kind RC-3 describes.
- **W12.2 Supervisor take-over is dead surface — the defect was in this plan, not the product.** `MonitorMode.TakeOver` (`MonitorMode.cs:26`), the `TakeOver` capability bit (`ContactCenterVoiceProviderCapabilities.cs:78`) and its entry in `ContactCenterMonitoringService._monitorModes` (`:21`) all exist, but **no provider implements it**: Asterisk deliberately omits `TakeOver` from its declared capabilities (`AsteriskContactCenterVoiceProvider.cs:83-90`), and the mode switch (`:1591-1618`) falls through to `_ => Failure("monitor_mode_unsupported")` at `:1617`. The runtime behaviour is therefore *correct and honest* — the mode is filtered out by capability before it can be invoked. **This is not a production defect and needs no urgent fix.** What was wrong is that §2.2 of this plan previously certified `take-over` for release via `gate-supervision-audio-proof` — a gate that could never pass, for a feature no provider implements. §2.2 is corrected; the capability stays prohibited. Either implement it properly (supervisor joins, agent leg drops, interaction ownership transfers) or delete the enum value, capability bit and mode-list entry so the surface stops advertising an unbuilt feature.
  - **Generalized root cause:** capability bits can exist with no implementation anywhere and nothing detects it. Add `gate-capability-implemented` — every bit in `ContactCenterVoiceProviderCapabilities` must be declared by at least one in-tree provider **and** exercised by at least one test, or be explicitly listed as reserved. This closes the class, not the instance.
- **W12.3 Media security and codec correctness.** Remove the hardcoded `format=ulaw` on external media; enforce SRTP/DTLS at the application layer rather than assuming transport config; enforce TURN credential lifetime alongside the HMAC.
- **W12.4 Explicit call state machine + verified reconciliation.** Legal transitions declared as data, not implied by rank comparison. Prove `ITelephonyCallStateProvider`'s Asterisk implementation actually queries live ARI — reconciliation that reads local state is not reconciliation.
- **W12.5 Idempotency from event identity, not payload hash.** SHA-256 over the raw ARI payload breaks on any upstream serialization change, silently converting duplicate suppression into duplicate processing.
- **W12.6 Orphan sweeper.** Reap orphaned bridges/channels and snoop channels leaked by crash-in-the-middle. Without this, a PBX accumulates zombie resources until manual intervention.
- **W12.7 PBX failover.** Multiple ARI endpoints with health-based selection. A single PBX with no failover makes the PBX a hard SPOF regardless of how many app nodes exist.
- **W12.8 Media quality telemetry** — MOS, jitter, packet loss, one-way delay per call leg. Without these the answer to "why did the call sound bad?" is permanently unavailable.
- **W12.9 Telephony table stakes** absent today: inbound DTMF/IVR digit collection, music-on-hold, call park, and CLI presentation rules (privacy/withheld, per-queue outbound identity).
- **W12.10 STIR/SHAKEN attestation passthrough** for US outbound. Increasingly a carrier-side requirement, not a nice-to-have.
- **W12.11 Replace the magic 1,000-event channel bound** with a documented, load-derived value plus a saturation metric and alert.

### W13 — Omnichannel/CRM layer review
Closes: challenger gap "CRM is an unreviewed production dependency".

The Contact Center depends on Omnichannel/CRM for the universal work item, yet that layer was never reviewed. Run the same review depth over it — authorization boundaries, migration safety, concurrency/CAS, retention, PII handling, background schedulers, tenant isolation — and fold its findings into this plan before release. **Shipping on an unreviewed dependency is how a "hardened" system fails in production.**

### W14 — Product-wide accessibility, localization, and UX
Extends W6 (agent desktop only) to the whole product.

- **W14.1** Admin UI, supervisor dashboard, and report views to WCAG 2.2 AA — the same bar as the agent desktop. Procurement audits the whole product, not one screen.
- **W14.2** Automated a11y gate (axe) across all Contact Center views, not a single page.
- **W14.3** Full localization: no enum `.ToString()` over the wire, no browser-default `toLocaleString()`, localized report headers/exports/notification templates, RTL verified, and a per-tenant/per-user timezone and locale contract (pairs with W9's UTC-only reporting fix).

### W15 — Upgrade, versioning, and deployment safety
Closes: ops F-DEP-01, F-DEP-02, F-DEP-03, F-DEP-04, and the missing N-1 test.

- **W15.1 Event/document upcasters.** `SchemaVersion` exists on persisted documents but nothing reads it. The first schema change after GA is unrecoverable without this.
- **W15.2 Additive-only migration enforcement test** — fail CI on any destructive column/table operation (the existing `UpdateFrom3Async` drop is the precedent). **DONE.** Every Orchard data migration is parsed and checked by three oracles, because each is blind to what the others see: schema-builder operations (`DropColumn`, `DropIndex`, `DropTable`, `RenameColumn`, `RenameTable`, `AlterColumn`), raw SQL passed as an argument to any synchronous or asynchronous execution method, and raw SQL assigned to a command's text — the last being the dominant raw-SQL shape in this repository and one an argument-only oracle would miss entirely. The raw-SQL oracles deliberately do not scan string literals: ordinary C# defeats that, since `"drop " + "table " + name` and `$"drop {kind} {name}"` are both destructive and neither contains a matching literal. The statement is reconstructed from the syntax tree across concatenation, interpolation, single-assignment locals, and read-only query-builder composition, then classified, so it is judged on what it does rather than on how it was spelled; a builder composed with any operation outside the read-only set falls back to unreadable rather than being assumed safe, and because that read-only special case is only sound while the single `SqlBuilder` in scope is the data layer's, a separate fact fails if the repository ever declares **or aliases** a type by that name, since `using SqlBuilder = Something;` is the same impersonation without the declaration. Classification does not stop at the leading verb: `with doomed as (...) delete from t` leads with `with`, and a batch can hide a second statement after a semicolon, so a destructive verb anywhere in the statement is a finding. Quoted values are stripped before that scan so a value that merely reads like a verb is not a false positive, and a statement that can execute another statement (`exec`, `sp_executesql`, or a procedural `do`/`begin` block, wherever it appears) is classified as unreadable rather than safe, since the gate can see the wrapper but not what it runs — without this, stripping quoted values would clear `DO $$ BEGIN EXECUTE 'DROP TABLE t'; END $$;`. A statement the gate cannot read is itself a finding, recorded per call site with what it does and why it cannot be destructive, because otherwise `ExecuteAsync(statement)` would be a one-line bypass. Such an approval is pinned to a fingerprint of the **declaring type**, not the call site: the approved expression here calls a sibling helper, so pinning the expression alone would let a change to what that helper builds inherit the old approval. Attribution is syntactic, so a destructive step is bound to the private helper that performs it rather than to the step that calls it — exactly the cases a line-proximity heuristic would mis-attribute. Each destructive step needs a register entry authorizing **one operation against one named object**, and an entry that matches no step or several steps fails: registering a method instead would make every entry a standing bypass of whatever that method later does. Justifications are machine-checked, not prose. A contract-phase entry must name a strictly older release as the one that introduced the object, which mechanically prevents expand and contract landing in the same release. A never-released entry must additionally name the **database object** it is about, and that claim is checked by searching the source of every stable release tag for it. The version alone is an author's assertion the gate cannot check — an object that shipped in `1.2.2` could be declared as introduced in `2.0.0` and the version rule would still clear it, because no stable `2.0.0` tag exists. Naming the introducing commit only relocates the assertion, since any recent untagged commit satisfies it; searching the released source checks the claim itself. The claim also has to be bound to the object the entry actually operates on. A schema operation names its object directly, so the claim must equal it. Raw SQL is read at the operand position — the identifier following `drop table`, `alter table`, `delete from`, and the like — rather than anywhere in the statement, because matching the object anywhere accepts it appearing in a trailing comment while a different table is dropped. Reconstruction is what makes that position readable, resolving constants, interpolation holes, table quoting, schema qualification, and index-table naming conventions; the interpolation case in particular required recursing into the holes, without which `$"drop table {quotedTable}"` reconstructed to `drop table ?`. Every operand in the statement must be the claimed object rather than only the first, so a batch that drops the authorized table and then a second, unauthorized one is rejected instead of being covered by one claim, and an operand that cannot be read is a finding rather than a pass. Without that binding, changing the constant that names the dropped table would leave the statement classification, the authorization, and the claim all unchanged while dropping something else entirely. Stable tags stop at `v1.2.2`, and none of the eight registered objects appears in that tree. The check fails closed when the released source cannot be read or no stable tags exist, since treating "no tags" as "nothing released" would turn the strongest rule into the weakest; CI therefore checks out with `fetch-depth: 0`. Prerelease tags are excluded deliberately: upgrading from a preview or RC is not a supported path, and that is a stated boundary rather than an oversight. `UninstallAsync` is exempt, and only `UninstallAsync`, because uninstall is not an upgrade path. Because the gate finds migrations by folder convention while Orchard finds them by registration, that convention is verified: every type passed to `AddDataMigration` must appear in the scanned surface. Scope boundary: destructive DDL executed from a background task, recipe, feature event handler, or service is outside this gate and is disclosed as such in the production-support documentation.
- **W15.3 N-1 rolling-upgrade test:** old and new schema/code versions running against one database, both healthy. This is what makes the zero-downtime claim in the deployment docs true rather than aspirational.
- **W15.4 Canary + rollback runbook**, exercised in CI, not just described.
- **W15.5 Telephony command handler must drain on shutdown**, not cancel — cancelling in-flight commands mid-call during a deploy drops live calls.

### W16 — Residual distributed and data hygiene
Closes: dist F14, F15, F16, F18, F19, F22; data F-08, F-10, F-11, F-13, F-14, F-15, F-16; plus the Elasticsearch verification.

- **W16.1** Run call-teardown services only on terminal events (currently every event runs all of them).
- **W16.2** Hub group-join sequences must not use `Context.ConnectionAborted` — partial group membership on a flaky client silently drops that agent's events. Establish and test one hub cancellation convention.
- **W16.3** Readiness gate before the ARI listener starts on shell activation.
- **W16.4** `ContactCenterEventMetric` daily counter: replace the hot-row read-modify-write with an append-and-rollup (or atomic increment). At tier-1 rates this row is a serialization point.
- **W16.5** `AgentSession` heartbeat must not be a full-document CAS write; bound `ListAsync` calls; make the retention cutoff scan index-seekable.
- **W16.6** Reduce sequential store round-trips inside the reservation lock; add lease renewal for long operations.
- **W16.7** Column sizing correction (`nvarchar(261)`, `nvarchar(128)`).
- **W16.8** Add a test proving no Contact Center correctness path depends on Elasticsearch (the support matrix asserts this; nothing enforces it). **DONE.** A search dependency can arrive through three independent mechanisms, and an oracle covering one is blind to the others. A CLR reference is the compile-time mechanism: the transitive reference closure of every shipped Contact Center, Telephony, Asterisk, and DialPad assembly is walked and any Elasticsearch or OpenSearch client reached at any depth fails the build, and direct references are additionally checked against a wider list covering Lucene and the Orchard search and indexing abstractions. An Orchard feature dependency is the runtime mechanism and is a *string* in a manifest with no CLR reference at all, so an assembly-only gate would stay green while a feature pulled an entire search module into every tenant that enabled it; a manifest gate and a real-tenant enabled-feature-closure gate cover it. The closure is read with `MetadataReader` rather than `Assembly.Load`, because loading forces every transitive dependency to resolve and swallowing a load failure would let a violation hide behind an intermediate assembly, and traversal completeness is asserted rather than assumed. The direct half is deliberately not transitive: measurement showed the closure legitimately reaches an embedded Lucene index through shared libraries depended on for unrelated reasons, and an in-process index needs no cluster and no operator action, so banning it transitively would report a dependency the module does not have and would turn the gate into something engineers suppress. Measurement also proved `OrchardCore.Indexing` is enabled by the platform baseline rather than by any Contact Center feature, so the enabled-feature gate targets search *engines* and reports the cause of every enabled feature it rejects. A further fact pins the directory-driven assembly discovery to an exact set, so the gate cannot pass vacuously by finding nothing. Static analysis alone cannot see a correctness path that reaches a cluster through an ordinary HTTP client, so a runtime fact executes routing selection, assignment through a real reservation, outbox dispatch, and provider ingest through the normalized voice-event seam every PBX adapter funnels into (including its replay suppression) against persisted state inside a real supported-profile tenant, with every outbound HTTP request in the process recorded and required to be empty; the recorder proves it is attached by observing a self-test request rather than assuming attachment. Executing those paths immediately found a pre-existing defect that made every successful outbox dispatch throw, which the existing mock-session tests could not reproduce; it is fixed and pinned against a real store. What remains unproved is behaviour with the search binaries physically absent, which needs a packaging harness and is disclosed as a follow-up rather than claimed.

- **W16.9** Decide the fate of the generic provider webhook ingress. Executing W16.8's runtime fact found that `IProviderVoiceWebhookAdapter` has **no concrete implementation anywhere in the product**: only the contract, the abstract `HmacProviderVoiceWebhookAdapterBase`, and `ProviderVoiceWebhookProcessor` ship, and every implementor is a test fake. `ProcessAsync` therefore returns `UnknownProvider` for every request in every supported profile. Both shipping PBX integrations ingest through the normalized `IProviderVoiceEventService` seam instead. Either an adapter is a missing provider integration and must be built and gated, or the generic webhook ingress is unused surface that falls under the legacy-and-cleanup mandate and must be removed rather than left as an HMAC-verified endpoint no request can ever satisfy. Shipping an authenticated public ingress with no implementation behind it is the worse of the two outcomes, so this must be resolved before GA rather than deferred.

### Additional CI gates

| Gate | Runs | Proves |
|---|---|---|
| `gate-recording-dual-channel` | nightly + release | recorded audio has separated agent/customer channels |
| `gate-capability-implemented` | PR | every capability bit is implemented by an in-tree provider and exercised by a test, or listed as reserved |
| `gate-erasure-proof` | nightly + release | erasure API deletes encrypted media bytes, transcripts and pointers; legal hold honoured; deletion receipt audited |
| `gate-config-validation` | PR + startup | all options validated on start; production refuses known development secrets |
| `gate-origination-authenticity` | PR | forged origination markers are rejected |
| `gate-event-journal` | nightly | induced channel saturation loses zero lifecycle events |
| `gate-headless-closure` | PR | headless feature set activates with zero UI features |
| `gate-feature-dependency-audit` | PR | used services declared (enforced); declared dependencies used (not enforceable — see W11.4) |
| `gate-public-api-approved` | PR | public surface matches the approved baseline |
| `gate-domain-invariants` | PR | no public setters on aggregates; illegal transitions rejected |
| `gate-migration-additive-only` | PR | no unauthorized destructive migration step; no unreadable migration SQL; every register entry authorizes exactly one step; every justification supported by `VersionPrefix` and stable release tags; every registered migration inside the scanned surface |
| `gate-upgrade-n-1` | nightly | N-1 and N run against one database |
| `gate-a11y-all-views` | PR | axe clean across all Contact Center views |
| `gate-no-elasticsearch-dependency` | PR | no shipped assembly reaches a search-cluster client transitively or a search/indexing API directly, no Contact Center feature manifest declares a search-backed dependency, no supported profile enables a search engine in a real tenant, and routing, assignment, dispatch and ingest execute without issuing any outbound HTTP request (runs inside the Fresh Tenant Activation job) |
| `gate-crm-review-closed` | release | W13 findings triaged and closed or accepted |

---

## 12. What a re-review can and cannot score 10/10

This section exists because the honest answer to "will this make everything 10/10?" is **partly**.

| Dimension | After W0–W9 | After W0–W16 (Track A) | Ceiling cause |
|---|---|---|---|
| Architecture | 7.5 | **9.0** | Residual: the domain model is being retrofitted, not designed from scratch. |
| Code Quality | 8.0 | **9.0** | — |
| Security | 7.5 | **9.0** | Requires an external penetration test, and erasure (W17) proven end-to-end, to go higher. |
| Reliability | 7.0 | **8.5** | Single PBX and single app node cap blast-radius containment; the W1 cutover carries residual risk until shadow replay is clean. |
| Testing | 8.0 | **9.0** | 10 requires sustained mutation-score and chaos evidence over time, not a one-off gate. |
| Documentation | 8.5 | **9.5** | 10 requires rehearsed drills behind every operational claim, not prose. |
| Operational Readiness | 7.5 | **9.0** | 10 requires production telemetry from real customers. |
| **Scalability** | 5.0 | **6.5** | **Structurally capped — see below.** |
| **Contact Center Domain** | 6.5 | **7.0** | **Structurally capped — see below.** |

> **Scores revised down 2026-07-26** after the independent challenge graded the original table's honesty **4/10**. The original claimed "7 dimensions reach 9.5–10". That was aspirational: it assumed every gate would be both implemented *and* falsifiable, while ~20 ledger entries still had blank gate values and several exit criteria were unmeasurable (§14 findings 4 and 5). Security and Reliability in particular cannot be near-perfect while subject erasure is a dead code path (W17) and the W1 cutover is unproven. **No dimension is claimed at 10.** These numbers are a target to be *earned* by passing gates, not a forecast.

**Scalability cannot reach 10/10 in this release, by construction.** The release topology is one application node. A reviewer asking "does this work at 100,000 users?" gets the answer "this is certified for 100 agents on one node." No amount of code quality changes that. Reaching 10 requires Track B.

**Contact Center Domain cannot reach 10/10 either.** §3 defers skill proficiency and bullseye relaxation, per-channel capacity, virtual hold, chat/email/SMS ingress, predictive dialing, WFM, and QM. Against Genesys/NICE/Five9/Amazon Connect, a voice-only ACD without those is a competent v1, not a 10.

**The choice is explicit:**
- **Track A (W0–W16)** — ship a genuinely production-ready single-node voice contact center. Seven of nine dimensions reach 9.5–10. Two are honestly capped. This is achievable and defensible.
- **Track B** — required to lift the remaining two. Do not start it before Track A completes; it multiplies surface area over an unhardened base, which is exactly the mistake that produced RC-1.

### Track B (post-Track-A, to lift the capped dimensions)

- **B1 — Multi-node certification.** The W3 work makes correctness *possible* on multiple nodes; B1 *proves* it: real multi-process, multi-container tests with induced partitions, Redis failover, and node kills. Flips the support matrix to multi-node production.
- **B2 — Partitioned routing engine.** Replace the per-queue lease with partitioned/sharded assignment so routing throughput scales with node count instead of being serialized per queue. Prerequisite for the 1,000+ agent tier.
- **B3 — Channel completeness.** Chat, email, and SMS ingress on the W10.6 open channel model, with per-channel agent capacity and per-channel presence.
- **B4 — Routing sophistication.** Skill proficiency levels, bullseye/progressive relaxation, service-level-driven and predictive routing.
- **B5 — Domain gap table closure.** Virtual hold, EWT and position announcements, max queue depth and overflow, per-outcome retry scheduling, local presence, list recycling, campaign quotas, occupancy/adherence/FCR.
- **B6 — WFM and QM** — forecasting, scheduling, adherence; scorecards, evaluations, screen recording.
- **B7 — Predictive dialing** with a real in-tree abandonment statistics provider and safe-harbor playback.
- **B8 — Multi-region and data residency enforcement.**


---

## 13. Amendment progress ledger (W10–W16)

Same rule as §10: a box is ticked only when its named gate passes in CI.

- [ ] W10.1 Aggregates de-anemized; illegal transitions rejected `gate:gate-domain-invariants`
- [ ] W10.2 Value objects replace primitives; one canonical E.164 parser `gate:gate-domain-invariants`
- [ ] W10.3 `VoiceContactCenterCallRouter` decomposed into a testable pipeline `gate:`
- [ ] W10.4 Catalog CRUD duplication collapsed `gate:`
- [ ] W10.5 `ProviderVoiceEvent` immutable `gate:gate-public-api-approved`
- [ ] W10.7 Mega-files decomposed `gate:`
- [ ] W10.8 Scoped report factory mutable-state leak fixed `gate:`
- [ ] W10.9 Provider-neutral transfer targets and outcomes `gate:`
- [ ] W11.1 Headless/admin feature split `gate:gate-headless-closure`
- [ ] W11.2 `ContactCenter.Admin` real or deleted `gate:gate-feature-dependency-audit`
- [ ] W11.3 `Analytics` declares its capability closure `gate:gate-feature-dependency-audit`
- [x] W11.4 Feature-dependency audit gate live `gate:contact_center_feature_activation_matrix.yml#fresh-tenant-activation, pr_ci.yml#build_test` — ContactCenterFeatureDependencyAuditTests` (3 tests) + `ContactCenterOptionalDependencyTests` + `CallControlAuthorizationBoundaryTests`. "Declared dependency is used" half is disclosed as not dynamically enforceable.`
- [ ] W11.5 Recipes and deployment steps for all CC configuration `gate:`
- [ ] W11.6 Voice webhook body streamed `gate:`
- [ ] W11.7 Write-path rules out of display drivers `gate:`
- [ ] W11.8 Public API baseline approved `gate:gate-public-api-approved`
- [ ] W11.9 Agent-workspace N+1 removed `gate:gate-explain-budget`
- [ ] W12.1 Dual-channel recording `gate:gate-recording-dual-channel`
- [ ] W12.2 Supervisor take-over implemented, or capability stays prohibited `gate:gate-supervision-audio-proof`
- [ ] W12.3 Codec negotiation, SRTP/DTLS enforcement, TURN credential lifetime `gate:`
- [ ] W12.4 Explicit call state machine; live-ARI reconciliation proven `gate:`
- [ ] W12.5 Idempotency keyed on event identity `gate:`
- [ ] W12.6 Orphan bridge/channel/snoop sweeper `gate:gate-restart-drain`
- [ ] W12.7 PBX failover across multiple ARI endpoints `gate:gate-dependency-failure`
- [ ] W12.8 Media quality telemetry (MOS, jitter, loss) `gate:`
- [ ] W12.9 DTMF/IVR collection, music-on-hold, call park, CLI presentation `gate:`
- [ ] W12.10 STIR/SHAKEN attestation passthrough `gate:`
- [ ] W12.11 Channel bound load-derived, saturation metric alerting `gate:`
- [ ] W13 Omnichannel/CRM layer reviewed; findings closed or accepted `gate:gate-crm-review-closed`
- [ ] W14.1–W14.3 Product-wide accessibility and localization `gate:gate-a11y-all-views`
- [ ] W15.1 Document/event upcasters `gate:`
- [x] W15.2 Additive-only migration enforcement `gate:pr_ci.yml#build_test` — MigrationAdditiveOnlyGuardTests` (85 executed cases, 0 failed) in the unit test project, run by `pr_ci.yml`, `main_ci.yml`, `preview_ci.yml`, and `release_ci.yml`, all of which now check out with `fetch-depth: 0` so the release-tag justification check can run. Three oracles (schema-builder operations, SQL arguments, command-text assignments) over a Roslyn syntax tree, with statement reconstruction across concatenation, interpolation, single-assignment locals, and read-only query-builder composition; per-occurrence contract register with machine-checked contract-phase and never-released justifications; a separate reviewed-dynamic-SQL register for statements the gate cannot read; and a discovery-coverage fact pinning the folder convention to Orchard's registration. Falsified with eleven probes — an unregistered `DropTable` inside an already-registered method, concatenated and interpolated `drop`, an opaque statement variable, a `RenameColumn`, a renamed `UninstallAsync`, a query builder composed with `Trail`, a `VersionPrefix` regressed onto a shipped release, a stale register entry, a weakened contract-phase comparison, and removal of the rename operations from the ban list — each of which failed the suite and was reverted.`
- [ ] W15.3 N-1 rolling upgrade proven `gate:gate-upgrade-n-1`
- [ ] W15.4 Canary and rollback exercised `gate:`
- [ ] W15.5 Telephony command handler drains on shutdown `gate:gate-restart-drain`
- [ ] W16.1–W16.3 Teardown scoping, hub cancellation convention, listener readiness `gate:`
- [ ] W16.4–W16.7 Metric hot row, heartbeat write, bounded scans, lock leases, column sizing `gate:gate-explain-budget`
- [x] W16.8 No Elasticsearch in any correctness path `gate:contact_center_feature_activation_matrix.yml#fresh-tenant-activation` — ContactCenterSearchEngineIndependenceTests` (6 test methods, 8 executed cases) in the feature-activation project, run by `contact_center_feature_activation_matrix.yml` on every `src/**` change. Transitive PE-metadata closure bans search-cluster clients; direct references additionally ban all search/indexing APIs; manifests may not declare a search-backed feature dependency; each supported profile is started in a real tenant and its enabled-feature closure must contain no search engine; discovery is pinned to an exact assembly set and traversal completeness is asserted; and the correctness paths are executed against persisted state in a real tenant with outbound HTTP recorded and required to be empty.`
- [ ] W17 Subject erasure + media lifecycle proven end-to-end `gate:gate-erasure-proof` **(blocks `recording`)**
- [ ] W18 Options validated on start; production rejects development secrets `gate:gate-config-validation`
- [ ] W12.12 Signed origination markers `gate:gate-origination-authenticity`
- [ ] W12.13 Durable pre-buffer event journal; zero lifecycle loss under saturation `gate:gate-event-journal`
- [ ] Every gate has a versioned test specification with numerical thresholds `gate:gate-ledger-evidence`

---

## 14. Independent challenge and consensus (2026-07-26)

This plan was challenged by an independent reviewer on a different model family, given repo access and instructed to find what the plan fails to close. Its verdict on the plan as written was **"No — does not produce a production-ready platform"**. Every claim below was independently verified against the code before acceptance. Findings 1, 6 and the erasure finding are defects **in this plan**, not merely in the product.

### Accepted — plan corrected in place

1. **W1.1 was architecturally impossible.** Corrected above. `ProviderVoiceEventService` carries 9 Contact Center-specific dependencies; moving it wholesale into `Telephony.Core` would have inverted the layering. Now split into provider-neutral ingress mechanics plus a Contact Center projection, with shadow-replay cutover.
2. **W3.1's lease key would have caused cross-tenant event leakage.** Corrected above. Tenant must stay ownership *metadata*, never part of the key.
3. **Sequencing contradictions.** The risk table said "ship the W8.5 reset tool first" while scheduling it in Wave 8; W1's mitigation named ARI cassettes and property tests that were scheduled four waves later; W15.3 (N-1 proof) landed after the W2 breaking migrations it exists to protect. **Resolved in §6:** W5.4 cassettes and W5.6 property tests move to Wave 2 as W1 entry criteria; W8.5 reset tooling and W15.2/W15.3 move to Wave 1 as W2 entry criteria. **A breaking data change may not merge before the tooling that makes it recoverable.**
4. **W15.2's destructive-migration ban needs an explicit contract-step exception**, otherwise it forbids the very "contract" phase of the expand-migrate-contract policy W2 depends on. The gate must permit destructive steps only when accompanied by a declared, reviewed contract-phase marker. **Resolved in W15.2:** the register carries a `ContractPhase` justification that is accepted only when the object was introduced in a strictly older release, so the contract phase is permitted while expand-and-contract-in-one-release remains impossible.
5. **Track A positioning must be stated, not implied.** Track A is a voice-only ACD for customers explicitly accepting no omnichannel, no proficiency/bullseye routing, no virtual hold, and no WFM/QM. All language implying parity with Genesys/NICE/Five9/Amazon Connect is removed. §12 already says this; §1 must not contradict it in marketing terms.
6. **W10.6 (open channel model) is scope creep — deferred to Track B.** The justification was "avoid a second breaking change", but breaking changes are permitted pre-GA, so that is not sufficient reason to build a generic extension system before a single concrete channel exists to inform its routing, capacity, reporting, retention and consent requirements. Removed from Wave 2; moves to B3 as the first task of the first channel vertical slice.

### Accepted — new work added

7. **W17 — Subject erasure and media lifecycle (BLOCKING for the `recording` capability).** The single most serious product finding of the entire review, missed by all eight specialists and by the first challenge. `IRecordingAccessGovernanceService.EraseAsync` clears the interaction pointer, stamps `RecordingErasedUtc` and emits a `RecordingErased` event (`RecordingAccessGovernanceService.cs:112-141`) — but a repository-wide search finds **zero callers of `EraseAsync` outside its own definition, and zero consumers of the `RecordingErased` event.** The encrypted media store does expose deletion (`LocalEncryptedRecordingMediaStore.cs:81-102`), and nothing calls it. Separately, retention purges **only** `InteractionEvent` rows (`ContactCenterRetentionService.cs:23-49`) and never the media referenced by `RecordingRetainUntilUtc`. **Net effect: recorded call audio is never deleted, by any path, ever.** GDPR Article 17, CCPA deletion, and every contractual retention commitment are currently unimplementable. Required: an authenticated erasure API and workflow that calls `EraseAsync`; a durable media-deletion command routed through the outbox with retry and reconciliation; a retention sweeper that deletes expired media and honours legal hold; transcript erasure; tenant-decommission cleanup; a deletion receipt in the audit trail; and an integration test proving the encrypted bytes are gone from the store.
8. **W18 — Configuration validation, fail-closed.** Retention and health options are registered with bare `Configure<T>` and no validation (`ContactCenter/Startup.cs:72-90`). Add `IValidateOptions<T>` with `ValidateOnStart` for every Contact Center, Telephony and provider options class, and a production startup validator that **refuses to start** on known-development values — including the checked-in Coturn development secret `static-auth-secret=crestapps-dev-turn-secret` (`Coturn/turnserver.conf:8`). Externalize hard-coded timing and lease values.
9. **W12.12 — Signed origination markers.** `AsteriskRealtimeVoiceEventMapper.cs:43-47` infers origination ownership from the mere *presence* of a channel variable, with no signature. Anything able to set that variable can forge ownership. Replace with an HMAC over tenant, call and command identity, verified before ownership is accepted. This is the correct closure of telephony 4.5, which W12 claimed but did not own.
10. **W12.13 — Durable pre-buffer event journal.** Telephony 4.2 is not closed by reconciliation: reconciliation recovers *current* state but cannot reconstruct the intermediate Hold/Resume sequence lost when the bounded channel saturates. Journal normalized events durably *before* the bounded channel, account for overflow explicitly, and gate on zero lifecycle loss under induced saturation.
11. **W5.4/W5.5 must cover DialPad, not only Asterisk.** DialPad is a supported provider profile; contract and reconciliation tests currently stop at Asterisk cassettes.
12. **W9.1/W12.4 — DialPad timestamp parsing is timezone-unsafe.** `DateTimeOffset.TryParse(value.GetString(), out var parsed)` (`DialPadTelephonyProvider.cs:966`) supplies neither `CultureInfo.InvariantCulture` nor `DateTimeStyles.AssumeUniversal | AdjustToUniversal`, so an offset-less provider timestamp is interpreted in the **host's** local timezone — corrupting call duration and report bucketing, and doing so differently on either side of a DST boundary. Require offset-bearing timestamps or parse with explicit universal styles; test both DST boundaries.
13. **W0.5 must include container image scanning** (Trivy/Grype) for the Asterisk image; the task listed SBOM, secret scanning and license inventory but omitted it.
14. **W0.4 — docs validation must run on code PRs**, not only doc-path PRs, or code changes silently invalidate documented claims. *(Resolved in W0.4: `validate_docs.yml` now triggers on `src/**` and `tests/**`.)*
15. **Legacy inbound endpoint disposition (orchard F-11)** must be named explicitly: sunset or rename it, and require a strict E.164-or-ambiguous routing result rather than a best-effort match.

### Accepted — evidence model strengthened

16. **A workflow job name is not evidence.** W0.3 only proved that a ticked checkbox referenced an *existing* job — not that the job tests the claim. That is RC-3 recurring one level up. **New rule (supersedes AD-6):** every gate requires a versioned test specification stating topology, inputs and injected faults, the invariant asserted, the observation source, numerical pass thresholds, and prohibited mocks/skips. `gate-ledger-evidence` validates that each gate has a specification and that the specification's thresholds are the ones the job actually asserts.
17. **Blank gate values are now release-blocking.** ~20 ledger entries in §13 carry `gate:` with no value. Any entry without a named gate and specification cannot be ticked.
18. **Named-but-unspecified gates must be quantified:** `gate-capacity` (duration, workload mix, error budget, p95 ceiling, minimum retained capacity), W2.3's "5M rows return to steady state" (hardware, distribution, runtime, rate), W5.7's "publish p95" (thresholds, not just publication), and the audio proofs (unique per-leg stimuli, channel-mapping validation, real provider path — a file-existence assertion must not pass).
19. **`gate-crm-review-closed` must not be satisfiable by accepting every finding.** Risk acceptance requires a named independent reviewer and a dated, written rationale per accepted finding.
20. **Capacity caps need a commercial floor.** §2.3 says to lower published caps to measured values, with no lower bound — a system could "pass" at 5 agents. Define the minimum viable commercial capacity below which the release does not ship.

### Rejected / retained as-is

- **Keep the Telephony module as a standalone SKU.** Re-confirmed by both challenges.
- **Instrument after the seams stabilize (AD-5).** Re-confirmed; instrumenting duplicate stacks first would institutionalize identifiers that W1 then reworks.
- **Track A before Track B.** Re-confirmed as correct engineering: hardening authority and persistence before multiplying channel surface is precisely the discipline whose absence produced RC-1. The challenge agreed this ordering must not be reversed.
- **One application node cannot earn a 10/10 scalability score.** Re-confirmed against `support-matrix.v1.json:29-47`.
