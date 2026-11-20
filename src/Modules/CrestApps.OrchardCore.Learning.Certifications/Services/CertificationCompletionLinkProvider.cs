using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Certifications.Services;

/// <summary>
/// Links the certifications a completed course counts toward on the course completion panel: the certificate when the
/// learner holds it, otherwise the certification it brings them closer to.
/// </summary>
internal sealed class CertificationCompletionLinkProvider : ICourseCompletionLinkProvider
{
    private readonly ICertificationPathService _paths;
    private readonly ICertificationManager _certifications;

    internal readonly IStringLocalizer S;

    public CertificationCompletionLinkProvider(
        ICertificationPathService paths,
        ICertificationManager certifications,
        IStringLocalizer<CertificationCompletionLinkProvider> stringLocalizer)
    {
        _paths = paths;
        _certifications = certifications;
        S = stringLocalizer;
    }

    public async Task<IEnumerable<CourseCompletionLink>> GetLinksAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(enrollment?.UserId) || string.IsNullOrEmpty(enrollment.CourseId))
        {
            return [];
        }

        var links = new List<CourseCompletionLink>();

        foreach (var path in await _paths.ListByTargetAsync(enrollment.CourseId, cancellationToken))
        {
            var progress = await _paths.FindProgressAsync(path.ItemId, enrollment.UserId, cancellationToken);

            if (progress is null)
            {
                continue;
            }

            var name = (await _certifications.FindByIdAsync(path.CertificationId))?.Name;

            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            links.Add(new CourseCompletionLink
            {
                Text = progress.State is CertificationPathState.Certified or CertificationPathState.RenewalOpen
                    ? S["Your certificate: {0}", name]
                    : S["Counts toward {0}", name],
                Url = "~/learning/certifications",
                Icon = "fa-solid fa-certificate",
                Position = 10,
            });
        }

        return links;
    }
}
