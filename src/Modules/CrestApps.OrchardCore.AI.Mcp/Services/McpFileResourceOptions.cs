namespace CrestApps.OrchardCore.AI.Mcp.Services;

/// <summary>
/// Limits for MCP "File" resources, bound from the <c>CrestApps:AI:Mcp:FileResources</c> configuration section.
/// </summary>
public sealed class McpFileResourceOptions
{
    /// <summary>
    /// The configuration section the options are bound from.
    /// </summary>
    public const string SectionName = "CrestApps:AI:Mcp:FileResources";

    /// <summary>
    /// Gets or sets the largest file, in bytes, a resource reads. A file is read into memory to answer, so a larger one
    /// is refused. The default is 10 MB.
    /// </summary>
    public long MaxBytes { get; set; } = 10 * 1024 * 1024;
}
