using System.Text.Json;
using CrestApps.AI.Models;
using CrestApps.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.AI.Chat.Handlers;

public sealed class DataSourceChatInteractionSettingsHandler : IChatInteractionSettingsHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public DataSourceChatInteractionSettingsHandler(
        IServiceProvider serviceProvider,
        ILogger<DataSourceChatInteractionSettingsHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task UpdatingAsync(ChatInteraction interaction, JsonElement settings)
    {
        var dataSourceId = GetString(settings, "dataSourceId");
        var isInScope = GetBool(settings, "isInScope") ?? false;

        if (string.IsNullOrWhiteSpace(dataSourceId))
        {
            interaction.Alter<DataSourceMetadata>(metadata => metadata.DataSourceId = null);
            interaction.Alter<AIDataSourceRagMetadata>(metadata =>
            {
                metadata.Strictness = null;
                metadata.TopNDocuments = null;
                metadata.IsInScope = isInScope;
                metadata.Filter = null;
            });
            return;
        }

        var dataSourceCatalog = _serviceProvider.GetService<ICatalog<AIDataSource>>();

        if (dataSourceCatalog == null)
        {
            _logger.LogDebug("Skipping chat interaction data source settings because no AI data source catalog is registered.");
            return;
        }

        var dataSource = await dataSourceCatalog.FindByIdAsync(dataSourceId);

        if (dataSource == null)
        {
            _logger.LogWarning("Chat interaction data source '{DataSourceId}' was not found while saving settings.", dataSourceId);
            interaction.Alter<DataSourceMetadata>(metadata => metadata.DataSourceId = null);
            interaction.Alter<AIDataSourceRagMetadata>(metadata =>
            {
                metadata.Strictness = null;
                metadata.TopNDocuments = null;
                metadata.IsInScope = isInScope;
                metadata.Filter = null;
            });
            return;
        }

        interaction.Alter<DataSourceMetadata>(metadata =>
        {
            metadata.DataSourceId = dataSource.ItemId;
        });

        interaction.Alter<AIDataSourceRagMetadata>(metadata =>
        {
            metadata.Strictness = GetInt(settings, "strictness");
            metadata.TopNDocuments = GetInt(settings, "topNDocuments");
            metadata.IsInScope = isInScope;
            metadata.Filter = GetString(settings, "filter");
        });
    }

    public Task UpdatedAsync(ChatInteraction interaction, JsonElement settings)
        => Task.CompletedTask;

    private static string GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }

        return null;
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            return number;
        }

        if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out number))
        {
            return number;
        }

        return null;
    }

    private static bool? GetBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.String &&
            bool.TryParse(property.GetString(), out var value))
        {
            return value;
        }

        return null;
    }
}
