using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;

namespace CrestApps.OrchardCore.AI.Migrations;

internal static class LegacyAIDeploymentMigrationHelper
{
    /// <summary>
    /// The field names the deployment purpose was stored under, newest first.
    /// </summary>
    /// <remarks>
    /// The same field was called <c>Type</c>, then <c>Capability</c>, then <c>Purpose</c> before it was
    /// replaced by model capabilities. A stored document can still carry any one of them.
    /// </remarks>
    private static readonly string[] _legacyPurposeFieldNames = ["Purpose", "Capability", "Type"];

    /// <summary>
    /// Reads the legacy purpose a stored deployment still carries, under whichever of its historical field
    /// names it was written with.
    /// </summary>
    /// <returns><see langword="true"/> when the document named a purpose this migration understands.</returns>
    public static bool TryReadLegacyPurpose(JsonNode deploymentNode, out LegacyAIDeploymentPurpose purpose)
    {
        purpose = LegacyAIDeploymentPurpose.None;

        if (deploymentNode is null)
        {
            return false;
        }

        foreach (var fieldName in _legacyPurposeFieldNames)
        {
            if (TryReadLegacyPurposeValue(deploymentNode[fieldName], out purpose))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads a single stored purpose value, which can be one name, a comma-separated set of names, the
    /// numeric flags, or an array of names.
    /// </summary>
    public static bool TryReadLegacyPurposeValue(JsonNode purposeNode, out LegacyAIDeploymentPurpose purpose)
    {
        purpose = LegacyAIDeploymentPurpose.None;

        if (purposeNode is null)
        {
            return false;
        }

        if (purposeNode is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is null ||
                    !Enum.TryParse<LegacyAIDeploymentPurpose>(item.GetValue<string>(), ignoreCase: true, out var parsedPurpose) ||
                    parsedPurpose == LegacyAIDeploymentPurpose.None)
                {
                    purpose = LegacyAIDeploymentPurpose.None;

                    return false;
                }

                purpose |= parsedPurpose;
            }

            return purpose.IsValidSelection();
        }

        var purposeText = purposeNode.GetValue<string>();

        return !string.IsNullOrWhiteSpace(purposeText) &&
            Enum.TryParse(purposeText, ignoreCase: true, out purpose) &&
            purpose.IsValidSelection();
    }

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

    public static LegacyAIDeploymentPurpose MergeDeploymentTypes(
        LegacyAIDeploymentPurpose existingType,
        LegacyAIDeploymentPurpose incomingType)
    {
        var mergedType = existingType.IsValidSelection()
            ? existingType
            : LegacyAIDeploymentPurpose.None;

        if (incomingType.IsValidSelection())
        {
            mergedType |= incomingType;
        }

        return NormalizeInteractiveTypes(mergedType);
    }

    public static LegacyAIDeploymentPurpose NormalizeInteractiveTypes(LegacyAIDeploymentPurpose deploymentType)
    {
        if (deploymentType.HasFlag(LegacyAIDeploymentPurpose.Chat) || deploymentType.HasFlag(LegacyAIDeploymentPurpose.Utility))
        {
            deploymentType |= LegacyAIDeploymentPurpose.Chat | LegacyAIDeploymentPurpose.Utility;
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
                LegacyAIDeploymentPurpose.Chat,
                static connection => connection.GetLegacyChatDeploymentName()));

        updated |= TryPopulateDefaultDeploymentName(
            settings.DefaultUtilityDeploymentName,
            value => settings.DefaultUtilityDeploymentName = value,
            FindPreferredDeploymentName(
                deployments,
                connections,
                LegacyAIDeploymentPurpose.Utility,
                static connection => connection.GetLegacyUtilityDeploymentName()));

        updated |= TryPopulateDefaultDeploymentName(
            settings.DefaultEmbeddingDeploymentName,
            value => settings.DefaultEmbeddingDeploymentName = value,
            FindPreferredDeploymentName(
                deployments,
                connections,
                LegacyAIDeploymentPurpose.Embedding,
                static connection => connection.GetLegacyEmbeddingDeploymentName()));

        updated |= TryPopulateDefaultDeploymentName(
            settings.DefaultImageDeploymentName,
            value => settings.DefaultImageDeploymentName = value,
            FindPreferredDeploymentName(
                deployments,
                connections,
                LegacyAIDeploymentPurpose.Image,
                static connection => connection.GetLegacyImageDeploymentName()));

        updated |= TryPopulateDefaultDeploymentName(
            settings.DefaultSpeechToTextDeploymentName,
            value => settings.DefaultSpeechToTextDeploymentName = value,
            FindPreferredDeploymentName(
                deployments,
                connections,
                LegacyAIDeploymentPurpose.SpeechToText,
                static connection => connection.GetLegacySpeechToTextDeploymentName()));

        updated |= TryPopulateDefaultDeploymentName(
            settings.DefaultTextToSpeechDeploymentName,
            value => settings.DefaultTextToSpeechDeploymentName = value,
            FindPreferredDeploymentName(
                deployments,
                connections,
                LegacyAIDeploymentPurpose.TextToSpeech));

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
        LegacyAIDeploymentPurpose type,
        Func<AIProviderConnection, string> legacyDeploymentNameAccessor = null)
    {
        var candidates = deployments
            .Where(deployment => type.IsSupportedBy(deployment))
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
        LegacyAIDeploymentPurpose type,
        AIProviderConnection connection,
        IEnumerable<AIDeployment> deployments,
        string deploymentName)
    {
        return deployments
            .Where(deployment =>
                type.IsSupportedBy(deployment) &&
                MatchesLegacyDeploymentName(deployment, deploymentName) &&
                (string.Equals(deployment.ConnectionName, connection.ItemId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(deployment.ConnectionName, connection.Name, StringComparison.OrdinalIgnoreCase)))
            .Select(deployment => deployment.Name)
            .FirstOrDefault()
            ?? deployments
                .Where(deployment =>
                    type.IsSupportedBy(deployment) &&
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
