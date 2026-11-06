# Building blocks: reuse before you build

This solution is a set of building blocks. Each module owns one concept (an account, a team, an address, a calendar, a skill, money, a report) and other modules compose it into products. A new module reuses the building blocks that exist, extends them through their extension points, and adds a new building block only when the concept is missing. It never copies a concept into a second place.

Read this page before you add a module, a record type, a settings page or a shared helper. `AGENTS.md` (section *Building Blocks and Reuse*) holds the binding rules; this page is the catalog those rules point to. The findings that led to it, and the plan to remove today's duplicates, are in [`reusability-audit-2026-11.md`](reusability-audit-2026-11.md).

## 1. The reuse ladder

Take the first step that works:

1. **Use** an existing building block as it is: reference its `.Abstractions` project, declare its feature as a dependency (or gate your integration with `[RequireFeatures]`), and call its contracts.
2. **Extend** it through its extension points: a display driver or shape on its record, a content part, a handler or observer, a provider interface, an `AddXxx` registration (for example `AddCalendarType`, `AddReport`), or its `Properties` bag.
3. **Add the missing extension point** to the building block (a new interface, handler hook, driver-based editor or open "kind" value), then use it. Change the owner; do not work around it.
4. **Extract a new building block** when a second module needs a concept that only one module has. Move the concept to its own `X.Abstractions` / `X.Core` / module (or into an existing one that fits), and point both modules at it in the same plan. The second copy is the trigger, not the third.
5. **Keep it private** only when the concept is truly specific to one module. Give it a name that does not collide with a building block (`ReceiptNumber`, not `Address`), and say in the pull request why no building block fits.

Before you start, search for the concept by name and by meaning (for example "hours", "opening", "schedule", "availability" for business hours), read the catalog below, and check `src/Targets/CrestApps.OrchardCore.Cms.Core.Targets` for a module that already owns it.

## 2. Layers and references

| Layer | Holds | May reference |
| --- | --- | --- |
| `CrestApps.OrchardCore.X.Abstractions` | Contracts (interfaces), records and value types others read, constants, feature IDs, events | Other `.Abstractions` projects, Orchard Core `*.Abstractions`, CrestApps.Core abstractions |
| `CrestApps.OrchardCore.X.Core` | Implementations, stores, indexes, background logic, and anything two modules share, UI building blocks included (base drivers, shared view models, tag helpers, view components). No module-specific views, controllers or endpoints | Its own `.Abstractions`, other `.Abstractions`, shared platform libraries (`Core`, `YesSql.Core`) |
| `CrestApps.OrchardCore.X` (module) | Manifest, startups, drivers, controllers, views, migrations, recipe and deployment steps, endpoints | Its own `.Core` and `.Abstractions`, other modules' `.Abstractions` |
| Integration (`[RequireFeatures]` startup, or a bridge module `X.Y`) | Code that makes two building blocks work together | Both sides' `.Abstractions` |

Rules that follow from the table:

- A module may reference another module project only to carry the feature dependency (the manifest declares it, and the reference makes the dependency's NuGet package come along). Module code, including compiled views, never uses a type from another module assembly: no services, models, view models, tag helpers or view components. Whatever two modules share lives in the owner's `.Abstractions` or `.Core`. There is no exception for sub-modules, providers or `Resources`.
- A `.Core` library never references a module project, and an `.Abstractions` library never references a `.Core` library or a module project.
- Reference another building block's `.Core` only to reuse an implementation base class; reference `.Abstractions` for everything else. Remove references nothing uses.
- A building block never names its consumers. No hard-coded tabs, enum values, feature IDs or types of the modules that build on it: consumers register themselves (an open string "kind", a registration helper, a placement position).
- Optional integrations (AI tools, reports, workflows, another domain module) live in a `[RequireFeatures]` startup in the consumer, or in a bridge module, so the building block works without them.
- Domain types never go into the solution-wide `CrestApps.OrchardCore.Abstractions`; they go into their domain's `.Abstractions`.
- Feature IDs are constants in the owning `.Abstractions` project, never string literals repeated in `[RequireFeatures]`, manifests or recipes.

## 3. Catalog

"Gap" marks a building block that is not complete yet. Until the plan in the audit closes it, extend the named owner rather than adding another copy.

### People and organizations

| Concept | Owner (feature) | Reuse it through | Do not |
| --- | --- | --- | --- |
| A person who signs in | Orchard Users + **Users** (`CrestApps.OrchardCore.Users`) | `UserFullNamePart`, `UserAvatarPart`, `IDisplayNameProvider` and `IUserSearchService` (Users.Abstractions, behind the `SearchUsers` permission), the user picker by shape name, `IRoleGrantService` to grant a role on behalf of a source | Copy user names or e-mail into your records, or add or remove roles directly |
| An organization (company, household, B2B buyer) | **Accounts** | `AccountPart` content type, `IAccountMemberService`, `IAccountContactFinder`, `IAccountChildTypeRule`, account page tabs, the account picker, `ListPart` child records | Add a `CompanyName`/`TaxId`/`Kind=Business` to your own record or create another "company" or "vendor" record. Gap: `Customer` still carries business fields and `Vendor` is its own record |
| Organization extensions (B2B terms, price lists) | **Accounts.B2B** | A content part on the Account type | Put commerce fields on Accounts itself |
| A commerce buyer | **Customers** | `Customer`, `ICustomerHandler`, `ICustomerMergeHandler`, `ICustomerPrivacyHandler` | Treat `Customer` as an organization. Gap: no link from Customer to Account yet |
| A CRM contact, lead or opportunity | **Omnichannel.Managements** (CRM) | Contact content items with `OmnichannelContactPart`, `IOmnichannelContactTypeProvider` | Re-model contacts in another module. Gap: contact contracts sit in `Omnichannel.Core`, not in an Abstractions project |
| A group of people | **Teams** | `Team` (tree, kind, time zone), `ITeamManager`, `ITeamMembershipService` and `ITeamMemberProvider` (Teams.Abstractions; Workforce supplies the members), `ITeamDeletionGuard`, `DisplayDriver<Team>` | Use a role as a team, or keep your own member list. Team calendars still belong to a linked role; Teams membership adds write access |
| Skills and certifications | **Skills** | `ISkillAssignmentService`, `ISkillAssignmentHandler`, `ISkillSubjectLinkProvider` (a worker and its user share skills), open `SkillSubjectKind` strings, `SkillSubjectSkillsDisplayDriverBase<T>` (Skills.Core) for a skills section on any editor | Keep another skill list. Gap: Contact Center routing still reads its `AgentProfile.SkillProficiencies` mirror |
| A child tenant and access to it | **TenantHierarchy** | `ITenantHierarchyBroker`, `IChildTenantDatabaseProvisioner` | Create or set up tenants yourself (`ISetupService`, `IShellSettingsManager`) |
| A party a record belongs to (user, customer, account, contact) | **Parties** (`Parties.Abstractions`) | `PartyRef { Kind, Id }`, `IPartySource` per kind, `IPartyDirectory` | Add another owner-kind enum and lookup. Gap: Orders, Transactions, Subscriptions, Inventory and Calendars do not use `PartyRef` yet |
| Contact details snapshot | **Addresses** (`ContactInfo`) and **PhoneNumbers** | `ContactInfo { Name, Email, Phone, Address }`, `PhoneNumberNormalization.ToE164OrTrimmed` | Re-declare name, e-mail and phone fields, or store a phone without normalizing it |

### Places and contact details

| Concept | Owner (feature) | Reuse it through | Do not |
| --- | --- | --- | --- |
| A postal address | **Addresses** | `Address` value type, `IAddressFormatter`, `IAddressValidator`, the address editor shape (`IAddressEditorService`), the country catalog | Declare `AddressLine1`/`City`/`PostalCode` fields or store an address as one string |
| A named place (site, warehouse, pickup point, home base) | **Addresses** | `NamedAddress { Id, Name, Address }` (Addresses.Abstractions); the store's places through `IStoreLocationService` and `IStoreLocationSource` (Commerce.Abstractions) | Add another location record with a free-text address. Gap: stock locations, pickup locations and registers do not use `NamedAddress` yet |
| Coordinates, geocoding, routing, territories | **Geo** (+ providers) | `GeoPoint`, `IGeocodingProvider`, `IGeocodeQueue` + `IGeocodeTarget`, `ServiceTerritory`, `ITerritoryMemberSource` | Store raw latitude and longitude pairs or call a maps API directly |
| A phone number | **PhoneNumbers** | `PhoneNumber` (E.164 value type), `IPhoneNumberService` to parse and normalize | Store a phone as an unnormalized string; it can then never match a CRM, DNC or caller-ID number |
| Phone verification | **PhoneNumbers.Verifications** | `IPhoneNumberVerificationProvider` | Add a lookup provider with its own credentials |
| Time-zone pickers | Orchard `ITimeZoneSelectListProvider`, refined by **TimeZones** | Inject `ITimeZoneSelectListProvider` | Build `SelectListItem`s from `IClock.GetTimeZones()` |
| Content fields for phone and address | **ContentFields** | `PhoneField`, `AddressField` and their settings in `ContentFields.Abstractions` | Reference the ContentFields module project from a `.Core` library |

### Time

| Concept | Owner (feature) | Reuse it through | Do not |
| --- | --- | --- | --- |
| Events, repeats, free/busy, reminders, feeds | **Calendars** | `AddCalendarType` (owner kind, capacity, `ManagedByCode`), `ICalendarBusyTimeSource`, `ICalendarEventSource`, `ICalendarAvailabilityService`, `ICalendarReminderOffsetProvider`, calendar handlers and observers. See `.agents/skills/crestapps-calendar-types` | Build your own reminder loop, iCal feed or busy-time check |
| Wall-clock time and time zones | **Calendars** `CalendarTime` (NodaTime) in `Calendars.Abstractions`, with `TimeWindow`, `RecurrenceRule` and `CalendarTimeZones` | `CalendarTime` for every local-to-UTC conversion; `ITimeZoneSelectListProvider` (via `GetTimeZoneOptionsAsync`) for pickers | Use `TimeZoneInfo.ConvertTimeToUtc` (it throws in a daylight-saving gap) or write another resolver. Gap: no shared `ITimeZoneResolver` yet; Dispatch, TimeTracker and Contact Center reports keep their own chains |
| Repeating rules | **Calendars** `RecurrenceRule` / `RecurrenceExpander` | The RRULE types | Write another recurrence model |
| Business hours and holidays | **BusinessHours** (`BusinessHours.Abstractions`/`.Core`) | `HoursSchedule`, `IOpenHoursService`, `IHoursScheduleSource`; Contact Center calendars are one source and feed `IBusinessHoursGate` and work-order SLAs | Add another weekly-hours or holiday list. Gap: Scheduling working hours and Bookings opening hours still use their own models |
| Bookable appointments and resources | **Scheduling** | `AppointmentType`, `SchedulingResource`, `IAppointmentSlotFilter`, `IAppointmentObserver` | Build another slot engine |
| Time entries and stages | **TimeTracker** | `ITimeTrackerService`, `IExternalTimeClock`, `ITimeEntryObserver` | Keep your own time ledger |
| Shifts, time off, labor rules, timesheets | **Workforce** | `IShiftScheduleSolver`, `ILaborRule`, `ITimeOffApprovalHandler`, `IPayrollExportProvider`, `IWorkedTimeSource` | Re-model workers or shifts |

### Work and field service

| Concept | Owner (feature) | Reuse it through | Do not |
| --- | --- | --- | --- |
| The universal work item | **Omnichannel** `OmnichannelActivity` | Activities, subjects, dispositions | Create another task or work-item record |
| Jobs and service agreements | **WorkOrders** | `IWorkOrderHandler`, `IWorkOrderObserver`, `IServiceHoursProvider` | Copy job records into another module |
| Visits and routes | **Dispatch** | `IDispatchOptimizer`, `IDispatchCandidateFinder`, `IVisitObserver` | Write another route optimizer; plug one in like OrTools |
| Equipment and fleet | **Assets** | `Asset`, `IAssetOwnerSource` | Keep equipment lists elsewhere |

### Communication

| Concept | Owner (feature) | Reuse it through | Do not |
| --- | --- | --- | --- |
| A messaging channel (SMS, e-mail, chat) | **Omnichannel.Messaging** | `IMessagingChannel`. See `.agents/skills/crestapps-messaging-channel` | Add channel code to the workspace |
| Consent to be contacted (opt-outs, do-not-call, marketing) | **Consent** (`Consent.Abstractions`/`.Core`) | `IConsentService` (per channel and address), `IConsentSource` | Read a do-not-contact flag or DNC list directly |
| Bulk sends (broadcasts, campaigns) | **Outreach** (`Outreach.Abstractions`/`.Core`) | `IOutreachEngine`, audience source, channel sender, template renderer and recipient policy, on the leased work queue | Write another batch-send loop |
| Commerce e-mails | **Commerce.Abstractions/Core** (`CrestApps.OrchardCore.Commerce.Emails`) | `ICommerceEmailSender` with a kind, named models and a Liquid override per kind | Build a `MailMessage` with concatenated HTML |
| A telephony provider | **Telephony** | `ITelephonyProvider` and its capability interfaces. See `.agents/skills/crestapps-phone-provider` | Put orchestration (queues, IVR, routing) in a carrier adapter |
| Provider webhooks (inbound) | **Webhooks** (`Webhooks.Abstractions`/`.Core`, feature `ContactCenter.ProviderInbox`) | `IProviderWebhookInbox` (durable, de-duplicated, on the leased work queue), `IWebhookSignatureValidator` | Process a webhook in a fire-and-forget task or de-duplicate in memory |
| Opt-out and do-not-contact | **Omnichannel** `IContactOptOutResolver`, **DncRegistry** | `IContactOptOutResolver` for every contact decision | Read `DoNotCall`/`DoNotSms` flags directly or add another STOP-keyword list |
| AI-to-agent handoff | **Omnichannel** | `IOmnichannelHandoffService` | Build another handoff path |
| Real-time push to browsers | Orchard SignalR hubs (`SignalRHubRoutes` in `CrestApps.OrchardCore.Core`) | A hub in your module | Poll from the browser when a hub exists; use the deprecated SignalR module |
| Provider media sockets | **WebSockets** | `WebSockets.Abstractions` | Use it for browser push |

### Commerce

| Concept | Owner (feature) | Reuse it through | Do not |
| --- | --- | --- | --- |
| Money, currency precision | `CrestApps.OrchardCore.Abstractions` (`CrestApps.OrchardCore.Currencies`) and **Payments.Abstractions** | `CurrencyScale` (the only precision table; Stripe overrides only real API differences), `Money`, `BalanceLedger` for stored balances | Round to 2 decimals or keep another currency table |
| Products and prices | **Products** | `ProductPart`, `ISellableProduct`, `IPriceResolver`, `IBuyerPriceProvider` | Model a sellable thing outside Products |
| Selling a new kind of product (donation, download, membership, booking) | **Carts**, **Checkout**, **Orders** | `ICartLineHandler`, `ICheckoutHandler`, `ICheckoutTenderProvider`, `IOrderEventHandler`, `OrderGrantHandlerBase` | Create orders, capture payments or send receipts yourself |
| Money owed later (terms, balances, installments) | **Transactions** and **Checkout.Core** | `Transaction`, `PaymentAttempt`, `DeferredBalanceTenderProviderBase` | Keep another ledger or copy a balance tender |
| Store identity | **Commerce.StoreProfile** | `StoreProfileSettings` via `GetStoreProfileAsync()`; receipts, ship-from and tax origin fall back to it | Add another business name or address setting |
| Money failures someone must handle | **Checkout** | `IPaymentIssueRecorder` (Commerce > Payment issues) | Only log a failed refund or cancellation |
| Document numbers and codes | `CrestApps.OrchardCore.Abstractions` (`CrestApps.OrchardCore.Numbering`) and **Transactions** | `INumberSequence` (locked, gap-free series), `ReadableCodes`; `IFinancialDocumentNumberGenerator` for receipts and invoices | Generate numbers from `Random` or max+1 queries. Gap: the financial generator still has its own store |
| Recurring access and purchased grants | **Subscriptions**, **Orders.Core** and **Users** | `ISubscriptionEntitlementApplier`, `OrderLineGrantHandlerBase<TPart>` (reads the purchased version), `IRoleGrantService` | Grant or remove roles directly, or read the latest product version after payment |
| Receipts | **Receipts** | `IReceiptService` | Render receipts yourself |
| Personal data export and erasure | **PersonalData** (`PersonalData.Abstractions`/`.Core`) | `IPersonalDataContributor` per module, run by `IPersonalDataService`; the old `ICommercePersonalDataContributor` is bridged | Store personal data without a contributor |
| Commerce API | **Commerce.Api.Core** | `MapCommerceApi`, `IdempotencyEndpointFilter`, `ApiProblems` | Hand-roll `Idempotency-Key` handling |

### Data, reports and content

| Concept | Owner (feature) | Reuse it through | Do not |
| --- | --- | --- | --- |
| Reports | **Reports** | `IReport` + `AddReport<T>`, `IReportRunner`, `ReportDocument`, `IndexedReportDataSet` (designer data sets), `IReportExportFormat`. See `.agents/skills/crestapps-report-data-source` | Build analytics pages with their own filters, charts and CSV export |
| Bulk import and export of content | **ContentTransfer** | `IContentImportHandler`, `IContentTransferFileFormatProvider`, `IContentImportRowFilter`, `IContentExportBatchHandler` | Write your own CSV import page |
| CSV and Excel files | `CrestApps.OrchardCore.Abstractions` (`CrestApps.OrchardCore.Tabular`), with **Reports** export formats and **ContentTransfer** readers on top | `CsvWriter` (RFC 4180, formula-injection guard), `CsvReader`, `CsvRow` | Write another `EscapeCsv` or split lines on commas |
| External systems (databases, APIs) | **DataConnectors** | `IDataConnector`, `DataConnection`; AI external data sources can reference a connection instead of embedding credentials | Store another set of endpoint credentials. Gap: FTP/SFTP sources and carriers still keep their own credentials |
| Multi-step flows | **Wizard** | `WizardPart`, `IWizardEngine` | Hand-roll a step-by-step flow |
| Structured data (Schema.org) | **Seo** | `IStructuredDataManager`, `IStructuredDataProvider` | Write JSON-LD in a view |
| AI tools, prompts, profiles | **AI** | `AddCoreAITool`, `Templates/Prompts/*.md` with `ITemplateService` | Hard-code prompts in C# |
| Search indexes for AI | CrestApps.Core search packages | Core's Azure AI Search and Elasticsearch services | Fork Core's search services into a module |

### Platform infrastructure

| Concept | Owner | Reuse it through | Do not |
| --- | --- | --- | --- |
| Stored records | CrestApps.Core catalogs (`ICatalog`, catalog managers, `CatalogEntryHandlerBase<T>`) and the `DocumentCatalog` base in `CrestApps.OrchardCore.YesSql.Core` | Catalog managers, so handlers run | Write a custom YesSql store for a CRUD record |
| Created/owner/author stamping | `CrestApps.OrchardCore.Core` | `AuditedCatalogEntryHandlerBase` with `IAuditedModel` | Copy the stamping into each handler or store |
| Catalog admin pages | `CrestApps.OrchardCore.Core` | `CatalogAdminController<T>` (index on `<admin-list>`, create, edit, delete, bulk actions) | Write another 300-line index/create/edit/delete controller |
| Status transitions | `CrestApps.OrchardCore.Abstractions` | `TransitionTable<TEnum>` (`CanMove`, `IsTerminal`) | Add another hand-rolled transition dictionary |
| Recipe and deployment steps for catalog records | `CrestApps.OrchardCore.Core` | `NamedCatalogRecipeStep`, `NamedCatalogDeploymentSource`, `CatalogDeploymentSerializer` | Copy the serializer or hand-write the same step again |
| Secrets in settings | `CrestApps.OrchardCore.Core` | `ProtectedSettingExtensions` | Call `CreateProtector` in each driver |
| Signed links and tokens | `CrestApps.OrchardCore.Core` | `SignedTokenService` | Write another signed-link class |
| Absolute URLs | `CrestApps.OrchardCore.Core` | `SiteUrlGenerator` | Build URLs from the request by hand |
| Safe log values | `CrestApps.OrchardCore.Core` | `SanitizeLogValue()` | Log raw user input |
| Strings, JSON, comparisons | CrestApps.Core.Support and .NET | `Truncate` and `Slug` (CrestApps.Core.Support), `JsonSerializerOptions.Web`, `CryptographicOperations.FixedTimeEquals` | Write a private `Truncate`, `new(JsonSerializerDefaults.Web)` or `FixedTimeEquals` |
| Background pacing and locks | `CrestApps.OrchardCore.Abstractions` | `BackgroundWorkPacer`, `DistributedLockExtensions` including the ordered, all-or-nothing `LockAllAsync` | Write another batch-and-lock loop |
| Durable queues, retries and retention | `CrestApps.OrchardCore.Abstractions` and `YesSql.Core` | `ILeasedWorkQueue<T>` / `ILeasedWorkItem` (the Contact Center outbox and the provider inbox run on it); `IRetentionPolicy` with `DocumentRetentionPolicyBase` and one retention task; .NET resilience pipelines for HTTP retries | Write another lease/fence/backoff loop or an ad hoc pruning task. Gap: `ProviderCommand`, recording ingest and a few purges are not on these yet |
| Tenant file storage (local or Azure Blob) | `FileStorage.Core` and `CrestApps.OrchardCore.Core` | `AddTenantFileStore` (local under `App_Data` or Azure Blob from shell configuration), `EncryptedFileStore` | Add another `.Azure` module with its own blob store |
| Outbound webhooks | **Webhooks** (`Webhooks.Abstractions`/`.Core`, feature `CrestApps.OrchardCore.Webhooks`) | `IWebhookEventProvider`, `IWebhookPublisher`, `IWebhookSigner`, `IWebhookSender`; outbound calls through `EgressNetwork` | Write another HMAC signer, retry table or private-IP check |
| Admin lists and forms | Orchard `AdminList`, `action-bar-bottom` | `.agents/skills/crestapps-admin-index-views`, `.agents/skills/crestapps-admin-edit-views` | Hand-write a list or a button row |
| Editors that others extend | Orchard display management | `IDisplayManager<TModel>` with display drivers, so other modules add fields through their own drivers | Build an editor as a plain controller form with a fixed view model |

## 4. When you add a building block

- Split it into `X.Abstractions`, `X.Core` and the module, as in section 2.
- Give it extension points from day one: driver-based editors, handlers or observers for its life cycle, open string kinds instead of enums that name consumers, and `AddXxx` registration helpers.
- Add a recipe step and a deployment step for every record type, and a `ConfigurationSchema.json` entry for every options class.
- Add it to this catalog and to `src/Targets`, and add a skill under `.agents/skills/` when others will build on it.
- Migrate the existing copies of the concept in the same plan, so the solution never keeps two.
