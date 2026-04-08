using System.Security.Cryptography;
using System.Text;

namespace CrestApps.Core.AI.Services;

public static class AIConfigurationRecordIds
{
    private const string _connectionPrefix = "cfgc";
    private const string _deploymentPrefix = "cfgd";

    public static string CreateConnectionId(string providerName, string connectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        var input = $"{providerName}:{connectionName}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input.ToLowerInvariant()));

        return $"{_connectionPrefix}{Convert.ToHexStringLower(hash)[..22]}";
    }

    public static string CreateDeploymentId(string providerName, string connectionName, string deploymentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentName);

        var input = $"{providerName}:{connectionName ?? string.Empty}:{deploymentName}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input.ToLowerInvariant()));

        return $"{_deploymentPrefix}{Convert.ToHexStringLower(hash)[..22]}";
    }

    public static bool IsConfigurationConnectionId(string itemId)
        => !string.IsNullOrWhiteSpace(itemId) &&
            itemId.StartsWith(_connectionPrefix, StringComparison.OrdinalIgnoreCase);

    public static bool IsConfigurationDeploymentId(string itemId)
        => !string.IsNullOrWhiteSpace(itemId) &&
            itemId.StartsWith(_deploymentPrefix, StringComparison.OrdinalIgnoreCase);
}
