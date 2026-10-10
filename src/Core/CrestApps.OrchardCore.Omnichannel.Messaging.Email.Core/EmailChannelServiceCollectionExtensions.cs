using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Transports;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Webhooks;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the pieces a feature can add to the email channel.
/// </summary>
public static class EmailChannelServiceCollectionExtensions
{
    /// <summary>
    /// Adds a way for email addresses to send, offered in the address editor beside Orchard Core's email service and the
    /// address's own SMTP server: a provider's HTTP API, for example.
    /// </summary>
    /// <typeparam name="TTransport">The transport.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddEmailTransport<TTransport>(this IServiceCollection services)
        where TTransport : class, IEmailTransport
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IEmailTransport, TTransport>());

        return services;
    }

    /// <summary>
    /// Adds a provider format the inbound email webhook reads, served at
    /// <c>api/omnichannel/email/inbound/{name}</c>, where the name is the parser's <see cref="IInboundEmailWebhookParser.Name"/>.
    /// </summary>
    /// <typeparam name="TParser">The parser.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddInboundEmailWebhookParser<TParser>(this IServiceCollection services)
        where TParser : class, IInboundEmailWebhookParser
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IInboundEmailWebhookParser, TParser>());

        return services;
    }
}
