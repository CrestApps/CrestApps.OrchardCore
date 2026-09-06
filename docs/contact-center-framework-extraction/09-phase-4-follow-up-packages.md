# 09 - Phase 4: follow-up packages (reports, compliance registries, Redis, Azure, time zones)

Phase 4 collects the framework packages that a standalone host needs for a complete contact center but that are not required to prove the split in Phases 1-2. It starts after Phase 2 (packages published) and can overlap with Phase 3; the reports package should land **before** the Phase 3 reports screens are built. Each package follows the same rules as everything else (no Orchard references, builder method + `AddCore*` method, docs page, tests in the Core repository).

Not in scope of any phase, by decision (D-18): Orchard workflows, recipes, deployment steps, audit trail, content transfer, widgets. These stay Orchard-only integrations of framework events and models; an MVC host uses `IContactCenterEventHandler`/`IOmnichannelEventHandler` and its own configuration files instead.

## P4.1 - Reports framework (`CrestApps.Core.Reports.*`)

Today the Contact Center and Omnichannel report providers implement `IReport` from `CrestApps.OrchardCore.Reports.Abstractions`, run through `ReportManager`/`ReportExportManager` in `CrestApps.OrchardCore.Reports.Core`, and render in the `CrestApps.OrchardCore.Reports` admin module (CSV export in core, Excel in `Reports.OpenXml`). The abstractions are already almost framework-clean (27 files; only `IReport.Permission` uses `OrchardCore.Security.Permissions` and one file uses `OrchardCore.Entities`).

| Package | Source | Contents |
| --- | --- | --- |
| `CrestApps.Core.Reports.Abstractions` | `CrestApps.OrchardCore.Reports.Abstractions` | `IReport` (with `Permission` replaced by `string AuthorizationPolicy` / an `OperationAuthorizationRequirement` per report, S8 style), `IReportProvider`, `IReportExportFormat`, `IReportFilterMetadata`, `ReportContext`, `ReportValue`, `ReportFilterExtensions`, `Models/*` (document, section, row, column, chart, metric, style, date range, filter), `Services/IReportManager`, `IReportExportManager` |
| `CrestApps.Core.Reports` | `CrestApps.OrchardCore.Reports.Core` + `ReportDisplayValueResolver` from the module | `ReportManager`, `ReportExportManager`, `CsvReportExportFormat`, `ReportDisplayValueResolver`, `AddCoreReports()`, `suite.AddReports(r => r.AddCsvExport())`, report registration helpers (`AddReport<T>()`) |
| `CrestApps.Core.Reports.OpenXml` | `CrestApps.OrchardCore.Reports.OpenXml` | `ExcelReportExportFormat`, `ExcelStyleRegistry`, `AddExcelExport()` |
| Report providers | `CrestApps.OrchardCore.ContactCenter/Reports/Providers/*`, `Omnichannel.Managements/Reports/*` (`ActivitySummaryReportProvider`, `CampaignPerformanceReportProvider`, `DispositionBreakdownReportProvider`, `EnterpriseActivityReportProvider`, `HandoffContainmentReportProvider`, aggregators, `OmnichannelReportBase`, `OmnichannelReportFilter`) | move into `CrestApps.Core.ContactCenter/Reports` and `CrestApps.Core.Omnichannel/Reports` (or `CrestApps.Core.ContactCenter.Reports` if the dependency on the reports package should stay optional); `IContactCenterCapabilityDependentReport` and `ContactCenterReportCapabilityRequirements` move with them and key on `ContactCenterCapabilities` |

Orchard side: `CrestApps.OrchardCore.Reports` keeps the admin controller, views, filter display drivers, admin menu, and permission mapping (`IReport.AuthorizationPolicy` -> `Permission`); the Orchard abstractions/core projects are deleted and the Orchard module references the framework packages. The Contact Center `AnalyticsStartup` and Omnichannel `ReportsStartup` register the framework report providers.

MVC sample (Phase 3): a `Reports` area that lists `IReportManager` reports, renders sections/tables/charts from `ReportDocument`, applies date-range filters, and exports CSV/Excel.

## P4.2 - Do-not-call registries (`CrestApps.Core.Omnichannel.Compliance.DncRegistry`)

Source: `CrestApps.OrchardCore.DncRegistry` services and models (`LocalDncRegistry`, `DefaultLocalDncListManager`, `ILocalDncFileStore`/`LocalDncFileStore`, `UsaFtcDncRegistry`, `CanadaDnclRegistry`, `LocalDncList`, `LocalDncEntry`, `ImportLocalDncList`, options/settings, indexes, import background task) and `CrestApps.OrchardCore.DncRegistry.Azure` (blob-backed file store).

- Framework package registers `INationalDoNotCallRegistry` implementations (replacing the Phase 1 default `ContactPreferenceDoNotCallRegistry`), a local list store with YesSql index, an import cycle, and options (`LocalDncListOptions`, USA FTC and Canada DNCL settings as options POCOs). `IDncListFileStore` gets a local file-system default; an Azure blob variant lives in `CrestApps.Core.Omnichannel.Compliance.DncRegistry.Azure`.
- Orchard modules keep the settings screens, admin menus, permissions, controllers, display drivers, and register the framework services.
- MVC sample: import screen for the local list and settings for the national registries.

## P4.3 - Redis infrastructure (`CrestApps.Core.Redis`)

- `RedisDistributedLockProvider` (`IDistributedLockProvider` over `StackExchange.Redis` with expiration and timeout semantics matching the Orchard implementation).
- `RedisRendezvousOwnerStore` (from `CrestApps.OrchardCore.WebSockets/Services/RedisRendezvousOwnerStore.cs`, rewritten on `IConnectionMultiplexer`).
- Redis-backed `ISmsAgentPresenceTracker` if `IDistributedCache` proves insufficient for multi-node presence.
- `suite.AddRedis(configuration)` registering all three; Orchard keeps using `OrchardCore.Redis` and its own adapters.
- Documentation: multi-node deployment page (SignalR backplane via `AddStackExchangeRedis`, lock provider, rendezvous, health checks).

## P4.4 - Azure integrations

- `CrestApps.Core.Telephony.Azure`: `AzureBlobRecordingMediaStore` on `Azure.Storage.Blobs` with the same encryption-at-rest as the local store (source: `CrestApps.OrchardCore.Telephony.Azure`). Orchard's module switches to it if it can drop `OrchardCore.FileStorage.AzureBlob`; otherwise it keeps its own.
- `CrestApps.Core.Omnichannel.Azure.EventGrid`: `EventGridOptions` + `MapOmnichannelEventGridEndpoint` (source: `CrestApps.OrchardCore.Omnichannel.EventGrid`).
- `CrestApps.Core.Sms.Azure` (optional): Azure Communication Services SMS provider implementing the framework `ISmsProvider`, so the MVC sample can use ACS as well as Telnyx and Twilio.

## P4.5 - Time-zone map (`TimeZoneMap` in `CrestApps.Core.Omnichannel`)

If the Phase 1 libphonenumber-based `IContactTimeZoneResolver` default is not accurate enough for North American area codes, move the Orchard `TimeZoneMap` model (`CrestApps.OrchardCore.TimeZones/Models/TimeZoneMap.cs`, handler, migration) into the framework as a catalog with a seeded default map (from the Orchard recipe data) and `TimeZoneMapContactTimeZoneResolver`; the Orchard TimeZones module keeps its admin screens and registers the framework services.

## P4.6 - Contact import (`IOmnichannelContactImporter`)

CSV import of contacts into the framework default model (mapping rows to `OmnichannelContact` through `IOmnichannelContactWriter`, duplicate lookup through `IOmnichannelContactSearch`, phone normalization, do-not-contact flags). Orchard keeps Content Transfer for content-item contacts. MVC sample gets an upload screen.

## Ordering and gates

| Order | Package | Depends on | Gate |
| --- | --- | --- | --- |
| 1 | P4.1 Reports | Phase 2 | Core build/tests/docs green; Orchard Reports module on packages; report outputs byte-identical for the same data (snapshot test) |
| 2 | P4.2 DNC registries | Phase 2, P4.1 (none) | dialer compliance tests pass with the framework registries; Orchard DNC screens unchanged |
| 3 | P4.3 Redis | Phase 2 | `DistributedTests` equivalent in the Core repo against a Redis container |
| 4 | P4.4 Azure | Phase 2 | recording round-trip against Azurite; Event Grid signature tests |
| 5 | P4.5 Time-zone map | Phase 1 W4.2b outcome | resolver accuracy test set |
| 6 | P4.6 Contact import | Phase 3 contact screens | import of the sample CSV |

Each package adds a page under `docs/contact-center/` in the Core docs and a changelog entry.
