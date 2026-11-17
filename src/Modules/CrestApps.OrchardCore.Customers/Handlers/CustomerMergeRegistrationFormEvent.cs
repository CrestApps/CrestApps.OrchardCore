using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Users.Core.Services;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.DisplayManagement.Notify;
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
    private readonly ICustomerStore _store;
    private readonly IEmailOwnershipConfirmationPage _confirmationPage;
    private readonly INotifier _notifier;
    private readonly ILogger _logger;

    internal readonly IHtmlLocalizer H;

    public CustomerMergeRegistrationFormEvent(
        ICustomerStore store,
        IEmailOwnershipConfirmationPage confirmationPage,
        INotifier notifier,
        ILogger<CustomerMergeRegistrationFormEvent> logger,
        IHtmlLocalizer<CustomerMergeLoginFormEvent> htmlLocalizer)
    {
        _store = store;
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
            : CustomerMerge.OfferConfirmationAsync(_store, _confirmationPage, _notifier, H, user, _logger);
}
