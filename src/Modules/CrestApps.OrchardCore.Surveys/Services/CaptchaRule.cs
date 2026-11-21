using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Asks respondents who are not signed in to pass a CAPTCHA when the survey requires it. The runner checks the CAPTCHA
/// with Orchard Core ReCaptcha and passes the outcome in the respondent identity.
/// </summary>
public sealed class CaptchaRule : ISurveyAccessRule
{
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
            || context.Identity?.CaptchaPassed == true)
        {
            return Task.FromResult(SurveyAccessDecision.Allow);
        }

        return Task.FromResult(SurveyAccessDecision.Deny(SurveyErrorCode.CaptchaRequired));
    }
}
