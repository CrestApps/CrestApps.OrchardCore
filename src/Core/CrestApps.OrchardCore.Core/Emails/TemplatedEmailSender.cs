using CrestApps.Core.Support;
using CrestApps.OrchardCore.Emails;
using Microsoft.Extensions.Logging;
using OrchardCore.Email;

namespace CrestApps.OrchardCore.Core.Emails;

/// <summary>
/// The default <see cref="ITemplatedEmailSender"/>: builds the email in its culture, passes an email with a kind through
/// <see cref="ITemplatedEmailTemplates"/>, and sends it through Orchard Core's <see cref="IEmailService"/> when the
/// Email feature is on.
/// </summary>
public sealed class TemplatedEmailSender : ITemplatedEmailSender
{
    private readonly IEmailService _emailService;
    private readonly ITemplatedEmailTemplates _emailTemplates;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplatedEmailSender"/> class.
    /// </summary>
    /// <param name="emailServices">The email service, when the Email feature is on; none otherwise.</param>
    /// <param name="emailTemplates">The site's templates of its emails.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public TemplatedEmailSender(
        IEnumerable<IEmailService> emailServices,
        ITemplatedEmailTemplates emailTemplates,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _emailService = emailServices?.FirstOrDefault();
        _emailTemplates = emailTemplates;
        _logger = loggerFactory.CreateLogger<TemplatedEmailSender>();
    }

    /// <inheritdoc/>
    public bool CanSend
        => _emailService is not null;

    /// <inheritdoc/>
    public async Task<bool> SendAsync(TemplatedEmail email, Func<CancellationToken, Task<TemplatedEmailContent>> buildAsync, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(buildAsync);

        if (string.IsNullOrWhiteSpace(email.To) || _emailService is not { } emailService)
        {
            return false;
        }

        using var language = TemplatedEmailCulture.Use(email.Culture);

        var content = await buildAsync(cancellationToken);

        if (content is null)
        {
            return false;
        }

        var message = string.IsNullOrEmpty(email.Kind)
            ? new TemplatedEmailMessage(content.Subject, content.Text, content.Html)
            : await _emailTemplates.ApplyAsync(new TemplatedEmailContext
            {
                Kind = email.Kind,
                Models = email.Models ?? new Dictionary<string, object>(StringComparer.Ordinal),
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
