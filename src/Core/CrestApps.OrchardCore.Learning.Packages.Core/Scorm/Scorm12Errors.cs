namespace CrestApps.OrchardCore.Learning.Packages.Core.Scorm;

/// <summary>
/// The error codes of the SCORM 1.2 run-time API and their texts.
/// </summary>
public static class Scorm12Errors
{
    /// <summary>No error.</summary>
    public const string NoError = "0";

    /// <summary>General exception, such as initializing twice.</summary>
    public const string GeneralException = "101";

    /// <summary>Invalid argument: an unknown element, an array index out of range, or a parameter that must be empty.</summary>
    public const string InvalidArgument = "201";

    /// <summary>The element cannot have children.</summary>
    public const string CannotHaveChildren = "202";

    /// <summary>The element is not an array, so it cannot have a count.</summary>
    public const string NotAnArray = "203";

    /// <summary>The API is not initialized, or already finished.</summary>
    public const string NotInitialized = "301";

    /// <summary>The element is not implemented.</summary>
    public const string NotImplemented = "401";

    /// <summary>A keyword (<c>_children</c>, <c>_count</c>, <c>_version</c>) cannot be set.</summary>
    public const string InvalidSetKeyword = "402";

    /// <summary>The element is read only.</summary>
    public const string ReadOnly = "403";

    /// <summary>The element is write only.</summary>
    public const string WriteOnly = "404";

    /// <summary>The value is not of the element's data type, vocabulary or range.</summary>
    public const string IncorrectDataType = "405";

    private static readonly Dictionary<string, string> _texts = new(StringComparer.Ordinal)
    {
        [NoError] = "No error",
        [GeneralException] = "General exception",
        [InvalidArgument] = "Invalid argument error",
        [CannotHaveChildren] = "Element cannot have children",
        [NotAnArray] = "Element not an array - cannot have count",
        [NotInitialized] = "Not initialized",
        [NotImplemented] = "Not implemented error",
        [InvalidSetKeyword] = "Invalid set value, element is a keyword",
        [ReadOnly] = "Element is read only",
        [WriteOnly] = "Element is write only",
        [IncorrectDataType] = "Incorrect Data Type",
    };

    /// <summary>
    /// Gets the text of an error code.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>The text, or an empty string for an unknown code.</returns>
    public static string GetText(string code)
        => code is not null && _texts.TryGetValue(code, out var text) ? text : string.Empty;
}
