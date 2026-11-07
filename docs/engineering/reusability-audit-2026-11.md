# Reusability and code-quality audit (November 2026)

Status: findings recorded 2026-11-03 against `main` at `5af617880`. The plan in section 8 was carried out in November 2026; section 0 says what is done and what is left. The binding rules that came out of this audit are in `AGENTS.md` (*Building Blocks and Reuse*), and the catalog of building blocks is [`building-blocks.md`](building-blocks.md).

Paths are relative to `src/`. `M/X` is `Modules/CrestApps.OrchardCore.X`, `C/X` is `Core/CrestApps.OrchardCore.X`, and `A/X` is `Abstractions/CrestApps.OrchardCore.X`.

## Summary

The solution has 119 modules (331 features), 48 `.Core` libraries and 33 `.Abstractions` libraries. The audit read every module, its project references and its extension points, in six slices: people and organizations; time, scheduling and work; commerce; communication and Contact Center; AI, data and platform; and a solution-wide sweep of shared infrastructure and quality metrics.

**The building-block design works where it was applied:**

- The purchase pipeline is shared. One place creates orders, and the verticals plug in through `ICartLineHandler` (5 implementations), `ICheckoutHandler` (21), `ICheckoutTenderProvider` (6), `IOrderEventHandler` (12) and `OrderGrantHandlerBase` (3).
- Calendars is a real building block: Workforce, Dispatch, Scheduling, Assets and Bookings register calendar types and busy-time sources instead of building their own calendars.
- Accounts is reused by the CRM, Accounts.B2B, WorkOrders, Assets and Orders; `Address` by 15 projects; `IDisplayNameProvider` by about 40 files; `IndexedReportDataSet` by 24 modules; Seo structured data by Products, Reviews and NLWeb; ContentTransfer by Accounts, Inventory, the CRM and Products; Skills by Workforce and Dispatch; Geo territories by Workforce, WorkOrders and Dispatch.
- OrTools is the model plug-in: it implements `IShiftScheduleSolver` and `IDispatchOptimizer` without touching the modules it serves.
- Admin index pages are on `<admin-list>`, `IClock` is used almost everywhere, and the commerce slice has 107 `[RequireFeatures]` integrations.

**Where it was not applied, the same concept was built again.** Section 2 lists about 80 duplicated concepts. They fall into recurring themes: organizations, people, phones, named locations, time zones, business hours, holidays, skills, teams, money, document numbers, webhooks, durable work queues, retention, file storage, CSV, secrets, audit stamping and recipe/deployment plumbing. Several of those copies have already drifted into live defects (section 1). Dependencies also run the wrong way in places: `.Core` libraries reference module projects, channel and carrier modules reference Contact Center's implementation, and the platform `Abstractions` and `Recipes` projects know their consumers (section 4). Many editors are plain controller forms, so other modules cannot extend them through drivers, and that is why some modules copied a concept instead of extending it (section 5).

## 0. Implementation status (November 2026)

The plan below was carried out on branch `claude/modular-reusability-audit-ab2aa5`. This section records what is done and what is left. The tables after it keep the original findings so the reasoning stays visible.

### Guardrails (phase 0)

| Guard | Test | State |
| --- | --- | --- |
| `.Core`/`.Abstractions` reference no module; module code uses no type from another module assembly | `LayeringRatchetTests` (compiled assemblies, every module built for the scan) | Allowlists empty |
| No new copies of known duplicates (`EscapeCsv`, `new BlobFileStore(`, serializer copies, `GetTimeZones()` pickers, `DateTime.UtcNow`, `ConvertTimeToUtc`, private `FixedTimeEquals`, blocking `GetResult()`) | `DuplicatePatternRatchetTests` | `DateTime` and serializer lists empty; the rest only shrink |
| Feature ids as constants | `FeatureIdLiteralRatchetTests` (`OrchardCoreFeatureIds` for Orchard ids) | 47 literals left, listed in the test |
| No silent empty `catch` | `EmptyCatchRatchetTests` | None left |
| Time-zone pickers and user lists | `TimeZonePickerArchitectureTests`, `UserListLoadArchitectureTests` | Pass |

### Defects (section 1)

All 23 are fixed, each with regression tests. Partly done items say what is left.

| # | Fix |
| --- | --- |
| 1 | Stable Stripe idempotency key from vendor, order and earnings; payout provider chosen by name |
| 2 | `IRoleGrantService` keeps a role while any source still grants it (Subscriptions, Memberships) |
| 3 | B2B tax profile handler feeds the account's tax id |
| 4 | User search behind `SearchUsers`; one `IUserSearchService` for the pickers |
| 5 | Business Hours building block answers `IServiceHoursProvider`; SLA editor has an hours-calendar picker |
| 6 | Skill readers merge a worker and its user (`ISkillSubjectLinkProvider`); subject kinds are open strings. Left: Contact Center routing still reads its `SkillProficiencies` mirror |
| 7 | Twilio and Event Grid SMS go through the durable provider inbox |
| 8 | STOP from an unknown number is recorded (`ContactAddressOptOut`, read through `IConsentService`) |
| 9 | One keyword policy for both paths |
| 10 | Serializer copies deleted |
| 11 | `ODataFilter` escapes filter values in the three forks |
| 12 | `CsvWriter`/`CsvReader` with a formula guard everywhere |
| 13 | `CurrencyScale` is the only precision table, including Products |
| 14 | Scheduling and Calendars share `CalendarLockKeys` and the calendar conflict check |
| 15 | Records authorized by id |
| 16 | `INumberSequence` for orders, returns, purchase orders, visits, work orders and agreements; `ReadableCodes`. Left: the financial document generator keeps its own store |
| 17 | One outbound Webhooks building block behind `EgressNetwork`, with retention |
| 18 | `CalendarTime` everywhere a local time becomes UTC |
| 19 | `IPersonalDataService` with contributors in every module the audit named. Left: Contact Center's data-governance catalog is not unified |
| 20 | Shift reminders logged before sending, under a lock |
| 21 | Grants read the purchased version (`OrderLineGrantHandlerBase`, `GetPurchasedAsync`) |
| 22 | Payment issues list for money failures; migration skips kept and shown to administrators |
| 23 | Request paths count and page in SQL. Left: a few small settings lists still page in memory |

### Building blocks (section 3)

| Block | State |
| --- | --- |
| P1 tabular files, P6 number sequence, P7 secret fields, P11 transition tables, P12 balance ledger, P14 catalog admin controller, P15 `LockAllAsync` | Done |
| P2 tenant file store | Done; the five `.Azure` modules are thin features over `AddTenantFileStore` with unchanged feature ids and settings |
| P3 outbound webhooks, P4 provider inbox | Done (`Webhooks.Abstractions`/`.Core`); AI and Commerce are event providers |
| P5 leased work queue | Done; the Contact Center outbox and the provider inbox run on it. Left: `ProviderCommand`, recording ingest, Messaging outbound state |
| P8 setup guide | Not started |
| P9 search index schema | Partly: one embedding index-profile base. Left: the search service forks wait for client-factory overloads in CrestApps.Core |
| P10 connections | Partly: AI external data sources can use a `DataConnection`. Left: FTP/SFTP and carriers |
| P13 retention | Done for the platform runner and several purges. Left: a few ad hoc purges |
| D1 parties, D2 contact info, D4 named addresses and store profile, D5 team membership, D6 business hours, D7 time windows, D8 consent, D9 outreach, D10 money, D11 order-line grants, D12 role grants, D13 balance tenders, D14 commerce e-mails, D15 personal data, D16 open skill subjects, D19 ContentFields.Abstractions | Done, with the gaps the catalog notes |
| D20 Commerce.Abstractions | Partly: commerce e-mails, store profile and commerce webhooks moved out. Left: `IStoreSetupStep` and `CustomerAdminPageAttribute` in the platform Abstractions, `CommerceReportDataSets` in Reports.Abstractions |
| D18 Contact Center distribution contracts | Partly: Messaging.Sms and the SMS inbox no longer use `ContactCenter.Core`. Left: Omnichannel.Messaging, Messaging.Core and Telnyx still read stored Contact Center records |
| D3 contacts abstraction, D17 recording ingest and call control | Not started |

### Phases 2 to 5

- **Phase 2 (platform DRY):** done. Catalog recipe steps, deployment sources, audit stamping, store timestamps, small helpers and AI clones are on shared bases. Left: one shared `escapeHtml` script helper.
- **Phase 3 (re-layering):** done; the layering ratchet allowlists are empty. AI tools in Calendars, Scheduling, Workforce and Seo use the CrestApps.Core.AI package; AI recipe schemas live in the AI modules. Left: `Omnichannel.Core` still references `AI.Core`.
- **Phase 4 (building blocks):** see the table above.
- **Phase 5 (larger refactors):** item 7 (driver-based editors) done for workers, work orders, assets, scheduling resources and appointment types, and visits take panels; item 9 (analytics on Reports) done for AI chat, usage, QA and Time Tracker; item 3 (outreach) done. Left: Customer linked to Account and Vendor as an Account (2), Messaging routing on the Contact Center pipeline (4), the Telnyx split (5), Subscriptions through Orders (8), and god-class splits (11). Items 4 and 5 change live call and message routing and need live-call testing.
- **Quality sweep:** `DateTime.UtcNow` gone outside tests, one blocking call left (documented), 22 classes sealed or made static or abstract, every module has a README, missing recipe and deployment steps added for the records in section 6.

## 1. Defects and risks found (fix first)

Each of these was found while reading for duplication. Fix each one test-first: add the failing regression test, then the fix (see *Test-Driven Development* in `AGENTS.md`).

| # | Problem | Where | Effect | Fix | Size |
| --- | --- | --- | --- | --- | --- |
| 1 | Marketplace payouts can pay a vendor twice | `M/Marketplace/Services/MarketplaceService.cs:264-276`, `StripeConnectPayouts.cs:171` | The Stripe idempotency key is a new `VendorPayout.ItemId` on every run, so a transfer that succeeds before the save fails is sent again | Derive the key from vendor + order + earning IDs; choose the payout provider by name, not `FirstOrDefault()` | S |
| 2 | Ending a subscription removes a role that an active membership still grants | `M/Subscriptions/Services/RoleSubscriptionEntitlementApplier.cs` (`RevokeAsync`) vs `M/Memberships/Services/MembershipService.cs:106-185` | Users lose access they paid for | One role-grant service keyed by source (building block D12) | M |
| 3 | A B2B account's tax ID never reaches tax | Only `M/Customers/Handlers/CustomerCheckoutTaxProfileHandler.cs` feeds the tax profile; nothing reads `B2BAccountPart.TaxId` | B2B buyers are taxed as consumers | Add a B2B `ICheckoutTaxProfileHandler` | S |
| 4 | The admin user search is open to every signed-in user | `M/Users/Endpoints/UserSearchEndpoints.cs:21-22` (`RequireAuthorization()` only) | Storefront shoppers can list user IDs and names, filtered by role | Require a permission, as `AccountSearchEndpoints` does | S |
| 5 | Work-order SLA business hours do nothing | `IServiceHoursProvider` (`A/WorkOrders.Abstractions/Services/WorkOrderSeams.cs:8`) has no implementation; `C/WorkOrders.Core/Services/SlaClock.cs:42-67` | An SLA set to a business-hours calendar counts around the clock | Implement it over business hours (D6); a `[RequireFeatures]` adapter over `IBusinessHoursGate` first | S |
| 6 | Contact Center skills are invisible to Workforce and Dispatch | `M/ContactCenter/SharedSkills/AgentSkillsSync.cs:67` writes `SkillSubject.User`; Workforce and Dispatch read `SkillSubject.Worker` | Agents' skills never reach the auto-scheduler, coverage or dispatch | One skill model (section 8, phase 5, item 1) | M |
| 7 | Inbound SMS can be lost or duplicated | `C/Omnichannel.Messaging.Sms.Core/Twilio/TwilioWebhookEndpoint.cs:148`, `AzureEventGridEndpoint.cs:327` (fire-and-forget), de-duplicated by the in-memory `InMemoryAutomatedConversationGate` | Messages lost on restart and processed twice on a multi-node site | Route through the durable provider inbox (P4) | M |
| 8 | An SMS STOP from a number with no linked contact is dropped | `SmsKeywordInboundHandler.cs:157-160` | Compliance risk | Record a per-number opt-out (D8) | S |
| 9 | Two STOP-keyword policies behave differently | `C/Omnichannel.Core/Services/OmnichannelSmsComplianceHelper.cs` (prefix match) vs `SmsKeywordPolicy.cs` (exact match, HELP/START) | "STOP please" opts out on one path only | One keyword policy | S |
| 10 | Copied deployment serializers miss the `Version` exclusion | `M/Omnichannel.Managements/Deployments/OmnichannelDeploymentSerializer.cs`, `ContactCenterDeploymentSerializer.cs` vs `C/Core/Deployments/CatalogDeploymentSerializer.cs` | Deployment plans carry or regress version stamps | Delete the copies; use `CatalogDeploymentSerializer` | S |
| 11 | OData filter values are not escaped | `M/AI.Documents.AzureAI/Services/AzureAISearchVectorSearchService.cs:67`, `M/AI.Memory.AzureAI/Services/AzureAISearchMemoryVectorSearchService.cs:66`, `OrchardCoreAzureAISearchDataSourceContentManager.cs:20,174` | Filter injection; these are forks of CrestApps.Core classes that do escape | Use Core's services (P9) | S–M |
| 12 | CSV exports allow formula injection, and one does not escape at all | Reports CSV export, three `EscapeCsv` copies in `M/AI.Chat/Controllers`, Inventory, Orders; `M/Checkout/Controllers/CouponsAdminController.cs:470` | Spreadsheet formula execution; broken files | One tabular writer with a guard (P1) | S |
| 13 | Currency precision disagrees between tax, payment and display | `C/Taxation.Core/Services/DefaultTaxRoundingStrategy.cs:35` (2 decimals default) vs `A/Payments.Abstractions/CurrencyScale.cs`; `C/Stripe.Core/StripeCurrency.cs` disagrees on ISK, UGX, MGA, IQD, LYD; nine places hard-code 2 decimals; `M/Bookings/Services/BookingDepositService.cs:83` does not round | Yen tax rounds to 2 decimals while payment uses 0 | `CurrencyScale` as the only source (D10) | S |
| 14 | Scheduling and Calendars can double-book a room | `DefaultAppointmentService.cs:461,742` locks `SCHEDULING_RESOURCE_{id}` and skips the calendar conflict check; `DefaultCalendarEventManager.cs:553-588` locks `CALENDAR_{id}` | Two bookings for one resource | One lock and conflict check (section 8, phase 5, item 6) | M |
| 15 | Authorization checks against a capped list | `M/Workforce/Controllers/AttendanceController.cs:152,197`, `MyTeamController.cs:293` (first 1,000 rows) | Legitimate records beyond 1,000 are refused | Query the record by ID | S |
| 16 | Document-number generators can return duplicates | `C/Orders.Core/Services/OrderService.cs:278-302`, `M/Orders/Services/OrderReturnService.cs:335-352`, `M/Inventory/Controllers/PurchaseOrdersAdminController.cs:590-605` (copies of each other; `R-` collides with receipts); `C/Dispatch.Core/Services/DefaultDispatchService.cs:654` and `C/WorkOrders.Core/Services/WorkOrderService.cs:761` (identical "highest + 1" over every index row, with no sequence lock) | Duplicate order, return, PO, visit or work-order numbers; full scans | A locked sequence (P6) | S–M |
| 17 | AI webhooks have no private-network guard, and their deliveries are never purged | `M/AI/WebhooksStartup.cs:41`, `M/AI/Startup.cs:333` (`SendSignedWebhookTask`) vs Commerce's `WebhookNetwork.cs` and 30-day retention | Server-side request forgery to internal addresses; a delivery table that grows forever | One webhook module (P3) | S |
| 18 | Local-to-UTC conversions throw in a daylight-saving gap | `C/WorkOrders.Core/Services/SlaClock.cs:144-145`, `BookingSchedule.cs:74` (`TimeZoneInfo.ConvertTimeToUtc`) | Exceptions twice a year | `CalendarTime` (NodaTime lenient resolver) | S |
| 19 | Personal-data export and erasure are incomplete | Ten commerce verticals (Fundraising, Bookings, Reviews, Marketing, Memberships, Downloads, Classifieds, Marketplace, B2B, Point of Sale), plus the CRM, Workforce and Scheduling, have no personal-data contributor | Privacy requests miss data | Add contributors (D15) | M |
| 20 | Shift reminders de-duplicate with a cache read-then-write | `C/Workforce.Core/Services/ShiftReminderService.cs:100-107` | Duplicate reminders across nodes, lost state on restart | Calendar reminders (`ICalendarReminderOffsetProvider`) | M |
| 21 | Verticals read the current or draft product after payment | `DonationService.cs:116`, `MarketplaceService.cs:84`, `ClassifiedHandlers.cs:102` (`VersionOptions.Latest`); Memberships and Bookings use `Published` | Grants use what the product is today, not what was bought | Use `OrderLine.ContentItemVersionId` (D11) | S |
| 22 | Money failures are only written to the log | `C/Checkout.Core/Services/DefaultCheckoutEngine.cs:1182` (failed refund "must be refunded manually"), `C/Subscriptions.Core/Handlers/GatewayBillingSubscriptionLifecycleHandler.cs:127` ("customer may still be billed"), `TransactionSettlementCheckoutHandler.cs:502`; 18 migrations log and skip failed records (8 in AI, for example `AIProfileDocumentMigrations.cs:189`) | Nobody sees a refund or cancellation that must be done by hand | Persist an operator-visible "needs attention" record; report migration skips | M |
| 23 | Request paths load whole tables | `OmnichannelActivityDisplayDriver.cs:146` and `TelephonyExtensionDisplayDriver.cs:107` load every enabled user into a dropdown; `M/Assets/Controllers/AssetTypesController.cs:63` and `M/WorkOrders/Controllers/WorkOrderSettingsController.cs:646` load every index row to count; 12 controllers page in memory after `GetAllAsync` | Slow pages and time-outs on large tenants | Use the user picker; count and page in SQL | S–M |

## 2. Duplicated concepts

Each row is one concept built more than once. "Canonical" is where the concept should live; section 3 describes the building blocks that do not exist yet.

### People, organizations and places

| Concept | Copies today | Canonical | Action |
| --- | --- | --- | --- |
| Organization | Account + `B2BAccountPart`; `Customer` with `Kind=Business`, `CompanyName`, `TaxId` (`C/Customers.Core/Models/Customer.cs:18-72`); Marketplace `Vendor` (`M/Marketplace/Models/MarketplaceModels.cs:10-41`); free-text `LeadPart.Company` | Accounts | Link Customer to Account and drop its business fields; Vendor becomes an Account with a `VendorPart` |
| "Customer" | `Order.CustomerId` is a Customer record ID; `WorkOrder.CustomerId` and `AssetOwnerKind.Customer` are a **user ID**; `CustomerOwner` is a user or guest ID | Distinct names + `PartyRef` (D1) | Rename the user-ID uses; document the ID kind on every `*CustomerId` |
| Copied person names | `AgentProfile.UserName/DisplayName`, `AccountMember.UserName/Email`, `Vendor.UserNames`, `Membership.UserName`, `CalendarAttendee`, `UserLink.ParentUserName` | `UserId` + `IDisplayNameProvider` | Stop copying names; they go stale on rename |
| Contact snapshot | About 12 records re-declare name, e-mail and phone (`Order`, `OrderReturn`, `Transaction`, `Subscription`, `InstallmentPlan`, `Appointment`, `Booking`, `Donation`, `WorkOrderContact`, `CheckoutContactInfo`, `ReceiptDocument`…) | `ContactInfo` value type (D2) | Snapshots are correct; the type should be shared |
| Named location | `AccountLocation`, `CustomerAddress`, Shipping `PickupLocation` (flattened address lines, `ShippingSettings.cs:57-63`), Inventory `StockLocation`/`InventoryLocation` (string address), Workforce `WorkforceLocation` (string + coordinates), `ReceiptSettings.BusinessAddress` (string), POS `Register.Address` | `NamedAddress` + store locations (D4) | One named-address type with geocoding |
| Phone number | `PhoneNumber` (E.164) in Omnichannel, Contact Center, DNC, Telephony; `PhoneField` (three strings); `ContactPhoneNumber`; about 15 raw `string Phone` (Customer, WorkerProfile, Order, Appointment, Checkout…) that are only trimmed | `PhoneNumber` + `IPhoneNumberService` | Commerce and HR phones cannot match CRM, DNC or caller-ID numbers today |
| Time-zone picker | Nine or more modules build lists from `IClock.GetTimeZones()` (`TeamDisplayDriver.cs:55`, `WorkersController.cs:499`, `CalendarSelectLists.cs:22`, `TerritoriesController.cs:257`, …) | `ITimeZoneSelectListProvider` (TimeZones) | The TimeZones module's friendly names work only in the CRM today |
| Groups of people | Teams has no members; membership on `WorkerProfile.TeamMemberships`; Calendars "Team Calendars" use a role as the team (`M/Calendars/TeamsStartup.cs:26`); `Team.LinkedRoleName`; `AccountMembersPart`; `Vendor.UserIds` | Teams + membership (D5) | Contact Center queues are a different concept (routing) and stay |
| Polymorphic party reference | `CustomerOwner`, `AssetOwnerKind` + `IAssetOwnerSource`, `TerritoryMember`, Inventory `OwnerKind/OwnerId`, `CalendarOwnerKind`, `AccessGrantPrincipalType` | `PartyRef` + `IPartySource` (D1) | Lift `IAssetOwnerSource` out of Assets |
| Tenant provisioning | `C/Subscriptions.Core/Services/TenantProvisioningService.cs` vs `ITenantHierarchyBroker` | TenantHierarchy | Provision through the broker |
| User lookup | `M/Users/Endpoints/UserSearchEndpoints.cs`, `M/Calendars/Services/CalendarUserLookup.cs`, `CustomerAssetOwnerSource`, the report designer, 12 files in Omnichannel.Managements | Users search service | One permission-checked user search |
| Maps credentials | `GooglePlacesSettings.ApiKey` (Addresses) and `GoogleMapsSettings.Key` (Geo.GoogleMaps); two provider stacks | Geo | One Google credential |
| Saved addresses at checkout | B2B implements `IShippingAddressSource`; Shipping calls `GetService<ICustomerService>()` (`ShippingCheckoutStepService.cs:292`) | `IShippingAddressSource` | Customers implements the source |

### Time, scheduling and work

| Concept | Copies today | Canonical | Action |
| --- | --- | --- | --- |
| Weekly business hours | `SchedulingWorkingHours`, `BookingOpeningHours` (`M/Bookings/Models/BookingPart.cs:145`), Contact Center `BusinessHoursDay`, WorkOrders `WeeklyHours`; four time-zone engines | Business Hours (D6) | Contact Center's model is the most complete start. Workforce `AvailabilityWindow` is a preference and stays |
| Holidays and closures | `BusinessHoursCalendar.Holidays`, Workforce `TimeClockPolicy.Holidays`, Scheduling `DateOverrides`, Calendars "Holiday" and "Closed" event types | One holiday list, as calendar events (D6) | Nobody reads anyone else's list today |
| Skills | `ContactCenterSkill` catalog, `AgentProfile.Skills`, `AgentProfile.SkillProficiencies`, a 768-line mirror (`M/ContactCenter/SharedSkills/*`) into Skills | Skills | Contact Center reads `ISkillAssignmentService`; one subject per person |
| Time-zone resolution | About 10: `CalendarTime`, `Teams.Core/TeamTimeZones.cs` (copy of `NormalizeZoneId`), `TeamTimeZoneResolver` (no parent teams), Dispatch `WorkerAvailability` (with parents), `DispatchTimeZone`, `TimeTrackerTimeZone`, `DefaultBusinessHoursService`, `SlaClock`, `BookingCapacityService`, Contact Center `ReportTimeZone`; plus `NormalizeTimeZoneId` copied in four Omnichannel.Managements files | `ITimeZoneResolver` (D7) | The same worker resolves to different zones in Workforce and Dispatch today |
| Interval maths | `CalendarTimeWindow`, Dispatch `TimeSpanUtc`, Workforce `CoverageInterval`, tuples in `SlaClock` | `TimeWindow` in Calendars.Abstractions (D7) | |
| Bookable resources | Calendars Room/Equipment calendars, `SchedulingResource`, `Asset.Fleet`, `BookingPart.BlockingCalendarId` | Calendars resource calendars | Scheduling creates its own calendar unless one is linked by hand (defect 14) |
| Time slots | Bookings `BookingSchedule.GetTimes` + `IBookingSlotFilter` vs Scheduling `AppointmentType.SlotIntervalMinutes` + `IAppointmentSlotFilter` | Scheduling | |
| Reminders | `ShiftReminderService` vs Calendar reminders | Calendars | Defect 20 |
| Notifications and feeds | Workforce inbox, web-push and e-mail channels (`IWorkforceNotificationChannel`) beside Orchard Notifications; a per-worker iCal feed beside Calendars Feeds | Orchard Notifications, Calendars Feeds | |
| Absence | Workforce `TimeOffRequest` vs Calendars "OutOfOffice" events; Workforce and Dispatch ignore personal busy time | Calendars busy time | |
| Overtime thresholds | `WorkerProfile.Max*Hours`, labor rules, `TimeClockPolicy` overtime hours | Labor rules | Inside Workforce |
| Setup checklists | Commerce `IStoreSetupStep` (in the platform `A/Abstractions`), `WorkforceSetupController`, `FieldServiceSetupController` (borrows a Workforce view model) | `ISetupGuideStep` (P8) | Wizard is a different concept and stays |

### Communication and Contact Center

| Concept | Copies today | Canonical | Action |
| --- | --- | --- | --- |
| Inbound provider webhook intake | Twilio, Azure Event Grid and Telnyx SMS endpoints: three durability models; `IsFresh` twice, `FixedTimeEquals` four times | Provider webhook inbox in a neutral library (P4) | Endpoints only authenticate, parse and accept |
| Opt-out evaluation | `ContactOptOutResolver` (follows shared numbers) vs direct flag reads in `DefaultDialerEligibilityService.cs:136` and `SmsMessagingChannel.cs:158`; the resolver registered three times with `TryAdd`; the national-registry loop copied between the dialer and the manual screener | `IContactOptOutResolver`, then consent (D8) | |
| Consent storage | Contact `DoNot*` flags, DNC local lists and `NotInServiceNumber`, `Customer.AcceptsMarketing` + Marketing's unsubscribe token | Consent per channel and address (D8) | Recording consent is a different concept |
| Bulk sending | `MessagingBroadcast` (recipient lists inside the document) and Marketing `Campaign` | Outreach engine (D9) | `OmnichannelCampaign` groups work items and stays, minus its dead AI fields |
| Routing, presence and capacity | Seven `IActivityRoutingStrategy` implementations vs `IMessagingRoutingStrategy` + `DistributedCacheMessagingPresenceTracker` | Contact Center pipeline with channel-aware capacity | |
| Reservation state | `ActivityReservation`, `ContactCenterWorkState`, and a read model on `OmnichannelActivity` that `ActivitiesController.cs:2031-2056` writes directly | `ContactCenterWorkState` | |
| Call control | `ITelephony*Provider` (`TelephonyResult`), `IContactCenterVoice*Provider` (`ContactCenterVoiceProviderResult`), `IVoiceAgentMediaProvider` (`Task<bool>`) | One call-control result family | Soft phone vs orchestration split is fine |
| Recording ingest | `TelnyxRecordingIngestJob` vs `AsteriskRecordingIngestJob` and their stores (about 1,390 lines) | `IRecordingIngestService` in Telephony.Core | |
| Leased durable work items | `ContactCenterOutboxMessage`, `ProviderWebhookInboxMessage`, `ProviderCommand`, both ingest jobs, Messaging outbound state: each re-implements leasing | Leased work queue (P5) | |
| Carrier credentials | `TelnyxSettings` and `TelnyxSmsSettings` (one account, two keys, two read paths); Twilio Lookup vs Orchard `TwilioSettings` | One credential set per carrier, later connections (P10) | |
| Outbound SMS | Automation calls `ISmsProviderRouter` directly; the workspace uses `ISmsDispatcher` (outbox, delivery IDs, MMS) | `ISmsDispatcher` | |
| AI conversation wrap-up | Conclusion and summary prompts in `SmsOmnichannelEventHandler.cs:754,1118` and in both voice engines | Shared conversation services | |
| Encrypted files and signed links | `LocalEncryptedMessagingAttachmentStore`, `LocalEncryptedRecordingMediaStore`; `CallRecordingLinks`, `MessagingAttachmentLinks`, `SignedTokenService` | `EncryptedFileStore` (P2) + `SignedTokenService` | |

### Commerce

| Concept | Copies today | Canonical | Action |
| --- | --- | --- | --- |
| Currency precision and formatting | Four precision tables (`CurrencyScale`, `StripeCurrency`, Taxation options, `ProductPriceFormatter`); 2 decimals hard-coded in nine places; five formatters | `CurrencyScale`, `Money` + a formatter (D10) | Defect 13 |
| Document numbers | Five generators (orders, returns, purchase orders, donation receipts, gap-free financial documents) plus max+1 scans in Dispatch and WorkOrders | `INumberSequence` (P6) | Defect 16 |
| After-payment grants | The same loop seven times: `DownloadService.cs:74`, `MembershipService.cs:58`, `DonationService.cs:114`, `MarketplaceService.cs:82`, `BookingService.cs:82`, `ClassifiedHandlers.cs:99`, `GiftCardOrderEventHandler.cs:40` | `OrderLineGrantHandlerBase<TPart>` (D11) | Defect 21 |
| Pro-rata refunds | `DonationService.cs:176`, `MarketplaceLedger.GetRefundShare`, `LoyaltyOrderEventHandler.cs:106` (because `OrderRefund` has no line references) | Line-level refunds + `IRefundAllocator` (D11) | |
| Balance tenders | `BookingBalanceTenderProvider.cs:105-200`, `AccountTermsTenderProvider.cs:100-190` (about 70% identical), `PayLaterTransactionCheckoutHandler.cs:138-186` | `DeferredBalanceTenderProviderBase` (D13) | |
| Role grants | Memberships (tracks the roles it added) vs Subscriptions (removes unconditionally) | `IRoleGrantService` (D12) | Defect 2 |
| Purchase pipelines | Subscriptions check out as `ReferenceType="Subscription"` and never create an order | Orders | Reports, loyalty, Marketing, webhooks and Marketplace miss subscription revenue |
| E-mails | 13 builders concatenate HTML; Liquid templates exist for orders only; `ICommerceEmailSender` lives in `Orders.Core` | Commerce e-mail templates (D14) | |
| Store identity and locations | `ReceiptSettings.BusinessAddress`, `ShippingSettings.ShipFrom`, `TaxOriginSettings.Origin`, stock and inventory locations, `PickupLocation`, `Register.Address` | Store profile + store locations (D4) | |
| "Is this address in this area?" | `Market.Covers`, `DefaultShippingRateService.Covers`, `CatalogTaxJurisdictionResolver.Matches`, each with its own normalization | One address-area matcher in Addresses | The area concepts are legitimately different |
| API idempotency | `IdempotencyEndpointFilter` vs hand-rolled handling in `M/Dispatch/Api/FieldServiceApiEndpoints.cs:175` and the Workforce API | The filter, moved out of `Commerce.Api.Core` | |
| Event fan-out | Webhook + workflow handler pairs in five modules | Webhooks module event providers (P3) | |

### AI, data and platform

| Concept | Copies today | Canonical | Action |
| --- | --- | --- | --- |
| Search services | Orchard forks of CrestApps.Core's Azure AI Search and Elasticsearch services in AI.DataSources, AI.Documents and AI.Memory; they have drifted (escaping, source handlers 485 of 520 lines changed) | CrestApps.Core services with client-factory overloads (P9) | Defect 11 |
| Index profile handlers | `AIDocumentIndexProfileHandlerBase`, `AIMemoryIndexProfileHandlerBase`, `DataSourceIndexProfileHandlerBase` (only the index type differs), `ProductSearchIndexProfileHandlerBase`; Azure index-profile handlers about 95% identical; identical document-index handlers per provider; `AIConstants.ColumnNames` copies Core's | Index schema + provider appliers (P9) | 3 consumers × 2 providers = 6 modules today |
| FTP/SFTP connections | AI.FileSources.Ftp/Sftp and AI.Mcp.Resources.Ftp/Sftp each have the metadata class (an MCP copy of Core's), constants, view models, views and drivers, with different validation | One shared connection editor | |
| Tenant blob storage | Five identical `.Azure` modules (ContentTransfer, DncRegistry, AI.Documents, Telephony, Omnichannel.Messaging); five local `FileSystemStore` setups; six per-purpose `IFileStore` wrappers | `AddTenantFileStore` (P2) | |
| CSV | About 12 writers and 4 parsers (Reports, three `EscapeCsv` in AI.Chat, `StockCsv`, `OrderCsv`, `ProductFeedWriter`, payroll, coupons; ContentTransfer, `ProductCsv.CsvTable`, `StockCsv.Read`, DNC `line.Split(',')`) | Tabular reader/writer (P1) | Defect 12 |
| Outbound signed webhooks | `AIWebhookPublisher.cs:186`, `SendSignedWebhookTask.cs:229`, Commerce `WebhookDispatcher.cs:90` (same `v1=` HMAC); two subscription/delivery record sets; three private-IP classifiers | Webhooks module (P3) | Defect 17 |
| Secret settings | About 25 projects call `CreateProtector` and hand-write "keep the stored value when blank, blank it on export"; `ProtectedSettingExtensions` is used by 7 | Secret-field helpers (P7) | |
| Recipe and deployment steps | `NamedCatalogRecipeStep`/`NamedCatalogDeploymentSource` used by Shipping and Taxation only; AI hand-writes 7 deployment sources and 8 recipe steps (about 1,000 lines), as do AI.Mcp, AI.DataSources and DataConnectors; step schemas both centralized in Recipes.Core and module-owned | Core catalog steps; schemas owned by each module | |
| External connections | Elasticsearch connected three ways (Orchard module, per-source credentials in AI.DataSources, DataConnectors REST); Azure Search and PostgreSQL AI sources embed credentials | `DataConnection` (P10) | |
| Analytics pages | AI.Chat analytics, QA dashboard, usage analytics, conversion goals, extracted data, TimeTracker report: own filters, charts and CSV, outside Reports | Reports (`IReport`, `ReportDocument`, `IReportExportFormat`) | |
| Small helpers | `HasPart` (`AccountRecordKinds.cs:23`, `OmnichannelRecordKinds.cs:65`, 16 inline), `NormalizeCountryCode`/`NormalizeCountryPrefix` (three verification providers), `ParseRecipients` (two Messaging controllers), deployment serializer copies | Shared extension methods in `A/Abstractions` or `C/Core` | |

### Shared infrastructure (solution-wide sweep)

| Concept | Copies today | Canonical | Action |
| --- | --- | --- | --- |
| Audit stamping on catalog records | `C/Core/Handlers/AuditedCatalogEntryHandlerBase.cs` is used by 6 handlers (Shipping, Taxation); 17 other `CatalogEntryHandlerBase` subclasses copy its created/owner/author stamping, 10 byte-identical (AI.Core 3, AI.Mcp 3, AI.A2A 1, Omnichannel.Managements 7, Assets, Products, TimeZones) | `AuditedCatalogEntryHandlerBase` | Adopt it |
| Created/updated timestamps in stores | The same `SavingAsync` hook in 11 stores across 5 projects (`CouponStore.cs:225`, `GiftCardStore.cs:124`, `SubscriptionStore.cs:167`, `TransactionStore.cs:174`, …) | A hook in the `DocumentCatalog` base | |
| Store base class | 122 stores derive from the local `C/YesSql.Core/Services/DocumentCatalog.cs`, which shares its name with `DocumentCatalog` in CrestApps.Core.Data.YesSql; about 40 more stores implement their interface directly on YesSql | One base, reconciled with CrestApps.Core | |
| Catalog admin controllers | `M/ContactCenter/Controllers/ContactCenterCatalogController.cs` (510 lines) lets 7 controllers stay about 155 lines; the equivalent Omnichannel.Managements controllers (lead statuses, opportunity stages, dispositions, campaigns, campaign groups, cadences) are 258–320 lines each | `CatalogAdminController<T>` in Core (P14) | |
| Recipe steps and deployment sources | 32 recipe steps (about 3,300 lines: Contact Center 7, Omnichannel.Managements 8, AI 7, AI.Mcp 3; `LeadStatusStep` and `OpportunityStageStep` differ only in names) and about 14 deployment sources clone `NamedCatalogRecipeStep`/`NamedCatalogDeploymentSource`, which only Taxation and Shipping use | The Core catalog steps | About 4,000 lines go, mechanically |
| Status transition tables | Nine hand-rolled `Dictionary<TEnum, TEnum[]>` + `CanTransition` tables (five Contact Center lifecycles, `ProviderCommandStateService`, `VisitStatusRules`, `WorkOrderStatusRules`); `IsTerminal(VoiceCallState)` copied four times from `Telephony.Abstractions` | `TransitionTable<TEnum>` (P11) | |
| Balance ledgers | `GiftCardActivity` and `StoreCreditActivity` have identical fields, and `GiftCard.ChangeBalance`/`StoreCreditAccount.ChangeBalance` identical bodies; `LoyaltyActivity` is the same with points | `BalanceLedger<TEntry>` (P12) | |
| Record event logs and notes | `{ CreatedUtc, Type, Message, Actor }` re-declared by subscription, installment-plan, transaction, visit-status, time-entry and messaging events; `OrderNote` and `WorkOrderNote` differ in shape; only 4 modules write to Orchard's Audit Trail | A shared event-entry type and Audit Trail events | |
| Multi-key locks | An ordered multi-key lock with identical bodies in `DefaultDispatchService.cs:680`, `DefaultAppointmentService.cs:757`, `WorkerLocks.cs:25`, `TimeOffLocks.cs:52` | `LockAllAsync` in `DistributedLockExtensions` (P15) | |
| Due-item polling | 9 `ListDueAsync` store implementations; 96 background tasks (45 on a one-minute schedule); two tasks in Dispatch declare no `LockTimeout` | The leased work queue (P5) | |
| Retry and backoff | About 12 hand-rolled backoff calculators (`DataConnectorHttpClient.cs:300`, `TelnyxApiRetryPolicy.cs`, `ImportFilesBackgroundTask.RetryBackoff`, two webhook tables…); 11 files use Polly or .NET resilience | .NET resilience pipelines | |
| Retention | A retention framework with 18 policies is inside Contact Center (`ContactCenterRetentionPolicyBase`); elsewhere pruning is ad hoc (`FieldStores.cs:51`, `CalendarReminderService.cs:140`, `ExpiredCartCleaner.cs:34`, `DelegatedAccessCodeStore.cs:69`, `WebhookStores.cs:110`) | A platform retention building block (P13) | |
| Readable codes | The alphabet `ABCDEFGHJKLMNPQRSTUVWXYZ23456789` and generator in coupons, gift cards, referrals, returns and purchase orders | `IReadableCodeGenerator` beside the number sequence (P6) | |
| E-mail composition | 13 call sites build `MailMessage` by hand; `ICommerceEmailSender` with Liquid templates serves commerce only; Workforce has its own notifier, channels and inbox beside Orchard Notifications | Templates (D14) and Orchard Notifications | |
| Index column truncation | About 20 private `Truncate` copies in index providers | `Truncate` in CrestApps.Core.Support | |
| Constant-time comparison | `FixedTimeEquals` in `A2AApiKeyAuthenticationHandler.cs:98`, `McpApiKeyAuthenticationHandler.cs:100`, `AzureEventGridEndpoint.cs:435` | `CryptographicOperations.FixedTimeEquals` | |
| JSON options | 65 static `JsonSerializerOptions`; 20 are `new(JsonSerializerDefaults.Web)` | `JsonSerializerOptions.Web` | |
| Address comparison | `IsSameAddress` in `DefaultCustomerMergeService.cs:150`, `CustomerContactCheckoutHandler.cs:178`, `ShippingCheckoutHandler.cs:114` | Addresses.Abstractions | |
| Whole-file clones | `A2AConnectionDisplayDriver` vs `SseMcpConnectionDisplayDriver` (87%), the A2A and MCP API-key authentication handlers (90%), `CheckoutFlow` vs `WizardFlow` (88%), `ExtractTitleFromContent` and `GetSourceType` five times each in AI.DataSources.* although `AIDataSourceDriverHelper.GetSourceType` exists | One shared base per pair | |
| Browser helpers | `escapeHtml` defined 11 times across module scripts and views; Contact Center calls `window.telephonyClient.escapeHtml` from another module; the `@crestapps/ai-chat-ui` version repeated 10 times | A shared helper in Resources; one version constant | |

## 3. Building blocks to add or complete

Platform building blocks (in `CrestApps.OrchardCore.Core`/`Abstractions`, or a small dedicated module):

- **P1. Tabular files.** `ITabularWriter.WriteAsync(stream, headers, IAsyncEnumerable<string[]> rows, format)` and `ITabularReader`: RFC 4180 CSV with a formula-injection guard, and XLSX. Reports export formats and ContentTransfer file formats become thin layers over it. Consumers: Reports, ContentTransfer, AI.Chat, Inventory, Orders, Products, Workforce payroll, DncRegistry, Checkout.
- **P2. Tenant file store.** `services.AddTenantFileStore<TStore>(purpose, configSection, factory, removeOnTenantRemoval)`, choosing the local store under `App_Data` or Azure Blob from shell configuration, with shared container tenant events, and `EncryptedFileStore(IFileStore, purpose)`. Collapses the five `.Azure` modules and the two encrypted stores.
- **P3. Webhooks (outbound).** Rename and generalize Commerce.Webhooks: `IWebhookEventProvider` (modules declare events), `IWebhookPublisher.PublishAsync(eventName, payload)`, one `IWebhookSigner`, the delivery outbox, and one egress classifier in Core. AI and Commerce become event providers; the AI workflow task calls the publisher.
- **P4. Provider webhook inbox (inbound).** Move `IProviderWebhookInbox`, its store, handler and ingress limiter from `ContactCenter.Abstractions` to a neutral `Webhooks.Abstractions`/`Core`, with `IWebhookSignatureValidator`. Consumers: Twilio, Telnyx SMS and voice, Event Grid, Stripe.
- **P5. Leased work queue.** `ILeasedWorkItem` (status, owner token, fence token, attempt count, next attempt, last error) and `ILeasedWorkQueue<T>` (`EnqueueAsync`, `ClaimDueAsync`, `CompleteAsync`, `RetryAsync`). Consumers: Contact Center outbox, inbox and provider commands, recording ingest, Messaging outbound, broadcasts, Marketing campaigns.
- **P6. Number sequence.** `INumberSequence.NextAsync(series, format)`, generalized from `SequentialFinancialDocumentNumberGenerator` (locked, gap-free). Consumers: orders (optional), returns, purchase orders, donation receipts, Dispatch, WorkOrders.
- **P7. Secret fields.** `ApplySecret(existing, posted)` and `HasSecret` on top of `ProtectedSettingExtensions`, and a `[Secret]` marker that the catalog deployment serializer blanks automatically.
- **P8. Setup guide.** `ISetupGuideStep { Guide; … }` rendered by one controller and shape; Commerce, Workforce and Dispatch become providers.
- **P9. Search index schema.** In CrestApps.Core: registration overloads that take a client factory, so Orchard modules supply Orchard's client and stop forking services. Here: `IManagedIndexSchema { IndexType; Fields }` and one `IIndexSchemaApplier` per provider, replacing three handler bases, six mapping handlers and six document-index handlers; one embedding-dimension resolver that does not call the model on every save.
- **P10. Connections.** `DataConnection` (DataConnectors) as the one record for external endpoints and credentials; AI external sources, FTP/SFTP editors and carriers store a connection ID.
- **P11. Transition tables.** `TransitionTable<TEnum>` (allowed moves, `CanMove`, `IsTerminal`) in `CrestApps.OrchardCore.Abstractions`, for the nine hand-rolled status tables.
- **P12. Balance ledger.** `BalanceLedger<TEntry>` (balance, activity entries, `ChangeBalance`) in Payments.Abstractions, for gift cards, store credit and loyalty points.
- **P13. Retention.** Lift `ContactCenterRetentionPolicyBase` and its runner into the platform (`IRetentionPolicy`, one pruning background task), so every module that keeps logs, deliveries, codes or carts declares a policy instead of pruning ad hoc.
- **P14. Catalog admin controller.** Lift `ContactCenterCatalogController` into Core as `CatalogAdminController<T>` (index on `AdminListContext`, create, edit, delete, bulk actions), then move the Omnichannel.Managements catalog controllers onto it.
- **P15. Lock helpers.** `LockAllAsync(keys)` (ordered, all-or-nothing) in `DistributedLockExtensions`, replacing four copies.

Domain building blocks:

- **D1. Parties.** `PartyRef { Kind, Id }` (well-known kinds User, Customer, Account, Contact, Guest; open for more) and `IPartySource { Kind; SearchAsync; GetNamesAsync; GetContactInfoAsync }`, lifted from `IAssetOwnerSource`. Consumers: Assets, WorkOrders, Orders, Transactions, Subscriptions, Geo territories, Inventory, Calendars, Scheduling.
- **D2. ContactInfo.** `{ Name, Email, PhoneNumber Phone, Address }` for the snapshot records and `CheckoutContactInfo`.
- **D3. Contacts abstraction.** Move `IOmnichannelContactTypeProvider`, `OmnichannelContactPart` and a contact-methods reader out of `Omnichannel.Core`, so WorkOrders, Assets, PhoneNumbers.Verifications and Contact Center stop referencing the CRM implementation.
- **D4. Named addresses and store locations.** `NamedAddress { Id, Name, Address }`, a generic `AddressGeocodeTarget`, an editor shape, and a store profile (`StoreProfileSettings`: name, logo, tax ID, address) with `IStoreLocationService` for shipping origin, tax origin, pickup, stock locations, registers and receipts.
- **D5. Team membership.** `TeamMember { UserId, Role, EffectiveFrom, EffectiveTo }` and `ITeamMembershipService` (members of a subtree, teams of a person). Workforce keeps only its HR extras; Calendars team calendars use it instead of roles.
- **D6. Business hours.** `HoursSchedule { TimeZoneId; Windows; DateExceptions }` and `IOpenHoursService` (`IsOpenAsync`, `GetOpenWindowsAsync`, `ListSchedulesAsync`), implementing `IBusinessHoursGate` and `IServiceHoursProvider`; holidays as all-day events on a linked calendar so one list feeds everyone. Consumers: Contact Center, SMS, the CRM, Messaging quiet hours, WorkOrders SLA, Scheduling, Bookings, Workforce holiday pay. Omnichannel.Sms then stops depending on a Contact Center feature.
- **D7. Time zones and intervals.** `CalendarTime`, `RecurrenceRule`/`RecurrenceExpander` and a `TimeWindow` type move to `Calendars.Abstractions`; `ITimeZoneResolver` with an ordered chain of `ITimeZoneSource` (record → team ancestors → module settings → site).
- **D8. Consent.** `IConsentService.GetAsync(channel, address, contactId?)` and `RecordAsync(channel, address, change)` stored per channel and address. Consumers: dialer, manual screener, `IMessagingChannel`, SMS automation, keyword handlers, Marketing, broadcasts, DNC local lists.
- **D9. Outreach engine.** An audience source, a channel sender and a template processed with cursor paging on the leased work queue; replaces `MessagingBroadcast` and Marketing campaign sending.
- **D10. Money service.** `ICurrencyPrecision.DecimalsFor(currency)` and `IMoneyFormatter.Format(amount, currency, style)` in Payments.Abstractions, with provider quirks (Stripe) as overrides.
- **D11. Order-line grants and line refunds.** `OrderLineGrantHandlerBase<TPart>` (resolves `ContentItemVersionId`, idempotent per order and line, `GrantLineAsync`/`RevokeLineAsync`), `OrderRefund.Lines` and `IRefundAllocator`.
- **D12. Role grants.** `IRoleGrantService.GrantAsync(userId, role, source, sourceId)` / `RevokeAsync(...)`, keeping a role while any source still grants it. Consumers: Memberships, Subscriptions, Accounts.B2B.
- **D13. Deferred balance tenders.** `DeferredBalanceTenderProviderBase` (or `ITransactionManager.OweRemainderAsync`) for Bookings, B2B terms, PayLater and installments.
- **D14. Commerce e-mail templates.** Kind + model + Liquid override, out of `Orders.Core`, used by the 13 e-mail builders.
- **D15. Personal data.** `PersonalData.Abstractions` (subject by user ID, e-mail or phone; export and anonymize contributors) unifying `ICommercePersonalDataContributor` and Contact Center data governance, with contributors in every module that stores personal data.
- **D16. Person directory and open skill subjects.** `IPersonDirectory` mapping User, WorkerProfile, AgentProfile and Scheduling staff; `SkillSubjectKind` becomes an open string kind.
- **D17. Recording ingest and call control.** `IRecordingIngestService` with a per-carrier fetcher, and one call-control result family.
- **D18. Contact Center distribution contracts.** `IAgentProfileReader`, `IQueueReader`, `IEntryPointReader` and `IAgentLoadProvider(channel)` in `ContactCenter.Abstractions`, so Messaging, Telnyx and future channels never reference `ContactCenter.Core`.
- **D19. ContentFields.Abstractions.** `PhoneField` (wrapping `PhoneNumber`) and `AddressField`, so no `.Core` library references the ContentFields module.
- **D20. Commerce.Abstractions.** `CommerceConstants`, store setup, commerce webhooks and `CustomerAdminPageAttribute` (now in the platform `A/Abstractions`) and `CommerceReportDataSets` (now in `A/Reports.Abstractions`).

## 4. Layering and coupling

| Problem | Evidence | Fix |
| --- | --- | --- |
| `.Core` libraries reference module projects | `C/Omnichannel.Core` → ContentFields module (only for `PhoneField`); this pulls ContentFields into every Contact Center library | D19 |
| Module projects reference other domains' module projects | 75 module-to-module references in 50 modules. A reference that only carries a feature dependency is fine; module code that uses another module's types is not, including sub-modules and providers that use their parent's view models (`AI.Documents.AzureAI` → `AI.Documents`, `OpenAI` → `AI`, `*.Azure` → parent) and views that use `Resources` tag helpers. The worst are the cross-domain ones: Dispatch → Workforce (for `Workforce.AI`, `Workforce.Portal`, view models; Dispatch's AI tools compile only because AI.Core comes through Workforce); Workforce.ContactCenter → Workforce; Point of Sale → Orders (concrete `OrderOperationsService`); NLWeb → Seo; ContactCenter → Telephony (to push through `TelephonyHub`); ContactCenter and Omnichannel.Voice → Omnichannel.Managements (no code uses it); Omnichannel.Managements → DncRegistry; AI.Documents → AI.Chat and AI.Chat.Interactions; AI.FileSources → AI.DataSources and AI.Documents; AI.WebCrawlers → AI.DataSources; AI.Agent references AI twice and the Recipes module | Move the shared types to `.Abstractions` or `.Core`; keep a module reference only to carry a feature dependency |
| Channel and carrier modules reference Contact Center's implementation | Omnichannel.Messaging(.Core) and Messaging.Sms(.Core) → `ContactCenter.Core` (about 40 files); their manifests require Contact Center features; `Telnyx.Core` → `ContactCenter.Core` + `Telephony.Core`, and Telnyx hosts orchestration (`TelnyxOutboundBridgeOrchestrator`, IVR, queue treatment, inbound router). Asterisk shows the right shape: it references `ContactCenter.Abstractions` only | D18; split Telnyx into carrier core, a `[RequireFeatures]` Contact Center integration and AI voice |
| Hard AI dependencies | Calendars, Scheduling, Workforce, Seo and `Omnichannel.Core` reference `AI.Core`; `Recipes.Core` references `CrestApps.Core.AI` only for AI step schemas, so its 20 consumers depend on AI | Reference the CrestApps.Core.AI package or move tools to `X.AI` projects; AI schemas move to the AI modules |
| Building blocks that know their consumers | `A/Accounts.Abstractions/AccountPageTabs.cs:41-66` hard-codes tabs for Opportunities, Leads, Orders, Invoices and Service; `M/Recipes/Startup.cs` (2,044 lines, 148 `[RequireFeatures]` startups) hard-codes Contact Center, Telnyx, Omnichannel and AI feature IDs; `SkillSubjectKind { User, Worker }`; `TimePunchSource.ContactCenter`; commerce types in the platform `A/Abstractions` and `A/Reports.Abstractions` | Consumers register themselves; open string kinds; D20 |
| Contracts in the wrong home | `Customer`, `ICustomerService` and `ICustomerStore` in `Customers.Core` (11 projects reference it, 5 without using it); `ICommercePersonalData*` in `Customers.Abstractions`; `IMessagingChannel` in `Messaging.Core`; no `Omnichannel.Abstractions`; `IProviderWebhookInbox` in `ContactCenter.Abstractions`; `ICommerceEmailSender` and `IdempotencyEndpointFilter` in commerce libraries; `CalendarTime`/`RecurrenceRule` in `Calendars.Core` (7 consumers reference the implementation only for them) | Move to the matching `.Abstractions` |
| A building block depends on its consumers | The Customers module references `Orders.Core` and owns `OrderCustomerMergeHandler`, `OrderCustomerPrivacyHandler` and `OrderAnonymizer`; `Checkout.Core` → `Transactions.Core` | Orders implements `ICustomerMergeHandler`/`ICustomerPrivacyHandler` itself |
| Records carry other modules' fields | `Order` holds Point of Sale, B2B and Reviews fields (`Order.cs:90-149`); `OmnichannelActivity` holds dialer, reservation and business-hours fields | Parts or `Properties` owned by each extender |
| Dead or copied project references | `C/Subscriptions.Core` → `Stripe.Core`, `C/Stripe.Core` → `Products.Core`, and the verticals' copied commerce reference sets (Memberships uses none of six) | Remove |
| Bypassing managers | Workforce uses `TeamStore` directly; OrTools instantiates `DefaultDispatchOptimizer`/`DefaultShiftScheduleSolver`; Subscriptions read Transactions' `PaymentAttemptIndex` | Go through the managers and contracts |
| One large implementation assembly | `ContactCenter.Core`: 846 files, 523 in a flat `Services/`; anything that references it pulls in the dialer, IVR and secure capture | D18, then split by feature |

## 5. Extensibility gaps

- **Editors other modules cannot extend.** WorkerProfile, WorkOrder, Asset, ServiceVisit, SchedulingResource and AppointmentType editors are controller forms with fixed view models; their drivers render list rows only. That is why Workforce edits skills itself, while Skills extends the user editor through `UserSkillsDisplayDriver`. Calendars and Teams are the slice's driver-based editors. Rule: build editors on `IDisplayManager<TModel>`.
- **Closed enums that name consumers**: `SkillSubjectKind`, `TimePunchSource.ContactCenter`. Compare Geo `TerritoryMember.Kind`, an open string.
- **Hard-coded extension lists**: `AccountPageTabs`, the Recipes module's 148 startups, `CommerceReportDataSets`.
- **Service locator instead of a seam**: Shipping's `GetService<ICustomerService>()`; `CampaignService(…, IEmailService emailService = null)` sends nothing, silently, when Email is off.

## 6. Code-quality metrics

Solution-wide counts over `src/` (9,158 C# files and 2,905 views; excluding `bin`, `obj`, `node_modules`, `wwwroot` and tests).

| Metric | Count | Notes and top offenders |
| --- | --- | --- |
| `DateTime`/`DateTimeOffset` `.UtcNow`/`.Now`/`.Today` | 64 | 46 are latency timers in Omnichannel.Voice.Core and Telnyx.Core (use `TimeProvider` or `Stopwatch`). Entity timestamps: `EntryPointAddressMigrations.cs:127,171`, `MessagingEntryPointMigrations.cs:165`, `TelephonyHub.cs:443,474`, `GeoWorkOrders.cs:117`, `ChatConversationStartingOrchestrationHandler.cs:40`, `AIConversationTurnNotifier.cs:41`, `GuestSessionTokenManager.cs:238`, `DataConnectorHttpClient.cs:300`, both maps providers (`:91`), `LeadImportProvider.cs:209`, `InMemoryAutomatedConversationGate.cs:59`. Three `_clock?.UtcNow ?? DateTime.UtcNow` fallbacks. Server-local `DateTime.Today`: `PayPeriodsController.cs:215`, `ReportColumnColorizer.cs:59` |
| Sync-over-async | 5 `GetAwaiter().GetResult()`, no `.Wait()` | `AITemplateMethodProvider.cs:23`, `ContactActivityExportHandler.cs:347,361,401`, `SmsPhoneFieldButtonShapeTableProvider.cs:74`. The `IContentTransferFileReader` contract is synchronous |
| Fire-and-forget | 17 discarded tasks, 1 untracked `Task.Run`, no `async void` | Data-loss risk at `TwilioWebhookEndpoint.cs:148` and `AzureEventGridEndpoint.cs:327` (defect 7); `DialerAbandonmentTracker.cs:267` |
| Exceptions | 646 `catch (Exception)`, 53 bare `catch`, 197 broad log-and-continue | Uncommented empty catches: `OrchardCoreAzureAISearchDataSourceDocumentReader.cs:153`, `AIMemoryIndexProfileHandlerBase.cs:67`, `ReportQueryEngine.cs:797`. Money failures in logs only: defect 22 |
| Process-wide state | 2 mutable statics (benign); 58 static readonly collections | Per-node runtime registries in realtime voice and Asterisk (`SecureKeypadCaptures.cs:23`, `TurnBasedToolFillers.cs:17`, `TelnyxRecordingIngestService.cs:42`, `AsteriskAriApplicationOwnershipRegistry.cs:27`, `AsteriskPendingCallerTerminationRegistry.cs:22`) pin those features to one node; `InMemoryAutomatedConversationGate` is the only redelivery guard for Twilio and Azure SMS |
| Files over 1,000 lines | 18 C# files | `ActivitiesController.cs` 2,093; `Recipes/Startup.cs` 2,045; `DefaultInstallmentPlanService.cs` 1,540; `ProductListingService.cs` 1,510; `SmsOmnichannelEventHandler.cs` 1,415; `ContentTransfer/Controllers/AdminController.cs` 1,402; `AsteriskTelephonyProviderBase.cs` 1,349; `ImportFilesBackgroundTask.cs` 1,311; `DefaultContactActivityBatchLoader.cs` 1,296; `DefaultCheckoutEngine.cs` 1,280; `TelephonyHub.cs` 1,238; `EnterpriseInteractionReportProvider.cs` 1,220; `DataSourceIndexingService.cs` 1,188; `MyWorkController.cs` 1,119 (with an inline service-worker script); `DefaultLocalDncListManager.cs` 1,116; `TenantHierarchyBroker.cs` 1,102; `ReportDesignDocumentBuilder.cs` 1,034; `DefaultCalendarEventManager.cs` 1,034. Partial classes hide more: `VoiceAgentConversationLoop` 4,775 and `RealtimeVoiceConversationRunner` 4,662 lines across partial files; Telnyx and Asterisk providers 2,099–2,499. `soft-phone.js` is 11,813 lines |
| More than one public type per file | 728 files, 2,992 types | Breaks the one-type-per-file rule. 114 startup files, 104 view-model files; `Recipes/Startup.cs` has 149 types; `DataConnectorViewModels.cs` 21; `FieldModels.cs` 20; `ReportDesignerViewModels.cs` 15; `TimeTrackerModels.cs` 11 |
| Unsealed public classes | 40 that should be sealed, static or abstract | 17 Stripe.Core DTOs; `UsersConstants` and `UserPermissions` (static); `PaymentEventContextBase` and `ContentImportHandlerBase` (abstract); `DefaultContactActivityBatchLoader` |
| Unbounded queries | 504 `ListAsync`; 355 without `Take` or paging; 141 not keyed by ID | Request-path examples in defect 23; `AccountsDeployment.cs:62`; `TerritoryMatcher.cs:99` loads all territories per match; `MessagingConversationService.cs:317-321` loads every outbound message to pick the newest |
| Hand-written admin lists | 149 views use `<admin-list>`; 2 CRUD lists do not | `TenantHierarchy/Views/Access/Index.cshtml:50`, `TenantHierarchy/Views/Platform/Index.cshtml:56`, `AI.Chat/Views/Admin/Index.cshtml:29`; the unused `ChatInteractionsList.cshtml` |
| Records without recipe or deployment steps | About 25 record types | Workforce `ShiftPattern`, `TimeClockKiosk`, `CoverageRequirement`, `PayrollCsvSettings` (and `LaborRuleSet` missing from deployment); Bookings dates (no deployment); Customers (no deployment), `StoreCreditAccount`, `LoyaltyAccount`, `ReferralProfile`; `Membership`; TenantHierarchy `AccessGrant`; Marketing segments and campaigns; DNC local lists; Messaging broadcasts; `Market`, `Register`, `StockLocation`, `Vendor`, `MerchantTaxRegistration`, `ExemptionCertificate`, `WebhookSubscription`; AI FileSource and WebCrawler records; Seo, NLWeb and AI.Memory settings |
| Feature IDs as literals | 482 `[Feature]`/`[RequireFeatures]` with a literal vs 474 with a constant | `"OrchardCore.Recipes.Core"` in 34 projects; 31 literal `"CrestApps.OrchardCore.AI…"` IDs, 16 of them in Recipes |
| Unlocalized UI strings | A few | `ContactCenterHub.cs:383,451,471,592` (`HubException` text shown to agents), `ForecastService.cs:110,117,126`, `MarketplaceService.cs:242,247`, `MessagingQuietHoursGuard`, DNC import row errors, `AIProfileDataExtraction.Edit.cshtml:164-176` |
| Modules without tests | 21 | `DncRegistry` (2,173 lines of services), the four `.Azure` adapters, the Elasticsearch/FTP/SFTP adapters, `AzureAIInference`, `Ollama`, `Resources`; module projects of Omnichannel, Omnichannel.Voice, PhoneNumbers, Recipes, Wizard |
| Modules without `README.md` | 85 of 119 | `AGENTS.md` requires one per module |
| Other | | One `new HttpClient(` (the Asterisk.Web sample host), one TODO, no secrets, no hand-written `global using` (`ImplicitUsings` is on in `Directory.Build.props`) |

## 7. Repository hygiene

- `AGENTS.md` was stale: it still described Orchard Core 3.0, listed about 35 of the 119 modules (some that no longer exist), pointed to a `copilot-instructions.md` that does not exist, and named one of seven test projects. Fixed with this audit.
- `IMPLEMENTATION-PROGRESS.md` at the repository root tracks an SMS portal (`Omnichannel.Sms.Portal`) whose projects do not exist. Remove it.
- `tests/tenant-hierarchy.coverage.xml` is a coverage output committed by mistake. Remove it and ignore `*.coverage.xml`.
- `src/Modules/CrestApps.OrchardCore.Companies` holds only `bin`/`obj`: the module was replaced by Accounts in #1266. Delete the folder. The `orchardcore-companies` plugin skill is stale too.
- The SignalR module is marked deprecated and nothing depends on it; `SignalR.Core` holds three classes while `SignalRHubRoutes` lives in `CrestApps.OrchardCore.Core`. Remove the module and fold `SignalR.Core` into Core.
- Dead code: the AI fields on `OmnichannelCampaign` (read only in `Clone()`); `OmnichannelContactInfoPart` registered while its constant is commented out; `M/AI.Chat.Interactions/Views/ChatInteractionsList.cshtml` and its view model, referenced nowhere.
- Misspelled names: `TwillioRequestValidator`, `OminchannelActivityAIChatSessionIndex`.
- 85 of 119 modules have no `README.md`, and 21 modules have no tests (section 6).

## 8. Improvement plan

One plan and pull request per phase, with a commit per item. Every item that changes behavior starts with a failing test. Every phase ends with a full build (`-warnaserror`), the unit tests, the public-API baselines and a UI pass of the screens it touched.

### Phase 0: guardrails (first, small)

So the solution stops gaining new duplicates while the old ones are removed:

1. **Layering tests.** A unit test that reads every `.csproj` and fails when a `.Core` library references a module (today: `Omnichannel.Core` → ContentFields), an `.Abstractions` library references a `.Core` library or a module (today: none), or a module assembly uses a type from another module assembly (checked on the compiled assemblies; a module may keep a project reference to carry a feature dependency, but its code must not use the other module's types, with no exception for sub-modules, providers or `Resources`). Today's violations go into an allowlist that may only shrink (a ratchet), like `SiteSettingsGroupPermissionRegistrationTests`.
2. **Duplicate-pattern tests.** Fail on new copies of known duplicates: a private `EscapeCsv`, `new BlobFileStore(` outside the shared helper, a `*DeploymentSerializer` class outside Core, `IClock.GetTimeZones()` in a select list, `DateTime.UtcNow`/`DateTime.Now`/`DateTime.Today` outside tests, `TimeZoneInfo.ConvertTimeToUtc`.
3. **The rules and the catalog** (this change): `AGENTS.md` *Building Blocks and Reuse*, `CLAUDE.md`, `docs/engineering/building-blocks.md`.

### Phase 1: correctness and security

The 23 defects in section 1, one commit each, each with a regression test. Start with 1–5, 7, 8, 10–13 (all small), then 22 and 23.

### Phase 2: platform DRY (low risk, high reuse)

1. Delete the two serializer copies (defect 10).
2. Tabular reader/writer (P1); move Reports, AI.Chat, Inventory, Orders and Checkout onto it.
3. `AddTenantFileStore` and `EncryptedFileStore` (P2); collapse the five `.Azure` modules into it (keep their feature IDs as aliases or add a migration so sites keep them enabled).
4. Secret-field helpers (P7); migrate the AI, provider, MCP, data-source and A2A drivers.
5. `NamedCatalogRecipeStep`/`NamedCatalogDeploymentSource` for the 32 cloned recipe steps and about 14 deployment sources (Contact Center, Omnichannel.Managements, AI, AI.Mcp, AI.DataSources, DataConnectors); add the missing recipe/deployment steps (section 6).
6. `AuditedCatalogEntryHandlerBase` for the 17 copied handlers and a timestamp hook in the `DocumentCatalog` base for the 11 copied stores.
7. `CatalogAdminController<T>` (P14) and `LockAllAsync` (P15).
8. Shared small helpers (`HasPart`, `NormalizeTimeZoneId`, `IsSameAddress`, country-code normalization, `ParseRecipients`, `CryptographicOperations.FixedTimeEquals`, `JsonSerializerOptions.Web`, `Truncate` from CrestApps.Core.Support, one `escapeHtml` in Resources, one ai-chat-ui version constant); `ITimeZoneSelectListProvider` everywhere.
9. The AI clones: one connection-auth editor and API-key handler for A2A and MCP; the AI.DataSources helpers.
10. Remove dead project references and the hygiene items in section 7.

### Phase 3: re-layering (no behavior change)

1. Move contracts into Abstractions: Customers, Omnichannel (new `Omnichannel.Abstractions`, contacts D3), ContentFields (D19), Commerce (D20), `CalendarTime`/recurrence (D7), Contact Center distribution readers (D18), Workforce AI and portal base types.
2. Remove the module-to-module references in section 4.
3. AI tools in consumers reference the CrestApps.Core.AI package or move to `X.AI` projects; `Recipes.Core` drops `CrestApps.Core.AI`; each module owns its recipe-step schemas and the Recipes module stops naming feature IDs.
4. Split the designer engine out of `Reports.Core` so data-set base classes come from a slim library; move AI.Documents below chat.
5. `AccountPageTabs` and similar lists become registrations by the extenders.

### Phase 4: extract the missing building blocks

In this order, because later items use earlier ones: number sequence and readable codes (P6), transition tables and balance ledger (P11, P12), leased work queue (P5), retention (P13), provider webhook inbox (P4), outbound webhooks (P3), money service (D10), order-line grants, line refunds and role grants (D11, D12), balance tenders (D13), time zones and intervals (D7), business hours and holidays (D6), team membership (D5), parties, contact info, named addresses and store locations (D1, D2, D4), consent (D8), personal data (D15), search index schema with the Core client-factory overloads, deleting the forks (P9).

Each extraction migrates every copy listed in section 2 in the same plan, with data migrations where stored shapes change.

### Phase 5: larger refactors

1. One skill model (Contact Center on Skills, open subject kinds, D16); delete the 768-line mirror.
2. Customer linked to Account; Vendor as an Account; retire `Customer.Kind=Business`.
3. Outreach engine (D9) for broadcasts and Marketing.
4. Messaging routing on the Contact Center strategy pipeline; retire the second presence tracker.
5. Split Telnyx (carrier core, Contact Center integration, AI voice); recording ingest service and one call-control result family (D17).
6. One lock and conflict check for Scheduling and Calendars resources (defect 14); Bookings time slots on Scheduling.
7. Editors on `IDisplayManager<TModel>` for WorkerProfile, WorkOrder, Asset, ServiceVisit, SchedulingResource and AppointmentType.
8. Subscriptions through Orders, or an order record per subscription payment, so revenue reaches every order consumer.
9. AI.Chat analytics and the TimeTracker report on Reports.
10. Connections (P10) for AI external sources, FTP/SFTP and carriers.
11. Split the god classes (section 6), starting with the ones a phase already touches.
