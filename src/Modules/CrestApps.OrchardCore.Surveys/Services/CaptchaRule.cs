using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ReCaptcha.Services;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Asks respondents who are not signed in to pass a CAPTCHA when the survey requires it. The runner checks the CAPTCHA
/// with Orchard Core ReCaptcha and passes the outcome in the respondent identity. The rule is always registered and
/// fails closed: when the survey requires a CAPTCHA but the reCAPTCHA feature is off, respondents who are not signed in
/// are refused with <see cref="SurveyErrorCode.CaptchaUnavailable"/> instead of being let through. An invitation sent
/// to someone (a user, party, e-mail address or phone number) already proves who it was sent to, so invitees are not
/// asked; a bare personal link made for no one still asks.
/// </summary>
public sealed class CaptchaRule : ISurveyAccessRule
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="CaptchaRule"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider, which has the CAPTCHA service when reCAPTCHA is on.</param>
    public CaptchaRule(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public string Name => "Captcha";

    /// <inheritdoc />
    public int Order => SurveyAccessRuleOrder.Captcha;

    /// <inheritdoc />
    public Task<SurveyAccessDecision> CheckStartAsync(SurveyAccessContext context)
    {
        if (context.IsTest
            || context.Survey.Access?.RequireCaptcha != true
            || !string.IsNullOrEmpty(context.Identity?.UserId)
            || HasRecipient(context.Invitation)
            || context.Identity?.CaptchaPassed == true)
        {
            return Task.FromResult(SurveyAccessDecision.Allow);
        }

        return Task.FromResult(SurveyAccessDecision.Deny(IsAvailable(_serviceProvider) ? SurveyErrorCode.CaptchaRequired : SurveyErrorCode.CaptchaUnavailable));
    }

    /// <summary>
    /// Gets whether an invitation was sent to someone: a user, a party, an e-mail address or a phone number. A bare
    /// personal link made for no one proves nothing about who opens it, so it still asks for the CAPTCHA.
    /// </summary>
    /// <param name="invitation">The invitation, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the invitation names its recipient.</returns>
    public static bool HasRecipient(SurveyInvitation invitation)
        => invitation is not null
            && (invitation.Recipient is not null
                || !string.IsNullOrWhiteSpace(invitation.UserId)
                || !string.IsNullOrWhiteSpace(invitation.Email)
                || !string.IsNullOrWhiteSpace(invitation.Phone));

    /// <summary>
    /// Gets whether a CAPTCHA can be shown and checked: the reCAPTCHA feature is on.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns><see langword="true"/> when the CAPTCHA service is registered.</returns>
    public static bool IsAvailable(IServiceProvider serviceProvider)
        => serviceProvider?.GetService<ReCaptchaService>() is not null;
}
