using CrestApps.OrchardCore.Telnyx.Endpoints;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Telnyx;

/// <summary>
/// Registers the Telnyx SMS feature: the Telnyx <see cref="ISmsProvider"/> under the technical name "Telnyx"
/// (so the SMS portal's dispatcher can resolve it per number) and the signed messaging webhook that receives
/// inbound messages and outbound delivery receipts.
/// </summary>
[Feature(TelnyxConstants.Feature.Sms)]
public sealed class SmsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSmsProvider<TelnyxSmsProvider>(TelnyxConstants.ProviderTechnicalName);
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddTelnyxSmsWebhookEndpoint();
    }
}
