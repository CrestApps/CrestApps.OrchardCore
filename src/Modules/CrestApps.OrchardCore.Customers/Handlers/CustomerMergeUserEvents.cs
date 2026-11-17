using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Core.Services;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Users;
using OrchardCore.Users.Events;
using OrchardCore.Users.Handlers;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Customers.Handlers;

/// <summary>
/// Merges a user's guest customers when the user signs in, registers or confirms their email. The merge itself checks
/// that the email is verified; a failure is logged and the merge is tried again on the next sign-in. A sign-in whose
/// address isn't verified yet, and a registration, are told that guest orders placed with it join the account once it
/// is confirmed, and where to confirm it.
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
        if (user is not User { UserId: { Length: > 0 } userId, EmailConfirmed: true } account || !UserEmailOwnership.IsVerified(account.Properties, account.Email))
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

    // A user whose address isn't proved gets nothing bought with it (owner decision 17). The user is told that guest
    // orders placed with it join the account once it is confirmed, with a link to the page where a code proves it, so
    // "register with the same email, and your orders appear in your account" stays true. The page is there whenever this
    // feature is on, which depends on it. Every such user gets the same notice, worded conditionally, and the store isn't
    // asked: telling only those whose address has guest orders would tell anyone who registers with an address whether
    // its owner bought here (G3).
    public static async Task OfferConfirmationAsync(
        IEmailOwnershipConfirmationPage confirmationPage,
        INotifier notifier,
        IHtmlLocalizer htmlLocalizer,
        IUser user,
        ILogger logger)
    {
        if (user is not User { Email: { Length: > 0 } email } account ||
            UserEmailOwnership.IsVerified(account.Properties, email) ||
            confirmationPage is null)
        {
            return;
        }

        try
        {
            if (await confirmationPage.GetUrlAsync(account) is not { } url)
            {
                return;
            }

            var H = htmlLocalizer;

            await notifier.InformationAsync(H["If you placed orders as a guest with {0}, <a href=\"{1}\">confirm it's you</a> to add them to your account.", email, url]);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not tell a user where to confirm their email so the guest orders placed with it join their account.");
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

    public override async Task LoggedInAsync(IUser user)
    {
        await CustomerMerge.RunAsync(_serviceProvider, user, _logger);
        await CustomerMerge.OfferConfirmationAsync(
            _serviceProvider.GetService<IEmailOwnershipConfirmationPage>(),
            _serviceProvider.GetRequiredService<INotifier>(),
            _serviceProvider.GetRequiredService<IHtmlLocalizer<CustomerMergeLoginFormEvent>>(),
            user,
            _logger);
    }
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

    // Following the confirmation link is the round trip that proves the user controls the mailbox. The Users building
    // block's EmailOwnershipConfirmationHandler, registered before this one, records it; the merge then sees it.
    public override Task ConfirmedAsync(UserConfirmContext context)
        => CustomerMerge.RunAsync(_serviceProvider, context.User, _logger);
}
