namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Why a survey operation was refused. The module maps each code to a localized message.
/// </summary>
public enum SurveyErrorCode
{
    /// <summary>
    /// No error.
    /// </summary>
    None,

    /// <summary>
    /// The survey, collector or response does not exist.
    /// </summary>
    NotFound,

    /// <summary>
    /// The survey is not published or not open.
    /// </summary>
    NotOpen,

    /// <summary>
    /// The survey or collector is closed.
    /// </summary>
    Closed,

    /// <summary>
    /// The weekly schedule does not allow responses now.
    /// </summary>
    NotAvailableNow,

    /// <summary>
    /// The respondent is not in the survey's audience.
    /// </summary>
    NotInAudience,

    /// <summary>
    /// The respondent must sign in.
    /// </summary>
    SignInRequired,

    /// <summary>
    /// The respondent reached a limit.
    /// </summary>
    LimitReached,

    /// <summary>
    /// The respondent must wait before another response.
    /// </summary>
    CooldownActive,

    /// <summary>
    /// The survey or collector has all the responses it accepts.
    /// </summary>
    MaxResponsesReached,

    /// <summary>
    /// The collector needs a password.
    /// </summary>
    PasswordRequired,

    /// <summary>
    /// The password is wrong.
    /// </summary>
    PasswordInvalid,

    /// <summary>
    /// The respondent must pass a CAPTCHA.
    /// </summary>
    CaptchaRequired,

    /// <summary>
    /// The invitation does not exist or expired.
    /// </summary>
    InvitationInvalid,

    /// <summary>
    /// The invitation was already used.
    /// </summary>
    InvitationUsed,

    /// <summary>
    /// The respondent already has a response in progress.
    /// </summary>
    AlreadyInProgress,

    /// <summary>
    /// The deadline passed.
    /// </summary>
    DeadlinePassed,

    /// <summary>
    /// The caller does not own the response.
    /// </summary>
    NotOwner,

    /// <summary>
    /// The response changed meanwhile; reload and try again.
    /// </summary>
    Busy,

    /// <summary>
    /// The answers are not valid.
    /// </summary>
    InvalidAnswer,

    /// <summary>
    /// The quota for the respondent's group is full.
    /// </summary>
    QuotaFull,

    /// <summary>
    /// The response is not in a state that allows the operation.
    /// </summary>
    InvalidState,

    /// <summary>
    /// The survey definition is not valid.
    /// </summary>
    InvalidDefinition,

    /// <summary>
    /// The survey has responses and cannot be deleted.
    /// </summary>
    HasResponses,

    /// <summary>
    /// The survey requires a CAPTCHA, but no CAPTCHA service is turned on, so respondents who are not signed in can't
    /// start it.
    /// </summary>
    CaptchaUnavailable,

    /// <summary>
    /// The survey asks the respondent to accept a statement (such as an honor statement) before starting, and the start
    /// didn't accept it.
    /// </summary>
    StatementNotAccepted,

    /// <summary>
    /// Too many wrong passwords were entered; the respondent waits before trying again.
    /// </summary>
    PasswordLockedOut,
}
