using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.AI.Core;

public static class AIProviderConnectionCatalogExtensions
{
    public static async ValueTask<AIProviderConnection> FindByConnectionNameAsync(
        this INamedSourceCatalog<AIProviderConnection> connectionsCatalog,
        string providerName,
        string connectionName)
    {
        ArgumentNullException.ThrowIfNull(connectionsCatalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        var connections = await connectionsCatalog.GetAsync(providerName);

        return connections.FirstOrDefault(connection =>
            string.Equals(connection.ItemId, connectionName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(connection.Name, connectionName, StringComparison.OrdinalIgnoreCase));
    }

    public static string GetDisplayName(this AIProviderConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return !string.IsNullOrWhiteSpace(connection.DisplayText)
            ? connection.DisplayText
            : !string.IsNullOrWhiteSpace(connection.Name)
                ? connection.Name
                : connection.ItemId;
    }
}
