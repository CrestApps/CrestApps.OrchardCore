using CrestApps.OrchardCore.Outreach.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Outreach;

/// <summary>
/// Adds the e-mail channel sender when Orchard's e-mail feature is enabled.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Email)]
public sealed class EmailStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddOutreachChannelSender<EmailOutreachChannelSender>();
}
