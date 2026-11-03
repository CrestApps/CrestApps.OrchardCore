using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.DataConnectors.SqlServer.ViewModels;

/// <summary>
/// The settings of a SQL Server connection.
/// </summary>
public class SqlServerConnectionViewModel
{
    /// <summary>
    /// Gets or sets the server.
    /// </summary>
    public string Server { get; set; }

    /// <summary>
    /// Gets or sets the database.
    /// </summary>
    public string Database { get; set; }

    /// <summary>
    /// Gets or sets how the connection signs in.
    /// </summary>
    public SqlServerAuthenticationType Authentication { get; set; }

    /// <summary>
    /// Gets or sets the login or client identifier.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets a new password or client secret; empty keeps the stored one.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the traffic is encrypted.
    /// </summary>
    public bool Encrypt { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the server's certificate is trusted without being validated.
    /// </summary>
    public bool TrustServerCertificate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a read-only replica is asked for.
    /// </summary>
    public bool ReadOnlyIntent { get; set; }

    /// <summary>
    /// Gets or sets the seconds to wait for the connection to open.
    /// </summary>
    public int ConnectTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Gets or sets a value indicating whether a password is stored.
    /// </summary>
    [BindNever]
    public bool HasPassword { get; set; }
}
