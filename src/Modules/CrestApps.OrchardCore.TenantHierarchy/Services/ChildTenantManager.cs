using System.Security.Claims;
using CrestApps.OrchardCore.TenantHierarchy.Core;
using CrestApps.OrchardCore.TenantHierarchy.Core.Provisioning;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.BackgroundJobs;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Setup.Services;

namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// Manages the child tenants of the current parent tenant: it validates requests against the parent policy, keeps the
/// registry and the activity log, and calls the broker for everything that touches a child tenant.
/// </summary>
public sealed class ChildTenantManager
{
    private const int MaxDisplayNameLength = 100;
    private const int MaxDescriptionLength = 500;

    private static readonly TimeSpan _quotaLockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _quotaLockExpiration = TimeSpan.FromMinutes(2);

    private readonly ITenantHierarchyBroker _broker;
    private readonly ChildTenantEntryStore _entryStore;
    private readonly AccessGrantStore _grantStore;
    private readonly DelegatedAccessSessionStore _sessionStore;
    private readonly HierarchyAuditLog _auditLog;
    private readonly ChildDatabaseProvisioning _databaseProvisioning;
    private readonly TenantHierarchyOptions _options;
    private readonly IDistributedLock _distributedLock;
    private readonly ShellSettings _shellSettings;
    private readonly IServiceProvider _serviceProvider;
    private readonly HierarchyLabelsProvider _labelsProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChildTenantManager"/> class.
    /// </summary>
    /// <param name="broker">The tenant hierarchy broker.</param>
    /// <param name="entryStore">The registry store.</param>
    /// <param name="grantStore">The access grant store.</param>
    /// <param name="sessionStore">The delegated access session store.</param>
    /// <param name="auditLog">The hierarchy activity log.</param>
    /// <param name="databaseProvisioning">The database provisioning service.</param>
    /// <param name="options">The tenant hierarchy options.</param>
    /// <param name="distributedLock">The distributed lock.</param>
    /// <param name="shellSettings">The settings of the parent tenant.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="labelsProvider">The labels provider.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ChildTenantManager(
        ITenantHierarchyBroker broker,
        ChildTenantEntryStore entryStore,
        AccessGrantStore grantStore,
        DelegatedAccessSessionStore sessionStore,
        HierarchyAuditLog auditLog,
        ChildDatabaseProvisioning databaseProvisioning,
        IOptions<TenantHierarchyOptions> options,
        IDistributedLock distributedLock,
        ShellSettings shellSettings,
        IServiceProvider serviceProvider,
        HierarchyLabelsProvider labelsProvider,
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        ILogger<ChildTenantManager> logger,
        IStringLocalizer<ChildTenantManager> stringLocalizer)
    {
        _broker = broker;
        _entryStore = entryStore;
        _grantStore = grantStore;
        _sessionStore = sessionStore;
        _auditLog = auditLog;
        _databaseProvisioning = databaseProvisioning;
        _options = options.Value;
        _distributedLock = distributedLock;
        _shellSettings = shellSettings;
        _serviceProvider = serviceProvider;
        _labelsProvider = labelsProvider;
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets a value indicating whether the host-level guards of <c>AddTenantHierarchy()</c> are installed. Without
    /// them, no child tenant may be created.
    /// </summary>
    public bool IsHostGuardInstalled => _serviceProvider.GetService<TenantHierarchyHostMarker>() is not null;

    /// <summary>
    /// Returns the policy of the current parent tenant.
    /// </summary>
    public ParentTenantPolicy GetPolicy()
        => _shellSettings.GetParentPolicy();

    /// <summary>
    /// Returns the host pattern of child tenants: the one of the policy, or <c>{business}.{parent host}</c>.
    /// </summary>
    public string GetHostPattern()
    {
        var pattern = GetPolicy().ChildHostPattern;

        if (!string.IsNullOrWhiteSpace(pattern))
        {
            return pattern.Trim();
        }

        var parentHost = _shellSettings.GetPrimaryHost();

        return string.IsNullOrEmpty(parentHost)
            ? null
            : TenantHierarchyNaming.GetDefaultHostPattern(parentHost);
    }

    /// <summary>
    /// Returns the setup recipes the policy allows.
    /// </summary>
    public async Task<IReadOnlyList<RecipeDescriptor>> GetAllowedRecipesAsync()
    {
        var setupService = _serviceProvider.GetRequiredService<ISetupService>();
        var allowed = GetPolicy().Recipes;
        var recipes = (await setupService.GetSetupRecipesAsync())
            .Where(recipe => !recipe.Tags?.Contains("hidden", StringComparer.OrdinalIgnoreCase) ?? true);

        if (allowed.Length > 0)
        {
            recipes = recipes.Where(recipe => allowed.Contains(recipe.Name, StringComparer.OrdinalIgnoreCase));
        }

        return recipes.OrderBy(recipe => recipe.DisplayName ?? recipe.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Validates a creation request against the parent policy and returns the errors by field name. An empty result
    /// means the request is valid.
    /// </summary>
    /// <param name="request">The creation request.</param>
    public async Task<IReadOnlyDictionary<string, string>> ValidateCreateAsync(CreateChildTenantRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new Dictionary<string, string>(StringComparer.Ordinal);

        ValidateDisplayName(request.DisplayName, errors);
        ValidateDescription(request.Description, errors);
        await ValidateSlugAsync(request.Slug, null, errors);

        var recipes = await GetAllowedRecipesAsync();

        if (string.IsNullOrEmpty(request.RecipeName) ||
            !recipes.Any(recipe => string.Equals(recipe.Name, request.RecipeName, StringComparison.OrdinalIgnoreCase)))
        {
            errors[nameof(CreateChildTenantRequest.RecipeName)] = S["Select one of the available setup recipes."];
        }

        return errors;
    }

    /// <summary>
    /// Creates a child tenant: it checks the quota under a lock, places the database, writes the registry entry and
    /// the shell settings. The setup runs afterwards, see <see cref="ScheduleSetup(string)"/>.
    /// </summary>
    /// <param name="request">The creation request.</param>
    public async Task<(TenantHierarchyResult Result, ChildTenantEntry Entry)> CreateAsync(CreateChildTenantRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var labels = _labelsProvider.GetLabels();

        if (!_shellSettings.IsParentTenant() || !IsHostGuardInstalled)
        {
            return (TenantHierarchyResult.Failure(S["This tenant cannot create {0}. Ask the platform administrator to finish the setup of the tenant hierarchy.", labels.ChildrenLower]), null);
        }

        var errors = await ValidateCreateAsync(request);

        if (errors.Count > 0)
        {
            return (TenantHierarchyResult.Failure(errors.Values.First()), null);
        }

        var policy = GetPolicy();
        var slug = request.Slug.Trim().ToLowerInvariant();

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            $"TenantHierarchy:Create:{_shellSettings.TenantId}",
            _quotaLockTimeout,
            _quotaLockExpiration);

        if (!locked)
        {
            return (TenantHierarchyResult.Failure(S["Another {0} is being created. Try again in a moment.", labels.ChildLower]), null);
        }

        ChildTenantEntry entry;

        await using (locker)
        {
            if (await _entryStore.CountAsync() >= policy.MaxChildren)
            {
                return (TenantHierarchyResult.Failure(S["You have reached the limit of {0} {1}. Remove one, or ask the platform administrator for a higher limit.", policy.MaxChildren, labels.ChildrenLower]), null);
            }

            if (await _entryStore.FindBySlugAsync(slug) is not null)
            {
                return (TenantHierarchyResult.Failure(S["This address is already in use."]), null);
            }

            var actor = GetActor();

            entry = new ChildTenantEntry
            {
                EntryId = IdGenerator.GenerateId(),
                TenantName = TenantHierarchyNaming.GenerateTenantName(),
                Slug = slug,
                DisplayName = request.DisplayName.Trim(),
                Description = request.Description?.Trim(),
                Host = TenantHierarchyNaming.BuildHost(GetHostPattern(), slug),
                RecipeName = request.RecipeName,
                Status = ChildTenantStatus.Provisioning,
                DatabaseStrategy = policy.DatabaseStrategy,
                DatabasePool = policy.DatabasePool,
                CreatedUtc = _clock.UtcNow,
                CreatedById = request.ActorUserId ?? actor.UserId,
                CreatedByName = request.ActorUserName ?? actor.UserName,
            };

            var database = await ProvisionDatabaseAsync(entry, policy);

            if (!database.Succeeded)
            {
                return (TenantHierarchyResult.Failure(database.Error), null);
            }

            entry.ProvisionedResource = database.ProvisionedResource;

            await _entryStore.SaveAsync(entry);
            await _entryStore.SaveChangesAsync();

            var result = await _broker.CreateChildSettingsAsync(entry.EntryId, database);

            if (!result.Succeeded)
            {
                _entryStore.Delete(entry);
                await _entryStore.SaveChangesAsync();

                return (result, null);
            }

            entry = await _entryStore.FindByEntryIdAsync(entry.EntryId);
            await RecordAsync(HierarchyAuditEventNames.Created, entry, $"{entry.Host} ({entry.RecipeName})");
            await _entryStore.SaveChangesAsync();
        }

        return (TenantHierarchyResult.Success, entry);
    }

    /// <summary>
    /// Runs the setup of a child tenant after the current request ends.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    public static Task ScheduleSetup(string entryId)
    {
        return HttpBackgroundJob.ExecuteAfterEndOfRequestAsync("TenantHierarchy.SetupChild", async scope =>
        {
            var manager = scope.ServiceProvider.GetRequiredService<ChildTenantManager>();
            await manager.SetupAsync(entryId);
        });
    }

    /// <summary>
    /// Runs the setup of a child tenant now and records the outcome.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    public async Task<TenantHierarchyResult> SetupAsync(string entryId)
    {
        var result = await _broker.SetupChildAsync(entryId);
        var entry = await _entryStore.FindByEntryIdAsync(entryId);

        if (entry is not null)
        {
            await RecordAsync(
                result.Succeeded ? HierarchyAuditEventNames.SetupSucceeded : HierarchyAuditEventNames.SetupFailed,
                entry,
                result.Error);
        }

        return result;
    }

    /// <summary>
    /// Changes the display name, slug and description of a child tenant. A new slug changes its host.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="slug">The slug.</param>
    /// <param name="description">The description.</param>
    public async Task<(TenantHierarchyResult Result, IReadOnlyDictionary<string, string> Errors)> UpdateAsync(
        string entryId,
        string displayName,
        string slug,
        string description)
    {
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        var info = await _broker.GetChildAsync(entryId);

        if (info is null || info.State == ChildTenantRuntimeState.ChangedByPlatform)
        {
            return (NotFound(), errors);
        }

        var entry = info.Entry;
        slug = slug?.Trim().ToLowerInvariant();

        ValidateDisplayName(displayName, errors);
        ValidateDescription(description, errors);

        if (!string.Equals(slug, entry.Slug, StringComparison.Ordinal))
        {
            await ValidateSlugAsync(slug, entry.EntryId, errors);
        }

        if (errors.Count > 0)
        {
            return (TenantHierarchyResult.Failure(errors.Values.First()), errors);
        }

        var previousHost = entry.Host;
        entry.DisplayName = displayName.Trim();
        entry.Description = description?.Trim();
        entry.Slug = slug;
        entry.Host = TenantHierarchyNaming.BuildHost(GetHostPattern(), slug);

        await _entryStore.SaveAsync(entry);
        await _entryStore.SaveChangesAsync();

        var result = await _broker.UpdateChildSettingsAsync(entryId);

        if (result.Succeeded)
        {
            await RecordAsync(
                HierarchyAuditEventNames.Edited,
                entry,
                previousHost == entry.Host ? null : $"{previousHost} → {entry.Host}");
        }

        return (result, errors);
    }

    /// <summary>
    /// Suspends a child tenant. Its own users are stopped too.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    public Task<TenantHierarchyResult> SuspendAsync(string entryId)
        => RunAsync(entryId, _broker.SuspendChildAsync, HierarchyAuditEventNames.Suspended);

    /// <summary>
    /// Resumes a suspended child tenant.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    public async Task<TenantHierarchyResult> ResumeAsync(string entryId)
    {
        var entry = await _entryStore.FindByEntryIdAsync(entryId);

        if (entry?.Status == ChildTenantStatus.PendingRemoval)
        {
            return TenantHierarchyResult.Failure(S["Restore the {0} before you resume it.", _labelsProvider.GetLabels().ChildLower]);
        }

        return await RunAsync(entryId, _broker.ResumeChildAsync, HierarchyAuditEventNames.Resumed);
    }

    /// <summary>
    /// Reloads a child tenant.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    public Task<TenantHierarchyResult> ReloadAsync(string entryId)
        => RunAsync(entryId, _broker.ReloadChildAsync, HierarchyAuditEventNames.Reloaded);

    /// <summary>
    /// Removes a suspended child tenant, at once or after the grace period of the policy.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    public async Task<TenantHierarchyResult> RemoveAsync(string entryId)
    {
        var info = await _broker.GetChildAsync(entryId);

        if (info is null || info.State == ChildTenantRuntimeState.ChangedByPlatform)
        {
            return NotFound();
        }

        if (info.State is ChildTenantRuntimeState.Running or ChildTenantRuntimeState.Initializing)
        {
            return TenantHierarchyResult.Failure(S["Suspend the {0} before you remove it.", _labelsProvider.GetLabels().ChildLower]);
        }

        var graceDays = GetPolicy().RemovalGraceDays;

        if (graceDays > 0 && info.Entry.Status != ChildTenantStatus.PendingRemoval && info.State == ChildTenantRuntimeState.Suspended)
        {
            info.Entry.Status = ChildTenantStatus.PendingRemoval;
            info.Entry.RetainUntilUtc = _clock.UtcNow.AddDays(graceDays);
            await _entryStore.SaveAsync(info.Entry);
            await EndSessionsOfChildAsync(entryId);
            await RecordAsync(HierarchyAuditEventNames.RemovalScheduled, info.Entry, info.Entry.RetainUntilUtc?.ToString("u"));

            return TenantHierarchyResult.Success;
        }

        return await RemoveNowAsync(info.Entry);
    }

    /// <summary>
    /// Restores a child tenant that is pending removal. It stays suspended until it is resumed.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    public async Task<TenantHierarchyResult> RestoreAsync(string entryId)
    {
        var entry = await _entryStore.FindByEntryIdAsync(entryId);

        if (entry is null || entry.Status != ChildTenantStatus.PendingRemoval)
        {
            return NotFound();
        }

        entry.Status = ChildTenantStatus.Ready;
        entry.RetainUntilUtc = null;
        await _entryStore.SaveAsync(entry);
        await RecordAsync(HierarchyAuditEventNames.Restored, entry, null);

        return TenantHierarchyResult.Success;
    }

    /// <summary>
    /// Removes a child tenant whose setup failed, together with its registry entry.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    public async Task<TenantHierarchyResult> DiscardAsync(string entryId)
    {
        var entry = await _entryStore.FindByEntryIdAsync(entryId);

        if (entry is null || entry.Status != ChildTenantStatus.Failed)
        {
            return NotFound();
        }

        return await RemoveNowAsync(entry);
    }

    /// <summary>
    /// Creates a child tenant whose setup failed again, with the same name, address and recipe, and sets it up.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    public async Task<(TenantHierarchyResult Result, ChildTenantEntry Entry)> RetryAsync(string entryId)
    {
        var entry = await _entryStore.FindByEntryIdAsync(entryId);

        if (entry is null || entry.Status != ChildTenantStatus.Failed)
        {
            return (NotFound(), null);
        }

        var request = new CreateChildTenantRequest
        {
            DisplayName = entry.DisplayName,
            Slug = entry.Slug,
            Description = entry.Description,
            RecipeName = entry.RecipeName,
        };

        var removal = await RemoveNowAsync(entry);

        if (!removal.Succeeded)
        {
            return (removal, null);
        }

        await _entryStore.SaveChangesAsync();

        return await CreateAsync(request);
    }

    /// <summary>
    /// Removes the registry entry of a child tenant that the platform removed or moved to another parent.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    public async Task<TenantHierarchyResult> DismissAsync(string entryId)
    {
        var info = await _broker.GetChildAsync(entryId);

        if (info is null || info.State != ChildTenantRuntimeState.ChangedByPlatform)
        {
            return NotFound();
        }

        await DeleteEntryAsync(info.Entry);
        await RecordAsync(HierarchyAuditEventNames.Dismissed, info.Entry, null);

        return TenantHierarchyResult.Success;
    }

    /// <summary>
    /// Removes the child tenants whose grace period ended.
    /// </summary>
    public async Task<int> RemoveDueAsync()
    {
        var count = 0;

        foreach (var entry in await _entryStore.ListDueForRemovalAsync(_clock.UtcNow))
        {
            var result = await RemoveNowAsync(entry);

            if (result.Succeeded)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Enables or disables features in a child tenant and records each change.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    /// <param name="featureIds">The feature identifiers.</param>
    /// <param name="enable"><see langword="true"/> to enable, <see langword="false"/> to disable.</param>
    public async Task<TenantHierarchyResult> SetFeaturesAsync(string entryId, IEnumerable<string> featureIds, bool enable)
    {
        ArgumentNullException.ThrowIfNull(featureIds);

        var ids = featureIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
        var result = await _broker.SetChildFeaturesAsync(entryId, ids, enable);

        if (result.Succeeded)
        {
            var entry = await _entryStore.FindByEntryIdAsync(entryId);
            await RecordAsync(
                enable ? HierarchyAuditEventNames.FeatureEnabled : HierarchyAuditEventNames.FeatureDisabled,
                entry,
                string.Join(", ", ids));
        }

        return result;
    }

    /// <summary>
    /// Records an event in the hierarchy activity log for the current user.
    /// </summary>
    /// <param name="name">The event name.</param>
    /// <param name="entry">The child tenant the event is about, or <see langword="null"/>.</param>
    /// <param name="details">The details.</param>
    public Task RecordAsync(string name, ChildTenantEntry entry, string details)
    {
        var actor = GetActor();

        return _auditLog.RecordAsync(new HierarchyAuditEvent
        {
            Name = name,
            ChildEntryId = entry?.EntryId,
            ChildDisplayName = entry?.DisplayName,
            UserId = actor.UserId,
            UserName = actor.UserName,
            IpAddress = actor.IpAddress,
            Details = details,
        });
    }

    private async Task<TenantHierarchyResult> RemoveNowAsync(ChildTenantEntry entry)
    {
        var info = await _broker.GetChildAsync(entry.EntryId);

        if (info is null)
        {
            return NotFound();
        }

        if (info.State != ChildTenantRuntimeState.ChangedByPlatform)
        {
            var previousStatus = entry.Status;
            entry.Status = ChildTenantStatus.Removing;
            await _entryStore.SaveAsync(entry);
            await _entryStore.SaveChangesAsync();

            var result = await _broker.RemoveChildAsync(entry.EntryId);

            if (!result.Succeeded)
            {
                entry.Status = previousStatus;
                await _entryStore.SaveAsync(entry);
                await RecordAsync(HierarchyAuditEventNames.RemovalFailed, entry, result.Error);

                return result;
            }
        }

        await DeleteEntryAsync(entry);
        await RecordAsync(HierarchyAuditEventNames.Removed, entry, entry.Host);

        return TenantHierarchyResult.Success;
    }

    private async Task DeleteEntryAsync(ChildTenantEntry entry)
    {
        await EndSessionsOfChildAsync(entry.EntryId);
        await _grantStore.DeleteForChildAsync(entry.EntryId);
        _entryStore.Delete(entry);
    }

    private async Task EndSessionsOfChildAsync(string entryId)
    {
        foreach (var session in await _sessionStore.ListOpenByChildAsync(entryId))
        {
            session.EndedUtc = _clock.UtcNow;
            session.EndReason = DelegatedSessionRules.ChildRemoved;
            await _sessionStore.SaveAsync(session);
        }
    }

    private async Task<TenantHierarchyResult> RunAsync(string entryId, Func<string, Task<TenantHierarchyResult>> operation, string eventName)
    {
        var result = await operation(entryId);

        if (result.Succeeded)
        {
            await RecordAsync(eventName, await _entryStore.FindByEntryIdAsync(entryId), null);
        }

        return result;
    }

    private async Task<ChildDatabaseProvisioningResult> ProvisionDatabaseAsync(ChildTenantEntry entry, ParentTenantPolicy policy)
    {
        var result = await _databaseProvisioning.ProvisionAsync(entry.TenantName, policy.DatabaseStrategy, policy.DatabasePool);

        if (!result.Succeeded)
        {
            _logger.LogError(
                "The database of the new child tenant of parent tenant '{ParentTenant}' could not be prepared: {Error}",
                _shellSettings.Name,
                result.Error);

            return ChildDatabaseProvisioningResult.Failed(S["The database for the new {0} could not be prepared. Ask the platform administrator to check the database settings.", _labelsProvider.GetLabels().ChildLower]);
        }

        return result;
    }

    private void ValidateDisplayName(string displayName, Dictionary<string, string> errors)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            errors[nameof(CreateChildTenantRequest.DisplayName)] = S["The name is required."];
        }
        else if (displayName.Trim().Length > MaxDisplayNameLength)
        {
            errors[nameof(CreateChildTenantRequest.DisplayName)] = S["The name can have at most {0} characters.", MaxDisplayNameLength];
        }
    }

    private void ValidateDescription(string description, Dictionary<string, string> errors)
    {
        if (description?.Trim().Length > MaxDescriptionLength)
        {
            errors[nameof(CreateChildTenantRequest.Description)] = S["The description can have at most {0} characters.", MaxDescriptionLength];
        }
    }

    private async Task ValidateSlugAsync(string slug, string currentEntryId, Dictionary<string, string> errors)
    {
        const string field = nameof(CreateChildTenantRequest.Slug);

        slug = slug?.Trim().ToLowerInvariant();

        var validation = TenantHierarchyNaming.ValidateSlug(slug, _options.ReservedSlugs);

        switch (validation)
        {
            case SlugValidationResult.Empty:
                errors[field] = S["The address is required."];
                return;
            case SlugValidationResult.TooShort:
                errors[field] = S["The address needs at least {0} characters.", TenantHierarchyNaming.MinSlugLength];
                return;
            case SlugValidationResult.TooLong:
                errors[field] = S["The address can have at most {0} characters.", TenantHierarchyNaming.MaxSlugLength];
                return;
            case SlugValidationResult.InvalidCharacters:
                errors[field] = S["Use only lowercase letters, digits and hyphens. The address cannot start or end with a hyphen."];
                return;
            case SlugValidationResult.Reserved:
                errors[field] = S["This address is reserved. Choose another one."];
                return;
        }

        var pattern = GetHostPattern();

        if (!TenantHierarchyNaming.IsValidHostPattern(pattern))
        {
            errors[field] = S["The address pattern of this tenant is not valid. Ask the platform administrator to check it."];

            return;
        }

        var existing = await _entryStore.FindBySlugAsync(slug);

        if (existing is not null && !string.Equals(existing.EntryId, currentEntryId, StringComparison.Ordinal))
        {
            errors[field] = S["This address is already in use."];

            return;
        }

        if (await _broker.IsHostInUseAsync(TenantHierarchyNaming.BuildHost(pattern, slug)))
        {
            errors[field] = S["This address is already in use."];
        }
    }

    private TenantHierarchyResult NotFound()
        => TenantHierarchyResult.Failure(S["The {0} was not found.", _labelsProvider.GetLabels().ChildLower]);

    private (string UserId, string UserName, string IpAddress) GetActor()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;

        return (
            user?.FindFirstValue(ClaimTypes.NameIdentifier),
            user?.Identity?.Name,
            httpContext?.Connection.RemoteIpAddress?.ToString());
    }
}
