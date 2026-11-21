using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Learning.Lti.ViewModels;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Lti.Drivers;

/// <summary>
/// The External tool panel of the learner player: the tool, the learner's latest score from it, and the Launch button
/// that starts the tool's login in a window of its own. Only the signed-in learner's own enrollment and score are read.
/// </summary>
internal sealed class ExternalToolItemPartLearnerDisplayDriver : ContentPartDisplayDriver<ExternalToolItemPart>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CourseItemLearnerResolver _learnerResolver;
    private readonly IExternalToolStore _tools;
    private readonly LtiLineItemStore _lineItems;
    private readonly LtiScoreStore _scores;

    public ExternalToolItemPartLearnerDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        CourseItemLearnerResolver learnerResolver,
        IExternalToolStore tools,
        LtiLineItemStore lineItems,
        LtiScoreStore scores)
    {
        _httpContextAccessor = httpContextAccessor;
        _learnerResolver = learnerResolver;
        _tools = tools;
        _lineItems = lineItems;
        _scores = scores;
    }

    public override IDisplayResult Display(ExternalToolItemPart part, BuildPartDisplayContext context)
        => Initialize<ExternalToolPanelViewModel>("ExternalToolItemPart_Learner", async model =>
        {
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var (_, enrollment) = await _learnerResolver.ResolveAsync(userId, part.ContentItem);
            var tool = string.IsNullOrEmpty(part.ToolId) ? null : await _tools.FindByIdAsync(part.ToolId);

            model.ItemId = part.ContentItem.ContentItemId;
            model.ToolName = tool?.Name;
            model.ToolAvailable = tool is { Enabled: true };
            model.SignedIn = !string.IsNullOrEmpty(userId);
            model.Enrolled = enrollment is not null;
            model.CanLaunch = model.ToolAvailable && enrollment?.Status == EnrollmentStatus.Active;

            if (tool is not null && enrollment is not null && string.Equals(enrollment.UserId, userId, StringComparison.Ordinal))
            {
                var lineItem = await _lineItems.FindByResourceLinkAsync(part.ContentItem.ContentItemId, tool.ItemId);

                if (lineItem is not null)
                {
                    model.Score = await _scores.FindAsync(lineItem.ItemId, userId);
                }
            }
        }).Location("Detail", "Content:5");
}
