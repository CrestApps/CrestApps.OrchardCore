using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.FileUpload;

/// <summary>
/// Decides who may see and manage the files of a response: staff who may view or delete responses, the survey's
/// owner and anyone the survey authorization service lets view, grade or delete the survey's responses (such as course
/// graders), and the signed-in respondent of an identified response (managing only while it is in progress). Nobody
/// may see or manage the files of a response that was deleted.
/// </summary>
public sealed class SurveyResponseAttachmentAccessProvider : IAttachmentAccessProvider
{
    private readonly SurveyResponseStore _responses;
    private readonly ICatalog<Survey> _surveys;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<ISurveyAuthorizationService> _surveyAuthorization;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseAttachmentAccessProvider"/> class.
    /// </summary>
    /// <param name="responses">The response store.</param>
    /// <param name="surveys">The survey store.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="surveyAuthorization">The survey authorization service, when the authoring feature registers it.</param>
    public SurveyResponseAttachmentAccessProvider(
        SurveyResponseStore responses,
        ICatalog<Survey> surveys,
        IAuthorizationService authorizationService,
        IEnumerable<ISurveyAuthorizationService> surveyAuthorization)
    {
        _responses = responses;
        _surveys = surveys;
        _authorizationService = authorizationService;
        _surveyAuthorization = surveyAuthorization;
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

        var response = await _responses.FindByIdAsync(owner.Id, cancellationToken);

        // The files of a response that is gone are nobody's: they wait for the orphan sweep.
        if (response is null)
        {
            return false;
        }

        if (await _authorizationService.AuthorizeAsync(user, SurveysPermissions.ViewSurveyResponses))
        {
            return true;
        }

        if (IsRespondent(user, response))
        {
            return true;
        }

        return await AuthorizeOnSurveyAsync(user, response, cancellationToken, SurveyOperation.ViewResponses, SurveyOperation.Grade);
    }

    /// <inheritdoc />
    public async Task<bool> CanManageAsync(ClaimsPrincipal user, RecordRef owner, CancellationToken cancellationToken = default)
    {
        if (user is null || owner.IsEmpty)
        {
            return false;
        }

        var response = await _responses.FindByIdAsync(owner.Id, cancellationToken);

        // The files of a response that is gone are nobody's: they wait for the orphan sweep.
        if (response is null)
        {
            return false;
        }

        if (await _authorizationService.AuthorizeAsync(user, SurveysPermissions.DeleteSurveyResponses))
        {
            return true;
        }

        if (IsRespondent(user, response) && response.Status == SurveyResponseStatus.InProgress)
        {
            return true;
        }

        return await AuthorizeOnSurveyAsync(user, response, cancellationToken, SurveyOperation.DeleteResponses);
    }

    private async Task<bool> AuthorizeOnSurveyAsync(ClaimsPrincipal user, SurveyResponse response, CancellationToken cancellationToken, params SurveyOperation[] operations)
    {
        if (response is null
            || string.IsNullOrEmpty(response.SurveyId)
            || _surveyAuthorization.FirstOrDefault() is not { } authorization)
        {
            return false;
        }

        var survey = await _surveys.FindByIdAsync(response.SurveyId, cancellationToken);

        if (survey is null)
        {
            return false;
        }

        foreach (var operation in operations)
        {
            if (await authorization.AuthorizeAsync(user, survey, operation, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRespondent(ClaimsPrincipal user, SurveyResponse response)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        return response is not null
            && !string.IsNullOrEmpty(userId)
            && string.Equals(response.UserId, userId, StringComparison.Ordinal);
    }
}
