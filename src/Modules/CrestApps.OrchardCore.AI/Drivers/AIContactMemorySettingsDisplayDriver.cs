using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Edits the instructions the AI follows to rewrite a contact's memory note, on the AI settings page.
/// </summary>
public sealed class AIContactMemorySettingsDisplayDriver : SiteDisplayDriver<AIContactMemorySettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    /// <inheritdoc/>
    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIContactMemorySettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public AIContactMemorySettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, AIContactMemorySettings settings, BuildEditorContext context)
    {
        return Initialize<AIContactMemorySettingsViewModel>("AIContactMemorySettings_Edit", model =>
        {
            model.Instructions = settings.Instructions;
            model.DefaultInstructions = AIContactMemoryService.SystemPrompt;
        }).Location("Content:8%Contact memory;8")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, AIContactMemorySettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles))
        {
            return null;
        }

        var model = new AIContactMemorySettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.Instructions = string.IsNullOrWhiteSpace(model.Instructions) ? null : model.Instructions.Trim();

        return Edit(site, settings, context);
    }
}
