using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Transports;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Email.Services;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailAddressSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="transports">The registered sending transports.</param>
    /// <param name="providerOptions">Orchard Core's email providers.</param>
    /// <param name="secretProtector">The protector passwords are stored with.</param>
    /// <param name="webhookUrls">The builder of the inbound webhook URLs.</param>
    /// <param name="syncStates">The mailbox read states, shown on the card.</param>
    public EmailAddressSettingsDisplayDriver(
        IEnumerable<IEmailTransport> transports,
        IOptionsMonitor<EmailProviderOptions> providerOptions,
        IEmailSecretProtector secretProtector,
        IEmailWebhookUrls webhookUrls,
        ICatalog<EmailMailboxSyncState> syncStates)
    {
        _transports = transports;
        _providerOptions = providerOptions;
        _secretProtector = secretProtector;
        _webhookUrls = webhookUrls;
        _syncStates = syncStates;
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
            model.WebhookUrls = await _webhookUrls.GetAllAsync();

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

        settings.Smtp = Update(settings.Smtp, model.SmtpHost, model.SmtpPort, model.SmtpSecurity, model.SmtpUserName, model.SmtpPassword);

        settings.Mailbox ??= new EmailMailboxSettings();
        settings.Mailbox.Server = Update(settings.Mailbox.Server, model.ImapHost, model.ImapPort, model.ImapSecurity, model.ImapUserName, model.ImapPassword);
        settings.Mailbox.Folder = string.IsNullOrWhiteSpace(model.MailboxFolder) ? EmailMailboxSettings.DefaultFolder : model.MailboxFolder.Trim();
        settings.Mailbox.AfterProcessing = model.AfterProcessing;
        settings.Mailbox.ProcessedFolder = model.ProcessedFolder?.Trim();
        settings.Mailbox.InitialLookbackDays = Math.Clamp(model.InitialLookbackDays, 0, 30);

        // Whether the settings can work is checked by EmailAddressSettingsRule, which a recipe import runs too.
        endpoint.Put(settings);

        return Edit(endpoint, context);
    }

    // A password left empty keeps the one stored, so the form never has to show it back.
    private EmailServerSettings Update(EmailServerSettings server, string host, int port, EmailConnectionSecurity security, string userName, string password)
    {
        server ??= new EmailServerSettings();

        server.Host = host?.Trim();
        server.Port = Math.Clamp(port, 0, 65535);
        server.Security = security;
        server.UserName = userName?.Trim();

        if (!string.IsNullOrEmpty(password))
        {
            server.Password = _secretProtector.Protect(password);
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
