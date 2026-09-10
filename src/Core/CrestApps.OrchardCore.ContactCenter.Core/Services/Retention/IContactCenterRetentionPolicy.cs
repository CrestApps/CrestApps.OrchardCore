using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;

/// <summary>
/// Describes how one Contact Center table is aged out. Every high-volume table contributes a policy so the
/// retention service iterates policies instead of hard-coding a purge per table, and so a table added later
/// without a policy can be detected rather than silently growing forever.
/// </summary>
public interface IContactCenterRetentionPolicy
{
    /// <summary>
    /// Gets the stable technical name of the entity this policy purges. It is used for logging and for the
    /// coverage check that asserts no high-volume table lacks a policy.
    /// </summary>
    string EntityName { get; }

    /// <summary>
    /// Gets the index type whose table this policy purges. The coverage check matches registered policies to
    /// declared indexes through this property.
    /// </summary>
    Type IndexType { get; }

    /// <summary>
    /// Gets the model type this policy purges.
    /// </summary>
    Type ModelType { get; }

    /// <summary>
    /// Computes the cutoff before which this entity's records are eligible for purging.
    /// </summary>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <param name="options">The configured retention options.</param>
    /// <param name="cutoffUtc">
    /// When the method returns <see langword="true"/>, the UTC time before which records may be purged.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when purging is enabled for this entity; otherwise <see langword="false"/>
    /// because its records are kept indefinitely.
    /// </returns>
    bool TryGetCutoff(DateTime nowUtc, ContactCenterRetentionOptions options, out DateTime cutoffUtc);

    /// <summary>
    /// Gets the predicate this policy uses to select expired records. It is exposed so the retention coverage
    /// checks can verify what a policy considers expired without a database round trip, which is how a policy
    /// that purges purely by age and would therefore delete live records is caught.
    /// </summary>
    /// <param name="cutoffUtc">The exclusive UTC cutoff.</param>
    /// <returns>The expired-record predicate, typed against this policy's index.</returns>
    LambdaExpression GetExpiredPredicate(DateTime cutoffUtc);

    /// <summary>
    /// Deletes at most one batch of records older than the supplied cutoff.
    /// </summary>
    /// <param name="cutoffUtc">The exclusive UTC cutoff; only records older than this are eligible.</param>
    /// <param name="batchSize">The maximum number of records to delete in this batch.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of records deleted, which is zero once the entity has drained.</returns>
    Task<int> PurgeBatchAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken = default);
}
