using CrestApps.Core.AI;
using CrestApps.OrchardCore.Accounts.B2B.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Accounts.B2B;

/// <summary>
/// Registers the business buying and price list AI tools whenever the Accounts B2B and AI features are on. Every tool
/// needs <c>ManageB2BAccounts</c>; the tools that change an account also need <c>EditContent</c> on it. Saving an
/// account's business buying settings (it sets credit limits and net terms, which extends credit), punchout and deleting
/// a price list ask for approval.
/// </summary>
[RequireFeatures(AIFeatureIds.Area)]
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
        services.AddCoreAITool<GetB2BAccountSummaryTool>(GetB2BAccountSummaryTool.TheName)
            .WithTitle(S["Get B2B Account Summary"])
            .WithDescription(S["Returns an account's business-buying setup and standing: enabled, terms, credit limit and available credit, amount owed, open term invoices, monthly budget and spend, price list, buyers with their order limits, and whether punchout is on."])
            .WithCategory(S["Accounts"])
            .Selectable();

        services.AddCoreAITool<SaveB2BAccountSettingsTool>(SaveB2BAccountSettingsTool.TheName)
            .WithTitle(S["Save B2B Account Settings"])
            .WithDescription(S["Turns business buying on or off for an account and sets tax id, billing email, payment terms (net days, deposit percent, credit limit, purchase order required), price list, monthly budget and per-buyer order limits. Buyers must already be members of the account."])
            .WithCategory(S["Accounts"])
            .AsWrite()
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<ConfigureB2BPunchoutTool>(ConfigureB2BPunchoutTool.TheName)
            .WithTitle(S["Configure B2B Punchout"])
            .WithDescription(S["Enables or disables punchout for an account and chooses the member the procurement system shops as; the buyer cannot be a site administrator. Never issues, returns or accepts a punchout key: a new key is issued on the account's editor, which shows it once."])
            .WithCategory(S["Accounts"])
            .AsSecurity()
            .Selectable();

        services.AddCoreAITool<ListPriceListsTool>(ListPriceListsTool.TheName)
            .WithTitle(S["List Price Lists"])
            .WithDescription(S["Lists price lists with their percent off, currency and entry count, filtered by name; pass an id to get its SKU prices."])
            .WithCategory(S["Accounts"])
            .Selectable();

        services.AddCoreAITool<SavePriceListTool>(SavePriceListTool.TheName)
            .WithTitle(S["Save Price List"])
            .WithDescription(S["Creates or updates a price list: name, percent off and unit prices by SKU, in the store's currency. On update the entries you send replace the stored ones."])
            .WithCategory(S["Accounts"])
            .Selectable();

        services.AddCoreAITool<DeletePriceListTool>(DeletePriceListTool.TheName)
            .WithTitle(S["Delete Price List"])
            .WithDescription(S["Deletes a price list. Accounts that used it fall back to normal prices."])
            .WithCategory(S["Accounts"])
            .AsDestructive()
            .Selectable();
    }
}
