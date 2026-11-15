using CrestApps.OrchardCore.Learning.Certifications.Models;

namespace CrestApps.OrchardCore.Learning.Certifications.Services;

/// <summary>
/// Certification paths and the progress of each person on them: starting, checking the requirements and issuing the
/// Skills certificate, opening renewal and keeping every cycle.
/// </summary>
public interface ICertificationPathService
{
    /// <summary>
    /// Finds the path of a Skills certification.
    /// </summary>
    Task<CertificationPath> FindByCertificationAsync(string certificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a path.
    /// </summary>
    Task<CertificationPath> FindByIdAsync(string pathId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the enabled paths that have a requirement about a course or quiz.
    /// </summary>
    Task<IReadOnlyList<CertificationPath>> ListByTargetAsync(string targetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every path.
    /// </summary>
    Task<IReadOnlyList<CertificationPath>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a path.
    /// </summary>
    Task<CertificationPathResult> SaveAsync(CertificationPath path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a person on a path, or a new first cycle after a revocation or a lapse past the grace period, and checks
    /// the requirements at once.
    /// </summary>
    Task<CertificationPathResult> StartAsync(string pathId, string userId, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the requirements of a person's current cycle and issues (or renews) the certificate when they are met.
    /// Safe to call any number of times: one certificate per cycle.
    /// </summary>
    Task<CertificationPathResult> EvaluateAsync(string pathId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens renewal for a certified person whose window has come: a new cycle, new enrollments, and the observers.
    /// </summary>
    Task<CertificationPathResult> OpenRenewalAsync(string progressId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a person's progress on a path.
    /// </summary>
    Task<CertificationPathProgress> FindProgressAsync(string pathId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a person's progress on every path.
    /// </summary>
    Task<IReadOnlyList<CertificationPathProgress>> ListProgressForUserAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The result of a certification path change.
/// </summary>
public sealed class CertificationPathResult
{
    private readonly List<string> _errors = [];

    /// <summary>
    /// Gets the reasons the change was refused.
    /// </summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>
    /// Gets a value indicating whether the change was made.
    /// </summary>
    public bool Succeeded => _errors.Count == 0;

    /// <summary>
    /// Gets or sets the progress.
    /// </summary>
    public CertificationPathProgress Progress { get; set; }

    /// <summary>
    /// Gets or sets the id of the certificate this call issued or renewed.
    /// </summary>
    public string IssuedRecordId { get; set; }

    /// <summary>
    /// Records why the change was refused.
    /// </summary>
    /// <param name="error">The reason.</param>
    public void Fail(string error)
    {
        ArgumentException.ThrowIfNullOrEmpty(error);

        _errors.Add(error);
    }
}

/// <summary>
/// Hears about certification path progress, after the change is committed.
/// </summary>
public interface ICertificationPathObserver
{
    /// <summary>
    /// A person's renewal opened.
    /// </summary>
    Task RenewalOpenedAsync(CertificationPathProgress progress, CertificationPath path, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// A person's certificate lapsed.
    /// </summary>
    Task LapsedAsync(CertificationPathProgress progress, CertificationPath path, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
