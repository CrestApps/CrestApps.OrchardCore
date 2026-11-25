using CrestApps.Core.AI;
using CrestApps.OrchardCore.PointOfSale.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.PointOfSale;

/// <summary>
/// Registers <c>declareShiftTips</c> when the Point of Sale and AI features are on, under <c>ManageRegisters</c> as the
/// shift's page. With Payroll on, the card tips are paid to the cashier on the next pay run and the cash tips are
/// reported for tax, so the tool changes pay and overrides what the cashier declared: it asks for approval. The tips are
/// absolute values that can be corrected until a pay run takes them, so it needs no preview token.
/// </summary>
[Feature(PosConstants.Feature)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class ShiftTipsAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShiftTipsAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ShiftTipsAIToolsStartup(IStringLocalizer<ShiftTipsAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<DeclareShiftTipsTool>(DeclareShiftTipsTool.TheName)
            .WithTitle(S["Declare Shift Tips"])
            .WithDescription(S["Declares or corrects the card and cash tips of a closed register shift until payroll took them; with Payroll on, card tips are paid on the next pay run."])
            .WithCategory(S["Point of Sale"])
            .AsExternal()
            .Selectable();
    }
}
