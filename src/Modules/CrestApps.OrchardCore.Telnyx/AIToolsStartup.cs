using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Telnyx.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telnyx;

/// <summary>
/// Registers the Telnyx connection status and phone number tools whenever the AI feature is on. Every tool checks the
/// caller's permission itself; renaming a number asks for approval, since it changes what callers see. Buying and
/// releasing numbers stay on the Telnyx numbers page, and connecting and disconnecting the account on the Telnyx
/// settings page.
/// </summary>
[RequireFeatures(AIConstants.Feature.Area)]
[Feature(TelnyxConstants.Feature.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetTelnyxConnectionStatusTool>(GetTelnyxConnectionStatusTool.TheName)
            .WithTitle(S["Get Telnyx Connection Status"])
            .WithDescription(S["Tells whether the Telnyx provider is enabled and connected (connection ids present) and returns the default caller id. Never returns the API key."])
            .WithCategory(S["Telephony"])
            .Selectable();

        services.AddCoreAITool<ListTelnyxNumbersTool>(ListTelnyxNumbersTool.TheName)
            .WithTitle(S["List Telnyx Numbers"])
            .WithDescription(S["Lists the phone numbers the Telnyx account owns, with status and reputation."])
            .WithCategory(S["Telephony"])
            .Selectable();

        services.AddCoreAITool<SearchTelnyxAvailableNumbersTool>(SearchTelnyxAvailableNumbersTool.TheName)
            .WithTitle(S["Search Telnyx Available Numbers"])
            .WithDescription(S["Searches phone numbers that can be bought, by country, area code, city, region, digits contained and whether SMS is needed. Returns up to 30."])
            .WithCategory(S["Telephony"])
            .Selectable();

        services.AddCoreAITool<SetTelnyxNumberCallerNameTool>(SetTelnyxNumberCallerNameTool.TheName)
            .WithTitle(S["Set Telnyx Number Caller Name"])
            .WithDescription(S["Sets the caller name shown for an owned number. Carriers can take days to show it."])
            .WithCategory(S["Telephony"])
            .AsExternal()
            .Selectable();
    }
}
