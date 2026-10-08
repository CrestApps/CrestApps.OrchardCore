using CrestApps.OrchardCore.TenantHierarchy.Core;
using CrestApps.OrchardCore.TenantHierarchy.Core.Guards;
using CrestApps.OrchardCore.TenantHierarchy.Core.Provisioning;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Removing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// The platform side of the tenant hierarchy. It runs in the Default tenant only, which may reach every tenant, and
/// makes parents, writes their policies and acts on whole parents and their children.
/// </summary>
public sealed class TenantHierarchyPlatformService
{
    private readonly IShellHost _shellHost;
    private readonly IShellRemovalManager _shellRemovalManager;
    private readonly ChildDatabaseProvisioning _databaseProvisioning;
    private readonly TenantHierarchyOptions _options;
    private readonly ShellSettings _shellSettings;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWebHostEnvironment _environment;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantHierarchyPlatformService"/> class.
    /// </summary>
    /// <param name="shellHost">The shell host.</param>
    /// <param name="shellRemovalManager">The shell removal manager.</param>
    /// <param name="databaseProvisioning">The database provisioning service.</param>
    /// <param name="options">The tenant hierarchy options.</param>
    /// <param name="shellSettings">The settings of the Default tenant.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TenantHierarchyPlatformService(
        IShellHost shellHost,
        IShellRemovalManager shellRemovalManager,
        ChildDatabaseProvisioning databaseProvisioning,
        IOptions<TenantHierarchyOptions> options,
        ShellSettings shellSettings,
        IServiceProvider serviceProvider,
        IHttpContextAccessor httpContextAccessor,
        IWebHostEnvironment environment,
        IClock clock,
        ILogger<TenantHierarchyPlatformService> logger,
        IStringLocalizer<TenantHierarchyPlatformService> stringLocalizer)
    {
        _shellHost = shellHost;
        _shellRemovalManager = shellRemovalManager;
        _databaseProvisioning = databaseProvisioning;
        _options = options.Value;
        _shellSettings = shellSettings;
        _serviceProvider = serviceProvider;
        _httpContextAccessor = httpContextAccessor;
        _environment = environment;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets a value indicating whether the host-level guards of <c>AddTenantHierarchy()</c> are installed.
    /// </summary>
    public bool IsHostGuardInstalled => _serviceProvider.GetService<TenantHierarchyHostMarker>() is not null &&
        _shellHost is GuardedShellHost;

    /// <summary>
    /// Returns the platform domain parent hosts are built on.
    /// </summary>
    public string GetPlatformDomain()
    {
        if (!string.IsNullOrWhiteSpace(_options.PlatformDomain))
        {
            return _options.PlatformDomain.Trim().TrimStart('.');
        }

        var host = _httpContextAccessor.HttpContext?.Request.Host;

        return host?.HasValue == true
            ? host.Value.Value
            : _shellSettings.GetPrimaryHost() ?? "localhost";
    }

    /// <summary>
    /// Returns whether a strategy and pool can be used on this host.
    /// </summary>
    /// <param name="strategy">The strategy.</param>
    /// <param name="poolName">The pool name.</param>
    public bool CanProvision(ChildDatabaseStrategy strategy, string poolName)
        => _databaseProvisioning.CanProvision(strategy, poolName);

    /// <summary>
    /// Returns the names of the database pools configured on the host.
    /// </summary>
    public IReadOnlyList<string> GetDatabasePoolNames()
        => _options.DatabasePools.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>
    /// Returns the whole hierarchy: the parents with their children, the orphans and the tenants that can become parents.
    /// </summary>
    public HierarchyOverview GetOverview()
    {
        EnsureDefault();

        var all = _shellHost.GetAllSettings().ToList();
        var overview = new HierarchyOverview();
        var scheme = GetScheme();

        foreach (var parent in all.Where(settings => settings.IsParentTenant()).OrderBy(settings => settings.GetHierarchyDisplayName(), StringComparer.OrdinalIgnoreCase))
        {
            var node = new HierarchyTreeNode
            {
                Settings = parent,
                DisplayName = parent.GetHierarchyDisplayName(),
                Address = TenantHierarchyUrls.GetBaseAddress(parent, scheme),
            };

            foreach (var child in all.Where(settings => settings.IsChildOf(parent)).OrderBy(settings => settings[TenantHierarchyConstants.SettingsKeys.Description], StringComparer.OrdinalIgnoreCase))
            {
                node.Children.Add(ToChildNode(child, scheme));
            }

            overview.Parents.Add(node);
        }

        foreach (var orphan in all.Where(settings => settings.IsChildTenant() && !all.Any(parent => settings.IsChildOf(parent))))
        {
            var node = ToChildNode(orphan, scheme);
            node.Warnings.Add(S["Its parent tenant no longer exists."]);
            overview.Orphans.Add(node);
        }

        overview.Candidates = all
            .Where(settings => !settings.IsDefaultShell() && !settings.IsInTenantHierarchy())
            .OrderBy(settings => settings.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return overview;
    }

    /// <summary>
    /// Returns a parent with its children and the problems found by comparing its registry with the shell settings and
    /// by checking the features each running child has on.
    /// </summary>
    /// <param name="parentName">The tenant name of the parent.</param>
    public async Task<HierarchyTreeNode> GetParentDetailAsync(string parentName)
    {
        EnsureDefault();

        var node = GetOverview().Parents.FirstOrDefault(parent => string.Equals(parent.Settings.Name, parentName, StringComparison.OrdinalIgnoreCase));

        if (node is null)
        {
            return null;
        }

        if (node.Settings.IsRunning())
        {
            var entries = await InScopeAsync(node.Settings, services => services.GetRequiredService<ChildTenantEntryStore>().ListAsync()) ?? [];

            foreach (var child in node.Children)
            {
                if (!entries.Any(entry => string.Equals(entry.TenantId, child.Settings.TenantId, StringComparison.Ordinal)))
                {
                    child.Warnings.Add(S["The parent's registry has no entry for this tenant."]);
                }
            }

            foreach (var entry in entries.Where(entry => !string.IsNullOrEmpty(entry.TenantId) && !node.Children.Any(child => string.Equals(child.Settings.TenantId, entry.TenantId, StringComparison.Ordinal))))
            {
                node.Warnings.Add(S["The registry entry '{0}' has no matching tenant.", entry.DisplayName]);
            }
        }

        foreach (var child in node.Children.Where(child => child.Settings.IsRunning()))
        {
            var blocked = await InScopeAsync(child.Settings, async services =>
            {
                var enabled = await services.GetRequiredService<IShellFeaturesManager>().GetEnabledFeaturesAsync();

                return enabled
                    .Where(feature => feature.Id != TenantHierarchyConstants.Features.Child &&
                        !TenantHierarchyFeatureValidationProvider.IsFeatureValid(child.Settings, feature.Id))
                    .Select(feature => feature.Id)
                    .ToList();
            });

            foreach (var featureId in blocked ?? [])
            {
                child.Warnings.Add(S["The blocked feature '{0}' is enabled.", featureId]);
            }
        }

        return node;
    }

    /// <summary>
    /// Makes an ordinary tenant into a parent: its host becomes <c>{slug}.{platform domain}</c>, the policy is written
    /// into its shell settings and the Parent feature is forced on.
    /// </summary>
    /// <param name="tenantName">The tenant name.</param>
    /// <param name="slug">The parent slug.</param>
    /// <param name="displayName">The display name of the parent.</param>
    /// <param name="policy">The policy.</param>
    public async Task<(TenantHierarchyResult Result, IReadOnlyDictionary<string, string> Errors)> MakeParentAsync(
        string tenantName,
        string slug,
        string displayName,
        ParentTenantPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        EnsureDefault();

        var errors = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!IsHostGuardInstalled)
        {
            return (TenantHierarchyResult.Failure(S["Call AddTenantHierarchy() in Program.cs before you make parent tenants."]), errors);
        }

        if (!_shellHost.TryGetSettings(tenantName, out var settings) || settings.IsDefaultShell() || settings.IsInTenantHierarchy())
        {
            return (TenantHierarchyResult.Failure(S["Select a tenant that is not part of a hierarchy yet."]), errors);
        }

        slug = slug?.Trim().ToLowerInvariant();

        if (TenantHierarchyNaming.ValidateSlug(slug, _options.ReservedSlugs) != SlugValidationResult.Valid)
        {
            errors["Slug"] = S["Use 3 to 40 lowercase letters, digits and inner hyphens, and not a reserved name."];
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            errors["DisplayName"] = S["The display name is required."];
        }

        foreach (var error in ParentPolicyValidator.Validate(policy, CanProvision))
        {
            errors[error.Key] = error.Value;
        }

        var host = errors.ContainsKey("Slug") ? null : TenantHierarchyNaming.BuildParentHost(slug, GetPlatformDomain());

        if (host is not null && !TenantHierarchyNaming.IsValidHost(host))
        {
            errors["Slug"] = S["The address '{0}' is not a valid host name.", host];
        }
        else if (host is not null && _shellHost.GetAllSettings().Any(other =>
            !string.Equals(other.Name, settings.Name, StringComparison.OrdinalIgnoreCase) &&
            other.RequestUrlHosts.Any(candidate => string.Equals(candidate, host, StringComparison.OrdinalIgnoreCase))))
        {
            errors["Slug"] = S["The address '{0}' is already used by another tenant.", host];
        }

        if (errors.Count > 0)
        {
            return (TenantHierarchyResult.Failure(errors.Values.First()), errors);
        }

        settings.RequestUrlHost = host;
        settings.RequestUrlPrefix = string.Empty;
        settings[TenantHierarchyConstants.SettingsKeys.Role] = TenantHierarchyConstants.Roles.Parent;
        settings[TenantHierarchyConstants.SettingsKeys.Slug] = slug;
        settings[TenantHierarchyConstants.SettingsKeys.DisplayName] = displayName.Trim();
        TenantHierarchySettingsWriter.WritePolicy(settings, policy);
        TenantHierarchySettingsWriter.EnsureAlwaysEnabledFeature(settings, TenantHierarchyConstants.Features.Parent);

        await _shellHost.UpdateShellSettingsAsync(settings);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("The platform made tenant '{Tenant}' a parent tenant with the host '{Host}'.", settings.Name, host);
        }

        return (TenantHierarchyResult.Success, errors);
    }

    /// <summary>
    /// Writes a new display name and policy for a parent and copies the parts a child enforces into each of its children.
    /// </summary>
    /// <param name="parentName">The tenant name of the parent.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="policy">The policy.</param>
    public async Task<(TenantHierarchyResult Result, IReadOnlyDictionary<string, string> Errors)> UpdateParentAsync(
        string parentName,
        string displayName,
        ParentTenantPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        EnsureDefault();

        var parent = GetParent(parentName);

        if (parent is null)
        {
            return (NotFound(), new Dictionary<string, string>());
        }

        var errors = ParentPolicyValidator.Validate(policy, CanProvision).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(displayName))
        {
            errors["DisplayName"] = S["The display name is required."];
        }

        if (errors.Count > 0)
        {
            return (TenantHierarchyResult.Failure(errors.Values.First()), errors);
        }

        parent[TenantHierarchyConstants.SettingsKeys.DisplayName] = displayName.Trim();
        TenantHierarchySettingsWriter.WritePolicy(parent, policy);
        await _shellHost.UpdateShellSettingsAsync(parent);

        foreach (var child in GetChildren(parent))
        {
            TenantHierarchySettingsWriter.WriteChildPolicy(child, policy);
            child[TenantHierarchyConstants.SettingsKeys.Category] = displayName.Trim();
            await _shellHost.UpdateShellSettingsAsync(child);
        }

        return (TenantHierarchyResult.Success, errors);
    }

    /// <summary>
    /// Makes a parent an ordinary tenant again. Only a parent without children can be unmade.
    /// </summary>
    /// <param name="parentName">The tenant name of the parent.</param>
    public async Task<TenantHierarchyResult> UnmakeParentAsync(string parentName)
    {
        EnsureDefault();

        var parent = GetParent(parentName);

        if (parent is null)
        {
            return NotFound();
        }

        if (GetChildren(parent).Any())
        {
            return TenantHierarchyResult.Failure(S["Remove or move the child tenants of this parent first."]);
        }

        TenantHierarchySettingsWriter.ClearHierarchy(parent);
        await _shellHost.UpdateShellSettingsAsync(parent);

        return TenantHierarchyResult.Success;
    }

    /// <summary>
    /// Suspends a parent and every running child. Resuming the parent resumes exactly these children.
    /// </summary>
    /// <param name="parentName">The tenant name of the parent.</param>
    public async Task<TenantHierarchyResult> SuspendParentAsync(string parentName)
    {
        EnsureDefault();

        var parent = GetParent(parentName);

        if (parent is null)
        {
            return NotFound();
        }

        foreach (var child in GetChildren(parent).Where(child => child.IsRunning()))
        {
            child[TenantHierarchyConstants.SettingsKeys.SuspendedWithParent] = bool.TrueString;
            await _shellHost.UpdateShellSettingsAsync(child.AsDisabled());
        }

        if (parent.IsRunning())
        {
            await _shellHost.UpdateShellSettingsAsync(parent.AsDisabled());
        }

        return TenantHierarchyResult.Success;
    }

    /// <summary>
    /// Resumes a parent and the children that were suspended with it.
    /// </summary>
    /// <param name="parentName">The tenant name of the parent.</param>
    public async Task<TenantHierarchyResult> ResumeParentAsync(string parentName)
    {
        EnsureDefault();

        var parent = GetParent(parentName);

        if (parent is null)
        {
            return NotFound();
        }

        if (parent.IsDisabled())
        {
            await _shellHost.UpdateShellSettingsAsync(parent.AsRunning());
        }

        foreach (var child in GetChildren(parent).Where(child => child.IsDisabled() &&
            string.Equals(child[TenantHierarchyConstants.SettingsKeys.SuspendedWithParent], bool.TrueString, StringComparison.OrdinalIgnoreCase)))
        {
            child[TenantHierarchyConstants.SettingsKeys.SuspendedWithParent] = null;
            await _shellHost.UpdateShellSettingsAsync(child.AsRunning());
        }

        return TenantHierarchyResult.Success;
    }

    /// <summary>
    /// Removes a suspended parent and all its children: each child first, then the parent.
    /// </summary>
    /// <param name="parentName">The tenant name of the parent.</param>
    public async Task<TenantHierarchyResult> RemoveParentAsync(string parentName)
    {
        EnsureDefault();

        var parent = GetParent(parentName);

        if (parent is null)
        {
            return NotFound();
        }

        if (parent.IsRunning())
        {
            return TenantHierarchyResult.Failure(S["Suspend the parent tenant before you remove it."]);
        }

        foreach (var child in GetChildren(parent).ToList())
        {
            var result = await RemoveChildTenantAsync(child);

            if (!result.Succeeded)
            {
                return result;
            }
        }

        var context = await _shellRemovalManager.RemoveAsync(parent);

        if (!context.Success)
        {
            _logger.LogError(context.Error, "The platform could not remove parent tenant '{Tenant}': {Error}", parent.Name, context.ErrorMessage);

            return TenantHierarchyResult.Failure(S["The parent tenant could not be removed: {0}", context.ErrorMessage]);
        }

        return TenantHierarchyResult.Success;
    }

    /// <summary>
    /// Moves a child to another parent. Its host follows the new parent's pattern, its registry entry moves, and every
    /// delegated access session of the old parent fails its next validation.
    /// </summary>
    /// <param name="childName">The tenant name of the child.</param>
    /// <param name="newParentName">The tenant name of the new parent.</param>
    public async Task<TenantHierarchyResult> MoveChildAsync(string childName, string newParentName)
    {
        EnsureDefault();

        if (!_shellHost.TryGetSettings(childName, out var child) || !child.IsChildTenant())
        {
            return NotFound();
        }

        var newParent = GetParent(newParentName);

        if (newParent is null)
        {
            return TenantHierarchyResult.Failure(S["Select a parent tenant."]);
        }

        if (child.IsChildOf(newParent))
        {
            return TenantHierarchyResult.Failure(S["The tenant already belongs to this parent."]);
        }

        var newPolicy = newParent.GetParentPolicy();
        var pattern = string.IsNullOrWhiteSpace(newPolicy.ChildHostPattern)
            ? TenantHierarchyNaming.GetDefaultHostPattern(newParent.GetPrimaryHost())
            : newPolicy.ChildHostPattern.Trim();
        var slug = child.GetHierarchySlug();
        var host = TenantHierarchyNaming.BuildHost(pattern, slug);

        if (_shellHost.GetAllSettings().Any(other => other.Name != child.Name && other.RequestUrlHosts.Any(candidate => string.Equals(candidate, host, StringComparison.OrdinalIgnoreCase))))
        {
            return TenantHierarchyResult.Failure(S["The address '{0}' is already used by another tenant.", host]);
        }

        ChildTenantEntry movedEntry = null;
        var oldParent = _shellHost.GetAllSettings().FirstOrDefault(candidate => child.IsChildOf(candidate));

        if (oldParent?.IsRunning() == true)
        {
            movedEntry = await InScopeAsync(oldParent, async services =>
            {
                var store = services.GetRequiredService<ChildTenantEntryStore>();
                var entry = await store.FindByTenantIdAsync(child.TenantId);

                if (entry is not null)
                {
                    store.Delete(entry);
                    await services.GetRequiredService<AccessGrantStore>().DeleteForChildAsync(entry.EntryId);
                }

                return entry;
            });
        }

        var newEntry = new ChildTenantEntry
        {
            EntryId = IdGenerator.GenerateId(),
            TenantId = child.TenantId,
            TenantName = child.Name,
            Slug = slug,
            DisplayName = movedEntry?.DisplayName ?? child[TenantHierarchyConstants.SettingsKeys.Description] ?? child.Name,
            Description = movedEntry?.Description,
            Host = host,
            RecipeName = movedEntry?.RecipeName ?? child["RecipeName"],
            Status = child.IsUninitialized() ? ChildTenantStatus.Failed : ChildTenantStatus.Ready,
            DatabaseStrategy = Enum.TryParse<ChildDatabaseStrategy>(child[TenantHierarchyConstants.SettingsKeys.DatabaseStrategy], out var strategy) ? strategy : ChildDatabaseStrategy.SqlitePerChild,
            DatabasePool = child[TenantHierarchyConstants.SettingsKeys.DatabasePool],
            ProvisionedResource = child[TenantHierarchyConstants.SettingsKeys.ProvisionedResource],
            BootstrapUserId = movedEntry?.BootstrapUserId,
            CreatedUtc = movedEntry?.CreatedUtc ?? _clock.UtcNow,
            CreatedByName = movedEntry?.CreatedByName,
        };

        child.RequestUrlHost = host;
        child[TenantHierarchyConstants.SettingsKeys.ParentTenantId] = newParent.TenantId;
        child[TenantHierarchyConstants.SettingsKeys.ParentTenant] = newParent.Name;
        child[TenantHierarchyConstants.SettingsKeys.EntryId] = newEntry.EntryId;
        child[TenantHierarchyConstants.SettingsKeys.Category] = newParent.GetHierarchyDisplayName();
        TenantHierarchySettingsWriter.WriteChildPolicy(child, newPolicy);
        await _shellHost.UpdateShellSettingsAsync(child);

        if (newParent.IsRunning())
        {
            await InScopeAsync(newParent, async services =>
            {
                await services.GetRequiredService<ChildTenantEntryStore>().SaveAsync(newEntry);

                return true;
            });
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("The platform moved child tenant '{Child}' to parent tenant '{Parent}'.", child.Name, newParent.Name);
        }

        return TenantHierarchyResult.Success;
    }

    /// <summary>
    /// Makes an orphaned child an ordinary tenant.
    /// </summary>
    /// <param name="childName">The tenant name of the child.</param>
    public async Task<TenantHierarchyResult> DetachChildAsync(string childName)
    {
        EnsureDefault();

        if (!_shellHost.TryGetSettings(childName, out var child) || !child.IsChildTenant())
        {
            return NotFound();
        }

        TenantHierarchySettingsWriter.ClearHierarchy(child);
        await _shellHost.UpdateShellSettingsAsync(child);

        return TenantHierarchyResult.Success;
    }

    private async Task<TenantHierarchyResult> RemoveChildTenantAsync(ShellSettings child)
    {
        if (child.IsRunning())
        {
            await _shellHost.UpdateShellSettingsAsync(child.AsDisabled());
        }

        var context = await _shellRemovalManager.RemoveAsync(child);

        if (!context.Success)
        {
            _logger.LogError(context.Error, "The platform could not remove child tenant '{Tenant}': {Error}", child.Name, context.ErrorMessage);

            return TenantHierarchyResult.Failure(S["The child tenant '{0}' could not be removed: {1}", child[TenantHierarchyConstants.SettingsKeys.Description] ?? child.Name, context.ErrorMessage]);
        }

        _ = Enum.TryParse<ChildDatabaseStrategy>(child[TenantHierarchyConstants.SettingsKeys.DatabaseStrategy], out var strategy);

        await _databaseProvisioning.DeprovisionAsync(
            child.Name,
            strategy,
            child[TenantHierarchyConstants.SettingsKeys.DatabasePool],
            child[TenantHierarchyConstants.SettingsKeys.ProvisionedResource]);

        return TenantHierarchyResult.Success;
    }

    private static HierarchyTreeNode ToChildNode(ShellSettings child, string scheme)
    {
        return new HierarchyTreeNode
        {
            Settings = child,
            DisplayName = child[TenantHierarchyConstants.SettingsKeys.Description] ?? child.Name,
            Address = TenantHierarchyUrls.GetBaseAddress(child, scheme),
        };
    }

    private ShellSettings GetParent(string parentName)
    {
        return !string.IsNullOrEmpty(parentName) && _shellHost.TryGetSettings(parentName, out var parent) && parent.IsParentTenant()
            ? parent
            : null;
    }

    private IEnumerable<ShellSettings> GetChildren(ShellSettings parent)
        => _shellHost.GetAllSettings().Where(settings => settings.IsChildOf(parent));

    private async Task<T> InScopeAsync<T>(ShellSettings target, Func<IServiceProvider, Task<T>> operation)
    {
        try
        {
            var scope = await _shellHost.GetScopeAsync(target);
            var result = default(T);

            await scope.UsingAsync(async shellScope =>
            {
                result = await operation(shellScope.ServiceProvider);
            });

            return result;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            _logger.LogError(ex, "The platform could not open the scope of tenant '{Tenant}'.", target.Name);

            return default;
        }
    }

    private string GetScheme()
        => TenantHierarchyUrls.GetScheme(_options, _environment, _httpContextAccessor.HttpContext?.Request);

    private TenantHierarchyResult NotFound()
        => TenantHierarchyResult.Failure(S["The tenant was not found."]);

    private void EnsureDefault()
    {
        if (!_shellSettings.IsDefaultShell())
        {
            throw new TenantHierarchyAccessDeniedException("Only the Default tenant manages the tenant hierarchy.");
        }
    }
}
