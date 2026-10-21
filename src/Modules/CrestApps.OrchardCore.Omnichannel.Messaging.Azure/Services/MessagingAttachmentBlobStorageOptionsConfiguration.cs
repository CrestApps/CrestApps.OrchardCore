using Fluid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.FileStorage.AzureBlob;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Azure.Services;

internal sealed class MessagingAttachmentBlobStorageOptionsConfiguration : BlobStorageOptionsConfiguration<MessagingAttachmentBlobStorageOptions>
{
    internal const string ConfigurationSectionName = "CrestApps:Omnichannel:Messaging:AzureAttachments";

    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingAttachmentBlobStorageOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="fluidParser">The Fluid parser used to expand templated container and base-path values.</param>
    /// <param name="shellConfiguration">The shell configuration.</param>
    /// <param name="shellSettings">The shell settings.</param>
    /// <param name="logger">The logger.</param>
    public MessagingAttachmentBlobStorageOptionsConfiguration(
        FluidParser fluidParser,
        IShellConfiguration shellConfiguration,
        ShellSettings shellSettings,
        ILogger<MessagingAttachmentBlobStorageOptionsConfiguration> logger)
        : base(fluidParser, shellSettings, logger)
    {
        _shellConfiguration = shellConfiguration;
    }

    protected override MessagingAttachmentBlobStorageOptions GetRawOptions()
        => _shellConfiguration.GetSection(ConfigurationSectionName)
            .Get<MessagingAttachmentBlobStorageOptions>();

    protected override void FurtherConfigure(MessagingAttachmentBlobStorageOptions rawOptions, MessagingAttachmentBlobStorageOptions options)
    {
        options.CreateContainer = rawOptions.CreateContainer;
        options.RemoveContainer = rawOptions.RemoveContainer;
        options.RemoveFilesFromBasePath = rawOptions.RemoveFilesFromBasePath;
    }
}
