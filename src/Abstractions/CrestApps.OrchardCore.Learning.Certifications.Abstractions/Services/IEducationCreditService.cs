using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Ledgers;

namespace CrestApps.OrchardCore.Learning.Certifications.Services;

/// <summary>
/// Education credits: their types, the credits people earn (kept in a quantity ledger), and the external learning
/// people record for credits.
/// </summary>
public interface IEducationCreditService
{
    /// <summary>
    /// Lists the credit types.
    /// </summary>
    Task<IReadOnlyList<EducationCreditType>> ListTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a credit type.
    /// </summary>
    Task<EducationCreditType> FindTypeAsync(string typeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a credit type.
    /// </summary>
    /// <returns>The reasons it was refused; empty when it was saved.</returns>
    Task<IReadOnlyList<string>> SaveTypeAsync(EducationCreditType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a credit type; credits already earned stay in the ledger.
    /// </summary>
    Task<bool> DeleteTypeAsync(string typeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Awards credits once per source: awarding again for the same source, record and type changes nothing.
    /// </summary>
    Task<QuantityLedgerPostResult> AwardAsync(EducationCreditAwardRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the credits of a person per type on a day, leaving out credits that expired.
    /// </summary>
    Task<IReadOnlyDictionary<string, decimal>> GetBalancesAsync(string userId, DateOnly asOf, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sums the credits of a type a person earned in a period.
    /// </summary>
    Task<decimal> SumAsync(string userId, string typeId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes back the credits a record earned a person, such as an enrollment found wrong on review: each credit entry
    /// it posted gets one reversing entry on the same days, once.
    /// </summary>
    /// <param name="userId">The person.</param>
    /// <param name="sourceRef">The record whose credits are taken back.</param>
    /// <param name="actor">Who took them back.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many entries were reversed.</returns>
    Task<int> ReverseAsync(string userId, string sourceRef, string actor, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the credits of a person a record earned and that were not taken back yet.
    /// </summary>
    Task<IReadOnlyList<QuantityLedgerEntry>> ListReversibleAsync(string userId, string sourceRef, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the credit entries of a person.
    /// </summary>
    Task<IReadOnlyList<QuantityLedgerEntry>> ListEntriesAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records external learning, waiting for a review.
    /// </summary>
    /// <returns>The reasons it was refused; empty when it was saved.</returns>
    Task<IReadOnlyList<string>> SubmitExternalAsync(ExternalLearningSubmission submission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts external learning and posts its credits, once. Nobody reviews their own learning.
    /// </summary>
    /// <param name="submissionId">The external learning.</param>
    /// <param name="reviewerUserId">The user id of the reviewer; refused when it is the person who did the learning.</param>
    /// <param name="actor">The name kept as the reviewer.</param>
    /// <param name="note">A note kept with the review.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<ExternalLearningReviewResult> AcceptExternalAsync(string submissionId, string reviewerUserId, string actor, string note, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects external learning. It is refused once credits were posted for it, and nobody reviews their own learning.
    /// </summary>
    /// <param name="submissionId">The external learning.</param>
    /// <param name="reviewerUserId">The user id of the reviewer; refused when it is the person who did the learning.</param>
    /// <param name="actor">The name kept as the reviewer.</param>
    /// <param name="note">A note kept with the review.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<ExternalLearningReviewResult> RejectExternalAsync(string submissionId, string reviewerUserId, string actor, string note, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds external learning.
    /// </summary>
    Task<ExternalLearningSubmission> FindExternalAsync(string submissionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the external learning of a person.
    /// </summary>
    Task<IReadOnlyList<ExternalLearningSubmission>> ListExternalForUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pages external learning, newest first.
    /// </summary>
    Task<(int Count, IReadOnlyList<ExternalLearningSubmission> Items)> PageExternalAsync(ExternalLearningState? state, int skip, int take, CancellationToken cancellationToken = default);
}

/// <summary>
/// Credits to award.
/// </summary>
public sealed class EducationCreditAwardRequest
{
    /// <summary>
    /// Gets or sets the person.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the credit type.
    /// </summary>
    public string CreditTypeId { get; set; }

    /// <summary>
    /// Gets or sets the amount.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the day the credits were earned.
    /// </summary>
    public DateOnly EarnedOn { get; set; }

    /// <summary>
    /// Gets or sets the day they stop counting; <see langword="null"/> for good.
    /// </summary>
    public DateOnly? ExpiresOn { get; set; }

    /// <summary>
    /// Gets or sets what awarded them, such as <c>Learning.Course</c>.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the record in <see cref="Source"/>, such as the enrollment.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets who awarded them.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets a note, such as the title of the course.
    /// </summary>
    public string Note { get; set; }
}

/// <summary>
/// Hears that credits are awarded and external learning is recorded. Register one with
/// <c>services.AddEducationCreditObserver&lt;T&gt;()</c>.
/// </summary>
public interface IEducationCreditObserver
{
    /// <summary>
    /// Called after credits are awarded the first time for a source.
    /// </summary>
    Task AwardedAsync(QuantityLedgerEntry entry, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called after external learning is recorded.
    /// </summary>
    Task SubmittedAsync(ExternalLearningSubmission submission, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called after external learning is accepted or rejected.
    /// </summary>
    Task ReviewedAsync(ExternalLearningSubmission submission, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
