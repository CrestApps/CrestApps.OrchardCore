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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.AI.FileTransfer.Core;

/// <summary>
/// Puts the <see cref="FileTransferEgressPolicy"/> on CrestApps.Core's FTP and SFTP connectors and MCP resource types,
/// which build their clients themselves: the server and proxy are checked when a file source is saved through the
/// catalog, and before every connection (review 11). The package still resolves the name again when it connects; an
/// SFTP server is then held to its host key, and an FTP server to its TLS certificate, so a name that answers
/// differently the second time reaches no service, though the connection attempt itself is made.
/// </summary>
public static class FileTransferEgress
{
    /// <summary>
    /// The message shown when a server or proxy is on the site's own network.
    /// </summary>
    public const string RefusedMessage = "This server is on the site's own network, or its name could not be found. Use a public server, or ask the host to allow a private network for file transfers.";

    /// <summary>
    /// Guards the FTP ingestion connector. Call it after <c>AddCoreFtpIngestionConnector</c>.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddFtpIngestionEgressGuard(this IServiceCollection services)
        => services.AddFileTransferHostOptions()
            .DecorateKeyed<IIngestionConnector>(FtpIngestionConnector.ConnectorName, (serviceProvider, inner) => inner is EgressGuardedIngestionConnector
                ? inner
                : new EgressGuardedIngestionConnector(inner, serviceProvider.GetRequiredService<FileTransferEgressPolicy>(), source => FtpHosts(source.GetOrCreate<FtpConnectionMetadata>())));

    /// <summary>
    /// Guards the SFTP ingestion connector. Call it after <c>AddCoreSftpIngestionConnector</c>.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddSftpIngestionEgressGuard(this IServiceCollection services)
        => services.AddFileTransferHostOptions()
            .DecorateKeyed<IIngestionConnector>(SftpIngestionConnector.ConnectorName, (serviceProvider, inner) => inner is EgressGuardedIngestionConnector
                ? inner
                : new EgressGuardedIngestionConnector(inner, serviceProvider.GetRequiredService<FileTransferEgressPolicy>(), source => SftpHosts(source.GetOrCreate<SftpConnectionMetadata>())));

    /// <summary>
    /// Guards the FTP MCP resource type. Call it after <c>AddCoreAIFtpMcpResources</c>.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddFtpResourceEgressGuard(this IServiceCollection services)
        => services.AddFileTransferResourceValidation()
            .DecorateKeyed<IMcpResourceTypeHandler>(FtpResourceConstants.Type, (serviceProvider, inner) => inner is EgressGuardedMcpResourceTypeHandler
                ? inner
                : new EgressGuardedMcpResourceTypeHandler(inner, serviceProvider.GetRequiredService<FileTransferEgressPolicy>(), resource => FtpHosts(resource.TryGet<FtpConnectionMetadata>(out var metadata) ? metadata : null)));

    /// <summary>
    /// Guards the SFTP MCP resource type. Call it after <c>AddCoreAISftpMcpResources</c>.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddSftpResourceEgressGuard(this IServiceCollection services)
        => services.AddFileTransferResourceValidation()
            .DecorateKeyed<IMcpResourceTypeHandler>(SftpResourceConstants.Type, (serviceProvider, inner) => inner is EgressGuardedMcpResourceTypeHandler
                ? inner
                : new EgressGuardedMcpResourceTypeHandler(inner, serviceProvider.GetRequiredService<FileTransferEgressPolicy>(), resource => SftpHosts(resource.TryGet<SftpConnectionMetadata>(out var metadata) ? metadata : null)));

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
