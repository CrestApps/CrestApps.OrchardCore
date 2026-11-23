namespace CrestApps.OrchardCore.Banking.Checks.Models;

/// <summary>
/// A check about to be written, as the <see cref="Services.ICheckWriteHandler"/>s see it before anything is saved: each
/// one may change the request or refuse it.
/// </summary>
public sealed class CheckWritingContext
{
    private readonly List<string> _errors = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckWritingContext"/> class.
    /// </summary>
    /// <param name="request">What the check is written for.</param>
    /// <param name="reissueOf">The check a reissue replaces, or <see langword="null"/>.</param>
    public CheckWritingContext(CheckIssueRequest request, CheckRecord reissueOf = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        Request = request;
        ReissueOf = reissueOf;
    }

    /// <summary>
    /// Gets what the check is written for. A handler may change it, such as to give it the source kind and id of its
    /// own record, or the amount of its lines. Banking checks it after every handler was asked.
    /// </summary>
    public CheckIssueRequest Request { get; }

    /// <summary>
    /// Gets the check a reissue replaces, or <see langword="null"/> for a new check. The module that booked the payment
    /// of the replaced check moves it to the new check instead of booking it again.
    /// </summary>
    public CheckRecord ReissueOf { get; }

    /// <summary>
    /// Gets why the check was refused; empty while no handler refused it.
    /// </summary>
    public IReadOnlyList<string> Errors
        => _errors;

    /// <summary>
    /// Gets whether a handler refused the check.
    /// </summary>
    public bool IsRefused
        => _errors.Count > 0;

    /// <summary>
    /// Refuses the check: it is not written, and the reason is shown to the person who wrote it.
    /// </summary>
    /// <param name="error">Why, for the person who wrote the check.</param>
    public void Refuse(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        _errors.Add(error);
    }
}
