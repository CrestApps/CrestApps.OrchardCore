using System.Security.Cryptography;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Keeps each survey's anonymity secret, the key that turns a respondent into a pseudonymous key and signs personal
/// invitation links. The survey document holds the secret only as ASP.NET Data Protection ciphertext, whose keys live
/// outside the database, so reading the database is not enough to recompute a respondent's key. There is no fallback
/// to the survey id: a survey without a secret gets a new one.
/// </summary>
public sealed class SurveyAnonymitySecrets
{
    /// <summary>
    /// The Data Protection purpose of the secrets.
    /// </summary>
    public const string Purpose = "CrestApps.OrchardCore.Surveys.AnonymitySecret.v1";

    private const string Prefix = "dp1:";

    private readonly IDataProtector _protector;
    private readonly SurveyStore _surveyStore;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyAnonymitySecrets"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The Data Protection provider.</param>
    /// <param name="surveyStore">The survey store, to save a secret made on first use.</param>
    /// <param name="logger">The logger.</param>
    public SurveyAnonymitySecrets(
        IDataProtectionProvider dataProtectionProvider,
        SurveyStore surveyStore,
        ILogger<SurveyAnonymitySecrets> logger)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
        _surveyStore = surveyStore;
        _logger = logger;
    }

    /// <summary>
    /// Gets whether a stored secret is protected.
    /// </summary>
    /// <param name="stored">The stored value.</param>
    /// <returns><see langword="true"/> when it is Data Protection ciphertext.</returns>
    public static bool IsProtected(string stored)
        => stored?.StartsWith(Prefix, StringComparison.Ordinal) == true;

    /// <summary>
    /// Gives a survey a new random secret, protected. The caller saves the survey.
    /// </summary>
    /// <param name="survey">The survey.</param>
    public void Assign(Survey survey)
    {
        ArgumentNullException.ThrowIfNull(survey);

        survey.AnonymitySecret = Protect(SurveyNames.NewSecret());
    }

    /// <summary>
    /// Protects a survey's secret in place when it is still stored in plain text (surveys saved before secrets were
    /// protected), keeping its value so respondent keys and invitation links stay valid, or gives it one when it has
    /// none. The caller saves the survey.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <returns><see langword="true"/> when the survey changed.</returns>
    public bool EnsureProtected(Survey survey)
    {
        ArgumentNullException.ThrowIfNull(survey);

        if (IsProtected(survey.AnonymitySecret))
        {
            return false;
        }

        survey.AnonymitySecret = Protect(string.IsNullOrEmpty(survey.AnonymitySecret) ? SurveyNames.NewSecret() : survey.AnonymitySecret);

        return true;
    }

    /// <summary>
    /// Gets a survey's secret in plain text. A survey without one gets a new secret, saved.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The secret.</returns>
    public async Task<string> RevealAsync(Survey survey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);

        var stored = survey.AnonymitySecret;

        if (string.IsNullOrEmpty(stored))
        {
            return await ReplaceAsync(survey, cancellationToken);
        }

        if (!IsProtected(stored))
        {
            // Plain text from before secrets were protected: the upgrade step protects it.
            return stored;
        }

        try
        {
            return _protector.Unprotect(stored.Substring(Prefix.Length));
        }
        catch (CryptographicException exception)
        {
            // The Data Protection keys are missing on this node (a key ring that isn't persisted, nodes that don't share
            // keys, or a database restored on another host). Replacing the secret here would restart every limit, break
            // every invitation link and let nodes overwrite each other's secret, so the survey fails closed until the
            // keys come back or an administrator resets the secret.
            _logger.LogError(exception, "The anonymity secret of the survey '{SurveyId}' could not be read. Restore the Data Protection keys, or reset the secret on the survey's access and settings page.", survey.ItemId);

            throw new SurveyAnonymitySecretUnreadableException("The anonymity secret of the survey could not be read.", exception)
            {
                SurveyId = survey.ItemId,
            };
        }
    }

    /// <summary>
    /// Gets whether a survey's secret can be read on this node. A survey without a secret, or with one still stored in
    /// plain text, can.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <returns><see langword="false"/> when the secret is protected with Data Protection keys this node doesn't have.</returns>
    public bool CanReveal(Survey survey)
    {
        ArgumentNullException.ThrowIfNull(survey);

        if (!IsProtected(survey.AnonymitySecret))
        {
            return true;
        }

        try
        {
            _protector.Unprotect(survey.AnonymitySecret.Substring(Prefix.Length));

            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>
    /// Replaces a survey's secret with a new one and saves the survey. This is the explicit administrator action for a
    /// secret that can't be read any more: once-per-person limits start over, invitation links already sent stop
    /// working and must be sent again, and anonymous responses started from an invitation can no longer mark it
    /// completed. The caller re-issues the invitation token hashes.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new secret.</returns>
    public Task<string> ResetAsync(Survey survey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);

        _logger.LogWarning("The anonymity secret of the survey '{SurveyId}' was reset.", survey.ItemId);

        return ReplaceAsync(survey, cancellationToken);
    }

    /// <summary>
    /// Gets the key limits count a respondent by. Only an anonymous survey reads its secret.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="identity">Who responds.</param>
    /// <param name="invitation">The invitation, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The key, or <see langword="null"/> when the respondent cannot be recognized.</returns>
    public async Task<string> RespondentKeyAsync(Survey survey, SurveyRespondentIdentity identity, SurveyInvitation invitation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);

        var secret = SurveyAnonymity.IsAnonymous(survey) ? await RevealAsync(survey, cancellationToken) : null;

        return SurveyRespondentKeys.For(survey, secret, identity, invitation);
    }

    /// <summary>
    /// Makes the token of a personal invitation link.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="invitationId">The id of the invitation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The URL-safe token.</returns>
    public async Task<string> InvitationTokenAsync(Survey survey, string invitationId, CancellationToken cancellationToken = default)
        => SurveyInvitationTokens.Create(await RevealAsync(survey, cancellationToken), invitationId);

    private string Protect(string secret)
        => Prefix + _protector.Protect(secret);

    private async Task<string> ReplaceAsync(Survey survey, CancellationToken cancellationToken)
    {
        var secret = SurveyNames.NewSecret();

        survey.AnonymitySecret = Protect(secret);
        await _surveyStore.UpdateAsync(survey, cancellationToken);

        return secret;
    }
}
