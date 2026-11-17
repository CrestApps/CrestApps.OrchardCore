using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Communications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that sends a test email to check the email settings, when the Email feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Email)]
public sealed class EmailToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EmailToolsStartup(IStringLocalizer<EmailToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<SendTestEmailTool>(SendTestEmailTool.TheName)
            .WithTitle(S["Send Test Email"])
            .WithDescription(S["Sends a test email through a chosen email provider (or the default one) and returns the provider's result or error. Use it to check email settings after changing them; it also lists the available providers when none is given."])
            .WithCategory(S["Communications"])
            .RequireApproval()
            .Selectable();
    }
}
