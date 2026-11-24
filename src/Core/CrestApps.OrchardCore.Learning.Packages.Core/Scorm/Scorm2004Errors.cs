namespace CrestApps.OrchardCore.Learning.Packages.Core.Scorm;

/// <summary>
/// The error codes of the SCORM 2004 run-time API (RTE section 3.1.7) and their texts.
/// </summary>
public static class Scorm2004Errors
{
    /// <summary>No error.</summary>
    public const string NoError = "0";

    /// <summary>General exception.</summary>
    public const string GeneralException = "101";

    /// <summary>General initialization failure.</summary>
    public const string GeneralInitializationFailure = "102";

    /// <summary>Initialize after the session started.</summary>
    public const string AlreadyInitialized = "103";

    /// <summary>Initialize after the session terminated.</summary>
    public const string ContentInstanceTerminated = "104";

    /// <summary>General termination failure.</summary>
    public const string GeneralTerminationFailure = "111";

    /// <summary>Terminate before Initialize.</summary>
    public const string TerminationBeforeInitialization = "112";

    /// <summary>Terminate after Terminate.</summary>
    public const string TerminationAfterTermination = "113";

    /// <summary>GetValue before Initialize.</summary>
    public const string RetrieveDataBeforeInitialization = "122";

    /// <summary>GetValue after Terminate.</summary>
    public const string RetrieveDataAfterTermination = "123";

    /// <summary>SetValue before Initialize.</summary>
    public const string StoreDataBeforeInitialization = "132";

    /// <summary>SetValue after Terminate.</summary>
    public const string StoreDataAfterTermination = "133";

    /// <summary>Commit before Initialize.</summary>
    public const string CommitBeforeInitialization = "142";

    /// <summary>Commit after Terminate.</summary>
    public const string CommitAfterTermination = "143";

    /// <summary>General argument error: a parameter that must be empty was not.</summary>
    public const string GeneralArgumentError = "201";

    /// <summary>General get failure: an empty element, an index out of range, or a keyword the element does not have.</summary>
    public const string GeneralGetFailure = "301";

    /// <summary>General set failure: an empty element, an index out of range, or a value the data model refuses in its state.</summary>
    public const string GeneralSetFailure = "351";

    /// <summary>General commit failure.</summary>
    public const string GeneralCommitFailure = "391";

    /// <summary>The element is not in the data model.</summary>
    public const string UndefinedDataModelElement = "401";

    /// <summary>The element is in the data model but not implemented.</summary>
    public const string UnimplementedDataModelElement = "402";

    /// <summary>The element has no value yet.</summary>
    public const string ValueNotInitialized = "403";

    /// <summary>The element is read only.</summary>
    public const string ReadOnly = "404";

    /// <summary>The element is write only.</summary>
    public const string WriteOnly = "405";

    /// <summary>The value is not of the element's data type or vocabulary.</summary>
    public const string TypeMismatch = "406";

    /// <summary>The value is outside the element's range.</summary>
    public const string ValueOutOfRange = "407";

    /// <summary>The element needs another element set first, such as an id or an interaction type.</summary>
    public const string DependencyNotEstablished = "408";

    private static readonly Dictionary<string, string> _texts = new(StringComparer.Ordinal)
    {
        [NoError] = "No Error",
        [GeneralException] = "General Exception",
        [GeneralInitializationFailure] = "General Initialization Failure",
        [AlreadyInitialized] = "Already Initialized",
        [ContentInstanceTerminated] = "Content Instance Terminated",
        [GeneralTerminationFailure] = "General Termination Failure",
        [TerminationBeforeInitialization] = "Termination Before Initialization",
        [TerminationAfterTermination] = "Termination After Termination",
        [RetrieveDataBeforeInitialization] = "Retrieve Data Before Initialization",
        [RetrieveDataAfterTermination] = "Retrieve Data After Termination",
        [StoreDataBeforeInitialization] = "Store Data Before Initialization",
        [StoreDataAfterTermination] = "Store Data After Termination",
        [CommitBeforeInitialization] = "Commit Before Initialization",
        [CommitAfterTermination] = "Commit After Termination",
        [GeneralArgumentError] = "General Argument Error",
        [GeneralGetFailure] = "General Get Failure",
        [GeneralSetFailure] = "General Set Failure",
        [GeneralCommitFailure] = "General Commit Failure",
        [UndefinedDataModelElement] = "Undefined Data Model Element",
        [UnimplementedDataModelElement] = "Unimplemented Data Model Element",
        [ValueNotInitialized] = "Data Model Element Value Not Initialized",
        [ReadOnly] = "Data Model Element Is Read Only",
        [WriteOnly] = "Data Model Element Is Write Only",
        [TypeMismatch] = "Data Model Element Type Mismatch",
        [ValueOutOfRange] = "Data Model Element Value Out Of Range",
        [DependencyNotEstablished] = "Data Model Dependency Not Established",
    };

    /// <summary>
    /// Gets the text of an error code.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>The text, or an empty string for an unknown code.</returns>
    public static string GetText(string code)
        => code is not null && _texts.TryGetValue(code, out var text) ? text : string.Empty;
}
