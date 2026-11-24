namespace CrestApps.OrchardCore.Attachments;

/// <summary>
/// Names shared by the Attachments building block and the modules that build on it.
/// </summary>
public static class AttachmentsConstants
{
    /// <summary>
    /// The YesSql collection that holds attachment records.
    /// </summary>
    public const string CollectionName = "Attachments";

    /// <summary>
    /// The folder of the tenant's application data, or the blob container prefix, the files are kept in.
    /// </summary>
    public const string FolderName = "Attachments";

    /// <summary>
    /// The configuration section of the upload policy.
    /// </summary>
    public const string ConfigurationSection = "CrestApps:Attachments";

    /// <summary>
    /// The configuration section of the Azure Blob Storage options of the file store.
    /// </summary>
    public const string BlobStorageConfigurationSection = "CrestApps:Attachments:AzureBlob";

    /// <summary>
    /// The feature identifiers of the Attachments module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The Attachments feature.
        /// </summary>
        public const string Attachments = "CrestApps.OrchardCore.Attachments";
    }
}
