using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Secrets;

namespace CrestApps.OrchardCore.AI.Agent.Secrets;

/// <summary>
/// What the secret tools share: finding a secret by its name and store the way the Secrets admin page does, what a
/// tool returns about a secret, and the addresses of the admin pages. Everything a tool returns is built from the
/// information the stores give about a secret (name, store, type, description and dates), never from its value.
/// </summary>
internal static class SecretToolSupport
{
    /// <summary>
    /// The type name of a text secret.
    /// </summary>
    public const string TextType = nameof(TextSecret);

    /// <summary>
    /// The type name of an RSA key secret.
    /// </summary>
    public const string RsaKeyType = nameof(RsaKeySecret);

    /// <summary>
    /// The type name of an X.509 certificate secret.
    /// </summary>
    public const string X509Type = nameof(X509Secret);

    /// <summary>
    /// The number of days before its expiry date a secret counts as expiring soon, as on the Secrets admin page.
    /// </summary>
    public const int ExpiringSoonDays = 30;

    /// <summary>
    /// Gets the short type name of a secret from the type name a store keeps, as the Secrets admin page does.
    /// </summary>
    /// <param name="fullTypeName">The type name a store keeps.</param>
    /// <returns>The name without its namespace; a text secret when the store keeps none.</returns>
    public static string GetTypeName(string fullTypeName)
    {
        if (string.IsNullOrEmpty(fullTypeName))
        {
            return TextType;
        }

        var lastDot = fullTypeName.LastIndexOf('.');

        return lastDot >= 0
            ? fullTypeName.Substring(lastDot + 1)
            : fullTypeName;
    }

    /// <summary>
    /// Finds the one secret with a name, in a store when one is given.
    /// </summary>
    /// <param name="manager">The secret manager.</param>
    /// <param name="name">The name of the secret.</param>
    /// <param name="store">The name of the store, or <see langword="null"/> for any store.</param>
    /// <returns>The information about the secret, or the sentence the tool returns when there is none or several.</returns>
    public static async Task<(SecretInfo Info, string Error)> FindAsync(ISecretManager manager, string name, string store)
    {
        name = name?.Trim();
        store = store?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            return (null, "give the name of the secret. Use listSecrets to find it.");
        }

        var matches = (await manager.GetSecretInfosAsync())
            .Where(info => string.Equals(info.Name, name, StringComparison.OrdinalIgnoreCase))
            .Where(info => string.IsNullOrEmpty(store) || string.Equals(info.Store, store, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 1)
        {
            return (matches[0], null);
        }

        if (matches.Count == 0)
        {
            return (null, string.IsNullOrEmpty(store)
                ? $"there is no secret named '{name}'. Use listSecrets to find it."
                : $"there is no secret named '{name}' in the store '{store}'. Use listSecrets to find it.");
        }

        return (null, $"the name '{name}' is in several stores: {string.Join(", ", matches.Select(info => info.Store))}. Give store to say which one.");
    }

    /// <summary>
    /// Whether a store exists and can be written to.
    /// </summary>
    /// <param name="manager">The secret manager.</param>
    /// <param name="store">The name of the store.</param>
    /// <returns><see langword="true"/> when the store is writable.</returns>
    public static bool IsWritableStore(ISecretManager manager, string store)
    {
        return !string.IsNullOrEmpty(store) &&
            manager.GetStores().Any(candidate => !candidate.IsReadOnly && string.Equals(candidate.Name, store, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the names of the stores that can be written to.
    /// </summary>
    /// <param name="manager">The secret manager.</param>
    /// <returns>The names of the writable stores.</returns>
    public static List<string> GetWritableStores(ISecretManager manager)
    {
        return manager.GetStores()
            .Where(store => !store.IsReadOnly)
            .Select(store => store.Name)
            .ToList();
    }

    /// <summary>
    /// Whether a secret is past its expiry date.
    /// </summary>
    /// <param name="info">The information about the secret.</param>
    /// <param name="utcNow">The current time.</param>
    /// <returns><see langword="true"/> when the secret is expired.</returns>
    public static bool IsExpired(SecretInfo info, DateTime utcNow)
        => info.ExpiresUtc.HasValue && info.ExpiresUtc.Value < utcNow;

    /// <summary>
    /// Whether a secret expires within 30 days and is not expired yet.
    /// </summary>
    /// <param name="info">The information about the secret.</param>
    /// <param name="utcNow">The current time.</param>
    /// <returns><see langword="true"/> when the secret expires soon.</returns>
    public static bool IsExpiringSoon(SecretInfo info, DateTime utcNow)
        => info.ExpiresUtc.HasValue && !IsExpired(info, utcNow) && info.ExpiresUtc.Value < utcNow.AddDays(ExpiringSoonDays);

    /// <summary>
    /// Builds what a tool returns about one secret, from the information a store gives about it. The value of the
    /// secret is not read.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="info">The information about the secret.</param>
    /// <returns>The fields the tool returns.</returns>
    public static async Task<object> DescribeAsync(IServiceProvider services, SecretInfo info)
    {
        var localClock = services.GetRequiredService<ILocalClock>();
        var utcNow = services.GetRequiredService<IClock>().UtcNow;

        return new
        {
            name = info.Name,
            store = info.Store,
            type = GetTypeName(info.Type),
            description = info.Description,
            createdAt = await AgentToolResults.ToLocalAsync(localClock, info.CreatedUtc),
            updatedAt = await AgentToolResults.ToLocalAsync(localClock, info.UpdatedUtc),
            expiresAt = await AgentToolResults.ToLocalAsync(localClock, info.ExpiresUtc),
            expired = IsExpired(info, utcNow)
                ? true
                : (bool?)null,
            expiresWithin30Days = IsExpiringSoon(info, utcNow)
                ? true
                : (bool?)null,
            adminUrl = GetEditUrl(services, info.Name, info.Store),
        };
    }

    /// <summary>
    /// Gets the address of the Edit page of a secret, where a person changes its value.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="name">The name of the secret.</param>
    /// <param name="store">The name of its store.</param>
    /// <returns>The address, or <see langword="null"/> when the request cannot build one.</returns>
    public static string GetEditUrl(IServiceProvider services, string name, string store)
    {
        return AgentToolLinks.GetAdminPath(services, OrchardCoreFeatureIds.Secrets, "Admin", "Edit", new RouteValueDictionary
        {
            { "name", name },
            { "store", store },
        });
    }

    /// <summary>
    /// Gets the address of the Create page of the Secrets admin area for a type of secret, with the name filled in,
    /// where a person types a value or has a key pair generated.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="type">The type name of the secret.</param>
    /// <param name="name">The name of the new secret.</param>
    /// <returns>The address, or <see langword="null"/> when the request cannot build one.</returns>
    public static string GetCreateUrl(IServiceProvider services, string type, string name)
    {
        return AgentToolLinks.GetAdminPath(services, OrchardCoreFeatureIds.Secrets, "Admin", "Create", new RouteValueDictionary
        {
            { "type", type },
            { "name", name },
        });
    }

    /// <summary>
    /// Writes an address for the end of a sentence.
    /// </summary>
    /// <param name="url">The address, or <see langword="null"/>.</param>
    /// <returns>The address in brackets with a space before it, or nothing.</returns>
    public static string FormatLink(string url)
    {
        return string.IsNullOrEmpty(url)
            ? string.Empty
            : $" ({url})";
    }
}
