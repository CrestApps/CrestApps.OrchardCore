using CrestApps.Core.AI;
using CrestApps.OrchardCore.Orders.AI;
using CrestApps.OrchardCore.Orders.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders;

/// <summary>
/// Registers the gift card AI tools when the Gift cards and AI features are on. Every tool needs <c>ManageGiftCards</c>,
/// as Commerce › Gift cards. Issuing a card and changing a balance create or remove money a holder can spend, so they
/// ask for approval and need an amount, a currency and an idempotency key; emailing a card asks for approval too. No
/// tool returns or takes a card's code.
/// </summary>
[Feature(OrderConstants.Features.GiftCards)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class GiftCardsAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="GiftCardsAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public GiftCardsAIToolsStartup(IStringLocalizer<GiftCardsAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListGiftCardsTool>(ListGiftCardsTool.TheName)
            .WithTitle(S["List Gift Cards"])
            .WithDescription(S["Lists gift cards with the last characters of the code, balance and initial value, currency, status (active, used up, expired, disabled), expiry and recipient. Filters by text and status, with paging."])
            .WithCategory(S["Gift Cards"])
            .Selectable();

        services.AddCoreAITool<GetGiftCardTool>(GetGiftCardTool.TheName)
            .WithTitle(S["Get Gift Card"])
            .WithDescription(S["Returns one gift card with its balance, recipient, note, the order that bought it and its activity (issued, spent, returned, adjusted, with reasons and who). The full code is never returned, only its last characters."])
            .WithCategory(S["Gift Cards"])
            .Selectable();

        services.AddCoreAITool<IssueGiftCardTool>(IssueGiftCardTool.TheName)
            .WithTitle(S["Issue Gift Card"])
            .WithDescription(S["Creates a gift card worth an amount in a currency, with an optional expiry, recipient and note, and optionally emails it to the recipient with a message. It creates money the holder can spend at checkout. The site makes up the code, which is never shown here; every call needs a new idempotency key."])
            .WithCategory(S["Gift Cards"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<UpdateGiftCardTool>(UpdateGiftCardTool.TheName)
            .WithTitle(S["Update Gift Card"])
            .WithDescription(S["Changes a gift card's enabled flag, expiry, recipient name and email, and note. It does not change the balance."])
            .WithCategory(S["Gift Cards"])
            .Selectable();

        services.AddCoreAITool<AdjustGiftCardBalanceTool>(AdjustGiftCardBalanceTool.TheName)
            .WithTitle(S["Adjust Gift Card Balance"])
            .WithDescription(S["Adds to a gift card's balance, or takes from it with a negative amount, in the card's currency, with a required reason that is kept in the card's activity. It is refused while the card is being used at a checkout. Every call needs a new idempotency key."])
            .WithCategory(S["Gift Cards"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<EmailGiftCardTool>(EmailGiftCardTool.TheName)
            .WithTitle(S["Email Gift Card"])
            .WithDescription(S["Emails a gift card with its code to its saved recipient again. The card must have a recipient email."])
            .WithCategory(S["Gift Cards"])
            .RequireApproval()
            .Selectable();
    }
}
