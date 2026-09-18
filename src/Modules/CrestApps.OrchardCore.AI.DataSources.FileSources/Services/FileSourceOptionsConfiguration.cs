using CrestApps.Core.AI.FileSources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.AI.DataSources.FileSources.Services;

/// <summary>
/// Configures <see cref="FileSourceOptions"/> from the tenant's shell configuration, and pins the folders a
/// local file source may read to the one folder this tenant owns.
/// </summary>
/// <remarks>
/// <para>
/// Configuration supplies the effort knobs -- how much one run may take on, how many items it fetches at a
/// time, how often the schedule is evaluated. It does not supply the allowed roots. That list is a
/// host-wide allow-list in the framework, which is the wrong shape for a multi-tenant host: a tenant
/// administrator who can edit their own tenant's configuration would be able to widen their own reach.
/// </para>
/// <para>
/// The list is cleared before the tenant's folder is added, because binding leaves whatever configuration
/// named in place, and a single surviving entry is the whole boundary gone.
/// </para>
/// <para>
/// In code the section is <c>CrestApps:AI:FileSources</c>, because shell configuration is already rooted at
/// the tenant's <c>OrchardCore</c> section. The same setting is written at
/// <c>OrchardCore:CrestApps:AI:FileSources</c> in <c>appsettings.json</c>.
/// </para>
/// </remarks>
internal sealed class FileSourceOptionsConfiguration : IConfigureOptions<FileSourceOptions>
{
    /// <summary>
    /// The shell configuration section the options are read from.
    /// </summary>
    public const string ConfigurationSectionName = "CrestApps:AI:FileSources";

    private readonly IShellConfiguration _shellConfiguration;
    private readonly ITenantFileSourceRoot _tenantRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSourceOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The tenant's shell configuration.</param>
    /// <param name="tenantRoot">This tenant's file-source folder.</param>
    public FileSourceOptionsConfiguration(
        IShellConfiguration shellConfiguration,
        ITenantFileSourceRoot tenantRoot)
    {
        _shellConfiguration = shellConfiguration;
        _tenantRoot = tenantRoot;
    }

    /// <summary>
    /// Configures the <see cref="FileSourceOptions"/>.
    /// </summary>
    /// <param name="options">The options instance to configure.</param>
    public void Configure(FileSourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _shellConfiguration.GetSection(ConfigurationSectionName).Bind(options);

        // Whatever configuration said about roots is discarded. The boundary is the tenant's own folder and
        // nothing else.
        options.AllowedLocalRoots.Clear();
        options.AllowedLocalRoots.Add(_tenantRoot.GetRoot());
    }
}
