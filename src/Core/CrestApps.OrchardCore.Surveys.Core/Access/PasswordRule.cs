using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Identity;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Asks for the collector's password (an access code) when it has one. Wrong passwords are limited with
/// <see cref="SecretAttemptLimiter"/>: after five wrong tries within fifteen minutes, the respondent (the signed-in user,
/// or a visitor's IP address) waits fifteen minutes, so a password can't be guessed by trying. The CAPTCHA rule runs
/// first, so no try is made without solving it.
/// </summary>
public sealed class PasswordRule : ISurveyAccessRule
{
    /// <summary>
    /// The scope the wrong tries are counted under.
    /// </summary>
    public const string AttemptScope = "Surveys:Password";

    private static readonly PasswordHasher<SurveyCollector> _hasher = new();

    private readonly SecretAttemptLimiter _attempts;

    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordRule"/> class.
    /// </summary>
    /// <param name="attempts">The limit on wrong tries, or <see langword="null"/> for none.</param>
    public PasswordRule(SecretAttemptLimiter attempts = null)
    {
        _attempts = attempts;
    }

    /// <inheritdoc />
    public string Name => "Password";

    /// <inheritdoc />
    public int Order => SurveyAccessRuleOrder.Password;

    /// <inheritdoc />
    public async Task<SurveyAccessDecision> CheckStartAsync(SurveyAccessContext context)
    {
        // A check made before the respondent typed anything doesn't ask for the password: the start form does.
        if (context.IsTest || context.CheckOnly || string.IsNullOrEmpty(context.Collector?.PasswordHash))
        {
            return SurveyAccessDecision.Allow;
        }

        var subject = Subject(context.Identity);

        if (_attempts is not null && await _attempts.IsLockedOutAsync(AttemptScope, context.Collector.ItemId, subject))
        {
            return SurveyAccessDecision.Deny(SurveyErrorCode.PasswordLockedOut);
        }

        if (string.IsNullOrEmpty(context.Password))
        {
            return SurveyAccessDecision.Deny(SurveyErrorCode.PasswordRequired);
        }

        if (!Verify(context.Collector, context.Password))
        {
            if (_attempts is not null)
            {
                await _attempts.RecordFailureAsync(AttemptScope, context.Collector.ItemId, subject);
            }

            return SurveyAccessDecision.Deny(SurveyErrorCode.PasswordInvalid);
        }

        if (_attempts is not null)
        {
            await _attempts.ResetAsync(AttemptScope, context.Collector.ItemId, subject);
        }

        return SurveyAccessDecision.Allow;
    }

    // Who the wrong tries are counted for: the signed-in user, else the visitor's IP address. Visitors without either
    // share one count per collector, which can only lock them out sooner, never let more guesses through.
    private static string Subject(SurveyRespondentIdentity identity)
        => !string.IsNullOrEmpty(identity?.UserId)
            ? "user:" + identity.UserId
            : !string.IsNullOrEmpty(identity?.IpAddress) ? "ip:" + identity.IpAddress : "unknown";

    /// <summary>
    /// Hashes a collector password.
    /// </summary>
    /// <param name="collector">The collector.</param>
    /// <param name="password">The password.</param>
    /// <returns>The hash.</returns>
    public static string HashPassword(SurveyCollector collector, string password)
        => _hasher.HashPassword(collector, password);

    /// <summary>
    /// Verifies a collector password.
    /// </summary>
    /// <param name="collector">The collector.</param>
    /// <param name="password">The password entered.</param>
    /// <returns><see langword="true"/> when it matches.</returns>
    public static bool Verify(SurveyCollector collector, string password)
        => !string.IsNullOrEmpty(collector?.PasswordHash) && _hasher.VerifyHashedPassword(collector, collector.PasswordHash, password ?? string.Empty) != PasswordVerificationResult.Failed;
}
