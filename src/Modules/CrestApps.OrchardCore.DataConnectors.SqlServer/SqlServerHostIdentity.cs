using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.DataConnectors.SqlServer;

/// <summary>
/// Host settings for SQL Server connections, bound from the <c>CrestApps:DataConnectors:SqlServer</c> configuration
/// section, which only the host's operator controls.
/// </summary>
public sealed class SqlServerHostIdentityOptions
{
    /// <summary>
    /// The configuration section the options are bound from.
    /// </summary>
    public const string SectionName = "CrestApps:DataConnectors:SqlServer";

    /// <summary>
    /// Gets or sets a value indicating whether tenants other than the default one may sign in as the host (its Windows
    /// account, managed identity or default Entra credential). Off by default.
    /// </summary>
    public bool AllowHostIdentity { get; set; }
}

/// <summary>
/// Which SQL Server sign-in modes act as the host process, and where they may be used.
/// </summary>
public static class SqlServerHostIdentity
{
    /// <summary>
    /// Whether the mode signs in as the host process rather than with credentials the tenant entered.
    /// </summary>
    /// <param name="authentication">The sign-in mode.</param>
    public static bool IsHostIdentity(SqlServerAuthenticationType authentication)
        => authentication is SqlServerAuthenticationType.Integrated
            or SqlServerAuthenticationType.EntraManagedIdentity
            or SqlServerAuthenticationType.EntraDefault;

    /// <summary>
    /// Whether the host-identity modes may be used on this tenant: the default tenant, or any tenant when the host
    /// allows it.
    /// </summary>
    /// <param name="shellSettings">The settings of the current tenant.</param>
    /// <param name="options">The host's options.</param>
    public static bool IsAllowed(ShellSettings shellSettings, SqlServerHostIdentityOptions options)
        => shellSettings?.IsDefaultShell() == true || options?.AllowHostIdentity == true;
}
