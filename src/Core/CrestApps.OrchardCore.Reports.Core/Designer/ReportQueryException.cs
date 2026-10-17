namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Thrown when a designed query cannot run, listing every problem found.
/// </summary>
public sealed class ReportQueryException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportQueryException"/> class.
    /// </summary>
    public ReportQueryException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportQueryException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the problem.</param>
    public ReportQueryException(string message)
        : this([message])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportQueryException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the problem.</param>
    /// <param name="innerException">The exception that caused the problem.</param>
    public ReportQueryException(string message, Exception innerException)
        : base(message, innerException)
    {
        Errors = [message];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportQueryException"/> class.
    /// </summary>
    /// <param name="errors">The problems found.</param>
    public ReportQueryException(IReadOnlyList<string> errors)
        : base(string.Join(Environment.NewLine, errors ?? []))
    {
        Errors = errors ?? [];
    }

    /// <summary>
    /// Gets the problems found.
    /// </summary>
    public IReadOnlyList<string> Errors { get; } = [];
}
