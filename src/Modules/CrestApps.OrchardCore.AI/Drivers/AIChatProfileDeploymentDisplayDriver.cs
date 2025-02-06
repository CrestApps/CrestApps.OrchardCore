using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

public sealed class AIChatProfileDeploymentDisplayDriver : DisplayDriver<AIChatProfile>
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly AIProviderOptions _providerOptions;

    internal readonly IStringLocalizer S;

    public AIChatProfileDeploymentDisplayDriver(
        IAIDeploymentManager deploymentManager,
        IServiceProvider serviceProvider,
        IOptions<AIProviderOptions> providerOptions,
        IStringLocalizer<AIChatProfileDisplayDriver> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        _serviceProvider = serviceProvider;
        _providerOptions = providerOptions.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIChatProfile profile, BuildEditorContext context)
    {
        return Initialize<EditChatProfileDeploymentViewModel>("AIChatProfileDeployment_Edit", async model =>
        {
            var profileSource = _serviceProvider.GetKeyedService<IAIChatProfileSource>(profile.Source);

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

    public override async Task<IDisplayResult> UpdateAsync(AIChatProfile profile, UpdateEditorContext context)
    {
        var model = new EditChatProfileDeploymentViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        profile.DeploymentId = model.DeploymentId;
        profile.ConnectionName = model.ConnectionName;

        return Edit(profile, context);
    }
}
