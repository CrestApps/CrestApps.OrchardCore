using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

/// <summary>
/// Handles events for AI deployment profile.
/// </summary>
public sealed class AIDeploymentProfileHandler : CatalogEntryHandlerBase<AIProfile>
{
    private readonly INamedCatalog<AIDeployment> _deploymentsCatalog;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDeploymentProfileHandler"/> class.
    /// </summary>
    /// <param name="deploymentsCatalog">The deployments catalog.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIDeploymentProfileHandler(
        INamedCatalog<AIDeployment> deploymentsCatalog,
        IStringLocalizer<AIProfileHandler> stringLocalizer)
    {
        _deploymentsCatalog = deploymentsCatalog;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<AIProfile> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<AIProfile> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data);

    public override async Task ValidatingAsync(ValidatingContext<AIProfile> context, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(context.Model.ChatDeploymentName) &&
            await FindDeploymentAsync(context.Model.ChatDeploymentName, cancellationToken) is null)
        {
            context.Result.Fail(new ValidationResult(S["Invalid deployment selection provided."], [nameof(AIProfile.ChatDeploymentName)]));
        }

        if (!string.IsNullOrEmpty(context.Model.UtilityDeploymentName) &&
            await FindDeploymentAsync(context.Model.UtilityDeploymentName, cancellationToken) is null)
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

    private async Task<AIDeployment> FindDeploymentAsync(string selector, CancellationToken cancellationToken = default)
        => await _deploymentsCatalog.FindByIdAsync(selector, cancellationToken)
    ?? await _deploymentsCatalog.FindByNameAsync(selector, cancellationToken);

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
