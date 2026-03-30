using System.Text.Json;
using CrestApps.AI.Models;

namespace CrestApps.Mvc.Web.Services;

/// <summary>
/// Manages persistence of <see cref="InteractionDocumentSettings"/> in a JSON file stored under App_Data.
/// </summary>
public sealed class JsonFileInteractionDocumentSettingsService
{
    public const string SectionKey = "InteractionDocuments";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;

    public JsonFileInteractionDocumentSettingsService(string appDataPath)
    {
        _filePath = Path.Combine(appDataPath, "ai-document-settings.json");
    }

    public string FilePath => _filePath;

    public async Task<InteractionDocumentSettings> GetAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new InteractionDocumentSettings();
        }

        var json = await File.ReadAllTextAsync(_filePath);
        var wrapper = JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);

        if (TryGetSection(wrapper, out var section))
        {
            return section.Deserialize<InteractionDocumentSettings>(_jsonOptions) ?? new InteractionDocumentSettings();
        }

        return new InteractionDocumentSettings();
    }

    public async Task SaveAsync(InteractionDocumentSettings settings)
    {
        var sectionKey = JsonNamingPolicy.CamelCase.ConvertName(SectionKey);
        var wrapper = new Dictionary<string, InteractionDocumentSettings>
        {
            [sectionKey] = settings,
        };

        var json = JsonSerializer.Serialize(wrapper, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }

    private static bool TryGetSection(JsonElement wrapper, out JsonElement section)
    {
        if (wrapper.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in wrapper.EnumerateObject())
            {
                if (string.Equals(property.Name, SectionKey, StringComparison.OrdinalIgnoreCase))
                {
                    section = property.Value;
                    return true;
                }
            }
        }

        section = default;
        return false;
    }
}
