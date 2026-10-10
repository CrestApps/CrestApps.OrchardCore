using System.Security.Cryptography;
using CrestApps.OrchardCore.TenantHierarchy.Core.Guards;
using CrestApps.OrchardCore.TenantHierarchy.Core.Provisioning;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Abstractions.Setup;
using OrchardCore.BackgroundTasks;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Models;
using OrchardCore.Environment.Shell.Removing;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using OrchardCore.Security.Services;
using OrchardCore.Settings;
using OrchardCore.Setup.Services;
using OrchardCore.Users;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Services;

/// <summary>
/// The default <see cref="ITenantHierarchyBroker"/>. It is the only type of the tenant hierarchy that opens another
/// tenant's scope or changes another tenant, and it marks each such call with <see cref="BrokerCallContext"/> so the
/// <see cref="GuardedShellHost"/> lets exactly that counterpart through.
/// </summary>
public sealed class TenantHierarchyBroker : ITenantHierarchyBroker
{
    private const int MaxRoleCatalogChildren = 10;

    private static readonly TimeSpan _callTimeout = TimeSpan.FromSeconds(30);

    private static readonly string[] _systemRoles =
    [
        "Anonymous",
        "Authenticated",
    ];

    private static readonly string[] _defaultRoles =
    [
        "Administrator",
        "Editor",
        "Moderator",
        "Author",
        "Contributor",
    ];

    private readonly ShellSettings _shellSettings;
    private readonly IShellHost _shellHost;
    private readonly IShellSettingsManager _shellSettingsManager;
    private readonly IShellRemovalManager _shellRemovalManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly ChildDatabaseProvisioning _databaseProvisioning;
    private readonly TenantHierarchyOptions _options;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWebHostEnvironment _environment;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantHierarchyBroker"/> class.
    /// </summary>
    /// <param name="shellSettings">The settings of the tenant whose code is running.</param>
    /// <param name="shellHost">The shell host.</param>
    /// <param name="shellSettingsManager">The shell settings manager.</param>
    /// <param name="shellRemovalManager">The shell removal manager.</param>
    /// <param name="serviceProvider">The services of the current tenant scope.</param>
    /// <param name="databaseProvisioning">The database provisioning service.</param>
    /// <param name="options">The tenant hierarchy options.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TenantHierarchyBroker(
        ShellSettings shellSettings,
        IShellHost shellHost,
        IShellSettingsManager shellSettingsManager,
        IShellRemovalManager shellRemovalManager,
        IServiceProvider serviceProvider,
        ChildDatabaseProvisioning databaseProvisioning,
        IOptions<TenantHierarchyOptions> options,
        IHttpContextAccessor httpContextAccessor,
        IWebHostEnvironment environment,
        IClock clock,
        ILogger<TenantHierarchyBroker> logger,
        IStringLocalizer<TenantHierarchyBroker> stringLocalizer)
    {
        _shellSettings = shellSettings;
        _shellHost = shellHost;
        _shellSettingsManager = shellSettingsManager;
        _shellRemovalManager = shellRemovalManager;
        _serviceProvider = serviceProvider;
        _databaseProvisioning = databaseProvisioning;
        _options = options.Value;
        _httpContextAccessor = httpContextAccessor;
        _environment = environment;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ChildTenantInfo>> ListChildrenAsync()
    {
        EnsureParent();

        var entries = await GetEntryStore().ListAsync();

        return entries.Select(ToInfo).ToList();
    }

    /// <inheritdoc/>
    public async Task<ChildTenantInfo> GetChildAsync(string entryId)
    {
        EnsureParent();

        var entry = await GetEntryStore().FindByEntryIdAsync(entryId);

        return entry is null
            ? null
            : ToInfo(entry);
    }

    /// <inheritdoc/>
    public async Task<bool> IsHostInUseAsync(string host)
    {
        ArgumentException.ThrowIfNullOrEmpty(host);

        if (_shellHost is GuardedShellHost guarded)
        {
            return guarded.Inner.GetAllSettings().Any(settings => settings.RequestUrlHosts.Any(candidate => HostsMatch(candidate, host)));
        }

        foreach (var name in await _shellSettingsManager.LoadSettingsNamesAsync())
        {
            using var settings = (await _shellSettingsManager.LoadSettingsAsync(name)).AsDisposable();

            if (settings.RequestUrlHosts.Any(candidate => HostsMatch(candidate, host)))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<TenantHierarchyResult> CreateChildSettingsAsync(string entryId, ChildDatabaseProvisioningResult database)
    {
        ArgumentNullException.ThrowIfNull(database);

        EnsureParent();

        var store = GetEntryStore();
        var entry = await store.FindByEntryIdAsync(entryId);

        if (entry is null || entry.Status != ChildTenantStatus.Provisioning || !string.IsNullOrEmpty(entry.TenantId))
        {
            return NotFound();
        }

        if (_shellHost is GuardedShellHost guarded && guarded.Inner.TryGetSettings(entry.TenantName, out _))
        {
            return TenantHierarchyResult.Failure(S["The new {0} could not be created. Try again.", GetChildLabel()]);
        }

        var policy = _shellSettings.GetParentPolicy();

        using var child = _shellSettingsManager
            .CreateDefaultSettings()
            .AsUninitialized()
            .AsDisposable();

        child.Name = entry.TenantName;
        child.RequestUrlHost = entry.Host;
        child.RequestUrlPrefix = string.Empty;
        child[TenantHierarchyConstants.SettingsKeys.Category] = _shellSettings.GetHierarchyDisplayName();
        child[TenantHierarchyConstants.SettingsKeys.Description] = entry.DisplayName;
        child["DatabaseProvider"] = database.DatabaseProvider;
        child["ConnectionString"] = database.ConnectionString;
        child["TablePrefix"] = database.TablePrefix;
        child["Schema"] = database.Schema;
        child["DatabaseName"] = database.DatabaseName;
        child["Secret"] = Guid.NewGuid().ToString();
        child["RecipeName"] = entry.RecipeName;
        child[TenantHierarchyConstants.SettingsKeys.Role] = TenantHierarchyConstants.Roles.Child;
        child[TenantHierarchyConstants.SettingsKeys.ParentTenantId] = _shellSettings.TenantId;
        child[TenantHierarchyConstants.SettingsKeys.ParentTenant] = _shellSettings.Name;
        child[TenantHierarchyConstants.SettingsKeys.Slug] = entry.Slug;
        child[TenantHierarchyConstants.SettingsKeys.EntryId] = entry.EntryId;
        child[TenantHierarchyConstants.SettingsKeys.DatabaseStrategy] = entry.DatabaseStrategy.ToString();
        child[TenantHierarchyConstants.SettingsKeys.DatabasePool] = entry.DatabasePool;
        child[TenantHierarchyConstants.SettingsKeys.ProvisionedResource] = entry.ProvisionedResource;
        TenantHierarchySettingsWriter.WriteChildPolicy(child, policy);

        // The Child feature is forced on only after setup: a feature listed in the configuration is part of every
        // shell, including the minimal shell setup starts with, which cannot run the Users feature it depends on.
        await RunForAsync(child.Name, () => _shellHost.UpdateShellSettingsAsync(child));

        entry.TenantId = child.TenantId;
        await store.SaveAsync(entry);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Parent tenant '{ParentTenant}' ({ParentTenantId}) created child tenant '{ChildTenant}' ({ChildTenantId}).",
                _shellSettings.Name,
                _shellSettings.TenantId,
                child.Name,
                child.TenantId);
        }

        return TenantHierarchyResult.Success;
    }

    /// <inheritdoc/>
    public async Task<TenantHierarchyResult> SetupChildAsync(string entryId)
    {
        EnsureParent();

        var store = GetEntryStore();
        var (entry, child) = await ResolveChildAsync(entryId);

        if (child is null || !child.IsUninitialized())
        {
            return NotFound();
        }

        var setupService = _serviceProvider.GetRequiredService<ISetupService>();
        var recipe = (await setupService.GetSetupRecipesAsync())
            .FirstOrDefault(candidate => string.Equals(candidate.Name, entry.RecipeName, StringComparison.OrdinalIgnoreCase));

        if (recipe is null)
        {
            return await FailSetupAsync(store, entry, S["The setup recipe '{0}' is not available.", entry.RecipeName]);
        }

        var userName = $"hierarchy-bootstrap-{RandomNumberGenerator.GetString("abcdefghijklmnopqrstuvwxyz0123456789", 10)}";
        var setupContext = new SetupContext
        {
            ShellSettings = child,
            EnabledFeatures = null,
            Errors = new Dictionary<string, string>(),
            Recipe = recipe,
            Properties = new Dictionary<string, object>
            {
                { SetupConstants.SiteName, entry.DisplayName },
                { SetupConstants.AdminUsername, userName },
                { SetupConstants.AdminEmail, $"{userName}@bootstrap.invalid" },
                { SetupConstants.AdminPassword, CreateBootstrapPassword() },
                { SetupConstants.SiteTimeZone, await GetParentTimeZoneAsync() },
                { SetupConstants.DatabaseProvider, child["DatabaseProvider"] },
                { SetupConstants.DatabaseConnectionString, child["ConnectionString"] },
                { SetupConstants.DatabaseTablePrefix, child["TablePrefix"] },
                { SetupConstants.DatabaseSchema, child["Schema"] },
                { SetupConstants.DatabaseName, child["DatabaseName"] },
            },
        };

        var temporaryHttpContext = false;

        if (_httpContextAccessor.HttpContext is null && ShellScope.Context is not null)
        {
            // Setup records the recipe environment on the current HTTP context, which a background job may not have.
            _httpContextAccessor.HttpContext = ShellScope.Context.CreateHttpContext();
            temporaryHttpContext = true;
        }

        try
        {
            await RunForAsync(child.Name, () => setupService.SetupAsync(setupContext));
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            _logger.LogError(ex, "The setup of child tenant '{ChildTenant}' of parent tenant '{ParentTenant}' failed.", child.Name, _shellSettings.Name);
            setupContext.Errors[string.Empty] = S["An unexpected error occurred during the setup."];
        }
        finally
        {
            if (temporaryHttpContext)
            {
                _httpContextAccessor.HttpContext = null;
            }
        }

        if (setupContext.Errors.Count > 0)
        {
            return await FailSetupAsync(store, entry, string.Join(' ', setupContext.Errors.Values));
        }

        entry.BootstrapUserId = setupContext.Properties.TryGetValue(SetupConstants.AdminUserId, out var adminUserId)
            ? adminUserId?.ToString()
            : null;

        if (_shellHost.TryGetSettings(child.Name, out var setUp))
        {
            TenantHierarchySettingsWriter.EnsureAlwaysEnabledFeature(setUp, TenantHierarchyConstants.Features.Child);
            await RunForAsync(setUp.Name, () => _shellHost.UpdateShellSettingsAsync(setUp));
        }

        if (!string.IsNullOrEmpty(entry.BootstrapUserId) && _shellHost.TryGetSettings(child.Name, out var running))
        {
            await InScopeAsync(running, async services =>
            {
                // The bootstrap administrator exists only because setup needs one. It is disabled directly in the
                // store, so no handler of the child tenant runs with the parent user's request.
                var session = services.GetRequiredService<ISession>();
                var user = await session.Query<User, UserIndex>(index => index.UserId == entry.BootstrapUserId).FirstOrDefaultAsync();

                if (user is not null)
                {
                    user.IsEnabled = false;
                    await session.SaveAsync(user);
                }

                return true;
            });
        }

        entry.Status = ChildTenantStatus.Ready;
        entry.Error = null;
        await store.SaveAsync(entry);

        return TenantHierarchyResult.Success;
    }

    /// <inheritdoc/>
    public async Task<TenantHierarchyResult> UpdateChildSettingsAsync(string entryId)
    {
        EnsureParent();

        var (entry, child) = await ResolveChildAsync(entryId);

        if (child is null)
        {
            return NotFound();
        }

        child.RequestUrlHost = entry.Host;
        child[TenantHierarchyConstants.SettingsKeys.Slug] = entry.Slug;
        child[TenantHierarchyConstants.SettingsKeys.Description] = entry.DisplayName;
        child[TenantHierarchyConstants.SettingsKeys.Category] = _shellSettings.GetHierarchyDisplayName();

        await RunForAsync(child.Name, () => _shellHost.UpdateShellSettingsAsync(child));

        return TenantHierarchyResult.Success;
    }

    /// <inheritdoc/>
    public async Task<TenantHierarchyResult> SuspendChildAsync(string entryId)
    {
        EnsureParent();

        var (_, child) = await ResolveChildAsync(entryId);

        if (child is null)
        {
            return NotFound();
        }

        if (!child.IsRunning())
        {
            return TenantHierarchyResult.Failure(S["Only a running {0} can be suspended.", GetChildLabel()]);
        }

        await RunForAsync(child.Name, () => _shellHost.UpdateShellSettingsAsync(child.AsDisabled()));

        return TenantHierarchyResult.Success;
    }

    /// <inheritdoc/>
    public async Task<TenantHierarchyResult> ResumeChildAsync(string entryId)
    {
        EnsureParent();

        var (_, child) = await ResolveChildAsync(entryId);

        if (child is null)
        {
            return NotFound();
        }

        if (!child.IsDisabled())
        {
            return TenantHierarchyResult.Failure(S["Only a suspended {0} can be resumed.", GetChildLabel()]);
        }

        await RunForAsync(child.Name, () => _shellHost.UpdateShellSettingsAsync(child.AsRunning()));

        return TenantHierarchyResult.Success;
    }

    /// <inheritdoc/>
    public async Task<TenantHierarchyResult> ReloadChildAsync(string entryId)
    {
        EnsureParent();

        var (_, child) = await ResolveChildAsync(entryId);

        if (child is null)
        {
            return NotFound();
        }

        await RunForAsync(child.Name, () => _shellHost.ReloadShellContextAsync(child));

        return TenantHierarchyResult.Success;
    }

    /// <inheritdoc/>
    public async Task<TenantHierarchyResult> RemoveChildAsync(string entryId)
    {
        EnsureParent();

        var (entry, child) = await ResolveChildAsync(entryId);

        if (child is null)
        {
            return NotFound();
        }

        if (!child.IsDisabled() && !child.IsUninitialized())
        {
            return TenantHierarchyResult.Failure(S["Suspend the {0} before you remove it.", GetChildLabel()]);
        }

        var context = await RunForAsync(child.Name, () => _shellRemovalManager.RemoveAsync(child));

        if (!context.Success)
        {
            _logger.LogError(
                context.Error,
                "Parent tenant '{ParentTenant}' could not remove child tenant '{ChildTenant}': {Error}",
                _shellSettings.Name,
                child.Name,
                context.ErrorMessage);

            return TenantHierarchyResult.Failure(S["The {0} could not be removed. Try again later.", GetChildLabel()]);
        }

        await DeprovisionAsync(entry);

        return TenantHierarchyResult.Success;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ChildFeatureInfo>> GetChildFeaturesAsync(string entryId)
    {
        EnsureParent();

        var (_, child) = await ResolveChildAsync(entryId);

        if (child is null || !child.IsRunning())
        {
            return [];
        }

        return await InScopeAsync<IReadOnlyList<ChildFeatureInfo>>(child, async services =>
        {
            var featuresManager = services.GetRequiredService<IShellFeaturesManager>();
            var available = await featuresManager.GetAvailableFeaturesAsync();
            var enabled = (await featuresManager.GetEnabledFeaturesAsync()).Select(feature => feature.Id).ToHashSet(StringComparer.Ordinal);
            var alwaysEnabled = (await featuresManager.GetAlwaysEnabledFeaturesAsync()).Select(feature => feature.Id).ToHashSet(StringComparer.Ordinal);

            return available
                .Where(feature => !IsTheme(feature) && !feature.EnabledByDependencyOnly && !feature.DefaultTenantOnly)
                .Select(feature => new ChildFeatureInfo
                {
                    Id = feature.Id,
                    Name = string.IsNullOrEmpty(feature.Name) ? feature.Id : feature.Name,
                    Category = string.IsNullOrEmpty(feature.Category) ? "Uncategorized" : feature.Category,
                    Description = feature.Description,
                    IsEnabled = enabled.Contains(feature.Id),
                    IsAlwaysEnabled = alwaysEnabled.Contains(feature.Id),
                    Dependencies = feature.Dependencies ?? [],
                })
                .OrderBy(feature => feature.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(feature => feature.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }) ?? [];
    }

    /// <inheritdoc/>
    public async Task<TenantHierarchyResult> SetChildFeaturesAsync(string entryId, IEnumerable<string> featureIds, bool enable)
    {
        ArgumentNullException.ThrowIfNull(featureIds);

        EnsureParent();

        var (_, child) = await ResolveChildAsync(entryId);

        if (child is null)
        {
            return NotFound();
        }

        if (!child.IsRunning())
        {
            return TenantHierarchyResult.Failure(S["The features of a {0} can only be changed while it is running.", GetChildLabel()]);
        }

        var ids = featureIds.Where(id => !string.IsNullOrEmpty(id)).ToHashSet(StringComparer.Ordinal);

        if (ids.Count == 0)
        {
            return TenantHierarchyResult.Success;
        }

        var changed = await InScopeAsync(child, async services =>
        {
            var featuresManager = services.GetRequiredService<IShellFeaturesManager>();

            // Only the features the child may enable can be changed, so blocked features never get through and the
            // forced Child feature, which is never available, can never be disabled.
            var available = (await featuresManager.GetAvailableFeaturesAsync())
                .Where(feature => ids.Contains(feature.Id) && !IsTheme(feature) && !feature.DefaultTenantOnly)
                .ToList();

            if (enable)
            {
                await featuresManager.EnableFeaturesAsync(available, force: true);
            }
            else
            {
                var alwaysEnabled = (await featuresManager.GetAlwaysEnabledFeaturesAsync()).Select(feature => feature.Id).ToHashSet(StringComparer.Ordinal);
                await featuresManager.DisableFeaturesAsync(available.Where(feature => !alwaysEnabled.Contains(feature.Id)), force: true);
            }

            return available.Count;
        });

        return changed > 0
            ? TenantHierarchyResult.Success
            : TenantHierarchyResult.Failure(S["None of the selected features can be changed."]);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetChildRolesAsync(string entryId)
    {
        EnsureParent();

        var roles = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        IEnumerable<ChildTenantEntry> entries;

        if (string.IsNullOrEmpty(entryId))
        {
            foreach (var role in _defaultRoles)
            {
                roles.Add(role);
            }

            entries = (await GetEntryStore().ListAsync())
                .Where(entry => entry.Status == ChildTenantStatus.Ready)
                .OrderByDescending(entry => entry.CreatedUtc)
                .Take(MaxRoleCatalogChildren);
        }
        else
        {
            var entry = await GetEntryStore().FindByEntryIdAsync(entryId);
            entries = entry is null ? [] : [entry];
        }

        foreach (var entry in entries)
        {
            var (_, child) = await ResolveChildAsync(entry);

            if (child is null || !child.IsRunning())
            {
                continue;
            }

            var childRoles = await InScopeAsync(child, async services =>
            {
                var roleService = services.GetService<IRoleService>();

                return roleService is null
                    ? []
                    : (await roleService.GetRolesAsync()).Select(role => role.RoleName).ToArray();
            });

            foreach (var role in childRoles ?? [])
            {
                if (!_systemRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
                {
                    roles.Add(role);
                }
            }
        }

        return roles.ToList();
    }

    /// <inheritdoc/>
    public Task<string> GetParentAddressAsync()
    {
        EnsureChild();

        var parent = ResolveParent();

        return Task.FromResult(parent is null
            ? null
            : TenantHierarchyUrls.GetBaseAddress(parent, GetScheme()));
    }

    /// <inheritdoc/>
    public async Task<DelegatedAccessRedemption> RedeemCodeAsync(string code, string codeVerifier)
    {
        EnsureChild();

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(codeVerifier))
        {
            return null;
        }

        var parent = ResolveParent();

        if (parent is null || !parent.IsRunning())
        {
            return null;
        }

        var codeHash = DelegatedAccessTokens.Hash(code);
        var childTenantId = _shellSettings.TenantId;
        var ipAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var scheme = GetScheme();

        return await InScopeAsync(parent, async services =>
        {
            var codes = services.GetRequiredService<DelegatedAccessCodeStore>();
            var record = await codes.FindByHashAsync(codeHash);
            var now = _clock.UtcNow;

            if (record is null || record.Redeemed || record.ExpiresUtc < now ||
                !string.Equals(record.ChildTenantId, childTenantId, StringComparison.Ordinal))
            {
                return null;
            }

            var verified = DelegatedAccessTokens.VerifyCodeChallenge(codeVerifier, record.CodeChallenge);

            try
            {
                // A code is burned by the first attempt, even a failed one, so a verifier cannot be guessed.
                await codes.MarkRedeemedAsync(record);
            }
            catch (ConcurrencyException)
            {
                return null;
            }

            if (!verified)
            {
                return null;
            }

            var entry = await services.GetRequiredService<ChildTenantEntryStore>().FindByEntryIdAsync(record.ChildEntryId);

            if (entry is null || entry.Status != ChildTenantStatus.Ready ||
                !string.Equals(entry.TenantId, childTenantId, StringComparison.Ordinal))
            {
                return null;
            }

            var user = await FindParentUserAsync(services, record.ParentUserId);

            if (user is null || !user.IsEnabled)
            {
                return null;
            }

            var grants = await services.GetRequiredService<AccessGrantStore>().ListForPrincipalAsync(user.UserId, user.RoleNames);
            var roles = AccessGrantResolver.ResolveChildRoles(grants, entry.EntryId);

            if (roles.Length == 0)
            {
                return null;
            }

            var policy = parent.GetParentPolicy();
            var sessionId = DelegatedAccessTokens.CreateToken();
            var session = new DelegatedAccessSession
            {
                SessionHash = DelegatedAccessTokens.Hash(sessionId),
                ChildEntryId = entry.EntryId,
                ChildTenantId = childTenantId,
                ParentUserId = user.UserId,
                ParentUserName = user.UserName,
                ParentSessionId = record.ParentSessionId,
                SecurityStamp = user.SecurityStamp,
                AuthenticationMethods = record.AuthenticationMethods ?? [],
                IpAddress = ipAddress,
                CreatedUtc = now,
                LastSeenUtc = now,
            };

            await services.GetRequiredService<DelegatedAccessSessionStore>().SaveAsync(session);
            await services.GetRequiredService<HierarchyAuditLog>().RecordAsync(new HierarchyAuditEvent
            {
                Name = HierarchyAuditEventNames.Entered,
                ChildEntryId = entry.EntryId,
                ChildDisplayName = entry.DisplayName,
                UserId = user.UserId,
                UserName = user.UserName,
                IpAddress = ipAddress,
                SessionReference = DelegatedAccessTokens.GetReference(session.SessionHash),
                AuthenticationMethods = session.AuthenticationMethods,
                Details = string.Join(", ", roles),
            });

            await services.GetRequiredService<TenantSwitcherPreferenceStore>().AddRecentAsync(user.UserId, entry.EntryId);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Child tenant '{ChildTenant}' ({ChildTenantId}) redeemed a delegated access code of parent tenant '{ParentTenant}' ({ParentTenantId}) for user '{ParentUserId}'.",
                    _shellSettings.Name,
                    childTenantId,
                    parent.Name,
                    parent.TenantId,
                    user.UserId);
            }

            return new DelegatedAccessRedemption
            {
                ParentTenantId = parent.TenantId,
                ParentDisplayName = parent.GetHierarchyDisplayName(),
                ParentSlug = parent.GetHierarchySlug(),
                ParentAddress = TenantHierarchyUrls.GetBaseAddress(parent, scheme),
                ChildLabel = policy.Labels?.Child,
                SwitcherMode = policy.SwitcherMode,
                ParentUserId = user.UserId,
                ParentUserName = user.UserName,
                Email = user.Email,
                ChildRoles = roles,
                RolesVersion = DelegatedAccessTokens.ComputeRolesVersion(roles),
                SessionId = sessionId,
                AuthenticationMethods = session.AuthenticationMethods,
                ValidationInterval = policy.SessionValidationInterval,
            };
        });
    }

    /// <inheritdoc/>
    public async Task<DelegatedSessionValidation> ValidateSessionAsync(string sessionId)
    {
        EnsureChild();

        if (string.IsNullOrEmpty(sessionId))
        {
            return DelegatedSessionValidation.Inactive;
        }

        var parent = ResolveParent();

        if (parent is null || !parent.IsRunning())
        {
            return DelegatedSessionValidation.Inactive;
        }

        var sessionHash = DelegatedAccessTokens.Hash(sessionId);
        var childTenantId = _shellSettings.TenantId;

        return await InScopeAsync(parent, async services =>
        {
            var sessions = services.GetRequiredService<DelegatedAccessSessionStore>();
            var session = await sessions.FindByHashAsync(sessionHash);

            if (session is null || !string.Equals(session.ChildTenantId, childTenantId, StringComparison.Ordinal))
            {
                return DelegatedSessionValidation.Inactive;
            }

            var now = _clock.UtcNow;
            var entry = await services.GetRequiredService<ChildTenantEntryStore>().FindByEntryIdAsync(session.ChildEntryId);
            var childIsReady = entry is not null &&
                entry.Status == ChildTenantStatus.Ready &&
                string.Equals(entry.TenantId, childTenantId, StringComparison.Ordinal);

            var user = await FindParentUserAsync(services, session.ParentUserId);
            var roles = Array.Empty<string>();

            if (user is not null && childIsReady)
            {
                var grants = await services.GetRequiredService<AccessGrantStore>().ListForPrincipalAsync(user.UserId, user.RoleNames);
                roles = AccessGrantResolver.ResolveChildRoles(grants, entry.EntryId);
            }

            var reason = DelegatedSessionRules.GetEndReason(
                session,
                parent.GetParentPolicy(),
                now,
                user?.IsEnabled == true,
                user?.SecurityStamp,
                childIsReady,
                roles);

            if (reason is not null)
            {
                if (!session.EndedUtc.HasValue)
                {
                    await EndSessionAsync(services, session, entry, reason);
                }

                return DelegatedSessionValidation.Inactive;
            }

            session.LastSeenUtc = now;
            await sessions.SaveAsync(session);

            return new DelegatedSessionValidation
            {
                IsActive = true,
                ChildRoles = roles,
                RolesVersion = DelegatedAccessTokens.ComputeRolesVersion(roles),
            };
        }) ?? DelegatedSessionValidation.Inactive;
    }

    /// <inheritdoc/>
    public async Task EndSessionAsync(string sessionId, string reason)
    {
        EnsureChild();

        if (string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        var parent = ResolveParent();

        if (parent is null || !parent.IsRunning())
        {
            return;
        }

        var sessionHash = DelegatedAccessTokens.Hash(sessionId);
        var childTenantId = _shellSettings.TenantId;

        await InScopeAsync(parent, async services =>
        {
            var session = await services.GetRequiredService<DelegatedAccessSessionStore>().FindByHashAsync(sessionHash);

            if (session is null || session.EndedUtc.HasValue || !string.Equals(session.ChildTenantId, childTenantId, StringComparison.Ordinal))
            {
                return false;
            }

            var entry = await services.GetRequiredService<ChildTenantEntryStore>().FindByEntryIdAsync(session.ChildEntryId);
            await EndSessionAsync(services, session, entry, reason ?? DelegatedSessionRules.ChildSignOut);

            return true;
        });
    }

    /// <summary>
    /// Returns the address of a child tenant of the current parent, from its shell settings.
    /// </summary>
    /// <param name="child">The shell settings of the child tenant.</param>
    internal string GetChildAddress(ShellSettings child)
        => TenantHierarchyUrls.GetBaseAddress(child, GetScheme());

    private async Task EndSessionAsync(IServiceProvider services, DelegatedAccessSession session, ChildTenantEntry entry, string reason)
    {
        session.EndedUtc = _clock.UtcNow;
        session.EndReason = reason;
        await services.GetRequiredService<DelegatedAccessSessionStore>().SaveAsync(session);
        await services.GetRequiredService<HierarchyAuditLog>().RecordAsync(new HierarchyAuditEvent
        {
            Name = HierarchyAuditEventNames.SessionEnded,
            ChildEntryId = session.ChildEntryId,
            ChildDisplayName = entry?.DisplayName,
            UserId = session.ParentUserId,
            UserName = session.ParentUserName,
            SessionReference = DelegatedAccessTokens.GetReference(session.SessionHash),
            Details = reason,
        });
    }

    private static async Task<User> FindParentUserAsync(IServiceProvider services, string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        // The parent user is read straight from the parent store, so no user service of the parent runs with the
        // child tenant's request.
        var session = services.GetRequiredService<ISession>();

        return await session.Query<User, UserIndex>(index => index.UserId == userId).FirstOrDefaultAsync();
    }

    private ChildTenantInfo ToInfo(ChildTenantEntry entry)
    {
        var child = ResolveChildSettings(entry);

        if (child is null)
        {
            return new ChildTenantInfo
            {
                Entry = entry,
                State = ChildTenantRuntimeState.ChangedByPlatform,
            };
        }

        return new ChildTenantInfo
        {
            Entry = entry,
            State = child.State switch
            {
                TenantState.Running => ChildTenantRuntimeState.Running,
                TenantState.Disabled => ChildTenantRuntimeState.Suspended,
                TenantState.Initializing => ChildTenantRuntimeState.Initializing,
                _ => ChildTenantRuntimeState.Uninitialized,
            },
            Address = GetChildAddress(child),
        };
    }

    private async Task<(ChildTenantEntry Entry, ShellSettings Child)> ResolveChildAsync(string entryId)
    {
        var entry = await GetEntryStore().FindByEntryIdAsync(entryId);

        return entry is null
            ? (null, null)
            : await ResolveChildAsync(entry);
    }

    private Task<(ChildTenantEntry Entry, ShellSettings Child)> ResolveChildAsync(ChildTenantEntry entry)
        => Task.FromResult((entry, ResolveChildSettings(entry)));

    private ShellSettings ResolveChildSettings(ChildTenantEntry entry)
    {
        // The link must hold both ways: the registry of this parent names the tenant, and the tenant's own
        // host-controlled settings name this parent.
        if (string.IsNullOrEmpty(entry?.TenantName) ||
            string.IsNullOrEmpty(entry.TenantId) ||
            !_shellHost.TryGetSettings(entry.TenantName, out var child) ||
            !child.IsChildOf(_shellSettings) ||
            !string.Equals(child.TenantId, entry.TenantId, StringComparison.Ordinal))
        {
            return null;
        }

        return child;
    }

    private ShellSettings ResolveParent()
    {
        var parentName = _shellSettings[TenantHierarchyConstants.SettingsKeys.ParentTenant];
        var parentTenantId = _shellSettings.GetParentTenantId();

        if (string.IsNullOrEmpty(parentName) || string.IsNullOrEmpty(parentTenantId))
        {
            return null;
        }

        // Reading the parent's settings is itself a cross-tenant call, so it runs as a broker call too.
        var parent = BrokerCallContext.Run(parentName, () => _shellHost.TryGetSettings(parentName, out var found) ? found : null);

        if (parent is null ||
            !parent.IsParentTenant() ||
            !string.Equals(parent.TenantId, parentTenantId, StringComparison.Ordinal))
        {
            return null;
        }

        return parent;
    }

    private async Task<T> InScopeAsync<T>(ShellSettings target, Func<IServiceProvider, Task<T>> operation)
    {
        try
        {
            return await RunForAsync(target.Name, async () =>
            {
                var scope = await _shellHost.GetScopeAsync(target);
                var result = default(T);

                await scope.UsingAsync(async shellScope =>
                {
                    result = await operation(shellScope.ServiceProvider);
                });

                return result;
            }).WaitAsync(_callTimeout);
        }
        catch (Exception ex) when (ex is not TenantHierarchyAccessDeniedException && !ex.IsFatal())
        {
            _logger.LogError(
                ex,
                "A tenant hierarchy call from tenant '{CurrentTenant}' ({CurrentTenantId}) to tenant '{TargetTenant}' ({TargetTenantId}) failed.",
                _shellSettings.Name,
                _shellSettings.TenantId,
                target.Name,
                target.TenantId);

            return default;
        }
    }

    private static Task RunForAsync(string tenantName, Func<Task> operation)
        => BrokerCallContext.RunAsync(tenantName, operation);

    private static Task<T> RunForAsync<T>(string tenantName, Func<Task<T>> operation)
        => BrokerCallContext.RunAsync(tenantName, operation);

    private Task DeprovisionAsync(ChildTenantEntry entry)
        => _databaseProvisioning.DeprovisionAsync(entry.TenantName, entry.DatabaseStrategy, entry.DatabasePool, entry.ProvisionedResource);

    private static async Task<TenantHierarchyResult> FailSetupAsync(ChildTenantEntryStore store, ChildTenantEntry entry, string error)
    {
        entry.Status = ChildTenantStatus.Failed;
        entry.Error = error;
        await store.SaveAsync(entry);

        return TenantHierarchyResult.Failure(error);
    }

    private async Task<string> GetParentTimeZoneAsync()
    {
        var siteService = _serviceProvider.GetService<ISiteService>();

        if (siteService is null)
        {
            return null;
        }

        return (await siteService.GetSiteSettingsAsync())?.TimeZoneId;
    }

    private static string CreateBootstrapPassword()
    {
        const string letters = "abcdefghijkmnopqrstuvwxyz";
        const string capitals = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string digits = "23456789";
        const string symbols = "!@#$%^&*-_=+";

        // The password is never stored or shown: the account is disabled right after setup.
        return RandomNumberGenerator.GetString(letters + capitals + digits + symbols, 60) +
            RandomNumberGenerator.GetString(letters, 1) +
            RandomNumberGenerator.GetString(capitals, 1) +
            RandomNumberGenerator.GetString(digits, 1) +
            RandomNumberGenerator.GetString(symbols, 1);
    }

    private string GetScheme()
        => TenantHierarchyUrls.GetScheme(_options, _environment, _httpContextAccessor.HttpContext?.Request);

    private string GetChildLabel()
    {
        var label = _shellSettings.GetParentPolicy().Labels?.Child;

        return string.IsNullOrWhiteSpace(label)
            ? S["child tenant"]
            : label;
    }

    private TenantHierarchyResult NotFound()
        => TenantHierarchyResult.Failure(S["The {0} was not found.", GetChildLabel()]);

    private ChildTenantEntryStore GetEntryStore()
        => _serviceProvider.GetRequiredService<ChildTenantEntryStore>();

    private void EnsureParent()
    {
        if (!_shellSettings.IsParentTenant())
        {
            throw new TenantHierarchyAccessDeniedException("Only a parent tenant can manage child tenants.");
        }
    }

    private void EnsureChild()
    {
        if (!_shellSettings.IsChildTenant())
        {
            throw new TenantHierarchyAccessDeniedException("Only a child tenant can call its parent.");
        }
    }

    private static bool IsTheme(IFeatureInfo feature)
    {
        return string.Equals(feature.Extension?.Manifest?.Type, "Theme", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(feature.Id, feature.Extension.Id, StringComparison.Ordinal);
    }

    private static bool HostsMatch(string candidate, string host)
        => string.Equals(candidate?.Trim(), host?.Trim(), StringComparison.OrdinalIgnoreCase);
}
