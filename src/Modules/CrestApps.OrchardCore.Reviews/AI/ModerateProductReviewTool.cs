using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reviews.Models;
using CrestApps.OrchardCore.Reviews.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Reviews.AI;

/// <summary>
/// Approves or rejects a product review, as Approve and Reject on Commerce › Reviews do
/// (<see cref="ReviewService.ModerateAsync"/>, which also refreshes the product's rating). The service does nothing when
/// the review already has the status, so the tool says so instead of reporting a change.
/// </summary>
internal sealed class ModerateProductReviewTool : ReviewsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "moderateProductReview";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "reviewId": {
          "type": "string",
          "description": "The review's id, from listProductReviews."
        },
        "action": {
          "type": "string",
          "enum": ["approve", "reject"],
          "description": "approve shows the review on the product and counts it toward the rating; reject hides it."
        }
      },
      "required": ["reviewId", "action"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Approves a review so it shows on the product page and counts toward its rating, or rejects it so it is hidden (action: approve or reject). A rejected review can be approved later.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ReviewsPermissions.ManageReviews;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (ReadString(arguments, "reviewId") is not { } reviewId)
        {
            return "Nothing was changed: give the review's id. Use listProductReviews to find it.";
        }

        var action = ReadString(arguments, "action");
        ReviewStatus status;

        if (string.Equals(action, "approve", StringComparison.OrdinalIgnoreCase))
        {
            status = ReviewStatus.Approved;
        }
        else if (string.Equals(action, "reject", StringComparison.OrdinalIgnoreCase))
        {
            status = ReviewStatus.Rejected;
        }
        else
        {
            return "Nothing was changed: action must be approve or reject.";
        }

        var reviews = services.GetRequiredService<ReviewService>();
        var review = await reviews.FindAsync(reviewId, cancellationToken);

        if (review is null)
        {
            return $"Nothing was changed: no review has the id '{reviewId}'. Use listProductReviews to find it.";
        }

        if (review.Status == status)
        {
            return $"Nothing was changed: the review of {review.ProductTitle} is already {ToCamel(status.ToString())}.";
        }

        await reviews.ModerateAsync(review, status, caller.Identity?.Name, cancellationToken);

        if (await CommitAsync(services, cancellationToken) is { } error)
        {
            return error;
        }

        return Json(new
        {
            review = await ListProductReviewsTool.DescribeAsync(services, review),
            message = status == ReviewStatus.Approved
                ? $"The review of {review.ProductTitle} is approved and shows on the product."
                : $"The review of {review.ProductTitle} was rejected and is hidden.",
            note = ListProductReviewsTool.UntrustedNote,
        });
    }
}
