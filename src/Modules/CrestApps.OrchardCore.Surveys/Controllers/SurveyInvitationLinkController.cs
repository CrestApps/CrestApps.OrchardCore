using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys.Controllers;

/// <summary>
/// Opens a personal invitation link: starts or resumes the invited person's response.
/// </summary>
[Feature(SurveysConstants.Features.Invitations)]
public sealed class SurveyInvitationLinkController : Controller
{
    private readonly ISurveyResponseService _responses;
    private readonly SurveyInvitationStore _invitationStore;
    private readonly SurveyStore _surveyStore;
    private readonly SurveyCollectorStore _collectorStore;
    private readonly SurveyRespondentIdentityFactory _identities;
    private readonly SurveyErrorMessages _messages;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationLinkController"/> class.
    /// </summary>
    /// <param name="responses">The response service.</param>
    /// <param name="invitationStore">The invitation store.</param>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="collectorStore">The collector store.</param>
    /// <param name="identities">The respondent identity factory.</param>
    /// <param name="messages">The error messages.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyInvitationLinkController(
        ISurveyResponseService responses,
        SurveyInvitationStore invitationStore,
        SurveyStore surveyStore,
        SurveyCollectorStore collectorStore,
        SurveyRespondentIdentityFactory identities,
        SurveyErrorMessages messages,
        IClock clock,
        IStringLocalizer<SurveyInvitationLinkController> stringLocalizer)
    {
        _responses = responses;
        _invitationStore = invitationStore;
        _surveyStore = surveyStore;
        _collectorStore = collectorStore;
        _identities = identities;
        _messages = messages;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Opens an invitation link.
    /// </summary>
    /// <param name="token">The invitation token.</param>
    /// <returns>A redirect to the response, or a page that says why the link does not work.</returns>
    [HttpGet("s/i/{token}")]
    public async Task<IActionResult> Open(string token)
    {
        var invitation = string.IsNullOrWhiteSpace(token) || token.Length > 128
            ? null
            : await _invitationStore.FindByTokenHashAsync(SurveyInvitationTokens.Hash(token));
        var survey = invitation is null ? null : await _surveyStore.FindByIdAsync(invitation.SurveyId);
        var collector = invitation is null ? null : await _collectorStore.FindByIdAsync(invitation.CollectorId);

        if (survey is null || collector is null)
        {
            return Message(S["Survey not found"], S["This link does not work. It may have been replaced by a newer one."]);
        }

        if (invitation.Status is SurveyInvitationStatus.Pending or SurveyInvitationStatus.Sent)
        {
            invitation.Status = SurveyInvitationStatus.Opened;
            await _invitationStore.UpdateAsync(invitation);
        }

        if (invitation.ExpiresUtc is { } expires && _clock.UtcNow >= expires)
        {
            return Message(survey.Title, _messages.For(SurveyErrorCode.InvitationInvalid));
        }

        var identity = _identities.Build(survey.ItemId);
        var result = await _responses.StartAsync(new StartResponseRequest
        {
            SurveyId = survey.ItemId,
            CollectorToken = collector.Token,
            InvitationToken = token,
            Identity = identity,
            Context = SurveyInvitationService.ContextOf(invitation),
            Culture = System.Globalization.CultureInfo.CurrentUICulture.Name,
        });

        if (!result.Succeeded)
        {
            return Message(survey.Title, _messages.For(result.Error));
        }

        if (!string.IsNullOrEmpty(result.ResumeToken))
        {
            _identities.WriteResumeCookie(survey.ItemId, result.ResumeToken);
        }

        return Redirect(Url.Content("~/s/r/" + Uri.EscapeDataString(result.Response.ItemId)));
    }

    private ViewResult Message(string title, string message)
        => View("~/Views/SurveyRunner/Message.cshtml", new ViewModels.SurveyMessageViewModel { Title = title, Message = message });
}
