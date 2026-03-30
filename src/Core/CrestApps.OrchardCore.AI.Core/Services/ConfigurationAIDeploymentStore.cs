using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// A decorator around <see cref="DefaultAIDeploymentStore"/> that merges
/// configuration-sourced deployments (from appsettings.json) into all read operations.
/// Write operations are delegated directly to the underlying store.
/// </summary>
internal sealed class ConfigurationAIDeploymentStore : INamedSourceCatalog<AIDeployment>
{
    private readonly DefaultAIDeploymentStore _inner;
    private readonly IShellConfiguration _shellConfiguration;
    private readonly IOptions<AIProviderOptions> _providerOptions;
    private readonly IOptions<AIOptions> _aiOptions;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    private IReadOnlyCollection<AIDeployment> _configDeployments;

    public ConfigurationAIDeploymentStore(
        DefaultAIDeploymentStore inner,
        IShellConfiguration shellConfiguration,
        IOptions<AIProviderOptions> providerOptions,
        IOptions<AIOptions> aiOptions,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<ConfigurationAIDeploymentStore> logger)
    {
        _inner = inner;
        _shellConfiguration = shellConfiguration;
        _providerOptions = providerOptions;
        _aiOptions = aiOptions;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    public async ValueTask<AIDeployment> FindByIdAsync(string id)
    {
        var result = await _inner.FindByIdAsync(id);

        if (result != null)
        {
            return result;
        }

        return FindConfigDeployment(d => string.Equals(d.ItemId, id, StringComparison.OrdinalIgnoreCase));
    }

    public async ValueTask<IReadOnlyCollection<AIDeployment>> GetAllAsync()
    {
        var dbRecords = await _inner.GetAllAsync();
        var configRecords = GetConfigDeployments();

        if (configRecords.Count == 0)
        {
            return dbRecords;
        }

        return Merge(dbRecords, configRecords);
    }

    public async ValueTask<IReadOnlyCollection<AIDeployment>> GetAsync(IEnumerable<string> ids)
    {
        var dbRecords = await _inner.GetAsync(ids);

        var idSet = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var foundIds = dbRecords.Select(r => r.ItemId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingIds = idSet.Except(foundIds).ToList();

        if (missingIds.Count == 0)
        {
            return dbRecords;
        }

        var configMatches = GetConfigDeployments()
            .Where(d => missingIds.Contains(d.ItemId))
            .ToList();

        if (configMatches.Count == 0)
        {
            return dbRecords;
        }

        return Merge(dbRecords, configMatches);
    }

    public async ValueTask<PageResult<AIDeployment>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
        where TQuery : QueryContext
    {
        var configRecords = GetConfigDeployments();

        if (configRecords.Count == 0)
        {
            return await _inner.PageAsync(page, pageSize, context);
        }

        var allRecords = await GetAllAsync();
        IEnumerable<AIDeployment> filtered = allRecords;

        if (context != null)
        {
            filtered = ApplyFilters(context, filtered);
        }

        var totalCount = filtered.Count();
        var skip = (page - 1) * pageSize;

        return new PageResult<AIDeployment>
        {
            Count = totalCount,
            Entries = filtered.Skip(skip).Take(pageSize).ToArray(),
        };
    }

    public async ValueTask<AIDeployment> FindByNameAsync(string name)
    {
        var result = await _inner.FindByNameAsync(name);

        if (result != null)
        {
            return result;
        }

        return FindConfigDeployment(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public async ValueTask<IReadOnlyCollection<AIDeployment>> GetAsync(string source)
    {
        var dbRecords = await _inner.GetAsync(source);
        var configMatches = GetConfigDeployments()
            .Where(d => string.Equals(d.Source, source, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (configMatches.Count == 0)
        {
            return dbRecords;
        }

        return Merge(dbRecords, configMatches);
    }

    public async ValueTask<AIDeployment> GetAsync(string name, string source)
    {
        var result = await _inner.GetAsync(name, source);

        if (result != null)
        {
            return result;
        }

        return FindConfigDeployment(d =>
            string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(d.Source, source, StringComparison.OrdinalIgnoreCase));
    }

    public ValueTask<bool> DeleteAsync(AIDeployment entry)
        => _inner.DeleteAsync(entry);

    public ValueTask CreateAsync(AIDeployment entry)
        => _inner.CreateAsync(entry);

    public ValueTask UpdateAsync(AIDeployment entry)
        => _inner.UpdateAsync(entry);

    public ValueTask SaveChangesAsync()
        => _inner.SaveChangesAsync();

    private AIDeployment FindConfigDeployment(Func<AIDeployment, bool> predicate)
    {
        var match = GetConfigDeployments().FirstOrDefault(predicate);

        return match?.Clone();
    }

    private IReadOnlyCollection<AIDeployment> GetConfigDeployments()
    {
        if (_configDeployments != null)
        {
            return _configDeployments;
        }

        var deployments = new List<AIDeployment>();

        try
        {
            ReadConnectionDeployments(deployments);
            ReadStandaloneDeployments(deployments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading AI deployment configuration.");
        }

        _configDeployments = deployments;

        return _configDeployments;
    }

    /// <summary>
    /// Reads deployments from the Deployments array within connection entries
    /// in CrestApps_AI:Providers:{ProviderName}:Connections:{ConnectionId}.
    /// </summary>
    private void ReadConnectionDeployments(List<AIDeployment> deployments)
    {
        foreach (var (providerName, provider) in _providerOptions.Value.Providers)
        {
            if (provider.Connections == null)
            {
                continue;
            }

            foreach (var (connectionId, connectionEntry) in provider.Connections)
            {
                if (!connectionEntry.TryGetValue("Deployments", out var deploymentsObj))
                {
                    continue;
                }

                if (deploymentsObj is not JsonElement deploymentsElement || deploymentsElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var entryElement in deploymentsElement.EnumerateArray())
                {
                    var deployment = ParseConnectionDeploymentEntry(entryElement, providerName, connectionId, connectionEntry);

                    if (deployment != null)
                    {
                        deployments.Add(deployment);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Reads deployments from the standalone CrestApps_AI:Deployments section
    /// for contained-connection providers (e.g., AzureSpeech / Azure AI Services).
    /// </summary>
    private void ReadStandaloneDeployments(List<AIDeployment> deployments)
    {
        var section = _shellConfiguration.GetSection("CrestApps_AI:Deployments");

        if (section is null)
        {
            return;
        }

        JsonNode deploymentsNode;

        try
        {
            var deploymentsElement = JsonSerializer.Deserialize<JsonElement>(section.AsJsonNode());
            deploymentsNode = JsonNode.Parse(deploymentsElement.GetRawText());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid AI deployment configuration found in CrestApps_AI:Deployments.");
            return;
        }

        switch (deploymentsNode)
        {
            case JsonArray deploymentArray:
                ReadStandaloneDeploymentsFromArray(deploymentArray, deployments);
                return;

            case JsonObject deploymentObject:
                ReadStandaloneDeploymentsFromObject(deploymentObject, deployments);
                return;

            case null:
                return;

            default:
                _logger.LogWarning("The CrestApps_AI:Deployments configuration must be either an array or an object.");
                return;
        }
    }

    private void ReadStandaloneDeploymentsFromArray(JsonArray deploymentArray, List<AIDeployment> deployments)
    {
        foreach (var deploymentNode in deploymentArray)
        {
            if (deploymentNode is not JsonObject deploymentObject)
            {
                _logger.LogWarning("A standalone AI deployment entry in CrestApps_AI:Deployments is not a valid object. Skipping.");
                continue;
            }

            var deployment = CreateStandaloneDeployment(ParseStandaloneDeploymentEntry(deploymentObject));

            if (deployment != null)
            {
                deployments.Add(deployment);
            }
        }
    }

    private void ReadStandaloneDeploymentsFromObject(JsonObject deploymentObject, List<AIDeployment> deployments)
    {
        foreach (var (providerName, providerDeploymentsNode) in deploymentObject)
        {
            if (providerDeploymentsNode is not JsonArray providerDeployments)
            {
                _logger.LogWarning("The provider '{ProviderName}' in CrestApps_AI:Deployments must contain an array of deployments. Skipping.", providerName);
                continue;
            }

            foreach (var deploymentNode in providerDeployments)
            {
                if (deploymentNode is not JsonObject standaloneDeploymentObject)
                {
                    _logger.LogWarning("A standalone AI deployment entry for provider '{ProviderName}' is not a valid object. Skipping.", providerName);
                    continue;
                }

                var deployment = CreateStandaloneDeployment(ParseStandaloneDeploymentEntry(standaloneDeploymentObject, providerName));

                if (deployment != null)
                {
                    deployments.Add(deployment);
                }
            }
        }
    }

    private AIDeployment ParseConnectionDeploymentEntry(
        JsonElement element,
        string providerName,
        string connectionId,
        AIProviderConnectionEntry connectionEntry)
    {
        var name = element.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : null;
        var modelName = element.TryGetProperty("ModelName", out var modelNameProp)
            ? modelNameProp.GetString()
            : name;

        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("A deployment entry in connection '{ConnectionId}' of provider '{ProviderName}' is missing a Name. Skipping.", connectionId, providerName);
            return null;
        }

        if (!TryGetDeploymentType(element, out var type))
        {
            _logger.LogWarning("Deployment entry '{Name}' in connection '{ConnectionId}' of provider '{ProviderName}' has an invalid or missing Type. Skipping.", name, connectionId, providerName);
            return null;
        }

        var isDefault = element.TryGetProperty("IsDefault", out var defaultProp) && defaultProp.ValueKind == JsonValueKind.True;

        var connectionNameAlias = connectionEntry.TryGetValue("ConnectionNameAlias", out var aliasObj)
            ? aliasObj?.ToString()
            : null;

        return new AIDeployment
        {
            ItemId = GenerateConfigDeploymentId(providerName, connectionId, name),
            Name = name,
            ModelName = modelName,
            Source = providerName,
            ConnectionName = connectionId,
            ConnectionNameAlias = connectionNameAlias,
            Type = type,
            IsDefault = isDefault,
        };
    }

    private static AIDeploymentConfigurationEntry ParseStandaloneDeploymentEntry(JsonObject deploymentObject, string providerName = null)
    {
        var entry = new AIDeploymentConfigurationEntry
        {
            ProviderName = GetStringValue(deploymentObject["ClientName"]) ?? GetStringValue(deploymentObject["ProviderName"]) ?? providerName,
            Name = GetStringValue(deploymentObject["Name"]),
            ModelName = GetStringValue(deploymentObject["ModelName"]) ?? GetStringValue(deploymentObject["Name"]),
            IsDefault = GetBooleanValue(deploymentObject["IsDefault"]),
            Properties = BuildStandaloneDeploymentProperties(deploymentObject),
        };

        if (TryGetDeploymentType(deploymentObject["Type"], out var deploymentType))
        {
            entry.Type = deploymentType;
        }

        return entry;
    }

    private AIDeployment CreateStandaloneDeployment(AIDeploymentConfigurationEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.ProviderName))
        {
            _logger.LogWarning("A standalone AI deployment entry is missing a ProviderName. Skipping.");
            return null;
        }

        if (!_aiOptions.Value.Deployments.TryGetValue(entry.ProviderName, out var providerEntry))
        {
            _logger.LogWarning("Unknown deployment provider '{ProviderName}' in CrestApps_AI:Deployments configuration. Skipping.", entry.ProviderName);
            return null;
        }

        if (!providerEntry.SupportsContainedConnection)
        {
            _logger.LogWarning("Provider '{ProviderName}' does not support contained connections. Use the Providers:Connections section instead.", entry.ProviderName);
            return null;
        }

        if (string.IsNullOrWhiteSpace(entry.Name))
        {
            _logger.LogWarning("A deployment entry for provider '{ProviderName}' is missing a Name. Skipping.", entry.ProviderName);
            return null;
        }

        if (!entry.Type.IsValidSelection())
        {
            _logger.LogWarning("Deployment entry '{Name}' for provider '{ProviderName}' has an invalid Type. Skipping.", entry.Name, entry.ProviderName);
            return null;
        }

        if (entry.Properties?.Count > 0)
        {
            ProtectApiKeys(entry.Properties, entry.ProviderName, entry.Name);
        }

        return new AIDeployment
        {
            ItemId = GenerateConfigDeploymentId(entry.ProviderName, connectionName: null, entry.Name),
            Name = entry.Name,
            ModelName = entry.ModelName,
            Source = entry.ProviderName,
            Type = entry.Type,
            IsDefault = entry.IsDefault,
            Properties = entry.Properties?.Count > 0
                ? (JsonObject)entry.Properties.DeepClone()
                : null,
        };
    }

    private static bool TryGetDeploymentType(JsonElement element, out AIDeploymentType type)
    {
        type = AIDeploymentType.None;

        if (!element.TryGetProperty("Type", out var typeElement))
        {
            return false;
        }

        return TryParseDeploymentTypeElement(typeElement, out type);
    }

    private static bool TryParseDeploymentTypeElement(JsonElement typeElement, out AIDeploymentType type)
    {
        type = AIDeploymentType.None;

        if (typeElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in typeElement.EnumerateArray())
            {
                var typeName = item.GetString();

                if (string.IsNullOrWhiteSpace(typeName) ||
                    !Enum.TryParse<AIDeploymentType>(typeName, ignoreCase: true, out var parsedType) ||
                    parsedType == AIDeploymentType.None)
                {
                    type = AIDeploymentType.None;
                    return false;
                }

                type |= parsedType;
            }

            return type.IsValidSelection();
        }

        if (typeElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var typeNameValue = typeElement.GetString();

        return !string.IsNullOrWhiteSpace(typeNameValue) &&
            Enum.TryParse(typeNameValue, ignoreCase: true, out type) &&
            type.IsValidSelection();
    }

    private static bool TryGetDeploymentType(JsonNode typeNode, out AIDeploymentType type)
    {
        type = AIDeploymentType.None;

        if (typeNode is null)
        {
            return false;
        }

        if (typeNode is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is null)
                {
                    type = AIDeploymentType.None;
                    return false;
                }

                var typeName = GetStringValue(item);

                if (string.IsNullOrWhiteSpace(typeName) ||
                    !Enum.TryParse<AIDeploymentType>(typeName, ignoreCase: true, out var parsedType) ||
                    parsedType == AIDeploymentType.None)
                {
                    type = AIDeploymentType.None;
                    return false;
                }

                type |= parsedType;
            }

            return type.IsValidSelection();
        }

        var singleTypeName = GetStringValue(typeNode);

        return !string.IsNullOrWhiteSpace(singleTypeName) &&
            Enum.TryParse(singleTypeName, ignoreCase: true, out type) &&
            type.IsValidSelection();
    }

    private static JsonObject BuildStandaloneDeploymentProperties(JsonObject deploymentObject)
    {
        JsonObject properties = null;

        if (deploymentObject["Properties"] is JsonObject explicitProperties)
        {
            properties = (JsonObject)explicitProperties.DeepClone();
        }

        foreach (var (key, value) in deploymentObject)
        {
            if (IsStandaloneDeploymentMetadataKey(key))
            {
                continue;
            }

            properties ??= [];
            properties[key] = value?.DeepClone();
        }

        return properties;
    }

    private void ProtectApiKeys(JsonObject properties, string providerName, string deploymentName)
    {
        foreach (var (key, value) in properties)
        {
            if (value is JsonObject nestedObject)
            {
                ProtectApiKeys(nestedObject, providerName, deploymentName);
                continue;
            }

            if (!string.Equals(key, "ApiKey", StringComparison.OrdinalIgnoreCase) || value is not JsonValue apiKeyValue)
            {
                continue;
            }

            if (!apiKeyValue.TryGetValue<string>(out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
            {
                continue;
            }

            try
            {
                var protector = _dataProtectionProvider.CreateProtector(AIConstants.ConnectionProtectorName);
                properties[key] = protector.Protect(apiKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to protect API key for deployment '{Name}' of provider '{ProviderName}'.", deploymentName, providerName);
            }
        }
    }

    private static bool IsStandaloneDeploymentMetadataKey(string key)
        => string.Equals(key, "ProviderName", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "Name", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "Type", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "IsDefault", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "Properties", StringComparison.OrdinalIgnoreCase);

    private static string GetStringValue(JsonNode node)
    {
        if (node is not JsonValue jsonValue || !jsonValue.TryGetValue<string>(out var value))
        {
            return null;
        }

        return value;
    }

    private static bool GetBooleanValue(JsonNode node)
    {
        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<bool>(out var boolValue))
            {
                return boolValue;
            }

            if (jsonValue.TryGetValue<string>(out var stringValue) &&
                bool.TryParse(stringValue, out boolValue))
            {
                return boolValue;
            }
        }

        return false;
    }

    private static string GenerateConfigDeploymentId(string providerName, string connectionName, string deploymentName)
    {
        var input = $"{providerName}:{connectionName ?? string.Empty}:{deploymentName}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input.ToLowerInvariant()));

        return $"cfg{Convert.ToHexStringLower(hash)[..22]}";
    }

    private static List<AIDeployment> Merge(IReadOnlyCollection<AIDeployment> primary, IReadOnlyCollection<AIDeployment> secondary)
    {
        var merged = new List<AIDeployment>(primary.Count + secondary.Count);
        merged.AddRange(primary);
        merged.AddRange(secondary);

        return merged;
    }

    private static List<AIDeployment> Merge(IReadOnlyCollection<AIDeployment> primary, List<AIDeployment> secondary)
    {
        var merged = new List<AIDeployment>(primary.Count + secondary.Count);
        merged.AddRange(primary);
        merged.AddRange(secondary);

        return merged;
    }

    private static IEnumerable<AIDeployment> ApplyFilters(QueryContext context, IEnumerable<AIDeployment> records)
    {
        if (!string.IsNullOrEmpty(context.Source))
        {
            records = records.Where(x => string.Equals(x.Source, context.Source, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(context.Name))
        {
            records = records.Where(x => x.Name.Contains(context.Name, StringComparison.OrdinalIgnoreCase));
        }

        if (context.Sorted)
        {
            records = records.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase);
        }

        return records;
    }
}
