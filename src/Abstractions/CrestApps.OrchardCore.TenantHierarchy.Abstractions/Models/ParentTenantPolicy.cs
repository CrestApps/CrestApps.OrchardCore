namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Holds the policy of a parent tenant. The Default tenant writes it into the parent's shell settings, so the parent's
/// own administrators can never change it.
/// </summary>
public sealed class ParentTenantPolicy
{
    /// <summary>
    /// The default maximum number of child tenants.
    /// </summary>
    public const int DefaultMaxChildren = 100;

    /// <summary>
    /// Gets or sets the maximum number of child tenants the parent may own.
    /// </summary>
    public int MaxChildren { get; set; } = DefaultMaxChildren;

    /// <summary>
    /// Gets or sets the host pattern of a child tenant. It must contain <c>{business}</c> exactly once. When it is
    /// empty, the pattern is <c>{business}.{parent host}</c>.
    /// </summary>
    public string ChildHostPattern { get; set; }

    /// <summary>
    /// Gets or sets the setup recipes a child tenant may be created with. When it is empty, every setup recipe is allowed.
    /// </summary>
    public string[] Recipes { get; set; } = [];

    /// <summary>
    /// Gets or sets where the database of a new child tenant is placed.
    /// </summary>
    public ChildDatabaseStrategy DatabaseStrategy { get; set; } = ChildDatabaseStrategy.SqlitePerChild;

    /// <summary>
    /// Gets or sets the name of the database pool configured on the host. Pools are required by every strategy except
    /// <see cref="ChildDatabaseStrategy.SqlitePerChild"/>.
    /// </summary>
    public string DatabasePool { get; set; }

    /// <summary>
    /// Gets or sets the features that are blocked in the child tenants, in addition to the built-in block list.
    /// </summary>
    public string[] BlockedFeatures { get; set; } = [];

    /// <summary>
    /// Gets or sets the permissions denied to local users of a child tenant, for example <c>ManageRecipes</c>.
    /// </summary>
    public string[] DeniedLocalPermissions { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the parent session must have used multi-factor authentication before
    /// the user can enter a child tenant.
    /// </summary>
    public bool RequireMfa { get; set; }

    /// <summary>
    /// Gets or sets the interval at which a child tenant validates a delegated access session with the parent.
    /// </summary>
    public TimeSpan SessionValidationInterval { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets how long a delegated access session may be idle before it ends.
    /// </summary>
    public TimeSpan SessionIdleTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets the absolute lifetime of a delegated access session.
    /// </summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromHours(8);

    /// <summary>
    /// Gets or sets how the tenant switcher shows the list of child tenants.
    /// </summary>
    public SwitcherMode SwitcherMode { get; set; } = SwitcherMode.Hosted;

    /// <summary>
    /// Gets or sets the words the parent shows on screen for the hierarchy.
    /// </summary>
    public TenantHierarchyLabels Labels { get; set; } = new();

    /// <summary>
    /// Gets or sets the number of days a removed child tenant is kept suspended before it is removed. Zero removes it at once.
    /// </summary>
    public int RemovalGraceDays { get; set; }
}
