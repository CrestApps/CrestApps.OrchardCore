using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Models;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Documents;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Settings;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIDeploymentTypeMigrations : DataMigration
{
    /// <summary>
    /// Creates a new .
    /// </summary>
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
                needsSave |= TryCreateDeployment(deploymentDoc, connection, connection.GetLegacyChatDeploymentName(), LegacyAIDeploymentPurpose.Chat);
                needsSave |= TryCreateDeployment(deploymentDoc, connection, connection.GetLegacyEmbeddingDeploymentName(), LegacyAIDeploymentPurpose.Embedding);
                needsSave |= TryCreateDeployment(deploymentDoc, connection, connection.GetLegacyImageDeploymentName(), LegacyAIDeploymentPurpose.Image);
                needsSave |= TryCreateDeployment(deploymentDoc, connection, connection.GetLegacyUtilityDeploymentName(), LegacyAIDeploymentPurpose.Utility);
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

    /// <summary>
    /// Updates the from1.
    /// </summary>
    public static int UpdateFrom1()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var profileCatalog = scope.ServiceProvider.GetRequiredService<IAIProfileStore>();
            var deploymentManager = scope.ServiceProvider.GetRequiredService<IAIDeploymentManager>();

            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AIDeploymentTypeMigrations>>();

            var deployments = (await deploymentManager.GetAllBySlotAsync(AIDeploymentSlotNames.Chat)).ToList();

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

    /// <summary>
    /// Updates the from2.
    /// </summary>
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

    /// <summary>
    /// Updates the from3.
    /// </summary>
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

    /// <summary>
    /// Updates the from4.
    /// </summary>
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

    /// <summary>
    /// Updates the from5.
    /// </summary>
    public static int UpdateFrom5()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var connectionDocManager = scope.ServiceProvider.GetRequiredService<IDocumentManager<DictionaryDocument<AIProviderConnection>>>();
            var deploymentDocManager = scope.ServiceProvider.GetRequiredService<IDocumentManager<DictionaryDocument<AIDeployment>>>();

            var connectionDoc = await connectionDocManager.GetOrCreateImmutableAsync();
            var deploymentDoc = await deploymentDocManager.GetOrCreateMutableAsync();
            var connectionsById = connectionDoc.Records.Values
                .Where(static connection => !string.IsNullOrWhiteSpace(connection.ItemId))
                .ToDictionary(static connection => connection.ItemId, StringComparer.OrdinalIgnoreCase);
            var needsSave = false;

            foreach (var deployment in deploymentDoc.Records.Values)
            {
                if (string.IsNullOrWhiteSpace(deployment.ConnectionName) ||
                    !connectionsById.TryGetValue(deployment.ConnectionName, out var connection))
                {
                    continue;
                }

                var normalizedConnectionName = string.IsNullOrWhiteSpace(connection.Name)
                    ? connection.ItemId
                    : connection.Name;

                if (string.Equals(deployment.ConnectionName, normalizedConnectionName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                deployment.ConnectionName = normalizedConnectionName;
                needsSave = true;
            }

            if (needsSave)
            {
                await deploymentDocManager.UpdateAsync(deploymentDoc);
            }
        });

        return 6;
    }

    /// <summary>
    /// Previously rewrote each stored deployment's legacy <c>Type</c> field into the <c>Purpose</c> field.
    /// </summary>
    /// <remarks>
    /// Both fields are gone. The framework now projects any legacy <c>Purpose</c>, <c>Capability</c>, or
    /// <c>Type</c> a stored record still carries onto model capabilities every time that record is read, so
    /// there is nothing left for this step to rewrite. It stays as a no-op because sites that have already
    /// run it record step 7, and removing it would renumber the ones that have not.
    /// </remarks>
    public static int UpdateFrom6()
        => 7;

    /// <summary>
    /// Writes the model capabilities implied by each stored deployment's legacy purpose into the document,
    /// so a deployment declares its capabilities directly instead of relying on the read-time projection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deserializing the document has already run <c>AIDeployment.OnDeserialized</c> over every record, so
    /// each one is in memory with the framework's projection applied and its legacy purpose cleared. Saving
    /// the document writes that down, which is what stops the dead legacy field from being carried around
    /// and makes the stored JSON say what the editors and the deployment list show.
    /// </para>
    /// <para>
    /// The framework's projection is not the whole mapping, though. The chat and utility purposes also
    /// stood for tool calling and streaming -- see
    /// <see cref="LegacyAIDeploymentPurposeExtensions.ToDeclaredFeatureNames"/> -- and the framework has no
    /// way to infer those, because the purpose never named them separately. Nor can they be recovered from
    /// the projected record: once the projection has run, a migrated chat deployment and a hand-authored
    /// text-generation-only one are identical. So the purposes are read back out of the stored JSON first,
    /// before this step overwrites it, and applied to the records.
    /// </para>
    /// <para>
    /// Deployments that come from configuration rather than the store are untouched, because they are not
    /// in this document and are read-only. They are projected on every read, as before.
    /// </para>
    /// </remarks>
    public static int UpdateFrom7()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var deploymentDocManager = scope.ServiceProvider.GetRequiredService<IDocumentManager<DictionaryDocument<AIDeployment>>>();
            var deploymentDoc = await deploymentDocManager.GetOrCreateMutableAsync();

            if (deploymentDoc.Records.Count == 0)
            {
                return;
            }

            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AIDeploymentTypeMigrations>>();
            var storedPurposes = await ReadStoredDeploymentPurposesAsync(scope.ServiceProvider, logger);

            var widenedCount = 0;

            foreach (var (recordKey, deployment) in deploymentDoc.Records)
            {
                if (storedPurposes.TryGetValue(recordKey, out var purpose) && purpose.ApplyTo(deployment))
                {
                    widenedCount++;
                }
            }

            // Only worth reporting; every record is rewritten either way, since a record that already
            // declared its capabilities simply round-trips unchanged.
            var declaredCount = deploymentDoc.Records.Values
                .Count(deployment => deployment.TryGet<AIDeploymentMetadata>(out var metadata) && metadata.Features is { Length: > 0 });

            await deploymentDocManager.UpdateAsync(deploymentDoc);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Persisted model capabilities for {DeclaredCount} of {TotalCount} stored AI deployments, {WidenedCount} of which gained capabilities their legacy purpose implied.",
                    declaredCount,
                    deploymentDoc.Records.Count,
                    widenedCount);
            }
        });

        return 8;
    }

    /// <summary>
    /// Reads the legacy purpose each stored deployment still carries, keyed by the record key the
    /// deployment document uses.
    /// </summary>
    /// <remarks>
    /// This deliberately goes around the document manager. Its deserialization is what consumes the legacy
    /// purpose and then clears it, so by the time a typed record exists the purpose is already gone.
    /// </remarks>
    private static async Task<Dictionary<string, LegacyAIDeploymentPurpose>> ReadStoredDeploymentPurposesAsync(
        IServiceProvider serviceProvider,
        ILogger logger)
    {
        var purposes = new Dictionary<string, LegacyAIDeploymentPurpose>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var store = serviceProvider.GetRequiredService<IStore>();
            var dbConnectionAccessor = serviceProvider.GetRequiredService<IDbConnectionAccessor>();

            var dialect = store.Configuration.SqlDialect;
            var documentTableName = store.Configuration.TableNameConvention.GetDocumentTable(string.Empty);
            var table = $"{store.Configuration.TablePrefix}{documentTableName}";

            var sqlBuilder = new SqlBuilder(store.Configuration.TablePrefix, dialect);
            sqlBuilder.AddSelector(dialect.QuoteForColumnName(nameof(Document.Type)));
            sqlBuilder.AddSelector("," + dialect.QuoteForColumnName(nameof(Document.Content)));
            sqlBuilder.From(dialect.QuoteForTableName(table, store.Configuration.Schema));

            // The row is picked out in memory rather than with a LIKE on the type column. A generic
            // document's type name contains square brackets, which SQL Server reads as a character class
            // in a LIKE pattern, so such a filter quietly matches nothing there. The document table holds
            // one row per document type, so reading it whole costs nothing worth saving.
            var typePrefix = GetDeploymentDocumentTypePrefix();

            await using var connection = dbConnectionAccessor.CreateConnection();
            await connection.OpenAsync();

            foreach (var document in await connection.QueryAsync<Document>(sqlBuilder.ToSqlString()))
            {
                if (document.Type is null ||
                    !document.Type.StartsWith(typePrefix, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(document.Content) ||
                    JsonNode.Parse(document.Content)?["Records"] is not JsonObject recordsObject)
                {
                    continue;
                }

                foreach (var (recordKey, recordNode) in recordsObject)
                {
                    if (LegacyAIDeploymentMigrationHelper.TryReadLegacyPurpose(recordNode, out var purpose))
                    {
                        purposes[recordKey] = purpose;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // The capabilities the framework projected are already on every record; only the tool calling
            // and streaming a chat or utility purpose additionally implied are lost. Failing the migration
            // over that would leave the site on an older schema version, which is the worse outcome.
            logger.LogWarning(ex, "Could not read the stored AI deployment purposes. Migrated chat and utility deployments may need tool calling and streaming enabled by hand.");
        }

        return purposes;
    }

    /// <summary>
    /// Gets the leading portion of the deployment document's type name, stopping before the assembly
    /// version so the row matches whether or not the store simplified the name it wrote.
    /// </summary>
    private static string GetDeploymentDocumentTypePrefix()
    {
        var fullName = typeof(DictionaryDocument<AIDeployment>).FullName;
        var versionIndex = fullName.IndexOf(", Version=", StringComparison.Ordinal);

        return versionIndex < 0
            ? fullName
            : fullName[..versionIndex];
    }

    private static bool TryCreateDeployment(
        DictionaryDocument<AIDeployment> deploymentDoc,
        AIProviderConnection connection,
        string deploymentName,
        LegacyAIDeploymentPurpose type)
    {
        if (string.IsNullOrWhiteSpace(deploymentName))
        {
            return false;
        }

        var exists = deploymentDoc.Records.Values.Any(d =>
        d.ClientName == connection.ClientName &&
            (string.Equals(d.ConnectionName, connection.ItemId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(d.ConnectionName, connection.Name, StringComparison.OrdinalIgnoreCase)) &&

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
            ConnectionName = string.IsNullOrWhiteSpace(connection.Name)
                ? connection.ItemId
                : connection.Name,
            CreatedUtc = connection.CreatedUtc,
            Author = connection.Author,
            OwnerId = connection.OwnerId,
        };

        // The purpose this deployment is being created for is declared as the capabilities that replaced it,
        // so the record is written in the shape the framework reads today rather than a legacy one it would
        // have to keep projecting.
        type.ApplyTo(deployment);

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
            var metadata = template.GetOrCreate<ProfileTemplateMetadata>();

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
            LegacyAIDeploymentPurpose.Chat,
            static connection => connection.GetLegacyChatDeploymentName()));

        updated |= TryPopulateDefaultDeploymentId(
            settings.DefaultUtilityDeploymentName,
            value => settings.DefaultUtilityDeploymentName = value,
            FindDefaultDeploymentId(
            connections,
            deployments,
            LegacyAIDeploymentPurpose.Utility,
            static connection => connection.GetLegacyUtilityDeploymentName()));

        updated |= TryPopulateDefaultDeploymentId(
            settings.DefaultEmbeddingDeploymentName,
            value => settings.DefaultEmbeddingDeploymentName = value,
            FindDefaultDeploymentId(
            connections,
            deployments,
            LegacyAIDeploymentPurpose.Embedding,
            static connection => connection.GetLegacyEmbeddingDeploymentName()));

        updated |= TryPopulateDefaultDeploymentId(
            settings.DefaultImageDeploymentName,
            value => settings.DefaultImageDeploymentName = value,
            FindDefaultDeploymentId(
            connections,
            deployments,
            LegacyAIDeploymentPurpose.Image,
            static connection => connection.GetLegacyImageDeploymentName()));

        updated |= TryPopulateDefaultDeploymentId(
            settings.DefaultSpeechToTextDeploymentName,
            value => settings.DefaultSpeechToTextDeploymentName = value,
            FindDefaultDeploymentId(connections, deployments, LegacyAIDeploymentPurpose.SpeechToText));

        updated |= TryPopulateDefaultDeploymentId(
            settings.DefaultTextToSpeechDeploymentName,
            value => settings.DefaultTextToSpeechDeploymentName = value,
            FindDefaultDeploymentId(connections, deployments, LegacyAIDeploymentPurpose.TextToSpeech));

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
        LegacyAIDeploymentPurpose type,
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
            .Where(deployment => type.IsSupportedBy(deployment))
            .OrderBy(deployment => deployment.ConnectionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(deployment => deployment.Name, StringComparer.OrdinalIgnoreCase)
            .Select(deployment => deployment.Name)
            .FirstOrDefault();
    }

    private static string FindDefaultChatDeploymentName(AIProfile profile, IEnumerable<AIDeployment> deployments)
    {
        var legacyConnectionName = profile.GetLegacyConnectionName();

        return FindDefaultDeploymentId(LegacyAIDeploymentPurpose.Chat, legacyConnectionName, legacyConnectionName, deployments);
    }

    private static string FindDefaultDeploymentId(
        LegacyAIDeploymentPurpose type,
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
                type.IsSupportedBy(deployment) &&
                (string.Equals(deployment.ConnectionName, connectionId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(deployment.ConnectionName, connectionAlias, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return candidates.FirstOrDefault()?.Name;
    }
}
