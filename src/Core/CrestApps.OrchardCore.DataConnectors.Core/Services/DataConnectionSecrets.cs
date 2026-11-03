using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.DataConnectors.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.DataConnectors.Services;

/// <summary>
/// Protects the secrets of data connections with the tenant's data protection keys, and reads them back for a
/// connector.
/// </summary>
public interface IDataConnectionSecretManager
{
    /// <summary>
    /// Stores a secret on a connection, protected. An empty value leaves the stored secret as it was.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="name">The secret's name.</param>
    /// <param name="value">The secret in clear text.</param>
    void Set(DataConnection connection, string name, string value);

    /// <summary>
    /// Removes a secret from a connection.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="name">The secret's name.</param>
    void Remove(DataConnection connection, string name);

    /// <summary>
    /// Returns whether a connection stores a secret.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="name">The secret's name.</param>
    /// <returns><see langword="true"/> when the secret is stored.</returns>
    bool Has(DataConnection connection, string name);

    /// <summary>
    /// Returns the reader a connector uses to read the connection's secrets.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <returns>The reader.</returns>
    IDataConnectorSecrets CreateReader(DataConnection connection);
}

internal sealed class DataConnectionSecretManager : IDataConnectionSecretManager
{
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    public DataConnectionSecretManager(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<DataConnectionSecretManager> logger)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    public void Set(DataConnection connection, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        connection.Secrets ??= new(StringComparer.OrdinalIgnoreCase);
        connection.Secrets[name] = _dataProtectionProvider.ProtectSetting(DataConnectorConstants.SecretsProtectionPurpose, value);
    }

    public void Remove(DataConnection connection, string name)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrEmpty(name);

        connection.Secrets?.Remove(name);
    }

    public bool Has(DataConnection connection, string name)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return !string.IsNullOrEmpty(name) && connection.Secrets?.TryGetValue(name, out var value) == true && !string.IsNullOrEmpty(value);
    }

    public IDataConnectorSecrets CreateReader(DataConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return new Reader(this, connection);
    }

    private string Read(DataConnection connection, string name)
    {
        if (connection.Secrets is null || !connection.Secrets.TryGetValue(name, out var protectedValue))
        {
            return null;
        }

        return _dataProtectionProvider.UnprotectSettingOrDefault(
            DataConnectorConstants.SecretsProtectionPurpose,
            protectedValue,
            _logger,
            // The warning names the connection, not the secret: a secret's name, such as Password, has no place in a log.
            $"secret of the data connection '{connection.Name}'");
    }

    private sealed class Reader : IDataConnectorSecrets
    {
        private readonly DataConnectionSecretManager _manager;
        private readonly DataConnection _connection;

        public Reader(DataConnectionSecretManager manager, DataConnection connection)
        {
            _manager = manager;
            _connection = connection;
        }

        public string Get(string name)
            => string.IsNullOrEmpty(name) ? null : _manager.Read(_connection, name);
    }
}
