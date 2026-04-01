using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrestApps.Mvc.Web.Services;

/// <summary>
/// Persists admin-managed configuration sections into <c>App_Data\appsettings.json</c>.
/// Keeping those writes in the same JSON file that <see cref="IConfiguration"/> loads with
/// <c>reloadOnChange: true</c> lets runtime settings updates survive restarts and refresh the
/// live configuration pipeline without introducing a separate settings store.
/// </summary>

public sealed class AppDataConfigurationFileService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,

    };

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public AppDataConfigurationFileService(string appDataPath)
    {

        FilePath = Path.Combine(appDataPath, "appsettings.json");
    }

    public string FilePath { get; }

    public async Task SaveSectionAsync<T>(string sectionPath, T settings)
    {

        await _writeLock.WaitAsync();

        try
        {
            var root = await ReadRootAsync();
            var sectionNode = JsonSerializer.SerializeToNode(settings, _jsonOptions) ?? new JsonObject();

            SetSectionNode(root, sectionPath, sectionNode);

            var directoryPath = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            await File.WriteAllTextAsync(FilePath, root.ToJsonString(_jsonOptions));

        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<JsonObject> ReadRootAsync()
    {
        if (!File.Exists(FilePath))
        {
            return [];

        }

        var json = await File.ReadAllTextAsync(FilePath);

        if (string.IsNullOrWhiteSpace(json))
        {
            return [];

        }

        return JsonNode.Parse(json) as JsonObject ?? [];
    }

    private static JsonNode GetSectionNode(JsonObject root, string sectionPath)
    {
        JsonNode current = root;

        foreach (var segment in GetSegments(sectionPath))
        {
            if (current is not JsonObject currentObject)
            {

                return null;
            }

            if (!TryGetPropertyName(currentObject, segment, out var propertyName))
            {

                return null;
            }

            current = currentObject[propertyName];
        }

        return current;
    }

    private static void SetSectionNode(JsonObject root, string sectionPath, JsonNode value)
    {
        var segments = GetSegments(sectionPath);
        var current = root;

        for (var i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];
            var propertyName = TryGetPropertyName(current, segment, out var existingName)
            ? existingName

            : segment;
            if (current[propertyName] is not JsonObject nextObject)

            {
                nextObject = [];
                current[propertyName] = nextObject;
            }

            current = nextObject;
        }

        var lastSegment = segments[^1];
        if (TryGetPropertyName(current, lastSegment, out var lastPropertyName))

        {
            current[lastPropertyName] = value;

            return;
        }

        current[lastSegment] = value;
    }

    private static string[] GetSegments(string sectionPath) =>
    sectionPath.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool TryGetPropertyName(JsonObject obj, string segment, out string propertyName)
    {
        foreach (var property in obj)
        {

            if (string.Equals(property.Key, segment, StringComparison.OrdinalIgnoreCase))
            {
                propertyName = property.Key;
                return true;
            }
        }

        propertyName = null;
        return false;
    }
}
