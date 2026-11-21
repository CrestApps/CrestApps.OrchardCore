using CrestApps.OrchardCore.Learning.Credentials.Models;

namespace CrestApps.OrchardCore.Learning.Credentials.Services;

/// <summary>
/// The result of awarding or revoking a credential.
/// </summary>
public sealed class CredentialAwardResult
{
    /// <summary>
    /// Gets whether it succeeded.
    /// </summary>
    public bool Succeeded => string.IsNullOrEmpty(ErrorCode);

    /// <summary>
    /// Gets or sets whether a new award was made; <see langword="false"/> when an existing one was returned.
    /// </summary>
    public bool Created { get; set; }

    /// <summary>
    /// Gets or sets the award.
    /// </summary>
    public CredentialAward Award { get; set; }

    /// <summary>
    /// Gets or sets the error code (see <see cref="CredentialAwardErrors"/>), when it failed.
    /// </summary>
    public string ErrorCode { get; set; }

    /// <summary>
    /// Makes a failed result.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <returns>The result.</returns>
    public static CredentialAwardResult Fail(string errorCode)
        => new() { ErrorCode = errorCode };

    /// <summary>
    /// Makes a successful result.
    /// </summary>
    /// <param name="award">The award.</param>
    /// <param name="created">Whether it was made now.</param>
    /// <returns>The result.</returns>
    public static CredentialAwardResult Success(CredentialAward award, bool created)
        => new() { Award = award, Created = created };
}
