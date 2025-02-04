using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public sealed class AIDeploymentChatProfileHandler : AIChatProfileHandlerBase
{
    private readonly IAIDeploymentStore _deploymentStore;

    internal readonly IStringLocalizer S;

    public AIDeploymentChatProfileHandler(
        IAIDeploymentStore deploymentStore,
        IStringLocalizer<AIChatProfileHandler> stringLocalizer)
    {
        _deploymentStore = deploymentStore;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingAIChatProfileContext context)
        => PopulateAsync(context.Profile, context.Data);

    public override Task UpdatingAsync(UpdatingAIChatProfileContext context)
        => PopulateAsync(context.Profile, context.Data);

    public override async Task ValidatingAsync(ValidatingAIChatProfileContext context)
    {
        if (!string.IsNullOrEmpty(context.Profile.DeploymentId) && await _deploymentStore.FindByIdAsync(context.Profile.DeploymentId) is null)
        {
            context.Result.Fail(new ValidationResult(S["Invalid DeploymentId provided."], [nameof(AIChatProfile.DeploymentId)]));
        }
    }

    private static Task PopulateAsync(AIChatProfile profile, JsonNode data)
    {
        var deploymentId = data[nameof(AIChatProfile.DeploymentId)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(deploymentId))
        {
            profile.DeploymentId = deploymentId;
        }

        return Task.CompletedTask;
    }
}
