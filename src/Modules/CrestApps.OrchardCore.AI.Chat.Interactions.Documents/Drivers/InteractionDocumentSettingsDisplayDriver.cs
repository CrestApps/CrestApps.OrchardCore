using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;
using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Indexing;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Drivers;

public sealed class InteractionDocumentSettingsDisplayDriver : SiteDisplayDriver<InteractionDocumentSettings>
{
    public const string GroupId = "interaction-documents";

    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    protected override string SettingsGroupId => GroupId;

    public InteractionDocumentSettingsDisplayDriver(
        IIndexProfileStore indexProfileStore,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<InteractionDocumentSettingsDisplayDriver> stringLocalizer)
    {
        _indexProfileStore = indexProfileStore;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ISite model, InteractionDocumentSettings section, BuildEditorContext context)
    {
        return Initialize<InteractionDocumentSettingsViewModel>("InteractionDocumentSettings_Edit", async viewModel =>
        {
            viewModel.IndexProfileName = section.IndexProfileName;

            var items = await _indexProfileStore.GetByTypeAsync(ChatInteractionsConstants.IndexingTaskType);

            // Here you would typically populate the IndexProfiles from your data source.
            viewModel.IndexProfiles = items.Select(x => new SelectListItem(x.Name, x.Name));

        }).Location("Content:5")
        .OnGroup(GroupId)
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

        if (string.IsNullOrEmpty(model.IndexProfileName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.IndexProfileName), S["The Index profile field is required."]);
        }
        else
        {
            var indexProfile = await _indexProfileStore.FindByNameAsync(model.IndexProfileName);

            if (indexProfile == null || indexProfile.Type != ChatInteractionsConstants.IndexingTaskType)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.IndexProfileName), S["Invalid index profile."]);
            }
        }

        settings.IndexProfileName = model.IndexProfileName;

        return Edit(site, settings, context);
    }
}
