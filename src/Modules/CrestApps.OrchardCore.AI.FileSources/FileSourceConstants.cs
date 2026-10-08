namespace CrestApps.OrchardCore.AI.FileSources;

/// <summary>
/// Names this feature and the folder every tenant's local file sources are confined to.
/// </summary>
public static class FileSourceConstants
{
    /// <summary>
    /// Feature ids.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The base File Sources feature, which ships the file-system connector.
        /// </summary>
        public const string FileSources = "CrestApps.OrchardCore.AI.FileSources";
    }

    /// <summary>
    /// The folder, under the tenant's own App_Data directory, that a local file source may read.
    /// </summary>
    /// <remarks>
    /// A tenant administrator configures a local file source through the admin UI, so without a boundary
    /// that screen is a way to read any file the host process can open -- including another tenant's. The
    /// boundary is one folder per tenant, computed from the tenant's own identity and never taken from
    /// configuration or from the request.
    /// </remarks>
    public const string TenantFolderName = "file-sources";
}
