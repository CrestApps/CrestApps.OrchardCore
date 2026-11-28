using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AI.Agent.Queries;

/// <summary>
/// One page of the rows a query returned, as a tool returns it.
/// </summary>
internal sealed class QueryResultPage
{
    /// <summary>
    /// Gets the rows of the page.
    /// </summary>
    public List<JsonNode> Items { get; } = [];

    /// <summary>
    /// Gets or sets the number of rows the query returned.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the page holds fewer rows than asked for, because the size limit of a
    /// result was reached.
    /// </summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a long value or a long list was shortened.
    /// </summary>
    public bool ValuesShortened { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a value was left out because its name says it is a credential.
    /// </summary>
    public bool ValuesRedacted { get; set; }

    /// <summary>
    /// Gets the notes a tool adds to its result for what was cut or left out.
    /// </summary>
    /// <returns>The notes, or <see langword="null"/> when the page is complete.</returns>
    public List<string> GetNotes()
    {
        var notes = new List<string>();

        if (Truncated)
        {
            notes.Add("The page holds fewer rows than asked for, because a result is limited in size. Ask for the next page, or select fewer columns.");
        }

        if (ValuesShortened)
        {
            notes.Add($"Values longer than {QueryResultRows.MaxValueLength} characters and long lists were shortened.");
        }

        if (ValuesRedacted)
        {
            notes.Add($"Values whose name says they are a credential (a password, a hash, a token, a key) were replaced with {QueryResultRows.Redacted}.");
        }

        return notes.Count > 0
            ? notes
            : null;
    }
}
