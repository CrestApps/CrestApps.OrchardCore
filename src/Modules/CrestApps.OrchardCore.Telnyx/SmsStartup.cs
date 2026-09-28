using CrestApps.OrchardCore.Telnyx.Drivers;
using CrestApps.OrchardCore.Telnyx.Endpoints;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Telnyx;

/// <summary>
/// Registers the Telnyx SMS feature, mirroring OrchardCore's Twilio provider structure: the Telnyx
/// <see cref="ISmsProvider"/> under the technical name "Telnyx", its options resolved from a merge of the
/// <c>OrchardCore_Sms_Telnyx</c> appsettings section and the UI site settings (via
/// <see cref="IOptionsMonitor{TOptions}"/>), gating that only enables the provider when configured, the UI
/// settings driver, and the signed messaging webhook.
/// </summary>
[Feature(TelnyxConstants.Feature.Sms)]
public sealed class SmsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Resolve the Telnyx SMS options from appsettings + UI settings, refreshed on change.
        services
            .AddOptions<TelnyxSmsOptions>()
            .Services
            .AddTransient<IConfigureOptions<TelnyxSmsOptions>, TelnyxSmsOptionsConfiguration>()
            .AddSignalOptionsChangeTokenSource<TelnyxSmsOptions>();

        // Register the provider under its technical name, then gate its enabled state on the resolved options
        // (registered after AddSmsProvider so the gate's IsEnabled wins).
        services.AddSmsProvider<TelnyxSmsProvider>(TelnyxConstants.ProviderTechnicalName);
        services.AddSmsProviderOptionsConfiguration<TelnyxSmsProviderOptionsConfiguration>();

        // The UI-driven settings on the SMS settings screen.
        services.AddSiteDisplayDriver<TelnyxSmsSettingsDisplayDriver>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddTelnyxSmsWebhookEndpoint();
    }
}
