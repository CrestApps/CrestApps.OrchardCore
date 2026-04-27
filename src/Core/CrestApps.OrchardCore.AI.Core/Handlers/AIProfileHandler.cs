using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

/// <summary>
/// Handles catalog lifecycle events for <see cref="AIProfile"/> entries, including initialization, validation, creation, and population from JSON data.
/// </summary>
public sealed class AIProfileHandler : CatalogEntryHandlerBase<AIProfile>
{
    private readonly INamedCatalog<AIDeployment> _deploymentCatalog;
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileHandler"/> class.
    /// </summary>
    /// <param name="deploymentCatalog">The deployment catalog used for legacy deployment ID resolution.</param>
    /// <param name="liquidTemplateManager">The Liquid template manager for validating prompt templates.</param>
    /// <param name="stringLocalizer">The string localizer for validation messages.</param>
    public AIProfileHandler(
        INamedCatalog<AIDeployment> deploymentCatalog,
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<AIProfileHandler> stringLocalizer)
    {
        _deploymentCatalog = deploymentCatalog;
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<AIProfile> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data, true);

    public override Task UpdatingAsync(UpdatingContext<AIProfile> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data, false);

    public override async Task ValidatingAsync(ValidatingContext<AIProfile> context, CancellationToken cancellationToken = default)
    {
        if (context.Model.Type == AIProfileType.TemplatePrompt)
        {
            if (!string.IsNullOrWhiteSpace(context.Model.PromptTemplate) &&
                !_liquidTemplateManager.Validate(context.Model.PromptTemplate, out var _))
            {
                context.Result.Fail(new ValidationResult(S["Invalid liquid template used for Prompt template."], [nameof(AIProfile.PromptTemplate)]));
            }
        }
    }

    private async Task PopulateAsync(AIProfile profile, JsonNode data, bool isNew)
    {
        var type = data[nameof(AIProfile.Type)]?.GetEnumValue<AIProfileType>();

        if (type.HasValue)
        {
            profile.Type = type.Value;
        }

        var titleType = data[nameof(AIProfile.TitleType)]?.GetEnumValue<AISessionTitleType>();

        if (titleType.HasValue)
        {
            profile.TitleType = titleType.Value;
        }

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

        if (string.IsNullOrWhiteSpace(profile.DisplayText))
        {
            profile.DisplayText = profile.Name;
        }
    }

    private async Task<string> ResolveLegacyDeploymentIdAsync(string deploymentId, string currentValue)
    {
        if (!string.IsNullOrWhiteSpace(deploymentId))
        {
            var deployment = await _deploymentCatalog.FindByIdAsync(deploymentId);

            if (deployment != null)
            {
                return deployment.Name;
            }
        }

        return currentValue;
    }
}
