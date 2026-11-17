using Microsoft.Extensions.Logging;
using OrchardCore.Users;
using OrchardCore.Users.Events;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Customers.Handlers;

/// <summary>
/// Tells a new registrant when guest orders placed with their address are waiting for their account, and where to
/// confirm the address, which is what adds them. Registering doesn't prove the address, so nothing merges here.
/// </summary>
public sealed class CustomerMergeRegistrationFormEvent : RegistrationFormEventsBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public CustomerMergeRegistrationFormEvent(IServiceProvider serviceProvider, ILogger<CustomerMergeRegistrationFormEvent> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// On a site that requires confirmed addresses the registrant isn't signed in until they follow Orchard Core's link,
    /// so they are told at their first sign-in instead.
    /// </remarks>
    public override Task RegisteredAsync(IUser user)
        => user is User { EmailConfirmed: false }
            ? Task.CompletedTask
            : CustomerMerge.OfferConfirmationAsync(_serviceProvider, user, _logger);
}
