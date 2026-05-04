using CrestApps.Core.AI.Connections;
using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Provides extension methods for searching and displaying AI provider connections from a catalog.
/// </summary>
public static class AIProviderConnectionCatalogExtensions
{
    /// <summary>
    /// Finds an AI provider connection by its connection name or item identifier within a specific provider.
    /// </summary>
    /// <param name="connectionsCatalog">The catalog to search in.</param>
    /// <param name="providerName">The provider name to scope the search to.</param>
    /// <param name="connectionName">The connection name or item identifier to match.</param>
    /// <returns>The matching <see cref="AIProviderConnection"/>, or <see langword="null"/> if not found.</returns>
    public static async ValueTask<AIProviderConnection> FindByConnectionNameAsync(
        this IAIProviderConnectionStore connectionsCatalog,
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

    /// <summary>
    /// Gets a human-readable display name for the connection, falling back to the name or item identifier.
    /// </summary>
    /// <param name="connection">The AI provider connection to get the display name for.</param>
    /// <returns>The display text, name, or item identifier, whichever is available first.</returns>
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
