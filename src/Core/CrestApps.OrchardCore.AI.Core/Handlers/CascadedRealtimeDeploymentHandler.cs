using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Realtime;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

/// <summary>
/// Keeps a cascaded realtime deployment coherent: it reads the chained deployment names from recipe data,
/// declares the realtime feature the orchestrator resolves on, and refuses a chain that cannot run.
/// </summary>
/// <remarks>
/// The realtime feature is set here rather than left to the operator because, for a cascade, it is not a
/// claim about a model — it is structural. A cascade is realtime by construction, and a deployment that
/// failed to declare it would simply never be offered to a profile.
/// </remarks>
public sealed class CascadedRealtimeDeploymentHandler : CatalogEntryHandlerBase<AIDeployment>
{
    private readonly IAIDeploymentStore _deploymentStore;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CascadedRealtimeDeploymentHandler"/> class.
    /// </summary>
    /// <param name="deploymentStore">The store used to verify the chained deployments exist.</param>
    /// <param name="stringLocalizer">The string localizer for validation messages.</param>
    public CascadedRealtimeDeploymentHandler(
        IAIDeploymentStore deploymentStore,
        IStringLocalizer<CascadedRealtimeDeploymentHandler> stringLocalizer)
    {
        _deploymentStore = deploymentStore;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override Task InitializingAsync(InitializingContext<AIDeployment> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data);

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<AIDeployment> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data);

    /// <inheritdoc />
    public override Task CreatingAsync(CreatingContext<AIDeployment> context, CancellationToken cancellationToken = default)
    {
        DeclareRealtimeFeature(context.Model);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task ValidatingAsync(ValidatingContext<AIDeployment> context, CancellationToken cancellationToken = default)
    {
        if (!IsCascaded(context.Model))
        {
            return;
        }

        DeclareRealtimeFeature(context.Model);

        if (!context.Model.TryGet<CascadedRealtimeMetadata>(out var metadata) || !metadata.IsComplete())
        {
            context.Result.Fail(new ValidationResult(S["A cascaded realtime deployment must name a speech-to-text, a chat, and a text-to-speech deployment."]));

            return;
        }

        await ValidateLegAsync(context, metadata.SpeechToTextDeploymentName, S["speech-to-text"], rejectCascade: true, cancellationToken);
        await ValidateLegAsync(context, metadata.ChatDeploymentName, S["chat"], rejectCascade: false, cancellationToken);
        await ValidateLegAsync(context, metadata.TextToSpeechDeploymentName, S["text-to-speech"], rejectCascade: false, cancellationToken);
    }

    private async Task ValidateLegAsync(
        ValidatingContext<AIDeployment> context,
        string name,
        LocalizedString role,
        bool rejectCascade,
        CancellationToken cancellationToken)
    {
        var leg = await _deploymentStore.FindByNameAsync(name, cancellationToken);

        if (leg is null)
        {
            context.Result.Fail(new ValidationResult(S["No deployment named '{0}' exists for the {1} step.", name, role.Value]));

            return;
        }

        // A cascade whose transcribing leg is itself a cascade would compose forever.
        if (rejectCascade && IsCascaded(leg))
        {
            context.Result.Fail(new ValidationResult(S["The {0} step cannot be another cascaded realtime deployment. Choose a deployment that transcribes directly.", role.Value]));
        }
    }

    /// <summary>
    /// Reads the chained deployment names from the data a recipe or import supplied.
    /// </summary>
    /// <param name="deployment">The deployment being populated.</param>
    /// <param name="data">The source data, or <see langword="null"/> when there is none.</param>
    private static Task PopulateAsync(AIDeployment deployment, JsonNode data)
    {
        if (data is null || !IsCascaded(deployment))
        {
            return Task.CompletedTask;
        }

        var node = data[nameof(CascadedRealtimeMetadata)];

        if (node is not null)
        {
            deployment.Alter<CascadedRealtimeMetadata>(metadata =>
            {
                metadata.SpeechToTextDeploymentName = ReadName(node, nameof(CascadedRealtimeMetadata.SpeechToTextDeploymentName)) ?? metadata.SpeechToTextDeploymentName;
                metadata.ChatDeploymentName = ReadName(node, nameof(CascadedRealtimeMetadata.ChatDeploymentName)) ?? metadata.ChatDeploymentName;
                metadata.TextToSpeechDeploymentName = ReadName(node, nameof(CascadedRealtimeMetadata.TextToSpeechDeploymentName)) ?? metadata.TextToSpeechDeploymentName;
            });
        }

        DeclareRealtimeFeature(deployment);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads one deployment name from the supplied metadata node.
    /// </summary>
    /// <param name="node">The metadata node.</param>
    /// <param name="propertyName">The name of the property to read.</param>
    private static string ReadName(JsonNode node, string propertyName)
    {
        var value = node[propertyName]?.GetValue<string>()?.Trim();

        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    /// Adds the realtime feature to the deployment's declared capabilities when it is missing.
    /// </summary>
    /// <param name="deployment">The deployment to update.</param>
    private static void DeclareRealtimeFeature(AIDeployment deployment)
    {
        if (!IsCascaded(deployment))
        {
            return;
        }

        deployment.Alter<AIDeploymentMetadata>(metadata =>
        {
            if (metadata.SupportsFeature(AIDeploymentFeatureNames.Realtime))
            {
                return;
            }

            metadata.Features = [.. metadata.Features ?? [], AIDeploymentFeatureNames.Realtime];
        });
    }

    private static bool IsCascaded(AIDeployment deployment)
        => string.Equals(deployment.ClientName, AIConstants.CascadedRealtimeClientName, StringComparison.OrdinalIgnoreCase);
}
