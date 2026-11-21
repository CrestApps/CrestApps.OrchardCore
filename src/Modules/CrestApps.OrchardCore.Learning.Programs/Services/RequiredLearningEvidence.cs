using System.Security.Claims;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Programs.Core;
using CrestApps.OrchardCore.Learning.Programs.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Learning.Programs.Services;

/// <summary>
/// The protected file area of the evidence attached to exemptions.
/// </summary>
public sealed class RequiredLearningEvidence
{
    /// <summary>
    /// The folder of the area in the tenant's App_Data.
    /// </summary>
    public const string Folder = "LearningEvidence";
}

/// <summary>
/// Opens the evidence of an exemption to the person it is about and to people who manage required learning.
/// </summary>
internal sealed class RequiredLearningEvidencePolicy : IProtectedFileAccessPolicy<RequiredLearningEvidence>
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IRequiredLearningService _requiredLearning;

    public RequiredLearningEvidencePolicy(IAuthorizationService authorizationService, IRequiredLearningService requiredLearning)
    {
        _authorizationService = authorizationService;
        _requiredLearning = requiredLearning;
    }

    public async Task<bool> CanReadAsync(ProtectedFileAccess access, CancellationToken cancellationToken = default)
    {
        if (access.User is null)
        {
            return false;
        }

        if (await _authorizationService.AuthorizeAsync(access.User, LearningProgramsPermissions.ExemptFromRequiredLearning))
        {
            return true;
        }

        var assignment = await _requiredLearning.FindAssignmentAsync(access.ResourceId, cancellationToken);

        return assignment?.Exemption?.EvidenceFileId == access.FileId
            && assignment.UserId == access.User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
