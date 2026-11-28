using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reviews.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Reviews.AI;

/// <summary>
/// Permanently deletes one product review, as Delete on Commerce › Reviews does (<see cref="ReviewService.DeleteAsync"/>,
/// which refreshes the product's rating when the review was approved).
/// </summary>
internal sealed class DeleteProductReviewTool : ReviewsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteProductReview";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "reviewId": {
          "type": "string",
          "description": "The review's id, from listProductReviews."
        }
      },
      "required": ["reviewId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Permanently deletes one product review. Prefer rejecting; use delete for spam or personal data that must be removed.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ReviewsPermissions.ManageReviews;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (ReadString(arguments, "reviewId") is not { } reviewId)
        {
            return "Nothing was deleted: give the review's id. Use listProductReviews to find it.";
        }

        var reviews = services.GetRequiredService<ReviewService>();
        var review = await reviews.FindAsync(reviewId, cancellationToken);

        if (review is null)
        {
            return $"Nothing was deleted: no review has the id '{reviewId}'. Use listProductReviews to find it.";
        }

        await reviews.DeleteAsync(review, cancellationToken);

        if (await CommitAsync(services, cancellationToken) is { } error)
        {
            return error;
        }

        return Json(new
        {
            deletedId = review.ItemId,
            productId = review.ProductId,
            product = review.ProductTitle,
            message = $"The review of {review.ProductTitle} was deleted for good.",
        });
    }
}
