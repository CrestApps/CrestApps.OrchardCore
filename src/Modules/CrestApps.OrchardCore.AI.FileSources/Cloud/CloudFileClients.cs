using System.Text;
using System.Text.Json;
using CrestApps.Core.AI.FileSources.FileTransfer;
using CrestApps.OrchardCore.AI.Core.Integrations;

namespace CrestApps.OrchardCore.AI.FileSources.Cloud;

/// <summary>
/// Reads files from a Google Drive folder: regular files as they are, and Google Docs, Sheets and Slides exported as
/// text. A file's path is its Drive id and name, so a renamed file keeps its id.
/// </summary>
public sealed class GoogleDriveFileClient : IRemoteFileClient
{
    private const string BaseUrl = "https://www.googleapis.com/drive/v3/";
    private const string FolderMimeType = "application/vnd.google-apps.folder";

    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleDriveFileClient"/> class.
    /// </summary>
    /// <param name="httpClient">A client that carries the connection's token.</param>
    public GoogleDriveFileClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc/>
    public async Task<RemoteListing> ListAsync(string rootPath, bool recursive, int maxItems, CancellationToken cancellationToken = default)
    {
        var files = new List<RemoteFile>();
        var folders = new Queue<string>([CloudPaths.Root(rootPath) ?? "root"]);
        var limit = maxItems > 0 ? maxItems : int.MaxValue;

        while (folders.Count > 0 && files.Count < limit)
        {
            var folder = folders.Dequeue();
            string pageToken = null;

            do
            {
                var query = Uri.EscapeDataString($"'{folder.Replace("'", "\\'", StringComparison.Ordinal)}' in parents and trashed = false");
                var url = $"{BaseUrl}files?q={query}&fields=nextPageToken,files(id,name,mimeType,size,modifiedTime)&pageSize=100&supportsAllDrives=true&includeItemsFromAllDrives=true"
                    + (pageToken is null ? string.Empty : $"&pageToken={Uri.EscapeDataString(pageToken)}");

                using var response = await _httpClient.GetAsync(url, cancellationToken);
                var json = await AIIntegrationHttp.ReadAsync(response, "Google Drive", cancellationToken);

                foreach (var item in json.GetProperty("files").EnumerateArray())
                {
                    var id = AIIntegrationHttp.String(item, "id");
                    var name = AIIntegrationHttp.String(item, "name");
                    var mimeType = AIIntegrationHttp.String(item, "mimeType");

                    if (mimeType == FolderMimeType)
                    {
                        if (recursive)
                        {
                            folders.Enqueue(id);
                        }

                        continue;
                    }

                    files.Add(new RemoteFile(
                        CloudPaths.Combine(id, ExportName(name, mimeType)),
                        long.TryParse(AIIntegrationHttp.String(item, "size"), out var size) ? size : null,
                        DateTimeOffset.TryParse(AIIntegrationHttp.String(item, "modifiedTime"), out var modified) ? modified : null));
                }

                pageToken = AIIntegrationHttp.String(json, "nextPageToken");
            }
            while (pageToken is not null && files.Count < limit);
        }

        return new RemoteListing(files.Take(limit).ToList(), folders.Count == 0);
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenReadAsync(string rootPath, string path, CancellationToken cancellationToken = default)
    {
        var (id, name) = CloudPaths.Split(path);

        // Exported files carry the extension of what they were exported to.
        var url = name.EndsWith(".gdoc.txt", StringComparison.Ordinal) || name.EndsWith(".gslides.txt", StringComparison.Ordinal)
            ? $"{BaseUrl}files/{Uri.EscapeDataString(id)}/export?mimeType=text/plain"
            : name.EndsWith(".gsheet.csv", StringComparison.Ordinal)
                ? $"{BaseUrl}files/{Uri.EscapeDataString(id)}/export?mimeType=text/csv"
                : $"{BaseUrl}files/{Uri.EscapeDataString(id)}?alt=media&supportsAllDrives=true";

        return await CloudPaths.DownloadAsync(_httpClient, url, "Google Drive", cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _httpClient.Dispose();

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Names a Google file by what it is read as: Docs and Slides as text, Sheets as CSV.
    /// </summary>
    /// <param name="name">The file's name.</param>
    /// <param name="mimeType">The file's type.</param>
    /// <returns>The name with the extension it is read as.</returns>
    public static string ExportName(string name, string mimeType)
        => mimeType switch
        {
            "application/vnd.google-apps.document" => name + ".gdoc.txt",
            "application/vnd.google-apps.presentation" => name + ".gslides.txt",
            "application/vnd.google-apps.spreadsheet" => name + ".gsheet.csv",
            _ => name,
        };
}

/// <summary>
/// Reads files from a SharePoint document library or a OneDrive folder through Microsoft Graph.
/// </summary>
public sealed class SharePointFileClient : IRemoteFileClient
{
    private const string BaseUrl = "https://graph.microsoft.com/v1.0/";

    private readonly HttpClient _httpClient;
    private readonly string _drivePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="SharePointFileClient"/> class.
    /// </summary>
    /// <param name="httpClient">A client that carries the connection's token.</param>
    /// <param name="driveId">The document library's drive id; empty reads the signed-in user's OneDrive.</param>
    public SharePointFileClient(HttpClient httpClient, string driveId)
    {
        _httpClient = httpClient;
        _drivePath = string.IsNullOrWhiteSpace(driveId) ? "me/drive" : $"drives/{Uri.EscapeDataString(driveId.Trim())}";
    }

    /// <inheritdoc/>
    public async Task<RemoteListing> ListAsync(string rootPath, bool recursive, int maxItems, CancellationToken cancellationToken = default)
    {
        var files = new List<RemoteFile>();
        var limit = maxItems > 0 ? maxItems : int.MaxValue;
        var root = CloudPaths.Root(rootPath);
        var folders = new Queue<string>([root is null ? $"{BaseUrl}{_drivePath}/root/children" : $"{BaseUrl}{_drivePath}/root:/{Uri.EscapeDataString(root).Replace("%2F", "/", StringComparison.Ordinal)}:/children"]);

        while (folders.Count > 0 && files.Count < limit)
        {
            string url = folders.Dequeue() + "?$select=id,name,size,lastModifiedDateTime,file,folder&$top=200";

            while (url is not null && files.Count < limit)
            {
                using var response = await _httpClient.GetAsync(url, cancellationToken);
                var json = await AIIntegrationHttp.ReadAsync(response, "Microsoft 365", cancellationToken);

                foreach (var item in json.GetProperty("value").EnumerateArray())
                {
                    var id = AIIntegrationHttp.String(item, "id");

                    if (item.TryGetProperty("folder", out _))
                    {
                        if (recursive)
                        {
                            folders.Enqueue($"{BaseUrl}{_drivePath}/items/{Uri.EscapeDataString(id)}/children");
                        }

                        continue;
                    }

                    files.Add(new RemoteFile(
                        CloudPaths.Combine(id, AIIntegrationHttp.String(item, "name")),
                        item.TryGetProperty("size", out var size) && size.TryGetInt64(out var bytes) ? bytes : null,
                        DateTimeOffset.TryParse(AIIntegrationHttp.String(item, "lastModifiedDateTime"), out var modified) ? modified : null));
                }

                url = AIIntegrationHttp.String(json, "@odata.nextLink");
            }
        }

        return new RemoteListing(files.Take(limit).ToList(), folders.Count == 0);
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenReadAsync(string rootPath, string path, CancellationToken cancellationToken = default)
    {
        var (id, _) = CloudPaths.Split(path);

        return await CloudPaths.DownloadAsync(_httpClient, $"{BaseUrl}{_drivePath}/items/{Uri.EscapeDataString(id)}/content", "Microsoft 365", cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _httpClient.Dispose();

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Reads Notion pages as Markdown: the pages of a database, or the sub-pages of a page, shared with the integration.
/// </summary>
public sealed class NotionFileClient : IRemoteFileClient
{
    private const string BaseUrl = "https://api.notion.com/v1/";

    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotionFileClient"/> class.
    /// </summary>
    /// <param name="httpClient">A client that carries the integration token and the Notion version.</param>
    public NotionFileClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc/>
    public async Task<RemoteListing> ListAsync(string rootPath, bool recursive, int maxItems, CancellationToken cancellationToken = default)
    {
        var root = CloudPaths.Root(rootPath) ?? throw new InvalidOperationException("Enter the Notion database or page id to read.");
        var limit = maxItems > 0 ? maxItems : int.MaxValue;
        var files = new List<RemoteFile>();

        // A database lists its pages; a page lists its sub-pages, and is read itself too.
        using var databaseResponse = await _httpClient.PostAsync($"{BaseUrl}databases/{Uri.EscapeDataString(root)}/query", new StringContent("{\"page_size\":100}", Encoding.UTF8, "application/json"), cancellationToken);

        if (databaseResponse.IsSuccessStatusCode)
        {
            var json = await AIIntegrationHttp.ReadAsync(databaseResponse, "Notion", cancellationToken);

            foreach (var page in json.GetProperty("results").EnumerateArray().Take(limit))
            {
                files.Add(new RemoteFile(
                    CloudPaths.Combine(AIIntegrationHttp.String(page, "id"), PageTitle(page) + ".md"),
                    null,
                    DateTimeOffset.TryParse(AIIntegrationHttp.String(page, "last_edited_time"), out var edited) ? edited : null));
            }

            return new RemoteListing(files, !json.TryGetProperty("has_more", out var more) || !more.GetBoolean());
        }

        files.Add(new RemoteFile(CloudPaths.Combine(root, "page.md")));

        using var childrenResponse = await _httpClient.GetAsync($"{BaseUrl}blocks/{Uri.EscapeDataString(root)}/children?page_size=100", cancellationToken);
        var children = await AIIntegrationHttp.ReadAsync(childrenResponse, "Notion", cancellationToken);

        foreach (var block in children.GetProperty("results").EnumerateArray())
        {
            if (AIIntegrationHttp.String(block, "type") == "child_page" && files.Count < limit)
            {
                var title = block.GetProperty("child_page").TryGetProperty("title", out var name) ? name.GetString() : "page";
                files.Add(new RemoteFile(CloudPaths.Combine(AIIntegrationHttp.String(block, "id"), title + ".md"), null, DateTimeOffset.TryParse(AIIntegrationHttp.String(block, "last_edited_time"), out var edited) ? edited : null));
            }
        }

        return new RemoteListing(files, true);
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenReadAsync(string rootPath, string path, CancellationToken cancellationToken = default)
    {
        var (id, name) = CloudPaths.Split(path);
        var markdown = new StringBuilder();
        markdown.Append("# ").AppendLine(Path.GetFileNameWithoutExtension(name)).AppendLine();
        string cursor = null;

        do
        {
            using var response = await _httpClient.GetAsync($"{BaseUrl}blocks/{Uri.EscapeDataString(id)}/children?page_size=100" + (cursor is null ? string.Empty : $"&start_cursor={Uri.EscapeDataString(cursor)}"), cancellationToken);
            var json = await AIIntegrationHttp.ReadAsync(response, "Notion", cancellationToken);

            foreach (var block in json.GetProperty("results").EnumerateArray())
            {
                var line = RenderBlock(block);

                if (line is not null)
                {
                    markdown.AppendLine(line);
                }
            }

            cursor = json.TryGetProperty("has_more", out var more) && more.GetBoolean() ? AIIntegrationHttp.String(json, "next_cursor") : null;
        }
        while (cursor is not null);

        return new MemoryStream(Encoding.UTF8.GetBytes(markdown.ToString()));
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _httpClient.Dispose();

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Writes a Notion block as a line of Markdown.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <returns>The line, or <see langword="null"/> for a block with no text.</returns>
    public static string RenderBlock(JsonElement block)
    {
        var type = AIIntegrationHttp.String(block, "type");

        if (type is null || !block.TryGetProperty(type, out var content) || content.ValueKind != JsonValueKind.Object || !content.TryGetProperty("rich_text", out var richText))
        {
            return null;
        }

        var text = string.Concat(richText.EnumerateArray().Select(part => AIIntegrationHttp.String(part, "plain_text")));

        return type switch
        {
            "heading_1" => "# " + text,
            "heading_2" => "## " + text,
            "heading_3" => "### " + text,
            "bulleted_list_item" => "- " + text,
            "numbered_list_item" => "1. " + text,
            "to_do" => (content.TryGetProperty("checked", out var done) && done.GetBoolean() ? "- [x] " : "- [ ] ") + text,
            "quote" => "> " + text,
            "code" => "```\n" + text + "\n```",
            _ => text,
        };
    }

    private static string PageTitle(JsonElement page)
    {
        if (page.TryGetProperty("properties", out var properties))
        {
            foreach (var property in properties.EnumerateObject())
            {
                if (AIIntegrationHttp.String(property.Value, "type") == "title" && property.Value.TryGetProperty("title", out var title))
                {
                    var text = string.Concat(title.EnumerateArray().Select(part => AIIntegrationHttp.String(part, "plain_text")));

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }
        }

        return "page";
    }
}

/// <summary>
/// How cloud file paths are written: the provider's id, a slash, and the file's name.
/// </summary>
public static class CloudPaths
{
    /// <summary>
    /// Joins an id and a name.
    /// </summary>
    /// <param name="id">The provider's id.</param>
    /// <param name="name">The name.</param>
    /// <returns>The path.</returns>
    public static string Combine(string id, string name)
        => $"{id}/{(name ?? "file").Replace('/', '-')}";

    /// <summary>
    /// Splits a path into its id and name.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The id and the name.</returns>
    public static (string Id, string Name) Split(string path)
    {
        var separator = (path ?? string.Empty).IndexOf('/');

        return separator > 0 ? (path[..separator], path[(separator + 1)..]) : (path, path);
    }

    /// <summary>
    /// Reads the configured root: empty or <c>/</c> is the top.
    /// </summary>
    /// <param name="rootPath">The root.</param>
    /// <returns>The root, or <see langword="null"/> for the top.</returns>
    public static string Root(string rootPath)
        => string.IsNullOrWhiteSpace(rootPath) || rootPath.Trim() == "/" ? null : rootPath.Trim().Trim('/');

    /// <summary>
    /// Downloads a file into memory.
    /// </summary>
    /// <param name="httpClient">The client.</param>
    /// <param name="url">The address.</param>
    /// <param name="provider">The provider's name, for the error.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The file.</returns>
    public static async Task<Stream> DownloadAsync(HttpClient httpClient, string url, string provider, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await AIIntegrationHttp.ReadAsync(response, provider, cancellationToken);
        }

        var memory = new MemoryStream();
        await response.Content.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;

        return memory;
    }
}
