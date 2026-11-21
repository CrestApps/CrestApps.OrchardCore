using System.Security.Claims;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.FileUpload;

/// <summary>
/// Decides who may see and manage the files of a response: staff who may view or delete responses, and the signed-in
/// respondent of an identified response (managing only while it is in progress).
/// </summary>
public sealed class SurveyResponseAttachmentAccessProvider : IAttachmentAccessProvider
{
    private readonly SurveyResponseStore _responses;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseAttachmentAccessProvider"/> class.
    /// </summary>
    /// <param name="responses">The response store.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public SurveyResponseAttachmentAccessProvider(
        SurveyResponseStore responses,
        IAuthorizationService authorizationService)
    {
        _responses = responses;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc />
    public string OwnerKind => SurveysConstants.ResponseRecordKind;

    /// <inheritdoc />
    public async Task<bool> CanViewAsync(ClaimsPrincipal user, RecordRef owner, CancellationToken cancellationToken = default)
    {
        if (user is null || owner.IsEmpty)
        {
            return false;
        }

        if (await _authorizationService.AuthorizeAsync(user, SurveysPermissions.ViewSurveyResponses))
        {
            return true;
        }

        var response = await _responses.FindByIdAsync(owner.Id, cancellationToken);

        return IsOwner(user, response);
    }

    /// <inheritdoc />
    public async Task<bool> CanManageAsync(ClaimsPrincipal user, RecordRef owner, CancellationToken cancellationToken = default)
    {
        if (user is null || owner.IsEmpty)
        {
            return false;
        }

        if (await _authorizationService.AuthorizeAsync(user, SurveysPermissions.DeleteSurveyResponses))
        {
            return true;
        }

        var response = await _responses.FindByIdAsync(owner.Id, cancellationToken);

        return IsOwner(user, response) && response.Status == SurveyResponseStatus.InProgress;
    }

    private static bool IsOwner(ClaimsPrincipal user, SurveyResponse response)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        return response is not null
            && !string.IsNullOrEmpty(userId)
            && string.Equals(response.UserId, userId, StringComparison.Ordinal);
    }
}
