using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Telephony.AI;
using CrestApps.OrchardCore.Telephony.Core.Models;
using CrestApps.OrchardCore.Telephony.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Exposes the telephony extensions to the generic record tools (<c>listRecords</c>, <c>getRecord</c>,
/// <c>saveRecord</c> and <c>deleteRecord</c>) as the <c>telephonyExtension</c> record kind whenever the AI feature is
/// on. Writes replay the extension editor and run the extension's rules (a unique number, an existing user), with the
/// permission the Extensions page checks.
/// </summary>
[RequireFeatures(AIFeatureIds.Area)]
[Feature(TelephonyConstants.Feature.Area)]
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
        services.AddAIRecordKind<TelephonyExtension>("telephonyExtension", kind => kind
            .WithTitle(S["Telephony extension"])
            .WithDescription(S["an internal phone extension (number and the user it rings)"])
            .WithPermission(TelephonyPermissions.ManageExtensions)
            .UseCatalogManager<ITelephonyExtensionManager>()
            .WithProjection(extension => new
            {
                id = extension.ItemId,
                number = extension.Number,
                displayName = extension.DisplayName,
                userId = extension.UserId,
                userName = extension.UserName,
            })
            .UseEditorReplay());
    }
}

/// <summary>
/// Registers the tool that asks the caller's own soft phone to dial whenever the AI feature is on.
/// </summary>
[RequireFeatures(AIFeatureIds.Area)]
[Feature(TelephonyConstants.Feature.SoftPhoneCore)]
public sealed class SoftPhoneAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftPhoneAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SoftPhoneAIToolsStartup(IStringLocalizer<SoftPhoneAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<DialFromSoftPhoneTool>(DialFromSoftPhoneTool.TheName)
            .WithTitle(S["Dial From Soft Phone"])
            .WithDescription(S["Asks the calling user's own soft phone to dial a number. Nothing happens when their soft phone is not open; outbound screening (do-not-call, short codes) still applies in the phone."])
            .WithCategory(S["Telephony"])
            .RequireApproval()
            .Selectable();
    }
}
