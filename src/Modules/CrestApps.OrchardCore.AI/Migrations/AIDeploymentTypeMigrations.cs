using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Models;
using Microsoft.Extensions.DependencyInjection;
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
#pragma warning disable CS0618 // Type or member is obsolete
                needsSave |= TryCreateDeployment(deploymentDoc, connection, connection.ChatDeploymentName, AIDeploymentType.Chat);
                needsSave |= TryCreateDeployment(deploymentDoc, connection, connection.EmbeddingDeploymentName, AIDeploymentType.Embedding);
                needsSave |= TryCreateDeployment(deploymentDoc, connection, connection.ImagesDeploymentName, AIDeploymentType.Image);
                needsSave |= TryCreateDeployment(deploymentDoc, connection, connection.UtilityDeploymentName, AIDeploymentType.Utility);
#pragma warning restore CS0618 // Type or member is obsolete
            }

            if (needsSave)
            {
                await deploymentDocManager.UpdateAsync(deploymentDoc);
            }
        });

        return Task.FromResult(1);
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
}
