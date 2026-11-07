using CrestApps.OrchardCore.Outreach.Models;
using OrchardCore.Email;

namespace CrestApps.OrchardCore.Outreach.Core.Services;

/// <summary>
/// Sends outreach messages as e-mail through Orchard's e-mail service, with a plain-text part for HTML messages.
/// </summary>
public sealed class EmailOutreachChannelSender : IOutreachChannelSender
{
    private readonly IEmailService _emailService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailOutreachChannelSender"/> class.
    /// </summary>
    /// <param name="emailService">The e-mail service.</param>
    public EmailOutreachChannelSender(IEmailService emailService)
    {
        _emailService = emailService;
    }

    /// <inheritdoc/>
    public string Name
        => OutreachConstants.EmailSender;

    /// <inheritdoc/>
    public bool ObservesQuietHours(OutreachRun run)
        => false;

    /// <inheritdoc/>
    public async Task<OutreachSendResult> SendAsync(OutreachRun run, OutreachRecipient recipient, OutreachContent content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(content);

        var message = new MailMessage
        {
            To = recipient.Address,
            Subject = content.Subject,
        };

        if (!string.IsNullOrWhiteSpace(run.SenderAddress))
        {
            message.From = run.SenderAddress;
        }

        if (content.IsHtml)
        {
            message.HtmlBody = content.Body;
            message.TextBody = OutreachText.FromHtml(content.Body);
        }
        else
        {
            message.TextBody = content.Body;
        }

        var result = await _emailService.SendAsync(message, cancellationToken: cancellationToken);

        return result.Succeeded
            ? OutreachSendResult.Success()
            : OutreachSendResult.Failure("The e-mail service did not accept the message.");
    }
}
