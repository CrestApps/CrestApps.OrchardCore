using CrestApps.Core.AI.Documents.Models;
using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Indexing;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Documents.Drivers;

/// <summary>
/// Display driver for the interaction document settings shape.
/// </summary>
public sealed class InteractionDocumentSettingsDisplayDriver : SiteDisplayDriver<InteractionDocumentSettings>
{
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShellReleaseManager _shellReleaseManager;

    internal readonly IStringLocalizer S;

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionDocumentSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="indexProfileStore">The index profile store.</param>
    /// <param name="httpContextAccessor">The http context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="shellReleaseManager">The shell release manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public InteractionDocumentSettingsDisplayDriver(
        IIndexProfileStore indexProfileStore,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IShellReleaseManager shellReleaseManager,
        IStringLocalizer<InteractionDocumentSettingsDisplayDriver> stringLocalizer)
    {
        _indexProfileStore = indexProfileStore;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _shellReleaseManager = shellReleaseManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ISite model, InteractionDocumentSettings section, BuildEditorContext context)
    {
        context.AddTenantReloadWarningWrapper();

        return Initialize<InteractionDocumentSettingsViewModel>("InteractionDocumentSettings_Edit", async viewModel =>
        {
            viewModel.IndexProfileName = section.IndexProfileName;

            var items = await _indexProfileStore.GetByTypeAsync(AIConstants.AIDocumentsIndexingTaskType);

            viewModel.IndexProfiles = items.Select(x => new SelectListItem(x.Name, x.Name));
        }).Location("Content:5%Documents;1")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageChatInteractionSettings));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, InteractionDocumentSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageChatInteractionSettings))
        {
            return null;
        }

        var model = new InteractionDocumentSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var indexProfileName = string.IsNullOrWhiteSpace(model.IndexProfileName)
            ? null
            : model.IndexProfileName;
        var settingsChanged = !string.Equals(settings.IndexProfileName, indexProfileName, StringComparison.Ordinal);

        settings.IndexProfileName = indexProfileName;

        if (!string.IsNullOrWhiteSpace(settings.IndexProfileName))
        {
            var indexProfile = await _indexProfileStore.FindByNameAsync(settings.IndexProfileName);

            if (indexProfile is null || !string.Equals(indexProfile.Type, AIConstants.AIDocumentsIndexingTaskType, StringComparison.OrdinalIgnoreCase))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.IndexProfileName), S["Invalid index profile."]);
            }
        }

        if (settingsChanged && context.Updater.ModelState.IsValid)
        {
            _shellReleaseManager.RequestRelease();
        }

        return Edit(site, settings, context);
    }
}
