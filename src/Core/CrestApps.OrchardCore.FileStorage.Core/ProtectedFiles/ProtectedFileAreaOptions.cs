namespace CrestApps.OrchardCore.FileStorage.ProtectedFiles;

/// <summary>
/// Where an area keeps its files.
/// </summary>
/// <typeparam name="TArea">The marker type of the area.</typeparam>
public sealed class ProtectedFileAreaOptions<TArea>
{
    /// <summary>
    /// Gets or sets the folder under the tenant's App_Data.
    /// </summary>
    public string Folder { get; set; }
}
