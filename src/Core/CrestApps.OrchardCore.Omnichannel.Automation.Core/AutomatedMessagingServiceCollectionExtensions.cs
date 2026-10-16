using CrestApps.OrchardCore.Omnichannel.Automation;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers a messaging channel with the automated (AI) conversation engine.
/// </summary>
public static class AutomatedMessagingServiceCollectionExtensions
{
    /// <summary>
    /// Automates a messaging channel: its activities start with an AI-written opening message, its inbound messages are
    /// answered by the AI, an entry point can route its first messages to an AI agent, and owed replies and cadence
    /// follow-ups are sent on it. The engine's shared services are registered once, whichever channels use them.
    /// </summary>
    /// <typeparam name="TChannel">The channel's adapter.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAutomatedMessagingChannel<TChannel>(this IServiceCollection services)
        where TChannel : class, IAutomatedMessagingChannel
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<TChannel>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAutomatedMessagingChannel, TChannel>());

        // One processor per channel: the automated activities task picks the processor by the activity's channel.
        services.AddScoped<IOmnichannelProcessor>(sp => ActivatorUtilities.CreateInstance<AutomatedConversationProcessor>(sp, sp.GetRequiredService<TChannel>()));

        services.TryAddScoped<AutomatedConversationHandler>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOmnichannelEventHandler, AutomatedConversationHandler>());

        // Scoped on purpose: the automated handler and the messaging workspace both ask it about the same message, and
        // the one instance remembers the answer.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IMessagingAIConversationStarter, AutomatedEntryPointConversationStarter>());

        services.TryAddScoped<AutomatedOwedReplyRecovery>();
        services.TryAddScoped<AutomatedFollowUpService>();

        return services;
    }
}
