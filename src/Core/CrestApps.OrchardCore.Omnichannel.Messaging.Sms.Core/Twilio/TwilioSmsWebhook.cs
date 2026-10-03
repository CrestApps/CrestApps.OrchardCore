using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Settings;
using OrchardCore.Sms.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Twilio;

/// <summary>
/// Receives texts sent to Twilio numbers: the signed inbound-SMS webhook, its address on the Twilio SMS settings screen,
/// and the logging of the reasons Twilio refuses a message.
/// </summary>
/// <remarks>
/// Both SMS Omnichannel Automation and the SMS Messaging Channel hear inbound texts, and either can be on without the
/// other, so each registers this when Twilio is enabled. Registering is idempotent: with both on, the services and the
/// route are added once, which a second copy of the route or of the refusal logging would not survive.
/// </remarks>
internal sealed class TwilioSmsWebhook
{
    private bool _mapped;

    /// <summary>
    /// Registers the webhook's services, unless another feature already has.
    /// </summary>
    /// <param name="services">The tenant's service collection.</param>
    public static void AddServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.Any(descriptor => descriptor.ServiceType == typeof(TwilioSmsWebhook)))
        {
            return;
        }

        services.AddSingleton<TwilioSmsWebhook>();

        // Twilio says why it refused a message, and its provider throws that away. Recorded, so a text that fails
        // for credentials, region or a trial restriction says which in the log, and a refusal for an opted-out
        // recipient reaches the SMS dispatcher, which records the opt-out.
        services.AddTransient<TwilioErrorLoggingHandler>();
        services.AddHttpClient(TwilioSmsProvider.TechnicalName)
            .AddHttpMessageHandler<TwilioErrorLoggingHandler>();

        // Shows the inbound-SMS webhook address under Orchard Core's Twilio settings, beside the endpoint it names.
        services.AddDisplayDriver<ISite, TwilioSmsWebhookSettingsDisplayDriver>();
    }

    /// <summary>
    /// Maps the webhook route, unless another feature already has for this tenant.
    /// </summary>
    /// <param name="routes">The tenant's route builder.</param>
    /// <param name="serviceProvider">The tenant's services.</param>
    public static void MapEndpoint(IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var registration = serviceProvider.GetRequiredService<TwilioSmsWebhook>();

        if (registration._mapped)
        {
            return;
        }

        registration._mapped = true;
        routes.AddTwilioWebhookEndpoint();
    }
}
