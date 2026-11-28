using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DataPipelines.SharedLinks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.SharedLinks;

/// <summary>
/// Describes shared links for the shared link tools, as the Shared Links admin page shows them: from an explicit list of
/// fields, with times in the user's or the site's time zone. It never returns the hash of a link's token, the identifier
/// of its resource, its handler's values or the user IDs of its recipients.
/// </summary>
internal static class SharedLinkToolSupport
{
    /// <summary>
    /// Describes a link.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="link">The link.</param>
    /// <returns>The description.</returns>
    public static async Task<object> DescribeAsync(IServiceProvider services, SharedLink link)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(link);

        var localClock = services.GetRequiredService<ILocalClock>();
        var utcNow = services.GetRequiredService<IClock>().UtcNow;

        return new
        {
            linkId = link.LinkId,
            title = link.Title,
            kind = link.Kind,
            kindName = GetKindName(services, link.Kind),
            sharedBy = link.SourceName,
            recipients = link.RecipientNames?.ToArray() ?? [],
            state = GetState(link, utcNow),
            isActive = link.IsActive(utcNow),
            createdAt = await AgentToolResults.ToLocalAsync(localClock, link.CreatedUtc),
            expiresAt = await AgentToolResults.ToLocalAsync(localClock, link.ExpiresUtc),
            revokedAt = await AgentToolResults.ToLocalAsync(localClock, link.RevokedUtc),
            uses = link.Uses,
            maxUses = link.MaxUses > 0 ? link.MaxUses : (int?)null,
            lastUsedAt = await AgentToolResults.ToLocalAsync(localClock, link.LastUsedUtc),
        };
    }

    /// <summary>
    /// Gets why a link works or does not, as the Shared Links admin page shows it.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns>Revoked, Expired, UsedUp or Active.</returns>
    public static string GetState(SharedLink link, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(link);

        if (link.RevokedUtc is not null)
        {
            return "Revoked";
        }

        if (link.ExpiresUtc <= utcNow)
        {
            return "Expired";
        }

        return link.IsUsedUp ? "UsedUp" : "Active";
    }

    /// <summary>
    /// Gets the address of the Shared Links admin page.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The address, or <see langword="null"/> when the request cannot build one.</returns>
    public static string GetAdminPath(IServiceProvider services)
        => AgentToolLinks.GetAdminPath(services, "OrchardCore.DataPipelines", "SharedLinksAdmin", "Index");

    private static string GetKindName(IServiceProvider services, string kind)
    {
        var descriptor = services.GetService<IOptions<SharedLinkHandlerOptions>>()?.Value.Handlers
            .FirstOrDefault(handler => string.Equals(handler.Kind, kind, StringComparison.Ordinal));

        if (descriptor is null)
        {
            return kind;
        }

        return services.GetService<IStringLocalizerFactory>()?.Localize(descriptor.DisplayName)?.Value ?? kind;
    }
}
