using CrestApps.Core.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Email;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// The default <see cref="ICommerceEmailSender"/>. It sends through the site's email service, when the Email feature is
/// on, and through <see cref="IOrderEmailTemplates"/>, the store's templates of its emails.
/// </summary>
public sealed class CommerceEmailSender : ICommerceEmailSender
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOrderEmailTemplates _emailTemplates;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommerceEmailSender"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider, for the email service, which is there only when the Email
    /// feature is on.</param>
    /// <param name="emailTemplates">The store's templates of its emails.</param>
    /// <param name="logger">The logger.</param>
    public CommerceEmailSender(
        IServiceProvider serviceProvider,
        IOrderEmailTemplates emailTemplates,
        ILogger<CommerceEmailSender> logger)
    {
        _serviceProvider = serviceProvider;
        _emailTemplates = emailTemplates;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool CanSend
        => _serviceProvider.GetService<IEmailService>() is not null;

    /// <inheritdoc />
    public async Task<bool> SendAsync(CommerceEmail email, Func<CancellationToken, Task<CommerceEmailContent>> buildAsync, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(buildAsync);

        if (string.IsNullOrWhiteSpace(email.To) || _serviceProvider.GetService<IEmailService>() is not { } emailService)
        {
            return false;
        }

        using var language = OrderCulture.Use(email.Culture);

        var content = await buildAsync(cancellationToken);

        if (content is null)
        {
            return false;
        }

        var message = string.IsNullOrEmpty(email.Kind)
            ? new OrderEmailMessage(content.Subject, content.Text, content.Html)
            : await _emailTemplates.ApplyAsync(new OrderEmailContext
            {
                Kind = email.Kind,
                Order = email.Order,
                Subject = content.Subject,
                Text = content.Text,
                Html = content.Html,
                Values = content.Values ?? new Dictionary<string, string>(),
            }, cancellationToken);

        try
        {
            var result = await emailService.SendAsync(new MailMessage
            {
                To = email.To,
                Subject = message.Subject,
                TextBody = message.Text,
                HtmlBody = message.Html,
            }, cancellationToken: cancellationToken);

            if (result.Succeeded)
            {
                return true;
            }

            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("The email about '{Reference}' ({Kind}) couldn't be sent.", email.Reference.SanitizeLogValue(), email.Kind);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A mail server that's down never undoes what the email is about.
            _logger.LogError(exception, "The email about '{Reference}' ({Kind}) failed.", email.Reference.SanitizeLogValue(), email.Kind);
        }

        return false;
    }
}
