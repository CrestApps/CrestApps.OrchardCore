using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.FileStorage.AzureBlob;

namespace CrestApps.OrchardCore.AI.Documents.Azure.Services;

internal sealed class AIDocumentBlobStorageOptionsConfiguration : BlobStorageOptionsConfiguration<AIDocumentBlobStorageOptions>
{
    internal const string ConfigurationSectionName = "CrestApps:AI:AzureDocuments";

    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDocumentBlobStorageOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration.</param>
    /// <param name="shellSettings">The shell settings.</param>
    /// <param name="logger">The logger.</param>
    public AIDocumentBlobStorageOptionsConfiguration(
        IShellConfiguration shellConfiguration,
        ShellSettings shellSettings,
        ILogger<AIDocumentBlobStorageOptionsConfiguration> logger)
        : base(shellSettings, logger)
    {
        _shellConfiguration = shellConfiguration;
    }

    protected override AIDocumentBlobStorageOptions GetRawOptions()
        => _shellConfiguration.GetSection(ConfigurationSectionName)
        .Get<AIDocumentBlobStorageOptions>();

    protected override void FurtherConfigure(AIDocumentBlobStorageOptions rawOptions, AIDocumentBlobStorageOptions options)
    {
        options.CreateContainer = rawOptions.CreateContainer;
        options.RemoveContainer = rawOptions.RemoveContainer;
        options.RemoveFilesFromBasePath = rawOptions.RemoveFilesFromBasePath;
    }
}
