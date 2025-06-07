using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public sealed class AIDeploymentProfileHandler : ModelHandlerBase<AIProfile>
{
    private readonly INamedCatalog<AIDeployment> _deploymentStore;

    internal readonly IStringLocalizer S;

    public AIDeploymentProfileHandler(
        INamedCatalog<AIDeployment> deploymentStore,
        IStringLocalizer<AIProfileHandler> stringLocalizer)
    {
        _deploymentStore = deploymentStore;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<AIProfile> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<AIProfile> context)
        => PopulateAsync(context.Model, context.Data);

    public override async Task ValidatingAsync(ValidatingContext<AIProfile> context)
    {
        if (!string.IsNullOrEmpty(context.Model.DeploymentId) && await _deploymentStore.FindByIdAsync(context.Model.DeploymentId) is null)
        {
            context.Result.Fail(new ValidationResult(S["Invalid DeploymentId provided."], [nameof(AIProfile.DeploymentId)]));
        }
    }

    private static Task PopulateAsync(AIProfile profile, JsonNode data)
    {
        var deploymentId = data[nameof(AIProfile.DeploymentId)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(deploymentId))
        {
            profile.DeploymentId = deploymentId;
        }

        var connectionName = data[nameof(AIProfile.ConnectionName)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(connectionName))
        {
            profile.ConnectionName = connectionName;
        }

        return Task.CompletedTask;
    }
}
