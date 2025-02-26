using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

public sealed class AIProfileDeploymentDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly AICompletionOptions _options;
    private readonly AIProviderOptions _providerOptions;

    internal readonly IStringLocalizer S;

    public AIProfileDeploymentDisplayDriver(
        IAIDeploymentManager deploymentManager,
        IOptions<AICompletionOptions> options,
        IOptions<AIProviderOptions> providerOptions,
        IStringLocalizer<AIProfileDisplayDriver> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        _options = options.Value;
        _providerOptions = providerOptions.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditProfileDeploymentViewModel>("AIProfileDeployment_Edit", async model =>
        {
            if (!_options.ProfileSources.TryGetValue(profile.Source, out var profileSource))
            {
                return;
            }

            model.ConnectionName = profile.ConnectionName;
            model.DeploymentId = profile.DeploymentId;
            model.ProviderName = profileSource.ProviderName;

            if (!string.IsNullOrEmpty(profile.DeploymentId))
            {
                var deployment = await _deploymentManager.FindByIdAsync(profile.DeploymentId);

                if (deployment is not null)
                {
                    model.Deployments = (await _deploymentManager.GetAsync(profileSource.ProviderName, deployment.ConnectionName))
                    .Select(x => new SelectListItem(x.Name, x.Id));
                }
            }

            if (model.Deployments is null || !model.Deployments.Any())
            {
                var connectionName = profile.ConnectionName;

                if (string.IsNullOrEmpty(connectionName) && _providerOptions.Providers.TryGetValue(profileSource.ProviderName, out var provider))
                {
                    connectionName = provider.DefaultConnectionName;
                }

                if (!string.IsNullOrEmpty(connectionName))
                {
                    model.Deployments = (await _deploymentManager.GetAsync(profileSource.ProviderName, connectionName))
                    .Select(x => new SelectListItem(x.Name, x.Id));
                }
            }
        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        if (!_options.ProfileSources.TryGetValue(profile.Source, out var profileSource))
        {
            return null;
        }

        var model = new EditProfileDeploymentViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        profile.DeploymentId = model.DeploymentId;
        profile.ConnectionName = model.ConnectionName;

        return Edit(profile, context);
    }
}
