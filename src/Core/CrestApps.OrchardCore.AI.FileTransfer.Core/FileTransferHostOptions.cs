using CrestApps.Core.AI.Ftp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.AI.FileTransfer.Core;

/// <summary>
/// What the host's operator allows the remote file connections of a tenant to do, read from
/// <c>CrestApps_AI:FileTransfer</c> in the tenant's configuration (not from its settings, which its administrators edit).
/// </summary>
public sealed class FileTransferHostOptions
{
    /// <summary>
    /// The configuration section.
    /// </summary>
    public const string SectionName = "CrestApps_AI:FileTransfer";

    /// <summary>
    /// Gets or sets a value indicating whether an FTP connection may accept any server certificate. Off by default:
    /// accepting any certificate lets anyone on the network path read the credentials and the files.
    /// </summary>
    public bool AllowAnyFtpCertificate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an FTP connection may send a user name and password without TLS (an
    /// encryption mode other than Explicit or Implicit). Off by default: anyone on the network path reads them.
    /// </summary>
    public bool AllowUnencryptedFtp { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an FTP or SFTP connection, or an SFTP proxy, may be on the site's own
    /// network (a private, loopback or shared address, or a name that resolves to one), for a host whose file servers
    /// are internal on purpose. Off by default: a tenant's administrator could otherwise probe and read the host's
    /// internal services. Link-local addresses and the cloud metadata services are refused even when on.
    /// </summary>
    public bool AllowPrivateNetwork { get; set; }
}

/// <summary>
/// Registers <see cref="FileTransferHostOptions"/>.
/// </summary>
public static class FileTransferHostOptionsServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="FileTransferHostOptions"/> to the tenant's configuration, and CrestApps.Core's
    /// <see cref="FtpOptions"/>, which its FTP client checks each connection against, to the same settings, and registers
    /// the <see cref="FileTransferEgressPolicy"/> that reads them. Safe to call more than once.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddFileTransferHostOptions(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<FileTransferHostOptions>, FileTransferHostOptionsConfiguration>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<FtpOptions>, FtpOptionsConfiguration>());
        services.TryAddSingleton<FileTransferEgressPolicy>();

        return services;
    }

    private sealed class FileTransferHostOptionsConfiguration : IConfigureOptions<FileTransferHostOptions>
    {
        private readonly IShellConfiguration _shellConfiguration;

        public FileTransferHostOptionsConfiguration(IShellConfiguration shellConfiguration)
        {
            _shellConfiguration = shellConfiguration;
        }

        public void Configure(FileTransferHostOptions options)
            => _shellConfiguration.GetSection(FileTransferHostOptions.SectionName).Bind(options);
    }

    // The editor refuses what the host doesn't allow, and the package refuses it again when it connects, so both read the
    // same settings.
    private sealed class FtpOptionsConfiguration : IConfigureOptions<FtpOptions>
    {
        private readonly IOptions<FileTransferHostOptions> _hostOptions;

        public FtpOptionsConfiguration(IOptions<FileTransferHostOptions> hostOptions)
        {
            _hostOptions = hostOptions;
        }

        public void Configure(FtpOptions options)
        {
            options.AllowAnyCertificate = _hostOptions.Value.AllowAnyFtpCertificate;
            options.AllowUnencrypted = _hostOptions.Value.AllowUnencryptedFtp;
        }
    }
}
