using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Communications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that sends the test text message to check the SMS settings, when the SMS feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Sms)]
public sealed class SmsToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SmsToolsStartup(IStringLocalizer<SmsToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<SendTestSmsTool>(SendTestSmsTool.TheName)
            .WithTitle(S["Send Test SMS"])
            .WithDescription(S["Sends the fixed test text message to a phone number through a chosen SMS provider and returns the provider's result or error. Each message costs money; use it only to check SMS settings."])
            .WithCategory(S["Communications"])
            .AsExternal()
            .Selectable();
    }
}
