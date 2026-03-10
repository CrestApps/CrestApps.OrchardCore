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
        if (!string.IsNullOrEmpty(context.Model.ChatDeploymentId) && await _deploymentsCatalog.FindByIdAsync(context.Model.ChatDeploymentId) is null)
        {
            context.Result.Fail(new ValidationResult(S["Invalid DeploymentId provided."], [nameof(AIProfile.ChatDeploymentId)]));
        }
    }

    private static Task PopulateAsync(AIProfile profile, JsonNode data)
    {
        var deploymentId = data[nameof(AIProfile.ChatDeploymentId)]?.GetValue<string>()?.Trim()
            ?? data["DeploymentId"]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(deploymentId))
        {
            profile.ChatDeploymentId = deploymentId;
        }

        var connectionName = data[nameof(AIProfile.ConnectionName)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(connectionName))
        {
            profile.ConnectionName = connectionName;
        }

        return Task.CompletedTask;
    }
}
