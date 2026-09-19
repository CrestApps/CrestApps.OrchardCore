# Phase 0 progress

Branch `ma/contact-center-framework-extraction`, cut from `ma/add-contact-center` at `a71550af`,
to be merged back into `ma/add-contact-center`.

After every commit below: `dotnet build CrestApps.OrchardCore.slnx -c Release -warnaserror` succeeds,
and both suites are green. They have grown as workstreams landed, because each one pins what it
touches: the main suite started at 5294 cases and the activation suite at 76, against the
baseline in [phase-0-baseline.md](phase-0-baseline.md).

## Done

| Workstream | What landed |
| --- | --- |
| P0.1 | Baseline recorded. The global 80 percent coverage bar is not enforced as an up-front gate; each workstream pins what it touches instead (sequencing decision, recorded in the baseline document) |
| P0.2 (S1) | `IClock` → `TimeProvider` across every in-scope project. `AddCoreTimeProvider()` in `CrestApps.OrchardCore.Core`. Tests on `FakeTimeProvider`; `StubClock` deleted |
| P0.3 (S2) | `IDistributedLockProvider`/`ILocker` + `LocalDistributedLockProvider`; `OrchardCoreDistributedLockProvider` adapter; all 37 call sites migrated |
| P0.3 (S3) | `ITenantAccessor` + `SingleTenantAccessor`; `ShellSettingsTenantAccessor` adapter |
| P0.3 (S4) | `IScopedWorkExecutor`, `IAfterCommitTaskQueue`, `IDetachedWorkExecutor` with framework defaults; `ShellScopeAfterCommitTaskQueue` and `ShellDetachedWorkExecutor` adapters |
| P0.10 (S7) | `TelephonyInteraction`, `OmnichannelMessage`, `Interaction`, `InteractionEvent` and the two activity filters drop the Orchard entity base for their own `JsonObject`; `JsonPropertyBag` + per-type extensions preserve the stored shape, pinned by a test asserting the JSON text |
| S6 | `IContactCenterConfigurationChangeNotifier` replaces `ISignal` in the configuration cache |
| P0.14 | `ConcurrentDocumentCatalog<T, TIndex>` carries what `DocumentCatalog` held; `DocumentCatalog` stays a thin subclass because it is published; the 30 suite stores repoint at the new base |
| P0.1 | **Reduced by decision, see below.** The pre-extraction upgrade test landed; the coverage audit and its gate were dropped in favour of a test-count ratchet |
| P0.1 | Reduced by decision (see below): the pre-extraction upgrade test landed; the coverage audit and its gate were dropped for a test-count ratchet |
| P0.13a | The dependency-injection snapshot: 19 feature/profile baselines plus 4 resolution-order baselines, captured before any registration moves. See the note below on why order is pinned separately |
| P0.10 (S18) | Feature-owned work is keyed on a capability the owning startup contributes, not on an Orchard feature id, so no module has to name another module's features |
| P0.3 (S21) | `IAgentSignOutHandler` owns what ending a session means; the cookie hook only calls it. Takes a user id, because the principal is already gone by then |
| P0.5 | `ISmsProvider`/`ISmsProviderResolver` with an Orchard adapter; the suite sends SMS through the contract |
| P0.11 | Migration bodies move behind `ISchemaMigration`; the Orchard `DataMigration` classes keep their names, attributes and version numbers and delegate |
| P0.3 (S9) | `IUserDirectory` + `IUserProfileStore` replace `UserManager<IUser>`, `IDisplayNameProvider` and the user index across the suite; `ITelephonyUserAccessor` collapses into them |
| P0.6 | The five services that read tenant settings take `IOptionsMonitor<T>` through a generic site-settings bridge; the Telnyx media provider's base-URL read became `IPublicBaseUrlAccessor`. The framework defaults in `CrestApps.Core.Hosting` gained the tests they never had |
| P0.7 | `ContactCenterOperations`/`SmsPortalOperations` replace the three permission checks in the framework-bound projects; Orchard handlers map each operation to the permission that already governed it. `ContactCenterPermissions`/`SmsPortalPermissions` moved to the host-side projects |
| P0.4 (part) | The customer-record vocabulary and contracts; subject flow settings re-based on `SubjectDefinition`; Orchard bindings for contact definitions, contacts and subjects; SMS and voice consumers migrated; `OmnichannelSubjectWriter` retired. **See the open item below** |
| P0.8 | All 30 background tasks split into cycles. The Orchard task keeps its name, attribute and schedule and resolves the cycle; `CycleRunner<TCycle>` drives the same cycles for a host with no scheduler, and is unit-tested |
| Phase 1 W1 (part) | `src/Abstractions/Transitions/CrestApps.Core.Hosting.Abstractions` and `src/Core/Transitions/CrestApps.Core.Hosting` created with their final names and namespaces, Core-repo package metadata, `IsPackable=false`, and matching solution folders. `grep -rlE "OrchardCore" src/*/Transitions` is empty |
| P0.9 | All three hubs split. `SmsPortalHubBase`, `ContactCenterHubBase` and `TelephonyHubBase` live in their `*.Core` projects and name no Orchard type; each module keeps a sealed subclass of the same name carrying `[Authorize]`, because a hub's route is derived from `typeof(T).Name`. None of the three needed an abstract hook. `ShellScopedWorkExecutor` joins the host seams so `IScopedWorkExecutor` opens a unit of work here rather than a bare scope |
| P0.12 | The eighteen endpoint methods renamed to the `Map*` names the registration document is written against, made public and documented, and each given an optional `Action<RouteHandlerBuilder>` applied to every route it maps. No route template, verb, name, authorization attribute or filter changed |
| P0.3 (S23) | Seven client-configuration models in the Transitions projects carry what the browser is told, with the JSON unchanged. A new test reads each script and asserts every configuration key it touches is one the server sends |
| P0.13 (part) | Eleven `AddCore*` methods covering 150 of the 275 framework-eligible registrations, across Telnyx, the Contact Center base and six of its features, the SMS portal and automated voice. The dependency-injection snapshot is unchanged for every one, order included |
| (added) | The public-API gate now governs the `CrestApps.Core.*` projects - the ones being extracted - rather than only the Orchard-named families. Seven surfaces recorded for the first time |
| (added) | Three more service chains pinned before the registration moves began, each by what actually decides it rather than by registration order |

## Exit-criteria scoreboard

Counted over the framework-bound projects only (the `*.Abstractions` and `*.Core` projects in scope);
the Orchard modules are expected to keep these.

| Forbidden reference | Files | Note |
| --- | ---: | --- |
| `IClock` | 0 | done |
| `IDistributedLock` | 0 | done |
| `UserManager<` | 0 | done |
| `IBackgroundTask` | 0 | done; the tasks live in the modules and the cycles here carry the work |
| `ShellSettings` | 3 | the process-health files, which the plan leaves on the Orchard side |
| `ShellScope` | 1 | `ContactCenterScopeExecutor`, the Orchard adapter, which stays |
| `ISignal` | 1 | `SignalContactCenterConfigurationChangeNotifier`, the Orchard adapter, which stays |
| `ISiteService` | 1 | `TelnyxSmsOptionsConfiguration`, the last settings read |
| `OrchardCore.Sms` | 2 | `TelnyxSmsOptionsConfiguration` and a constants file |
| `OrchardCore.Entities` | 1 | `VoiceAgentConversationLoop`, through the activity subject |
| `ContentItem` / `IContentManager` / `IContentDefinitionManager` | 6 / 5 / 4 | nine of these are the deliberate Orchard content bindings the plan keeps; the rest is P0.4's open item below |

## The execution order

Phase 0's remaining workstreams were scouted against the real code and batched so that each batch is
a shippable unit that keeps the build green:

1. **Measurement instruments** - P0.13a, P0.1, P0.14. Nothing here changes behaviour, and everything
   here is what the later batches are measured against. *(Done.)*
2. **Independent seams** - P0.10-S18, P0.3-S9, P0.3-S21, P0.5, P0.11. *(Done.)*
3. **Content boundary, settings, authorization** - P0.4, P0.6, P0.7. *(P0.6 and P0.7 done; P0.4 has one
   open item, below.)*
4. **Background work and routes** - P0.8, P0.12. *(Done.)*
5. **Hub split and client configuration** - P0.9, P0.3-S23. *(Done, with one client surface carved
   out; see below.)*
6. **Registration collapse** - P0.13, alone, because it rewrites the same 42 startup files every
   other workstream touches. *(As far as the type layout allows; see below.)*

### What P0.8 did not do

The cycles' tests still sit with the tasks and still call `DoWorkAsync`, so they cover the Orchard
wiring as well as the work. They have to be re-homed with the cycles in Phase 1; the test-count
ratchet is what catches it if they are not.

The scarce resource is contended files, not time: `ContactCenterHub.cs` is touched by four
workstreams across three batches, and `AgentWorkspaceEndpoints.cs` by four across four.

## P0.1: what was kept and what was dropped

Decided 2026-09-17. P0.1 as written bundles two unrelated things, and they were judged separately.

**Dropped: the per-class coverage audit and its architecture gate.** With 5294 test cases in the main
suite and 81 in the activation suite already green, a 532-row audit table mostly restates what the
suite proves, and the 80 percent line-coverage bar was never closable inside this effort. Worse, both
can be satisfied dishonestly: the bar by writing `needsCharacterizationTest` in 356 rows, the audit by
naming a covering test that does not really cover anything. The standing rule that each workstream
pins what it touches does the same job with none of the ceremony.

The one thing the audit would genuinely have caught is a relocation silently deleting test files, so
that is kept as `MovingSetTestCountRatchetTests`: a floor on the number of test methods, which cannot
be satisfied dishonestly and costs nothing.

**Kept: the pre-extraction upgrade test.** It sits inside P0.1 but it is not a coverage artefact — it
is the only thing that proves a real tenant's stored data survives. Nothing else covers it:
`ContactCenterRollingUpgradeTests` answers a different question, and the one stored type-name rewrite
that already exists here, `AILegacyDocumentTypeNameMigrations`, has no test at all.

It is reclassified as a **Phase 1 prerequisite** rather than a Phase 0 gate. Phase 0 moves nothing, so
it does not need it; Phase 1 renames every namespace, so it must not start without it. It reads from a
fixed commit, so it can be regenerated at any time and is never on the critical path.

## Definition of done, per workstream

Because the coverage audit is gone, one rule replaces it:

> Any framework-default type that no Orchard startup registers ships with unit tests in the same
> commit.

Those types are unreachable from every integration and feature-activation test here, by design: the
framework defaults exist for a standalone host, and Orchard binds its own implementations instead.
They would otherwise carry zero coverage into Phase 2, where they become the only implementation.
This applies to P0.4's default contact and subject model, P0.8's cycles, and P0.13's
`ContactPreferenceDoNotCallRegistry`.

## Deviations from the written plan

1. **Lock parameter order.** [03-host-seams.md](03-host-seams.md) spells the contract
   `TryAcquireLockAsync(key, expiration, timeout)` while stating the goal that "call sites change
   only the injected type". Orchard's own lock is `(key, timeout, expiration)`, so the documented
   order would have silently swapped two `TimeSpan` arguments at 37 call sites with nothing to catch
   it. The contract keeps Orchard's order.
2. **Cancellation token on the lock.** The contract carries one, so callers forward theirs. The
   Orchard adapter drops it because Orchard's lock takes none, which makes this behaviour-neutral
   here while letting a standalone host cancel a wait.
3. **`IDetachedWorkExecutor`** is not in the plan. `RealtimeCallCompletionRunner` needs work to
   outlive the request that started it, for a reason that is not Orchard-specific, so it became a
   seam rather than staying an Orchard detail.
4. **Transitions projects created during Phase 0** rather than at the start of Phase 1, so the new
   seam contracts are written once under their final names instead of being renamed later.
5. **The DI snapshot cannot record registration order.** [04-registration-api.md](04-registration-api.md)
   and the P0.13 gate assume a snapshot can pin "same order for `IEnumerable<T>` chains". It cannot:
   Orchard discovers module startups in an order that varies between runs, so three successive runs
   disagreed on 1152, then 140, then 11 lines as the key was narrowed. The snapshot pins the set
   (which services, how many, what lifetime, what implementation) and resolution order is pinned
   directly instead, by asking a real tenant container what it hands back for the chains where first
   match wins.
6. **`EnabledByDependencyOnly` features cannot be snapshotted alone.** Six Contact Center features
   are declared that way, so enabling one on its own is a no-op and records none of its own
   registrations. The supported feature combinations are snapshotted as well to cover them.

## Errata found in the plan documents

Scouting the remaining workstreams against the real code turned up nine places where
[00-phase-0-preparation.md](00-phase-0-preparation.md), [03-host-seams.md](03-host-seams.md) and
[04-registration-api.md](04-registration-api.md) are impossible as written. Each would have compiled
and passed the existing tests while being wrong. Summarised:

- **The pre-extraction snapshot cannot be built from `main`** - the Contact Center modules do not
  exist there. The baseline is `ma/add-contact-center` at `a71550af`, and it needs a worktree plus a
  generator, which the plan does not mention.
- **`AddCore*` methods cannot all live in the `*.Core` projects** - 70+ registered implementation
  types live in modules, and a `*.Core` project cannot reference a module. Each method has to go
  where its implementations are visible.
- **`IOmnichannelSubjectAccessor` cannot be id-based** - an Orchard subject is a transient
  `ContentItem` that is never saved and has no addressable id. The contract has to be instance-based.
- **`IAgentSignOutHandler.HandleAsync(ClaimsPrincipal)` cannot work** - the sign-out hook runs from
  `OnValidatePrincipal` exactly when the principal has already been nulled. It has to take a user id.
- **The S18 capability map cannot be one dictionary in the Contact Center module** - an architecture
  test forbids that module from referencing provider feature ids, so the map has to be
  DI-contributed by each owning startup, and it is feature to *set of* capabilities.
- **Appendix C is unusable as a checklist** - it was produced by a line scan that only catches
  statements beginning with `services.`, so every fluent chain is missing. `ContactCenter/Startup.cs`
  lists 19 registrations against roughly 109 real ones.
- Plus: `ContactPreferenceDoNotCallRegistry` cannot be built before P0.4; the tenant
  `IServiceCollection` has no public mechanism to dump (hence the application-module startup); and
  P0.1's own gate commands are stale in the way `phase-0-baseline.md` already records.

## Not finished

P0.4's open item, the part of P0.13 the type layout blocks, and two pieces carved out of P0.12 and
P0.3-S23 for the reasons recorded below.

### P0.4's open item: the carrier for an activity's subject

`OmnichannelActivity.Subject` is still an Orchard `ContentItem`. Nothing reaches into it any more:
the services that record what a conversation learned go through `IActivitySubjectWriter`, and
`ContentItemActivitySubjectWriter` is the only thing left that knows what the subject is stored as.
Changing the carrier is then a change to that one adapter plus the property, rather than a change to
every caller.

It is left for Phase 1 deliberately:

1. **It changes a stored shape.** The subject is serialized inside the activity document. Carrying a
   `JsonObject` instead would keep the stored text identical, the way `JsonPropertyBag` already does
   for the four types P0.10 moved - but "identical" has to be proved against a real tenant, which is
   exactly what the pre-extraction upgrade test exists for.
2. **It is visible to tenants.** The subject and the contact are handed to Liquid templates and
   rendered by the Orchard admin screens as content items. A tenant's prompt template that reaches
   into the content-item shape would keep compiling and quietly render nothing.

Both want the upgrade test green before they start rather than after, and both are now one adapter
away rather than thirty call sites away.

### P0.13: what is blocked rather than deferred

Eleven methods landed: `AddCoreTelnyx`, `AddCoreContactCenter`, `AddCoreContactCenterQueues`,
`AddCoreContactCenterAgents`, `AddCoreContactCenterAgentServices`,
`AddCoreContactCenterProviderInbox`, `AddCoreContactCenterRecordingGovernance`,
`AddCoreContactCenterVoiceMedia`, `AddCoreContactCenterPacedDialing`, `AddCoreSmsPortal` and
`AddCoreOmnichannelAutomatedVoice`. The dependency-injection snapshot is unchanged for every one of
them, order included.

What remains is 125 registrations, and the obstacle is not effort. A `*.Core` project cannot
reference a module, so a registration whose implementation is declared in a module cannot move into a
framework method however much one would like it to. Those implementations are what Phase 1 moves, and
these registrations follow them rather than leading them. The concentrations:

| Startup | Movable | Declared in the module |
| --- | ---: | ---: |
| `ContactCenter/VoiceStartup.cs` | 30 | 13 |
| `ContactCenter/DialerStartup.cs` | 12 | 4 |
| `ContactCenter/InboundVoiceStartup.cs` | 10 | 3 |
| `Telephony/Startup.cs` | 10 | 14 |
| `Omnichannel.Managements/Startup.cs` | 5 | 19 |
| `Asterisk/Startup.cs` | 4 | 28 |

Voice is the clearest case: its thirteen module-declared implementations are interleaved through one
chain with the thirty that could move, and two of the `IEnumerable` chains inside it are ones whose
order decides behaviour. Splitting that chain now would reorder them for no gain, so it waits for the
types.

Asterisk and Dialpad have no `*.Core` project at all, so their methods have nowhere to live yet.

### What P0.12 left out

`MapContactCenterVoiceMediaEndpoints` is listed in the registration document but is not a conversion
of an existing endpoint file. Its source is two controller actions reached through conventional area
routing, with no route names, serving governed recording and voicemail media. Writing new public
endpoints for that is a change to how sensitive media is reached, not a rename, and it does not
belong in a pass whose whole claim is that nothing about the routes changed.

### What P0.3-S23 left out

Five of the six client surfaces are typed. The sixth - the Contact Center panel inside the soft
phone - carries ten separate `data-` attributes rather than one payload, so folding them into a model
means editing the script that reads them in the same change. The constraint that the browser payload
does not change is what makes that a separate step.

### Where that leaves Phase 1

Phase 1 may start. Every workstream Phase 0 owns has either landed or is recorded above as waiting on
a type move that Phase 1 performs, which is the opposite of the situation this document described
before: the remaining items are no longer work Phase 1 depends on, they are work that depends on
Phase 1.

The exception is P0.4's open item. It changes a stored shape and a shape tenants can see, and it
wants the pre-extraction upgrade test green against a real tenant first. It is one adapter and one
property, and it should be the first thing Phase 1 does rather than something Phase 1 discovers.
