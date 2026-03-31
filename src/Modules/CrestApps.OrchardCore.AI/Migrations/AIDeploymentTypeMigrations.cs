using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
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
                if (!string.IsNullOrEmpty(profile.ChatDeploymentName))
                {
                    continue;
                }

                var deploymentName = FindDefaultChatDeploymentName(profile, deployments);

                if (string.IsNullOrEmpty(deploymentName))
                {
                    skippedCount++;
                    continue;
                }

                profile.ChatDeploymentName = deploymentName;
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
                    "Backfilled ChatDeploymentName for {UpdatedCount} AI profiles. Skipped {SkippedCount} profiles that had no matching legacy chat deployment.",
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

    public static int UpdateFrom3()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var deploymentDocManager = scope.ServiceProvider.GetRequiredService<IDocumentManager<DictionaryDocument<AIDeployment>>>();
            var deploymentManager = scope.ServiceProvider.GetRequiredService<IAIDeploymentManager>();
            var siteService = scope.ServiceProvider.GetRequiredService<ISiteService>();

            var deploymentDoc = await deploymentDocManager.GetOrCreateMutableAsync();
            var deploymentsUpdated = false;

            foreach (var deployment in deploymentDoc.Records.Values)
            {
                if (!string.IsNullOrWhiteSpace(deployment.ModelName))
                {
                    continue;
                }

                deployment.ModelName = deployment.Name;
                deploymentsUpdated = true;
            }

            if (deploymentsUpdated)
            {
                await deploymentDocManager.UpdateAsync(deploymentDoc);
            }

            var deploymentNameMap = (await deploymentManager.GetAllAsync())
                .Where(static deployment => !string.IsNullOrWhiteSpace(deployment.ItemId) && !string.IsNullOrWhiteSpace(deployment.Name))
                .GroupBy(static deployment => deployment.ItemId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(static group => group.Key, static group => group.First().Name, StringComparer.OrdinalIgnoreCase);

            await TryConvertStoredDeploymentSelectorsAsync(scope.ServiceProvider, siteService, deploymentNameMap);
        });

        return 4;
    }

    public static int UpdateFrom4()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var deploymentManager = scope.ServiceProvider.GetRequiredService<IAIDeploymentManager>();
            var siteService = scope.ServiceProvider.GetRequiredService<ISiteService>();

            var deploymentNameMap = (await deploymentManager.GetAllAsync())
                .Where(static deployment => !string.IsNullOrWhiteSpace(deployment.ItemId) && !string.IsNullOrWhiteSpace(deployment.Name))
                .GroupBy(static deployment => deployment.ItemId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(static group => group.Key, static group => group.First().Name, StringComparer.OrdinalIgnoreCase);

            await TryConvertStoredDeploymentSelectorsAsync(scope.ServiceProvider, siteService, deploymentNameMap);
        });

        return 5;
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
            ModelName = deploymentName,
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
        var site = await siteService.LoadSiteSettingsAsync();
        var updated = false;

        site.Alter<DefaultAIDeploymentSettings>(settings =>
            updated = TryPopulateDefaultDeploymentSettings(settings, connections, deployments));

        if (!updated)
        {
            return;
        }

        await siteService.UpdateSiteSettingsAsync(site);
    }

    private static async Task TryConvertDefaultDeploymentSettingsAsync(
        ISiteService siteService,
        IReadOnlyDictionary<string, string> deploymentNameMap)
    {
        var site = await siteService.LoadSiteSettingsAsync();
        var updated = false;

        site.Alter<DefaultAIDeploymentSettings>(settings =>
        {
            updated |= TryConvertDeploymentSelectorToName(deploymentNameMap, settings.DefaultChatDeploymentName, value => settings.DefaultChatDeploymentName = value);
            updated |= TryConvertDeploymentSelectorToName(deploymentNameMap, settings.DefaultUtilityDeploymentName, value => settings.DefaultUtilityDeploymentName = value);
            updated |= TryConvertDeploymentSelectorToName(deploymentNameMap, settings.DefaultEmbeddingDeploymentName, value => settings.DefaultEmbeddingDeploymentName = value);
            updated |= TryConvertDeploymentSelectorToName(deploymentNameMap, settings.DefaultImageDeploymentName, value => settings.DefaultImageDeploymentName = value);
            updated |= TryConvertDeploymentSelectorToName(deploymentNameMap, settings.DefaultSpeechToTextDeploymentName, value => settings.DefaultSpeechToTextDeploymentName = value);
            updated |= TryConvertDeploymentSelectorToName(deploymentNameMap, settings.DefaultTextToSpeechDeploymentName, value => settings.DefaultTextToSpeechDeploymentName = value);
        });

        if (!updated)
        {
            return;
        }

        await siteService.UpdateSiteSettingsAsync(site);
    }

    private static async Task TryConvertStoredDeploymentSelectorsAsync(
        IServiceProvider serviceProvider,
        ISiteService siteService,
        IReadOnlyDictionary<string, string> deploymentNameMap)
    {
        await TryConvertDefaultDeploymentSettingsAsync(siteService, deploymentNameMap);

        var profileCatalog = serviceProvider.GetRequiredService<IAIProfileStore>();
        var templateCatalog = serviceProvider.GetRequiredService<INamedSourceCatalog<AIProfileTemplate>>();
        var interactionCatalog = serviceProvider.GetRequiredService<ICatalog<ChatInteraction>>();

        foreach (var profile in await profileCatalog.GetAllAsync())
        {
            var updated = false;
            updated |= TryConvertDeploymentSelectorToName(deploymentNameMap, profile.ChatDeploymentName, value => profile.ChatDeploymentName = value);
            updated |= TryConvertDeploymentSelectorToName(deploymentNameMap, profile.UtilityDeploymentName, value => profile.UtilityDeploymentName = value);

            if (!updated)
            {
                continue;
            }

            await profileCatalog.UpdateAsync(profile);
        }

        foreach (var template in await templateCatalog.GetAllAsync())
        {
            var metadata = template.As<ProfileTemplateMetadata>();

            var updated = false;
            updated |= TryConvertDeploymentSelectorToName(deploymentNameMap, metadata.ChatDeploymentName, value => metadata.ChatDeploymentName = value);
            updated |= TryConvertDeploymentSelectorToName(deploymentNameMap, metadata.UtilityDeploymentName, value => metadata.UtilityDeploymentName = value);

            if (!updated)
            {
                continue;
            }

            template.Put(metadata);
            await templateCatalog.UpdateAsync(template);
        }

        foreach (var interaction in await interactionCatalog.GetAllAsync())
        {
            var updated = false;
            updated |= TryConvertDeploymentSelectorToName(deploymentNameMap, interaction.ChatDeploymentName, value => interaction.ChatDeploymentName = value);
            updated |= TryConvertDeploymentSelectorToName(deploymentNameMap, interaction.UtilityDeploymentName, value => interaction.UtilityDeploymentName = value);

            if (!updated)
            {
                continue;
            }

            await interactionCatalog.UpdateAsync(interaction);
        }
    }

    private static bool TryPopulateDefaultDeploymentSettings(
        DefaultAIDeploymentSettings settings,
        IEnumerable<AIProviderConnection> connections,
        IEnumerable<AIDeployment> deployments)
    {
        var updated = false;

        updated |= TryPopulateDefaultDeploymentId(
            settings.DefaultChatDeploymentName,
            value => settings.DefaultChatDeploymentName = value,
            FindDefaultDeploymentId(
                connections,
                deployments,
                AIDeploymentType.Chat,
                static connection => connection.GetLegacyChatDeploymentName()));

        updated |= TryPopulateDefaultDeploymentId(
            settings.DefaultUtilityDeploymentName,
            value => settings.DefaultUtilityDeploymentName = value,
            FindDefaultDeploymentId(
                connections,
                deployments,
                AIDeploymentType.Utility,
                static connection => connection.GetLegacyUtilityDeploymentName()));

        updated |= TryPopulateDefaultDeploymentId(
            settings.DefaultEmbeddingDeploymentName,
            value => settings.DefaultEmbeddingDeploymentName = value,
            FindDefaultDeploymentId(
                connections,
                deployments,
                AIDeploymentType.Embedding,
                static connection => connection.GetLegacyEmbeddingDeploymentName()));

        updated |= TryPopulateDefaultDeploymentId(
            settings.DefaultImageDeploymentName,
            value => settings.DefaultImageDeploymentName = value,
            FindDefaultDeploymentId(
                connections,
                deployments,
                AIDeploymentType.Image,
                static connection => connection.GetLegacyImageDeploymentName()));

        updated |= TryPopulateDefaultDeploymentId(
            settings.DefaultSpeechToTextDeploymentName,
            value => settings.DefaultSpeechToTextDeploymentName = value,
            FindDefaultDeploymentId(connections, deployments, AIDeploymentType.SpeechToText));

        updated |= TryPopulateDefaultDeploymentId(
            settings.DefaultTextToSpeechDeploymentName,
            value => settings.DefaultTextToSpeechDeploymentName = value,
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

    private static bool TryConvertDeploymentSelectorToName(
        IReadOnlyDictionary<string, string> deploymentNameMap,
        string currentValue,
        Action<string> assign)
    {
        if (string.IsNullOrWhiteSpace(currentValue) ||
            !deploymentNameMap.TryGetValue(currentValue, out var deploymentName) ||
            string.Equals(currentValue, deploymentName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        assign(deploymentName);
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
                .OrderBy(connection => connection.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var connection in orderedConnections)
            {
                var deploymentName = FindDefaultDeploymentId(type, connection.ItemId, connection.Name, deployments);

                if (!string.IsNullOrEmpty(deploymentName))
                {
                    return deploymentName;
                }
            }
        }

        return deployments
            .Where(deployment => deployment.SupportsType(type))
            .OrderByDescending(deployment => deployment.IsDefault)
            .ThenBy(deployment => deployment.ConnectionNameAlias ?? deployment.ConnectionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(deployment => deployment.Name, StringComparer.OrdinalIgnoreCase)
            .Select(deployment => deployment.Name)
            .FirstOrDefault();
    }

    private static string FindDefaultChatDeploymentName(AIProfile profile, IEnumerable<AIDeployment> deployments)
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

        return candidates.FirstOrDefault(deployment => deployment.IsDefault)?.Name
            ?? candidates.FirstOrDefault()?.Name;
    }
}
