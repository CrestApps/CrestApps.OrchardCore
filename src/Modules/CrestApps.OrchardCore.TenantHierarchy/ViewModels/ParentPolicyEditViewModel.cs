using CrestApps.OrchardCore.TenantHierarchy.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.TenantHierarchy.ViewModels;

/// <summary>
/// The model of the screens that make a tenant a parent and edit a parent policy.
/// </summary>
public class ParentPolicyEditViewModel
{
    /// <summary>
    /// Gets or sets the tenant name.
    /// </summary>
    public string TenantName { get; set; }

    /// <summary>
    /// Gets or sets the slug of the parent. It is set once, when the tenant is made a parent.
    /// </summary>
    public string Slug { get; set; }

    /// <summary>
    /// Gets or sets the display name of the parent.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of child tenants.
    /// </summary>
    public int MaxChildren { get; set; } = ParentTenantPolicy.DefaultMaxChildren;

    /// <summary>
    /// Gets or sets the host pattern of child tenants.
    /// </summary>
    public string ChildHostPattern { get; set; }

    /// <summary>
    /// Gets or sets the database strategy.
    /// </summary>
    public ChildDatabaseStrategy DatabaseStrategy { get; set; }

    /// <summary>
    /// Gets or sets the database pool.
    /// </summary>
    public string DatabasePool { get; set; }

    /// <summary>
    /// Gets or sets the allowed setup recipes.
    /// </summary>
    public List<string> Recipes { get; set; } = [];

    /// <summary>
    /// Gets or sets the blocked features, one per line.
    /// </summary>
    public string BlockedFeatures { get; set; }

    /// <summary>
    /// Gets or sets the permissions denied to local child users, one per line.
    /// </summary>
    public string DeniedLocalPermissions { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether entering a child requires multi-factor authentication.
    /// </summary>
    public bool RequireMfa { get; set; }

    /// <summary>
    /// Gets or sets the session validation interval, in minutes.
    /// </summary>
    public int SessionValidationMinutes { get; set; } = 2;

    /// <summary>
    /// Gets or sets the session idle timeout, in minutes.
    /// </summary>
    public int SessionIdleMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets the session lifetime, in hours.
    /// </summary>
    public int SessionLifetimeHours { get; set; } = 8;

    /// <summary>
    /// Gets or sets the switcher mode.
    /// </summary>
    public SwitcherMode SwitcherMode { get; set; }

    /// <summary>
    /// Gets or sets the word for the parent tenant.
    /// </summary>
    public string ParentLabel { get; set; }

    /// <summary>
    /// Gets or sets the word for one child tenant.
    /// </summary>
    public string ChildLabel { get; set; }

    /// <summary>
    /// Gets or sets the word for several child tenants.
    /// </summary>
    public string ChildrenLabel { get; set; }

    /// <summary>
    /// Gets or sets the removal grace period, in days.
    /// </summary>
    public int RemovalGraceDays { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a tenant is made a parent, rather than a parent edited.
    /// </summary>
    [BindNever]
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the platform domain parent hosts are built on.
    /// </summary>
    [BindNever]
    public string PlatformDomain { get; set; }

    /// <summary>
    /// Gets or sets the host of the parent, when it is edited.
    /// </summary>
    [BindNever]
    public string Host { get; set; }

    /// <summary>
    /// Gets or sets the tenants that can be made parents.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Candidates { get; set; } = [];

    /// <summary>
    /// Gets or sets the setup recipes.
    /// </summary>
    [BindNever]
    public List<SelectListItem> AvailableRecipes { get; set; } = [];

    /// <summary>
    /// Gets or sets the database pools configured on the host.
    /// </summary>
    [BindNever]
    public List<SelectListItem> DatabasePools { get; set; } = [];

    /// <summary>
    /// Builds a policy from the model.
    /// </summary>
    public ParentTenantPolicy ToPolicy()
    {
        return new ParentTenantPolicy
        {
            MaxChildren = MaxChildren,
            ChildHostPattern = string.IsNullOrWhiteSpace(ChildHostPattern) ? null : ChildHostPattern.Trim(),
            DatabaseStrategy = DatabaseStrategy,
            DatabasePool = string.IsNullOrWhiteSpace(DatabasePool) ? null : DatabasePool.Trim(),
            Recipes = (Recipes ?? []).Where(recipe => !string.IsNullOrWhiteSpace(recipe)).ToArray(),
            BlockedFeatures = SplitLines(BlockedFeatures),
            DeniedLocalPermissions = SplitLines(DeniedLocalPermissions),
            RequireMfa = RequireMfa,
            SessionValidationInterval = TimeSpan.FromMinutes(SessionValidationMinutes),
            SessionIdleTimeout = TimeSpan.FromMinutes(SessionIdleMinutes),
            SessionLifetime = TimeSpan.FromHours(SessionLifetimeHours),
            SwitcherMode = SwitcherMode,
            RemovalGraceDays = RemovalGraceDays,
            Labels = new TenantHierarchyLabels
            {
                Parent = ParentLabel,
                Child = ChildLabel,
                Children = ChildrenLabel,
            },
        };
    }

    /// <summary>
    /// Fills the model from a policy.
    /// </summary>
    /// <param name="policy">The policy.</param>
    public void FromPolicy(ParentTenantPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        MaxChildren = policy.MaxChildren;
        ChildHostPattern = policy.ChildHostPattern;
        DatabaseStrategy = policy.DatabaseStrategy;
        DatabasePool = policy.DatabasePool;
        Recipes = policy.Recipes.ToList();
        BlockedFeatures = string.Join(Environment.NewLine, policy.BlockedFeatures);
        DeniedLocalPermissions = string.Join(Environment.NewLine, policy.DeniedLocalPermissions);
        RequireMfa = policy.RequireMfa;
        SessionValidationMinutes = Math.Max(1, (int)policy.SessionValidationInterval.TotalMinutes);
        SessionIdleMinutes = Math.Max(1, (int)policy.SessionIdleTimeout.TotalMinutes);
        SessionLifetimeHours = Math.Max(1, (int)policy.SessionLifetime.TotalHours);
        SwitcherMode = policy.SwitcherMode;
        RemovalGraceDays = policy.RemovalGraceDays;
        ParentLabel = policy.Labels?.Parent;
        ChildLabel = policy.Labels?.Child;
        ChildrenLabel = policy.Labels?.Children;
    }

    private static string[] SplitLines(string value)
    {
        return (value ?? string.Empty)
            .Split(['\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
