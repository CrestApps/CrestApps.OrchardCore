using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Certifications.Services;

/// <summary>
/// Offers, on the review of an enrollment, to take back the education credits the enrollment earned.
/// </summary>
internal sealed class CreditsReviewActionProvider : IEnrollmentReviewActionProvider
{
    private readonly IEducationCreditService _credits;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    internal readonly IStringLocalizer S;

    public CreditsReviewActionProvider(IEducationCreditService credits, IHttpContextAccessor httpContextAccessor, LinkGenerator linkGenerator, IStringLocalizer<CreditsReviewActionProvider> stringLocalizer)
    {
        _credits = credits;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        S = stringLocalizer;
    }

    public async Task<IEnumerable<EnrollmentReviewAction>> GetActionsAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var entries = await _credits.ListReversibleAsync(enrollment.UserId, enrollment.ItemId, cancellationToken);

        if (httpContext is null || entries.Count == 0)
        {
            return [];
        }

        var total = entries.Sum(entry => entry.Amount);

        return
        [
            new EnrollmentReviewAction
            {
                Text = S["Take back the {0} credits this enrollment earned", total],
                Hint = S["Posts a reversing entry for each, on the same days, so balances and credits requirements no longer count them. The review stays open until you clear it."],
                Confirmation = S["Take back the {0} credits this enrollment earned?", total],
                Url = _linkGenerator.GetPathByAction(httpContext, "ReverseCreditsFromReview", "CertificationPaths", new { area = "CrestApps.OrchardCore.Learning.Certifications", enrollmentId = enrollment.ItemId }),
            },
        ];
    }
}
