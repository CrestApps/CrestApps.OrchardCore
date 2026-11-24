using System.Security.Claims;

namespace CrestApps.OrchardCore.FileStorage;

/// <summary>
/// Decides who may read a file of an area, such as the person who uploaded it and the staff who review it. A file is
/// served only when the area has at least one policy and every policy of the area allows it.
/// </summary>
/// <typeparam name="TArea">The marker type of the area.</typeparam>
public interface IProtectedFileAccessPolicy<TArea>
{
    /// <summary>
    /// Decides whether a person may read a file.
    /// </summary>
    /// <param name="access">The file, what it belongs to and the person.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the person may read it.</returns>
    Task<bool> CanReadAsync(ProtectedFileAccess access, CancellationToken cancellationToken = default);
}

/// <summary>
/// A request to read a protected file.
/// </summary>
public sealed class ProtectedFileAccess
{
    /// <summary>
    /// Gets or sets the file.
    /// </summary>
    public string FileId { get; set; }

    /// <summary>
    /// Gets or sets what the file belongs to, such as the record it is the evidence of.
    /// </summary>
    public string ResourceId { get; set; }

    /// <summary>
    /// Gets or sets the person.
    /// </summary>
    public ClaimsPrincipal User { get; set; }
}

/// <summary>
/// Makes and reads the signed, time-limited links of an area's files, and opens a file for a person the area's
/// policies allow.
/// </summary>
/// <typeparam name="TArea">The marker type of the area.</typeparam>
public interface IProtectedFileLinks<TArea>
{
    /// <summary>
    /// Makes a token for a file, valid for a while.
    /// </summary>
    /// <param name="fileId">The file.</param>
    /// <param name="resourceId">What the file belongs to.</param>
    /// <param name="lifetime">How long the token is valid.</param>
    /// <returns>The token, safe in a URL.</returns>
    string CreateToken(string fileId, string resourceId, TimeSpan lifetime);

    /// <summary>
    /// Opens the file of a token for a person, when the token is valid and every policy of the area allows it.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="user">The person.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The file and its stream, or <see langword="null"/> when it may not be read or no longer exists.</returns>
    Task<(ProtectedFile File, Stream Content)?> OpenAsync(string token, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}
