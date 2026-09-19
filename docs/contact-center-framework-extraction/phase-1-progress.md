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
| W3.2 | `3ca794ce` | The telephony primitive: hub base, models, services, and the reconciliation cycle into `CrestApps.Core.Telephony`. |
| W3.3 (first half), W3.5 | `91530579` | The module services with no host dependency, and the encrypted recording store with a backend contract under it. |
| W3.3 (second half) | `70820d96` | The three services and the dial endpoint that pushed to the soft phone, behind a notifier contract. |
| W3.3 (rest), W3.4 | `9a049eb2` | The token store, the provider resolver and the authentication service: the last module services that were not Orchard's own. |
| W3.6, W3.7 | `5bca9fe8` | The store package: the shared catalog base, the telephony indexes, their schema migrations and their stores. The Orchard `Telephony.Core` project is gone, and the documents it wrote are migrated to the names that replaced it. |
| W3.8 | this commit | The `AddCoreTelephony*` methods, and the Orchard startup reduced to calling them plus its own glue. |

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

### The Orchard `Telephony.Core` project did not disappear at first (W3.2)

The plan expects it to be deleted once emptied. Three index classes, three schema migrations, and
`TelephonyExtensionStore` are still in it, because they belong in
`CrestApps.Core.Data.YesSql.ContactCenter` (W3 item 6) and that move needs `ConcurrentDocumentCatalog`
moved first — a base class shared with the Contact Center, Omnichannel, and SMS Portal stores. The
project is deleted when the store package is built, not before.

`TelephonyExtensionStore` therefore stayed in `CrestApps.OrchardCore.Telephony.Core.Services` while
its interface moved to `CrestApps.Core.Telephony.Services`. That is the honest position: the
framework namespace belongs to framework assemblies.

W3.6 closed this. The catalog base moved, the indexes, migrations and stores followed it into
`CrestApps.Core.Data.YesSql.ContactCenter`, and the project was deleted.

### The recording store kept a backend seam rather than a root path (W3.5)

The plan says `LocalEncryptedRecordingMediaStore` should take a root path in place of Orchard's
`IOptions<ShellOptions>`/`ShellSettings`. It never took those. It takes an Orchard `IFileStore`, and
the Azure recording module deliberately reuses the same store with a blob-backed file store so
recordings are client-side encrypted before they reach Azure. Giving the class a root path would have
deleted that capability.

So the seam stayed, as a framework contract: `IRecordingMediaFileStore` with four operations over
flat names. `LocalRecordingMediaFileStore` is the framework's own backend, and
`FileStoreRecordingMediaFileStore` in the Orchard Telephony module binds any Orchard file store to
it, which is what the Azure module now passes. Encryption, the container format, and the derivation
of a file name from a storage key all stayed above the seam, so moving a deployment's recordings
between backends does not change how they are encrypted or what they are called — and a test pins
that the two backends agree on the name.

### The soft-phone pushes went behind a notifier, not behind a generic (W3.3)

Three services and the dial endpoint reached `IHubContext<TelephonyHub, ITelephonyClient>` and
composed a group name out of `ShellSettings.Name`. The plan's idiom for this is a class generic over
the hub, which would have made four generic types, four log categories carrying a type argument, and
four places that still know a hub exists.

They make three calls between them, so `ITelephonySoftPhoneNotifier` names those three:
`NotifyIncomingCallAsync`, `NotifyCallStateChangedAsync`, `RequestDialAsync`. One framework class,
`TelephonySoftPhoneNotifier<THub>`, is generic over the hub and is the only thing that knows about
SignalR or about how a user's connections are grouped. Orchard registers it closed over its own
`TelephonyHub`, which is where its `[Authorize]` lives.

### Telephony settings became options, and the settings screen now says when they change (W3.3)

The provider resolver and the authentication service read the default provider name straight from
`ISiteService`, which is always current. Moving them to `IOptionsMonitor<TelephonySettings>` would
have cached that value forever, because the module's own `IPostConfigureOptions` bridge had no
change-token source behind it: the default provider would have kept resolving to the old one until
the tenant restarted.

So the bridge was replaced by `AddSiteSettingsOptions<TelephonySettings>()`, which registers one, and
`TelephonySettingsDisplayDriver` now calls `IOptionsUpdateNotifier.RequestUpdate<TelephonySettings>()`
when it saves. That is the pattern the AI modules already use. `TelephonySettings` has exactly the
two properties the hand-written bridge copied, so nothing else about the value changes.

Three Contact Center settings use `AddSiteSettingsOptions` without any driver calling
`RequestUpdate`, which is the same gap this change closed for telephony. That is recorded as its own
task rather than folded in here, because it is a fix to shipped behaviour rather than part of the
extraction.

### The shared catalog base moved rather than being copied (W3.6)

The plan has the store package take a *copy* of the Orchard `DocumentCatalog` implementation under
the name `ConcurrentDocumentCatalog`. There is nothing to copy: Phase 0 already split that
implementation out under exactly that name, and it carries no host type. It moved, and the Orchard
`DocumentCatalog` — which is published API that consumers outside this repository derive from —
now derives from the framework one. One implementation, and the Contact Center, Omnichannel and SMS
Portal stores that sit on it did not change.

Its collection-name constructor was `internal`, which stopped working across an assembly boundary.
It is `protected` now, which is what it was for.

### The legacy type-name rewrite matches the assembly exactly (W3.7)

The three legacy telephony assembly names are prefixes of one another:
`CrestApps.OrchardCore.Telephony`, `...Telephony.Abstractions`, `...Telephony.Core`. The AI migration
this one is modelled on matches the assembly with a trailing wildcard, which here would rewrite an
Abstractions document to the wrong assembly and lose it. The telephony rules match the assembly as an
exact suffix, and a test pins that.

`TelephonyUserConnections` is in the plan's table and is not in the migration: it is stored in a
user's properties under its simple type name, so its namespace never reached the database.

### What stayed that the plan expected to move

`SoftPhoneExtensionEndpoints` is host glue in practice, whatever its name suggests. It resolves a
Contact Center route by name through Orchard's link generator, derives the hub URL from Orchard's own
hub-route convention, and builds the rest out of the current request. There is nothing in it that a
framework consumer could use without reimplementing all three, so it stays in the module.

### The registration methods live in their own namespace, not in `Microsoft.Extensions.DependencyInjection` (W3.8)

That is where a registration extension normally goes, and it is where the first draft put them. The
public-surface baselines do not record that namespace, so the methods a host calls -- the whole point
of the package — would have been the one part of it nobody reviewed. `AddCorePhoneNumbers` and
`AddCoreWebSockets` already sit in their own project's namespace for the same reason, so these follow
them.

The rewire itself changed no registration: the dependency-injection snapshot and the resolution-order
tests are both unchanged, which is what makes it a rewire rather than a rewrite.

## Guards that had to be repointed (W3.2)

Four architecture tests name the telephony primitive by path or assembly rather than by type. All
four now cover the framework project; two cover both it and the Orchard remnant, because until the
store package exists the primitive is in two places and a rule that only watches one of them is a
rule with a hole in it.

- `FileSizeRatchetTests` — scans the new project, and the `TelephonyHubBase.cs` ratchet entry follows it.
- `PhoneNumberCanonicalizationArchitectureTests` — guards the new root as well.
- `ContactCenterWorkStateAuthorityTests` — scans the new assembly and folder as well.
- `VoiceIngressLayeringArchitectureTests` — walks both project closures for a Contact Center reference.
