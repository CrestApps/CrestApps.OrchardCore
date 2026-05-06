using CrestApps.Core.AI.Chat.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Services;

/// <summary>
/// Configures <see cref="RowLevelTabularBatchOptions"/> from shell configuration,
/// supporting both the new and deprecated configuration paths.
/// </summary>
internal sealed class RowLevelTabularBatchOptionsConfiguration : IConfigureOptions<RowLevelTabularBatchOptions>
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="RowLevelTabularBatchOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration.</param>
    /// <param name="logger">The logger.</param>
    public RowLevelTabularBatchOptionsConfiguration(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    /// <summary>
    /// Configures the <see cref="RowLevelTabularBatchOptions"/>.
    /// </summary>
    /// <param name="options">The options instance to configure.</param>
    public void Configure(RowLevelTabularBatchOptions options)
    {
        _shellConfiguration.GetSection("CrestApps:AI:ChatInteractions:BatchProcessing").Bind(options);
    }
}
