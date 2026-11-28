using System.Text.Json;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Agent.AIModels;

/// <summary>
/// Deletes AI deployments, as the Delete action and bulk Delete of the Deployments page do. Deployments from
/// configuration, the site's default deployments and the deployments the running assistant uses are skipped; the
/// result names the profiles that named a deleted deployment.
/// </summary>
public sealed class DeleteAIDeploymentTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteAIDeployment";

    private const int MaxDeployments = 50;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "deploymentIds": {
          "type": "array",
          "items": { "type": "string" },
          "minItems": 1,
          "maxItems": 50,
          "description": "The IDs of the deployments to delete, from listAIDeployments."
        }
      },
      "required": ["deploymentIds"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes deployments by id. Profiles that named a deleted deployment fall back to the site default or stop working; the result names them. Deployments from configuration, the site's default deployments and the deployments the running assistant uses are skipped.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIDeployments))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!AgentToolArguments.TryReadStrings(arguments, "deploymentIds", out var deploymentIds) || deploymentIds is not { Count: > 0 })
        {
            return "Nothing was deleted: give the IDs of the deployments to delete in deploymentIds.";
        }

        if (deploymentIds.Count > MaxDeployments)
        {
            return $"Nothing was deleted: at most {MaxDeployments} deployments can be deleted at once.";
        }

        var manager = services.GetRequiredService<INamedSourceCatalogManager<AIDeployment>>();
        var protectedModels = await AIConfigurationGuard.GetProtectedModelsAsync(services, cancellationToken);
        var slotDefaults = AIModelToolSupport.GetSlotDefaults(await services.GetRequiredService<ISiteService>().GetSettingsAsync<DefaultAIDeploymentSettings>()).ToList();
        var profiles = (await services.GetRequiredService<IAIProfileManager>().GetAllAsync(cancellationToken)).ToList();
        var deleted = new List<object>();
        var skipped = new List<object>();

        foreach (var deploymentId in deploymentIds)
        {
            var deployment = await manager.FindByIdAsync(deploymentId, cancellationToken);

            if (deployment is null)
            {
                skipped.Add(new { id = deploymentId, reason = "No deployment has this ID." });

                continue;
            }

            if (deployment.IsReadOnly)
            {
                skipped.Add(new { id = deployment.ItemId, name = deployment.Name, reason = "The deployment is defined in configuration (appsettings) and cannot be deleted here." });

                continue;
            }

            if (protectedModels.IsProtected(deployment))
            {
                skipped.Add(new { id = deployment.ItemId, name = deployment.Name, reason = AIModelToolSupport.RunningAssistantRefusal($"the deployment '{deployment.Name}'", AIModelToolSupport.GetDeploymentEditPath(services, deployment.ItemId)) });

                continue;
            }

            var defaultFor = slotDefaults.Where(slot => AIModelToolSupport.IsNamed(deployment, slot.DeploymentName)).Select(slot => slot.Slot).ToList();

            if (defaultFor.Count > 0)
            {
                skipped.Add(new
                {
                    id = deployment.ItemId,
                    name = deployment.Name,
                    reason = $"The deployment is the site's default for {string.Join(", ", defaultFor)}. Pick another default on Settings → Artificial Intelligence first.",
                });

                continue;
            }

            if (!await manager.DeleteAsync(deployment, cancellationToken))
            {
                skipped.Add(new { id = deployment.ItemId, name = deployment.Name, reason = "The deployment manager did not delete it." });

                continue;
            }

            deleted.Add(new
            {
                id = deployment.ItemId,
                name = deployment.Name,
                profilesThatNamedIt = profiles
                    .Where(profile => AIModelToolSupport.IsNamed(deployment, profile.ChatDeploymentName) || AIModelToolSupport.IsNamed(deployment, profile.UtilityDeploymentName))
                    .Select(profile => string.IsNullOrWhiteSpace(profile.DisplayText) ? profile.Name : profile.DisplayText)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
            });
        }

        return AgentToolResults.Json(new
        {
            deleted,
            skipped,
        });
    }
}
