using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Data.Migration;
using OrchardCore.Documents;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIDeploymentTypeMigrations : DataMigration
{
    public static int Create()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var connectionDocManager = scope.ServiceProvider.GetRequiredService<IDocumentManager<DictionaryDocument<AIProviderConnection>>>();
            var deploymentDocManager = scope.ServiceProvider.GetRequiredService<IDocumentManager<DictionaryDocument<AIDeployment>>>();
            var siteService = scope.ServiceProvider.GetRequiredService<ISiteService>();

            var connectionDoc = await connectionDocManager.GetOrCreateMutableAsync();
            var deploymentDoc = await deploymentDocManager.GetOrCreateMutableAsync();

            var needsSave = false;

            foreach (var connection in connectionDoc.Records.Values)
            {
                needsSave |= TryCreateDeployment(deploymentDoc, connection, connection.GetLegacyChatDeploymentName(), AIDeploymentType.Chat);
                needsSave |= TryCreateDeployment(deploymentDoc, connection, connection.GetLegacyEmbeddingDeploymentName(), AIDeploymentType.Embedding);
                needsSave |= TryCreateDeployment(deploymentDoc, connection, connection.GetLegacyImageDeploymentName(), AIDeploymentType.Image);
                needsSave |= TryCreateDeployment(deploymentDoc, connection, connection.GetLegacyUtilityDeploymentName(), AIDeploymentType.Utility);
            }

            if (needsSave)
            {
                await deploymentDocManager.UpdateAsync(deploymentDoc);
            }

            await TryBackfillDefaultDeploymentSettingsAsync(
                siteService,
                connectionDoc.Records.Values,
                deploymentDoc.Records.Values);
        });

        return 1;
    }

    public static int UpdateFrom1()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var profileCatalog = scope.ServiceProvider.GetRequiredService<IAIProfileStore>();
            var deploymentManager = scope.ServiceProvider.GetRequiredService<IAIDeploymentManager>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AIDeploymentTypeMigrations>>();

            var deployments = (await deploymentManager.GetAllByTypeAsync(AIDeploymentType.Chat)).ToList();
            var profiles = await profileCatalog.GetAllAsync();

            var updatedCount = 0;
            var skippedCount = 0;

            foreach (var profile in profiles)
            {
                if (!string.IsNullOrEmpty(profile.ChatDeploymentId))
                {
                    continue;
                }

                var deploymentId = FindDefaultChatDeploymentId(profile, deployments);

                if (string.IsNullOrEmpty(deploymentId))
                {
                    skippedCount++;
                    continue;
                }

                profile.ChatDeploymentId = deploymentId;
                await profileCatalog.UpdateAsync(profile);
                updatedCount++;
            }

            if (updatedCount == 0 && skippedCount == 0)
            {
                return;
            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Backfilled ChatDeploymentId for {UpdatedCount} AI profiles. Skipped {SkippedCount} profiles that had no matching legacy chat deployment.",
                    updatedCount,
                    skippedCount);
            }
        });

        return 2;
    }

    public static int UpdateFrom2()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var connectionDocManager = scope.ServiceProvider.GetRequiredService<IDocumentManager<DictionaryDocument<AIProviderConnection>>>();
            var deploymentDocManager = scope.ServiceProvider.GetRequiredService<IDocumentManager<DictionaryDocument<AIDeployment>>>();
            var siteService = scope.ServiceProvider.GetRequiredService<ISiteService>();

            var connectionDoc = await connectionDocManager.GetOrCreateImmutableAsync();
            var deploymentDoc = await deploymentDocManager.GetOrCreateImmutableAsync();

            await TryBackfillDefaultDeploymentSettingsAsync(
                siteService,
                connectionDoc.Records.Values,
                deploymentDoc.Records.Values);
        });

        return 3;
    }

    private static bool TryCreateDeployment(
        DictionaryDocument<AIDeployment> deploymentDoc,
        AIProviderConnection connection,
        string deploymentName,
        AIDeploymentType type)
    {
        if (string.IsNullOrWhiteSpace(deploymentName))
        {
            return false;
        }

        var exists = deploymentDoc.Records.Values.Any(d =>
            d.ClientName == connection.ClientName &&
            string.Equals(d.ConnectionName, connection.ItemId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(d.Name, deploymentName, StringComparison.OrdinalIgnoreCase));

        if (exists)
        {
            return false;
        }

        var deployment = new AIDeployment
        {
            ItemId = IdGenerator.GenerateId(),
            Name = deploymentName,
            ClientName = connection.ClientName,
            ConnectionName = connection.ItemId,
            ConnectionNameAlias = connection.Name,
            Type = type,
            IsDefault = true,
            CreatedUtc = connection.CreatedUtc,
            Author = connection.Author,
            OwnerId = connection.OwnerId,
        };

        deploymentDoc.Records[deployment.ItemId] = deployment;
        return true;
    }

    private static async Task TryBackfillDefaultDeploymentSettingsAsync(
        ISiteService siteService,
        IEnumerable<AIProviderConnection> connections,
        IEnumerable<AIDeployment> deployments)
    {
        var site = await siteService.GetSiteSettingsAsync();
        var updated = false;

        site.Alter<DefaultAIDeploymentSettings>(settings =>
            updated = TryPopulateDefaultDeploymentSettings(settings, connections, deployments));

        if (!updated)
        {
            return;
        }

        await siteService.UpdateSiteSettingsAsync(site);
    }

    private static bool TryPopulateDefaultDeploymentSettings(
        DefaultAIDeploymentSettings settings,
        IEnumerable<AIProviderConnection> connections,
        IEnumerable<AIDeployment> deployments)
    {
        var updated = false;

        updated |= TryPopulateDefaultDeploymentId(
            settings.DefaultChatDeploymentId,
            value => settings.DefaultChatDeploymentId = value,
            FindDefaultDeploymentId(
                connections,
                deployments,
                AIDeploymentType.Chat,
                static connection => connection.GetLegacyChatDeploymentName()));

        updated |= TryPopulateDefaultDeploymentId(
            settings.DefaultUtilityDeploymentId,
            value => settings.DefaultUtilityDeploymentId = value,
            FindDefaultDeploymentId(
                connections,
                deployments,
                AIDeploymentType.Utility,
                static connection => connection.GetLegacyUtilityDeploymentName()));

        updated |= TryPopulateDefaultDeploymentId(
            settings.DefaultEmbeddingDeploymentId,
            value => settings.DefaultEmbeddingDeploymentId = value,
            FindDefaultDeploymentId(
                connections,
                deployments,
                AIDeploymentType.Embedding,
                static connection => connection.GetLegacyEmbeddingDeploymentName()));

        updated |= TryPopulateDefaultDeploymentId(
            settings.DefaultImageDeploymentId,
            value => settings.DefaultImageDeploymentId = value,
            FindDefaultDeploymentId(
                connections,
                deployments,
                AIDeploymentType.Image,
                static connection => connection.GetLegacyImageDeploymentName()));

        updated |= TryPopulateDefaultDeploymentId(
            settings.DefaultSpeechToTextDeploymentId,
            value => settings.DefaultSpeechToTextDeploymentId = value,
            FindDefaultDeploymentId(connections, deployments, AIDeploymentType.SpeechToText));

        updated |= TryPopulateDefaultDeploymentId(
            settings.DefaultTextToSpeechDeploymentId,
            value => settings.DefaultTextToSpeechDeploymentId = value,
            FindDefaultDeploymentId(connections, deployments, AIDeploymentType.TextToSpeech));

        return updated;
    }

    private static bool TryPopulateDefaultDeploymentId(
        string currentValue,
        Action<string> assign,
        string newValue)
    {
        if (!string.IsNullOrEmpty(currentValue) || string.IsNullOrEmpty(newValue))
        {
            return false;
        }

        assign(newValue);
        return true;
    }

    private static string FindDefaultDeploymentId(
        IEnumerable<AIProviderConnection> connections,
        IEnumerable<AIDeployment> deployments,
        AIDeploymentType type,
        Func<AIProviderConnection, string> legacyDeploymentNameAccessor = null)
    {
        if (legacyDeploymentNameAccessor != null)
        {
            var orderedConnections = connections
                .Where(connection => !string.IsNullOrWhiteSpace(legacyDeploymentNameAccessor(connection)))
                .OrderByDescending(connection => connection.IsDefault)
                .ThenBy(connection => connection.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var connection in orderedConnections)
            {
                var deploymentId = FindDefaultDeploymentId(type, connection.ItemId, connection.Name, deployments);

                if (!string.IsNullOrEmpty(deploymentId))
                {
                    return deploymentId;
                }
            }
        }

        return deployments
            .Where(deployment => deployment.SupportsType(type))
            .OrderByDescending(deployment => deployment.IsDefault)
            .ThenBy(deployment => deployment.ConnectionNameAlias ?? deployment.ConnectionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(deployment => deployment.Name, StringComparer.OrdinalIgnoreCase)
            .Select(deployment => deployment.ItemId)
            .FirstOrDefault();
    }

    private static string FindDefaultChatDeploymentId(AIProfile profile, IEnumerable<AIDeployment> deployments)
    {
        return FindDefaultDeploymentId(AIDeploymentType.Chat, profile.GetLegacyConnectionName(), null, deployments);
    }

    private static string FindDefaultDeploymentId(
        AIDeploymentType type,
        string connectionId,
        string connectionAlias,
        IEnumerable<AIDeployment> deployments)
    {
        if (string.IsNullOrWhiteSpace(connectionId) && string.IsNullOrWhiteSpace(connectionAlias))
        {
            return null;
        }

        var candidates = deployments
            .Where(deployment =>
                deployment.SupportsType(type) &&
                (string.Equals(deployment.ConnectionName, connectionId, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(deployment.ConnectionNameAlias ?? string.Empty, connectionId, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(deployment.ConnectionName, connectionAlias, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(deployment.ConnectionNameAlias ?? string.Empty, connectionAlias, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return candidates.FirstOrDefault(deployment => deployment.IsDefault)?.ItemId
            ?? candidates.FirstOrDefault()?.ItemId;
    }
}
