namespace CrestApps.OrchardCore.SourceAccess;

/// <summary>
/// What a person wants to do with records another module wrote, such as the checks or deposits of a pay statement.
/// </summary>
public enum SourceAccessLevel
{
    /// <summary>
    /// See the records: lists, data sets and exports.
    /// </summary>
    View,

    /// <summary>
    /// Act on the records: print, void, release, cancel and the like.
    /// </summary>
    Manage,
}
