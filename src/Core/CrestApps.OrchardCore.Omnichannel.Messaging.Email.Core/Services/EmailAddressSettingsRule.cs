using System.ComponentModel.DataAnnotations;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

/// <summary>
/// Refuses an email address whose settings cannot work: one that sends through its own SMTP server without a server,
/// reads its mailbox without an IMAP server, or moves received mail without naming the folder. It runs whether the
/// address is saved from the editor or imported by a recipe, so both are held to the same rules.
/// </summary>
public sealed class EmailAddressSettingsRule : IChannelEndpointRule
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailAddressSettingsRule"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EmailAddressSettingsRule(IStringLocalizer<EmailAddressSettingsRule> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public Task ValidateAsync(ValidatingContext<OmnichannelChannelEndpoint> context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var endpoint = context.Model;

        // The settings only matter while the address is used for email; an address that is not is never sent from.
        if (endpoint is null ||
            !endpoint.HasCapability(OmnichannelConstants.Channels.Email) ||
            !endpoint.TryGet<EmailAddressSettings>(out var settings))
        {
            return Task.CompletedTask;
        }

        if (string.Equals(settings.TransportName, EmailChannelConstants.Transports.Smtp, StringComparison.OrdinalIgnoreCase) &&
            settings.Smtp?.IsConfigured != true)
        {
            context.Result.Fail(new ValidationResult(S["Enter the SMTP server the address sends through."], ["SmtpHost"]));
        }

        if (settings.InboundMode == EmailInboundMode.Mailbox)
        {
            if (settings.Mailbox?.Server?.IsConfigured != true)
            {
                context.Result.Fail(new ValidationResult(S["Enter the IMAP server the address's mailbox is read from."], ["ImapHost"]));
            }

            if (settings.Mailbox?.AfterProcessing == EmailMailboxAfterProcessing.MoveToFolder &&
                string.IsNullOrWhiteSpace(settings.Mailbox.ProcessedFolder))
            {
                context.Result.Fail(new ValidationResult(S["Enter the folder received email is moved to."], ["ProcessedFolder"]));
            }
        }

        return Task.CompletedTask;
    }
}
