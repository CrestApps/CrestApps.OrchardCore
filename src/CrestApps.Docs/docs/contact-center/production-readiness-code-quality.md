---
sidebar_label: Readiness — architecture and tests
sidebar_position: 32
title: Production readiness — architecture, feature split, code quality and tests
description: Findings and remediation plan for dependency-injection patterns, feature boundaries, duplication, oversized types, memory and scalability, and the unit-test strategy across the Contact Center, Telephony, Telnyx, SMS Workspace and Omnichannel projects.
---

# Production readiness — architecture, feature split, code quality and tests

Part of the [Production Readiness Plan](production-readiness-plan.md). Workstream **E**.

## Strengths to preserve

- Clear three-layer split (Abstractions → Core → Module) with architecture tests that pin feature ownership
  (`ContactCenterFeatureDependencyArchitectureTests`, `ContactCenterCallStateProjectionArchitectureTests`,
  `ContactCenterTransferTopologyTypingArchitectureTests`).
- Twenty focused startups in the Contact Center module, each gated with `[Feature]` or `[RequireFeatures]`, with
  dependency-only features for shared services (Agent Services, Real-Time, Recording Governance, Voice, Voice Media).
- Durable outbox and inbox, idempotent handlers with declared `ReplaySafety`, event upcasting on read, retention
  with legal holds, query-plan and round-trip budgets as tests, property-based state-machine tests, distributed and
  feature-activation test projects.

## E1. Feature split defects (High)

| Defect | Evidence | Fix |
| --- | --- | --- |
| SMS Workspace needs Work Distribution at runtime but does not declare it | `LeastLoadedSmsRoutingStrategy` → `IActivityQueueManager` (registered only by `QueuesStartup`); manifest depends only on Agent Services | Split routed distribution into its own feature that depends on `Queues` (C3) |
| SMS Workspace references the CRM administration module | `Sms.Workspace.csproj` → `Omnichannel.Managements.csproj` | Move what is used into `Omnichannel.Core` or reusable endpoints; add a reference rule to the architecture tests |
| Automated voice orchestration lives in a provider module | `Telnyx/Services/VoiceOmnichannelProcessor.cs`, `TelnyxAiVoiceConversationHandler.cs`, feature `Telnyx.AiVoice` | New provider-neutral `Omnichannel.Voice` module (D8) |
| SMS automation depends on Business Hours by string id | `Omnichannel.Sms/Manifest.cs` uses the literal `"CrestApps.OrchardCore.ContactCenter.BusinessHours"` | Reference the constant from an abstractions assembly, or move business-hours abstractions to Omnichannel Core where `IBusinessHoursGate` already lives |
| Optional cross-feature services resolved by scanning | `QueuedVoiceWorkOfferService` takes `IEnumerable<IAgentWorkStateHealingService>` and `IEnumerable<IDialerProfileManager>` and calls `FirstOrDefault()`; 22 such sites in Contact Center | Null-object defaults with `TryAddScoped` in the owning feature's startup, or a feature-gated decorator (E2) |

**Tests first.** Extend `ContactCenterFeatureDependencyArchitectureTests` with assembly-reference rules for
`Sms.Workspace`, `Omnichannel.Sms`, and `Telnyx`; extend `FeatureActivationTests` with SMS Workspace and SMS
Automation profiles (enable alone, enable with each optional feature, disable and re-enable).

## E2. Replace service-locator and enumerable-scan optionality (High)

**Evidence.** 48 files across the reviewed projects inject `IServiceProvider` and call `GetService`
(`VoiceAgentHandoffService`, `ReofferVoiceWorkHandler`, `OmnichannelActivityAuthorizationHandler`, most endpoints);
22 constructors use `IEnumerable<T>.FirstOrDefault()` to express "this feature may be off".

**Why it matters.** The dependency graph is invisible to the container and to readers, unit tests must build a
service provider to cover a null branch, and a missing registration is a runtime null instead of a startup failure.

**Target design.**

- Every optional contract gets a null-object default registered with `TryAdd` by the feature that **declares** the
  contract (Contact Center base or the abstractions' `AddContactCenterCore` extension), and the feature that
  implements it uses `Replace`. This is the pattern already used for `IAgentEntitlementPolicy`; apply it to
  `IBusinessHoursGate`, `ICallbackService`, `IAgentWorkStateHealingService`, `IDialerProfileManager` (read-only
  facade), `IQueuedVoiceWorkOfferService`, `IInboundVoiceService`.
- Minimal API endpoints take their dependencies as handler parameters (`[FromServices]` is implicit) instead of
  `httpContext.RequestServices.GetService`.
- Keep `IServiceProvider` only in the documented composition roots: `IContactCenterScopeExecutor`, background tasks
  (`DoWorkAsync(IServiceProvider)` by Orchard contract), and hub scope contexts.

**Tests first.** A new `DependencyInjectionArchitectureTests` (Contact Center, SMS Workspace, Telnyx, Omnichannel):
no `IServiceProvider` constructor parameter outside an allow-list; no `.FirstOrDefault()` on an injected
`IEnumerable<T>` field; every `IEnumerable<T>` injected is a true chain. Existing handler tests updated to the
null-object defaults.

## E3. Configuration, not constants (Medium)

**Evidence.** `ActivityAssignmentService` and `ActivityReservationService` hard-code lock timeout/expiration and the
reclaim wait; `VoiceQueueOfferService` hard-codes `MaxOfferAttempts` (25) and `MaxReclaimPerOffer` (4);
`SmsRoutedReassignmentService` keeps static defaults but reads options (correct pattern);
`AutomatedActivitiesProcessorBackgroundTask` hard-codes lease, batch, attempts and retry. `production-support.md`
promises "timings are configuration, not constants".

**Target design.** `ContactCenterCoordinationOptions` (exists) gains assignment/reservation timings and offer bounds;
`OmnichannelAutomationOptions` gains the processor bounds; both validated with `IValidateOptions`; defaults
documented in `configuration-deployment.md`.

**Tests first.** `ContactCenterOptionsValidationTests` and `OmnichannelConfigurationCoverageTests` extended; the
existing `ContactCenterConfigurationCoverageTests` pattern ("every option is documented") applied.

## E4. Static mutable state (High)

| Site | Problem | Fix |
| --- | --- | --- |
| `OmnichannelHandoffTurnContext` (static `AsyncLocal`) | Ambient decision channel between the AI tool and the handler | Scoped `IOmnichannelHandoffTurn` (B2) |
| `SmsOmnichannelEventHandler._activeGenerations` (static `ConcurrentDictionary` of `CancellationTokenSource`) | Process-wide, not tenant-scoped, not cluster-aware; entries survive tenant reload | Per-tenant singleton `IAutomatedConversationGate`; distributed lock when Redis is enabled (C11) |
| `ContactCenterHubConnectionRegistry` (singleton holding `HubCallerContext`) | Acceptable (bounded by live connections, cleared on disconnect) — keep, but add a test that `Unregister` runs on abort |  |

## E5. Duplication (Medium)

| Duplicated logic | Copies | Consolidation |
| --- | --- | --- |
| `BuildPreview` / thread roll-up | `SmsInboundProcessor`, `SmsAgentHandoffService`, `SmsConversationService` | `SmsConversationRollup` helper (C7) |
| Transcript import | `SmsAgentHandoffService` and inbound message persistence | Same helper |
| `GetContactEmail`, `TryApplyContactEmail`, `GetSubjectTextFields`, `ApplySubjectFields` | `TelnyxAiVoiceConversationHandler`, `SmsOmnichannelEventHandler` (verbatim) | `Omnichannel.Core` writers (C11/D8) |
| `CreateClient`, `SafeReadContentAsync`, `ReadDataStringAsync` | four Telnyx classes | `TelnyxApiClient` (D5) |
| Failure result builders (`Failure(code, message)`) | every Telnyx provider class | Typed result factory on the client |
| Call/offer timers and offer panel rendering | `agent-workspace.js`, `contact-center-agent-bar.js`, `contact-center-soft-phone.js` | shared JS modules (D6) |
| Direct offer bookkeeping | `VoiceQueueOfferService.OfferNextAsync` and `OfferToAgentAsync` share 30 lines of interaction re-offer logic | private `ApplyOfferAsync(reservation)` |

**Tests first.** Characterization tests for each helper before extraction (inputs and outputs captured from the
current implementations), so the consolidation is provably behavior-preserving.

## E6. Oversized types to split (Medium)

| Type | Lines | Split |
| --- | --- | --- |
| `Omnichannel.Managements/Controllers/ActivitiesController` | 1,544 | List/queries, create, edit, complete, purge, bulk into separate controllers or a mediator-style service per use case; view-model building into `IActivityViewModelBuilder` |
| `Omnichannel.Sms/Handlers/SmsOmnichannelEventHandler` | 1,390 | intake, generation, conclusion, handoff, opt-out services |
| `Telephony/Hubs/TelephonyHub` | 1,376 | hub filter for scope + authorization; per-verb describe/log helpers into a `HubActionLogger` |
| `Telnyx/Services/TelnyxAiVoiceConversationHandler` | 978 | move loop to `Omnichannel.Voice`; provider adapter stays |
| `ContactCenter.Core/Services/ActivityReservationService` | 902 | keep the state machine, extract `ReservationTransitionCommitter` and the metadata builders |
| `Telnyx.Core/Services/TelnyxTelephonyProvider` (+ Extensions) | 872 + 234 | on top of `TelnyxApiClient`, split voicemail flow into `TelnyxVoicemailFlow` |
| `ContactCenter/Endpoints/AgentWorkspaceEndpoints` | ~850 | one static class per endpoint group (state, presence, complete, recording, voicemail) |
| `ContactCenter/Services/InboundVoiceCallProcessor` | 678 | extract `InboundActivityFactory` and `InboundTerminalizer` |
| `Telephony/Assets/js/soft-phone.js` | 6,251 | ES modules (D6) |

## E7. Memory and scalability (Medium)

**Evidence.**
- 53 `GetAllAsync()` call sites in admin surfaces; the SMS inbox, templates list and several Omnichannel screens load
  entire catalogs into memory (C5 covers SMS).
- `LeastLoadedSmsRoutingStrategy` loads every conversation per candidate agent to count open assigned threads.
- Reports aggregate through `OmnichannelReportAggregator` on request; the Contact Center metric store already
  rolls up per minute — verify Omnichannel reports use rollups rather than raw scans for large tenants.
- Per-second client-driven sync (A5) multiplies lock acquisitions and reclaim passes by connected agents.
- `ContactCenterConfigurationCache` is a singleton with an invalidation handler (good); confirm SMS routing settings
  on channel endpoints are read through the same cache rather than a catalog query per inbound message.

**Target design.** Extend the budget-test discipline: every list surface has a page size; every routing hot path
has a round-trip budget test; a load test in `DistributedTests` (Redis + PostgreSQL) drives 200 agents × 20 queues
with synthetic inbound at 10 calls/second and asserts p95 offer latency below 500 ms and no lock-wait warnings.

## E8. Test strategy and gaps (High)

The suite is large and mostly meaningful. Findings:

- **Useless or weak tests**: `OfferQueuedVoiceWorkOnAvailabilityHandlerTests.HandleAsync_WhenQueuedVoiceOfferServiceIsMissing_ReturnsWithoutFailure`
  asserts nothing (only that no exception is thrown) — make it assert the null-object default was used once E2
  lands. `AsteriskBrowserAudioE2ETests` has one test with no assertion in the unit project (it is a manual proof;
  move it to the Playwright project or gate it with an environment variable as the distributed tests do).
- **Architecture tests that duplicate each other**: several ownership rules are asserted both in
  `ContactCenterFeatureDependencyArchitectureTests` and `ContactCenterFeatureLifecycleTests`; keep one owner per rule.
- **Missing coverage (add, test-first, in the order below)**:
  1. SMS Workspace: controller authorization matrix, `SmsRealTimeNotifier` targeting, inbound concurrency and
     idempotency, inbox paging budgets, delivery-receipt matching by provider id, keyword policy.
  2. Handoff: `VoiceAgentHandoffService` (no tests exist), tool-name contract, scoped turn context, SMS handoff
     routed mode and business hours.
  3. Telnyx: everything in D3.
  4. Routing: cross-queue selector, skills proficiency and relaxation, idle-since semantics, options validation,
     queue treatment and callbacks, IVR state machine (property-based), predictive pacing gate.
  5. Feature activation: SMS Workspace and SMS Automation tenant profiles; SMS Workspace without Work Distribution.
  6. JavaScript: Vitest for pure soft-phone modules; Playwright specs for the SMS Workspace.
- **CI wiring**: `FeatureActivationTests` and `PlaywrightTests` only run in `release_ci.yml`; run them in
  `pr_ci.yml` (Playwright can be a required job with browser caching). Keep `DistributedTests` release-only but
  add the SMS distributed tests to it.
- **Public API**: `PublicApiApprovalTests` baselines must be regenerated for every assembly touched by this plan and
  reviewed in the PR (the baselines are the contract for the provider-module authors).

## E9. Dead or misleading surface (Medium)

| Item | Evidence | Action |
| --- | --- | --- |
| SMS endpoint `AutoReplyMessage` | Stored by `SmsEndpointRoutingDisplayDriver`, never sent | Implement (C9) or remove the field |
| `DialerMode.Predictive` | Editor offers it; resolver returns null | Hide until implemented (A10) |
| `SmsConversation.LabelIds`, `WindowExpiresUtc` | Present on the model; no UI, no index, no logic | Implement labels/filters or remove |
| `IEntryPointResolver` as a chain | Only the first is used | Decide chain vs single (A11) |
| `VoiceIngressEndpoint` (`api/contact-center/voice/inbound`) | Requires `ManageInteractions` and antiforgery is disabled; intended for provider-neutral ingestion but undocumented | Document the consumer or remove |

## E10. Logging, PII and diagnostics (Low)

Address redaction (`IRedactorProvider`) and `SanitizeLogValue` are used consistently in the reviewed code; the
operational-log privacy tests (`ContactCenterOperationalLogPrivacyTests`) exist. Extend the same test to the SMS
Workspace and SMS Automation modules (message bodies must never be logged; only ids and lengths).

## E11. Documentation debt created by this plan (Low)

Update `sms-workspace.md`, `agents-queues-dialer.md`, `voice-routing.md` (remove fixed limitations), and
`configuration-deployment.md` (new options) as each item lands; add an "AI escalations" section to
`report-catalog.md`.

## E12. Suggested engineering rules to adopt (enforced by tests)

1. No `Clients.All` in any hub notifier.
2. No `IServiceProvider` constructor injection outside the allow-list.
3. No `IEnumerable<T>.FirstOrDefault()` on injected chains.
4. No static mutable state in Core or Module assemblies (allow-list for caches with invalidation).
5. Every `IOptions` class has a validator and a documentation entry.
6. Every provider-originated write has a dedupe key and a test that a duplicate delivery is a no-op.
7. Every list surface pages; every hot path has a budget test.
8. Files above 800 lines fail an analyzer warning (informational at first, error after P3).
