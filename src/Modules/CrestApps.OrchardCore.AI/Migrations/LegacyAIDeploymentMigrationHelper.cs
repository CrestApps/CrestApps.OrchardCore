using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;

namespace CrestApps.OrchardCore.AI.Migrations;

internal static class LegacyAIDeploymentMigrationHelper
{
    public static AIDeployment FindWritableDeployment(
        IEnumerable<AIDeployment> deployments,
        string itemId,
        string deploymentName,
        string modelName,
        string sourceName,
        string connectionName)
    {
        ArgumentNullException.ThrowIfNull(deployments);

        if (!string.IsNullOrWhiteSpace(itemId) &&
            deployments.FirstOrDefault(deployment => string.Equals(deployment.ItemId, itemId, StringComparison.OrdinalIgnoreCase)) is { IsReadOnly: false } deploymentById)
        {
            return deploymentById;
        }

        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return null;
        }

        return deployments.FirstOrDefault(deployment =>
            !deployment.IsReadOnly &&
            string.Equals(deployment.Source, sourceName, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(connectionName) ||
                string.Equals(deployment.ConnectionName, connectionName, StringComparison.OrdinalIgnoreCase)) &&
            MatchesDeploymentIdentity(deployment, deploymentName, modelName));
    }

    public static bool HasNameConflict(IEnumerable<AIDeployment> deployments, string deploymentName)
    {
        ArgumentNullException.ThrowIfNull(deployments);

        if (string.IsNullOrWhiteSpace(deploymentName))
        {
            return false;
        }

        return deployments.Any(deployment => string.Equals(deployment.Name, deploymentName, StringComparison.OrdinalIgnoreCase));
    }

    public static string GenerateUniqueDeploymentName(IEnumerable<AIDeployment> deployments, string deploymentName)
    {
        ArgumentNullException.ThrowIfNull(deployments);

        var normalizedName = string.IsNullOrWhiteSpace(deploymentName)
            ? "migrated-deployment"
            : deploymentName.Trim();

        if (!HasNameConflict(deployments, normalizedName))
        {
            return normalizedName;
        }

        var baseCandidate = $"{normalizedName}-migrated";
        if (!HasNameConflict(deployments, baseCandidate))
        {
            return baseCandidate;
        }

        for (var index = 2; ; index++)
        {
            var candidate = $"{baseCandidate}-{index}";

            if (!HasNameConflict(deployments, candidate))
            {
                return candidate;
            }
        }
    }

    public static AIDeploymentType MergeDeploymentTypes(
        AIDeploymentType existingType,
        AIDeploymentType incomingType)
    {
        var mergedType = existingType.IsValidSelection()
            ? existingType
            : AIDeploymentType.None;

        if (incomingType.IsValidSelection())
        {
            mergedType |= incomingType;
        }

        return NormalizeInteractiveTypes(mergedType);
    }

    public static AIDeploymentType NormalizeInteractiveTypes(AIDeploymentType deploymentType)
    {
        if (deploymentType.HasFlag(AIDeploymentType.Chat) || deploymentType.HasFlag(AIDeploymentType.Utility))
        {
            deploymentType |= AIDeploymentType.Chat | AIDeploymentType.Utility;
        }

        return deploymentType;
    }

    public static bool TryPopulateDefaultDeploymentSettings(
        DefaultAIDeploymentSettings settings,
        IEnumerable<AIProviderConnection> connections,
        IEnumerable<AIDeployment> deployments)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(deployments);

        var updated = false;

        updated |= TryPopulateDefaultDeploymentName(
            settings.DefaultChatDeploymentName,
            value => settings.DefaultChatDeploymentName = value,
            FindPreferredDeploymentName(
                deployments,
                connections,
                AIDeploymentType.Chat,
                static connection => connection.GetLegacyChatDeploymentName()));

        updated |= TryPopulateDefaultDeploymentName(
            settings.DefaultUtilityDeploymentName,
            value => settings.DefaultUtilityDeploymentName = value,
            FindPreferredDeploymentName(
                deployments,
                connections,
                AIDeploymentType.Utility,
                static connection => connection.GetLegacyUtilityDeploymentName()));

        updated |= TryPopulateDefaultDeploymentName(
            settings.DefaultEmbeddingDeploymentName,
            value => settings.DefaultEmbeddingDeploymentName = value,
            FindPreferredDeploymentName(
                deployments,
                connections,
                AIDeploymentType.Embedding,
                static connection => connection.GetLegacyEmbeddingDeploymentName()));

        updated |= TryPopulateDefaultDeploymentName(
            settings.DefaultImageDeploymentName,
            value => settings.DefaultImageDeploymentName = value,
            FindPreferredDeploymentName(
                deployments,
                connections,
                AIDeploymentType.Image,
                static connection => connection.GetLegacyImageDeploymentName()));

        updated |= TryPopulateDefaultDeploymentName(
            settings.DefaultSpeechToTextDeploymentName,
            value => settings.DefaultSpeechToTextDeploymentName = value,
            FindPreferredDeploymentName(
                deployments,
                connections,
                AIDeploymentType.SpeechToText,
                static connection => connection.GetLegacySpeechToTextDeploymentName()));

        updated |= TryPopulateDefaultDeploymentName(
            settings.DefaultTextToSpeechDeploymentName,
            value => settings.DefaultTextToSpeechDeploymentName = value,
            FindPreferredDeploymentName(
                deployments,
                connections,
                AIDeploymentType.TextToSpeech));

        return updated;
    }

    private static bool MatchesDeploymentIdentity(
        AIDeployment deployment,
        string deploymentName,
        string modelName)
    {
        if (!string.IsNullOrWhiteSpace(deploymentName) &&
            string.Equals(deployment.Name, deploymentName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(modelName) &&
            string.Equals(deployment.ModelName, modelName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(deploymentName) &&
            string.Equals(deployment.ModelName, deploymentName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryPopulateDefaultDeploymentName(
        string currentValue,
        Action<string> assign,
        string newValue)
    {
        if (!string.IsNullOrWhiteSpace(currentValue) || string.IsNullOrWhiteSpace(newValue))
        {
            return false;
        }

        assign(newValue);

        return true;
    }

    private static string FindPreferredDeploymentName(
        IEnumerable<AIDeployment> deployments,
        IEnumerable<AIProviderConnection> connections,
        AIDeploymentType type,
        Func<AIProviderConnection, string> legacyDeploymentNameAccessor = null)
    {
        var candidates = deployments
            .Where(deployment => deployment.SupportsType(type))
            .OrderBy(deployment => deployment.ConnectionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(deployment => deployment.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (legacyDeploymentNameAccessor != null)
        {
            foreach (var connection in connections
                .Where(connection => !string.IsNullOrWhiteSpace(legacyDeploymentNameAccessor(connection)))
                .OrderBy(connection => connection.Name, StringComparer.OrdinalIgnoreCase))
            {
                var deploymentName = FindConnectionDeploymentName(type, connection, candidates, legacyDeploymentNameAccessor(connection));

                if (!string.IsNullOrWhiteSpace(deploymentName))
                {
                    return deploymentName;
                }
            }
        }

        return candidates.FirstOrDefault()?.Name;
    }

    private static string FindConnectionDeploymentName(
        AIDeploymentType type,
        AIProviderConnection connection,
        IEnumerable<AIDeployment> deployments,
        string deploymentName)
    {
        return deployments
            .Where(deployment =>
                deployment.SupportsType(type) &&
                MatchesLegacyDeploymentName(deployment, deploymentName) &&
                (string.Equals(deployment.ConnectionName, connection.ItemId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(deployment.ConnectionName, connection.Name, StringComparison.OrdinalIgnoreCase)))
            .Select(deployment => deployment.Name)
            .FirstOrDefault()
            ?? deployments
                .Where(deployment =>
                    deployment.SupportsType(type) &&
                    MatchesLegacyDeploymentName(deployment, deploymentName))
                .Select(deployment => deployment.Name)
                .FirstOrDefault();
    }

    private static bool MatchesLegacyDeploymentName(AIDeployment deployment, string deploymentName)
    {
        return string.Equals(deployment.Name, deploymentName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(deployment.ModelName, deploymentName, StringComparison.OrdinalIgnoreCase);
    }
}
