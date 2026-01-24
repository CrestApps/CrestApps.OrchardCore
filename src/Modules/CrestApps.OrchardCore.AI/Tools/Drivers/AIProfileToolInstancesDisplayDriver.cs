using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Tools.Drivers;

internal sealed class AIProfileToolInstancesDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly ICatalog<AIToolInstance> _toolInstanceStore;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public AIProfileToolInstancesDisplayDriver(
        ICatalog<AIToolInstance> toolInstanceStore,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<AIProfileToolsDisplayDriver> stringLocalizer)
    {
        _toolInstanceStore = toolInstanceStore;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(AIProfile profile, BuildEditorContext context)
    {
        var instances = await _toolInstanceStore.GetAllAsync();

        if (instances.Count == 0)
        {
            return null;
        }

        // Filter instances based on user permissions
        var user = _httpContextAccessor.HttpContext.User;
        var accessibleInstances = new List<AIToolInstance>();

        foreach (var instance in instances)
        {
            // Check if user has access to this tool instance
            var authResult = await _authorizationService.AuthorizeAsync(user, AIPermissions.AccessAITool, instance.ItemId);
            if (authResult.Succeeded)
            {
                accessibleInstances.Add(instance);
            }
        }

        if (accessibleInstances.Count == 0)
        {
            return null;
        }

        return Initialize<EditProfileToolInstancesViewModel>("EditProfileToolInstances_Edit", model =>
        {
            var toolMetadata = profile.As<AIProfileFunctionInstancesMetadata>();

            model.Instances = accessibleInstances.Select(instance => new ToolEntry
            {
                ItemId = instance.ItemId,
                DisplayText = instance.DisplayText,
                Description = instance.As<InvokableToolMetadata>()?.Description,
                IsSelected = toolMetadata.InstanceIds?.Contains(instance.ItemId) ?? false,
            }).OrderBy(entry => entry.DisplayText)
            .ToArray();

        }).Location("Content:8Content:8.5#Capabilities:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var instances = await _toolInstanceStore.GetAllAsync();

        if (instances.Count == 0)
        {
            return null;
        }

        var model = new EditProfileToolInstancesViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var metadata = new AIProfileFunctionInstancesMetadata
        {
            InstanceIds = model.Instances?.Where(x => x.IsSelected).Select(x => x.ItemId).ToArray() ?? []
        };

        profile.Put(metadata);

        return Edit(profile, context);
    }
}
