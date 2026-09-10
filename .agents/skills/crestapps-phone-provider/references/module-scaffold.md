# Module scaffold, settings, options, and DI registration

A new provider (say `Twilio`) is two projects plus a docs page. Mirror the Telnyx layout exactly.

## Project layout

```
src/Core/CrestApps.OrchardCore.<Provider>.Core/          # shared, testable logic (no Orchard module chrome)
  <Provider>Constants.cs
  Models/<Provider>Settings.cs, <Provider>SmsSettings.cs
  Services/<Provider>Options.cs, <Provider>TelephonyProvider.cs, <Provider>ContactCenterVoiceProvider.cs,
           <Provider>WebhookSignatureValidator.cs, <Provider>WebhookInboxHandler.cs,
           <Provider>SoftPhoneRegistrationConfigContributor.cs, <Provider>AgentCredentialStore.cs, ...
  Indexes/…                                               # if you persist per-agent credentials / ingest jobs

src/Modules/CrestApps.OrchardCore.<Provider>/            # the Orchard module
  Manifest.cs
  Startup.cs                                              # + feature-gated sub-startups
  <Provider>Constants partial / uses Core constants
  Controllers/, Drivers/, Endpoints/, ViewModels/, Views/, Migrations/, BackgroundTasks/
  Services/<Provider>OptionsConfigurations.cs, <Provider>ProviderOptionsConfigurations.cs,
           <Provider>SmsProvider.cs, <Provider>SettingsDisplayDriver.cs, ...
```

Why the split: the `.Core` project holds everything unit tests need (the provider, webhook parser/validator,
credential logic) with a fakeable HTTP seam, while the module project holds Orchard integration (manifest,
startup, endpoints, display drivers, views). Tests live under `tests/CrestApps.OrchardCore.Tests/<Provider>/`.

## csproj wiring

- Both projects target the repo TFM via `Directory.Build.props` (do not pin a TFM in the csproj).
- The `.Core` project references the abstraction projects it needs:
  `CrestApps.OrchardCore.Telephony.Abstractions`, `CrestApps.OrchardCore.ContactCenter.Abstractions`,
  and Orchard packages via `Directory.Packages.props` (centralized versions — never inline a version).
- The module project references the `.Core` project and the framework/module packages it needs
  (`OrchardCore.Module.Targets`, `OrchardCore.Sms.Abstractions` for SMS, `CrestApps.OrchardCore.WebSockets`
  if you stream media, etc.).
- **Add the module to the Targets project** so Orchard discovers it:
  `src/Targets/CrestApps.OrchardCore.Cms.Core.Targets/CrestApps.OrchardCore.Cms.Core.Targets.targets`
  (see `AGENTS.md` → *Adding a New Module*).

## Constants

Put stable strings in `<Provider>Constants` (see `TelnyxConstants.cs` for the full set):
`ProviderTechnicalName`, `BrowserMediaAdapterName`, data-protector names (API key, webhook key), default API
base URL, signaling URLs/domains, webhook path(s), signature/timestamp header names, feature ids
(`Feature.Area`, `Feature.Sms`), work-partition keys, recording/media constants, SMS section name.

The technical name is the identity used everywhere (provider resolution, `HttpClient` name, credential
selection). Keep it constant and unique.

## Manifest & features

```csharp
[assembly: Module(Name = "Twilio", Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website, Version = CrestAppsManifestConstants.Version,
    Description = "Integrates the Twilio voice platform with the Telephony soft phone.", Category = "Telephony")]

[assembly: Feature(Id = TwilioConstants.Feature.Area, Name = "Twilio",
    Description = "Provides the Twilio telephony provider, its browser soft phone, and signed call-event webhooks. When Contact Center Voice is also enabled, the Twilio provider automatically participates in contact center orchestration.",
    Category = "Telephony",
    Dependencies = [ TelephonyConstants.Feature.Area, WebSocketsConstants.Feature.Area /* if streaming media */ ])]

[assembly: Feature(Id = TwilioConstants.Feature.Sms, Name = "Twilio SMS",
    Description = "Adds the Twilio SMS/MMS provider and its signed inbound and delivery-receipt webhook.",
    Category = "Communication", Dependencies = [ TwilioConstants.Feature.Area, "OrchardCore.Sms" ])]
```

The main provider feature depends on the Telephony feature (and WebSockets only if you stream media). SMS is
a **separate feature** depending on the provider feature and `OrchardCore.Sms`.

## Settings + options (the IOptionsMonitor pattern)

Persist admin-editable configuration as a site settings class, but expose a resolved **options** object that
merges appsettings + UI settings and refreshes without a shell restart.

1. `Models/<Provider>Settings.cs` — the persisted settings (data-protect secrets like API key & webhook key).
2. `Services/<Provider>Options.cs` — the runtime options with an `IsConfigured`/`IsEnabled` guard used by
   every provider method to fail closed when unconfigured.
3. `Services/<Provider>OptionsConfigurations.cs : IConfigureOptions<<Provider>Options>` — merges appsettings
   section + the site settings, unprotecting secrets.
4. Register: `services.AddOptions<<Provider>Options>().Services
      .AddTransient<IConfigureOptions<<Provider>Options>, <Provider>OptionsConfigurations>()
      .AddSignalOptionsChangeTokenSource<<Provider>Options>()` so settings edits invalidate the options.
   (See the memory note on `IOptionsMonitor` settings refresh for the shell-signal pattern.)
5. `Drivers/<Provider>SettingsDisplayDriver.cs` + `Views/<Provider>Settings.Edit.cshtml` for the admin UI,
   registered with `.AddSiteDisplayDriver<<Provider>SettingsDisplayDriver>()`.

Read settings/options with `IOptionsMonitor<T>.CurrentValue` (not `IOptions<T>`) so changes are picked up.

## Registering the provider with the telephony framework

```csharp
public sealed class TwilioProviderOptionsConfigurations : IConfigureOptions<TelephonyProviderOptions>
{
    private readonly IOptionsMonitor<TwilioOptions> _options;
    public TwilioProviderOptionsConfigurations(IOptionsMonitor<TwilioOptions> options) => _options = options;

    public void Configure(TelephonyProviderOptions options)
    {
        var typeOptions = new TelephonyProviderTypeOptions(typeof(TwilioTelephonyProvider))
        {
            IsEnabled = _options.CurrentValue.IsEnabled,
        };
        options.TryAddProvider(TwilioConstants.ProviderTechnicalName, typeOptions);
    }
}
```

Register with `.AddTelephonyProviderOptionsConfiguration<TwilioProviderOptionsConfigurations>()`. This is
what makes the provider selectable under **Settings → Communication → Telephony**.

## Startup (mirror Telnyx)

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // 1. Resilient named HTTP client (see webhooks-and-resilience.md).
        services.AddHttpClient(TwilioConstants.ProviderTechnicalName)
            .AddStandardResilienceHandler(options => { /* retry+jitter, circuit breaker, DisableForUnsafeHttpMethods */ });

        // 2. Options + provider registration + settings UI.
        services.AddOptions<TwilioOptions>().Services
            .AddTransient<IConfigureOptions<TwilioOptions>, TwilioOptionsConfigurations>()
            .AddSignalOptionsChangeTokenSource<TwilioOptions>()
            .AddTelephonyProviderOptionsConfiguration<TwilioProviderOptionsConfigurations>()
            .AddSiteDisplayDriver<TwilioSettingsDisplayDriver>();

        // 3. Provider-scoped services: webhook service, credential issuer/store, registration contributor,
        //    soft-phone registrar/revoker, media provisioner, etc.
        services
            .AddScoped<ITwilioWebhookService, TwilioWebhookService>()
            .AddScoped<ISoftPhoneRegistrationConfigContributor, TwilioSoftPhoneRegistrationConfigContributor>()
            .AddScoped<ISoftPhoneCredentialRegistrar, TwilioSoftPhoneCredentialRegistrar>()
            .AddScoped<ISoftPhoneCredentialRevoker, TwilioSoftPhoneCredentialRevoker>();

        // 4. Inbound-router fallback via TryAddScoped so the Contact Center router wins when Voice is enabled.
        services.TryAddScoped<ITwilioInboundCallRouter, TwilioDirectInboundCallRouter>();

        // 5. Indexes/migrations for persisted credentials / ingest jobs, background tasks, health metrics.
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider sp)
        => routes.AddTwilioWebhookEndpoint();
}

[RequireFeatures(ContactCenterConstants.Feature.Voice)]
public sealed class DialerStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<TwilioContactCenterVoiceProvider>()
            .AddScoped<IContactCenterVoiceProvider>(sp => sp.GetRequiredService<TwilioContactCenterVoiceProvider>())
            .AddSingleton<IProviderIdentityProvider, TwilioProviderIdentityProvider>()
            .AddScoped<ITwilioInboundCallRouter, ContactCenterTwilioInboundCallRouter>()
            .AddScoped<IProviderWebhookInboxHandler, TwilioWebhookInboxHandler>();
        // + IContactCenterFeatureLifecycleParticipant per work partition, recording ingest, etc.
    }
}

[RequireFeatures(ContactCenterConstants.Feature.VoiceMedia)]
public sealed class TwilioContactCenterMediaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddScoped<IContactCenterVoiceMediaProvider, TwilioContactCenterVoiceMediaProvider>();
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider sp)
        => routes.AddTwilioMediaStreamEndpoint();
}

[RequireFeatures(TwilioConstants.Feature.Sms)]      // often a dedicated SmsStartup.cs
public sealed class SmsStartup : StartupBase { /* AddSmsProvider<TwilioSmsProvider>, settings driver, sms webhook */ }
```

Key points reproduced from Telnyx:
- CC voice and CC media adapters are **integration glue**, not operator-facing features — they auto-activate
  via `[RequireFeatures(...)]` when both the provider and the CC feature are on. No redundant per-provider toggle.
- Use `TryAddScoped`/`TryAdd*` for fallbacks that a higher-priority feature should be able to override.
- Register lifecycle participants (`IContactCenterFeatureLifecycleParticipant`) per stable work-partition key
  so in-flight work drains cleanly across shell reloads. Keep the partition key value stable forever.

## README

Every module needs a `README.md` (purpose, features, installation, configuration, usage, dependencies) —
`AGENTS.md` → *Module README Files*.
