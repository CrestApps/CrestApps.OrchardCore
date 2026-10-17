using CrestApps.OrchardCore.Customers.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Users;
using OrchardCore.Users.Events;
using OrchardCore.Users.Handlers;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Customers.Handlers;

/// <summary>
/// Merges a user's guest customers when the user signs in, registers or confirms their email. The merge itself checks
/// that the email is verified; a failure is logged and the merge is tried again on the next sign-in.
/// </summary>
/// <remarks>
/// The merge service is resolved when the event fires, not injected: Orchard Core's user store and user service build
/// these handlers, and the merge service needs the user service, so injecting it would be a circular dependency.
/// </remarks>
internal static class CustomerMerge
{
    public static async Task RunAsync(IServiceProvider serviceProvider, IUser user, ILogger logger)
    {
        if (user is not User { UserId: { Length: > 0 } userId, EmailConfirmed: true })
        {
            return;
        }

        try
        {
            var merged = await serviceProvider.GetRequiredService<ICustomerMergeService>().MergeVerifiedGuestsAsync(userId);

            if (merged > 0 && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Merged {Count} guest customers into the customer of a user whose email is verified.", merged);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The guest customers of a user could not be merged; they merge on the next sign-in.");
        }
    }
}

public sealed class CustomerMergeLoginFormEvent : LoginFormEventBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public CustomerMergeLoginFormEvent(IServiceProvider serviceProvider, ILogger<CustomerMergeLoginFormEvent> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public override Task LoggedInAsync(IUser user)
        => CustomerMerge.RunAsync(_serviceProvider, user, _logger);
}

public sealed class CustomerMergeRegistrationFormEvents : RegistrationFormEventsBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public CustomerMergeRegistrationFormEvents(IServiceProvider serviceProvider, ILogger<CustomerMergeRegistrationFormEvents> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    // A site that does not ask users to confirm their email marks it confirmed at registration.
    public override Task RegisteredAsync(IUser user)
        => CustomerMerge.RunAsync(_serviceProvider, user, _logger);
}

public sealed class CustomerMergeUserEventHandler : UserEventHandlerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public CustomerMergeUserEventHandler(IServiceProvider serviceProvider, ILogger<CustomerMergeUserEventHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public override Task ConfirmedAsync(UserConfirmContext context)
        => CustomerMerge.RunAsync(_serviceProvider, context.User, _logger);
}
