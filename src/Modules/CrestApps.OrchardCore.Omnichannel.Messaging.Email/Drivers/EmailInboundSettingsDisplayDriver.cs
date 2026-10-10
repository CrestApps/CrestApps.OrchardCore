using System.Security.Cryptography;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Drivers;

/// <summary>
/// Adds the inbound email webhook settings to Orchard Core's email settings page: the secret key every webhook call
/// carries (created on the first save), the Mailgun signing key and the Amazon SNS topics allowed to deliver mail.
/// </summary>
internal sealed class EmailInboundSettingsDisplayDriver : SiteDisplayDriver<EmailInboundSettings>
{
    // Orchard Core's email settings group, so the card shows under /Admin/Settings/email.
    private const string EmailSettingsGroupId = "email";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEmailSecretProtector _secretProtector;
    private readonly IEmailWebhookUrls _webhookUrls;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailInboundSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The accessor of the current request.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="secretProtector">The protector the keys are stored with.</param>
    /// <param name="webhookUrls">The builder of the webhook URLs shown on the card.</param>
    public EmailInboundSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IEmailSecretProtector secretProtector,
        IEmailWebhookUrls webhookUrls)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _secretProtector = secretProtector;
        _webhookUrls = webhookUrls;
    }

    /// <inheritdoc/>
    protected override string SettingsGroupId => EmailSettingsGroupId;

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, EmailInboundSettings settings, BuildEditorContext context)
    {
        return Initialize<EmailInboundSettingsViewModel>("EmailInboundSettings_Edit", async model =>
        {
            model.HasMailgunSigningKey = !string.IsNullOrEmpty(settings.MailgunSigningKey);
            model.AmazonSnsTopicArns = string.Join(Environment.NewLine, settings.AmazonSnsTopicArns ?? []);
            model.WebhookUrls = await _webhookUrls.GetAllAsync();
        }).Location("Content:10#Inbound email;10")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, OmnichannelConstants.Permissions.ManageChannelEndpoints));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, EmailInboundSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, OmnichannelConstants.Permissions.ManageChannelEndpoints))
        {
            return null;
        }

        var model = new EmailInboundSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // The key is created on the first save and replaced only when asked, because every provider's webhook address
        // carries it and replacing it stops them all until they are updated.
        if (model.RegenerateWebhookKey || string.IsNullOrEmpty(_secretProtector.Unprotect(settings.WebhookKey)))
        {
            settings.WebhookKey = _secretProtector.Protect(WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32)));
        }

        if (model.ClearMailgunSigningKey)
        {
            settings.MailgunSigningKey = null;
        }
        else if (!string.IsNullOrWhiteSpace(model.MailgunSigningKey))
        {
            settings.MailgunSigningKey = _secretProtector.Protect(model.MailgunSigningKey.Trim());
        }

        settings.AmazonSnsTopicArns = (model.AmazonSnsTopicArns ?? string.Empty)
            .Split(['\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return Edit(site, settings, context);
    }
}
