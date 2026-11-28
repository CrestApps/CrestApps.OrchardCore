using CrestApps.Core.AI;
using CrestApps.OrchardCore.Reviews.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reviews;

/// <summary>
/// Registers the product review AI tools when the AI feature is on, with the permission of Commerce › Reviews.
/// </summary>
[Feature(ReviewsConstants.Feature)]
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

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListProductReviewsTool>(ListProductReviewsTool.TheName)
            .WithTitle(S["List Product Reviews"])
            .WithDescription(S["Lists product reviews by status (default: waiting for approval) and optionally by product, with paging: rating, title, text, author, verified purchase, photos. With a product id it also returns the product's average rating and count."])
            .WithCategory(S["Product Reviews"])
            .Selectable();

        services.AddCoreAITool<ModerateProductReviewTool>(ModerateProductReviewTool.TheName)
            .WithTitle(S["Moderate Product Review"])
            .WithDescription(S["Approves a review so it shows on the product page and counts toward its rating, or rejects it so it is hidden (action: approve or reject). A rejected review can be approved later."])
            .WithCategory(S["Product Reviews"])
            .Selectable();

        services.AddCoreAITool<DeleteProductReviewTool>(DeleteProductReviewTool.TheName)
            .WithTitle(S["Delete Product Review"])
            .WithDescription(S["Permanently deletes one product review. Prefer rejecting; use delete for spam or personal data that must be removed."])
            .WithCategory(S["Product Reviews"])
            .AsDestructive()
            .Selectable();
    }
}
