# Phase 1 progress

**Branch:** `ma/contact-center-framework-extraction`, branched from `main`.
**Pull request:** [#677 Contact Center Suite extraction: Phase 0 and Phase 1](https://github.com/CrestApps/CrestApps.OrchardCore/pull/677),
open as a draft against `main` and staying a draft until Phase 1 is finished. Every Phase 0 and Phase 1
commit referenced in this file is on that branch and nowhere else; nothing has merged to `main` yet, and
the whole of Phase 1 is meant to land as one reviewable branch. The one piece of this work that lives
outside it is the store-neutral concurrency exception, which belongs to the `CrestApps.Core` repository
(see "The concurrency exception is settled, in the other repository" below).

What has actually landed on that branch, and the decisions that were made while landing it that the plan
did not anticipate. The plan in [05](05-phase-1-transition.md) says what Phase 1 intends; this file says
what it did.

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
| W3.8 | `83c7b18c` | The `AddCoreTelephony*` methods, and the Orchard startup reduced to calling them plus its own glue. |
| W4.1 (first half) | `57d65f62` | The 44 Omnichannel models that name no content type, into `CrestApps.Core.Omnichannel.Abstractions`. |
| W4.1 (second half) | `d42653e3` | The eleven Omnichannel contracts that name no content type and no persistence. |
| W4.2 (first half) | `1f791056` | The nine Omnichannel services that name no content type, into `CrestApps.Core.Omnichannel`. |
| W5.1 | `6f86b698` | The Contact Center contracts: 102 files into `CrestApps.Core.ContactCenter.Abstractions`. |
| (bookkeeping) | `84ec31e0` | Records the commit the Contact Center contracts landed in. |
| (review pass) | `217e7da6` | Brings the extracted projects in line with the Core repository. |
| W4.1 (CRM) | `3cdf444e` | The customer-record contracts. |
| W4.2 | `c57217a8` | The activity's subject stops being a content item. |
| W4.3 | `92e11c3e` | The activity contracts, and the rewrite migration that was missing. |
| W4.4 | `fbacc06a` | The omnichannel indexes and their schema into the store package. |
| W4.6 | `d3cab2fc`, `cd5a19e6` | The automated voice primitive, then the SMS primitive. |
| W5.1a | `f2847584` | The Contact Center models, and a rewrite that was missing every row. |
| W5.1b | `c5de0fce` | The Contact Center services, and four gates that had stopped looking. |
| W5.1c | `a974e103` | The Contact Center migrations, and the gates that read a folder. |
| W5.1d | `2621b5ba` | The hub, the health checks, and a dead `using` worth thirteen files. |
| W5.2 | `4b2462a9` | A Contact Center builder, and a measurement of what cannot be on it yet. |
| W5.2a | `f11a001a`, `89ba11ca` | Eleven services stop taking a YesSql session; deduplication reads through its store. |
| W5.2b | `91bc2fa6` | D-4 was smaller than it looked, and twenty-one imports were stale. |
| W5.2c | `316e4514` | Records the concurrency decision and what it unblocks. |
| W5.2d | `4cf0b008` | Fixes a rewrite that skipped five of the eight types it claimed. |
| Phase 0 closeout | `a818a47b` | The Phase 0 items nothing was blocking. |
| S14 | `e248c305` | The startup-check seam three workstreams were waiting on. |

The table had stopped at W5.1 while twenty commits landed behind it. Several of those commits are
described in the prose below and were simply never given a row; W4.6 was neither. W5.2e through W5.2k
have no commit and have not been started.

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

**Correction (review pass, 2026-09-19):** the conclusion holds but the helper was the wrong one, and it
is gone. `CrestApps.Core.Abstractions` already ships `UniqueId.GenerateId()`, and writing a second
generator beside it was the duplication this extraction exists to remove. Worse, the two were not the
same shape: the deleted helper encoded with the standard base32 alphabet
(`0123456789abcdefghijklmnopqrstuv`) while both the host's generator and `UniqueId` use the Crockford
one (`0123456789abcdefghjkmnpqrstvwxyz`), so the claim above that it "generates the same shape the host
does" was true of the width and false of the characters. Both call sites now use `UniqueId`.

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

### The telephony tests did not move, and should move with everything else (W3.9)

W3 asks for seventeen telephony test files to move into the framework test project. They share their
doubles with the rest of the suite: `RecordingTelephonyProvider` and `PassThroughStringLocalizer`
alone are used by twenty-six files across the Orchard test project, most of which stay. Moving a
handful of tests now means either duplicating those doubles or having one test project reference
another, and both are worse than the tests sitting where they are for another workstream.

W12 exists to consolidate the test projects. That is where the doubles can move once, with everything
that uses them. The framework test project already references every framework project and carries the
same packages, so nothing about that move is blocked.

### The Omnichannel models split along the content-model line, not the folder line (W4.1)

Forty-four of the models in `Omnichannel.Core/Models` name no host type at all and moved. Ten did not,
and the line between them is the one S11 draws: `OmnichannelActivity` carries a `ContentItem Subject`
and resolves a contact from a `ContentItem`; `OmnichannelContactPart`, `OmnichannelSubjectPart` and
their settings are content parts; and six more models are typed in terms of those. They wait for the
CRM contracts, which is what W4.0 is for.

Three enums — `ActivityStatus`, `ActivityUrgencyLevel`, `ActivityInteractionType` — were declared
at the bottom of `OmnichannelActivity.cs` and are used across every pillar. They are now files of
their own in the framework, so the whole suite did not have to wait for the one model that cannot
move yet.

### Four contracts stayed behind with the models (W4.1)

`IOmnichannelProcessor` is typed in terms of `OmnichannelActivity`; `BulkManageActivityFilterContext`
carries YesSql's `ISqlBuilder`, `ISqlDialect` and `ITableNameConvention`, which makes it persistence
rather than a contract; and the two filter-handler interfaces are typed in terms of those contexts.
They move when the thing they name moves.

`OmnichannelConstants` also stayed whole. Half of it — `NamedParts`, `Sterotypes`, `ContentParts`,
`ContentTypes` — describes a content model and imports the host's permission type. Splitting it is
the spike's decision, not a mechanical one.

### The channel names split out of the constants (W4.2)

The channel-endpoint manager canonicalizes a phone or SMS address before looking it up, so it needs
to know which channels carry a number. Those three names were inside `OmnichannelConstants`, which
cannot move. They are `CrestApps.Core.Omnichannel.OmnichannelChannels` now, and the module's
`OmnichannelConstants.Channels` members are `const` references to them, so every existing caller
still compiles against the name it already uses and the stored values cannot drift apart.

### The Contact Center feature ids are still in the framework (W5.1)

`ContactCenterConstants.Features` holds the host's feature ids, and it moved with the rest of the
partial class because a partial class cannot be split across assemblies. The no-host gate passes it,
because they are string literals and the gate ignores those, but a framework package should not be
naming `CrestApps.OrchardCore.ContactCenter.Agents`.

W5 already owns the fix: the plan replaces feature ids in framework code with
`ContactCenterCapabilities`, and has Orchard map a feature id to a capability. Doing it here would
have meant renaming several hundred `ContactCenterConstants.Feature.*` call sites inside a commit
that is otherwise a pure relocation. This is the first thing W5 should do.

Telephony had the same problem and solved it differently, because its feature ids were few: W3.1 left
them behind in a new Orchard `TelephonyFeatures` class. That option is not available here without the
rename, since the Contact Center ids are reached through the partial `ContactCenterConstants`.

### Four ratcheted files grew by one line each (W5.1)

Each gained one `using` for the framework namespace while still needing the namespace it already
imported, so the line is real and cannot be avoided by removing a dead import the way the SMS portal
controller's was. The recorded sizes went up by one. A ratchet exists to stop a file accumulating
logic; a namespace move is not that, and pretending otherwise would mean splitting four large files
inside a relocation commit.

### The store implementations never needed the Orchard catalog (W5.1b)

The twenty-five moved stores derived from `DocumentCatalog<T, TIndex>` in the Orchard
`YesSql.Core` project, which exists only to give the framework's `ConcurrentDocumentCatalog` an
Orchard-shaped name and adds two constructors and nothing else. They now derive from the framework
base directly, so the store package references no Orchard assembly. The behaviour is identical,
because there was never any behaviour in between.

### Three things the stores reached into the host for (W5.1b)

The stores compiled against constants that lived on host services, which is the wrong direction:
persistence asking the dispatcher how many rows to read. All three moved to `ContactCenterStorage`,
where both sides can see them, and the host constants now read from there so there is one value:

- `ProviderWebhookInbox.MaxBatchSize` -> `ContactCenterStorage.WebhookInboxBatchSize`.
- `ProviderWebhookInbox.MaxTombstoneCleanupBatchSize` -> `ContactCenterStorage.WebhookInboxTombstoneCleanupBatchSize`.
- `ProviderWebhookInbox.TombstoneRetentionDays` -> `ContactCenterStorage.WebhookInboxTombstoneRetentionDays`.
  This one is load-bearing: retention deletes the row that makes a redelivery a duplicate, so the
  floor has to be visible to the policy that deletes, not only to the dispatcher that wrote it.

`QueueItemQueries` and `InteractionQueries` build raw SQL against YesSql index tables and moved into
the store package with the stores that execute them. The query-plan gates follow them, which is the
point of those two classes existing separately at all.

### The retention policies are persistence, and moved with it (W5.1b)

Sixteen classes declared the framework namespace `CrestApps.Core.ContactCenter.Services.Retention`
while sitting in the Orchard host project. Every one of them takes an `ISession` and builds an
`Expression<Func<TIndex, bool>>`, so they are YesSql code: they are now
`CrestApps.Core.Data.YesSql.ContactCenter.Services.Retention` in the store package. Their contract,
`IContactCenterRetentionPolicy`, stays in the abstractions, which is what lets a host that stores
its Contact Center data elsewhere write its own.

The coverage gate had been anchored on that contract's assembly to discover policies. Once the
contract was in the abstractions - which declares no policy - discovery returned zero and the
coverage assertion passed over an empty set while reporting success. It is anchored on a policy now,
and the "at least twelve policies" floor is what caught it.

### Twenty-six files had no reason to be in the host at all (W5.1b)

Sweeping the host project for files whose only non-framework imports were the two stale Orchard
usings turned up twenty-six: nineteen services, three cycles and four helpers, none of which
referenced an Orchard or YesSql type. They are in the framework package now. Two of them,
`RequiredSkillsRoutingStrategy` and `RepeatCallerPriorityContributor`, were the last members of
chains whose other members had already moved - the kind of straggler that is invisible in a build
and shows up only as a reordered snapshot.

Health checks and hubs also came back clean on that sweep and were deliberately left: they need
ASP.NET references the services package does not carry, and they have their own W5 items.

### What the snapshots said, and what they meant (W5.1b)

Every approved surface moved, so each one had to be read rather than accepted:

- The three framework public-API baselines are pure additions. Nothing was removed from any of them.
- The host baseline lost 2,193 lines. Every type declaration among them was checked against the
  host's own new surface and the framework baselines: none vanished, all moved.
- The DI snapshots are sorted by type name, so a namespace change reorders them without any
  registration changing. Each diff was compared with namespaces stripped; after the two stragglers
  moved, the only remaining difference is `ContactCenterVoiceProjection` sorting ahead of
  `TelephonyCallHistoryVoiceEventHandler`, which is the sort and not the chain.
- `ServiceResolutionOrderTests` is the gate that pins real resolution order, and all four of its
  chains resolved in the same order throughout. Its baselines were rewritten name-for-name in place,
  so a reorder could not hide inside the rename.

### The migrations moved without changing what a database has applied (W5.1c)

All twenty-seven Contact Center schema migrations are in the store package now. Nothing about a
tenant's applied state changed, and that is by construction rather than by luck: `ISchemaMigration`
carries a `Name` that is the original Orchard migration class's name, and `SchemaVersion` is keyed by
that name rather than by CLR type. A database migrated under either host agrees on which version it
is at, whatever assembly the step lives in now.

They became `public` on the way. The Orchard `DataMigration` wrappers construct them directly, and
`internal` only worked because of an `InternalsVisibleTo` that does not survive the move to a
package. The migrations already moved in earlier workstreams are public for the same reason.

`SchemaQualifiedIndexDrop` and `IndexStringColumnRebuild` went with them. Both are pure YesSql, both
lived in the Orchard `YesSql.Core` project, and after the migrations moved they had no consumer left
there. They are staged under `CrestApps.Core.Data.YesSql.Migrations` in the store package, the way
`ConcurrentDocumentCatalog` is staged under `CrestApps.Core.Data.YesSql.Services` - the namespace
they belong to is in a Core package this repository does not build.

`CrestApps.OrchardCore.YesSql.Core` has no approved public-API baseline, so the two types leaving it
was not gated by anything. Worth adding one.

### The migration gates read directories, and the directory moved (W5.1c)

Four gates take a path rather than a type: `MigrationAdditiveOnlyGuardTests` pins each authorized
destructive step and each reviewed dynamic-SQL site by file, and
`ContactCenterRetentionCoverageTests` scans a migrations folder four separate times. Pointed at the
old folder they reported zero migrations found - which their own "this gate is not reading the files
it is meant to check" floors caught, and which is the only reason the move did not silently disarm
them.

`ContactCenterMigrationSql`'s reviewed-SQL fingerprint moved with it. The file was diffed against its
previous contents before the new fingerprint was recorded: two lines, the namespace and the
accessibility, and no statement.

### A dead `using` was holding thirteen files in the host (W5.1d)

Seventeen Contact Center services imported `CrestApps.OrchardCore.Telephony`, which declares exactly
two types - `TelephonyFeatures` and `TelephonyPermissions` - and not one of the seventeen named
either. The import was left over from before the telephony primitive moved. To a dependency sweep it
looked like a host dependency, which is how thirteen otherwise framework-clean services stayed
behind. Removing it moved them.

Worth remembering as a method note: "what does this file import" is a weak proxy for "what does this
file need", and it fails in the direction that makes an extraction look more finished than it is - a
stale import blocks a move, it never forces one.

### The hub base and the health checks ship with the component (W5.1d)

`CrestApps.Core.Telephony` already takes `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
and ships its own `Hubs` and `Endpoints`. The Contact Center package follows it: the hub base, the
hub's client contract and all fifteen health checks are in it now, and the three
`Microsoft.Extensions.*` package references the framework reference supersedes are gone (NuGet
refuses them outright, which is a helpful way to be told).

The alternative - separate `.Hubs` and `.HealthChecks` packages - would keep the services package
free of ASP.NET, but it would also be the only component in the suite split that way. Consistency
with telephony wins; if the split is wanted it should be made for both at once.

### The contact centre has a builder, and four of its eight features are on it (W5.2)

`CrestAppsContactCenterBuilder` exists now, and `AddContactCenter` hangs off the suite builder the way
`AddTelephony` and `AddOmnichannel` do. Four features moved onto it - the agent directory, recording
governance, voice media and paced dialing - each as a builder method that is sugar over an
`AddCoreContactCenter*` method a host can call directly.

The other four - the base feature, work distribution, agent presence and the provider inbox - stayed
in the host. The reason was measured rather than assumed: of the 44 implementations the base feature
registers, six are services that still take a YesSql session directly, and work distribution has five
more, agent presence one, the provider inbox one. Registering any of those four from the framework
package would mean the package deciding where a tenant's records live, which is the one thing the
split exists to avoid. They follow when those thirteen services read through a store contract
instead - the same blocker that keeps thirty-two files in the host project.

`AddAgentDirectoryYesSqlStores` is the first Contact Center store method, and it is deliberately per
feature rather than one method for the whole component. Several stores come with a retention policy,
and a policy registered for a feature the host did not enable is a purge aimed at a table that was
never created - so "register every store, it is free" is not free here.

The activation suite is what proves the split changed nothing: the per-feature dependency-injection
snapshots are byte-identical, so every feature registers exactly what it registered before, from a
different package.

Three composition tests exercise the new builder, because the builder is the part of the package no
Orchard startup runs - the host calls the `AddCore*` methods underneath it.

### Eleven services took a YesSql session to call one method on it (W5.2a)

The thing keeping the base feature, work distribution, agent presence and the provider inbox in the
host was a constructor parameter. Eleven services took `ISession` and used it for exactly one thing:
`SaveChangesAsync`. Core already publishes the seam for that - `IStoreCommitter`, with a YesSql
implementation and an EntityCore one - so all eleven now take the commit boundary instead, and the
host registers `YesSqlStoreCommitter` behind it.

`AddCoreContactCenter` registers it with `TryAdd`, because a tenant running another CrestApps module
has one already and two descriptors for one contract resolve by "last one wins". The registration
sits in the host for now and follows the base feature when that moves.

The per-feature dependency-injection snapshots gained exactly one descriptor each, and nothing else
in them changed - which is the evidence that eleven constructors changed shape and no behaviour did.

### What is still holding the last services: a YesSql exception type (W5.2a)

Ten Contact Center services catch YesSql's `ConcurrencyException`, so nine of them still import
YesSql for nothing but that one `catch`. `IStoreCommitter` hands a caller a commit boundary but says
nothing about how a commit fails, so a service that has to distinguish "someone else wrote this row"
from any other failure has to name the store's own exception.

This is a Core decision rather than something to settle here, and it is the last mechanical blocker
for those services. The options are roughly:

- Core publishes a store-neutral concurrency exception, and each store package translates its own.
  Every store's write path gains a catch-and-rethrow.
- `IStoreCommitter` grows a documented failure contract that includes it.
- The services stop distinguishing the case, which they should not: the ones that catch it are the
  ones that retry rather than fail, and losing that turns a retryable write into a lost one.

Until then those nine services stay in the host. They no longer take a session, so what is left is a
`using` and a `catch` rather than a dependency on where the data lives.

### The remaining blockers, counted (W5.2a)

Thirty-two files are still in the Orchard Contact Center project, and the reasons now divide cleanly:

- Nine wait on the concurrency exception above.
- Twelve reference `CrestApps.OrchardCore.Omnichannel.Core.Services` or `.Models`, which is D-4:
  the activity contracts that are typed on the MVC filter models and could not move with W4.
- Four are genuinely the host's: the shell-scope executor, the cache-signal notifier, the topology
  evaluator and the diagnostics source.
- Three query YesSql directly - reporting, deduplication and orphan recovery - and belong in the
  store package rather than the services package.
- The rest are the registration file and assembly info.

### Deduplication asked the session a question its store should answer (W5.2a)

`ContactCenterEventDeduplicationService` composed its own YesSql query - handler id and event id over
the processed-event index - and staged the marker with `Session.SaveAsync`, while
`IContactCenterProcessedEventStore` sat next to it doing nothing but `ICatalog<T>`. The pair is what
deduplication asks about, so `FindByHandlerAndEventAsync` is on the store now, the service reads
through it, and the service is in the framework package.

The marker is still staged rather than committed - `CreateAsync` calls the same
`Session.SaveAsync(record, checkConcurrency: false, ...)` the service called - so it still lands
atomically with the handler effect it guards, which is the property the whole mechanism rests on.

### D-4 was smaller than it looked: nine services wanted the catalog manager (W5.2b)

Twelve Contact Center files depended on `CrestApps.OrchardCore.Omnichannel.Core.Services`, which is
the D-4 blocker W4 recorded: `IOmnichannelActivityManager` carries four paging methods typed on the
host's administration filter models, so the contract could not move.

Counting what the Contact Center actually calls on it settles it. Across nine services the calls are
`FindByIdAsync` (ten), `UpdateAsync` (two), `NewAsync` and `CreateAsync` - every one of them a member
of `ICatalogManager<OmnichannelActivity>`, which is already a framework contract over a model that
already moved. Not one call touches the paging methods. So the nine take the generic contract, and
the host forwards it:

    services.AddScoped<ICatalogManager<OmnichannelActivity>>(
        static sp => sp.GetRequiredService<IOmnichannelActivityManager>());

Forwarding rather than letting the open generic `CatalogManager<>` answer is the point: the open
generic would construct a second manager, with its own handler pipeline, and two managers writing the
same activities is exactly the kind of divergence this suite has architecture tests for. The
dependency-injection snapshots confirm the closed registration is the only descriptor at that key.

Nothing resolved `ICatalogManager<OmnichannelActivity>` before this, so the forwarder changes nobody
else's answer.

### Twenty-one imports were describing a dependency that had already gone (W5.2b)

With the manager repointed, a sweep for imports whose namespace contributes no name the file uses
removed twenty-one host imports across seventeen files, and the build stayed clean. Seven services
came out of it with no Orchard or YesSql dependency at all and moved.

This is the same lesson as the dead telephony import, at larger scale: a stale `using` reads as a
dependency to any sweep, and it fails in the direction that makes an extraction look less finished
than it is. It is worth running the dead-import sweep before, not after, deciding what is blocked.

### The concurrency exception is settled, in the other repository (W5.2c)

The decision on the `ConcurrencyException` blocker was to publish a store-neutral exception from
`CrestApps.Core` rather than stage one here. That is done, on the `ma/store-concurrency-exception`
branch of `CrestApps.Core`:

- `CrestApps.Core.Services.StoreConcurrencyException` in the abstractions package, next to
  `IStoreCommitter`.
- `YesSqlStoreCommitter` translates YesSql's `ConcurrencyException`; `EntityCoreStoreCommitter`
  translates Entity Framework's `DbUpdateConcurrencyException`. Both keep the original as the inner
  exception, and neither touches any other failure.
- Four tests, one of which drives a real lost race - a scope holds a row, the row is deleted
  underneath it, the update affects no rows - rather than a substituted failure.
- The changelog records it as a breaking change, because a `catch` around `CommitAsync` looking for
  the store's own exception stops firing.

**This repository cannot consume it yet.** `CrestApps.Core` arrives here as a NuGet package from the
Cloudsmith preview feed, so the sequence is: merge the Core branch, let a preview publish, bump
`CrestAppsCoreVersion`, and only then do the work below.

When the preview lands:

1. Change the fourteen services' `catch (ConcurrencyException)` to
   `catch (StoreConcurrencyException)`. Thirteen of them are held in the host project by nothing else.
2. `ConcurrentDocumentCatalog` stages writes rather than flushing them, so the exception surfaces at
   the commit boundary the committer already translates. Check each catch site before moving it:
   `CallbackService` catches around `_callbackManager.UpdateAsync`, which stages, so that catch may
   already be dead code - worth confirming rather than translating.
3. Move the services, and the four features they were holding back onto
   `CrestAppsContactCenterBuilder`.

### The rewrite skipped five of the eight types it claimed (W5.2d)

An independent audit found this, and it is the worst defect of the extraction so far: merged code that
loses data on upgrade.

`OmnichannelLegacyDocumentTypeNameMigrations` matched the namespace at the start of the recorded type
and the assembly at the end. That describes a type stored one document per record. It does not
describe a type stored through a catalog: `Catalog<T>` persists through
`IDocumentManager<DictionaryDocument<T>>`, so the document records the wrapper, as
`CrestApps.OrchardCore.Models.DictionaryDocument`1[[Namespace.TypeName, Assembly, Version=...]],
CrestApps.OrchardCore.Abstractions` - a value that neither starts with the namespace nor ends with the
assembly.

Five of the eight types it names are stored that way: `OmnichannelCampaign`,
`OmnichannelCampaignGroup` and `OmnichannelDisposition` through the open-generic registration,
`OmnichannelChannelEndpoint` through an explicit `Catalog<T>`, and `SubjectAction` through
`SourceCatalog<T>`. On upgrade each would have been skipped, the new code would have asked for the new
nested name, found nothing, and been handed a fresh empty document. Campaigns, campaign groups,
dispositions, channel endpoints - which is inbound number to queue routing - and subject actions, all
gone.

`Cadence` is safe, contrary to the audit: it is registered through `AddYesSqlDocumentCatalog`, which is
one document per record.

The replacement itself was always right, because it rewrites substrings. Only the match was wrong, and
only the match changed. The inner `Version=` segment is deliberately left alone: the data layer
computes the same segment when it resolves a type, and every assembly here carries the one version set
by `VersionPrefix`, so what comes out is what a read looks for. Verified against the real values in the
checked-in snapshot database rather than assumed.

The Contact Center and Telephony rewrites were checked and are unaffected - every store behind them is
a `ConcurrentDocumentCatalog`. They got the same widened match anyway, so the next type to move to a
catalog-backed store is not skipped in silence.

### Why nothing caught it, and what now does (W5.2d)

Three separate reasons, all worth remembering:

- The only test exercised a C# mirror of the rules, never the SQL, and every case in it was a flat name.
- The pre-extraction snapshot database has an **empty** `Omnichannel_Document` table, so the upgrade
  test had nothing to lose.
- `appendix-b` names this exact case - generic documents "need a nested REPLACE like
  `AIDeploymentIndexMigrations` does" - and that module already does it. The checklist existed; nobody
  was assigned to run it.

`LegacyDocumentTypeNameRewriteSqlTests` now seeds both shapes into SQLite and runs the migration's own
predicate, fetched by reflection so the test cannot drift from the statement that executes. Against the
old predicate, six of its thirteen cases fail; that was confirmed by reverting the fix and re-running,
not assumed.

### Appendix B checks 1 and 3, run and recorded (W5.2d)

They had no owner in any workstream. Both are now run:

- **Check 1 (collections):** the three flat omnichannel types are written into the `Omnichannel`
  collection; the five catalog-backed ones land in the default collection. The rewrite iterates both,
  so both are covered.
- **Check 3 (CLR names inside JSON):** the only persisted-name comparison in the moving set is
  `ContactCenterOutbox.TryResolveCompletedCheckpoint`, which matches a legacy checkpoint against the
  handler's runtime `FullName`. All nine `IContactCenterEventHandler` implementations are
  module-resident and none changed namespace, so the alias still resolves. Everything else the sweep
  found is an in-memory cache key or a log message.

### S14 exists now, and it was blocking two workstreams (W1/W5.2i/W8)

`IStartupCheck` - the seam S14 names - was never built, and the audit found it cited as a blocker in three
separate places: `AsteriskRealtimeVoiceTenantEvents` in W8, the health checks in W5.2i, and the
process-health split in W5.1 that appendix A marks as only half done.

`IContactCenterStartupCheck` is in the abstractions now, with `SharedHealthEndpointStartupCheck` as the
first implementation and one Orchard `ContactCenterStartupCheckTenantEvents` driving every registered
check on activation. Adding a check is a registration rather than another Orchard lifecycle class.

Two decisions worth recording:

- **A check reports, it does not throw.** Failing activation over a configuration mistake removes the
  administration screens an operator would use to correct it. The wrapper logs a check that could not run -
  which is a different thing from a check reporting a problem - and lets the others run.
- **The host hands over the answers rather than the check going to find them.** The first attempt gave the
  check an `IServiceProvider` so it could invoke the route resolver already on the liveness options. The
  `DependencyInjectionArchitectureTests` guard rejected that, correctly: it is service location. The shape
  now is `ISharedHealthEndpointDescriptor`, which Orchard implements over `IShellConfiguration`. Which key
  carries the route, what it falls back to, and where an operator accepts the hazard all stay host
  knowledge, and the check takes two properties.

The guard earning its keep on a first attempt is the argument for having it.

## W8.1: the Asterisk provider (in flight)

Ninety-one `.cs` files moved from `src/Modules/CrestApps.OrchardCore.Asterisk` into
`src/Core/Transitions/CrestApps.Core.Telephony.Asterisk`, leaving thirty-seven in the module: the manifest,
the startup, the settings display driver and its view model, the views, the three Orchard `DataMigration`
wrappers, the three `IBackgroundTask` wrappers, and the services that still name an Orchard type. The
package has no `OrchardCore.*` reference.

Two deviations from the plan text in [05](05-phase-1-transition.md), both deliberate:

- **`AsteriskSettings` moved**, although the plan lists it among the files that stay. It is a plain settings
  record; what makes it an Orchard site document is the display driver and the registration, and both of
  those stayed.
- **`AsteriskRealtimeVoiceTenantEvents` has not been converted onto S14** and is still a
  `ModularTenantEvents`. The seam it was waiting on landed in `e248c305`, so this is unblocked work that is
  simply not done yet. It is the one W8.1 item still open.

### A namespace sweep rewrote four string literals that are identifiers (W8.1)

The bulk namespace rewrite that moved the files also edited quoted strings, and four of them were stable
identifiers rather than type names:

| Constant | Was | Sweep made it |
| --- | --- | --- |
| `AsteriskConstants.Feature.Area` | `CrestApps.OrchardCore.Asterisk` | `CrestApps.Core.Telephony.Asterisk` |
| `ContactCenterVoiceWorkPartition` | `CrestApps.OrchardCore.Asterisk.ContactCenterVoice` | `CrestApps.Core.Telephony.Asterisk.ContactCenterVoice` |
| `ContactCenterMediaWorkPartition` | `CrestApps.OrchardCore.Asterisk.ContactCenterMedia` | `CrestApps.Core.Telephony.Asterisk.ContactCenterMedia` |
| `AsteriskDiagnostics.MeterName` | `CrestApps.OrchardCore.Asterisk` | `CrestApps.Core.Telephony.Asterisk` |

The first is the Orchard feature id. Renaming it does not rename a feature: it makes the feature every
existing tenant has enabled disappear, and it makes the shipped `contact-center-asterisk-ga-core` recipe
enable a feature id that no longer exists, which Orchard skips in silence. There is no fallback to the module id
either: OrchardCore documents that when an assembly declares at least one `[assembly: Feature]`, the module
default feature is ignored and only the declared features exist. The module declares exactly one, so the
old id would simply have stopped existing rather than surviving as a default.

The two partition keys carry an XML comment two lines above them saying the value is "intentionally kept
equal to the former feature identifier so partitioned leases and provider-command recovery survive the
upgrade". The sweep broke exactly the invariant the comment names. The meter name carries a comment saying
it must not change without a documented migration.

All four are restored, and the comments now also say why the value does not follow the namespace of the
package it sits in. The feature id itself did not simply revert: it follows the W3.1 precedent and moved to
the host as `AsteriskFeatures.Area` in `src/Modules/CrestApps.OrchardCore.Asterisk`, modelled on
`TelephonyFeatures`, so there is no longer a feature id inside a framework package for a sweep to reach.

**Why nothing caught it at the source.** `TransitionsNameNoHostTests` strips string literals before looking
for the host name, deliberately, because data-protection purposes and feature ids must keep their exact text
(see "The no-host gate ignores string literals (W0)"). That is the right call, and it means this class of
damage has to be caught another way. What caught it was the feature-activation suite refusing to find the
feature. The check that exists for it now is cheap: compare every `const string` whose value contains
`CrestApps` between `main` and the branch, and look at anything whose value changed. Run against the whole
branch it reports three names, all of them new constants rather than rewrites, so the committed workstreams
are clean and only W8 was affected.

### The document type names needed a rewrite migration, and did not have one (W8.1)

Appendix B.2 requires one per move. `AsteriskChannelTenantBinding`, `AsteriskPjsipCredentialLease` and
`AsteriskRecordingIngestJob` are YesSql documents saved through the ambient session, so their recorded
`Document.Type` still named `CrestApps.OrchardCore.Asterisk.Models.*, CrestApps.OrchardCore.Asterisk` and
none of them would have resolved after the move: an inbound call already in flight would not find the tenant
that owns it, a registered soft phone would lose the credential it authenticated with, and a recording
waiting to be fetched would never be ingested.

`AsteriskLegacyDocumentTypeNameMigrations` now does it, derived from the telephony one and registered on the
base feature so that a tenant enabling any Asterisk feature rewrites exactly once.
`AsteriskLegacyDocumentTypeNameRewriteSqlTests` seeds both stored shapes into SQLite and runs the migration
predicate itself, which is the form these tests take since W5.2d.

### Three internal helpers became public, and the package ships no friend declaration (W8.1)

The move first kept the three `internal static` helpers alive with an `AssemblyInfo.cs` granting
`InternalsVisibleTo` to `CrestApps.OrchardCore.Tests`. That is the pattern W5.1c already ruled out for the
migrations: a friend declaration does not survive the move to a package, and it writes the name of the host
into the metadata of the shipped assembly, where the no-host gate cannot see it because the gate strips
string literals. `AsteriskHangupCauseMapper`, `AsteriskRtpPacketCodec` and `AsteriskTerminalVoiceEvents` are
public now and the `AssemblyInfo.cs` is gone. No Transitions project carries a friend declaration.

`AsteriskVoiceResultMetadata` became public for a different reason: the four consumers that read its keys
stayed in the module, so it is now read across an assembly boundary. Its keys are therefore no longer
compiler-private to the provider, and the architecture gate below is what keeps them provider-private.

### Gates that stopped looking, again (W8.1)

The same failure mode as W5.1b and W5.1c, and worth stating as a rule: **a gate with a hardcoded root list
does not fail when code moves out from under it, it passes over less code.** Every check of the form "the
roots I scan exist" still passed, because the module directory still exists with thirty-seven files in it.

The trap specific to this workstream is that `CrestApps.Core.Telephony.Asterisk` is a **sibling** of
`CrestApps.Core.Telephony`, not a child, so root lists that had already grown a `CrestApps.Core.Telephony`
entry during W3 did not pick the provider package up by prefix.

| Gate | What it stopped covering |
| --- | --- |
| `ContactCenterOperationalLogPrivacyTests` | Eight moved files, fifteen logger call sites. The moved code complies, so the gate passed while enforcing nothing there. It now also names the Dialpad and Telnyx packages, which had the same gap waiting for them. |
| `AggregateLifecycleArchitectureTests` | Ninety-one of one hundred twenty-eight Asterisk files. |
| `ContactCenterWorkStateAuthorityTests` | The same, across both of its parallel lists. |
| `CallTopologyAuthorityTests` | The same. |
| `ProviderNeutralContractArchitectureTests` | It defined the owning provider as one folder. It now takes two roots, because a provider spans a module and a package. |
| `ContactCenterArchitectureGuardTests` | An allowlist entry pointing at a path that no longer exists, so the exemption had become unreachable. |
| `ContactCenterOptionsValidationTests` | `typeof(DefaultAsteriskOptions)` resolved into the package, which has no `StartupBase`, so every Asterisk option silently stopped being governed. |

**Still open, and bigger than W8.**
`CallTopologyAuthorityTests.EveryProjectThatCanSeeTheCallSession_IsScannedByTheAuthorityGate` is the
meta-gate whose stated job is to force that root list to stay complete. It discovers projects at
`src/<group>/<project>/*.csproj`, exactly two levels under `src`. Every Transitions project lives at
`src/Core/Transitions/<project>`, three levels down, so **no Transitions project has ever been evaluated by
it**. Making the discovery recurse is a two-line change, but it will then name several framework projects
that can reach `CallSession` and have never been in the list, so it is a backlog to triage rather than a fix
to land inside W8. Recorded here so it is not rediscovered a third time.

### What W8 still owes

- W8.1: convert `AsteriskRealtimeVoiceTenantEvents` onto the S14 seam.
- W8.2: Dialpad. Forty files, untouched; its framework project is still empty.
- W8.3: the Asterisk tests, deferred to W12 with the rest, following the W3.9 precedent.

## Guards that had to be repointed (W3.2)

Four architecture tests name the telephony primitive by path or assembly rather than by type. All
four now cover the framework project; two cover both it and the Orchard remnant, because until the
store package exists the primitive is in two places and a rule that only watches one of them is a
rule with a hole in it.

- `FileSizeRatchetTests` — scans the new project, and the `TelephonyHubBase.cs` ratchet entry follows it.
- `PhoneNumberCanonicalizationArchitectureTests` — guards the new root as well.
- `ContactCenterWorkStateAuthorityTests` — scans the new assembly and folder as well.
- `VoiceIngressLayeringArchitectureTests` — walks both project closures for a Contact Center reference.

### The rewrite migrations were rewriting the wrong table (W5.1a)

Moving the Contact Center models made the pre-extraction upgrade test fail, which is the first time that
gate has had anything to say. Chasing it found a defect in the rewrite pattern itself, not in the move:
the migration resolved its target with `TableNameConvention.GetDocumentTable()` and no argument, which is
the **default** collection's document table. Contact Center documents live in the `ContactCenter`
collection, and omnichannel documents in `Omnichannel` - in `<prefix>_ContactCenter_Document` and
`<prefix>_Omnichannel_Document`. The rewrite ran, reported nothing to do, and left every row it existed
for untouched.

The telephony rewrite shipped with the same defect. It passed only because no test has telephony
documents in a restored tenant; a real tenant's call history and extension directory would have gone the
same way.

All three now iterate the collections their documents were written into, skipping a collection whose
table a tenant never created. The upgrade test proves it against a real database rather than against the
rules: four documents written before the extraction are read back through the new type names.

Two lessons worth keeping. A rewrite rule that is unit-tested against strings proves the *rules*, not
that the statement reached the rows - only a restored tenant does that. And the snapshot covers four
types out of the thirty-three that have now moved, so it should be regenerated with one document per
stored type; today it would not have caught this for omnichannel.

### Eight stored types had moved with no rewrite migration (W4.3)

W4.1's first half moved forty-four omnichannel models into the framework in commit `57d65f62`. Seven of
them are stored documents - `OmnichannelActivityBatch`, `OmnichannelCampaign`,
`OmnichannelCampaignGroup`, `OmnichannelDisposition`, `OmnichannelChannelEndpoint`, `SubjectAction` and
`Cadence` - and no type-name rewrite migration went with them. `OmnichannelActivity` makes eight.

YesSql records a document's CLR type as `Namespace.TypeName, AssemblyName` and resolves it on read, so
as things stood a tenant upgrading onto this branch would have found its activities, the batches that
loaded them, its campaigns, dispositions, cadences, subject actions and channel endpoints all gone -
the whole CRM side of the product, silently. Nothing in the suite reported it: the pre-extraction
snapshot contains four Contact Center types and none of these, and every other test builds its data
from scratch.

`OmnichannelLegacyDocumentTypeNameMigrations` covers all eight, on the telephony pattern and with the
same exact-suffix assembly match for the same reason. Its tests name every one of the eight
individually rather than testing the rule once, so a ninth type moving without a rule is a failing
test rather than a silent loss.

The lesson generalises: **a commit that moves a stored type and does not touch a rewrite migration is
incomplete.** W5 moves far more stored types than W4 did.

### The activity's subject stopped being a content item, and the upgrade test did not cover it (W4.2)

P0.4 left `OmnichannelActivity.Subject` as an Orchard `ContentItem` deliberately, to be closed once the
pre-extraction upgrade test was green against a real tenant. It is closed now, and one thing found on
the way is worth recording: **the snapshot does not contain an activity.** Its manifest lists four
document types - `ActivityQueue`, `AgentProfile`, `AgentSession`, `QueueItem` - so the test the plan
named as this change's prerequisite would have passed whatever the change did to an activity.

The evidence is `ActivitySubjectCarrierTests` instead, which measures the two claims the swap rests on
rather than describing them: a content item and the node it serializes to produce identical text in
that slot, an absent subject is identical either way, and a stored node deserializes back to a content
item that is identical again. The field path `ContentItemActivitySubjectWriter` reads is pinned too,
because that service already read through `(JsonObject)activity.Subject.Content` and now reads the
node directly.

`Subject` is a `JsonObject`, and `TryResolveContact` takes the contact's id and type rather than a
content item - the model's last two content-model references. `ActivitySubjectContentItemExtensions`
in the Orchard core project is the one place that converts, so the controller, driver, export handler
and SMS handler still work in content items and nothing else has to know.

The snapshot should be regenerated from `a71550af` with an activity in it; the generator is checked in
beside it. Until that happens, the carrier is covered by the characterization tests and not by a real
tenant's bytes.

### D-4 is scoped down: contacts now, subjects and compliance later (W4)

D-4 puts a complete default CRM model in the framework - contacts, subjects, dispositions, campaigns,
cadences - so a host with no content model can manage all of it. That is net-new code rather than
extraction: Orchard registers content-item implementations of the same contracts and will never
resolve any of it, so nothing in this repository exercises it and its only coverage is the unit tests
written alongside it.

Decided 2026-09-19: build the **contacts** half now and defer the rest.

- **Now:** `OmnichannelContact` and `ContactDefinition` - store, manager, catalog handler, YesSql
  indexes under distinct table names, and `AddCoreOmnichannelContacts()`. Contacts are what the SMS
  and voice paths actually resolve against, so a standalone host without them has no working channel.
- **Deferred to Phase 4:** the subject half (`SubjectDefinition`, `OmnichannelSubject` and their
  stores), the do-not-call registry (D-13) and the contact time-zone resolver (D-14). The plan already
  puts the national DNC registries in Phase 4, and subjects are only reached through
  `ISubjectFlowSettingsService`, which a standalone host can implement against its own model.

The contracts for all of it move regardless, so deferring the implementations costs a host nothing but
the default; it is the difference between shipping a contract and shipping a product.

## Review pass against the Core repository (2026-09-19)

An independent review read the extracted projects beside `CrestApps.Core` and asked one question of
each: would this be the same code if it had been written in that repository? The answer was mostly
yes - the project split, the contracts, the store package and the hub split all match - with the
exceptions below. What was found is recorded here whether or not it was fixed, because a gap nobody
wrote down is a gap nobody closes.

### Fixed in this pass

**The builder API now exists for more than telephony, and something runs it.** `AddContactCenterSuite`
had no caller and no test, and `CrestAppsOmnichannelBuilder` was a type nothing constructed. There is
now `AddOmnichannel(...)` with `AddChannelEndpoints()` and `AddAutomation(...)`, `AddPhoneNumbers()`,
`AddWebSockets()`, and `AddYesSqlStores()` on the telephony builder - the last of which the telephony
builder's own XML doc already told hosts to call. `ContactCenterSuiteCompositionTests` composes the
suite the way the Core sample host composes the AI suite and pins what each call registers, which is
what makes the front door of the package something that runs rather than something that compiles.

**The two telephony registration classes became one `ServiceCollectionExtensions`.** That is the shape
Core uses: `CrestApps.Core.AI/ServiceCollectionExtensions.cs` holds both `AddAISuite` and the
`AddCoreAI*` methods it is sugar over. The Omnichannel primitive gained the same file.

**`AddCoreTelephony` takes an `IConfigurationSection` and has a no-configuration overload**, matching
`AddElasticsearch` / `AddAzureAISearch` / `AddPostgreSQL`, which all offer both.

**Enumerable chains register with `TryAddEnumerable`.** The index providers, the normalized voice-event
handler and the cycle runner used plain `Add*`, a form Core never uses for a chain. `AddCoreWebSockets`
used `AddSingleton` where its own remarks said a host replaces the registry, so it is `TryAddSingleton`.
Guards were added to the public registration methods that had none.

**The Orchard feature ids left the framework package.** `ContactCenterConstants.Feature` - seventeen
`CrestApps.OrchardCore.ContactCenter.*` strings - shipped inside
`CrestApps.Core.ContactCenter.Abstractions`. They are now `ContactCenterFeatures` in
`CrestApps.OrchardCore.ContactCenter.Abstractions`, which is exactly what W3.1 did for telephony, and
what [02](02-inventory-and-target-layout.md) always said should happen. The 168 call sites across 58
files were rewritten, and the seventeen string values are byte-identical, because a renamed feature id
is a feature a tenant had enabled disappearing.

**The process liveness probe stopped reading the host's configuration.** The framework middleware read
`OrchardCore_HealthChecks:Url` and defaulted to that module's route, inside a package that must not
know the host exists - and the no-host gate could not see it, because both are string literals. Which
key names the shared health route, and what it falls back to, now live in
`ContactCenterProcessHealthServiceCollectionExtensions` on the Orchard side and reach the framework
through `ContactCenterProcessLivenessOptions.SharedHealthEndpointRouteResolver`. Behaviour is
unchanged, and a host with no shared health endpoint now has nothing checked instead of being measured
against a route it does not serve.

**The MVC filter models went back to the host.** `ListOmnichannelActivityFilter` and
`BulkManageActivityFilter` carry `[BindNever]` and a `RouteValueDictionary`; they are admin-list view
models, and no framework code referenced them. They moved to
`CrestApps.OrchardCore.Omnichannel.Core.Models`, next to the filter contexts and handler contracts that
never left. That removed the last `Microsoft.AspNetCore.App` framework reference from
`CrestApps.Core.Omnichannel.Abstractions`, which is the Core rule: an abstractions package is
"framework-independent, usable in any ASP.NET Core application", not dependent on ASP.NET Core itself.

**`CrestApps.Core.ContactCenter.Abstractions` stopped declaring dependencies it does not use.** It
referenced `Hosting.Abstractions` and `Omnichannel.Abstractions` and used neither, and the three
provider packages reference it - so a host that wanted a telephony provider transitively acquired the
whole omnichannel contract set.

**A live defect: the only guard around telephony token persistence caught a type nothing throws.**
`TelephonyUserPersistenceException` survived the move to the general
`CrestApps.Core.Security.UserPersistenceException`, and both `catch` clauses in
`DefaultTelephonyAuthenticationService` still named the dead one. A failed token write therefore
escaped `CompleteAuthorizationAsync` as an unhandled exception instead of returning the friendly
result, and `GetStatusAsync` faulted instead of degrading to "not connected". The duplicate type is
deleted and both catches repointed.

**The property bag did not write what it claimed to write.** The bag serialized through
`ExtensibleEntityExtensions.JsonSerializerOptions`, with a comment saying the settings "reproduce the
host's text exactly, and a test pins that they do". Measured against the host helper, three of the four
`DateTime` shapes disagreed: the host stamps the clock components it is given with a `Z` and second
precision, while the serializer's default writes sub-second ticks, a local offset, or no suffix at all.
The single-fixture test used the one shape where they agree. The bag now has its own read-only options
with a converter that reproduces the host's format, and the test is a theory over all four shapes. The
options are also no longer the shared mutable static: that static is settable, the AI suite's options
initializer sets it, and durable tenant data must not change shape because an unrelated feature added a
converter. One difference is documented and deliberate - an `object`-typed member reads back as a
`JsonElement` rather than a CLR primitive - because no aspect stored through the bag declares one.

**`AddCoreHosting` stopped registering a queue nothing drains.** What commits a unit of work is the
store package a host chose, so the drain belongs with the store; a no-op `IAfterCommitTaskQueue` accepts
work and silently loses it, where a missing one fails at startup. The package description, which
promised an after-commit queue and "an empty user directory" that does not exist, was corrected.

**A silent soft-phone notifier default.** Three services that record what a call did take
`ITelephonySoftPhoneNotifier` as a required dependency, and only the optional
`AddCoreTelephonySoftPhoneNotifier<THub>()` registered one - so a host that wanted call history and no
soft phone could not resolve any of them. `NullTelephonySoftPhoneNotifier` is the `TryAdd` default, and
the hub-bound notifier `Replace`s it rather than shadowing it, so there is never more than one
descriptor deciding by registration order.

**`TimeProvider` in the last three places that used the clock directly**, and the background-cycle
registration extensions moved out of the `Microsoft.Extensions.DependencyInjection` namespace, where
the public-surface baselines cannot see them - the same reason W3.8 gives for the `AddCoreTelephony*`
methods living in their own namespace.

**Three Contact Center settings screens now ask the options system to refresh.** Recording governance,
secure capture and external transfer destinations were read as options and saved without a
`RequestUpdate`, so a change took effect on the next tenant restart. This is the gap the entry above
recorded as "its own task"; it is closed.

**The no-host gate sees more.** It discovers `Transitions` folders rather than listing three, reads
`.targets`, `.json`, `.cshtml` and `.razor` as well, and gained a second rule that loads every extracted
assembly and asserts nothing in `GetReferencedAssemblies()` names the host - which catches a dependency
however it was introduced, including through an imported build file or a transitive package. Both pass.

### Found and not fixed

- **`ConcurrentDocumentCatalog<T, TIndex>` is a second catalog base in a namespace Core already owns.**
  `CrestApps.Core.Data.YesSql` ships `DocumentCatalog<T, TIndex>` in `CrestApps.Core.Data.YesSql.Services`,
  and the Contact Center store package declares `ConcurrentDocumentCatalog` in that same namespace from a
  second assembly. They are largely the same code. Collapsing them needs Core's `DocumentCatalog` to
  gain the two things this one adds - an overridable concurrency check and a load hook - and its write
  methods are not virtual, so the change belongs in the Core repository. Until then the merge D-3 calls
  "namespace-neutral" would land two catalog bases side by side. **This is Phase 2's first task, not a
  copy.**
- **No `Create*IndexSchemaAsync` schema-builder extensions.** The README's stated rule is index classes
  plus schema extensions in the store package; what exists is the `ISchemaMigration` runner, and the
  runner itself is registered by nothing and called by nothing. One of the two has to become real.
- **Provider packages depend on the contact centre.** `Telephony.Telnyx/Asterisk/Dialpad` each reference
  `ContactCenter.Abstractions`, so a soft-phone-only host takes the contact centre with it. The
  dependency is real for the `IContactCenterVoice*Provider` adapters and false for the call control,
  credentials and webhook parsing in the same assembly. Splitting each provider into `<Provider>` and
  `<Provider>.ContactCenter` is cheap while the projects are empty and expensive after W7/W8 fill them.
  Recorded as a decision for W7 rather than pre-built here, because three more empty projects is not a
  split.
- **The dependency-injection snapshot never sees Telnyx or the SMS Portal**, the two largest startup
  rewrites on this branch. The support matrix has two profiles, both Asterisk/Dialpad. A baseline
  captured now would pin the shape from here on but could not prove the rewrites changed nothing, which
  is what the gate is for; the honest fix is to capture those profiles from `a71550af` the way the
  original snapshot was.
- **Core's `Directory.Packages.props` cannot restore the copied projects.** Eight package ids the
  Transitions projects reference have no `PackageVersion` there, and
  [06](06-phase-2-move-to-core.md)'s checklist lists four of them plus one that is already present. The
  list to add is `libphonenumber-csharp`, `System.Memory.Data`,
  `Microsoft.Extensions.Compliance.Abstractions`, `Microsoft.Extensions.Compliance.Redaction`,
  `Microsoft.Extensions.Caching.Abstractions`, `Microsoft.Extensions.Hosting.Abstractions`,
  `Microsoft.Extensions.TimeProvider.Testing` and `PublicApiGenerator`.
- **Suite package metadata lives in `Transitions/Directory.Build.props`, which the copy deletes.** Title
  and Description are per project and survive; `PackageTags` and the suite description do not, so the
  packages would ship with the AI suite's description unless those properties move into the csproj files
  or into a props file that travels with them.
- **Five Orchard `DataMigration` classes with real schema bodies were never moved behind
  `ISchemaMigration`**, and every `ISchemaMigration` step outside the Transitions store package is
  `internal`, so no standalone host can register one. P0.11's row states the conversion without the
  qualifier.
- **Framework unit tests still live in the Orchard test project**, W12's subject. The composition tests
  added here are in the framework project; everything covering the framework defaults is not.
- **XML docs on framework contracts still define themselves in host terms** in places. The no-host gate
  strips comments by design, so nothing catches it; it costs little to fix and nothing forces it.
- **`SanitizedLoggingExtensions.SanitizeLogValue` sits in `CrestApps.Core.Support`, a namespace Core
  already owns, beside `StringExtensions.SanitizeForLog` which does nearly the same job under a
  different name.** They are not interchangeable - one removes line breaks, the other replaces every
  control character with a space and trims - so this is a Phase 2 reconciliation rather than a delete,
  but two log sanitizers in one namespace is a choice somebody has to make once rather than a hundred
  callers making it by which `using` they happened to write.
- **Four abstractions packages take a `FrameworkReference` on `Microsoft.AspNetCore.App` for one type**,
  `OperationAuthorizationRequirement`. No `CrestApps.Core` abstractions package takes that reference; a
  `PackageReference` to `Microsoft.AspNetCore.Authorization` would do, but neither repository pins that
  package today, so it is a Phase 2 packaging decision rather than a one-line edit. Omnichannel's copy
  of this is gone because its cause was the MVC filter models, which moved.
- **`AddCoreHosting()` and the host's `AddCoreHostSeams()` register four of the same seams with
  different lifetimes** - `IScopedWorkExecutor` and `IDetachedWorkExecutor` are singletons in the
  framework defaults and scoped in the Orchard adapters. Only one of the two methods runs in any host
  today, so nothing is wrong now; a host that called both would get whichever ran first, at that one's
  lifetime. The lifetimes should agree before both can be called.

### What did not need changing

The builder types themselves are a character-for-character match for Core's: sealed, in
`CrestApps.Core.Builders`, one `Services` property, guarded constructor. Their home in
`CrestApps.Core.Hosting.Abstractions` is right rather than wrong - Core keeps every builder in
`CrestApps.Core.Abstractions/Builders`, including `CrestAppsMcpServerBuilder`, whose feature lives in a
different package - and `Hosting.Abstractions` folds into `CrestApps.Core.Abstractions` at Phase 2 by
D-7. The `AddCoreHosting()` call inside `AddContactCenterSuite` mirrors what `AddAISuite` does
unconditionally. No extracted assembly references the host, and the assembly-level rule added here
proves it rather than inferring it from text.
