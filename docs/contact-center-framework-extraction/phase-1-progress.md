# Phase 1 progress

What has actually landed on `ma/contact-center-framework-extraction`, and the decisions that were
made while landing it that the plan did not anticipate. The plan in
[05](05-phase-1-transition.md) says what Phase 1 intends; this file says what it did.

Every entry below passed the same gate before it was committed: the Release build with
`TreatWarningsAsErrors=true`, the main suite, the framework suite, the feature-activation suite, and
a reviewed diff of every approval baseline that moved.

## Landed

| Workstream | Commit | What moved |
| --- | --- | --- |
| W0 | `9e77b074` | The thirteen `Transitions` projects, the framework test project, and the no-host gate that governs them. |
| W1 | `fd71e996` | `TenantSignalRGroupName`, `HubConnectionWork`, `LogDataClassifications`. |
| W2 | `b1435bf0` | Phone numbers (`DefaultPhoneNumberService` and its contracts) and the WebSocket connection registry. |
| W3.1 | `bffa165f` | The telephony contracts: 110 files into `CrestApps.Core.Telephony.Abstractions`, with the feature ids left behind in a new Orchard `TelephonyFeatures`. |
| W3.2 | this commit | The telephony primitive: hub base, models, services, and the reconciliation cycle into `CrestApps.Core.Telephony`. |

## Decisions the plan did not make

### The no-host gate ignores string literals (W0)

`TransitionsNameNoHostTests` strips comments *and* string literals before looking for the host name.
Two of those literals are data-protection purposes, which are key-derivation inputs: renaming one
silently destroys every value already encrypted under it. A type resolved by name in a string is the
lesser risk, because it fails loudly the first time it is resolved.

### Identifier generation is a utility, not a seam (W3.2)

The hub stamps an identifier on the interaction it records, and reached `OrchardCore.IdGenerator`
for it. The seam table has no row for this, and it does not need one: an identifier carries no
meaning beyond being unique, so there is nothing a host can do better. `CrestApps.Core.Support.IdentifierGenerator`
generates the same shape the host does — 26 lowercase base32 characters, which is the width every
index column that stores one is declared with — so a record created by the framework and a record
created by the host sit in the same column.

The alternative was an injected `IIdentifierGenerator` with an Orchard adapter. It would have added
a constructor parameter, a registration, and a baseline entry to every framework type that creates a
record, to make two random values agree on nothing.

### `SanitizedLoggingExtensions` moved with the hub (W3.2)

It is a pure helper already sitting in the `CrestApps.Core.Support` namespace, and the hub needs it.
It now lives in `CrestApps.Core.Hosting/Support/` beside `IdentifierGenerator`; `CrestApps.OrchardCore.Core`
references that project so its hundred-odd existing callers still resolve it without changing a line.
Phase 2 folds both into the `CrestApps.Core.Support` package, which is where the namespace already
says they belong.

### The Orchard `Telephony.Core` project did not disappear (W3.2)

The plan expects it to be deleted once emptied. Three index classes, three schema migrations, and
`TelephonyExtensionStore` are still in it, because they belong in
`CrestApps.Core.Data.YesSql.ContactCenter` (W3 item 6) and that move needs `ConcurrentDocumentCatalog`
moved first — a base class shared with the Contact Center, Omnichannel, and SMS Portal stores. The
project is deleted when the store package is built, not before.

`TelephonyExtensionStore` therefore stayed in `CrestApps.OrchardCore.Telephony.Core.Services` while
its interface moved to `CrestApps.Core.Telephony.Services`. That is the honest position: the
framework namespace belongs to framework assemblies.

## Guards that had to be repointed (W3.2)

Four architecture tests name the telephony primitive by path or assembly rather than by type. All
four now cover the framework project; two cover both it and the Orchard remnant, because until the
store package exists the primitive is in two places and a rule that only watches one of them is a
rule with a hole in it.

- `FileSizeRatchetTests` — scans the new project, and the `TelephonyHubBase.cs` ratchet entry follows it.
- `PhoneNumberCanonicalizationArchitectureTests` — guards the new root as well.
- `ContactCenterWorkStateAuthorityTests` — scans the new assembly and folder as well.
- `VoiceIngressLayeringArchitectureTests` — walks both project closures for a Contact Center reference.
