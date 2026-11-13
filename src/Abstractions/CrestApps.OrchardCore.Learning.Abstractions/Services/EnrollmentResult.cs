using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// The outcome of an enrollment request.
/// </summary>
public sealed class EnrollmentResult
{
    /// <summary>
    /// Gets a value indicating whether the request succeeded.
    /// </summary>
    public bool Succeeded => string.IsNullOrEmpty(ErrorCode);

    /// <summary>
    /// Gets or sets the enrollment created or changed.
    /// </summary>
    public Enrollment Enrollment { get; set; }

    /// <summary>
    /// Gets or sets whether a new enrollment was created, rather than a grant added to an open one.
    /// </summary>
    public bool Created { get; set; }

    /// <summary>
    /// Gets or sets whether the request changed anything. A repeated grant changes nothing.
    /// </summary>
    public bool Changed { get; set; }

    /// <summary>
    /// Gets or sets the reason the request failed, one of <see cref="EnrollmentErrors"/>.
    /// </summary>
    public string ErrorCode { get; set; }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="errorCode">The reason.</param>
    /// <returns>The result.</returns>
    public static EnrollmentResult Fail(string errorCode)
        => new() { ErrorCode = errorCode };
}
