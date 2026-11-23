namespace CrestApps.OrchardCore.Banking.Checks.Models;

/// <summary>
/// A check that was just written, as the <see cref="Services.ICheckWriteHandler"/>s see it after it is saved in the
/// unit of work.
/// </summary>
public sealed class CheckWrittenContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckWrittenContext"/> class.
    /// </summary>
    /// <param name="check">The new check.</param>
    /// <param name="request">The request it was written from, as the handlers left it.</param>
    /// <param name="reissueOf">The check a reissue replaces, or <see langword="null"/>.</param>
    public CheckWrittenContext(CheckRecord check, CheckIssueRequest request, CheckRecord reissueOf = null)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(request);

        Check = check;
        Request = request;
        ReissueOf = reissueOf;
    }

    /// <summary>
    /// Gets the new check, saved with its id. Save any change to it through <see cref="Services.ICheckManager"/>.
    /// </summary>
    public CheckRecord Check { get; }

    /// <summary>
    /// Gets the request the check was written from, as the handlers left it.
    /// </summary>
    public CheckIssueRequest Request { get; }

    /// <summary>
    /// Gets the check a reissue replaces, or <see langword="null"/> for a new check.
    /// </summary>
    public CheckRecord ReissueOf { get; }
}
