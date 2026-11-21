using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.PersonalData;

namespace CrestApps.OrchardCore.Learning.Credentials.Services;

/// <summary>
/// The person's credential awards in a personal data export, with each credential and signed token as issued; on
/// erasure each award is revoked and its personal data removed.
/// </summary>
internal sealed class CredentialsPersonalDataContributor : IPersonalDataContributor
{
    internal const string AwardKind = "LearningCredentialAward";

    private readonly CredentialAwardStore _store;
    private readonly CredentialAwardEraser _eraser;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialsPersonalDataContributor"/> class.
    /// </summary>
    /// <param name="store">The award store.</param>
    /// <param name="eraser">The eraser.</param>
    public CredentialsPersonalDataContributor(CredentialAwardStore store, CredentialAwardEraser eraser)
    {
        _store = store;
        _eraser = eraser;
    }

    /// <inheritdoc/>
    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        foreach (var userId in context.Subject.UserIds)
        {
            foreach (var award in await _store.ListForUserAsync(userId, cancellationToken))
            {
                var record = context.Add(AwardKind, award.ItemId, award.Achievement?.Name, award.IssuedUtc);

                record.Fields["Credential"] = award.Achievement?.Name;
                record.Fields["AwardedBy"] = award.RuleKind;
                record.Fields["IssuedUtc"] = award.IssuedUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                record.Fields["ExpiresUtc"] = award.ExpiresUtc?.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                record.Fields["RevokedUtc"] = award.RevokedUtc?.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                record.Fields["ShowName"] = award.ShowName ? "true" : "false";
                record.Fields["OpenBadgeCredential"] = award.Credential;
                record.Fields["SignedCredential"] = award.Jwt;
            }
        }
    }

    /// <inheritdoc/>
    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        var erased = 0;

        foreach (var userId in context.Subject.UserIds)
        {
            var count = await _eraser.EraseAsync(userId, cancellationToken);

            for (var index = 0; index < count; index++)
            {
                context.Result.Count(AwardKind);
            }

            erased += count;
        }

        if (erased > 0)
        {
            context.Result.Warnings.Add("Each digital credential was revoked and its personal data removed; its slot in the revocation list stays revoked. Copies the person downloaded, added to a wallet or shared stay signed by this site; a verifier that checks the revocation list sees them revoked, but the copies themselves cannot be reached or deleted.");
        }
    }
}
