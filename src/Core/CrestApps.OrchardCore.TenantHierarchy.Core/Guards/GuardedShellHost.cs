using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Builders;
using OrchardCore.Environment.Shell.Descriptor.Models;
using OrchardCore.Environment.Shell.Events;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Guards;

/// <summary>
/// Decorates the host's <see cref="IShellHost"/> so that code running in a parent, child or ordinary tenant can only
/// reach the tenants <see cref="TenantHierarchyAccessRules"/> allows. Calls that change a tenant or open its scope
/// throw; calls that read settings or shell contexts hide the tenants that are out of reach.
/// </summary>
/// <remarks>
/// Every tenant container resolves the one host instance, so the guard covers every installed feature, whether it
/// reaches another tenant by design or by mistake. It does not stop malicious server code, which can get around any
/// in-process check.
/// </remarks>
public sealed class GuardedShellHost : IShellHost, IDisposable, IAsyncDisposable
{
    private readonly IShellHost _inner;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GuardedShellHost"/> class.
    /// </summary>
    /// <param name="inner">The host that is decorated.</param>
    /// <param name="logger">The logger.</param>
    public GuardedShellHost(
        IShellHost inner,
        ILogger<GuardedShellHost> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    /// <summary>
    /// Gets the decorated host. Only host-level tenant hierarchy code uses it, to read every tenant without filtering.
    /// </summary>
    internal IShellHost Inner => _inner;

    /// <inheritdoc/>
    public ShellsEvent LoadingAsync
    {
        get => _inner.LoadingAsync;
        set => _inner.LoadingAsync = value;
    }

    /// <inheritdoc/>
    public ShellEvent ReleasingAsync
    {
        get => _inner.ReleasingAsync;
        set => _inner.ReleasingAsync = value;
    }

    /// <inheritdoc/>
    public ShellEvent ReloadingAsync
    {
        get => _inner.ReloadingAsync;
        set => _inner.ReloadingAsync = value;
    }

    /// <inheritdoc/>
    public ShellEvent RemovingAsync
    {
        get => _inner.RemovingAsync;
        set => _inner.RemovingAsync = value;
    }

    /// <inheritdoc/>
    public Task InitializeAsync()
        => _inner.InitializeAsync();

    /// <inheritdoc/>
    public Task<ShellContext> GetOrCreateShellContextAsync(ShellSettings settings)
    {
        EnsureCanReach(settings, nameof(GetOrCreateShellContextAsync));

        return _inner.GetOrCreateShellContextAsync(settings);
    }

    /// <inheritdoc/>
    public Task<ShellScope> GetScopeAsync(ShellSettings settings)
    {
        EnsureCanReach(settings, nameof(GetScopeAsync));

        return _inner.GetScopeAsync(settings);
    }

    /// <inheritdoc/>
    public Task UpdateShellSettingsAsync(ShellSettings settings)
    {
        EnsureCanReach(settings, nameof(UpdateShellSettingsAsync));

        return _inner.UpdateShellSettingsAsync(settings);
    }

    /// <inheritdoc/>
    public Task ReloadShellContextAsync(ShellSettings settings, bool eventSource = true)
    {
        EnsureCanReach(settings, nameof(ReloadShellContextAsync));

        return _inner.ReloadShellContextAsync(settings, eventSource);
    }

    /// <inheritdoc/>
    public Task ReleaseShellContextAsync(ShellSettings settings, bool eventSource = true)
    {
        EnsureCanReach(settings, nameof(ReleaseShellContextAsync));

        return _inner.ReleaseShellContextAsync(settings, eventSource);
    }

    /// <inheritdoc/>
    public IEnumerable<ShellContext> ListShellContexts()
    {
        var current = GetCurrentSettings();
        var brokerTarget = BrokerCallContext.TargetTenantName;

        return _inner.ListShellContexts()
            .Where(context => TenantHierarchyAccessRules.IsListed(current, Resolve(context.Settings), brokerTarget));
    }

    /// <inheritdoc/>
    public bool TryGetShellContext(string name, out ShellContext shellContext)
    {
        if (!_inner.TryGetShellContext(name, out shellContext))
        {
            return false;
        }

        if (CanReach(Resolve(shellContext.Settings), isRead: true))
        {
            return true;
        }

        shellContext = null;

        return false;
    }

    /// <inheritdoc/>
    public bool TryGetSettings(string name, out ShellSettings settings)
    {
        if (!_inner.TryGetSettings(name, out settings))
        {
            return false;
        }

        if (CanReach(settings, isRead: true))
        {
            return true;
        }

        settings = null;

        return false;
    }

    /// <inheritdoc/>
    public IEnumerable<ShellSettings> GetAllSettings()
    {
        var current = GetCurrentSettings();
        var brokerTarget = BrokerCallContext.TargetTenantName;

        return _inner.GetAllSettings()
            .Where(settings => TenantHierarchyAccessRules.IsListed(current, settings, brokerTarget));
    }

    /// <inheritdoc/>
    public Task RemoveShellSettingsAsync(ShellSettings settings)
    {
        EnsureCanReach(settings, nameof(RemoveShellSettingsAsync));

        return _inner.RemoveShellSettingsAsync(settings);
    }

    /// <inheritdoc/>
    public Task RemoveShellContextAsync(ShellSettings settings, bool eventSource = true)
    {
        EnsureCanReach(settings, nameof(RemoveShellContextAsync));

        return _inner.RemoveShellContextAsync(settings, eventSource);
    }

    /// <inheritdoc/>
    public Task ChangedAsync(ShellDescriptor descriptor, ShellSettings settings)
        => _inner.ChangedAsync(descriptor, settings);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_inner is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (_inner is IAsyncDisposable disposable)
        {
            return disposable.DisposeAsync();
        }

        Dispose();

        return ValueTask.CompletedTask;
    }

    private static ShellSettings GetCurrentSettings()
        => ShellScope.Context?.Settings;

    private static bool CanReach(ShellSettings target, bool isRead)
        => TenantHierarchyAccessRules.CanReach(GetCurrentSettings(), target, BrokerCallContext.TargetTenantName, isRead);

    private ShellSettings Resolve(ShellSettings settings)
    {
        // The registered settings decide, so a caller cannot pass a crafted settings object that claims to be a child
        // of its own while it names another tenant. A tenant that is not registered yet is judged by what it claims.
        if (settings?.Name != null && _inner.TryGetSettings(settings.Name, out var registered))
        {
            return registered;
        }

        return settings;
    }

    private void EnsureCanReach(ShellSettings settings, string operation)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var current = GetCurrentSettings();
        var target = Resolve(settings);

        if (TenantHierarchyAccessRules.CanReach(current, target, BrokerCallContext.TargetTenantName, isRead: false))
        {
            return;
        }

        _logger.LogWarning(
            "The tenant hierarchy refused '{Operation}' from tenant '{CurrentTenant}' ({CurrentTenantId}) to tenant '{TargetTenant}' ({TargetTenantId}).",
            operation,
            current?.Name,
            current?.TenantId,
            target?.Name,
            target?.TenantId);

        throw new TenantHierarchyAccessDeniedException();
    }
}
