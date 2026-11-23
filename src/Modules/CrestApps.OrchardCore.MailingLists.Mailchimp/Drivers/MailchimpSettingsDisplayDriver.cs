using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Models;
using CrestApps.OrchardCore.MailingLists.Mailchimp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Secrets;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Drivers;

/// <summary>
/// The Mailchimp card of <b>Settings &gt; Mailing Lists</b>: which Orchard Core secret holds the API key, and the
/// server prefix. The key itself is never on this page.
/// </summary>
internal sealed class MailchimpSettingsDisplayDriver : SiteDisplayDriver<MailchimpSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISecretManager _secretManager;

    internal readonly IStringLocalizer S;

    public MailchimpSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        ISecretManager secretManager,
        IStringLocalizer<MailchimpSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _secretManager = secretManager;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => MailingListsConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, MailchimpSettings settings, BuildEditorContext context)
        => Initialize<MailchimpSettingsViewModel>("MailchimpSettings_Edit", async model =>
            {
                // No secret is chosen until the admin picks one, so saving the page for another card never turns
                // Mailchimp on with a secret that does not exist.
                model.SecretName = settings.SecretName;
                model.ServerPrefix = settings.ServerPrefix;
                model.Secrets = await TextSecretSelectList.BuildAsync(_secretManager, model.SecretName, S);
                model.IsConfigured = settings.IsConfigured();
            })
            .Location("Content:10")
            .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, MailingListsPermissions.ManageMailingListSettings))
            .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, MailchimpSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, MailingListsPermissions.ManageMailingListSettings))
        {
            return null;
        }

        var model = new MailchimpSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var serverPrefix = model.ServerPrefix?.Trim().ToLowerInvariant();

        if (!string.IsNullOrEmpty(serverPrefix) && !MailchimpCredentials.IsValidServerPrefix(serverPrefix))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.ServerPrefix), S["Enter the server prefix as Mailchimp shows it, such as us21, or leave it empty."]);
        }

        settings.SecretName = string.IsNullOrWhiteSpace(model.SecretName) ? null : model.SecretName.Trim();
        settings.ServerPrefix = string.IsNullOrEmpty(serverPrefix) ? null : serverPrefix;

        return Edit(site, settings, context);
    }
}
