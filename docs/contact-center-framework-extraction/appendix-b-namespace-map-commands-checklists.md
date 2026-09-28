# Appendix B - Namespace map, stored type-name migrations, commands, checklists

## B.1 Namespace and assembly map

Apply with a scripted find/replace over `using` directives, `namespace` declarations, fully qualified names, `InternalsVisibleTo`, test baselines, and docs. Order matters: longest prefixes first.

| Old namespace prefix | New namespace prefix | New assembly |
| --- | --- | --- |
| `CrestApps.OrchardCore.ContactCenter.Core.Models.Reports` | `CrestApps.Core.ContactCenter.Models.Reports` | `CrestApps.Core.ContactCenter` |
| `CrestApps.OrchardCore.ContactCenter.Core.Services.Retention` | `CrestApps.Core.ContactCenter.Services.Retention` | `CrestApps.Core.ContactCenter` |
| `CrestApps.OrchardCore.ContactCenter.Core.Services` | `CrestApps.Core.ContactCenter.Services` | `CrestApps.Core.ContactCenter` |
| `CrestApps.OrchardCore.ContactCenter.Core.Models` | `CrestApps.Core.ContactCenter.Models` | `CrestApps.Core.ContactCenter` |
| `CrestApps.OrchardCore.ContactCenter.Core.Indexes` | `CrestApps.Core.Data.YesSql.Indexes.ContactCenter` | `CrestApps.Core.Data.YesSql.ContactCenter` |
| `CrestApps.OrchardCore.ContactCenter.Core.HealthChecks` | `CrestApps.Core.ContactCenter.HealthChecks` | `CrestApps.Core.ContactCenter` |
| `CrestApps.OrchardCore.ContactCenter.Core.Telemetry` | `CrestApps.Core.ContactCenter.Telemetry` | `CrestApps.Core.ContactCenter` |
| `CrestApps.OrchardCore.ContactCenter.Core` | `CrestApps.Core.ContactCenter` | `CrestApps.Core.ContactCenter` |
| `CrestApps.OrchardCore.ContactCenter.Models` (abstractions) | `CrestApps.Core.ContactCenter.Models` | `CrestApps.Core.ContactCenter.Abstractions` |
| `CrestApps.OrchardCore.ContactCenter.Services` (abstractions) | `CrestApps.Core.ContactCenter.Services` | `CrestApps.Core.ContactCenter.Abstractions` |
| `CrestApps.OrchardCore.ContactCenter.Hubs` | `CrestApps.Core.ContactCenter.Hubs` (base + client) / stays for the Orchard hub | `CrestApps.Core.ContactCenter` |
| `CrestApps.OrchardCore.ContactCenter.Endpoints` | `CrestApps.Core.ContactCenter.Endpoints` | `CrestApps.Core.ContactCenter` |
| `CrestApps.OrchardCore.ContactCenter.Handlers` | `CrestApps.Core.ContactCenter.Handlers` | `CrestApps.Core.ContactCenter` |
| `CrestApps.OrchardCore.ContactCenter.Services` (module) | `CrestApps.Core.ContactCenter.Services` | `CrestApps.Core.ContactCenter` |
| `CrestApps.OrchardCore.ContactCenter.BackgroundTasks` | `CrestApps.Core.ContactCenter.BackgroundWork` (cycles); wrappers stay | `CrestApps.Core.ContactCenter` |
| `CrestApps.OrchardCore.ContactCenter.Indexes` (providers) | `CrestApps.Core.Data.YesSql.Indexes.ContactCenter` | `CrestApps.Core.Data.YesSql.ContactCenter` |
| `CrestApps.OrchardCore.ContactCenter.Migrations` (`ContactCenterMigrationSql`) | `CrestApps.Core.Data.YesSql.Migrations` | `CrestApps.Core.Data.YesSql.ContactCenter` |
| `CrestApps.OrchardCore.ContactCenter` (abstractions root, e.g. `ContactCenterConstants`) | `CrestApps.Core.ContactCenter` | `CrestApps.Core.ContactCenter.Abstractions` |
| `CrestApps.OrchardCore.Telephony.Core.Services` / `.Models` / `.Indexes` | `CrestApps.Core.Telephony.Services` / `.Models` / `CrestApps.Core.Data.YesSql.Indexes.Telephony` | `CrestApps.Core.Telephony` / store |
| `CrestApps.OrchardCore.Telephony.Models` / `.Services` / `.Extensions` | `CrestApps.Core.Telephony.Models` / `.Services` / `.Extensions` | `CrestApps.Core.Telephony.Abstractions` |
| `CrestApps.OrchardCore.Telephony.Hubs` / `.Endpoints` / `.Services` (module) | `CrestApps.Core.Telephony.Hubs` / `.Endpoints` / `.Services` | `CrestApps.Core.Telephony` |
| `CrestApps.OrchardCore.Telephony` | `CrestApps.Core.Telephony` | `CrestApps.Core.Telephony.Abstractions` |
| `CrestApps.OrchardCore.Telnyx.Core.Services` / `.Models` / `.Indexes` | `CrestApps.Core.Telephony.Telnyx.Services` / `.Models` / `.Data.YesSql` | `CrestApps.Core.Telephony.Telnyx` |
| `CrestApps.OrchardCore.Telnyx.*` (module) | `CrestApps.Core.Telephony.Telnyx.*` | `CrestApps.Core.Telephony.Telnyx` |
| `CrestApps.OrchardCore.Asterisk.*` | `CrestApps.Core.Telephony.Asterisk.*` | `CrestApps.Core.Telephony.Asterisk` |
| `CrestApps.OrchardCore.Dialpad.*` | `CrestApps.Core.Telephony.Dialpad.*` | `CrestApps.Core.Telephony.Dialpad` |
| `CrestApps.OrchardCore.Omnichannel.Core.Models` | `CrestApps.Core.Omnichannel.Models` | `CrestApps.Core.Omnichannel.Abstractions` |
| `CrestApps.OrchardCore.Omnichannel.Core.Services` | `CrestApps.Core.Omnichannel.Services` | abstractions (interfaces) / `CrestApps.Core.Omnichannel` (implementations) |
| `CrestApps.OrchardCore.Omnichannel.Core.Indexes` | `CrestApps.Core.Data.YesSql.Indexes.Omnichannel` | store |
| `CrestApps.OrchardCore.Omnichannel.Core` | `CrestApps.Core.Omnichannel` | `CrestApps.Core.Omnichannel.Abstractions` |
| `CrestApps.OrchardCore.Omnichannel.Managements.Services` / `.Handlers` / `.Tools` (moved subset) | `CrestApps.Core.Omnichannel.Services` / `.Handlers` / `.Tools` | `CrestApps.Core.Omnichannel` |
| `CrestApps.OrchardCore.Omnichannel.Sms.*` | `CrestApps.Core.Omnichannel.Sms.*` | `CrestApps.Core.Omnichannel.Sms` |
| `CrestApps.OrchardCore.Omnichannel.Voice.Core.*` | `CrestApps.Core.Omnichannel.Voice.*` | `CrestApps.Core.Omnichannel.Voice` |
| `CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.*` | `CrestApps.Core.Omnichannel.Sms.Portal.*` | `CrestApps.Core.Omnichannel.Sms.Portal` |
| `CrestApps.OrchardCore.Omnichannel.Sms.Portal.*` (abstractions) | `CrestApps.Core.Omnichannel.Sms.Portal.*` | `CrestApps.Core.Omnichannel.Sms.Portal.Abstractions` |
| `CrestApps.OrchardCore.Omnichannel.Sms.Portal.Indexes` | `CrestApps.Core.Data.YesSql.Indexes.SmsPortal` | store |
| `CrestApps.OrchardCore.PhoneNumbers.*` | `CrestApps.Core.PhoneNumbers.*` | `CrestApps.Core.PhoneNumbers(.Abstractions)` |
| `CrestApps.OrchardCore.WebSockets.*` | `CrestApps.Core.WebSockets.*` | `CrestApps.Core.WebSockets` |
| `CrestApps.OrchardCore.SignalR.Core` | `CrestApps.Core.SignalR` | `CrestApps.Core.Hosting` (Phase 1) / `CrestApps.Core.SignalR` (Phase 2) |
| `CrestApps.OrchardCore.YesSql.Core.Services.DocumentCatalog` | `CrestApps.Core.Data.YesSql.Services.ConcurrentDocumentCatalog` | store |
| `CrestApps.OrchardCore.DncRegistry` (contracts) | `CrestApps.Core.Omnichannel.Compliance` | `CrestApps.Core.Omnichannel.Abstractions` |
| `CrestApps.OrchardCore.Diagnostics.LogDataClassifications` | `CrestApps.Core.Diagnostics.LogDataClassifications` | `CrestApps.Core.Hosting.Abstractions` (Phase 1) / `CrestApps.Core.Infrastructure.Abstractions` (Phase 2) |

Name collisions to resolve during the move (both halves land in the same namespace):

- `CrestApps.OrchardCore.ContactCenter.Models` (abstractions) and `CrestApps.OrchardCore.ContactCenter.Core.Models` (core) both map to `CrestApps.Core.ContactCenter.Models`. Check for duplicate simple names before moving (`comm -12 <(ls Abstractions/.../Models) <(ls Core/.../Models)`); today the survey found none, but `TransferRequest` exists in both Telephony abstractions and Contact Center core models (different namespaces, no clash).
- `CrestApps.OrchardCore.ContactCenter.Services` (abstractions, 4 files) and the two `Services` folders in core and module all map to `CrestApps.Core.ContactCenter.Services`; the abstractions half lives in the abstractions assembly, the rest in the primitive, same namespace (this mirrors `CrestApps.Core.AI.Chat` vs `CrestApps.Core.AI.Abstractions` sharing `CrestApps.Core.AI.Chat`).

## B.2 Stored document type-name rewrite table

Every row is a `(LegacyNamespacePrefix, LegacyAssemblyName, CurrentNamespacePrefix, CurrentAssemblyName, Collection)` entry for a `*LegacyDocumentTypeNameMigrations` class modelled on `src/Modules/CrestApps.OrchardCore.AI/Migrations/AILegacyDocumentTypeNameMigrations.cs`. One migration class per Orchard module that owns the collection. The rewrite runs as a deferred task on `Create`/`UpdateFromN`, is idempotent (`WHERE Type LIKE ...`), and logs the count. Add an `UpdateFrom` step whenever a new row is added later.

| Orchard module (migration class) | Collection | Legacy namespace prefix | Legacy assembly | Current namespace prefix | Current assembly |
| --- | --- | --- | --- | --- | --- |
| ContactCenter (`ContactCenterLegacyDocumentTypeNameMigrations`) | `ContactCenter` (`ContactCenterStoreOptions.CollectionName`) | `CrestApps.OrchardCore.ContactCenter.Core.Models.` | `CrestApps.OrchardCore.ContactCenter.Core` | `CrestApps.Core.ContactCenter.Models.` | `CrestApps.Core.ContactCenter` |
| ContactCenter | `ContactCenter` | `CrestApps.OrchardCore.ContactCenter.Models.` | `CrestApps.OrchardCore.ContactCenter.Abstractions` | `CrestApps.Core.ContactCenter.Models.` | `CrestApps.Core.ContactCenter.Abstractions` |
| ContactCenter | default (`null`) - check whether any Contact Center document is saved outside the collection (`grep -rn "SaveAsync(" src/Core/CrestApps.OrchardCore.ContactCenter.Core | grep -v CollectionName`) | same as above | same | same | same |
| Telephony (`TelephonyLegacyDocumentTypeNameMigrations`) | default | `CrestApps.OrchardCore.Telephony.Models.` | `CrestApps.OrchardCore.Telephony.Abstractions` | `CrestApps.Core.Telephony.Models.` | `CrestApps.Core.Telephony.Abstractions` |
| Telephony | default | `CrestApps.OrchardCore.Telephony.Core.Models.` | `CrestApps.OrchardCore.Telephony.Core` | `CrestApps.Core.Telephony.Models.` | `CrestApps.Core.Telephony` |
| Telephony | default | `CrestApps.OrchardCore.Telephony.Models.` (module `Models/TelephonyUserConnections`) | `CrestApps.OrchardCore.Telephony` | `CrestApps.Core.Telephony.Models.` | `CrestApps.Core.Telephony` |
| Omnichannel.Managements (`OmnichannelLegacyDocumentTypeNameMigrations`) | default | `CrestApps.OrchardCore.Omnichannel.Core.Models.` | `CrestApps.OrchardCore.Omnichannel.Core` | `CrestApps.Core.Omnichannel.Models.` | `CrestApps.Core.Omnichannel.Abstractions` |
| Omnichannel.Sms.Portal (`SmsPortalLegacyDocumentTypeNameMigrations`) | `SmsWorkspace` | `CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models.` | `CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core` | `CrestApps.Core.Omnichannel.Sms.Portal.Models.` | `CrestApps.Core.Omnichannel.Sms.Portal` |
| Omnichannel.Sms.Portal | `SmsWorkspace` | `CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models.` | `CrestApps.OrchardCore.Omnichannel.Sms.Portal.Abstractions` | `CrestApps.Core.Omnichannel.Sms.Portal.Models.` | `CrestApps.Core.Omnichannel.Sms.Portal.Abstractions` |
| Telnyx (`TelnyxLegacyDocumentTypeNameMigrations`) | default | `CrestApps.OrchardCore.Telnyx.Core.Models.` | `CrestApps.OrchardCore.Telnyx.Core` | `CrestApps.Core.Telephony.Telnyx.Models.` | `CrestApps.Core.Telephony.Telnyx` |
| Asterisk (`AsteriskLegacyDocumentTypeNameMigrations`) | default | `CrestApps.OrchardCore.Asterisk.Models.` | `CrestApps.OrchardCore.Asterisk` | `CrestApps.Core.Telephony.Asterisk.Models.` | `CrestApps.Core.Telephony.Asterisk` |

Checks before finalising the table:

1. Confirm the collection of every stored type by reading its store (`CollectionName` in the `DocumentCatalog` subclass or the `collection:` argument to `SaveAsync`).
2. Search for generic documents (`DictionaryDocument<` and any `Document<T>` wrapper): their `Type` value contains the argument type and needs a nested REPLACE like `AIDeploymentIndexMigrations` does.
3. Search for type names stored inside JSON payloads (event envelopes, outbox messages, `ContactCenterOutbox` handler ids - see `ContactCenterOutbox.cs` around line 476 where a legacy handler type name is compared) and add data-level rewrites if a full CLR name is persisted. The outbox already tolerates a legacy prefix; extend its list rather than rewriting rows.
4. Write an upgrade test: create a SQLite database with `main`'s assemblies (or a checked-in snapshot like `tests/.../Migrations/SqliteSchemaSnapshot`), run the branch's migrations, assert every collection's `Document.Type` values start with `CrestApps.Core.` and the documents load through the framework stores.

## B.3 Commands

```bash
# Inventory of Orchard usages inside a project (run before moving it)
grep -rhoE '^using OrchardCore[A-Za-z.]*' src/Core/CrestApps.OrchardCore.ContactCenter.Core --include=*.cs | sort | uniq -c | sort -rn

# Move a file with history
git mv src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/AgentProfileManager.cs src/Core/Transitions/CrestApps.Core.ContactCenter/Services/AgentProfileManager.cs

# Namespace rewrite inside Transitions (PowerShell; one prefix at a time, longest first)
Get-ChildItem src\Core\Transitions -Recurse -Include *.cs | ForEach-Object {
  (Get-Content $_.FullName -Raw) -replace 'CrestApps\.OrchardCore\.ContactCenter\.Core\.Services', 'CrestApps.Core.ContactCenter.Services' | Set-Content $_.FullName -NoNewline
}

# Framework-must-be-Orchard-free gate
grep -rlE "OrchardCore" src/Abstractions/Transitions src/Core/Transitions src/Modules/Transitions tests/Transitions --include=*.cs --include=*.csproj --include=*.props

# Build like CI
dotnet build CrestApps.OrchardCore.slnx -c Release -warnaserror /p:TreatWarningsAsErrors=true /p:RunAnalyzers=true /p:NuGetAudit=false

# Tests (Microsoft Testing Platform runner)
dotnet test tests/Transitions/CrestApps.Core.ContactCenter.Tests -c Release /p:NuGetAudit=false
dotnet test tests/CrestApps.OrchardCore.Tests -c Release /p:NuGetAudit=false
dotnet test tests/CrestApps.OrchardCore.ContactCenter.FeatureActivationTests -c Release /p:NuGetAudit=false
dotnet test tests/CrestApps.OrchardCore.ContactCenter.DistributedTests -c Release /p:NuGetAudit=false

# Public API baselines (delete the approved file and rerun the approval test to regenerate)
rm tests/CrestApps.OrchardCore.Tests/PublicApi/Baselines/CrestApps.OrchardCore.ContactCenter.Core.approved.txt

# Manifest diff gate
git diff main -- "src/Modules/**/Manifest.cs"

# Docs
cd src/CrestApps.Docs && npm run build
```

## B.4 Per-file move checklist

For every file moved into a `Transitions` project:

- [ ] `git mv` used; file compiles in the new project.
- [ ] Namespace rewritten per B.1; no `CrestApps.OrchardCore` `using` left.
- [ ] `OrchardCore.*` usings removed; each one replaced by the seam in [03](03-host-seams.md) (S1-S20) or by a Microsoft abstraction.
- [ ] Public types `sealed` unless inherited; XML docs on every public type/member/parameter; blank-line and `return` rules applied.
- [ ] `DateTime.UtcNow` absent; `TimeProvider` injected.
- [ ] If persisted: JSON property names unchanged; row added to B.2; upgrade test covers it.
- [ ] If it registers services: `TryAdd` for defaults, `TryAddEnumerable` for handlers, exposed through an `AddCore*` method and a builder method.
- [ ] If it was an `IBackgroundTask`: cycle extracted; Orchard wrapper kept with the same `[BackgroundTask]` attribute.
- [ ] If it was a hub: base class generic over the client interface; tenant name from `ITenantAccessor`; concrete Orchard hub sealed with `[Authorize]`.
- [ ] If it consulted permissions: framework operation + resource; Orchard mapping handler added; permission id unchanged.
- [ ] Tests for the type moved to the transition test project; Orchard doubles replaced.
- [ ] No competitor product names introduced.

## B.5 Per-workstream pull request checklist

- [ ] Grep gate green.
- [ ] Release build with `-warnaserror` green.
- [ ] All four test projects green (DistributedTests with the documented environment variables).
- [ ] `git diff main -- src/Modules/**/Manifest.cs` empty.
- [ ] Public API baselines regenerated only for assemblies that changed; diffs reviewed.
- [ ] Production-readiness changelog entry added.
- [ ] Appendix A/B updated if the survey turned out to be wrong for a file (record it, do not silently diverge).
