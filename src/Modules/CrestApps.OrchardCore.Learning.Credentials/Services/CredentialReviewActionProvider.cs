using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Credentials.Services;

/// <summary>
/// Offers to revoke, from the review card of an enrollment, the credentials its completion awarded. A completion
/// flagged for review is not revoked by itself: staff decide.
/// </summary>
internal sealed class CredentialReviewActionProvider : IEnrollmentReviewActionProvider
{
    private readonly CredentialSourceRevoker _revoker;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    internal readonly IStringLocalizer S;

    public CredentialReviewActionProvider(
        CredentialSourceRevoker revoker,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator,
        IStringLocalizer<CredentialReviewActionProvider> stringLocalizer)
    {
        _revoker = revoker;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        S = stringLocalizer;
    }

    public async Task<IEnumerable<EnrollmentReviewAction>> GetActionsAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (enrollment is null || !enrollment.NeedsReview || httpContext is null)
        {
            return [];
        }

        var actions = new List<EnrollmentReviewAction>();

        foreach (var award in await _revoker.ListActiveAsync(CredentialsConstants.RuleKinds.CourseCompleted, enrollment.ItemId, cancellationToken))
        {
            var name = award.Achievement?.Name ?? award.DefinitionId;

            actions.Add(new EnrollmentReviewAction
            {
                Text = S["Revoke the credential {0}", name],
                Hint = S["The credential was awarded for this completion. Revoking it shows it as revoked on its verification page and in its status list at once; the review stays open until you clear it."],
                Confirmation = S["Revoke the credential {0}?", name],
                Url = _linkGenerator.GetPathByAction(httpContext, "RevokeFromReview", "CredentialAwardsAdmin", new { area = CredentialsConstants.Features.DigitalCredentials, enrollmentId = enrollment.ItemId, awardId = award.ItemId }),
            });
        }

        return actions;
    }
}
