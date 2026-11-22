using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Turns survey error codes into messages respondents and staff read.
/// </summary>
public sealed class SurveyErrorMessages
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyErrorMessages"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyErrorMessages(IStringLocalizer<SurveyErrorMessages> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the message of an error code.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>The message.</returns>
    public string For(SurveyErrorCode error)
        => error switch
        {
            SurveyErrorCode.None => string.Empty,
            SurveyErrorCode.NotFound => S["This survey or response was not found, or you cannot open it."],
            SurveyErrorCode.NotOpen => S["This survey is not open yet."],
            SurveyErrorCode.Closed => S["This survey is closed."],
            SurveyErrorCode.NotAvailableNow => S["This survey is not available at this time. Try again later."],
            SurveyErrorCode.NotInAudience => S["This survey is not open to you."],
            SurveyErrorCode.SignInRequired => S["Sign in to respond to this survey."],
            SurveyErrorCode.LimitReached => S["You have given all the responses this survey allows."],
            SurveyErrorCode.CooldownActive => S["You must wait before you respond again."],
            SurveyErrorCode.MaxResponsesReached => S["This survey has all the responses it needs."],
            SurveyErrorCode.PasswordRequired => S["Enter the password to start this survey."],
            SurveyErrorCode.PasswordInvalid => S["The password is not correct."],
            SurveyErrorCode.CaptchaRequired => S["Confirm that you are not a robot."],
            SurveyErrorCode.InvitationInvalid => S["This invitation is not valid or has expired."],
            SurveyErrorCode.InvitationUsed => S["This invitation was already used."],
            SurveyErrorCode.AlreadyInProgress => S["You already have a response in progress."],
            SurveyErrorCode.DeadlinePassed => S["Your time is up. Your answers were submitted."],
            SurveyErrorCode.NotOwner => S["You cannot open this response."],
            SurveyErrorCode.Busy => S["Your response changed in another window. The page was reloaded."],
            SurveyErrorCode.InvalidAnswer => S["Correct the answers marked below."],
            SurveyErrorCode.QuotaFull => S["This survey has all the responses it needs from your group."],
            SurveyErrorCode.InvalidState => S["This response can no longer be changed."],
            SurveyErrorCode.InvalidDefinition => S["This survey has problems that must be fixed first."],
            SurveyErrorCode.HasResponses => S["This survey has responses and cannot be deleted. Archive it instead."],
            SurveyErrorCode.CaptchaUnavailable => S["This survey asks for a CAPTCHA, which this site can't show right now. Sign in to respond, or ask the site administrator to turn on reCAPTCHA."],
            _ => S["Something went wrong."],
        };
}
