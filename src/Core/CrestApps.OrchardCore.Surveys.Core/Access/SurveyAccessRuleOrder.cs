namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// The order the built-in access rules run in. Consumers pick an order between them.
/// </summary>
public static class SurveyAccessRuleOrder
{
    /// <summary>
    /// The status rule.
    /// </summary>
    public const int Status = 100;

    /// <summary>
    /// The availability window rule.
    /// </summary>
    public const int Window = 200;

    /// <summary>
    /// The weekly schedule rule.
    /// </summary>
    public const int Schedule = 300;

    /// <summary>
    /// The audience rule.
    /// </summary>
    public const int Audience = 400;

    /// <summary>
    /// The CAPTCHA rule. It runs before the statement and the password, so a password can't be guessed without solving
    /// the CAPTCHA.
    /// </summary>
    public const int Captcha = 450;

    /// <summary>
    /// The statement rule (a statement such as an honor statement the respondent accepts before starting).
    /// </summary>
    public const int Statement = 475;

    /// <summary>
    /// The password rule, with its limit on wrong tries.
    /// </summary>
    public const int Password = 500;

    /// <summary>
    /// The invitation rule.
    /// </summary>
    public const int Invitation = 700;

    /// <summary>
    /// The respondent limits rule.
    /// </summary>
    public const int Limits = 800;

    /// <summary>
    /// The maximum responses rule.
    /// </summary>
    public const int MaxResponses = 900;

    /// <summary>
    /// The quota rule.
    /// </summary>
    public const int Quota = 1000;
}
