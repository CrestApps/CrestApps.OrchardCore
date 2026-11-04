using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Email;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Customers.Core.Services;

/// <summary>
/// The default <see cref="ICustomerOwnerNotifier"/>. The notification and email services belong to features a site may
/// leave off, so they are resolved when a message is sent, and a buyer they would reach is not reached without them. A
/// message with a kind passes through the store's templates of its emails (<see cref="ICommerceEmailTemplates"/>) first.
/// </summary>
public sealed class DefaultCustomerOwnerNotifier : ICustomerOwnerNotifier
{
    private readonly IUserService _userService;
    private readonly ICustomerContactResolver _contactResolver;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCustomerOwnerNotifier"/> class.
    /// </summary>
    /// <param name="userService">The user service that finds a buyer's account.</param>
    /// <param name="contactResolver">The resolver that finds a guest's email.</param>
    /// <param name="serviceProvider">The service provider the optional notification and email services are resolved from.</param>
    /// <param name="logger">The logger.</param>
    public DefaultCustomerOwnerNotifier(
        IUserService userService,
        ICustomerContactResolver contactResolver,
        IServiceProvider serviceProvider,
        ILogger<DefaultCustomerOwnerNotifier> logger)
    {
        _userService = userService;
        _contactResolver = contactResolver;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> NotifyAsync(CustomerOwner owner, ICustomerContact guestContact, CustomerOwnerMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(message);

        message = await ApplyTemplateAsync(message, cancellationToken);

        return owner.Kind == CustomerOwnerKind.Guest
            ? await EmailGuestAsync(owner, guestContact, message, cancellationToken)
            : await NotifyUserAsync(owner, message, cancellationToken);
    }

    // The store's template of the message's kind, when it wrote one; the built message otherwise.
    private async Task<CustomerOwnerMessage> ApplyTemplateAsync(CustomerOwnerMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(message.Kind) || _serviceProvider.GetService<ICommerceEmailTemplates>() is not { } templates)
        {
            return message;
        }

        var applied = await templates.ApplyAsync(new CommerceEmailContext
        {
            Kind = message.Kind,
            Models = message.Models ?? new Dictionary<string, object>(StringComparer.Ordinal),
            Subject = message.Subject,
            Text = message.TextBody,
            Html = message.HtmlBody,
            Values = message.Values ?? new Dictionary<string, string>(StringComparer.Ordinal),
        }, cancellationToken);

        return new CustomerOwnerMessage
        {
            Kind = message.Kind,
            Models = message.Models,
            Values = message.Values,
            Subject = applied.Subject,
            Summary = message.Summary,
            TextBody = applied.Text,
            HtmlBody = applied.Html,
        };
    }

    private async Task<bool> NotifyUserAsync(CustomerOwner owner, CustomerOwnerMessage message, CancellationToken cancellationToken)
    {
        if (_serviceProvider.GetService<INotificationService>() is not { } notificationService)
        {
            _logger.LogWarning("A message for the user '{OwnerId}' was not sent because notifications are not enabled.", owner.Id);

            return false;
        }

        var user = string.IsNullOrEmpty(owner.Id) ? null : await _userService.GetUserByUniqueIdAsync(owner.Id);

        if (user is null)
        {
            _logger.LogWarning("A message for the user '{OwnerId}' was not sent because the user was not found.", owner.Id);

            return false;
        }

        var result = await notificationService.SendAsync(
            user,
            new NotificationMessage
            {
                Subject = message.Subject,
                Summary = string.IsNullOrEmpty(message.Summary) ? message.TextBody : message.Summary,
                TextBody = message.TextBody,
                HtmlBody = message.HtmlBody,
                IsHtmlPreferred = message.HtmlBody is not null,
            },
            cancellationToken);

        return result.SuccessfulCount > 0;
    }

    private async Task<bool> EmailGuestAsync(CustomerOwner owner, ICustomerContact guestContact, CustomerOwnerMessage message, CancellationToken cancellationToken)
    {
        var contact = await _contactResolver.ResolveAsync(owner, guestContact, cancellationToken);

        if (string.IsNullOrEmpty(contact?.Email))
        {
            _logger.LogWarning("A message for the guest '{OwnerId}' was not sent because they have no contact email.", owner.Id);

            return false;
        }

        if (_serviceProvider.GetService<IEmailService>() is not { } emailService)
        {
            _logger.LogWarning("A message for the guest '{OwnerId}' was not sent because no email service is registered.", owner.Id);

            return false;
        }

        var result = await emailService.SendAsync(
            new MailMessage
            {
                To = contact.Email,
                Subject = message.Subject,
                TextBody = message.TextBody,
                HtmlBody = message.HtmlBody,
            },
            cancellationToken: cancellationToken);

        if (!result.Succeeded)
        {
            _logger.LogWarning("A message for the guest '{OwnerId}' could not be delivered by email.", owner.Id);
        }

        return result.Succeeded;
    }
}
