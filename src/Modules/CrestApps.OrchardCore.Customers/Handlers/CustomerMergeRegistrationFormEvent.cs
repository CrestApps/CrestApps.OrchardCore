using CrestApps.OrchardCore.Users.Core.Services;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Users;
using OrchardCore.Users.Events;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Customers.Handlers;

/// <summary>
/// Tells a new registrant that guest orders placed with their address join their account once they confirm it, and
/// where to confirm it. Registering doesn't prove the address, so nothing merges here, and whether the address has guest
/// orders isn't looked up, so the notice tells nobody whether its owner bought here.
/// </summary>
public sealed class CustomerMergeRegistrationFormEvent : RegistrationFormEventsBase
{
    private readonly IEmailOwnershipConfirmationPage _confirmationPage;
    private readonly INotifier _notifier;
    private readonly ILogger _logger;

    internal readonly IHtmlLocalizer H;

    public CustomerMergeRegistrationFormEvent(
        IEmailOwnershipConfirmationPage confirmationPage,
        INotifier notifier,
        ILogger<CustomerMergeRegistrationFormEvent> logger,
        IHtmlLocalizer<CustomerMergeLoginFormEvent> htmlLocalizer)
    {
        _confirmationPage = confirmationPage;
        _notifier = notifier;
        _logger = logger;
        H = htmlLocalizer;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// On a site that requires confirmed addresses the registrant isn't signed in until they follow Orchard Core's link,
    /// so they are told at their first sign-in instead.
    /// </remarks>
    public override Task RegisteredAsync(IUser user)
        => user is User { EmailConfirmed: false }
            ? Task.CompletedTask
            : CustomerMerge.OfferConfirmationAsync(_confirmationPage, _notifier, H, user, _logger);
}
