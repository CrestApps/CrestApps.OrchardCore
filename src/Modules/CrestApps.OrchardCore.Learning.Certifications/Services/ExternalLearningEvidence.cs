using System.Security.Claims;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Certifications.Core;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Learning.Certifications.Services;

/// <summary>
/// The protected file area of the evidence of external learning.
/// </summary>
public sealed class ExternalLearningEvidence
{
    /// <summary>
    /// The folder of the area in the tenant's App_Data.
    /// </summary>
    public const string Folder = "ExternalLearningEvidence";
}

/// <summary>
/// Opens the evidence of external learning to the person who recorded it and to reviewers.
/// </summary>
internal sealed class ExternalLearningEvidencePolicy : IProtectedFileAccessPolicy<ExternalLearningEvidence>
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IEducationCreditService _credits;

    public ExternalLearningEvidencePolicy(IAuthorizationService authorizationService, IEducationCreditService credits)
    {
        _authorizationService = authorizationService;
        _credits = credits;
    }

    public async Task<bool> CanReadAsync(ProtectedFileAccess access, CancellationToken cancellationToken = default)
    {
        if (access.User is null)
        {
            return false;
        }

        if (await _authorizationService.AuthorizeAsync(access.User, LearningCertificationsPermissions.ReviewExternalLearning))
        {
            return true;
        }

        var submission = await _credits.FindExternalAsync(access.ResourceId, cancellationToken);

        return submission?.EvidenceFileId == access.FileId && submission.UserId == access.User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
