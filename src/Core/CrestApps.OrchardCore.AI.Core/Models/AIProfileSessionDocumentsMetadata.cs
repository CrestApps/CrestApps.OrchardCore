namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Metadata stored on <see cref="AIProfile.Properties"/> to control
/// whether users can upload documents within AI Chat Sessions.
/// </summary>
public sealed class AIProfileSessionDocumentsMetadata
{
    /// <summary>
    /// Gets or sets whether users are allowed to upload and attach documents
    /// to chat sessions that use this profile.
    /// </summary>
    public bool AllowSessionDocuments { get; set; }
}
