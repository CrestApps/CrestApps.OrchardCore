# 07 - Phase 3: expose every suite feature in the `CrestApps.Core.Mvc.Web` sample host

Outline only. This phase is re-planned in detail after Phase 2 ships; the goal here is to fix the scope, the approach, and the inventory so the detailed plan is a fill-in exercise.

## 1. Goal

A developer runs `dotnet run --project src/Startup/CrestApps.Core.Mvc.Web` and can configure and operate every Contact Center Suite feature (agents, queues, entry points, dialer, business hours, recording governance, supervision, soft phone, SMS portal, Telnyx/Asterisk/Dialpad settings, automation settings) with standard ASP.NET Core MVC screens, the same way the AI areas (`Areas/AI`, `Areas/AIChat`, `Areas/ChatInteractions`, `Areas/Mcp`, ...) expose the AI suite today. `Blazor.Web` parity follows the MVC areas.

## 2. Approach (same as the existing MVC areas)

- One MVC **area per pillar**: `Areas/Omnichannel`, `Areas/Telephony`, `Areas/ContactCenter`, `Areas/SmsPortal`, `Areas/TelephonyProviders`.
- Controllers + Razor views + view models in the sample host. Orchard display drivers become view models plus partial views; Orchard shapes/`Initialize<T>` editors become tag-helper forms; Orchard `AdminMenu`s become the area's `_Layout` navigation; Orchard permissions become role policies (`Admin`, `Supervisor`, `Agent`) registered in `Program.cs` and `AuthorizationHandler`s for the framework operations.
- Site-level settings (`TelephonySettings`, `ContactCenterRecordingSettings`, `SecureCaptureSettings`, `ContactCenterExternalTransferSettings`, `TelnyxOptions`, `TelnyxSmsOptions`, `DefaultAsteriskOptions`, `DialpadOptions`, `SmsPortalOptions`, `OmnichannelAutomationOptions`) are edited through the existing `SiteSettingsStore` bridge in `CrestApps.Core.Startup.Shared` (`SiteSettingsConfigureStoredOptions<T>` + change tokens), like the AI provider connection settings page.
- Concrete hubs (`TelephonyHub`, `ContactCenterHub`, `SmsPortalHub`) in `Areas/*/Hubs`, mapped in `Program.cs`.
- Front-end: reference `CrestApps.ContactCenter.Resources` static web assets; the soft phone, agent workspace, supervisor dashboard, and SMS inbox scripts are reused as-is with a small MVC-specific bootstrap (endpoint URLs, hub URLs, antiforgery token) rendered by the views.
- Sample-only plumbing (users, roles, a "simulated" telephony provider for local test drives, seed data) goes in `CrestApps.Core.Startup.Shared` so Blazor reuses it.

## 3. Screen inventory (derived from the Orchard controllers)

| Area | Orchard source | MVC screens |
| --- | --- | --- |
| Omnichannel | `Omnichannel.Managements/Controllers/{Activities,ActivityBatches,Cadences,CampaignGroups,Campaigns,ChannelEndpoints,Dispositions,SubjectFlows}Controller` | list/create/edit/delete for contact definitions, contacts, subject definitions (including the subject flow settings and AI settings tabs that Orchard shows on the content type), subjects, campaigns, groups, dispositions, subject actions, channel endpoints, cadences; activity list with filters, bulk actions, disposition; activity batch load (framework default loaders over the contact store) |
| Telephony | `Telephony/Controllers/{Extensions,SoftPhone,TelephonyOAuth}Controller`, settings driver | telephony settings, provider selection, extensions CRUD, soft phone page (standalone `/softphone`), OAuth connect flow for providers that need it, dialer endpoints |
| Providers | `Telnyx/Controllers/TelnyxConnectController`, settings drivers for Telnyx/Telnyx SMS/Asterisk/Dialpad, Orchard `OrchardCore.Sms.Twilio` settings, `DialPad/Controllers/DialpadWebhookRegistrationController` | a `Providers` section with one settings page per provider: Telnyx voice (API key, connection, numbers, webhook URL display), Telnyx SMS, Twilio SMS (account SID, auth token, from number), Asterisk (ARI, PJSIP, realtime), Dialpad (OAuth, webhook registration), the simulated provider; a "test send SMS" and "test call" action on each so a consumer can verify configuration end to end; provider selection for the soft phone and per-channel-endpoint SMS provider selection |
| Reports (after Phase 4 P4.1) | `Reports/Controllers/ReportsController`, filter drivers | report list, run with date-range filters, render `ReportDocument` sections/tables/charts, CSV/Excel export |
| Contact Center | `ContactCenter/Controllers/{AgentEntitlements,AgentSoftPhone,AgentStateReasonCodes,AgentWorkspace,BusinessHoursCalendars,ContactCenterCatalog,DialerProfiles,EntryPoints,MyVoicemailGreeting,QueueGroups,Queues,SecureCapture,Skills,SupervisorDashboard,VoiceMedia}Controller` + settings drivers | settings (coordination, recording, secure capture, external transfer, health); skills, queue groups, queues (routing strategy, treatment, limits, overflow, after-hours), business hours calendars, agent profiles + reason codes + entitlements, entry points (IVR flow editor: reuse the JSON model binder as a plain JSON textarea first), dialer profiles, agent workspace (offers, presence, wrap-up, soft phone), supervisor dashboard (live queues/agents, monitor/whisper/barge), secure capture page, voice media items, my voicemail greeting, recording erasure |
| SMS Portal | `Omnichannel.Sms.Portal/Controllers/{Admin,SmsBroadcasts,SmsTemplates}Controller` | inbox and conversation view, composer, templates CRUD, broadcasts CRUD, endpoint routing settings |
| Health | `ContactCenterHealthEndpoints` | already an endpoint; add a simple status page |

Workflows, deployments, recipes, audit trail, content transfer, and widgets have no MVC equivalent (D-18). Reports are covered once Phase 4 P4.1 lands.

## 4. Things that do not map one-to-one (decide before starting)

- **Contacts and subjects** use the framework default model (`AddContacts()`/`AddSubjects()`, D-4): the sample gets full CRUD screens for contact definitions, contacts (with contact methods and communication preferences), subject definitions (settings, AI settings, field schema), and subjects, plus a seed (like `articles-seed.json`). The subject editor renders inputs from the definition's field schema, which is the MVC equivalent of Orchard's content-type editor. No sample-only model is needed.
- **Display-driver composition** (several drivers contributing to one editor, e.g. queue editors extended by the Dialer or SMS features) becomes explicit partial views included conditionally by the controller based on the registered features (check `IServiceProvider` for the feature's marker options).
- **Permissions** become role policies; the framework `OperationAuthorizationRequirement`s get a `RoleOperationAuthorizationHandler` in `Startup.Shared`.
- **Multi-tenancy** does not exist in the sample; `SingleTenantAccessor` is used.
- **Background tasks** run through `AddBackgroundWorkers()` hosted runners.
- **Provider test drive** without a carrier account: add a `Simulated` telephony provider (in `Startup.Shared`, based on the `FakeTelephonyProvider*` test doubles) that emits provider events on a timer so the workspace, routing, and supervisor screens can be exercised locally; real providers (Telnyx first) are configured through appsettings/user secrets.
- **IVR flow editor** and the agent bar are Orchard-specific UI; the sample ships a JSON editor for IVR flows and a simplified workspace page instead of the docked bar.

## 5. Deliverables

- MVC areas, controllers, views, view models, hubs, sample services, seed data.
- `docs/contact-center/mvc-example.md` in the Core docs describing the screens and how the sample wires each feature.
- Playwright smoke run (`tests/realtime-client`-style) for login, create queue, sign in agent, simulated inbound call, accept, wrap-up.
- Blazor parity plan as a follow-up.
