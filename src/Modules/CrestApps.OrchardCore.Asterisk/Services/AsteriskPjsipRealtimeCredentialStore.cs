using System.Data.Common;
using CrestApps.OrchardCore.Asterisk.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskPjsipRealtimeCredentialStore : IAsteriskPjsipRealtimeCredentialStore
{
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly DefaultAsteriskOptions _defaultOptions;

    public AsteriskPjsipRealtimeCredentialStore(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<DefaultAsteriskOptions> defaultOptions)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _defaultOptions = defaultOptions.Value;
    }

    public async Task UpsertAsync(
        AsteriskPjsipRealtimeCredential credential,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);

        var settings = ResolveSettings();
        await using var connection = CreateConnection(settings);
        await connection.OpenAsync(cancellationToken);
        await DeleteAsync(connection, settings, credential.AuthorizationUser, cancellationToken);
        await ExecuteAsync(connection, $"INSERT INTO {Table(settings, "ps_auths")} (id, auth_type, username, password) VALUES (@id, @authType, @username, @password)", command =>
        {
            AddParameter(command, "id", credential.AuthorizationUser);
            AddParameter(command, "authType", "userpass");
            AddParameter(command, "username", credential.AuthorizationUser);
            AddParameter(command, "password", credential.Password);
        }, cancellationToken);
        await ExecuteAsync(connection, $"INSERT INTO {Table(settings, "ps_aors")} (id, max_contacts, remove_existing, default_expiration, minimum_expiration, maximum_expiration) VALUES (@id, @maxContacts, @removeExisting, @defaultExpiration, @minimumExpiration, @maximumExpiration)", command =>
        {
            var expiration = Math.Max(1, (int)credential.ContactExpiration.TotalSeconds);
            AddParameter(command, "id", credential.AuthorizationUser);
            AddParameter(command, "maxContacts", 1);
            AddParameter(command, "removeExisting", "yes");
            AddParameter(command, "defaultExpiration", expiration);
            AddParameter(command, "minimumExpiration", 1);
            AddParameter(command, "maximumExpiration", expiration);
        }, cancellationToken);
        await ExecuteAsync(connection, $"INSERT INTO {Table(settings, "ps_endpoints")} (id, transport, aors, auth, context, disallow, allow, webrtc, use_avpf, media_encryption, dtls_auto_generate_cert, ice_support, rtcp_mux, direct_media, force_rport, rewrite_contact, rtp_symmetric, callerid) VALUES (@id, @transport, @aors, @auth, @context, @disallow, @allow, @webrtc, @useAvpf, @mediaEncryption, @dtlsAutoGenerateCert, @iceSupport, @rtcpMux, @directMedia, @forceRport, @rewriteContact, @rtpSymmetric, @callerId)", command =>
        {
            AddParameter(command, "id", credential.AuthorizationUser);
            AddParameter(command, "transport", "transport-wss");
            AddParameter(command, "aors", credential.AuthorizationUser);
            AddParameter(command, "auth", credential.AuthorizationUser);
            AddParameter(command, "context", "crestapps-agents");
            AddParameter(command, "disallow", "all");
            AddParameter(command, "allow", string.Join(',', credential.Codecs));
            AddParameter(command, "webrtc", "yes");
            AddParameter(command, "useAvpf", "yes");
            AddParameter(command, "mediaEncryption", "dtls");
            AddParameter(command, "dtlsAutoGenerateCert", "yes");
            AddParameter(command, "iceSupport", "yes");
            AddParameter(command, "rtcpMux", "yes");
            AddParameter(command, "directMedia", "no");
            AddParameter(command, "forceRport", "yes");
            AddParameter(command, "rewriteContact", "yes");
            AddParameter(command, "rtpSymmetric", "yes");
            AddParameter(command, "callerId", $"{credential.DisplayName} <{credential.AuthorizationUser}>");
        }, cancellationToken);
    }

    public async Task DeleteAsync(
        string authorizationUser,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authorizationUser))
        {
            return;
        }

        var settings = ResolveSettings();
        await using var connection = CreateConnection(settings);
        await connection.OpenAsync(cancellationToken);
        await DeleteAsync(connection, settings, authorizationUser.Trim(), cancellationToken);
    }

    private static async Task DeleteAsync(
        DbConnection connection,
        AsteriskResolvedSettings settings,
        string authorizationUser,
        CancellationToken cancellationToken)
    {
        foreach (var table in new[] { "ps_endpoints", "ps_aors", "ps_auths" })
        {
            await ExecuteAsync(connection, $"DELETE FROM {Table(settings, table)} WHERE id = @id", command =>
            {
                AddParameter(command, "id", authorizationUser);
            }, cancellationToken);
        }
    }

    private AsteriskResolvedSettings ResolveSettings()
    {
        var tenantSettings = _siteService.GetSettings<AsteriskSettings>();

        if (!string.IsNullOrWhiteSpace(tenantSettings.PjsipRealtimeConnectionString))
        {
            return new AsteriskResolvedSettings
            {
                PjsipRealtimeProviderInvariantName = tenantSettings.PjsipRealtimeProviderInvariantName,
                PjsipRealtimeConnectionString = Unprotect(tenantSettings.PjsipRealtimeConnectionString),
                PjsipRealtimeTablePrefix = tenantSettings.PjsipRealtimeTablePrefix,
            };
        }

        return new AsteriskResolvedSettings
        {
            PjsipRealtimeProviderInvariantName = _defaultOptions.PjsipRealtimeProviderInvariantName,
            PjsipRealtimeConnectionString = _defaultOptions.PjsipRealtimeConnectionString,
            PjsipRealtimeTablePrefix = _defaultOptions.PjsipRealtimeTablePrefix,
        };
    }

    private static DbConnection CreateConnection(AsteriskResolvedSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.PjsipRealtimeProviderInvariantName) || string.IsNullOrWhiteSpace(settings.PjsipRealtimeConnectionString))
        {
            throw new InvalidOperationException("PJSIP Realtime storage is not configured.");
        }

        var factory = DbProviderFactories.GetFactory(settings.PjsipRealtimeProviderInvariantName);
        var connection = factory.CreateConnection() ?? throw new InvalidOperationException("The configured PJSIP Realtime provider did not create a connection.");
        connection.ConnectionString = settings.PjsipRealtimeConnectionString;

        return connection;
    }

    private static async Task ExecuteAsync(
        DbConnection connection,
        string sql,
        Action<DbCommand> bind,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        bind(command);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@" + name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string Table(
        AsteriskResolvedSettings settings,
        string tableName)
    {
        var prefix = AsteriskPjsipRealtimeTablePrefixValidator.EnsureValid(settings.PjsipRealtimeTablePrefix);

        return prefix.Length == 0
            ? tableName
            : prefix + tableName;
    }

    private string Unprotect(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return null;
        }

        try
        {
            return _dataProtectionProvider.CreateProtector(AsteriskConstants.ProtectorName).Unprotect(protectedValue);
        }
        catch
        {
            return protectedValue;
        }
    }
}
