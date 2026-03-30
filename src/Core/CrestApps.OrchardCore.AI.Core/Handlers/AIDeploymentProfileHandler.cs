using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public sealed class AIDeploymentProfileHandler : CatalogEntryHandlerBase<AIProfile>
{
    private readonly INamedCatalog<AIDeployment> _deploymentsCatalog;

    internal readonly IStringLocalizer S;

    public AIDeploymentProfileHandler(
        INamedCatalog<AIDeployment> deploymentsCatalog,
        IStringLocalizer<AIProfileHandler> stringLocalizer)
    {
        _deploymentsCatalog = deploymentsCatalog;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<AIProfile> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<AIProfile> context)
        => PopulateAsync(context.Model, context.Data);

    public override async Task ValidatingAsync(ValidatingContext<AIProfile> context)
    {
        if (!string.IsNullOrEmpty(context.Model.ChatDeploymentName) &&
            await FindDeploymentAsync(context.Model.ChatDeploymentName) is null)
        {
            context.Result.Fail(new ValidationResult(S["Invalid deployment selection provided."], [nameof(AIProfile.ChatDeploymentName)]));
        }

        if (!string.IsNullOrEmpty(context.Model.UtilityDeploymentName) &&
            await FindDeploymentAsync(context.Model.UtilityDeploymentName) is null)
        {
            context.Result.Fail(new ValidationResult(S["Invalid deployment selection provided."], [nameof(AIProfile.UtilityDeploymentName)]));
        }
    }

    private async Task PopulateAsync(AIProfile profile, JsonNode data)
    {
        var chatDeploymentName = data[nameof(AIProfile.ChatDeploymentName)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrWhiteSpace(chatDeploymentName))
        {
            profile.ChatDeploymentName = chatDeploymentName;
        }
        else
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var chatDeploymentId = data[nameof(AIProfile.ChatDeploymentId)]?.GetValue<string>()?.Trim()
                ?? data["DeploymentId"]?.GetValue<string>()?.Trim();
#pragma warning restore CS0618 // Type or member is obsolete

            profile.ChatDeploymentName = await ResolveLegacyDeploymentIdAsync(chatDeploymentId, profile.ChatDeploymentName);
        }

        var utilityDeploymentName = data[nameof(AIProfile.UtilityDeploymentName)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrWhiteSpace(utilityDeploymentName))
        {
            profile.UtilityDeploymentName = utilityDeploymentName;
        }
        else
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var utilityDeploymentId = data[nameof(AIProfile.UtilityDeploymentId)]?.GetValue<string>()?.Trim();
#pragma warning restore CS0618 // Type or member is obsolete

            profile.UtilityDeploymentName = await ResolveLegacyDeploymentIdAsync(utilityDeploymentId, profile.UtilityDeploymentName);
        }
    }

    private async Task<AIDeployment> FindDeploymentAsync(string selector)
        => await _deploymentsCatalog.FindByIdAsync(selector)
        ?? await _deploymentsCatalog.FindByNameAsync(selector);

    private async Task<string> ResolveLegacyDeploymentIdAsync(string deploymentId, string currentValue)
    {
        if (!string.IsNullOrWhiteSpace(deploymentId))
        {
            var deployment = await _deploymentsCatalog.FindByIdAsync(deploymentId);

            if (deployment != null)
            {
                return deployment.Name;
            }
        }

        return currentValue;
    }
}
