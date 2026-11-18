using CrestApps.Core.Support;
using CrestApps.OrchardCore.Commerce.Emails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Email;

namespace CrestApps.OrchardCore.Commerce.Core.Emails;

/// <summary>
/// The default <see cref="ICommerceEmailSender"/>. It sends through the site's email service, when the Email feature is
/// on, and through <see cref="ICommerceEmailTemplates"/>, the store's templates of its emails. Copies and attachments go
/// with the email whether or not a template applies; the attachments' streams are opened just before the send and
/// disposed after it.
/// </summary>
public sealed class CommerceEmailSender : ICommerceEmailSender
{
    // The separator Orchard Core's email providers split a list of addresses on.
    private const string AddressSeparator = ",";

    private readonly IServiceProvider _serviceProvider;
    private readonly ICommerceEmailTemplates _emailTemplates;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommerceEmailSender"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider, for the email service, which is there only when the Email
    /// feature is on.</param>
    /// <param name="emailTemplates">The store's templates of its emails.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The localizer of the reasons an email wasn't sent.</param>
    public CommerceEmailSender(
        IServiceProvider serviceProvider,
        ICommerceEmailTemplates emailTemplates,
        ILogger<CommerceEmailSender> logger,
        IStringLocalizer<CommerceEmailSender> stringLocalizer)
    {
        _serviceProvider = serviceProvider;
        _emailTemplates = emailTemplates;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public bool CanSend
        => _serviceProvider.GetService<IEmailService>() is not null;

    /// <inheritdoc />
    public async Task<bool> SendAsync(CommerceEmail email, Func<CancellationToken, Task<CommerceEmailContent>> buildAsync, CancellationToken cancellationToken = default)
        => (await SendWithResultAsync(email, buildAsync, cancellationToken)).Succeeded;

    /// <inheritdoc />
    public async Task<CommerceEmailResult> SendWithResultAsync(CommerceEmail email, Func<CancellationToken, Task<CommerceEmailContent>> buildAsync, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(buildAsync);

        if (_serviceProvider.GetService<IEmailService>() is not { } emailService)
        {
            return CommerceEmailResult.Failed(S["The site can't send email: the Email feature is off."]);
        }

        if (string.IsNullOrWhiteSpace(email.To))
        {
            return CommerceEmailResult.Failed(S["The email has no recipient."]);
        }

        using var language = CommerceEmailCulture.Use(email.Culture);

        var content = await buildAsync(cancellationToken);

        if (content is null)
        {
            return CommerceEmailResult.Failed(S["There was nothing to send."]);
        }

        var attachments = content.Attachments ?? [];

        foreach (var attachment in attachments)
        {
            if (!IsValidFileName(attachment?.FileName) || attachment.OpenAsync is null)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("The email about '{Reference}' has an attachment with no file, or with a file name that isn't a name only, so it wasn't sent.", email.Reference.SanitizeLogValue());
                }

                return CommerceEmailResult.Failed(S["The attachment '{0}' has no file, or its file name isn't a name only.", attachment?.FileName ?? string.Empty]);
            }
        }

        var message = string.IsNullOrEmpty(email.Kind)
            ? new CommerceEmailMessage(content.Subject, content.Text, content.Html)
            : await _emailTemplates.ApplyAsync(new CommerceEmailContext
            {
                Kind = email.Kind,
                Models = email.Models ?? new Dictionary<string, object>(StringComparer.Ordinal),
                Subject = content.Subject,
                Text = content.Text,
                Html = content.Html,
                Values = content.Values ?? new Dictionary<string, string>(),
            }, cancellationToken);

        var mailMessage = new MailMessage
        {
            To = email.To,
            Cc = JoinAddresses(email.Cc),
            Bcc = JoinAddresses(email.Bcc),
            Subject = message.Subject,
            TextBody = message.Text,
            HtmlBody = message.Html,
            Attachments = [],
        };

        // Every stream opened is disposed, whether the send succeeds, fails or throws.
        var opened = new List<Stream>(attachments.Count);

        try
        {
            foreach (var attachment in attachments)
            {
                var stream = await attachment.OpenAsync(cancellationToken);

                if (stream is null)
                {
                    if (_logger.IsEnabled(LogLevel.Warning))
                    {
                        _logger.LogWarning("The email about '{Reference}' has an attachment that opened no file, so it wasn't sent.", email.Reference.SanitizeLogValue());
                    }

                    return CommerceEmailResult.Failed(S["The attachment '{0}' couldn't be opened.", attachment.FileName]);
                }

                opened.Add(stream);
                mailMessage.Attachments.Add(new MailMessageAttachment
                {
                    Filename = attachment.FileName.Trim(),
                    Stream = stream,
                });
            }

            var result = await emailService.SendAsync(mailMessage, cancellationToken: cancellationToken);

            if (result.Succeeded)
            {
                return CommerceEmailResult.Success();
            }

            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("The email about '{Reference}' ({Kind}) couldn't be sent.", email.Reference.SanitizeLogValue(), email.Kind);
            }

            var errors = string.Join(" ", (result.Errors ?? []).Select(error => $"{error.Message}").Where(error => !string.IsNullOrWhiteSpace(error)));

            return CommerceEmailResult.Failed(string.IsNullOrEmpty(errors) ? S["The email provider refused the email."] : errors);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A mail server that's down never undoes what the email is about.
            _logger.LogError(exception, "The email about '{Reference}' ({Kind}) failed.", email.Reference.SanitizeLogValue(), email.Kind);

            return CommerceEmailResult.Failed(exception.Message);
        }
        finally
        {
            foreach (var stream in opened)
            {
                await stream.DisposeAsync();
            }
        }
    }

    // A file name is a name only: no folder, no drive and no characters a file name can't hold.
    internal static bool IsValidFileName(string fileName)
    {
        var name = fileName?.Trim();

        return !string.IsNullOrEmpty(name) &&
            name is not "." and not ".." &&
            name.IndexOfAny(['/', '\\', ':']) < 0 &&
            name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    private static string JoinAddresses(IReadOnlyList<string> addresses)
    {
        var values = (addresses ?? [])
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(address => address.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return values.Length == 0 ? null : string.Join(AddressSeparator, values);
    }
}
