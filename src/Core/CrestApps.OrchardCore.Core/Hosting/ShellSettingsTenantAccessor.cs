using CrestApps.Core.Hosting;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Core.Hosting;

/// <summary>
/// Reports the Orchard Core shell's tenant name to framework services that key work by tenant.
/// </summary>
public sealed class ShellSettingsTenantAccessor : ITenantAccessor
{
    private readonly ShellSettings _shellSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellSettingsTenantAccessor"/> class.
    /// </summary>
    /// <param name="shellSettings">The shell settings of the current tenant.</param>
    public ShellSettingsTenantAccessor(ShellSettings shellSettings)
    {
        _shellSettings = shellSettings;
    }

    /// <inheritdoc/>
    public string TenantName => _shellSettings.Name;
}
