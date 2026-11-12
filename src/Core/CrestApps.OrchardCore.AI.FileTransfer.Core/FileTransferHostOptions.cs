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
}

/// <summary>
/// Registers <see cref="FileTransferHostOptions"/>.
/// </summary>
public static class FileTransferHostOptionsServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="FileTransferHostOptions"/> to the tenant's configuration. Safe to call more than once.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddFileTransferHostOptions(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<FileTransferHostOptions>, FileTransferHostOptionsConfiguration>());

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
}
