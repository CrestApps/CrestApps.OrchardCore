using Fluid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.FileStorage.AzureBlob;

namespace CrestApps.OrchardCore.Telephony.Azure.Services;

internal sealed class TelephonyRecordingBlobStorageOptionsConfiguration : BlobStorageOptionsConfiguration<TelephonyRecordingBlobStorageOptions>
{
    internal const string ConfigurationSectionName = "CrestApps:Telephony:AzureRecordings";

    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyRecordingBlobStorageOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="fluidParser">The Fluid parser used to expand templated container and base-path values.</param>
    /// <param name="shellConfiguration">The shell configuration.</param>
    /// <param name="shellSettings">The shell settings.</param>
    /// <param name="logger">The logger.</param>
    public TelephonyRecordingBlobStorageOptionsConfiguration(
        FluidParser fluidParser,
        IShellConfiguration shellConfiguration,
        ShellSettings shellSettings,
        ILogger<TelephonyRecordingBlobStorageOptionsConfiguration> logger)
        : base(fluidParser, shellSettings, logger)
    {
        _shellConfiguration = shellConfiguration;
    }

    protected override TelephonyRecordingBlobStorageOptions GetRawOptions()
        => _shellConfiguration.GetSection(ConfigurationSectionName)
            .Get<TelephonyRecordingBlobStorageOptions>();

    protected override void FurtherConfigure(TelephonyRecordingBlobStorageOptions rawOptions, TelephonyRecordingBlobStorageOptions options)
    {
        options.CreateContainer = rawOptions.CreateContainer;
        options.RemoveContainer = rawOptions.RemoveContainer;
        options.RemoveFilesFromBasePath = rawOptions.RemoveFilesFromBasePath;
    }
}
