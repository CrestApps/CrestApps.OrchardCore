using CrestApps.AI.Models;
using CrestApps.AI;
using CrestApps.OrchardCore.Models;
using CrestApps.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Data.Migration;
using OrchardCore.Documents;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIDeploymentTypeMigrations : DataMigration
{
#pragma warning disable CA1822 // Member does not access instance data — called by convention from DataMigration base class
    public Task<int> CreateAsync()
#pragma warning restore CA1822
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var connectionDocManager = scope.ServiceProvider.GetRequiredService<IDocumentManager<DictionaryDocument<AIProviderConnection>>>();
            var deploymentDocManager = scope.ServiceProvider.GetRequiredService<IDocumentManager<DictionaryDocument<AIDeployment>>>();

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
        });

        return Task.FromResult(1);
    }

#pragma warning disable CA1822 // Mark members as static
    public int UpdateFrom1()
#pragma warning restore CA1822 // Mark members as static
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var profileCatalog = scope.ServiceProvider.GetRequiredService<INamedSourceCatalog<AIProfile>>();
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
            d.ProviderName == connection.ProviderName &&
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
            ProviderName = connection.ProviderName,
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

    private static string FindDefaultChatDeploymentId(AIProfile profile, IEnumerable<AIDeployment> deployments)
    {
        var connectionName = profile.GetLegacyConnectionName();

        if (string.IsNullOrWhiteSpace(connectionName) || string.IsNullOrWhiteSpace(profile.Source))
        {
            return null;
        }

        var candidates = deployments
            .Where(deployment =>
                deployment.Type == AIDeploymentType.Chat &&
                string.Equals(deployment.ProviderName, profile.Source, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(deployment.ConnectionName, connectionName, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(deployment.ConnectionNameAlias ?? string.Empty, connectionName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return candidates.FirstOrDefault(deployment => deployment.IsDefault)?.ItemId
            ?? candidates.FirstOrDefault()?.ItemId;
    }
}
