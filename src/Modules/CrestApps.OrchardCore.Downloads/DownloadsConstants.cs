namespace CrestApps.OrchardCore.Downloads;

public static class DownloadsConstants
{
    public const string Feature = "CrestApps.OrchardCore.Downloads";

    /// <summary>
    /// The YesSql collection the download entitlements are kept in.
    /// </summary>
    public const string CollectionName = "Downloads";

    public const string SettingsGroupId = "downloads";

    /// <summary>
    /// The folder under the tenant's App_Data the files are kept in, out of reach of any URL.
    /// </summary>
    public const string StoreFolder = "DigitalFiles";

    /// <summary>
    /// The largest file that can be uploaded: 1 GB.
    /// </summary>
    public const long MaxUploadBytes = 1L * 1024 * 1024 * 1024;

    public static class Parts
    {
        public const string DigitalFilesPart = nameof(DigitalFilesPart);
    }

    public static class RouteNames
    {
        public const string Download = "DigitalDownload";
    }
}
