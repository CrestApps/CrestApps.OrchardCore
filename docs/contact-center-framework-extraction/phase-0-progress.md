# Phase 0 progress

Branch `ma/contact-center-framework-extraction`, cut from `ma/add-contact-center` at `a71550af`,
to be merged back into `ma/add-contact-center`.

After every commit below: `dotnet build CrestApps.OrchardCore.slnx -c Release -warnaserror` succeeds,
`CrestApps.OrchardCore.Tests` is 5294 passed / 0 failed / 1 skipped, and
`ContactCenter.FeatureActivationTests` is 76 passed / 0 failed / 2 skipped — identical to the
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
| Phase 1 W1 (part) | `src/Abstractions/Transitions/CrestApps.Core.Hosting.Abstractions` and `src/Core/Transitions/CrestApps.Core.Hosting` created with their final names and namespaces, Core-repo package metadata, `IsPackable=false`, and matching solution folders. `grep -rlE "OrchardCore" src/*/Transitions` is empty |

## Exit-criteria scoreboard

Counted over the framework-bound projects only (the six `*.Abstractions` and eight `*.Core`
projects in scope); the Orchard modules are expected to keep these.

| Forbidden reference | Files | Note |
| --- | ---: | --- |
| `IClock` | 0 | done |
| `IDistributedLock` | 0 | done |
| `UserManager<` | 0 | already clear before this branch |
| `IBackgroundTask` | 0 | the tasks live in the modules; P0.8 still splits them into cycles |
| `ShellSettings` | 3 | all in the process-health files, which the plan leaves on the Orchard side |
| `ShellScope` | 1 | `ContactCenterScopeExecutor`, the Orchard adapter, which stays |
| `ISignal` | 1 | `SignalContactCenterConfigurationChangeNotifier`, the Orchard adapter, which stays |
| `OrchardCore.Entities` | 3 | all content-item code, cleared by P0.4 |
| `ISiteService` | 10 | P0.6 |
| `OrchardCore.Sms` | 9 | P0.5 |
| `ContentItem` / `IContentManager` / `IContentDefinitionManager` | 7 / 6 / 3 | P0.4, the largest remaining workstream |

## Not started

P0.4 (contacts, subjects, subject flows — the largest and highest risk), P0.5 (SMS provider
abstraction), P0.6 (settings to options), P0.7 (authorization operations), P0.8 (background tasks to
cycles), P0.9 (hub base classes), P0.11 (schema migration steps), P0.12 (endpoints as `Map*`
methods), P0.13 (`AddCore*` registration methods and the DI snapshot test), P0.14 (store base class),
and the rest of P0.3 (`IUserDirectory`, `IAgentSignOutHandler`, client configuration models).

All of Phase 1 is ahead, except the two Transitions projects noted above.

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
