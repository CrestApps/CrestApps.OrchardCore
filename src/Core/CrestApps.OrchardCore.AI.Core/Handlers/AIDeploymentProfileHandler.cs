using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public sealed class AIDeploymentProfileHandler : AIProfileHandlerBase
{
    private readonly IAIDeploymentStore _deploymentStore;

    internal readonly IStringLocalizer S;

    public AIDeploymentProfileHandler(
        IAIDeploymentStore deploymentStore,
        IStringLocalizer<AIProfileHandler> stringLocalizer)
    {
        _deploymentStore = deploymentStore;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingAIProfileContext context)
        => PopulateAsync(context.Profile, context.Data);

    public override Task UpdatingAsync(UpdatingAIProfileContext context)
        => PopulateAsync(context.Profile, context.Data);

    public override async Task ValidatingAsync(ValidatingAIProfileContext context)
    {
        if (!string.IsNullOrEmpty(context.Profile.DeploymentId) && await _deploymentStore.FindByIdAsync(context.Profile.DeploymentId) is null)
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

        return Task.CompletedTask;
    }
}
