using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Credentials.Services;

/// <summary>
/// Links the digital credentials a course completion earned on the course completion panel.
/// </summary>
internal sealed class CredentialCompletionLinkProvider : ICourseCompletionLinkProvider
{
    private readonly ICredentialAwardService _awards;

    internal readonly IStringLocalizer S;

    public CredentialCompletionLinkProvider(
        ICredentialAwardService awards,
        IStringLocalizer<CredentialCompletionLinkProvider> stringLocalizer)
    {
        _awards = awards;
        S = stringLocalizer;
    }

    public async Task<IEnumerable<CourseCompletionLink>> GetLinksAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(enrollment?.UserId))
        {
            return [];
        }

        // A course completion is awarded with the enrollment as its source.
        return (await _awards.ListForUserAsync(enrollment.UserId, cancellationToken))
            .Where(award => award.RevokedUtc is null && string.Equals(award.SourceId, enrollment.ItemId, StringComparison.Ordinal))
            .Select(award => new CourseCompletionLink
            {
                Text = S["Your credential: {0}", award.Achievement?.Name ?? string.Empty],
                Url = "~/learning/credentials",
                Icon = "fa-solid fa-id-badge",
                Position = 20,
            })
            .ToList();
    }
}
