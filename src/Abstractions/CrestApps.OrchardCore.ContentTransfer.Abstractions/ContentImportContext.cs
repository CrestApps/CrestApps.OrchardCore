using System.Data;

namespace CrestApps.OrchardCore.ContentTransfer;

public sealed class ContentImportContext : ImportContentContext
{
    private readonly List<string> _errors = [];

    public DataColumnCollection Columns { get; set; }

    public DataRow Row { get; set; }

    /// <summary>
    /// Gets the reasons a handler gave for not importing the row. A row with a reason is recorded as failed and its
    /// content item is not saved.
    /// </summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>
    /// Gets the short name of the kind of reason the row failed, the same for every row that fails for it, such as
    /// "Not supported", which the import counts the failed rows by.
    /// </summary>
    public string ErrorCategory { get; private set; }

    /// <summary>
    /// Fails the row: the import records the reason and does not save the content item. A handler fails a row this way
    /// rather than by throwing, because a handler's exception is logged and the row goes on.
    /// </summary>
    /// <param name="error">Why the row cannot be imported, shown to the person who imported the file.</param>
    /// <param name="category">The short name of the kind of reason, or <see langword="null"/>.</param>
    public void Fail(string error, string category = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        _errors.Add(error);
        ErrorCategory ??= category;
    }
}
