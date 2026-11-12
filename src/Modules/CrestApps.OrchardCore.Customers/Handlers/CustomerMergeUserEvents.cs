using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Users;
using OrchardCore.Users.Events;
using OrchardCore.Users.Handlers;
using OrchardCore.Modules;
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
        // Only a user who proved they control their mailbox by a round trip gets the guests that bought with it.
        if (user is not User { UserId: { Length: > 0 } userId, EmailConfirmed: true } account || !UserEmailOwnership.IsVerified(account.Properties))
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

public sealed class CustomerMergeUserEventHandler : UserEventHandlerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public CustomerMergeUserEventHandler(IServiceProvider serviceProvider, ILogger<CustomerMergeUserEventHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    // Following the confirmation link is the round trip that proves the user controls the mailbox, so it is recorded
    // before the guests that bought with the address are merged.
    public override async Task ConfirmedAsync(UserConfirmContext context)
    {
        if (context.User is User user && !UserEmailOwnership.IsVerified(user.Properties))
        {
            UserEmailOwnership.MarkVerified(user.Properties, _serviceProvider.GetRequiredService<IClock>().UtcNow);
            await _serviceProvider.GetRequiredService<UserManager<IUser>>().UpdateAsync(user);
        }

        await CustomerMerge.RunAsync(_serviceProvider, context.User, _logger);
    }
}
