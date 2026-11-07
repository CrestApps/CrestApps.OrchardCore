namespace CrestApps.OrchardCore.DataConnectors;

/// <summary>
/// The outcome of resolving a data connection a record references: the connection with its secrets, or why it cannot
/// be used.
/// </summary>
/// <param name="Context">The connection and its secrets, or <see langword="null"/> when <paramref name="Error"/> is set.</param>
/// <param name="Error">Why the connection cannot be used, or <see langword="null"/> when it can.</param>
public sealed record DataConnectionResolution(DataConnectorContext Context, string Error)
{
    /// <summary>
    /// Gets a value indicating whether the connection can be used.
    /// </summary>
    public bool Succeeded => Error is null;
}
