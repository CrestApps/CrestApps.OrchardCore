using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Invitations;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using OrchardCore.RateLimits;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Controllers;

/// <summary>
/// The unsubscribe link of an invitation e-mail. Opening it (GET) changes nothing and shows a confirm button; the post
/// stops the invitation and its reminders and records the address as opted out of survey invitations through the
/// consent service, so no survey invites it again and every send that asks the consent service skips it.
/// </summary>
[Feature(SurveysConstants.Features.Invitations)]
public sealed class SurveyInvitationUnsubscribeController : Controller
{
    // A save that loses a concurrency race (another unsubscribe changed the opt-out list) is tried again this often.
    private const int Attempts = 3;

    private readonly SurveyInvitationStore _invitationStore;
    private readonly SurveyStore _surveyStore;
    private readonly ISession _session;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationUnsubscribeController"/> class.
    /// </summary>
    /// <param name="invitationStore">The invitation store.</param>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="session">The session of the request.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyInvitationUnsubscribeController(
        SurveyInvitationStore invitationStore,
        SurveyStore surveyStore,
        ISession session,
        IStringLocalizer<SurveyInvitationUnsubscribeController> stringLocalizer)
    {
        _invitationStore = invitationStore;
        _surveyStore = surveyStore;
        _session = session;
        S = stringLocalizer;
    }

    /// <summary>
    /// Shows the unsubscribe page of an invitation.
    /// </summary>
    /// <param name="token">The invitation token.</param>
    /// <returns>The page.</returns>
    [HttpGet("s/i/{token}/unsubscribe")]
    [RateLimitGroup(RespondRateLimitsStartup.RateLimitGroup)]
    public async Task<IActionResult> Index(string token)
    {
        var (invitation, survey) = await FindAsync(token);

        if (invitation is null || string.IsNullOrWhiteSpace(invitation.Email))
        {
            return NotFoundMessage();
        }

        return View(new SurveyInvitationUnsubscribeViewModel
        {
            Token = token,
            Title = survey?.Title,
            Email = invitation.Email,
            IsUnsubscribed = invitation.Status == SurveyInvitationStatus.OptedOut,
        });
    }

    /// <summary>
    /// Unsubscribes the invitation's address. Mail apps' one-click unsubscribe posts without a form token, so the link's
    /// own token is the proof.
    /// </summary>
    /// <param name="token">The invitation token.</param>
    /// <returns>The page.</returns>
    [HttpPost("s/i/{token}/unsubscribe")]
    [ActionName(nameof(Index))]
    [IgnoreAntiforgeryToken]
    [RateLimitGroup(RespondRateLimitsStartup.RateLimitGroup)]
    public async Task<IActionResult> IndexPost(string token)
    {
        var (invitation, survey) = await FindAsync(token);

        if (invitation is null || string.IsNullOrWhiteSpace(invitation.Email))
        {
            return NotFoundMessage();
        }

        // The opt-out list is one document saved with a concurrency check, so an unsubscribe at the same moment can make
        // this save fail. The failed session is spent (its transaction rolled back), so the work is done again in a
        // fresh scope that commits on its own.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (attempt == 1)
                {
                    await UnsubscribeAsync(HttpContext.RequestServices, invitation.ItemId, HttpContext.RequestAborted);
                    await _session.SaveChangesAsync(HttpContext.RequestAborted);
                }
                else
                {
                    await ShellScope.UsingChildScopeAsync(async scope =>
                    {
                        await UnsubscribeAsync(scope.ServiceProvider, invitation.ItemId, HttpContext.RequestAborted);
                        await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(HttpContext.RequestAborted);
                    });
                }

                break;
            }
            catch (ConcurrencyException) when (attempt < Attempts)
            {
            }
        }

        return View(new SurveyInvitationUnsubscribeViewModel
        {
            Token = token,
            Title = survey?.Title,
            Email = invitation.Email,
            IsUnsubscribed = true,
        });
    }

    private static async Task UnsubscribeAsync(IServiceProvider services, string invitationId, CancellationToken cancellationToken)
    {
        var invitationStore = services.GetRequiredService<SurveyInvitationStore>();
        var invitation = await invitationStore.FindByIdAsync(invitationId, cancellationToken);

        if (invitation is null || string.IsNullOrWhiteSpace(invitation.Email))
        {
            return;
        }

        if (invitation.Status != SurveyInvitationStatus.Completed && invitation.Status != SurveyInvitationStatus.OptedOut)
        {
            invitation.Status = SurveyInvitationStatus.OptedOut;
            await invitationStore.UpdateAsync(invitation, cancellationToken);
        }

        // Recorded through the consent service: the survey invitations source keeps the address, and the sources of
        // other modules (contacts, for one) record it too when they keep address opt-outs.
        await services.GetRequiredService<IConsentService>().RecordAsync(ConsentChannels.Email, invitation.Email.Trim(), new ConsentChange
        {
            OptedOut = true,
            Source = SurveyInvitationConsentSource.SourceName,
        }, cancellationToken);
    }

    private async Task<(SurveyInvitation Invitation, Survey Survey)> FindAsync(string token)
    {
        var invitation = string.IsNullOrWhiteSpace(token) || token.Length > 128
            ? null
            : await _invitationStore.FindByTokenHashAsync(SurveyInvitationTokens.Hash(token), HttpContext.RequestAborted);
        var survey = invitation is null ? null : await _surveyStore.FindByIdAsync(invitation.SurveyId, HttpContext.RequestAborted);

        return (invitation, survey);
    }

    private ViewResult NotFoundMessage()
    {
        Response.StatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status404NotFound;

        return View("~/Views/SurveyInvitationLink/Message.cshtml", new SurveyMessageViewModel
        {
            Title = S["Survey not found"],
            Message = S["This link does not work. It may have been replaced by a newer one."],
        });
    }
}
