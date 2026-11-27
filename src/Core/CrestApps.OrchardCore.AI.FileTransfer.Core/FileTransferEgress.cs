using CrestApps.Core;
using CrestApps.Core.AI.Ftp;
using CrestApps.Core.AI.Ftp.Models;
using CrestApps.Core.AI.Indexing;
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Sftp;
using CrestApps.Core.AI.Sftp.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.FileTransfer.Core.Services;
using FluentFTP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.AI.FileTransfer.Core;

/// <summary>
/// Puts the <see cref="FileTransferEgressPolicy"/> on CrestApps.Core's FTP and SFTP connectors and MCP resource types,
/// which build their clients themselves: the server and proxy are checked when a file source is saved through the
/// catalog, and before every connection (review 11). The package then connects to the address that was checked (review 3):
/// an SFTP server is verified by its host key, and a plain FTP server, or one whose certificate is not checked, has no
/// name to prove. Only an FTP server held to its TLS certificate keeps its name, which the package resolves again; a name
/// that answers differently the second time reaches no service, though the connection attempt itself is made.
/// </summary>
public static class FileTransferEgress
{
    /// <summary>
    /// The message shown when a server or proxy is on the site's own network.
    /// </summary>
    public const string RefusedMessage = "This server is on the site's own network, or its name could not be found. Use a public server, or ask the host to allow a private network for file transfers.";

    /// <summary>
    /// The message shown when an FTP data connection type could send the data channel to the site's own network.
    /// </summary>
    public const string DataConnectionRefusedMessage = "This data connection type lets the server send the connection to the site's own network. Choose EPSV or PASVEX.";

    /// <summary>
    /// Guards the FTP ingestion connector. Call it after <c>AddCoreFtpIngestionConnector</c>.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddFtpIngestionEgressGuard(this IServiceCollection services)
        => services.AddFileTransferHostOptions()
            .DecorateKeyed<IIngestionConnector>(FtpIngestionConnector.ConnectorName, (serviceProvider, inner) => inner is EgressGuardedIngestionConnector
                ? inner
                : new EgressGuardedIngestionConnector(
                    inner,
                    serviceProvider.GetRequiredService<FileTransferEgressPolicy>(),
                    source => FtpHosts(source.GetOrCreate<FtpConnectionMetadata>()),
                    (source, policy) => FtpDataConnectionRefusal(source.GetOrCreate<FtpConnectionMetadata>(), policy),
                    (source, policy) => source.Put(PrepareFtpConnection(source.GetOrCreate<FtpConnectionMetadata>(), policy))));

    /// <summary>
    /// Guards the SFTP ingestion connector. Call it after <c>AddCoreSftpIngestionConnector</c>.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddSftpIngestionEgressGuard(this IServiceCollection services)
        => services.AddFileTransferHostOptions()
            .DecorateKeyed<IIngestionConnector>(SftpIngestionConnector.ConnectorName, (serviceProvider, inner) => inner is EgressGuardedIngestionConnector
                ? inner
                : new EgressGuardedIngestionConnector(
                    inner,
                    serviceProvider.GetRequiredService<FileTransferEgressPolicy>(),
                    source => SftpHosts(source.GetOrCreate<SftpConnectionMetadata>()),
                    prepare: (source, policy) => source.Put(PrepareSftpConnection(source.GetOrCreate<SftpConnectionMetadata>(), policy))));

    /// <summary>
    /// Guards the FTP MCP resource type. Call it after <c>AddCoreAIFtpMcpResources</c>.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddFtpResourceEgressGuard(this IServiceCollection services)
        => services.AddFileTransferResourceValidation()
            .DecorateKeyed<IMcpResourceTypeHandler>(FtpResourceConstants.Type, (serviceProvider, inner) => inner is EgressGuardedMcpResourceTypeHandler
                ? inner
                : new EgressGuardedMcpResourceTypeHandler(
                    inner,
                    serviceProvider.GetRequiredService<FileTransferEgressPolicy>(),
                    resource => FtpHosts(resource.TryGet<FtpConnectionMetadata>(out var metadata) ? metadata : null),
                    (resource, policy) =>
                    {
                        if (resource.TryGet<FtpConnectionMetadata>(out var metadata))
                        {
                            resource.Put(PrepareFtpConnection(metadata, policy));
                        }
                    }));

    /// <summary>
    /// Guards the SFTP MCP resource type. Call it after <c>AddCoreAISftpMcpResources</c>.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddSftpResourceEgressGuard(this IServiceCollection services)
        => services.AddFileTransferResourceValidation()
            .DecorateKeyed<IMcpResourceTypeHandler>(SftpResourceConstants.Type, (serviceProvider, inner) => inner is EgressGuardedMcpResourceTypeHandler
                ? inner
                : new EgressGuardedMcpResourceTypeHandler(
                    inner,
                    serviceProvider.GetRequiredService<FileTransferEgressPolicy>(),
                    resource => SftpHosts(resource.TryGet<SftpConnectionMetadata>(out var metadata) ? metadata : null),
                    (resource, policy) =>
                    {
                        if (resource.TryGet<SftpConnectionMetadata>(out var metadata))
                        {
                            resource.Put(PrepareSftpConnection(metadata, policy));
                        }
                    }));

    /// <summary>
    /// Holds an FTP connection to the policy just before it connects: the server is checked, the data connection type is
    /// held to a safe one (<see cref="HoldFtpDataConnection"/>), and the server is named by the address that was checked,
    /// so the package connects there rather than to whatever the name resolves to a moment later (review 3). Only a TLS
    /// connection that validates the server's certificate keeps the name, which the certificate must match: the client
    /// takes no separate name for the certificate, so an address would fail every valid certificate. The package still
    /// resolves that name again, so the connection attempt is made, but a server that cannot prove the name gets neither
    /// the credentials nor a request. Pass a copy: the connection's host is changed.
    /// </summary>
    /// <param name="metadata">A copy of the connection.</param>
    /// <param name="policy">The host's file transfer policy.</param>
    /// <returns>The connection.</returns>
    /// <exception cref="InvalidOperationException">The server is on the site's own network, or could not be found.</exception>
    public static FtpConnectionMetadata PrepareFtpConnection(FtpConnectionMetadata metadata, FileTransferEgressPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(policy);

        var address = policy.ResolveAllowed(metadata.Host);

        HoldFtpDataConnection(metadata, policy);

        if (address is not null && !ChecksFtpCertificateName(metadata))
        {
            metadata.Host = address.ToString();
        }

        return metadata;
    }

    /// <summary>
    /// Holds an SFTP connection to the policy just before it connects: the server and the proxy are checked, and named by
    /// the addresses that were checked. The server is verified by its host key, not by its name, so nothing is lost
    /// (review 3). Pass a copy: the connection's hosts are changed.
    /// </summary>
    /// <param name="metadata">A copy of the connection.</param>
    /// <param name="policy">The host's file transfer policy.</param>
    /// <returns>The connection.</returns>
    /// <exception cref="InvalidOperationException">The server or the proxy is on the site's own network, or could not be found.</exception>
    public static SftpConnectionMetadata PrepareSftpConnection(SftpConnectionMetadata metadata, FileTransferEgressPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(policy);

        if (policy.ResolveAllowed(metadata.Host) is { } address)
        {
            metadata.Host = address.ToString();
        }

        if (policy.ResolveAllowed(metadata.ProxyHost) is { } proxyAddress)
        {
            metadata.ProxyHost = proxyAddress.ToString();
        }

        return metadata;
    }

    // Whether the FTP client checks the server's certificate against the host name: TLS (Explicit or Implicit, not Auto,
    // which falls back to plain FTP, as the package's own policy reads it) without accepting any certificate.
    internal static bool ChecksFtpCertificateName(FtpConnectionMetadata metadata)
        => !metadata.ValidateAnyCertificate &&
            Enum.TryParse<FtpEncryptionMode>(metadata.EncryptionMode, ignoreCase: true, out var mode) &&
            mode is FtpEncryptionMode.Explicit or FtpEncryptionMode.Implicit;

    /// <summary>
    /// The field of an FTP connection whose data connection type could send the data channel to the site's own network,
    /// or <see langword="null"/>. An empty type is allowed: it is held to PASVEX before connecting.
    /// </summary>
    /// <param name="metadata">The connection.</param>
    /// <param name="policy">The host's file transfer policy.</param>
    public static string FtpDataConnectionRefusal(FtpConnectionMetadata metadata, FileTransferEgressPolicy policy)
        => !string.IsNullOrWhiteSpace(metadata?.DataConnectionType) && !policy.AllowPrivateNetwork && !FtpDataConnections.IsSafe(metadata.DataConnectionType)
            ? nameof(FtpConnectionMetadata.DataConnectionType)
            : null;

    /// <summary>
    /// The connection held to a safe data connection type before it connects (<see cref="FtpDataConnections"/>), or
    /// <see langword="null"/> when its own is safe or the host allows the site's own network.
    /// </summary>
    /// <param name="metadata">The connection.</param>
    /// <param name="policy">The host's file transfer policy.</param>
    public static FtpConnectionMetadata HoldFtpDataConnection(FtpConnectionMetadata metadata, FileTransferEgressPolicy policy)
    {
        if (metadata is null || policy.AllowPrivateNetwork || FtpDataConnections.IsSafe(metadata.DataConnectionType))
        {
            return null;
        }

        metadata.DataConnectionType = FtpDataConnections.Safe;

        return metadata;
    }

    /// <summary>
    /// The servers an FTP connection reaches.
    /// </summary>
    /// <param name="metadata">The connection.</param>
    public static IEnumerable<(string Field, string Host)> FtpHosts(FtpConnectionMetadata metadata)
    {
        if (!string.IsNullOrWhiteSpace(metadata?.Host))
        {
            yield return (nameof(FtpConnectionMetadata.Host), metadata.Host);
        }
    }

    /// <summary>
    /// The servers an SFTP connection reaches: the server, and the proxy when one is set.
    /// </summary>
    /// <param name="metadata">The connection.</param>
    public static IEnumerable<(string Field, string Host)> SftpHosts(SftpConnectionMetadata metadata)
    {
        if (!string.IsNullOrWhiteSpace(metadata?.Host))
        {
            yield return (nameof(SftpConnectionMetadata.Host), metadata.Host);
        }

        if (!string.IsNullOrWhiteSpace(metadata?.ProxyHost))
        {
            yield return (nameof(SftpConnectionMetadata.ProxyHost), metadata.ProxyHost);
        }
    }

    private static IServiceCollection AddFileTransferResourceValidation(this IServiceCollection services)
    {
        services.AddFileTransferHostOptions();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<McpResource>, FileTransferResourceEgressHandler>());

        return services;
    }

    // Replaces a keyed registration with one that wraps what it resolved to.
    private static IServiceCollection DecorateKeyed<TService>(this IServiceCollection services, string key, Func<IServiceProvider, TService, TService> decorate)
        where TService : class
    {
        var descriptor = services.LastOrDefault(candidate => candidate.IsKeyedService && candidate.ServiceType == typeof(TService) && Equals(candidate.ServiceKey, key))
            ?? throw new InvalidOperationException($"No {typeof(TService).Name} is registered for '{key}'. Register the package's services first.");

        services.Remove(descriptor);
        services.Add(new ServiceDescriptor(typeof(TService), key, (serviceProvider, serviceKey) => decorate(serviceProvider, (TService)Resolve(descriptor, serviceProvider, serviceKey)), descriptor.Lifetime));

        return services;
    }

    private static object Resolve(ServiceDescriptor descriptor, IServiceProvider serviceProvider, object serviceKey)
        => descriptor.KeyedImplementationFactory?.Invoke(serviceProvider, serviceKey)
            ?? descriptor.KeyedImplementationInstance
            ?? ActivatorUtilities.CreateInstance(serviceProvider, descriptor.KeyedImplementationType);
}
