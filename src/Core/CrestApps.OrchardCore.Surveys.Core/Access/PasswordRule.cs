using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Identity;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Asks for the collector's password when it has one.
/// </summary>
public sealed class PasswordRule : ISurveyAccessRule
{
    private static readonly PasswordHasher<SurveyCollector> _hasher = new();

    /// <inheritdoc />
    public string Name => "Password";

    /// <inheritdoc />
    public int Order => SurveyAccessRuleOrder.Password;

    /// <inheritdoc />
    public Task<SurveyAccessDecision> CheckStartAsync(SurveyAccessContext context)
    {
        if (context.IsTest || string.IsNullOrEmpty(context.Collector?.PasswordHash))
        {
            return Task.FromResult(SurveyAccessDecision.Allow);
        }

        if (string.IsNullOrEmpty(context.Password))
        {
            return Task.FromResult(SurveyAccessDecision.Deny(SurveyErrorCode.PasswordRequired));
        }

        return Task.FromResult(Verify(context.Collector, context.Password)
            ? SurveyAccessDecision.Allow
            : SurveyAccessDecision.Deny(SurveyErrorCode.PasswordInvalid));
    }

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
