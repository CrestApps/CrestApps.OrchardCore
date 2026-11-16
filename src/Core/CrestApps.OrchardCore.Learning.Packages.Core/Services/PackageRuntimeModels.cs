using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Services;

/// <summary>
/// Why a launch or a runtime call was refused.
/// </summary>
public static class PackageRuntimeErrors
{
    /// <summary>The item is not a published learning package of a course.</summary>
    public const string NotAPackage = "NotAPackage";

    /// <summary>No package was imported into the item yet.</summary>
    public const string NoPackageFile = "NoPackageFile";

    /// <summary>The learner has no enrollment in the item's course.</summary>
    public const string NotEnrolled = "NotEnrolled";

    /// <summary>The enrollment is not active (pending, suspended, withdrawn or expired).</summary>
    public const string NotActive = "NotActive";

    /// <summary>The learner's access window ended.</summary>
    public const string AccessEnded = "AccessEnded";

    /// <summary>The item's release rules keep it locked.</summary>
    public const string NotAvailableYet = "NotAvailableYet";

    /// <summary>The launch token is altered, expired, of another learner, or of another enrollment or package.</summary>
    public const string InvalidToken = "InvalidToken";

    /// <summary>A newer launch of the package replaced this one, or this one finished.</summary>
    public const string Superseded = "Superseded";

    /// <summary>Another write to the same runtime state held the lock.</summary>
    public const string Busy = "Busy";

    /// <summary>The request has more changes than one call may carry.</summary>
    public const string TooManyChanges = "TooManyChanges";
}

/// <summary>
/// A launch, or why it was refused.
/// </summary>
public sealed class PackageLaunchResult
{
    /// <summary>Gets or sets the error, one of <see cref="PackageRuntimeErrors"/>, or <see langword="null"/>.</summary>
    public string Error { get; set; }

    /// <summary>Gets or sets the launch token.</summary>
    public string Token { get; set; }

    /// <summary>Gets or sets the launch.</summary>
    public PackageLaunch Launch { get; set; }

    /// <summary>Gets or sets the package part.</summary>
    public LearningPackagePart Part { get; set; }

    /// <summary>Gets or sets the runtime state after the launch.</summary>
    public PackageRuntimeState State { get; set; }

    /// <summary>Gets or sets whether the launch is a review of a completed enrollment, whose data is not kept.</summary>
    public bool Review { get; set; }

    /// <summary>Gets whether the package was launched.</summary>
    public bool Succeeded => Error is null;

    internal static PackageLaunchResult Fail(string error)
        => new() { Error = error };
}

/// <summary>
/// A value the content set, as the package frame sent it.
/// </summary>
/// <param name="Element">The element.</param>
/// <param name="Value">The value.</param>
public sealed record PackageRuntimeChange(string Element, string Value);

/// <summary>
/// The values a package frame set since its last call, sent by the player page with the learner's session.
/// </summary>
public sealed class PackageRuntimeRequest
{
    /// <summary>Gets or sets the signed-in learner.</summary>
    public string UserId { get; set; }

    /// <summary>Gets or sets the enrollment named in the request; it must be the token's.</summary>
    public string EnrollmentId { get; set; }

    /// <summary>Gets or sets the package item named in the request; it must be the token's.</summary>
    public string PackageItemId { get; set; }

    /// <summary>Gets or sets the launch token.</summary>
    public string Token { get; set; }

    /// <summary>Gets or sets the values, in the order the content set them.</summary>
    public IList<PackageRuntimeChange> Changes { get; set; } = [];

    /// <summary>Gets or sets whether the content called <c>LMSCommit</c>.</summary>
    public bool Commit { get; set; }

    /// <summary>Gets or sets whether the content called <c>LMSFinish</c>.</summary>
    public bool Finish { get; set; }
}

/// <summary>
/// A value the server refused, with the SCORM 1.2 error code.
/// </summary>
/// <param name="Index">The position of the change in the request.</param>
/// <param name="Element">The element.</param>
/// <param name="Code">The error code.</param>
public sealed record PackageRuntimeRefusal(int Index, string Element, string Code);

/// <summary>
/// What a runtime call did.
/// </summary>
public sealed class PackageRuntimeResult
{
    /// <summary>Gets or sets the error, one of <see cref="PackageRuntimeErrors"/>, or <see langword="null"/>.</summary>
    public string Error { get; set; }

    /// <summary>Gets or sets the values the server refused.</summary>
    public IReadOnlyList<PackageRuntimeRefusal> Refused { get; set; } = [];

    /// <summary>Gets or sets the runtime state after the call.</summary>
    public PackageRuntimeState State { get; set; }

    /// <summary>Gets or sets what Learning recorded, when the call committed or finished.</summary>
    public ProgressUpdate Progress { get; set; }

    /// <summary>Gets or sets whether the launch is a review, whose data is not kept.</summary>
    public bool Review { get; set; }

    /// <summary>Gets whether the call was taken.</summary>
    public bool Succeeded => Error is null;

    internal static PackageRuntimeResult Fail(string error)
        => new() { Error = error };
}

/// <summary>
/// A file of a package opened for a launch. Dispose it once it is sent.
/// </summary>
public sealed class PackageFileResult : IAsyncDisposable
{
    private readonly IAsyncDisposable _owner;

    internal PackageFileResult(Stream content, string contentType, string path, IAsyncDisposable owner)
    {
        Content = content;
        ContentType = contentType;
        Path = path;
        _owner = owner;
    }

    /// <summary>Gets the content.</summary>
    public Stream Content { get; }

    /// <summary>Gets the content type, from the file extension.</summary>
    public string ContentType { get; }

    /// <summary>Gets the path of the file in the package.</summary>
    public string Path { get; }

    /// <summary>Gets whether the file is an HTML page, which gets the run-time API script.</summary>
    public bool IsHtml => ContentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase);

    /// <summary>Gets or sets the launch.</summary>
    public PackageLaunch Launch { get; init; }

    /// <summary>Gets or sets the runtime state of the launch, for the data an HTML page starts with.</summary>
    public PackageRuntimeState State { get; init; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Content.DisposeAsync();

        if (_owner is not null)
        {
            await _owner.DisposeAsync();
        }
    }
}
