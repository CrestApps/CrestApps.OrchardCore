# Contact Center — Plan 2: Single-Node Completion & Credibility

> **Status:** Active, executable. This is the **second** durable Contact Center plan, tracked separately from [`PLAN.md`](PLAN.md). Where `PLAN.md` tracks the R0–R9 architectural remediation program (R0–R7 complete; **R8** end-to-end proof and **R9** advanced capabilities not done), **Plan 2** is the functional-completion and credibility plan: it makes the bundled Asterisk provider actually carry live browser audio and run a *credible* contact center on a **single node first**, then feeds the distributed work back into `PLAN.md` R8.
>
> **How to use this document:**
>
> - Read this alongside `PLAN.md`. `PLAN.md` remains the master architecture/progress ledger and the R8 release gate; Plan 2 does not supersede it. Plan 2's distributed work (Part 8) *feeds* R8.
> - Start at the lowest incomplete wave in **Sequencing**; keep the **Progress status** section current after each meaningful change.
> - The **Cross-cutting principles** are non-negotiable and apply to every part. The most important is **CC-1 (tenant isolation & Orchard Core infrastructure conformance)**: this is a multi-tenant platform and every new service must be an extension of Orchard Core's own distributed infrastructure with zero cross-tenant leakage.
> - Never write competitor contact-center product names in code, comments, identifiers, or public docs. Asterisk, coturn, SIP.js, WebRTC, and DialPad are first-party integrations/open technologies already present in the repository and may be named.
> - Respect the layer boundary: **CRM (Omnichannel) owns business work data, Contact Center owns orchestration, Telephony owns media execution.** `OmnichannelActivity` is the universal work item; `Interaction` is communication history for one attempt and never owns workflow or disposition.

## Why Plan 2 exists

The 2026-07-16 independent Engineering Review Board found a strong orchestration layer wrapped around a **hollow execution layer**: no browser audio (`soft-phone.js` acquires the microphone but has no `RTCPeerConnection`/SIP), a no-op Asterisk `ConnectToAgentAsync`, no recording/monitoring/transfer/conference implementations, no Asterisk inbound routing, plus a set of single-node consistency, tenant-isolation, and test-honesty gaps. The board's capability claims in the public docs and the durable `support-matrix.v1.json` oversell what the code can actually do. Plan 2 closes that gap and proves it with a **runnable single-node end-to-end audio test**.

## Multi-model challenge record (how this plan was ratified)

Per the product owner's requirement, every part and the whole plan were independently red-teamed by two GPT-5.6-class models with different primary lenses, then reconciled and re-confirmed:

- **Model A — `gpt-5.6-terra`** (distributed systems / security / test honesty). Draft gate: **REJECT** (mis-sequencing: state authority and authorization correlation must come first). Confirmation gate on the reconciled plan: **RATIFY-WITH-CHANGES → RATIFY** after 5 precise additions.
- **Model B — `gpt-5.6-sol`** (telephony / WebRTC / SIP / Asterisk / Orchard-Core fit). Draft gate: **RATIFY-WITH-CHANGES** (credential lifecycle, durable ARI topology, correct recording/monitoring/conference primitives). Confirmation gate: **RATIFY-WITH-CHANGES → RATIFY** after 2 precise additions.

All 7 final changes are incorporated below. Consensus outcome: **both models RATIFY the plan with these changes applied.** The point-by-point consensus and dissent resolution is in the final section.

## Guiding decisions (locked with product owner)

1. **Single-node full functionality is the immediate goal.** Distributed correctness is secondary and explicitly sequenced (Part 8, which feeds `PLAN.md` R8). No single-node work may *depend* on Redis/backplane to be *correct* — but all distributed work, when it lands, is built on Orchard Core's own primitives (CC-1).
2. **Provider-agnostic browser-media abstraction; Asterisk is the first concrete WebRTC adapter.** DialPad stays external-device call-control for GA (its no-op connect is architecturally defensible — it owns the agent device); its SDK/embed WebRTC is a later adapter.
3. **Exit bar = a runnable single-node E2E proof of real bidirectional audio** between two browser agents (or agent + PSTN/softphone leg) through a bundled Asterisk, plus supervisor monitor/whisper/barge and recording verified against that same live call — asserted on **audio content**, not call state.
4. **Only content ratified by ≥2 independent GPT-5.6-class models is executable.**

---

# Cross-cutting principles (apply to EVERY part — non-negotiable)

## CC-1 — Tenant isolation & Orchard Core infrastructure conformance (HARD CONSTRAINT)

This is a multi-tenant Orchard Core platform. Every new or changed service is an **extension of Orchard Core's own infrastructure** and must not leak data across tenants.

**Build on Orchard Core primitives, never a parallel stack.** Use `IDistributedCache`, `IDistributedLock`, the `OrchardCore.Redis` feature (per-tenant Redis-backed cache/lock/data-protection), and the existing tenant-qualified SignalR backplane `CrestApps.OrchardCore.SignalR.Redis` (`RedisBackplaneStartup.cs` already applies a tenant channel prefix — **consume it, do not duplicate it**) with `TenantSignalRGroupName`. **Banned in application code:** raw `StackExchange.Redis`/`ConnectionMultiplexer`, custom global locks/semaphores, any parallel cache/lock stack, and unqualified process-wide `IMemoryCache` for tenant data. (The approved OrchardCore Redis adapter itself legitimately depends on `StackExchange.Redis`; the ban targets new application-level use — see Part 6 CI guards.)

**No shared static mutable state.** Any `static` field/property, process-global registry, or root-container singleton holding per-tenant data is a cross-tenant leak. Correct Orchard Core pattern per new stateful component (ratified by Model B):

| New stateful component | Correct Orchard Core pattern |
| --- | --- |
| Browser-media adapter registry | **Browser-page-local** JS registry (`soft-phone.js`); server-side adapters registered through **shell DI**. No static server registry. |
| SIP credential cache | Prefer a **stateless scoped issuer**. If state is required: **tenant-keyed `IDistributedCache` + `ISignal`**; never cache plaintext globally. |
| Agent→endpoint map | Tenant **YesSql** source of truth via shell-scoped service; optional tenant-keyed distributed cache + `ISignal`. |
| ARI/AMI connection manager + event listener | **Per-shell singleton** (current `AddSingleton` in `Asterisk/Startup.cs` is appropriate); multi-node ownership via `IDistributedLock`. |
| Provider settings / capability cache | Read tenant `ISiteService`; if cached, **per-shell singleton or tenant-keyed `IDistributedCache` + `ISignal`**. |
| TURN credential issuance | **Stateless scoped** service using `IClock` + a tenant-protected secret; cache only replay/revocation state in distributed cache. |
| Media-session registry | Tenant-local durable `CallSession` as truth; active UDP/WebRTC handles in a **per-shell singleton**. Never static, never in distributed cache. |

*Confirmed non-issues:* `Random.Shared` for SSRC (`AsteriskContactCenterVoiceMediaSession.cs`) holds no tenant state; existing SignalR connection registries and the ARI manager are already shell-scoped.

**Tenant-scoped caching only.** Any cache of provider settings, agent endpoints, credentials, or capabilities is tenant-keyed (or shell-scoped) and invalidated via `ISignal`/tenant-scoped mechanisms — exactly as the rest of the codebase does.

**Shared-PBX multi-tenancy is a real cross-tenant risk today.** Evidence in the current Asterisk module:

- ARI application name is a **global constant** `crestapps-telephony` (`AsteriskConstants.cs`; `AsteriskSettingsUtilities.cs`).
- The listener subscribes with **`subscribeAll=true`** (`AsteriskSettingsUtilities.cs`) → one tenant's listener can receive unrelated PBX events.
- `ShellSettings.Name` only qualifies **outbound** SignalR (`AsteriskRealtimeVoiceEventDispatcher.cs`); it does not authenticate inbound event ownership (`AsteriskRealtimeVoiceListener.cs` dispatches into a captured shell without validating an event tenant marker).
- The media provider **trusts a request-supplied `externalHost`** (`AsteriskContactCenterVoiceMediaProvider.cs`) and permits **unqualified Asterisk-generated bridge IDs**.

**Exact fix (release-blocking for Parts 3–4). Event ownership is resolved through a persisted binding, because arbitrary channel variables are not present on every ARI event:**

1. Generate a unique PBX-safe **application name per tenant + provider** (replace the global constant); set **`subscribeAll=false`**.
2. On the **initial `StasisStart`**, validate the application **and** a trusted Stasis argument / channel variable that carries the tenant marker (assigned by the ingress dialplan and every ARI `originate`, never from client input).
3. **Persist the channel→tenant binding** (and the owning `CallSession`) durably as part of the idempotent inbound claim (see Part 3b).
4. **Bind deterministic child channel/bridge/leg IDs** to that tenant as they are created.
5. **Resolve every later event through that persisted binding**; reject events referencing unknown/unowned resources.
6. **Verify `CallSession` ownership before every ARI mutation**, tenant-prefix every PBX identity (endpoint/auth/AOR, dialplan context, channel, bridge, snoop, supervisor leg, recording name, storage path, TURN identity), and resolve the media host from validated tenant config only.
7. On bounded-ingestion overflow (Part 3), **disconnect and reconcile — never silently drop lifecycle events.**

Also honor **per-tenant provider configuration** (each tenant may point at a different Asterisk/DialPad) and never eagerly read validated options in UI/setup paths — unconfigured providers appear **unavailable**, not crash.

## CC-2 — Provider truth (never trust an HTTP accept as state)

- Never advance interaction/call-session state on an ARI HTTP request accept; only on **confirmed provider events**.
- Persist a **durable command** and derive **deterministic resource IDs** (customer channel, agent leg, bridge, recording, supervisor leg) from the durable command id, so retries/reconnects are idempotent and reconcilable.
- **Preserve the canonical `ProviderCallId`.** The success projector currently overwrites it with the provider result (`AnswerProviderCommandTypeExecutor.cs`). The field ownership and this fix live in **Part A** (below), because Part 1b's authorization depends on the mapping.
- Every command has **reconciliation** for abandon/timeout/unregister/duplicate-`StasisStart`/bridge-rollback/ARI-disconnect/reconnect-adoption.

## CC-3 — Honest verification (real audio, no mock exit bar)

- The GA exit proof asserts **audio content**: inject distinct tones per leg, verify with Web Audio frequency analysis at each receiver, and decode recording contents — not merely call state or file existence.
- Cover **direct-ICE** *and* **forced-TURN** (`iceTransportPolicy:"relay"`) paths.
- Run on a **dedicated/self-hosted media runner** with a trusted test CA, fixed ports, network namespaces, and full Asterisk/coturn/browser logs. Deterministic REST/signaling checks run per-PR; real-media runs nightly and are **release-blocking**. **No honest-mock fallback** for the exit bar.

## CC-4 — State authority before power

A minimal **canonical live-topology owner** exists before powerful call control ships: `CallSession` owns the live channel/bridge/recording/supervisor-leg IDs as **typed, CAS-protected fields with invariants** (Part A); `Interaction` is projection/history. The full `ContactCenterWorkState` consistency-root rewrite (ADR-1) stays deferred to Part 9.

## CC-5 — Alignment with `PLAN.md` R8 and the durable support matrix

Plan 2 pulls capabilities that `support-matrix.v1.json` currently **prohibits at GA** (`recording`, `monitor`, `whisper`, `barge`, `bidirectional-media`) and a topology it marks **non-production** (single application node) into scope. This is a deliberate, product-owner-approved envelope change, executed as a **gated revision of the control file**, not just prose:

- Add a **certified single-node production profile** (a new topology entry) with a **published capacity ceiling** (WS-10). It is the only single-node configuration customers may trust for live audio.
- Move `recording`, `monitor`, `whisper`, `barge`, and `bidirectional-media` from `prohibitedCapabilities` to `allowedCapabilities` for a new **gated Asterisk provider profile** — but **only after** Parts 2–4 land and the Part 6 runnable-audio + two-tenant proofs pass. Until then they stay prohibited.
- The **2–4-node commercial claim and any media-HA claim remain gated by `PLAN.md` R8** (redundant PBX/TURN). Part 8 feeds R8; it does not supersede it.
- The public support docs and `support-matrix.v1.json` are updated together so no claim is published ahead of its proof.

---

# Sequencing (critical path)

```
Wave 1 (parallel):  Part 0 (harness + two-tenant test harness) · Part A (typed state authority + tenant keys) · Part 1a (soft-phone BOLA — existing fields)
Wave 2 (parallel):  Part 1b/1c/1d/1e (CC + first-command authz — needs Part A) · Part 2 (WebRTC — needs Part 0)
Wave 3:             Part 3 (connect + idempotent inbound — needs 1,2,A); two-tenant adversarial test is a Part 3 ACCEPTANCE gate
Wave 4:             Part 4 (supervisor + recording + transfer/conference — needs 2,3,1d)
Wave 5 (parallel):  Part 5 (reliability/observability) · Part 6 (tests + runnable E2E + CI guards)
Wave 6:             Part 7 (docs + support-matrix reconciliation) ─> SINGLE-NODE GA
Post-GA:            Part 8 (distributed, feeds PLAN.md R8) · Part 9 (future architecture)
```

**Resolved model conflicts:**

1. **Gate label.** The draft was rejected-and-restructured; with all 7 confirmation changes applied, both models RATIFY.
2. **Part 1 vs state-authority order.** Split: the Telephony soft-phone BOLA fix (**1a**) lands immediately on **existing** ownership fields — `TelephonyInteraction.UserId` + `FindByCallIdAsync(userId, callId)` already exist (`TelephonyInteraction.cs`; `DefaultTelephonyInteractionStore.cs`) — so it has no dependency on new state. The Contact-Center authorization (**1b**) depends on Part A's typed `CallSession` mapping.
3. **WorkState scope.** Minimal typed `CallSession` ownership now (Part A); full `ContactCenterWorkState` root deferred (Part 9).

---

## Part 0 — Baseline, scope lock, controlled media runner, and the two-tenant harness

**Objective:** an actually-runnable single-node reference environment plus falsifiable audio and isolation proofs before feature code.

**Tasks**

- Reference `docker-compose` stack (dev/test only, not shipped in the module): **Asterisk 22 LTS pinned** with `res_http_websocket`, `res_pjsip_transport_websocket`, `res_pjsip`, `res_crypto`, `codec_opus`; **coturn**; the CMS web app. Commit config templates (`pjsip.conf`, `http.conf`, `ari.conf`, Stasis `extensions.conf`) with **tenant-namespaced** contexts/app names (CC-1).
- **Controlled media runner** (CC-3): trusted test CA, fixed ports, network namespaces, container log capture. Two scenarios: **direct-ICE** and **forced-TURN relay**.
- **Two-tenant adversarial harness (from Model A):** provision two tenants that share one Asterisk + one coturn, seed identical logical IDs, and expose fixtures that attempt cross-tenant route/bridge/monitor/record. This harness is built here in Part 0 so it can be used as a **Part 3 acceptance gate**, then enforced in CI in Part 6.
- Seed recipe for a single-node reference tenant with the Asterisk provider features (Voice, Voice.SoftPhone, RealTime, Availability, Routing, Queues, EntryPoints, Recording, Supervision) — respecting the Voice vs Voice.SoftPhone feature split.
- Author the E2E proof scenario (Playwright), initially red: two browser agents sign in; inbound call via Stasis → queue → offer → accept → **bidirectional tone-verified audio** → supervisor monitor/whisper/barge (audibility-verified) → recording (content-verified, retrievable) → disposition.
- Pin SIP.js at a fixed version (MIT; latest tag 0.21.2) + **SBOM**; add CVE scanning.
- Pin a Plan-2 baseline (commit, build, test counts) analogous to `R0-BASELINE.md`.

**Exit:** `docker-compose up` yields Asterisk+TURN+CMS; the tone-based E2E scenario and the two-tenant harness exist (red); baseline pinned.
**Node scope:** single-node. **Depends on:** none.

---

## Part A — Typed state authority, consistency & tenant-scoped keys (FIRST WAVE)

> Pulled ahead of security and media (Model A: mis-sequencing was the top draft defect). These race within a single node, so they are single-node GA, not distributed. Everything here is tenant-scoped (CC-1).

**Findings:** DIST-1, DIST-2, DIST-3, DIST-4 (High); DB-1 (High); plus CC-4 minimal ownership.

- **CC-4 typed ownership (Model A refinement):** make `CallSession` the canonical owner of live topology as **typed, CAS-protected fields with explicit invariants and unique constraints** — not opaque metadata. Fields: canonical `ProviderCallId`, owning interaction id, queue id, assigned agent id, durable command/fence id, and the channel / bridge / recording / supervisor-leg ids. Define **one transactional write protocol** for advancing them. **The `ProviderCallId`-overwrite fix (CC-2) lives here** (`AnswerProviderCommandTypeExecutor.cs`), before Part 1b consumes the mapping. `Interaction`/`TelephonyInteraction` become projection/history.
- **DIST-1 Interaction CAS:** add `CheckConcurrency => true` to `InteractionStore` (mirrors `CallSessionStore`). On `ConcurrencyException`, **retry in a fresh child `ShellScope`** — a YesSql concurrency failure invalidates the session; do not reload-and-retry in place.
- **DIST-2 AgentSession CAS + unique claim:** add CAS to `AgentSessionStore`; add a **tenant-scoped unique** active-session claim key on `AgentSessionIndex` via `ContactCenterMigrationSql.CreateUniqueIndexAsync` (as `CallSessionIndexMigrations` already does) so one live session per user; preflight/repair duplicates in the upgrade migration; guard heartbeat vs locked connect/disconnect races.
- **DIST-3 durable tombstone:** stop DELETE-on-success in `ProviderWebhookInbox`. Mark `Completed` and **retain a minimal tombstone** (provider, delivery hash, completed time, outcome — no PII payload) for a retention window; delete the payload separately; bounded cleanup task (lifecycle-fenced, Part 5).
- **DIST-4 atomic enqueue:** in `ActivityQueueService.EnqueueAsync`, persist queue-item state + event/outbox in **one** transaction/`SaveChanges`, and **dispatch only after commit**. A crash cannot leave an item without its event, nor emit an event for an uncommitted item.
- **DB-1 enum-vs-string SQL (broader than one line):** enum columns store `.Column<string>` **names** but several filters cast `(int)`. Fix and add DB-backed tests for `OmnichannelActivityStore.BuildBulkManageableContextAsync`, `BulkManageActivityFilterHandler`, and `DefaultContactActivityBatchLoader`; audit for any other raw enum comparisons.
- **Tenant keys:** all new cache/lock keys are tenant-qualified (CC-1).

**Exit:** store-backed concurrency tests prove single-writer wins on Interaction and AgentSession (fresh-scope retry); the typed `CallSession` invariants are enforced; duplicate webhook re-delivery is idempotent across a completed tombstone; enqueue is atomic under crash injection; bulk-manage returns correct rows on SQLite **and** Postgres.
**Node scope:** single-node (foundational for distributed). **Depends on:** none.

---

## Part 1 — Security & the shared call-control authorization boundary

**Findings:** SEC-1 (Critical), SEC-2 (High), SEC-3 (High).

**1a. Soft-phone object authorization (SEC-1) — immediate, no new state.** `TelephonyHub.ExecuteAsync` authorizes only coarse `UseSoftPhone`, then runs a **client-supplied `CallId`** against the provider with no ownership check. Fix using the **existing** `TelephonyInteraction.UserId` + `FindByCallIdAsync(userId, callId)`: resolve the call, confirm it is owned by/assigned to `Context.UserIdentifier`, reject with a redacted failure otherwise.

**1b. Shared call-control authorization boundary (SEC-1 root) — needs Part A.** Object authorization is a **single shared boundary invoked by ALL entry points** (Telephony hub, Contact Center hub, supervisor endpoints, and every `IContactCenterCallCommandService`/executor path), covering **every verb** including `Merge`, `Dial`, and each participant in a conference — not hub-only. For server-side-ACD calls it resolves ownership from Part A's typed `CallSession` mapping. The client submits a **logical/interaction id**; the server resolves the provider call/leg id — clients never drive raw provider IDs.

**1c. Typed, server-resolved transfer destinations (SEC-2).** Clients submit a **typed logical destination** (agent/queue/approved-external ref); the server resolves it to an E.164/endpoint. **Deny premium/emergency ranges**, enforce RBAC on external transfer, and **audit** every transfer. No client-supplied arbitrary E.164.

**1d. Per-queue supervisor entitlement (SEC-3).** `ContactCenterHub.WatchQueue` and supervisor engage (`SupervisorDashboardEndpoints.cs`) gate only blanket `MonitorContactCenter`. Add per-queue entitlement (reuse the R1 manager-owned-queue model) at **both** subscription **and** each operation.

**1e. First-command authorization for new manual dials (from Model A).** A manual outbound dial has **no prior `CallSession`**, so 1b's ownership resolution does not apply. Require the server to resolve the caller identity, the allowed originating endpoint, and the destination policy (reusing 1c's allow-list/deny rules), then **create an owned interaction/CallSession before provider execution**. No dial is dispatched for an unauthorized origin/destination.

**Exit:** behavioral tests through the **real authenticated hubs/endpoints** (not source-string): a second agent cannot hang up/transfer/DTMF/merge another agent's call; a supervisor cannot watch/engage a non-owned queue; a transfer or first dial to a non-allow-listed/emergency number is rejected; all fail-closed.
**Node scope:** single-node (equally required distributed). **Depends on:** 1a none; 1b–1e depend on Part A.

---

## Part 2 — Provider-agnostic browser media (WebRTC) + Asterisk adapter

**Findings:** TEL-1 (Critical). **Centerpiece.**

**Existing seams (reuse, don't reinvent):** `ITelephonyAudioProvider` (`AudioCapabilities`, `ConfiguredAudioMode`, `BrowserMediaAdapterName`), `TelephonyAudioMode {None,Browser,ExternalDevice}`, `TelephonyAudioModeResolver`. The soft phone calls `getUserMedia` but has **no `RTCPeerConnection`/SIP/WebSocket** — audio goes nowhere.

**Architecture (ratified primary): chan_pjsip WebRTC + browser SIP user-agent.** Media flows browser↔Asterisk (WSS + DTLS-SRTP + ICE via coturn), **not** through .NET. The in-process WebRTC gateway is **rejected for agent audio** (it turns the app into a media server: CPU/codec/scaling/security/node-affinity). Keep the existing external-media RTP foundation (`AsteriskContactCenterVoiceMediaSession`, `AsteriskRtpPacketCodec`) for **AI media streaming / server-side taps only**.

**Must-fix additions:**

- **PJSIP credential lifecycle (Model B, blocking):** "short-lived PJSIP credentials" is not an implementation — ARI **cannot** create endpoint/auth/AOR objects. Choose **PJSIP Realtime** (DB-backed endpoints) *or* pre-provisioning, plus **rotation, contact/registration expiration, revocation, and cleanup**. Credential expiry ≠ terminating an existing registration/dialog — design explicit teardown. Credentials are **tenant-namespaced** and bound to tenant+session+expiry; the issuer is a stateless scoped service; any cache is tenant-keyed `IDistributedCache`+`ISignal` (CC-1).
- **Delivery model is provider-wide, not per-audio-mode (Model B):** `DeliveryModel` on `AsteriskContactCenterVoiceProvider` applies to the whole provider. Set Asterisk to **`ServerSideAcd`** for all managed endpoints (park customer → originate agent leg on accept → move to mixing bridge). Force Asterisk into the media path (`direct_media=no`) so recording/DTMF/snoop cannot be bypassed.
- **Separate, feature-gated WebRTC soft-phone adapter** respecting the Voice vs Voice.SoftPhone split.
- **Per-tenant availability, not host `ValidateOnStart` (Model B):** an unconfigured tenant provider is **unavailable / readiness-failed**, never a host-startup crash.
- **Browser adapter registry is page-local** (`soft-phone.js`); server adapters via shell DI; no static server registry (CC-1).

**Tasks**

- Client: `IBrowserMediaAdapter` JS contract; SIP.js adapter (WSS SIP UA, DTLS-SRTP, ICE); wire `getUserMedia` → adapter `RTCPeerConnection`; remote `<audio>` sink; mute/hold/hangup bound to call state; asset build (`npm run rebuild`).
- Server: PJSIP endpoint/auth/AOR provisioning per the chosen lifecycle; tenant-namespaced identities; capability/audio-mode advertisement; provider settings for WSS URL, TURN (coturn REST shared secret, time-limited), codecs (opus/g722/ulaw); readiness/availability wiring.
- Config: compose `pjsip.conf` WebRTC template; coturn config; DTLS-SRTP + WSS cert provisioning docs (dev certs in compose).

**Exit:** a browser agent registers and hears/sends **tone-verified** audio on a live Asterisk call (Part 0 E2E, both direct-ICE and forced-TURN); the audio-mode resolver returns `Browser` for Asterisk when configured, fails closed otherwise; credential rotation/revocation exercised.
**Node scope:** single-node first (media is node-agnostic; signaling affinity in Part 8). **Depends on:** Part 0.

---

## Part 3 — Executable connect + idempotent inbound routing (Asterisk)

**Findings:** TEL-2 (Critical, no-op connect), TEL-4 (High, no inbound routing). CC-1 tenant fixes are **release-blocking** here.

**3a. Real `ConnectToAgentAsync` (server-side ACD bridge).** Replace the `return Succeeded=true` no-op (`AsteriskContactCenterVoiceProvider`). On offer-accept, use **ARI** to bridge the parked customer channel to the agent's registered WebRTC endpoint. Per CC-2: derive **deterministic** bridge/leg IDs from the durable command id; **do not** advance interaction/call-session state on the ARI HTTP accept — advance only on confirmed bridge events; the canonical `ProviderCallId` and topology fields live in `CallSession` (Part A). Failure compensates (re-offer live work).

**3b. Idempotent inbound routing (Stasis → CC offer) (Model A refinement).** Today `StasisStart` is mapped to `Connecting` (`AsteriskRealtimeVoiceEventMapper`) but unknown/first-seen calls are **discarded** (`AsteriskRealtimeVoiceEventDispatcher`). Add a **raw inbound handler that runs before the generic interaction lookup** and that **dedupes before it creates work**: a durable provider-inbox/idempotency claim keyed on the inbound channel id, then **atomic creation** of the `CallSession` + channel→tenant binding (CC-1 step 3) + activity + subject + interaction + queue routing + outbox in one transaction. This makes duplicate or reconnect `StasisStart` safe (no duplicate work items). Park the call in a holding bridge/MoH until accepted (then 3a bridges). **Fail closed** on unknown/unentitled entry points (no generic-queue fallback).

**Ordering/perf:** the listener currently awaits events **serially** (`AsteriskRealtimeVoiceListener`) — introduce **bounded, per-call-ordered** ingestion so one slow handler cannot stall all ARI events. On overflow, **disconnect and reconcile** rather than drop events (CC-1 step 7).

**Reconciliation:** abandon/timeout/unregister/duplicate-`StasisStart`/bridge-rollback/ARI-disconnect/**reconnect-adoption** (adopt in-flight calls after a listener reconnect).

**CC-1 tenant fixes (release-blocking):** the full 7-step event-ownership fix — per-tenant ARI app, `subscribeAll=false`, `StasisStart` marker validation, persisted channel→tenant binding, deterministic child-ID binding, resolve-later-events-through-binding, ownership check before every mutation, media host from validated config only.

**Exit:** an inbound call appears as a CC offer, is accepted, and audio bridges to the agent browser (Part 0 E2E green through accept+audio); duplicate/reconnect `StasisStart` creates no duplicate work; **the two-tenant adversarial test (Part 0 harness) passes as a Part 3 acceptance gate** — a second tenant on the same Asterisk cannot see, route, or bridge the first tenant's call.
**Node scope:** single-node first (ARI single-consumer election deferred to Part 8). **Depends on:** Parts A, 1, 2.

---

## Part 4 — Supervisor, recording, transfer & conference (Asterisk)

**Findings:** TEL-3 (Critical), TEL-5 (High), DIST-8 (pulled to GA). Contracts already exist and correctly fail closed: `IContactCenterVoice{Recording,Monitoring,Transfer,Conference}Provider` + capability flags. The draft primitives were **wrong** — corrected below.

- **Recording (correct primitive):** use **ARI bridge recording** (`POST /bridges/{id}/record`) with live **pause/unpause/stop** — **not** dialplan `MixMonitor`. **Persist recording metadata** (name, storage ref, duration, checksum, retrieval path) — `ContactCenterRecordingService` currently updates only state and **discards** metadata (DIST-8). Securely ingest the ARI-stored file. Advertise `Recording`.
- **Recording continuity across transfer/conference (Model B):** ARI bridge recording is bound to one bridge. Require **either** (a) one **stable canonical conversation bridge** that persists across transfers/conferences, **or** (b) **recording segmentation with a durable ordered manifest** and retrieval-time stitching. The chosen approach is explicit and tested against a transfer scenario.
- **Recording governance (GA-mandatory, Model A):** consent + jurisdiction policy, encryption at rest, retention + legal-hold, access audit, right-to-erasure, and **failed-upload recovery**. Recording paths are tenant-namespaced (CC-1).
- **Monitoring (needs a real audio leg):** Snoop alone gives the supervisor **no audio** — **originate a supervisor endpoint and bridge it to the Snoop** channel. Map `Monitor` (listen-only), `Whisper` (supervisor→agent only), `Barge` (all parties). The contract has only `EngageAsync` (`IContactCenterVoiceMonitoringProvider`) — extend with **engagement IDs, update/stop, disconnect cleanup, concurrency, idempotency**, and per-CC-1 tenant-qualified snoop/supervisor-leg identities + ownership checks.
- **Transfer (`IContactCenterVoiceTransferProvider`):** blind (redirect/bridge-move), attended/consult (hold customer, consult target, complete). Advertise `CallTransfer`. Enforce Part 1c typed destinations.
- **Conference belongs to `IContactCenterVoiceConferenceProvider`** — **not** the transfer provider (Model B). Multi-party mixing bridge; advertise `Conference`.
- Advertise capabilities **only where executable**; supervisor UI shows only executable controls.

**Exit:** in Part 0 E2E, supervisor monitor→whisper→barge produce the **correct audibility** (Web Audio verified); recording captures the call, decodes to the injected tones, survives a transfer per the chosen continuity strategy, and is retrievable; blind + attended transfer + conference verified against the live bridge; a supervisor cannot monitor/record another tenant's or non-owned queue's call.
**Node scope:** single-node first. **Depends on:** Parts 2, 3, and 1d.

---

## Part 5 — Cross-cutting reliability, recovery & observability (single-node)

**Findings:** DIST-7 (lifecycle-fenced tasks), DIST-8 (late metadata, with Part 4); plus reliability workstreams.

- **Dependency health & readiness:** ARI/Asterisk/TURN health checks; provider **unavailable** (not crashing) when unconfigured/unreachable (CC-1); graceful degradation when Asterisk/ARI/coturn is down.
- **Recovery:** ARI reconnect + in-flight call adoption; browser reconnect, device-change, and active-call media recovery; feature-drain during active media (do not yank a live call on feature toggle — Model A).
- **Media observability (both models):** RTP loss/jitter/MOS, ICE candidate type (host/srflx/relay), correlation IDs, tenant identification, OpenTelemetry `ActivitySource`/metrics; no PII/secrets in logs.
- **Background-task hygiene:** lifecycle-fenced tasks (DIST-7); bounded tombstone cleanup (from Part A).

**Exit:** killing Asterisk/coturn degrades gracefully with clear health signals and no data corruption; a listener reconnect adopts in-flight calls; media-quality metrics are emitted and tenant-correlatable.
**Node scope:** single-node. **Depends on:** Parts 2–4.

---

## Part 6 — Test honesty, the runnable E2E proof & CI architecture guards

**Findings:** TEST-1..5 (High).

- **TEST-1:** delete source-substring "security" tests; replace with authenticated `TelephonyHub`/`ContactCenterHub` authorization tests invoking methods as different principals (Part 1).
- **TEST-2:** replace mock-callback state-machine/compensation tests with **store-backed** tests exercising the real CAS/fence/settlement (Part A).
- **TEST-3 / two-tenant adversarial (CC-1):** the Part 0 two-tenant harness graduates to CI enforcement here, covering **separate AND shared PBX** with identical logical IDs; prove inbound routing, SignalR, credentials, recording, monitoring, locks, and Redis keys cannot cross tenants. (Implementation/acceptance already gated in Part 3.)
- **TEST-4:** run the CAS + migration matrix on **real Postgres and SQL Server** (Testcontainers), not SQLite + mocked `ISqlDialect`.
- **TEST-5 / Part 0 proof (CC-3):** the runnable single-node E2E with **tone/Web-Audio content assertions**, direct-ICE + forced-TURN, on the controlled runner; nightly + release-blocking. Add an Asterisk **provider contract test** against a fake ARI/HTTP endpoint so connect/recording/monitoring/transfer/conference are exercised without a live PBX in unit CI.
- **CI architecture guards (both models):** fail the build on new application-level raw `StackExchange.Redis`/`ConnectionMultiplexer`, static mutable registries, unqualified `IMemoryCache` for tenant data, and `subscribeAll=true`. **Allowlist** the approved OrchardCore SignalR Redis adapter (`RedisBackplaneStartup.cs`), which legitimately imports `StackExchange.Redis` as the sanctioned integration.

**Exit:** dishonest tests gone; Parts A/1–5 invariants covered by tests that fail on regression; two-tenant adversarial tests green in CI; E2E audio proof green on the controlled runner; architecture guards enforced.
**Node scope:** single-node. **Depends on:** Parts A, 1–5.

---

## Part 7 — Documentation, capability matrix & support-matrix reconciliation

**Findings:** DOC-1 (Medium) + CC-5.

- Correct public-docs oversell (omnichannel breadth, IVR maturity, supervisor live control) to match implemented reality; publish an **honest, contract-tested provider capability matrix**.
- **Reconcile the durable control file (CC-5):** update `support-matrix.v1.json` to add the certified single-node production topology (with the WS-10 capacity ceiling) and move `recording`/`monitor`/`whisper`/`barge`/`bidirectional-media` to allowed for the gated Asterisk profile **only after** the Part 6 proofs pass. Keep the 2–4-node/media-HA claim gated by `PLAN.md` R8.
- **Secure deployment guide:** DTLS/WSS certs + rotation, firewall + RTP port ranges, TURN/coturn hardening, recording consent/retention, **E911/emergency limitations**, and the **tenant-namespacing requirements** for shared-PBX deployments.
- Single-node quickstart (run the Asterisk+coturn reference stack, enable the reference tenant, place a first call).
- A **sanitized public roadmap** page under `src/CrestApps.Docs/docs/contact-center/` (no exploit specifics), plus a changelog entry matching `VersionPrefix`.

**Exit:** docs build passes; capability matrix matches code (enforced by the contract test); no security-exploit specifics in public docs; support matrix reconciled with the code and R8.
**Node scope:** n/a. **Depends on:** Parts 2–4.

---

## Part 8 — Distributed hardening (SECONDARY; feeds `PLAN.md` R8)

**Findings:** DIST-5, DIST-6, DB-2, DB-3 (High); DB-4 (Medium). **All built on Orchard Core primitives only (CC-1).**

- **DIST-5 readiness:** fail-closed startup guard for the multi-node profile unless `IDistributedLock` + the `OrchardCore.Redis` SignalR backplane + a distributed rate limiter are configured.
- **DIST-6 distributed rate state:** back the webhook ingress limiter with `IDistributedCache` + `IDistributedLock`, **tenant-qualified keys** (today `ProviderWebhookIngressLimiter` is a per-shell singleton — fine for single-node, insufficient for multi-node).
- **ARI single-consumer election:** `IDistributedLock` lease, **one per tenant+provider+application**; tenant-unique app names; reconnect adoption; **leadership loss closes the tenant listener immediately.**
- **SignalR/media affinity:** SIP signaling needs no node affinity; SignalR UI needs the Orchard Core Redis backplane + sticky sessions. **Media HA caveat:** Asterisk/coturn remain single failure domains — **no media-failover claim** without redundant PBX/TURN (aligns with R8).
- **Scale queries:** bounded top-N queue selection (DB-2); paginated/aggregate reporting SQL (DB-3); composite indexes led by hot predicates QueueId/Status/NextAttemptUtc (DB-4).
- Adopt `PLAN.md` R8 load/soak/chaos/upgrade suites.

**Exit:** multi-node profile refuses unsafe config; two-node E2E audio + failover proof; load/soak within budgets — satisfying `PLAN.md` R8.
**Node scope:** distributed. **Depends on:** Parts A, 1–6.

---

## Part 9 — Future architecture & capability (post-GA)

**Findings:** DOM-1/2/3, DOM-CC-1/2/3, DB-5, DB-6.

- **ADR-1:** single fenced `ContactCenterWorkState` consistency root; collapse competing availability representations (DOM-1/2/3); demote other documents to projections/history.
- Nullable reference types as a **separate initiative** (DB-5); expand-migrate-contract for destructive migrations (DB-6).
- Real IVR (menus/DTMF/prompts) (DOM-CC-2); first-class non-voice channels (DOM-CC-1); predictive dialer **after pacing certification** (DOM-CC-3).
- DialPad SDK/embed WebRTC adapter.

**Node scope:** mixed. **Depends on:** GA (Parts 0–7).

---

# Added workstream register (from the two-model review)

| # | Workstream | Source | Home |
| --- | --- | --- | --- |
| WS-1 | PJSIP credential provisioning + rotation/expiration/revocation/cleanup | Model B | Part 2 |
| WS-2 | ARI topology reconciliation + reconnect adoption + deterministic IDs | both | Parts 3, 5 |
| WS-3 | Recording governance (consent/encryption/retention/legal-hold/erasure/failed-upload) | Model A | Part 4 |
| WS-4 | Media observability (RTP quality/MOS/ICE type/correlation IDs) | both | Part 5 |
| WS-5 | TURN security + DTLS/WSS cert provisioning & rotation runbooks | Model B | Parts 2, 7 |
| WS-6 | DTMF / hold / MoH / early-media handling | Model B | Parts 3, 4 |
| WS-7 | Browser UX: mic permission, device selection, AEC, accessibility | Model B | Part 2 |
| WS-8 | E911 / emergency policy (documented GA limitation) | Model B | Part 7 |
| WS-9 | SIP trunk / SBC security / toll-fraud controls | Model B | Parts 1c, 8 |
| WS-10 | Single-node capacity certification (published concurrent-call ceiling) | Model B | Parts 6, 7 |
| WS-11 | Tenant-isolation & Orchard-Core-infra conformance audit | both | Part 6 (CC-1) |
| WS-12 | Threat model + command reconciliation + feature-drain-during-media | Model A | Parts 1, 5 |

# Consensus & dissent record

- **Consensus (both models):** WebRTC via chan_pjsip + browser SIP UA (reject in-process gateway for agent audio); `ServerSideAcd` provider-wide; deterministic resource IDs + provider-truth; correct recording (ARI bridge record) / monitoring (originated supervisor leg) / conference (own provider) contracts; CAS retry in a fresh scope; atomic enqueue + retained tombstone; broad enum-SQL audit; real-audio-content E2E on a controlled runner (no mock exit bar); tenant namespacing + `subscribeAll=false` + persisted-binding event attribution + Orchard-Core-only distributed primitives; two-tenant adversarial tests; CI architecture guards.
- **Dissent resolved:** (1) gate label — draft rejected-and-restructured, both models RATIFY with the 7 confirmation changes; (2) security vs state-authority order — 1a immediate on existing fields, 1b–1e after Part A; (3) WorkState — minimal typed `CallSession` ownership now, full root Part 9.
- **Final gates:** Model A (`gpt-5.6-terra`) **RATIFY** (after 5 changes: typed Part A fields + move ProviderCallId fix; inbound dedupe-before-create; two-tenant harness to Part 0 + Part 3 gate; CI guard allowlist; first-command authz). Model B (`gpt-5.6-sol`) **RATIFY** (after 2 changes: persisted-binding ARI event attribution; recording continuity across transfer). **All 7 incorporated above.**

# Progress status

- 2026-07-16 — Plan 2 authored and ratified. Two independent GPT-5.6-class models (`gpt-5.6-terra`, `gpt-5.6-sol`) red-teamed every part and the whole; after one reconciliation and one confirmation round, **both RATIFY** with all 7 final changes incorporated. **No production code changed yet.** Next: begin Wave 1 (Part 0 harness + two-tenant harness, Part A typed state authority, Part 1a soft-phone BOLA) test-first, one plan bullet per commit.
