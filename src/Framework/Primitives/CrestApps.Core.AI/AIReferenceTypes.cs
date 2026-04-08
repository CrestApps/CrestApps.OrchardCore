namespace CrestApps.Core.AI;

/// <summary>
/// Shared reference type constants used by document and data source indexing.
/// Values intentionally match the legacy Orchard Core constants so existing indexed data remains compatible.
/// </summary>
public static class AIReferenceTypes
{
    public static class Document
    {
        public const string Profile = "profile";

        public const string ProfileTemplate = "profile-template";

        public const string ChatInteraction = "chat-interaction";

        public const string ChatSession = "chat-session";
    }

    public static class DataSource
    {
        public const string Document = "Document";
    }
}
