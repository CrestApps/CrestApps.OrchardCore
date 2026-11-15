using CrestApps.OrchardCore.Learning.Certifications.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Certifications.Services;

/// <summary>
/// A kind of certification requirement: whether a person met it, and who met it lately. Register one with
/// <c>services.AddCertificationRequirement&lt;T&gt;()</c>.
/// </summary>
public interface ICertificationRequirement
{
    /// <summary>
    /// Gets the kind, such as <c>CourseCompleted</c>.
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Gets the name shown in the path editor.
    /// </summary>
    LocalizedString DisplayName { get; }

    /// <summary>
    /// Finds what met the requirement for a person since a time.
    /// </summary>
    /// <param name="context">The person, the requirement and the time evidence counts from.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The evidence, or <see langword="null"/> when the requirement is not met.</returns>
    Task<CertificationEvidence> FindEvidenceAsync(CertificationRequirementContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the people who met the requirement since a time, so the upkeep catches up on events that were lost.
    /// </summary>
    /// <param name="requirement">The requirement.</param>
    /// <param name="sinceUtc">The earliest time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user ids.</returns>
    Task<IReadOnlyCollection<string>> ListUsersSinceAsync(CertificationRequirement requirement, DateTime sinceUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the title of what a requirement is about, such as the course title.
    /// </summary>
    /// <param name="requirement">The requirement.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The title, or <see langword="null"/>.</returns>
    Task<string> GetTitleAsync(CertificationRequirement requirement, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a requirement is checked for.
/// </summary>
public sealed class CertificationRequirementContext
{
    /// <summary>
    /// Gets or sets the person.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the requirement.
    /// </summary>
    public CertificationRequirement Requirement { get; set; }

    /// <summary>
    /// Gets or sets the earliest time evidence counts from, or <see langword="null"/> for any time.
    /// </summary>
    public DateTime? SinceUtc { get; set; }
}
