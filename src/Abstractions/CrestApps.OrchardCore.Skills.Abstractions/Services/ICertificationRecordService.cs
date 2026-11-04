using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.Skills.Services;

/// <summary>
/// Records people's certificates, checks them and answers whether one is valid on a day.
/// </summary>
public interface ICertificationRecordService
{
    /// <summary>
    /// Finds a record.
    /// </summary>
    /// <param name="id">The record id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The record, or <see langword="null"/>.</returns>
    Task<CertificationRecord> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a new or changed record. The expiry date is worked out from the certification's validity when the record
    /// has an issue date and no expiry date of its own.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether it was saved, or why not.</returns>
    Task<SkillsResult> SaveAsync(CertificationRecord record, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a record as checked, so it counts.
    /// </summary>
    /// <param name="id">The record id.</param>
    /// <param name="actor">Who checked it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether it was saved, or why not.</returns>
    Task<SkillsResult> VerifyAsync(string id, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraws a record, so it no longer counts.
    /// </summary>
    /// <param name="id">The record id.</param>
    /// <param name="actor">Who withdrew it.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether it was saved, or why not.</returns>
    Task<SkillsResult> RevokeAsync(string id, string actor, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a record.
    /// </summary>
    /// <param name="id">The record id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when it existed.</returns>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a person's records.
    /// </summary>
    /// <param name="subject">The person.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The records, newest expiry first.</returns>
    Task<IReadOnlyList<CertificationRecord>> GetForSubjectAsync(SkillSubject subject, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether a person holds a valid certificate of a certification on a day.
    /// </summary>
    /// <param name="subject">The person.</param>
    /// <param name="certificationId">The certification.</param>
    /// <param name="date">The day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when one counts that day.</returns>
    Task<bool> IsValidOnAsync(SkillSubject subject, string certificationId, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a page of records.
    /// </summary>
    /// <param name="query">Which records.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many match, and the page, soonest expiry first.</returns>
    Task<(int Count, IReadOnlyList<CertificationRecord> Items)> PageAsync(CertificationRecordQuery query, int skip, int take, CancellationToken cancellationToken = default);
}
