using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.AI.Deployments;
using CrestApps.AI.Models;
using CrestApps.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.AI.Services;

/// <summary>
/// Decorates a persisted AI deployment store with configuration-backed deployments from appsettings.json.
/// Read operations return the merged result while write operations continue to target the persisted store only.
/// </summary>
public sealed class ConfigurationAIDeploymentCatalog : IAIDeploymentStore
{
    private const string _connectionProtectorName = "AIProviderConnection";

    private readonly IAIDeploymentStore _inner;
    private readonly IConfiguration _configuration;
    private readonly AIProviderOptions _providerOptions;
    private readonly AIOptions _aiOptions;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    private IReadOnlyCollection<AIDeployment> _configDeployments;

    public ConfigurationAIDeploymentCatalog(
        IAIDeploymentStore inner,
        IConfiguration configuration,
        IOptions<AIProviderOptions> providerOptions,
        IOptions<AIOptions> aiOptions,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<ConfigurationAIDeploymentCatalog> logger)
    {
        _inner = inner;
        _configuration = configuration;
        _providerOptions = providerOptions.Value;
        _aiOptions = aiOptions.Value;
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

        return FindConfigDeployment(deployment => string.Equals(deployment.ItemId, id, StringComparison.OrdinalIgnoreCase));
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
        var requestedIds = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var foundIds = dbRecords.Select(static deployment => deployment.ItemId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingIds = requestedIds.Except(foundIds).ToList();

        if (missingIds.Count == 0)
        {
            return dbRecords;
        }

        var configMatches = GetConfigDeployments()
            .Where(deployment => missingIds.Contains(deployment.ItemId))
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
        var filtered = ApplyFilters(context, allRecords);
        var skip = (page - 1) * pageSize;

        return new PageResult<AIDeployment>
        {
            Count = filtered.Count(),
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

        return FindConfigDeployment(deployment => string.Equals(deployment.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public async ValueTask<IReadOnlyCollection<AIDeployment>> GetAsync(string source)
    {
        var dbRecords = await _inner.GetAsync(source);
        var configMatches = GetConfigDeployments()
            .Where(deployment => string.Equals(deployment.Source, source, StringComparison.OrdinalIgnoreCase))
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

        return FindConfigDeployment(deployment =>
            string.Equals(deployment.Name, name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(deployment.Source, source, StringComparison.OrdinalIgnoreCase));
    }

    public ValueTask<bool> DeleteAsync(AIDeployment entry) => _inner.DeleteAsync(entry);

    public ValueTask CreateAsync(AIDeployment entry) => _inner.CreateAsync(entry);

    public ValueTask UpdateAsync(AIDeployment entry) => _inner.UpdateAsync(entry);

    public ValueTask SaveChangesAsync() => _inner.SaveChangesAsync();

    private AIDeployment FindConfigDeployment(Func<AIDeployment, bool> predicate)
        => GetConfigDeployments().FirstOrDefault(predicate)?.Clone();

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

    private void ReadConnectionDeployments(List<AIDeployment> deployments)
    {
        foreach (var (providerName, provider) in _providerOptions.Providers)
        {
            if (provider.Connections is null)
            {
                continue;
            }

            foreach (var (connectionId, connectionEntry) in provider.Connections)
            {
                if (!connectionEntry.TryGetValue("Deployments", out var deploymentsObject))
                {
                    continue;
                }

                var deploymentArray = ConvertToJsonArray(deploymentsObject);

                if (deploymentArray is null)
                {
                    continue;
                }

                foreach (var deploymentNode in deploymentArray)
                {
                    if (deploymentNode is not JsonObject deploymentObject)
                    {
                        continue;
                    }

                    var deployment = ParseConnectionDeploymentEntry(deploymentObject, providerName, connectionId, connectionEntry);

                    if (deployment != null)
                    {
                        deployments.Add(deployment);
                    }
                }
            }
        }
    }

    private void ReadStandaloneDeployments(List<AIDeployment> deployments)
    {
        var section = GetDeploymentsSection();

        if (section is null)
        {
            return;
        }

        var deploymentsNode = ReadConfigurationNode(section);

        switch (deploymentsNode)
        {
            case JsonArray deploymentArray:
                ReadStandaloneDeploymentsFromArray(deploymentArray, deployments);
                break;
            case JsonObject deploymentObject:
                ReadStandaloneDeploymentsFromObject(deploymentObject, deployments);
                break;
            case null:
                break;
            default:
                _logger.LogWarning("The AI deployments configuration must be either an array or an object.");
                break;
        }
    }

    private IConfigurationSection GetDeploymentsSection()
    {
        var section = _configuration.GetSection("CrestApps:AI:Deployments");

        if (section.Exists())
        {
            return section;
        }

        section = _configuration.GetSection("CrestApps_AI:Deployments");

        return section.Exists() ? section : null;
    }

    private void ReadStandaloneDeploymentsFromArray(JsonArray deploymentArray, List<AIDeployment> deployments)
    {
        foreach (var deploymentNode in deploymentArray)
        {
            if (deploymentNode is not JsonObject deploymentObject)
            {
                _logger.LogWarning("A standalone AI deployment entry is not a valid object. Skipping.");
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
                _logger.LogWarning("The provider '{ProviderName}' must contain an array of deployments. Skipping.", providerName);
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
        JsonObject deploymentObject,
        string providerName,
        string connectionId,
        AIProviderConnectionEntry connectionEntry)
    {
        var name = GetStringValue(deploymentObject["Name"]);
        var modelName = GetStringValue(deploymentObject["ModelName"]) ?? name;

        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("A deployment entry in connection '{ConnectionId}' of provider '{ProviderName}' is missing a Name. Skipping.", connectionId, providerName);
            return null;
        }

        if (!TryGetDeploymentType(deploymentObject["Type"], out var type))
        {
            _logger.LogWarning("Deployment entry '{Name}' in connection '{ConnectionId}' of provider '{ProviderName}' has an invalid or missing Type. Skipping.", name, connectionId, providerName);
            return null;
        }

        var connectionNameAlias = connectionEntry.TryGetValue("ConnectionNameAlias", out var aliasValue)
            ? aliasValue?.ToString()
            : null;

        return new AIDeployment
        {
            ItemId = AIConfigurationRecordIds.CreateDeploymentId(providerName, connectionId, name),
            Name = name,
            ModelName = modelName,
            Source = providerName,
            ConnectionName = connectionId,
            ConnectionNameAlias = connectionNameAlias,
            Type = type,
            IsDefault = GetBooleanValue(deploymentObject["IsDefault"]),
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
            _logger.LogWarning("A standalone AI deployment entry is missing a ClientName. Skipping.");
            return null;
        }

        if (!_aiOptions.Deployments.TryGetValue(entry.ProviderName, out var providerEntry))
        {
            _logger.LogWarning("Unknown deployment provider '{ProviderName}' in AI deployment configuration. Skipping.", entry.ProviderName);
            return null;
        }

        if (!providerEntry.SupportsContainedConnection)
        {
            _logger.LogWarning("Provider '{ProviderName}' does not support contained connections. Use connection-scoped deployments instead.", entry.ProviderName);
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
            ItemId = AIConfigurationRecordIds.CreateDeploymentId(entry.ProviderName, connectionName: null, entry.Name),
            Name = entry.Name,
            ModelName = entry.ModelName,
            Source = entry.ProviderName,
            Type = entry.Type,
            IsDefault = entry.IsDefault,
            Properties = entry.Properties?.Count > 0
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(entry.Properties.DeepClone())
                : null,
        };
    }

    private static JsonArray ConvertToJsonArray(object value)
    {
        return value switch
        {
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Array => JsonNode.Parse(jsonElement.GetRawText()) as JsonArray,
            JsonArray jsonArray => jsonArray,
            IEnumerable<object> values => JsonSerializer.SerializeToNode(values) as JsonArray,
            _ => null,
        };
    }

    private static JsonNode ReadConfigurationNode(IConfigurationSection section)
    {
        var children = section.GetChildren().ToArray();

        if (children.Length == 0)
        {
            return section.Value is null ? null : JsonValue.Create(ParseScalar(section.Value));
        }

        if (children.All(static child => int.TryParse(child.Key, out _)))
        {
            var array = new JsonArray();

            foreach (var child in children.OrderBy(static child => int.Parse(child.Key)))
            {
                array.Add(ReadConfigurationNode(child));
            }

            return array;
        }

        var result = new JsonObject();

        foreach (var child in children)
        {
            result[child.Key] = ReadConfigurationNode(child);
        }

        return result;
    }

    private static object ParseScalar(string value)
    {
        if (bool.TryParse(value, out var booleanValue))
        {
            return booleanValue;
        }

        if (int.TryParse(value, out var intValue))
        {
            return intValue;
        }

        if (long.TryParse(value, out var longValue))
        {
            return longValue;
        }

        if (double.TryParse(value, out var doubleValue))
        {
            return doubleValue;
        }

        return value;
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
                var protector = _dataProtectionProvider.CreateProtector(_connectionProtectorName);
                properties[key] = protector.Protect(apiKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to protect API key for deployment '{Name}' of provider '{ProviderName}'.", deploymentName, providerName);
            }
        }
    }

    private static bool IsStandaloneDeploymentMetadataKey(string key)
        => string.Equals(key, "ClientName", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "ProviderName", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "Name", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "ModelName", StringComparison.OrdinalIgnoreCase)
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
        if (context is null)
        {
            return records;
        }

        if (!string.IsNullOrEmpty(context.Source))
        {
            records = records.Where(deployment => string.Equals(deployment.Source, context.Source, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(context.Name))
        {
            records = records.Where(deployment => deployment.Name.Contains(context.Name, StringComparison.OrdinalIgnoreCase));
        }

        if (context.Sorted)
        {
            records = records.OrderBy(static deployment => deployment.Name, StringComparer.OrdinalIgnoreCase);
        }

        return records;
    }
}
