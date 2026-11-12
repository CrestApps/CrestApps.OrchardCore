using CrestApps.OrchardCore.Subscriptions.Core.Services;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// How the sites a subscription sells are created: their database, and where a buyer may make them answer.
/// </summary>
public sealed class SubscriptionOnboardingSettings
{
    /// <summary>
    /// The prefixes reserved by default, because the host's own routes use them.
    /// </summary>
    public static readonly string[] DefaultReservedPrefixes =
    [
        "admin",
        "api",
        "login",
        "connect",
        "stripe",
        "media",
        "checkout",
        "cart",
        "orders",
    ];

    public string DatabaseProvider { get; set; }

    public string ProtectedConnectionString { get; set; }

    public string Schema { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a buyer may choose the site's URL prefix. When off, the prefix is the
    /// site's name.
    /// </summary>
    public bool AllowCustomPrefix { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a buyer may make the site answer on host names of their own. Off for new
    /// sites; an upgrade turns it on where sites were already sold with domains.
    /// </summary>
    public bool AllowCustomDomains { get; set; }

    /// <summary>
    /// Gets or sets the URL prefixes no sold site may take. Null means <see cref="DefaultReservedPrefixes"/>.
    /// </summary>
    public string[] ReservedPrefixes { get; set; }

    /// <summary>
    /// Gets or sets the host names no sold site may take, such as the operator's own. The host the settings were saved
    /// from and the default tenant's hosts are always reserved too.
    /// </summary>
    public string[] ReservedHosts { get; set; } = [];

    /// <summary>
    /// Gets or sets the domain under which each sold site gets its own host, <c>{name}.{BaseDomain}</c>, when buyers may
    /// not name their own. Empty means sold sites answer on a prefix.
    /// </summary>
    public string BaseDomain { get; set; }

    public string GetDatabaseProviderOrDefault()
        => string.IsNullOrWhiteSpace(DatabaseProvider) ? DefaultDatabaseProvider : DatabaseProvider.Trim();

    /// <summary>
    /// Builds the rules a purchase of a plan is held to.
    /// </summary>
    /// <param name="planRecipeName">The recipe the plan names.</param>
    /// <param name="adminPrefix">The admin URL prefix.</param>
    /// <param name="hostNames">Host names that are always reserved, such as the default tenant's and the operator's.</param>
    /// <returns>The rules.</returns>
    public TenantPurchaseRules ToRules(string planRecipeName, string adminPrefix, IEnumerable<string> hostNames)
        => new()
        {
            PlanRecipeName = planRecipeName,
            AllowCustomPrefix = AllowCustomPrefix,
            AllowCustomDomains = AllowCustomDomains,
            ReservedPrefixes = ReservedPrefixes ?? DefaultReservedPrefixes,
            ReservedHosts = (ReservedHosts ?? []).Concat(hostNames ?? []).ToArray(),
            BaseDomain = BaseDomain,
            AdminPrefix = adminPrefix,
        };

    public const string DefaultDatabaseProvider = "Sqlite";
}
