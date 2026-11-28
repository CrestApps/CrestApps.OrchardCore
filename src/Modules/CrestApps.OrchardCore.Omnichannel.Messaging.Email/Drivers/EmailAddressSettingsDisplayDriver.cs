using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Transports;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Email.Services;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Drivers;

/// <summary>
/// Adds the email settings to an email address in the address list: how it sends (Orchard Core's email service or its
/// own SMTP server), the sender name, signature and default subject, and how its mail reaches the workspace (a provider
/// webhook or its mailbox over IMAP).
/// </summary>
internal sealed class EmailAddressSettingsDisplayDriver : DisplayDriver<OmnichannelChannelEndpoint>
{
    private readonly IEnumerable<IEmailTransport> _transports;
    private readonly IOptionsMonitor<EmailProviderOptions> _providerOptions;
    private readonly IEmailSecretProtector _secretProtector;
    private readonly IEmailWebhookUrls _webhookUrls;
    private readonly ICatalog<EmailMailboxSyncState> _syncStates;
    private readonly IEmailSendingGovernor _governor;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailAddressSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="transports">The registered sending transports.</param>
    /// <param name="providerOptions">Orchard Core's email providers.</param>
    /// <param name="secretProtector">The protector passwords are stored with.</param>
    /// <param name="webhookUrls">The builder of the inbound webhook URLs.</param>
    /// <param name="syncStates">The mailbox read states, shown on the card.</param>
    /// <param name="governor">The sending governor, which reports the address's sending health.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EmailAddressSettingsDisplayDriver(
        IEnumerable<IEmailTransport> transports,
        IOptionsMonitor<EmailProviderOptions> providerOptions,
        IEmailSecretProtector secretProtector,
        IEmailWebhookUrls webhookUrls,
        ICatalog<EmailMailboxSyncState> syncStates,
        IEmailSendingGovernor governor,
        IClock clock,
        IStringLocalizer<EmailAddressSettingsDisplayDriver> stringLocalizer)
    {
        _transports = transports;
        _providerOptions = providerOptions;
        _secretProtector = secretProtector;
        _webhookUrls = webhookUrls;
        _syncStates = syncStates;
        _governor = governor;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(OmnichannelChannelEndpoint endpoint, BuildEditorContext context)
    {
        if (!IsEmailAddress(endpoint))
        {
            return null;
        }

        return Initialize<EmailAddressSettingsViewModel>("EmailAddressSettings_Edit", async model =>
        {
            var settings = endpoint.TryGet<EmailAddressSettings>(out var stored) ? stored : new EmailAddressSettings();
            var smtp = settings.Smtp ?? new EmailServerSettings();
            var mailbox = settings.Mailbox ?? new EmailMailboxSettings();
            var imap = mailbox.Server ?? new EmailServerSettings();

            model.AddressId = endpoint.ItemId;
            model.SenderName = settings.SenderName;
            model.TransportName = string.IsNullOrEmpty(settings.TransportName) ? EmailChannelConstants.Transports.OrchardCore : settings.TransportName;
            model.ProviderName = settings.ProviderName;
            model.SmtpHost = smtp.Host;
            model.SmtpPort = smtp.Port;
            model.SmtpSecurity = smtp.Security;
            model.SmtpUserName = smtp.UserName;
            model.HasSmtpPassword = !string.IsNullOrEmpty(smtp.Password);
            model.DefaultSubject = settings.DefaultSubject;
            model.Signature = settings.Signature;
            model.IncludeUnsubscribeLink = settings.IncludeUnsubscribeLink;
            model.InboundMode = settings.InboundMode;
            model.ImapHost = imap.Host;
            model.ImapPort = imap.Port;
            model.ImapSecurity = imap.Security;
            model.ImapUserName = imap.UserName;
            model.HasImapPassword = !string.IsNullOrEmpty(imap.Password);
            model.MailboxFolder = string.IsNullOrWhiteSpace(mailbox.Folder) ? EmailMailboxSettings.DefaultFolder : mailbox.Folder;
            model.AfterProcessing = mailbox.AfterProcessing;
            model.ProcessedFolder = mailbox.ProcessedFolder;
            model.InitialLookbackDays = mailbox.InitialLookbackDays;
            model.TrustedAuthenticationServerId = mailbox.TrustedAuthenticationServerId;
            var limits = settings.Limits ?? new EmailSendingLimits();

            model.MaxPerHour = limits.MaxPerHour;
            model.MaxPerDay = limits.MaxPerDay;
            model.MaxPerHourPerDomain = limits.MaxPerHourPerDomain;
            model.MinimumSecondsBetweenSends = limits.MinimumSecondsBetweenSends;
            model.WarmUp = limits.WarmUp;
            model.WarmUpFirstDayLimit = limits.WarmUpFirstDayLimit;
            model.WarmUpStartedUtc = limits.WarmUpStartedUtc;
            model.PauseOnPoorHealth = limits.PauseOnPoorHealth;
            model.WebhookUrls = await _webhookUrls.GetAllAsync();

            // A new address has sent nothing; its health shows once it exists.
            if (!string.IsNullOrEmpty(endpoint.ItemId))
            {
                model.Health = await _governor.GetHealthAsync(endpoint, settings);
            }

            if (!string.IsNullOrEmpty(endpoint.ItemId) && await _syncStates.FindByIdAsync(endpoint.ItemId) is { } state)
            {
                model.MailboxLastSucceededUtc = state.LastSucceededUtc;
                model.MailboxLastError = state.ConsecutiveFailures > 0 ? state.LastError : null;
            }

            model.Transports = _transports
                .Select(transport => new SelectListItem(transport.DisplayName.Value, transport.Name))
                .ToArray();

            model.Providers = _providerOptions.CurrentValue.Providers
                .Where(entry => entry.Value.IsEnabled)
                .Select(entry => new SelectListItem(entry.Key, entry.Key))
                .OrderBy(item => item.Text)
                .ToArray();
        }).Location("Content:1%Email;3");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(OmnichannelChannelEndpoint endpoint, UpdateEditorContext context)
    {
        if (!IsEmailAddress(endpoint))
        {
            return Edit(endpoint, context);
        }

        var model = new EmailAddressSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var settings = endpoint.TryGet<EmailAddressSettings>(out var stored) ? stored : new EmailAddressSettings();

        settings.SenderName = model.SenderName?.Trim();
        settings.TransportName = model.TransportName?.Trim();
        settings.ProviderName = model.ProviderName?.Trim();
        settings.DefaultSubject = model.DefaultSubject?.Trim();
        settings.Signature = model.Signature?.Trim();
        settings.IncludeUnsubscribeLink = model.IncludeUnsubscribeLink;
        settings.InboundMode = model.InboundMode;

        settings.Smtp = Update(
            settings.Smtp,
            model.SmtpHost,
            model.SmtpPort,
            model.SmtpSecurity,
            model.SmtpUserName,
            model.SmtpPassword,
            EmailServerConnection.SmtpSubmissionPort,
            EmailServerConnection.SmtpImplicitTlsPort,
            out var smtpPasswordDropped);

        // A dropped password is asked for again only where the address uses it, so the person knows to type it; a
        // server the address does not use simply loses it.
        if (smtpPasswordDropped && string.Equals(settings.TransportName, EmailChannelConstants.Transports.Smtp, StringComparison.OrdinalIgnoreCase))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SmtpPassword), S["Type the SMTP password again. A stored password is only kept for the server, port, security and user name it was saved with."]);
        }

        settings.Mailbox ??= new EmailMailboxSettings();
        settings.Mailbox.Server = Update(
            settings.Mailbox.Server,
            model.ImapHost,
            model.ImapPort,
            model.ImapSecurity,
            model.ImapUserName,
            model.ImapPassword,
            EmailServerConnection.ImapPort,
            EmailServerConnection.ImapImplicitTlsPort,
            out var imapPasswordDropped);

        if (imapPasswordDropped && settings.InboundMode == EmailInboundMode.Mailbox)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ImapPassword), S["Type the IMAP password again. A stored password is only kept for the server, port, security and user name it was saved with."]);
        }
        settings.Mailbox.Folder = string.IsNullOrWhiteSpace(model.MailboxFolder) ? EmailMailboxSettings.DefaultFolder : model.MailboxFolder.Trim();
        settings.Mailbox.AfterProcessing = model.AfterProcessing;
        settings.Mailbox.ProcessedFolder = model.ProcessedFolder?.Trim();
        settings.Mailbox.InitialLookbackDays = Math.Clamp(model.InitialLookbackDays, 0, 30);
        settings.Mailbox.TrustedAuthenticationServerId = string.IsNullOrWhiteSpace(model.TrustedAuthenticationServerId) ? null : model.TrustedAuthenticationServerId.Trim();

        if (settings.Mailbox.TrustedAuthenticationServerId is { } serverId && serverId.Any(character => char.IsWhiteSpace(character) || character is ';' or '=' or '(' or ')'))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TrustedAuthenticationServerId), S["Enter only the server name, such as mx.google.com: the first word of the mail host's Authentication-Results header."]);
        }

        settings.Limits ??= new EmailSendingLimits();

        var limitsChanged =
            settings.Limits.MaxPerHour != model.MaxPerHour ||
            settings.Limits.MaxPerDay != model.MaxPerDay ||
            settings.Limits.MaxPerHourPerDomain != model.MaxPerHourPerDomain ||
            settings.Limits.MinimumSecondsBetweenSends != model.MinimumSecondsBetweenSends ||
            settings.Limits.WarmUp != model.WarmUp ||
            settings.Limits.WarmUpFirstDayLimit != model.WarmUpFirstDayLimit;

        settings.Limits.MaxPerHour = model.MaxPerHour;
        settings.Limits.MaxPerDay = model.MaxPerDay;
        settings.Limits.MaxPerHourPerDomain = model.MaxPerHourPerDomain;
        settings.Limits.MinimumSecondsBetweenSends = model.MinimumSecondsBetweenSends;
        settings.Limits.WarmUpFirstDayLimit = model.WarmUpFirstDayLimit;
        settings.Limits.PauseOnPoorHealth = model.PauseOnPoorHealth;

        // The warm-up counts its days from when it was turned on; turning it off and on again starts it over. One
        // turned on without a date (by a recipe or an import) is dated on this save, so it starts to ramp at last.
        if (model.WarmUp && (!settings.Limits.WarmUp || settings.Limits.WarmUpStartedUtc is null))
        {
            settings.Limits.WarmUpStartedUtc = _clock.UtcNow;
        }
        else if (!model.WarmUp)
        {
            settings.Limits.WarmUpStartedUtc = null;
        }

        settings.Limits.WarmUp = model.WarmUp;

        if (model.ResumeBulkSending && !string.IsNullOrEmpty(endpoint.ItemId))
        {
            await _governor.ResumeAsync(endpoint.ItemId);
        }

        // Mail held under the old limits, or until the pause would have ended, keeps the turn it was given. Once the address
        // is saved, its held mail asks for new turns, spaced by the limits now in force.
        if ((limitsChanged || model.ResumeBulkSending) && !string.IsNullOrEmpty(endpoint.ItemId) && !string.IsNullOrEmpty(endpoint.Value))
        {
            var addressId = endpoint.ItemId;
            var serviceAddress = endpoint.Value;
            var modelState = context.Updater.ModelState;

            ShellScope.AddDeferredTask(async scope =>
            {
                // Read once every driver has run: a save any of them refused changed no limits.
                if (!modelState.IsValid)
                {
                    return;
                }

                await scope.ServiceProvider.GetRequiredService<IEmailSendingGovernor>().ResetTurnsAsync(addressId);
                if (scope.ServiceProvider.GetService<IMessagingOutbox>() is { } outbox)
                {
                    await outbox.BringForwardAsync(EmailChannelConstants.ChannelName, serviceAddress);
                }
            });
        }

        // Whether the settings can work is checked by EmailAddressSettingsRule, which a recipe import runs too.
        endpoint.Put(settings);

        return Edit(endpoint, context);
    }

    // A password left empty keeps the one stored, so the form never has to show it back, but only while the server,
    // port, security and user name stay the ones it was saved with. Otherwise anybody who may edit the address could
    // point it at a server of their own and have the site send them the password the next time it connects.
    private EmailServerSettings Update(
        EmailServerSettings server,
        string host,
        int port,
        EmailConnectionSecurity security,
        string userName,
        string password,
        int defaultPort,
        int implicitTlsPort,
        out bool passwordDropped)
    {
        server ??= new EmailServerSettings();
        passwordDropped = false;

        var saved = new EmailServerSettings
        {
            Host = server.Host,
            Port = server.Port,
            Security = server.Security,
            UserName = server.UserName,
        };

        server.Host = host?.Trim();
        server.Port = Math.Clamp(port, 0, 65535);
        server.Security = security;
        server.UserName = userName?.Trim();

        if (!string.IsNullOrEmpty(password))
        {
            server.Password = EmailServerConnection.ProtectPassword(_secretProtector, server, defaultPort, implicitTlsPort, password);
        }
        else if (!string.IsNullOrEmpty(server.Password) && !EmailServerConnection.IsSameAccount(saved, server, defaultPort, implicitTlsPort))
        {
            server.Password = null;
            passwordDropped = !string.IsNullOrEmpty(server.UserName);
        }

        if (string.IsNullOrEmpty(server.UserName))
        {
            server.Password = null;
        }

        return server;
    }

    // Shown on every email address and kept visible by the editor only while the address is used for email.
    private static bool IsEmailAddress(OmnichannelChannelEndpoint endpoint)
        => string.Equals(endpoint.GetAddressType(), OmnichannelAddressTypes.EmailAddress, StringComparison.OrdinalIgnoreCase);
}
