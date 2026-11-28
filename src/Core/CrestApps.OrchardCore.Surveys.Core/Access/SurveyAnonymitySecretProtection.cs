using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Protects a survey's anonymity secret with ASP.NET Data Protection. <see cref="SurveyAnonymitySecrets"/> and the
/// survey store share it, so every survey save stores the secret protected, whatever path saved it.
/// </summary>
internal static class SurveyAnonymitySecretProtection
{
    /// <summary>
    /// The prefix of a protected secret.
    /// </summary>
    public const string Prefix = "dp1:";

    /// <summary>
    /// Makes the Data Protection protector of the secrets.
    /// </summary>
    /// <param name="dataProtectionProvider">The Data Protection provider.</param>
    /// <returns>The protector.</returns>
    public static IDataProtector CreateProtector(IDataProtectionProvider dataProtectionProvider)
        => dataProtectionProvider.CreateProtector(SurveyAnonymitySecrets.Purpose);

    /// <summary>
    /// Gets whether a stored secret is protected.
    /// </summary>
    /// <param name="stored">The stored value.</param>
    /// <returns><see langword="true"/> when it is Data Protection ciphertext.</returns>
    public static bool IsProtected(string stored)
        => stored?.StartsWith(Prefix, StringComparison.Ordinal) == true;

    /// <summary>
    /// Protects a secret in plain text.
    /// </summary>
    /// <param name="protector">The protector.</param>
    /// <param name="secret">The secret.</param>
    /// <returns>The stored value.</returns>
    public static string Protect(IDataProtector protector, string secret)
        => Prefix + protector.Protect(secret);

    /// <summary>
    /// Protects a survey's secret in place when it is still stored in plain text, keeping its value, or gives it a new
    /// one when it has none. A secret already protected is left as it is, so this can run on every save.
    /// </summary>
    /// <param name="protector">The protector.</param>
    /// <param name="survey">The survey.</param>
    /// <returns><see langword="true"/> when the survey changed.</returns>
    public static bool EnsureProtected(IDataProtector protector, Survey survey)
    {
        if (IsProtected(survey.AnonymitySecret))
        {
            return false;
        }

        survey.AnonymitySecret = Protect(protector, string.IsNullOrEmpty(survey.AnonymitySecret) ? SurveyNames.NewSecret() : survey.AnonymitySecret);

        return true;
    }
}
