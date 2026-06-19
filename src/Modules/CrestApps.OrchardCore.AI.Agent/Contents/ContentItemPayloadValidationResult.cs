namespace CrestApps.OrchardCore.AI.Agent.Contents;

internal sealed class ContentItemPayloadValidationResult
{
    public static readonly ContentItemPayloadValidationResult Success = new(true, [], [], null);

    public ContentItemPayloadValidationResult(
        bool isValid,
        IReadOnlyList<string> messages,
        IReadOnlyList<string> unmappedPaths,
        string guidance)
    {
        IsValid = isValid;
        Messages = messages;
        UnmappedPaths = unmappedPaths;
        Guidance = guidance;
    }

    public bool IsValid { get; }

    public IReadOnlyList<string> Messages { get; }

    public IReadOnlyList<string> UnmappedPaths { get; }

    public string Guidance { get; }
}
