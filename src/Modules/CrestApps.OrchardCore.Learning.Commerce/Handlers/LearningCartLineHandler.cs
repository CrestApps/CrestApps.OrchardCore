using System.Security.Claims;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Commerce.Handlers;

/// <summary>
/// A course is for the person who buys it, so it needs a sign-in, a published course and its prerequisites met, and a
/// line buys it once; seats are for an account, so only one of its admins can buy them, in any quantity.
/// </summary>
internal sealed class LearningCartLineHandler : ICartLineHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAccountMemberService _members;
    private readonly ICourseStructureService _courses;
    private readonly IEnumerable<ICoursePrerequisiteEvaluator> _prerequisiteEvaluators;

    internal readonly IStringLocalizer S;

    public LearningCartLineHandler(
        IHttpContextAccessor httpContextAccessor,
        IAccountMemberService members,
        ICourseStructureService courses,
        IEnumerable<ICoursePrerequisiteEvaluator> prerequisiteEvaluators,
        IStringLocalizer<LearningCartLineHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _members = members;
        _courses = courses;
        _prerequisiteEvaluators = prerequisiteEvaluators;
        S = stringLocalizer;
    }

    public async Task PrepareAsync(CartLinePrepareContext context, CancellationToken cancellationToken = default)
    {
        var problem = await CheckAsync(context.ContentItem, context.Title, cancellationToken);

        if (problem is not null)
        {
            context.Refuse(problem);
        }
    }

    public async Task EvaluateAsync(CartLineEvaluateContext context, CancellationToken cancellationToken = default)
    {
        context.Problem ??= await CheckAsync(context.ContentItem, context.ContentItem?.DisplayText, cancellationToken);

        // A course or a path is for the buyer alone: more than one would charge for access nobody gets.
        if (context.ContentItem is not null && context.ContentItem.TryGet<LearningProductPart>(out var part) && !HasSeats(part) && context.MaxQuantity > 1)
        {
            context.MaxQuantity = 1;
            context.MaxQuantityReason = S["{0} is for you alone, so one is enough.", context.ContentItem.DisplayText ?? string.Empty];
        }
    }

    private static bool HasSeats(LearningProductPart part)
        => part.Grants.Any(grant => grant.Kind == LearningProductGrantKind.Seats);

    private async Task<LocalizedString> CheckAsync(ContentItem item, string title, CancellationToken cancellationToken)
    {
        if (item is null || !item.TryGet<LearningProductPart>(out var part))
        {
            return null;
        }

        title ??= string.Empty;

        var user = _httpContextAccessor.HttpContext?.User;
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return S["Sign in to buy {0}: learning is for your account.", title];
        }

        if (HasSeats(part))
        {
            var account = await _members.FindAccountAsync(userId, cancellationToken);
            var isAdmin = account is not null && account.TryGet<AccountMembersPart>(out var members) && members.FindMember(userId)?.Role == AccountMemberRole.Admin;

            if (!isAdmin)
            {
                return S["Only an admin of a business account can buy seats in {0}.", title];
            }
        }

        var grants = part.Grants.Count > 0
            ? part.Grants
            : [new LearningProductGrant { Kind = item.Has<LearningPathPart>() ? LearningProductGrantKind.LearningPath : LearningProductGrantKind.Course }];

        foreach (var grant in grants.Where(grant => grant.Kind is LearningProductGrantKind.Course or LearningProductGrantKind.Seats))
        {
            var courseId = string.IsNullOrEmpty(grant.TargetId) ? item.ContentItemId : grant.TargetId;
            var course = await _courses.GetCourseAsync(courseId);

            if (course is null)
            {
                return S["{0} is not open for enrollment.", title];
            }

            // Seats are for other people, so only a course for the buyer needs the buyer to meet its prerequisites.
            if (grant.Kind == LearningProductGrantKind.Course &&
                !await CoursePrerequisites.MeetsAsync(_prerequisiteEvaluators, userId, course.Get<CoursePart>(nameof(CoursePart)), cancellationToken))
            {
                return S["Finish the courses {0} needs first.", title];
            }
        }

        return null;
    }
}
