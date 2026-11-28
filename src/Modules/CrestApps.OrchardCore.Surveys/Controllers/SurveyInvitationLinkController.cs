using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Filters;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.RateLimits;
using OrchardCore.ReCaptcha.Services;

namespace CrestApps.OrchardCore.Surveys.Controllers;

/// <summary>
/// Opens a personal invitation link. Opening the link (GET) only marks the invitation opened and shows a page that
/// posts the start; the post starts or resumes the invited person's response. A mail scanner that follows the link
/// therefore uses up nothing, and the invitee resumes their response from any browser. A bare link made for no one
/// asks for the survey's CAPTCHA like a public link does.
/// </summary>
[Feature(SurveysConstants.Features.Invitations)]
[TypeFilter(typeof(SurveyAnonymitySecretUnreadableFilter))]
public sealed class SurveyInvitationLinkController : Controller
{
    private readonly ISurveyResponseService _responses;
    private readonly SurveyInvitationStore _invitationStore;
    private readonly SurveyStore _surveyStore;
    private readonly SurveyCollectorStore _collectorStore;
    private readonly SurveyRespondentIdentityFactory _identities;
    private readonly SurveyErrorMessages _messages;
    private readonly IServiceProvider _serviceProvider;
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
    /// <param name="serviceProvider">The services, for the optional CAPTCHA.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyInvitationLinkController(
        ISurveyResponseService responses,
        SurveyInvitationStore invitationStore,
        SurveyStore surveyStore,
        SurveyCollectorStore collectorStore,
        SurveyRespondentIdentityFactory identities,
        SurveyErrorMessages messages,
        IServiceProvider serviceProvider,
        IClock clock,
        IStringLocalizer<SurveyInvitationLinkController> stringLocalizer)
    {
        _responses = responses;
        _invitationStore = invitationStore;
        _surveyStore = surveyStore;
        _collectorStore = collectorStore;
        _identities = identities;
        _messages = messages;
        _serviceProvider = serviceProvider;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Opens an invitation link: marks the invitation opened and shows the page that posts the start.
    /// </summary>
    /// <param name="token">The invitation token.</param>
    /// <returns>The page, or a page that says why the link does not work.</returns>
    [HttpGet("s/i/{token}")]
    [RateLimitGroup(RespondRateLimitsStartup.RateLimitGroup)]
    public async Task<IActionResult> Open(string token)
    {
        var (invitation, survey, collector) = await FindAsync(token);

        if (survey is null || collector is null)
        {
            return NotFoundMessage();
        }

        if (invitation.Status is SurveyInvitationStatus.Pending or SurveyInvitationStatus.Sent)
        {
            // Applied to the copy the database has now (R8-M3): a send that is e-mailing this invitation commits its marks
            // (sent, reminded) right after the e-mail, and the open keeps them instead of writing back this older copy.
            invitation = await _invitationStore.UpdateCurrentAsync(invitation, current =>
            {
                if (current.Status is not (SurveyInvitationStatus.Pending or SurveyInvitationStatus.Sent))
                {
                    return false;
                }

                current.Status = SurveyInvitationStatus.Opened;

                return true;
            }, cancellationToken: HttpContext.RequestAborted);
        }

        if (invitation.ExpiresUtc is { } expires && _clock.UtcNow >= expires)
        {
            return Message(survey.Title, _messages.For(SurveyErrorCode.InvitationInvalid));
        }

        return View("Open", OpenModel(token, survey, invitation, message: null));
    }

    /// <summary>
    /// Starts the invited person's response, or resumes it when it is in progress.
    /// </summary>
    /// <param name="token">The invitation token.</param>
    /// <param name="acceptStatement">Whether the respondent accepted the survey's statement.</param>
    /// <returns>A redirect to the response, or a page that says why the link does not work.</returns>
    [HttpPost("s/i/{token}")]
    [ActionName(nameof(Open))]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(RespondRateLimitsStartup.RateLimitGroup)]
    public async Task<IActionResult> Start(string token, bool acceptStatement = false)
    {
        var (invitation, survey, collector) = await FindAsync(token);

        if (survey is null || collector is null)
        {
            return NotFoundMessage();
        }

        if (invitation.ExpiresUtc is { } expires && _clock.UtcNow >= expires)
        {
            return Message(survey.Title, _messages.For(SurveyErrorCode.InvitationInvalid));
        }

        var identity = _identities.Build(survey.ItemId);

        if (NeedsCaptcha(survey, invitation) && _serviceProvider.GetService<ReCaptchaService>() is { } captcha)
        {
            identity.CaptchaPassed = await captcha.ValidateCaptchaAsync((_, _) => { });
        }

        var result = await _responses.StartAsync(new StartResponseRequest
        {
            SurveyId = survey.ItemId,
            CollectorToken = collector.Token,
            InvitationToken = token,
            Identity = identity,
            Context = SurveyInvitationService.ContextOf(invitation),
            StatementAccepted = acceptStatement,
            // The invitation collector's language, when set, as on a public link; otherwise the visitor's.
            Culture = !string.IsNullOrWhiteSpace(collector.Culture) ? collector.Culture : System.Globalization.CultureInfo.CurrentUICulture.Name,
        });

        if (result.Error is SurveyErrorCode.CaptchaRequired or SurveyErrorCode.StatementNotAccepted)
        {
            return View("Open", OpenModel(token, survey, invitation, _messages.For(result.Error)));
        }

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

    private SurveyInvitationOpenViewModel OpenModel(string token, Survey survey, SurveyInvitation invitation, string message)
        => new()
        {
            Token = token,
            Title = survey.Title,
            NeedsCaptcha = NeedsCaptcha(survey, invitation) && _serviceProvider.GetService<ReCaptchaService>() is not null,
            Statement = string.IsNullOrWhiteSpace(survey.Access?.Statement) ? null : survey.Access.Statement,
            Message = message,
        };

    // A bare personal link made for no one proves nothing about who opens it, so it asks for the survey's CAPTCHA.
    private bool NeedsCaptcha(Survey survey, SurveyInvitation invitation)
        => survey.Access?.RequireCaptcha == true
            && User?.Identity?.IsAuthenticated != true
            && !CaptchaRule.HasRecipient(invitation);

    private async Task<(SurveyInvitation Invitation, Survey Survey, SurveyCollector Collector)> FindAsync(string token)
    {
        var invitation = string.IsNullOrWhiteSpace(token) || token.Length > 128
            ? null
            : await _invitationStore.FindByTokenHashAsync(SurveyInvitationTokens.Hash(token));
        var survey = invitation is null ? null : await _surveyStore.FindByIdAsync(invitation.SurveyId);
        var collector = invitation is null ? null : await _collectorStore.FindByIdAsync(invitation.CollectorId);

        return (invitation, survey, collector);
    }

    private ViewResult NotFoundMessage()
    {
        Response.StatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status404NotFound;

        return Message(S["Survey not found"], S["This link does not work. It may have been replaced by a newer one."]);
    }

    private ViewResult Message(string title, string message)
        => View("Message", new SurveyMessageViewModel { Title = title, Message = message });
}
