using System.Globalization;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.AI.Services;

internal sealed class ConfigurationAIProviderConnectionsOptionsConfiguration : IPostConfigureOptions<AIProviderOptions>
{
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;

    public ConfigurationAIProviderConnectionsOptionsConfiguration(
        IConfiguration configuration,
        ILogger<ConfigurationAIProviderConnectionsOptionsConfiguration> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public void PostConfigure(string name, AIProviderOptions options)
    {
        var section = _configuration.GetSection("CrestApps:AI:Connections");

        if (!section.Exists())
        {
            return;
        }

        foreach (var connectionSection in section.GetChildren())
        {
            try
            {
                var connection = ReadConnection(connectionSection);
                var connectionName = connection.GetStringValue("Name", false);
                var clientName = AIProviderNameNormalizer.Normalize(connection.GetStringValue("ClientName", false));

                if (string.IsNullOrWhiteSpace(connectionName))
                {
                    _logger.LogWarning(
                        "The AI connection entry at 'CrestApps:AI:Connections:{Index}' is missing the required 'Name' property and will be ignored.",
                        connectionSection.Key);

                    continue;
                }

                if (string.IsNullOrWhiteSpace(clientName))
                {
                    _logger.LogWarning(
                        "The AI connection '{ConnectionName}' is missing the required 'ClientName' property and will be ignored.",
                        connectionName);

                    continue;
                }

                AIProviderOptionsConnectionMerger.MergeConnection(options, clientName, connectionName, connection);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Unable to bind AI connection entry from 'CrestApps:AI:Connections:{Index}'.",
                    connectionSection.Key);
            }
        }
    }

    private static AIProviderConnectionEntry ReadConnection(IConfigurationSection section)
    {
        var values = ReadObject(section);

        if (!values.ContainsKey("ConnectionNameAlias"))
        {
            values["ConnectionNameAlias"] = values.GetStringValue("Name", false) ?? section.Key;
        }

        return new AIProviderConnectionEntry(values);
    }

    private static Dictionary<string, object> ReadObject(IConfigurationSection section)
    {
        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var child in section.GetChildren())
        {
            values[child.Key] = ReadValue(child);
        }

        return values;
    }

    private static object ReadValue(IConfigurationSection section)
    {
        var children = section.GetChildren().ToArray();

        if (children.Length == 0)
        {
            return ParseScalar(section.Value);
        }

        if (children.All(static child => int.TryParse(child.Key, out _)))
        {
            return children
                .OrderBy(static child => int.Parse(child.Key, CultureInfo.InvariantCulture))
                .Select(ReadValue)
                .ToArray();
        }

        return children.ToDictionary(static child => child.Key, ReadValue, StringComparer.OrdinalIgnoreCase);
    }

    private static object ParseScalar(string value)
    {
        if (bool.TryParse(value, out var booleanValue))
        {
            return booleanValue;
        }

        return value;
    }
}
