using CrestApps.OrchardCore.Learning.Credentials.Models;

namespace CrestApps.OrchardCore.Learning.Credentials.Services;

/// <summary>
/// Awards, revokes and reads digital credentials.
/// </summary>
public interface ICredentialAwardService
{
    /// <summary>
    /// Awards a credential: builds the Open Badges 3.0 credential and signs it. Idempotent per definition, rule kind,
    /// source id and recipient, under a lock; a person who holds a valid award of the definition gets no second one.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result, with the award that exists or was made.</returns>
    Task<CredentialAwardResult> AwardAsync(CredentialAwardRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes an award. The status list shows it at once; the award keeps its slot.
    /// </summary>
    /// <param name="awardId">The award id.</param>
    /// <param name="actor">The user id of the person who revokes it.</param>
    /// <param name="reason">Why it is revoked.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<CredentialAwardResult> RevokeAsync(string awardId, string actor, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an award by its id.
    /// </summary>
    /// <param name="awardId">The award id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The award, or <see langword="null"/>.</returns>
    Task<CredentialAward> FindByIdAsync(string awardId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an award by the code in its public addresses.
    /// </summary>
    /// <param name="publicId">The code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The award, or <see langword="null"/>.</returns>
    Task<CredentialAward> FindByPublicIdAsync(string publicId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a person's awards, newest first.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The awards.</returns>
    Task<IReadOnlyList<CredentialAward>> ListForUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets whether the recipient's name shows on the public verification page. Only the recipient may.
    /// </summary>
    /// <param name="awardId">The award id.</param>
    /// <param name="userId">The user id of the person asking, who must be the recipient.</param>
    /// <param name="showName">Whether the name shows.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the award is the person's and was changed.</returns>
    Task<bool> SetShowNameAsync(string awardId, string userId, bool showName, CancellationToken cancellationToken = default);
}
